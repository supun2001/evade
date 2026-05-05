using UnityEngine;
using Colyseus;
using System.Collections.Generic;
using System.Threading.Tasks;
using Colyseus.Schema;
using System;
using System.Collections;
using UnityEngine.SceneManagement;

[Serializable]
public struct NextbotSpawnPointConfig
{
    public Transform anchor;
    public Vector3 position;

    public Vector3 GetWorldPosition()
    {
        return anchor != null ? anchor.position : position;
    }
}

[Serializable]
public struct PlayerSpawnPointConfig
{
    public Transform anchor;
    public Vector3 position;

    public Vector3 GetWorldPosition()
    {
        return anchor != null ? anchor.position : position;
    }
}

[Serializable]
public struct NextbotPatrolPointConfig
{
    public Transform anchor;
    public Vector3 position;

    public Vector3 GetWorldPosition()
    {
        return anchor != null ? anchor.position : position;
    }
}

[Serializable]
public struct ServerObstacleConfig
{
    public float minX;
    public float maxX;
    public float minY;
    public float maxY;
    public float minZ;
    public float maxZ;
}

[Serializable]
public struct FloorHeightSampleConfig
{
    public float x;
    public float y;
    public float z;
}

public class NetworkManager : MonoBehaviour
{
    private const string HostedServerUrl = "wss://wargrid.games";
    private const int PlayerUpdateFieldCount = 31;
    private const string WalkableLayerName = "Walkable";
    private const string RampLayerName = "Ramp";
    private const string RandomMapId = "random";
    private const string ClassicMapId = "SampleScene";
    private const string ClassicMapSceneName = "Classic";
    private const string BackroomMapId = "backroom";
    private const string BackroomMapSceneName = "backroom";
    private const string BrutilistVoidMapId = "brutilistVoid";
    private const string BrutilistVoidMapSceneName = "BrutalistVoid";
    private const string ParkourMapId = "parkour";
    private const string ParkourMapSceneName = "parkour";
    private const string VitaminBMapId = "Vitamin_B";
    private const string VitaminBMapSceneName = "Vitamin_B";
    private const string VillageMapId = "vilage";
    private const string VillageMapSceneName = "Village";
    private const float MinServerObstacleThickness = 0.25f;
    private const int MaxServerFloorSamples = 384;
    private const int MaxServerObstacles = 384;
    private const float MaxServerObstacleEnclosingSpan = 45f;
    private static bool s_multiplayerShootingPresentationEnabled;
    private static bool s_joinGameUsesShootingMode;

    public static NetworkManager Instance;
    
    [Header("Network Configuration")]
    [Tooltip("Local development server URL")]
    [SerializeField] private string localServerUrl = "ws://localhost:2567";

    [Tooltip("Production server URL (Render/Railway). Must use wss:// for WebGL.")]
    [SerializeField] private string productionServerUrl = HostedServerUrl;
    [Tooltip("Use the production URL even while running in the Unity Editor.")]
    [SerializeField] private bool useProductionServerInEditor = false;
    [Header("Gameplay Configuration")]
    [Tooltip("Optional matchmaking map id. Empty uses the active scene name.")]
    [SerializeField] private string mapIdOverride = "";
    [Tooltip("Server-authoritative nextbot spawn points used when this client creates the room.")]
    [SerializeField] private List<NextbotSpawnPointConfig> nextbotSpawnPoints = new()
    {
        new NextbotSpawnPointConfig { position = new Vector3(6.45f, 0f, -2.38f) },
        new NextbotSpawnPointConfig { position = new Vector3(-6.45f, 0f, 2.38f) },
        new NextbotSpawnPointConfig { position = new Vector3(0f, 0f, 7.5f) },
        new NextbotSpawnPointConfig { position = new Vector3(7.5f, 0f, 5.5f) },
        new NextbotSpawnPointConfig { position = new Vector3(-7.5f, 0f, -5.5f) },
    };
    [Tooltip("Server-authoritative player spawn points used for joins and round resets.")]
    [SerializeField] private List<PlayerSpawnPointConfig> playerSpawnPoints = new()
    {
        new PlayerSpawnPointConfig { position = new Vector3(0f, 0f, -6f) },
        new PlayerSpawnPointConfig { position = new Vector3(2f, 0f, -6f) },
        new PlayerSpawnPointConfig { position = new Vector3(-2f, 0f, -6f) },
    };
    [SerializeField] private float spawnGroundProbeHeight = 6f;
    [SerializeField] private float spawnGroundProbeDistance = 30f;
    [SerializeField] private float spawnGroundOffset = 0.15f;
    [SerializeField] private LayerMask spawnGroundLayers = ~0;
    [SerializeField] private float serverFloorSampleSpacing = 0.75f;
    [SerializeField] private float serverFloorSamplePadding = 2f;
    [Tooltip("Optional patrol hints for idle nextbots. Leave empty to auto-roam using the overall spawn area.")]
    [SerializeField] private List<NextbotPatrolPointConfig> nextbotPatrolPoints = new();
    [Header("Round Timing")]
    [Tooltip("How long the intermission lasts before the round starts.")]
    [SerializeField, Min(1f)] private float intermissionDurationSeconds = 20f;
    [Tooltip("How long the active survive round lasts.")]
    [SerializeField, Min(5f)] private float roundDurationSeconds = 180f;

    public string serverUrl 
    {
        get 
        {
            #if UNITY_EDITOR
                return useProductionServerInEditor ? productionServerUrl : localServerUrl;
            #else
                return productionServerUrl;
            #endif
        }
    }
    public string roomName = "my_room";
    public GameObject playerPrefab; 
    public string currentRoomId { get; private set; } 
    public string AuthenticatedUsername { get; set; } = string.Empty;

    private ColyseusClient client;
    private ColyseusRoom<MyRoomState> room;
    public ColyseusRoom<MyRoomState> Room => room;
    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> simulatedPlayers = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, Player> simulatedPlayerStates = new Dictionary<string, Player>();
    private readonly float[] playerUpdatePayload = new float[PlayerUpdateFieldCount];
    private string simulatedLocalSessionId = string.Empty;
    private string selectedMapId = string.Empty;
    private string activeServerMapId = string.Empty;
    private bool _isLoadingServerMap;
    private bool _awaitingInitialMapSelection;
    private Coroutine _serverMapLoadCoroutine;
    public GameObject PlayerPrefab => playerPrefab;

    private bool ShouldUseCompactPlayerUpdatePayload
    {
        get
        {
            try
            {
                Uri uri = BuildServerUri(serverUrl);
                return uri.IsLoopback;
            }
            catch
            {
                return false;
            }
        }
    }

    private void Awake() 
    { 
        if (Instance != null && Instance != this) 
        { 
            Instance.ApplySceneConfigurationFrom(this);
            Destroy(gameObject); 
            return; 
        } 
        Instance = this; 
        DontDestroyOnLoad(gameObject); 

        MigrateLegacyServerUrls();
        
        // Ensure the game keeps running and syncing when focus is lost (e.g., when testing multiple instances)
        Application.runInBackground = true;
    }

    private async void Start()
    {
        client = CreateClient();
    }

    public System.Action<string, Player> OnPlayerAddedEvent;
    public System.Action<string, Player> OnPlayerRemovedEvent;
    public event Action<RoundPhaseMessageData> RoundPhaseChanged;
    public event Action<RoundAnnouncementMessageData> RoundAnnouncementReceived;
    public event Action<RoundResultsMessageData> RoundResultsReceived;
    public event Action<MapVoteStateMessageData> MapVoteStateReceived;
    public event Action<MapSelectedMessageData> MapSelectedReceived;
    public event Action RoomLeftEvent;
    public string LocalSessionId => room != null ? room.SessionId : simulatedLocalSessionId;
    public string CurrentMapId => ResolveCurrentMapId();
    public Dictionary<string, Player> SimulatedPlayerStates => simulatedPlayerStates;
    public bool HasSimulatedPlayerStates => simulatedPlayerStates.Count > 0;
    public bool IsPreparingServerSelectedMap => _awaitingInitialMapSelection || _isLoadingServerMap;
    public int IntermissionDurationMilliseconds => IntermissionDurationMs;
    public int RoundDurationMilliseconds => RoundDurationMs;
    public bool IsMultiplayerShootingPresentationEnabled => s_multiplayerShootingPresentationEnabled;
    public bool JoinGameUsesShootingMode => s_joinGameUsesShootingMode;
    private bool _hasReceivedRoundPhaseFromServer;
    private Coroutine _fallbackRoundFlowCoroutine;
    private Coroutine _localRoundResetCoroutine;

    public void SetMultiplayerShootingPresentationEnabled(bool isEnabled)
    {
        s_multiplayerShootingPresentationEnabled = isEnabled;
    }

    public void SetSelectedMapId(string mapId)
    {
        selectedMapId = SanitizeMapId(mapId);
    }


    public void UseServerRandomMap()
    {
        selectedMapId = RandomMapId;
    }

    private string ResolveCurrentMapId()
    {
        if (!string.IsNullOrWhiteSpace(activeServerMapId))
        {
            return activeServerMapId;
        }

        if (!string.IsNullOrWhiteSpace(selectedMapId))
        {
            return selectedMapId;
        }

        return ResolveSceneMapId();
    }

    private string ResolveSceneMapId()
    {
        if (!string.IsNullOrWhiteSpace(mapIdOverride))
        {
            return mapIdOverride.Trim();
        }

        string sceneName = SceneManager.GetActiveScene().name;
        return GetMapIdForSceneName(sceneName);
    }

    private static string SanitizeMapId(string mapId)
    {
        return string.IsNullOrWhiteSpace(mapId) ? string.Empty : mapId.Trim();
    }

    private static bool IsKnownMapId(string mapId)
    {
        return string.Equals(mapId, ClassicMapId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapId, BackroomMapId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapId, BrutilistVoidMapId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapId, ParkourMapId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapId, VitaminBMapId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapId, VillageMapId, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSceneNameForMapId(string mapId)
    {
        if (string.Equals(mapId, BackroomMapId, StringComparison.OrdinalIgnoreCase))
        {
            return BackroomMapSceneName;
        }

        if (string.Equals(mapId, BrutilistVoidMapId, StringComparison.OrdinalIgnoreCase))
        {
            return BrutilistVoidMapSceneName;
        }

        if (string.Equals(mapId, ParkourMapId, StringComparison.OrdinalIgnoreCase))
        {
            return ParkourMapSceneName;
        }

        if (string.Equals(mapId, VitaminBMapId, StringComparison.OrdinalIgnoreCase))
        {
            return VitaminBMapSceneName;
        }

        if (string.Equals(mapId, VillageMapId, StringComparison.OrdinalIgnoreCase))
        {
            return VillageMapSceneName;
        }

        return ClassicMapSceneName;
    }

    private static string GetMapIdForSceneName(string sceneName)
    {
        if (string.Equals(sceneName, BackroomMapSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return BackroomMapId;
        }

        if (string.Equals(sceneName, BrutilistVoidMapSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return BrutilistVoidMapId;
        }

        if (string.Equals(sceneName, ParkourMapSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return ParkourMapId;
        }

        if (string.Equals(sceneName, VitaminBMapSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return VitaminBMapId;
        }

        if (string.Equals(sceneName, VillageMapSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return VillageMapId;
        }

        return string.IsNullOrWhiteSpace(sceneName) ? ClassicMapId : sceneName;
    }

    private void ApplySceneConfigurationFrom(NetworkManager sceneManager)
    {
        if (sceneManager == null || sceneManager == this)
        {
            return;
        }

        localServerUrl = sceneManager.localServerUrl;
        productionServerUrl = sceneManager.productionServerUrl;
        useProductionServerInEditor = sceneManager.useProductionServerInEditor;
        mapIdOverride = sceneManager.mapIdOverride;
        nextbotSpawnPoints = new List<NextbotSpawnPointConfig>(sceneManager.nextbotSpawnPoints);
        playerSpawnPoints = new List<PlayerSpawnPointConfig>(sceneManager.playerSpawnPoints);
        spawnGroundProbeHeight = sceneManager.spawnGroundProbeHeight;
        spawnGroundProbeDistance = sceneManager.spawnGroundProbeDistance;
        spawnGroundOffset = sceneManager.spawnGroundOffset;
        spawnGroundLayers = sceneManager.spawnGroundLayers;
        serverFloorSampleSpacing = sceneManager.serverFloorSampleSpacing;
        serverFloorSamplePadding = sceneManager.serverFloorSamplePadding;
        nextbotPatrolPoints = new List<NextbotPatrolPointConfig>(sceneManager.nextbotPatrolPoints);
        intermissionDurationSeconds = sceneManager.intermissionDurationSeconds;
        roundDurationSeconds = sceneManager.roundDurationSeconds;
        roomName = sceneManager.roomName;
        playerPrefab = sceneManager.playerPrefab;

        if (string.IsNullOrWhiteSpace(selectedMapId))
        {
            selectedMapId = sceneManager.ResolveSceneMapId();
        }

        if (room == null)
        {
            client = CreateClient();
        }
    }

    private void OnPlayerAdded(string id, Player player)
    {
        if (IsPreparingServerSelectedMap)
        {
            return;
        }

        if (players.ContainsKey(id))
        {
            return;
        }

        bool isLocal = id == room.SessionId;
        if (player != null && player.isSpectator && !isLocal)
        {
            return;
        }

        Debug.Log($"Player added: {id}");
        Vector3 pos = new Vector3(player.x, player.y, player.z);
        if (isLocal && TryGetLocalPlayerGroundedSpawnPoint(out Vector3 groundedLocalSpawn))
        {
            pos = groundedLocalSpawn;
        }
        else
        {
            pos = ResolveGroundedSpawnPosition(pos);
        }

        GameObject obj = Instantiate(playerPrefab, pos, Quaternion.identity);
        obj.name = isLocal ? "LocalPlayer" : $"RemotePlayer_{id}";

        NetworkPlayer np = obj.GetComponent<NetworkPlayer>();
        if (np == null) np = obj.AddComponent<NetworkPlayer>();
        np.Initialize(player, isLocal);

        PlayerAppearance appearance = obj.GetComponent<PlayerAppearance>();
        if (appearance != null)
        {
            appearance.Initialize(player);
        }

        if (!isLocal)
        {
            var camera = obj.GetComponentInChildren<Camera>();
            if (camera)
            {
                camera.gameObject.SetActive(false);
            }
            
             var audioListener = obj.GetComponentInChildren<AudioListener>();
            if (audioListener) audioListener.enabled = false;
        }

        players.Add(id, obj);
        
        OnPlayerAddedEvent?.Invoke(id, player);
    }

    private void OnPlayerRemoved(string id, Player player)
    {
        if (players.ContainsKey(id))
        {
            Destroy(players[id]);
            players.Remove(id);
        }
        OnPlayerRemovedEvent?.Invoke(id, player);
    }

    private void OnStateChange(MyRoomState state, bool isFirstState)
    {
        ReconcilePlayerRepresentations(state);
    }

    private void ReconcilePlayerRepresentations(MyRoomState state)
    {
        if (state?.players == null || room == null)
        {
            return;
        }

        List<string> existingSessionIds = new List<string>(players.Keys);
        for (int i = 0; i < existingSessionIds.Count; i++)
        {
            string sessionId = existingSessionIds[i];
            bool hasStatePlayer = state.players.TryGetValue(sessionId, out Player syncedPlayer) && syncedPlayer != null;
            bool isLocal = string.Equals(sessionId, room.SessionId, StringComparison.Ordinal);
            bool shouldExist = hasStatePlayer && (!syncedPlayer.isSpectator || isLocal);

            if (!shouldExist && players.TryGetValue(sessionId, out GameObject existingObject))
            {
                if (existingObject != null)
                {
                    Destroy(existingObject);
                }

                players.Remove(sessionId);
            }
        }

        foreach (string sessionId in state.players.Keys)
        {
            if (!state.players.TryGetValue(sessionId, out Player syncedPlayer) || syncedPlayer == null)
            {
                continue;
            }

            bool isLocal = string.Equals(sessionId, room.SessionId, StringComparison.Ordinal);
            if (syncedPlayer.isSpectator && !isLocal)
            {
                continue;
            }

            if (players.TryGetValue(sessionId, out GameObject existingPlayerObject) && existingPlayerObject == null)
            {
                players.Remove(sessionId);
            }

            if (!players.ContainsKey(sessionId))
            {
                OnPlayerAdded(sessionId, syncedPlayer);
            }
        }
    }

    public void SendPlayerUpdate(
        Vector3 pos,
        float rotY,
        Vector3 vel,
        float aX,
        float aY,
        bool g,
        bool j,
        bool injured,
        bool crouching,
        bool wallRunning,
        int wallRunSide,
        Vector2 moveInput,
        float visualYaw,
        Vector2 camRot,
        bool hitReacting,
        float hitReactionTimeRemaining,
        float hitReactionPitch,
        float hitReactionRoll,
        float hitReactionSeed,
        float speedBoostMultiplier,
        float speedBoostTimeRemaining,
        float jumpBoostMultiplier,
        float jumpBoostTimeRemaining,
        bool isShootingMode,
        float shotTriggerId)
    {
        if (room == null) return;

        if (!ShouldUseCompactPlayerUpdatePayload)
        {
            // Keep production / hosted servers on the legacy object payload until the
            // deployed server is updated to understand the compact array protocol.
            _ = room.Send("playerUpdate", new {
                x = pos.x, y = pos.y, z = pos.z,
                rotationY = rotY,
                velocityX = vel.x, velocityY = vel.y, velocityZ = vel.z,
                animInputX = aX, animInputY = aY,
                isGrounded = g, isJumping = j,
                isInjured = injured,
                isCrouching = crouching,
                isWallRunning = wallRunning,
                wallRunSide = wallRunSide,
                moveInputX = moveInput.x,
                moveInputY = moveInput.y,
                visualYaw = visualYaw,
                cameraRotationX = camRot.x, cameraRotationY = camRot.y,
                isHitReacting = hitReacting,
                hitReactionTimeRemaining = hitReactionTimeRemaining,
                hitReactionPitch = hitReactionPitch,
                hitReactionRoll = hitReactionRoll,
                hitReactionSeed = hitReactionSeed,
                speedBoostMultiplier = speedBoostMultiplier,
                speedBoostTimeRemaining = speedBoostTimeRemaining,
                jumpBoostMultiplier = jumpBoostMultiplier,
                jumpBoostTimeRemaining = jumpBoostTimeRemaining,
                isShootingMode = isShootingMode,
                shotTriggerId = shotTriggerId
            });
            return;
        }

        // Reuse a compact numeric payload to avoid per-send anonymous object allocations
        // and repeated serialization of property names on every network tick.
        playerUpdatePayload[0] = pos.x;
        playerUpdatePayload[1] = pos.y;
        playerUpdatePayload[2] = pos.z;
        playerUpdatePayload[3] = rotY;
        playerUpdatePayload[4] = vel.x;
        playerUpdatePayload[5] = vel.y;
        playerUpdatePayload[6] = vel.z;
        playerUpdatePayload[7] = aX;
        playerUpdatePayload[8] = aY;
        playerUpdatePayload[9] = g ? 1f : 0f;
        playerUpdatePayload[10] = j ? 1f : 0f;
        playerUpdatePayload[11] = injured ? 1f : 0f;
        playerUpdatePayload[12] = crouching ? 1f : 0f;
        playerUpdatePayload[13] = wallRunning ? 1f : 0f;
        playerUpdatePayload[14] = wallRunSide;
        playerUpdatePayload[15] = moveInput.x;
        playerUpdatePayload[16] = moveInput.y;
        playerUpdatePayload[17] = visualYaw;
        playerUpdatePayload[18] = camRot.x;
        playerUpdatePayload[19] = camRot.y;
        playerUpdatePayload[20] = hitReacting ? 1f : 0f;
        playerUpdatePayload[21] = hitReactionTimeRemaining;
        playerUpdatePayload[22] = hitReactionPitch;
        playerUpdatePayload[23] = hitReactionRoll;
        playerUpdatePayload[24] = hitReactionSeed;
        playerUpdatePayload[25] = speedBoostMultiplier;
        playerUpdatePayload[26] = speedBoostTimeRemaining;
        playerUpdatePayload[27] = jumpBoostMultiplier;
        playerUpdatePayload[28] = jumpBoostTimeRemaining;
        playerUpdatePayload[29] = isShootingMode ? 1f : 0f;
        playerUpdatePayload[30] = shotTriggerId;

        _ = room.Send("playerUpdate", playerUpdatePayload);
    }

    public void SendReadyState(bool isReady)
    {
        if (room == null) return;
        room.Send("playerReady", isReady);
    }

    public void SendSpectatorState(bool isSpectating)
    {
        if (room == null) return;
        room.Send("playerSpectating", isSpectating);
    }

    public void SendHazardElimination()
    {
        if (room == null)
        {
            return;
        }

        room.Send("hazardElimination");
    }

    public void SendReviveRequest(string targetSessionId)
    {
        if (room == null || string.IsNullOrWhiteSpace(targetSessionId))
        {
            return;
        }

        room.Send("revivePlayer", new
        {
            targetSessionId
        });
    }

    public void SendCarryRequest(string targetSessionId)
    {
        if (room == null || string.IsNullOrWhiteSpace(targetSessionId))
        {
            return;
        }

        room.Send("carryPlayer", new
        {
            targetSessionId
        });
    }

    public bool TryGetPlayerObject(string sessionId, out GameObject playerObject)
    {
        if (players.TryGetValue(sessionId, out playerObject))
        {
            return true;
        }

        return simulatedPlayers.TryGetValue(sessionId, out playerObject);
    }

    public void RegisterSimulatedPlayerObject(string sessionId, GameObject playerObject)
    {
        RegisterSimulatedPlayerObject(sessionId, playerObject, null);
    }

    public void RegisterSimulatedPlayerObject(string sessionId, GameObject playerObject, Player simulatedPlayerState)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || playerObject == null)
        {
            return;
        }

        simulatedPlayers[sessionId] = playerObject;
        if (simulatedPlayerState != null)
        {
            simulatedPlayerStates[sessionId] = simulatedPlayerState;
        }
    }

    public void UnregisterSimulatedPlayerObject(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        simulatedPlayers.Remove(sessionId);
        simulatedPlayerStates.Remove(sessionId);

        if (string.Equals(simulatedLocalSessionId, sessionId, StringComparison.Ordinal))
        {
            simulatedLocalSessionId = string.Empty;
        }
    }

    public void SetSimulatedLocalSessionId(string sessionId)
    {
        simulatedLocalSessionId = sessionId ?? string.Empty;
    }

    public void PublishSimulatedRoundPhase(RoundPhaseMessageData payload)
    {
        if (payload == null)
        {
            return;
        }

        RoundPhaseChanged?.Invoke(payload);
    }

    public void PublishSimulatedRoundAnnouncement(RoundAnnouncementMessageData payload)
    {
        if (payload == null)
        {
            return;
        }

        RoundAnnouncementReceived?.Invoke(payload);
    }

    public void PublishSimulatedRoundResults(RoundResultsMessageData payload)
    {
        if (payload == null)
        {
            return;
        }

        RoundResultsReceived?.Invoke(payload);
    }

    public void PublishSimulatedRoomLeft()
    {
        RoomLeftEvent?.Invoke();
    }

    public void PublishSimulatedMapVoteState(MapVoteStateMessageData payload)
    {
        MapVoteStateReceived?.Invoke(payload);
    }

    public void PublishSimulatedMapSelected(MapSelectedMessageData payload)
    {
        HandleServerMapSelected(payload);
        MapSelectedReceived?.Invoke(payload);
    }

    public List<Vector3> GetConfiguredPlayerSpawnPositions()
    {
        List<Vector3> spawnPositions = new List<Vector3>(playerSpawnPoints.Count);
        for (int i = 0; i < playerSpawnPoints.Count; i++)
        {
            spawnPositions.Add(ResolveGroundedSpawnPosition(playerSpawnPoints[i].GetWorldPosition()));
        }

        return spawnPositions;
    }

    public List<Vector3> GetConfiguredNextbotSpawnPositions()
    {
        List<Vector3> spawnPositions = new List<Vector3>(nextbotSpawnPoints.Count);
        for (int i = 0; i < nextbotSpawnPoints.Count; i++)
        {
            spawnPositions.Add(ResolveGroundedSpawnPosition(nextbotSpawnPoints[i].GetWorldPosition()));
        }

        return spawnPositions;
    }

    public void SendNextbotUpdate(int nextbotId, Vector3 position, float rotationY)
    {
        if (room == null || !room.Connection.IsOpen)
        {
            return;
        }

        room.Send("nextbotUpdate", new {
            id = nextbotId,
            x = position.x,
            y = position.y,
            z = position.z,
            rotationY = rotationY
        });
    }

    public void SendNextbotHit(string nextbotId, Vector3 hitSourcePosition)
    {
        if (room == null || !room.Connection.IsOpen)
        {
            return;
        }

        room.Send("nextbotHit", new {
            id = nextbotId,
            x = hitSourcePosition.x,
            y = hitSourcePosition.y,
            z = hitSourcePosition.z,
        });
    }

    public void SendCombatHitPlayer(string targetSessionId, float damage)
    {
        if (room == null || !room.Connection.IsOpen || string.IsNullOrWhiteSpace(targetSessionId) || damage <= 0f)
        {
            return;
        }

        room.Send("combatHitPlayer", new {
            targetSessionId,
            damage,
        });
    }

    public void SendCombatHitNextbot(string nextbotId, float damage)
    {
        if (room == null || !room.Connection.IsOpen || string.IsNullOrWhiteSpace(nextbotId) || damage <= 0f)
        {
            return;
        }

        room.Send("combatHitNextbot", new {
            id = nextbotId,
            damage,
        });
    }

    public List<Vector3> GetConfiguredNextbotPatrolPositions()
    {
        List<Vector3> patrolPositions = new List<Vector3>(nextbotPatrolPoints.Count);
        for (int i = 0; i < nextbotPatrolPoints.Count; i++)
        {
            patrolPositions.Add(ResolveGroundedSpawnPosition(nextbotPatrolPoints[i].GetWorldPosition()));
        }

        return patrolPositions;
    }

    public async Task<string> CreateGame(){
        InitializeClient();
        try{
            room = await client.Create<MyRoomState>(roomName, BuildJoinRoomOptions());
            OnRoomJoined();
            return null; // Success

        }catch(System.Exception e){
            Debug.LogError($"Matchmaking Failed: {e.Message}");
            return e.Message;
        }
    }

    public async Task<string> JoinOrCreateGame()
    {
        InitializeClient();
        s_joinGameUsesShootingMode = false;
        try
        {
            room = await client.JoinOrCreate<MyRoomState>(roomName, BuildJoinRoomOptions());
            OnRoomJoined();
            return null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"JoinOrCreate Failed: {e.Message}");
            return e.Message;
        }
    }

    public async Task<string> JoinGame(string targetRoomId)
    {
        InitializeClient();
        SetMultiplayerShootingPresentationEnabled(true);
        s_joinGameUsesShootingMode = true;
        try
        {
            room = await client.JoinById<MyRoomState>(targetRoomId, BuildJoinRoomOptions());
            OnRoomJoined();
            return null; // Success
        }
        catch (System.Exception e) 
        { 
            Debug.LogError($"Join Failed: {e.Message}"); 
            return e.Message;
        }
    }

    private void InitializeClient()
    {
        if (client == null) client = CreateClient();
    }

    private Dictionary<string, object> BuildJoinRoomOptions()
    {
        return new Dictionary<string, object>
        {
            ["mapId"] = ResolveCurrentMapId(),
            ["username"] = string.IsNullOrWhiteSpace(AuthenticatedUsername) ? "Player" : AuthenticatedUsername,
            ["playerSpawnPoints"] = BuildSerializedPlayerSpawnPoints(),
        };
    }

    private List<object> BuildSerializedPlayerSpawnPoints()
    {
        AutoDiscoverSpawnPoints();
        List<object> serializedPlayerSpawnPoints = new List<object>();
        for (int i = 0; i < playerSpawnPoints.Count; i++)
        {
            Vector3 point = playerSpawnPoints[i].GetWorldPosition();
            Vector3 groundedPoint = ResolveGroundedSpawnPosition(point);
            serializedPlayerSpawnPoints.Add(new Dictionary<string, object>
            {
                ["x"] = groundedPoint.x,
                ["y"] = groundedPoint.y,
                ["z"] = groundedPoint.z,
            });
        }
        return serializedPlayerSpawnPoints;
    }

    private Dictionary<string, object> BuildMapSyncPayload()
    {
        AutoDiscoverSpawnPoints();
        List<object> serializedSpawnPoints = new List<object>();
        for (int i = 0; i < nextbotSpawnPoints.Count; i++)
        {
            Vector3 point = nextbotSpawnPoints[i].GetWorldPosition();
            Vector3 groundedPoint = ResolveGroundedSpawnPosition(point);
            serializedSpawnPoints.Add(new Dictionary<string, object>
            {
                ["x"] = groundedPoint.x,
                ["y"] = groundedPoint.y,
                ["z"] = groundedPoint.z,
            });
        }

        List<object> serializedPlayerSpawnPoints = new List<object>();
        for (int i = 0; i < playerSpawnPoints.Count; i++)
        {
            Vector3 point = playerSpawnPoints[i].GetWorldPosition();
            Vector3 groundedPoint = ResolveGroundedSpawnPosition(point);
            serializedPlayerSpawnPoints.Add(new Dictionary<string, object>
            {
                ["x"] = groundedPoint.x,
                ["y"] = groundedPoint.y,
                ["z"] = groundedPoint.z,
            });
        }

        List<object> serializedNextbotPatrolPoints = new List<object>();
        for (int i = 0; i < nextbotPatrolPoints.Count; i++)
        {
            Vector3 point = nextbotPatrolPoints[i].GetWorldPosition();
            Vector3 groundedPoint = ResolveGroundedSpawnPosition(point);
            serializedNextbotPatrolPoints.Add(new Dictionary<string, object>
            {
                ["x"] = groundedPoint.x,
                ["y"] = groundedPoint.y,
                ["z"] = groundedPoint.z,
            });
        }

        List<object> serializedNextbotIds = BuildSerializedNextbotIds();
        if (serializedNextbotIds.Count == 0)
        {
            Debug.LogWarning("NetworkManager: No nextbot IDs found in registry! Adding fallback 'angry_munci'.");
            serializedNextbotIds.Add("angry_munci");
        }

        List<object> serializedNextbotConfigs = BuildSerializedNextbotConfigs();
        List<object> serializedObstacles = BuildSerializedObstacleBounds();
        List<object> serializedFloorSamples = BuildSerializedFloorSamples(nextbotSpawnPoints, playerSpawnPoints, nextbotPatrolPoints);
        Debug.Log($"NetworkManager: syncing map {ResolveCurrentMapId()} with {serializedSpawnPoints.Count} spawn points, {serializedNextbotIds.Count} bots, {serializedObstacles.Count} obstacles and {serializedFloorSamples.Count} floor samples.");

        return new Dictionary<string, object>
        {
            ["nextbotSpawnPoints"] = serializedSpawnPoints,
            ["nextbotPatrolPoints"] = serializedNextbotPatrolPoints,
            ["nextbotIds"] = serializedNextbotIds,
            ["nextbotConfigs"] = serializedNextbotConfigs,
            ["nextbotObstacles"] = serializedObstacles,
            ["nextbotFloorSamples"] = serializedFloorSamples,
            ["playerSpawnPoints"] = serializedPlayerSpawnPoints,
            ["intermissionDurationMs"] = IntermissionDurationMs,
            ["roundDurationMs"] = RoundDurationMs,
            ["mapId"] = ResolveCurrentMapId(),
        };
    }

    private static List<object> BuildSerializedObstacleBounds()
    {
        List<object> serializedObstacles = new List<object>();
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        int walkableLayer = LayerMask.NameToLayer("Walkable");
        int rampLayer = LayerMask.NameToLayer("Ramp");

        for (int i = 0; i < colliders.Length; i++)
        {
            if (serializedObstacles.Count >= MaxServerObstacles)
            {
                break;
            }

            Collider collider = colliders[i];
            if (!ShouldIncludeServerObstacle(collider, walkableLayer, rampLayer))
            {
                continue;
            }

            Bounds bounds = ExpandThinServerObstacleBounds(collider.bounds);
            serializedObstacles.Add(new Dictionary<string, object>
            {
                ["minX"] = bounds.min.x,
                ["maxX"] = bounds.max.x,
                ["minY"] = bounds.min.y,
                ["maxY"] = bounds.max.y,
                ["minZ"] = bounds.min.z,
                ["maxZ"] = bounds.max.z,
            });
        }

        return serializedObstacles;
    }

    private static Bounds ExpandThinServerObstacleBounds(Bounds bounds)
    {
        Vector3 size = bounds.size;
        if (size.x < MinServerObstacleThickness)
        {
            size.x = MinServerObstacleThickness;
        }

        if (size.z < MinServerObstacleThickness)
        {
            size.z = MinServerObstacleThickness;
        }

        bounds.size = size;
        return bounds;
    }

    private static bool ShouldIncludeServerObstacle(Collider collider, int walkableLayer, int rampLayer)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger)
        {
            return false;
        }

        if (rampLayer >= 0 && collider.gameObject.layer == rampLayer)
        {
            return false;
        }

        if (collider.GetComponentInParent<PlayerController>() != null
            || collider.GetComponentInParent<NextbotFollowPlayer>() != null
            || collider.GetComponentInParent<SpeedBoostPickup>() != null
            || collider.GetComponentInParent<JumpBoostPickup>() != null)
        {
            return false;
        }

        Bounds bounds = collider.bounds;
        bool hasHorizontalBlockerExtent = bounds.size.x > 0.1f || bounds.size.z > 0.1f;
        if (!hasHorizontalBlockerExtent || bounds.size.y <= 0.25f)
        {
            return false;
        }

        bool isFlatWalkableSurface = bounds.size.y <= 1.5f;
        if (walkableLayer >= 0 && collider.gameObject.layer == walkableLayer && isFlatWalkableSurface)
        {
            return false;
        }

        // Skip very large flat surfaces like ground planes; the server only needs blockers.
        if (isFlatWalkableSurface && (bounds.size.x >= 25f || bounds.size.z >= 25f))
        {
            return false;
        }

        bool isLargeEnclosingBounds = bounds.size.x >= MaxServerObstacleEnclosingSpan
            && bounds.size.z >= MaxServerObstacleEnclosingSpan;
        if (isLargeEnclosingBounds)
        {
            return false;
        }

        return true;
    }

    private List<object> BuildSerializedFloorSamples(
        List<NextbotSpawnPointConfig> nextbotSpawnConfigs,
        List<PlayerSpawnPointConfig> playerSpawnConfigs,
        List<NextbotPatrolPointConfig> patrolConfigs)
    {
        List<Vector3> areaPoints = new List<Vector3>();
        for (int i = 0; i < nextbotSpawnConfigs.Count; i++)
        {
            areaPoints.Add(nextbotSpawnConfigs[i].GetWorldPosition());
        }

        for (int i = 0; i < playerSpawnConfigs.Count; i++)
        {
            areaPoints.Add(playerSpawnConfigs[i].GetWorldPosition());
        }

        for (int i = 0; i < patrolConfigs.Count; i++)
        {
            areaPoints.Add(patrolConfigs[i].GetWorldPosition());
        }

        List<object> serializedSamples = new List<object>();
        if (areaPoints.Count == 0)
        {
            return serializedSamples;
        }

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;
        for (int i = 0; i < areaPoints.Count; i++)
        {
            Vector3 point = areaPoints[i];
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minZ = Mathf.Min(minZ, point.z);
            maxZ = Mathf.Max(maxZ, point.z);
        }

        float spacing = Mathf.Max(0.75f, serverFloorSampleSpacing);
        minX -= serverFloorSamplePadding;
        maxX += serverFloorSamplePadding;
        minZ -= serverFloorSamplePadding;
        maxZ += serverFloorSamplePadding;

        float width = Mathf.Max(0f, maxX - minX);
        float depth = Mathf.Max(0f, maxZ - minZ);
        int estimatedColumnCount = Mathf.Max(1, Mathf.CeilToInt(width / spacing) + 1);
        int estimatedRowCount = Mathf.Max(1, Mathf.CeilToInt(depth / spacing) + 1);
        long estimatedSampleCount = (long)estimatedColumnCount * estimatedRowCount;
        int maxGridSamples = Mathf.Max(1, MaxServerFloorSamples - areaPoints.Count);
        if (estimatedSampleCount > maxGridSamples)
        {
            float sampleArea = Mathf.Max(width * depth, spacing * spacing);
            float adjustedSpacing = Mathf.Sqrt(sampleArea / maxGridSamples);
            spacing = Mathf.Max(spacing, adjustedSpacing);
            Debug.LogWarning($"NetworkManager: reduced server floor samples from about {estimatedSampleCount} to {maxGridSamples} for map {ResolveCurrentMapId()} using {spacing:0.##}m spacing.");
        }

        float referenceY = 0f;
        if (playerSpawnPoints.Count > 0)
        {
            float totalY = 0;
            foreach (var p in playerSpawnPoints) totalY += p.GetWorldPosition().y;
            referenceY = totalY / playerSpawnPoints.Count;
        }

        HashSet<string> dedup = new HashSet<string>();
        for (float x = minX; x <= maxX + 0.01f; x += spacing)
        {
            for (float z = minZ; z <= maxZ + 0.01f; z += spacing)
            {
                if (serializedSamples.Count >= maxGridSamples)
                {
                    break;
                }

                Vector3 grounded = ResolveGroundedSpawnPosition(new Vector3(x, referenceY, z));
                string key = $"{Mathf.RoundToInt(grounded.x * 100f)}:{Mathf.RoundToInt(grounded.z * 100f)}";
                if (!dedup.Add(key))
                {
                    continue;
                }

                serializedSamples.Add(new Dictionary<string, object>
                {
                    ["x"] = grounded.x,
                    ["y"] = grounded.y,
                    ["z"] = grounded.z,
                });
            }

            if (serializedSamples.Count >= maxGridSamples)
            {
                break;
            }
        }

        for (int i = 0; i < areaPoints.Count; i++)
        {
            if (serializedSamples.Count >= MaxServerFloorSamples)
            {
                break;
            }

            Vector3 grounded = ResolveGroundedSpawnPosition(areaPoints[i]);
            string key = $"{Mathf.RoundToInt(grounded.x * 100f)}:{Mathf.RoundToInt(grounded.z * 100f)}";
            if (!dedup.Add(key))
            {
                continue;
            }

            serializedSamples.Add(new Dictionary<string, object>
            {
                ["x"] = grounded.x,
                ["y"] = grounded.y,
                ["z"] = grounded.z,
            });
        }

        return serializedSamples;
    }

    private static List<object> BuildSerializedNextbotIds()
    {
        List<object> serializedNextbotIds = new List<object>();
        NextbotRegistry registry = Resources.Load<NextbotRegistry>("NextbotRegistry");
        if (registry == null || registry.entries == null)
        {
            return serializedNextbotIds;
        }

        for (int i = 0; i < registry.entries.Length; i++)
        {
            NextbotRegistryEntry entry = registry.entries[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.nextbotId))
            {
                serializedNextbotIds.Add(entry.nextbotId);
            }
        }

        return serializedNextbotIds;
    }

    private static List<object> BuildSerializedNextbotConfigs()
    {
        List<object> serializedNextbotConfigs = new List<object>();
        NextbotRegistry registry = Resources.Load<NextbotRegistry>("NextbotRegistry");
        if (registry == null || registry.entries == null)
        {
            return serializedNextbotConfigs;
        }

        for (int i = 0; i < registry.entries.Length; i++)
        {
            NextbotRegistryEntry entry = registry.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.nextbotId))
            {
                continue;
            }

            serializedNextbotConfigs.Add(new Dictionary<string, object>
            {
                ["id"] = entry.nextbotId,
                ["speed"] = entry.speed,
            });
        }

        return serializedNextbotConfigs;
    }

    private int IntermissionDurationMs => Mathf.Max(1000, Mathf.RoundToInt(intermissionDurationSeconds * 1000f));
    private int RoundDurationMs => Mathf.Max(5000, Mathf.RoundToInt(roundDurationSeconds * 1000f));

    public bool TryGetLocalPlayerSpawnPoint(out Vector3 spawnPosition)
    {
        spawnPosition = Vector3.zero;
        if (playerSpawnPoints == null || playerSpawnPoints.Count == 0 || string.IsNullOrEmpty(LocalSessionId))
        {
            return false;
        }

        int spawnIndex = GetStableSpawnIndex(LocalSessionId, playerSpawnPoints.Count);
        spawnPosition = ResolveGroundedSpawnPosition(playerSpawnPoints[spawnIndex].GetWorldPosition());
        return true;
    }

    public bool TryGetLocalPlayerGroundedSpawnPoint(out Vector3 spawnPosition)
    {
        spawnPosition = Vector3.zero;
        if (playerSpawnPoints == null || playerSpawnPoints.Count == 0 || string.IsNullOrEmpty(LocalSessionId))
        {
            return false;
        }

        int spawnIndex = GetStableSpawnIndex(LocalSessionId, playerSpawnPoints.Count);
        return TryResolveGroundedSpawnPosition(playerSpawnPoints[spawnIndex].GetWorldPosition(), out spawnPosition);
    }

    private Vector3 ResolveGroundedSpawnPosition(Vector3 desiredPosition)
    {
        if (TryResolveGroundedSpawnPosition(desiredPosition, out Vector3 resolvedPosition))
        {
            return resolvedPosition;
        }

        return desiredPosition;
    }

    private bool TryResolveGroundedSpawnPosition(Vector3 desiredPosition, out Vector3 resolvedPosition)
    {
        resolvedPosition = desiredPosition;
        // Start raycast only 2 meters above the desired position to avoid hitting roofs or skyboxes
        // that might be present high above the spawn point.
        Vector3 rayOrigin = desiredPosition + Vector3.up * 2f;
        float rayDistance = 40f; 
        int layerMask = GetSpawnGroundLayerMask();
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, rayDistance, layerMask, QueryTriggerInteraction.Ignore);

        if (hits.Length == 0)
        {
            int fallbackLayerMask = GetFallbackSpawnGroundLayerMask(layerMask);
            if (fallbackLayerMask != layerMask)
            {
                hits = Physics.RaycastAll(rayOrigin, Vector3.down, rayDistance, fallbackLayerMask, QueryTriggerInteraction.Ignore);
            }
        }

        if (hits.Length > 0)
        {
            int walkableLayer = LayerMask.NameToLayer(WalkableLayerName);
            RaycastHit bestHit = default;
            bool foundHit = false;
            bool foundWalkableHit = false;
            float bestDistanceToY = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                bool isWalkable = walkableLayer >= 0 && hit.collider.gameObject.layer == walkableLayer;
                float distanceToY = Mathf.Abs(hit.point.y - desiredPosition.y);

                if (isWalkable && !foundWalkableHit)
                {
                    bestHit = hit;
                    bestDistanceToY = distanceToY;
                    foundWalkableHit = true;
                    foundHit = true;
                }
                else if (isWalkable && foundWalkableHit)
                {
                    if (distanceToY < bestDistanceToY)
                    {
                        bestHit = hit;
                        bestDistanceToY = distanceToY;
                    }
                }
                else if (!foundWalkableHit)
                {
                    if (distanceToY < bestDistanceToY)
                    {
                        bestHit = hit;
                        bestDistanceToY = distanceToY;
                        foundHit = true;
                    }
                }
            }

            if (foundHit)
            {
                resolvedPosition = bestHit.point + Vector3.up * spawnGroundOffset;
                return true;
            }
        }

        return false;
    }

    private int GetFallbackSpawnGroundLayerMask(int primaryLayerMask)
    {
        int configuredMask = spawnGroundLayers.value;
        int fallbackMask = configuredMask != 0 ? configuredMask : ~0;
        return fallbackMask == primaryLayerMask ? primaryLayerMask : fallbackMask;
    }

    private int GetSpawnGroundLayerMask()
    {
        int configuredMask = spawnGroundLayers.value;
        int walkableLayer = LayerMask.NameToLayer(WalkableLayerName);
        int rampLayer = LayerMask.NameToLayer(RampLayerName);
        int preferredGroundMask = 0;
        if (walkableLayer >= 0)
        {
            preferredGroundMask |= 1 << walkableLayer;
        }

        if (rampLayer >= 0)
        {
            preferredGroundMask |= 1 << rampLayer;
        }

        if (preferredGroundMask != 0)
        {
            bool usesEverything = configuredMask == ~0;
            bool usesNothing = configuredMask == 0;
            if (usesEverything || usesNothing)
            {
                return preferredGroundMask;
            }
        }

        return configuredMask != 0 ? configuredMask : ~0;
    }

    private static int GetStableSpawnIndex(string sessionId, int spawnPointCount)
    {
        if (spawnPointCount <= 0)
        {
            return 0;
        }

        int hash = 0;
        for (int i = 0; i < sessionId.Length; i++)
        {
            hash = unchecked((hash * 31) + sessionId[i]);
        }

        int normalizedIndex = hash % spawnPointCount;
        return normalizedIndex < 0 ? normalizedIndex + spawnPointCount : normalizedIndex;
    }

    private ColyseusClient CreateClient()
    {
        Uri uri = BuildServerUri(serverUrl);
        bool secure = string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase);

        ColyseusSettings settings = ScriptableObject.CreateInstance<ColyseusSettings>();
        settings.colyseusServerAddress = uri.Host;
        settings.colyseusServerPort = uri.IsDefaultPort
            ? (secure ? "443" : "80")
            : uri.Port.ToString();
        settings.useSecureProtocol = secure;

        Debug.Log($"Connecting to Colyseus: {(secure ? "secure" : "insecure")}://{settings.colyseusServerAddress}:{settings.colyseusServerPort}");
        return new ColyseusClient(settings);
    }

    private void MigrateLegacyServerUrls()
    {
        if (IsLegacyHostedUrl(localServerUrl))
        {
            localServerUrl = HostedServerUrl;
        }

        if (IsLegacyHostedUrl(productionServerUrl) || string.IsNullOrWhiteSpace(productionServerUrl))
        {
            productionServerUrl = HostedServerUrl;
        }
    }

    private bool IsLegacyHostedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return url.Contains("azurewebsites.net", StringComparison.OrdinalIgnoreCase)
            || url.Contains("unity6-demo-mp.onrender.com", StringComparison.OrdinalIgnoreCase);
    }

    private Uri BuildServerUri(string rawServerUrl)
    {
        string candidate = (rawServerUrl ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(candidate))
        {
            candidate = "ws://localhost:2567";
        }

        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = $"ws://{candidate}";
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri uri))
        {
            throw new InvalidOperationException($"Invalid Colyseus server URL: {rawServerUrl}");
        }

        return uri;
    }

    private void OnRoomJoined()
    {
        currentRoomId = room.RoomId;
        _awaitingInitialMapSelection = true;
        _hasReceivedRoundPhaseFromServer = false;
        Debug.Log($"Connected! Room ID: {currentRoomId}");

        // Setup Handlers 
        room.OnStateChange += OnStateChange;
        room.OnLeave += (code) =>
        {
            Debug.LogWarning($"Disconnected from room {currentRoomId} with code {code}.");
            CleanupRoomState();
        };
        room.OnError += (code, message) =>
        {
            Debug.LogWarning($"Room error {code}: {message}");
        };
        
        // Listen for Start Game
        room.OnMessage<string>("startGame", (message) => {
            Debug.Log("Game Started!");
            
            // Notify LobbyUI to hide HUD
            LobbyUI lobby = FindObjectOfType<LobbyUI>();
            if (lobby != null)
            {
                lobby.HandleStartGameSignal();
            }

            if (!_hasReceivedRoundPhaseFromServer)
            {
                RestartFallbackRoundFlow();
                RoundPhaseChanged?.Invoke(new RoundPhaseMessageData
                {
                    phase = "intermission",
                    roundIndex = 0,
                    timeRemainingMs = IntermissionDurationMs,
                    roundDurationMs = RoundDurationMs,
                    intermissionDurationMs = IntermissionDurationMs,
                    isMapVoteOpen = false,
                });
            }
        });

        room.OnMessage<string>("playerRevived", (_) =>
        {
            if (!players.TryGetValue(room.SessionId, out GameObject localPlayer) || localPlayer == null)
            {
                return;
            }

            PlayerController controller = localPlayer.GetComponent<PlayerController>();
            controller?.ApplyNetworkRevive();
        });

        room.OnMessage<string>("playerRespawnCountdown", (json) =>
        {
            PlayerRespawnCountdownMessageData payload = ParseJsonMessage<PlayerRespawnCountdownMessageData>(json);
            if (payload == null
                || !players.TryGetValue(room.SessionId, out GameObject localPlayer)
                || localPlayer == null)
            {
                return;
            }

            NetworkPlayer networkPlayer = localPlayer.GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                networkPlayer.BeginRespawnCountdown(payload.durationMs / 1000f);
            }
        });

        room.OnMessage<string>("roundPlayerReset", (json) =>
        {
            RoundPlayerResetMessageData payload = ParseJsonMessage<RoundPlayerResetMessageData>(json);
            if (payload != null
                && players.TryGetValue(room.SessionId, out GameObject localPlayer)
                && localPlayer != null)
            {
                NetworkPlayer networkPlayer = localPlayer.GetComponent<NetworkPlayer>();
                if (networkPlayer != null)
                {
                    networkPlayer.ApplyImmediateRoundReset(
                        new Vector3(payload.x, payload.y, payload.z),
                        payload.rotationY);
                }
            }

            RestartLocalRoundResetCoroutine();
        });

        room.OnMessage<string>("roundPhase", (json) =>
        {
            RoundPhaseMessageData payload = ParseJsonMessage<RoundPhaseMessageData>(json);
            if (payload != null)
            {
                _hasReceivedRoundPhaseFromServer = true;
                StopFallbackRoundFlow();
                if (string.Equals(payload.phase, "intermission", StringComparison.Ordinal))
                {
                    RestartLocalRoundResetCoroutine();
                    if (payload.roundIndex > 0)
                    {
                        RequestMapVoteState();
                    }
                }
                RoundPhaseChanged?.Invoke(payload);
            }
        });

        room.OnMessage<string>("roundAnnouncement", (json) =>
        {
            RoundAnnouncementMessageData payload = ParseJsonMessage<RoundAnnouncementMessageData>(json);
            if (payload != null)
            {
                RoundAnnouncementReceived?.Invoke(payload);
            }
        });

        room.OnMessage<string>("roundResults", (json) =>
        {
            RoundResultsMessageData payload = ParseJsonMessage<RoundResultsMessageData>(json);
            if (payload != null)
            {
                RestartLocalRoundResetCoroutine();
                RoundResultsReceived?.Invoke(payload);
            }
        });

        room.OnMessage<string>("mapVoteState", (json) =>
        {
            MapVoteStateMessageData payload = ParseJsonMessage<MapVoteStateMessageData>(json);
            if (payload != null)
            {
                MapVoteStateReceived?.Invoke(payload);
            }
        });

        room.OnMessage<string>("mapSelected", (json) =>
        {
            MapSelectedMessageData payload = ParseJsonMessage<MapSelectedMessageData>(json);
            if (payload != null)
            {
                HandleServerMapSelected(payload);
                MapSelectedReceived?.Invoke(payload);
            }
        });

        RequestCurrentMapSelection();
        RequestRoundPhaseState();
        RequestMapVoteState();

        // Safety timeout: if the server's mapSelected response never arrives (e.g. network hiccup),
        // clear the flag after 5 seconds so the player is not permanently blocked from entering.
        StartCoroutine(ClearMapSelectionTimeoutSafety());


        var events = Colyseus.Schema.Callbacks.Get(room);
        events.OnAdd(state => state.players, (key, player) => OnPlayerAdded(key, player));
        events.OnRemove(state => state.players, (key, player) => OnPlayerRemoved(key, player));
    }

    public void SendMapVote(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId))
        {
            return;
        }

        if (room != null)
        {
            room.Send("voteMap", mapId.Trim());
            return;
        }

        if (OfflineModeManager.TryGetExisting(out OfflineModeManager manager) && manager.IsOfflineModeActive)
        {
            manager.SubmitOfflineMapVote(mapId.Trim());
        }
    }

    public void RequestMapVoteState()
    {
        if (room == null)
        {
            return;
        }

        room.Send("requestMapVoteState");
    }

    public void RequestCurrentMapSelection()
    {
        if (room == null)
        {
            return;
        }

        room.Send("requestMapSelected");
    }

    public void RequestRoundPhaseState()
    {
        if (room == null)
        {
            return;
        }

        room.Send("requestRoundPhase");
    }

    public void SyncCurrentMapConfiguration()
    {
        if (room == null)
        {
            return;
        }

        room.Send("syncMapConfig", BuildMapSyncPayload());
    }

    private IEnumerator ClearMapSelectionTimeoutSafety()
    {
        yield return new WaitForSeconds(5f);

        if (_awaitingInitialMapSelection)
        {
            Debug.LogWarning("NetworkManager: mapSelected response timed out — clearing flag and proceeding.");
            _awaitingInitialMapSelection = false;
            SyncCurrentMapConfiguration();
            ReconcilePlayerRepresentations(room?.State);
        }
    }

    private void HandleServerMapSelected(MapSelectedMessageData message)
    {
        if (message == null)
        {
            return;
        }

        string mapId = SanitizeMapId(message.mapId);
        if (!IsKnownMapId(mapId))
        {
            _awaitingInitialMapSelection = false;
            ReconcilePlayerRepresentations(room?.State);
            return;
        }

        _awaitingInitialMapSelection = false;
        activeServerMapId = mapId;
        selectedMapId = mapId;

        string sceneName = string.IsNullOrWhiteSpace(message.sceneName)
            ? GetSceneNameForMapId(mapId)
            : message.sceneName.Trim();
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            ReconcilePlayerRepresentations(room?.State);
            return;
        }

        if (string.Equals(SceneManager.GetActiveScene().name, sceneName, StringComparison.Ordinal))
        {
            SyncCurrentMapConfiguration();
            ReconcilePlayerRepresentations(room?.State);
            RestartLocalRoundResetCoroutine();
            return;
        }

        if (_serverMapLoadCoroutine != null)
        {
            StopCoroutine(_serverMapLoadCoroutine);
        }

        _serverMapLoadCoroutine = StartCoroutine(LoadServerSelectedMap(sceneName, mapId));
    }

    private IEnumerator LoadServerSelectedMap(string sceneName, string mapId)
    {
        _isLoadingServerMap = true;
        ClearSpawnedPlayerObjects();

        AsyncOperation loadOperation = null;
        Exception loadException = null;
        try
        {
            loadOperation = SceneManager.LoadSceneAsync(sceneName);
        }
        catch (Exception exception)
        {
            loadException = exception;
        }

        if (loadException != null || loadOperation == null)
        {
            Debug.LogError(loadException != null
                ? $"NetworkManager: failed to load server-selected map '{sceneName}': {loadException.Message}"
                : $"NetworkManager: server-selected map '{sceneName}' is not in Build Settings.");
            _isLoadingServerMap = false;
            _serverMapLoadCoroutine = null;
            ReconcilePlayerRepresentations(room?.State);
            yield break;
        }

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        // Let the newly loaded scene finish bringing colliders and spawn anchors online
        // before we probe for ground and rebuild the local player objects.
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();

        activeServerMapId = mapId;
        selectedMapId = mapId;
        SyncCurrentMapConfiguration();
        ReconcilePlayerRepresentations(room?.State);
        RestartLocalRoundResetCoroutine();
        _isLoadingServerMap = false;
        _serverMapLoadCoroutine = null;
    }

    private void ClearSpawnedPlayerObjects()
    {
        foreach (GameObject playerObject in players.Values)
        {
            if (playerObject != null)
            {
                Destroy(playerObject);
            }
        }

        players.Clear();
    }

    private IEnumerator ApplyLocalRoundResetFromState()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            string localSessionId = LocalSessionId;
            if (!string.IsNullOrWhiteSpace(localSessionId)
                && TryGetPlayerObject(localSessionId, out GameObject localPlayer)
                && localPlayer != null)
            {
                NetworkPlayer networkPlayer = localPlayer.GetComponent<NetworkPlayer>();
                if (networkPlayer != null)
                {
                    if (TryGetLocalPlayerGroundedSpawnPoint(out Vector3 groundedSpawnPosition))
                    {
                        networkPlayer.ApplyImmediateRoundReset(
                            groundedSpawnPosition,
                            localPlayer.transform.eulerAngles.y);
                        _localRoundResetCoroutine = null;
                        yield break;
                    }

                    if (networkPlayer.ApplyAuthoritativeRoundReset())
                    {
                        _localRoundResetCoroutine = null;
                        yield break;
                    }
                }
            }

            yield return null;
        }

        string fallbackLocalSessionId = LocalSessionId;
        if (!string.IsNullOrWhiteSpace(fallbackLocalSessionId)
            && TryGetLocalPlayerSpawnPoint(out Vector3 fallbackSpawnPosition)
            && TryGetPlayerObject(fallbackLocalSessionId, out GameObject fallbackLocalPlayer)
            && fallbackLocalPlayer != null)
        {
            NetworkPlayer networkPlayer = fallbackLocalPlayer.GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                networkPlayer.ApplyImmediateRoundReset(
                    fallbackSpawnPosition,
                    fallbackLocalPlayer.transform.eulerAngles.y);
            }
        }

        _localRoundResetCoroutine = null;
    }

    private void RestartLocalRoundResetCoroutine()
    {
        if (_localRoundResetCoroutine != null)
        {
            StopCoroutine(_localRoundResetCoroutine);
        }

        _localRoundResetCoroutine = StartCoroutine(ApplyLocalRoundResetFromState());
    }

    private static T ParseJsonMessage<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to parse room message into {typeof(T).Name}: {exception.Message}");
            return null;
        }
    }

    private void RestartFallbackRoundFlow()
    {
        StopFallbackRoundFlow();
        _fallbackRoundFlowCoroutine = StartCoroutine(RunFallbackRoundFlow());
    }

    private void StopFallbackRoundFlow()
    {
        if (_fallbackRoundFlowCoroutine == null)
        {
            return;
        }

        StopCoroutine(_fallbackRoundFlowCoroutine);
        _fallbackRoundFlowCoroutine = null;
    }

    private IEnumerator RunFallbackRoundFlow()
    {
        yield return new WaitForSecondsRealtime(IntermissionDurationMs / 1000f);

        if (_hasReceivedRoundPhaseFromServer || room == null)
        {
            _fallbackRoundFlowCoroutine = null;
            yield break;
        }

        RoundPhaseChanged?.Invoke(new RoundPhaseMessageData
        {
            phase = "round",
            roundIndex = 1,
            timeRemainingMs = RoundDurationMs,
            roundDurationMs = RoundDurationMs,
            intermissionDurationMs = IntermissionDurationMs,
            isMapVoteOpen = false,
        });

        RoundAnnouncementReceived?.Invoke(new RoundAnnouncementMessageData
        {
            title = "ROUND STARTED",
            subtitle = "SURVIVE FOR 3 MINUTES",
            durationSeconds = 3f,
        });

        _fallbackRoundFlowCoroutine = null;
    }

    private async void OnApplicationQuit()
    {
        await LeaveGame();
    }

    public async Task LeaveGame()
    {
        if (room != null)
        {
            try 
            {
               await room.Leave(true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error leaving room: {e.Message}");
            }
            finally
            {
                CleanupRoomState();
            }
        }
    }

    private void CleanupRoomState()
    {
        bool hadRoomState = room != null || !string.IsNullOrEmpty(currentRoomId) || players.Count > 0;
        room = null;
        currentRoomId = "";
        s_joinGameUsesShootingMode = false;
        s_multiplayerShootingPresentationEnabled = false;
        activeServerMapId = "";
        _isLoadingServerMap = false;
        _awaitingInitialMapSelection = false;
        if (_serverMapLoadCoroutine != null)
        {
            StopCoroutine(_serverMapLoadCoroutine);
            _serverMapLoadCoroutine = null;
        }
        _hasReceivedRoundPhaseFromServer = false;
        StopFallbackRoundFlow();

        ClearSpawnedPlayerObjects();

        if (hadRoomState)
        {
            RoomLeftEvent?.Invoke();
        }
    }

    // Called from index.html to keep the connection alive/active
    public void OnWindowBlur() { /* Keep running */ }
    public void OnWindowFocus() { /* Regain focus */ }
    private void AutoDiscoverSpawnPoints()
    {
        List<NextbotSpawnPointConfig> discoveredNextbotSpawns = new List<NextbotSpawnPointConfig>();
        List<PlayerSpawnPointConfig> discoveredPlayerSpawns = new List<PlayerSpawnPointConfig>();
        List<NextbotPatrolPointConfig> discoveredPatrolSpawns = new List<NextbotPatrolPointConfig>();
        Transform patrolRoot = FindNamedRootTransform("PatrolPoints");

        // Search for transforms by name since the config structs aren't components themselves.
        Transform[] allTransforms = FindObjectsOfType<Transform>();
        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;

            string name = t.name;
            if (name.IndexOf("Nextbot Spawn", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                discoveredNextbotSpawns.Add(new NextbotSpawnPointConfig { anchor = t, position = t.position });
            }
            else if (name.IndexOf("Player Spawn", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                discoveredPlayerSpawns.Add(new PlayerSpawnPointConfig { anchor = t, position = t.position });
            }
            else if (name.IndexOf("Patrol", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                discoveredPatrolSpawns.Add(new NextbotPatrolPointConfig { anchor = t, position = t.position });
            }
            else if (patrolRoot != null
                && t != patrolRoot
                && t.IsChildOf(patrolRoot)
                && t.name.StartsWith("Point", StringComparison.OrdinalIgnoreCase))
            {
                discoveredPatrolSpawns.Add(new NextbotPatrolPointConfig { anchor = t, position = t.position });
            }
        }

        if (discoveredNextbotSpawns.Count > 0)
        {
            nextbotSpawnPoints = discoveredNextbotSpawns;
        }

        if (discoveredPlayerSpawns.Count > 0)
        {
            playerSpawnPoints = discoveredPlayerSpawns;
        }

        if (discoveredPatrolSpawns.Count > 0)
        {
            nextbotPatrolPoints = discoveredPatrolSpawns;
        }
    }

    private static Transform FindNamedRootTransform(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        Transform[] allTransforms = FindObjectsOfType<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null || candidate.parent != null)
            {
                continue;
            }

            if (string.Equals(candidate.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }
}
