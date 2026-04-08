using UnityEngine;
using Colyseus;
using System.Collections.Generic;
using System.Threading.Tasks;
using Colyseus.Schema;
using System;
using System.Collections;

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
    private const string HostedServerUrl = "wss://evade-6o6d.onrender.com";
    private const int PlayerUpdateFieldCount = 29;

    public static NetworkManager Instance;
    
    [Header("Network Configuration")]
    [Tooltip("Local development server URL")]
    [SerializeField] private string localServerUrl = "ws://localhost:2567";

    [Tooltip("Production server URL (Render/Railway). Must use wss:// for WebGL.")]
    [SerializeField] private string productionServerUrl = HostedServerUrl;
    [Tooltip("Use the production URL even while running in the Unity Editor.")]
    [SerializeField] private bool useProductionServerInEditor = false;
    [Header("Gameplay Configuration")]
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
    [SerializeField] private float spawnGroundProbeHeight = 30f;
    [SerializeField] private float spawnGroundProbeDistance = 120f;
    [SerializeField] private float spawnGroundOffset = 0.15f;
    [SerializeField] private LayerMask spawnGroundLayers = ~0;
    [SerializeField] private float serverFloorSampleSpacing = 2f;
    [SerializeField] private float serverFloorSamplePadding = 2f;
    [Tooltip("Optional patrol hints for idle nextbots. Leave empty to auto-roam using the overall spawn area.")]
    [SerializeField] private List<NextbotPatrolPointConfig> nextbotPatrolPoints = new();
    [Header("Round Timing")]
    [Tooltip("How long the intermission lasts before the round starts.")]
    [SerializeField, Min(1f)] private float intermissionDurationSeconds = 30f;
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
    private readonly float[] playerUpdatePayload = new float[PlayerUpdateFieldCount];

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
    public event Action RoomLeftEvent;
    public string LocalSessionId => room != null ? room.SessionId : string.Empty;
    private bool _hasReceivedRoundPhaseFromServer;
    private Coroutine _fallbackRoundFlowCoroutine;
    private Coroutine _localRoundResetCoroutine;

    private void OnPlayerAdded(string id, Player player)
    {
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
        Vector3 pos = ResolveGroundedSpawnPosition(new Vector3(player.x, player.y, player.z));
        GameObject obj = Instantiate(playerPrefab, pos, Quaternion.identity);

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

            obj.name = $"RemotePlayer_{id}";
        }
        else
        {
            obj.name = "LocalPlayer";
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
        float jumpBoostTimeRemaining)
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
                jumpBoostTimeRemaining = jumpBoostTimeRemaining
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
        return players.TryGetValue(sessionId, out playerObject);
    }

    public async Task<string> CreateGame(){
        InitializeClient();
        try{
            room = await client.Create<MyRoomState>(roomName, BuildRoomOptions());
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
        try
        {
            room = await client.JoinOrCreate<MyRoomState>(roomName, BuildRoomOptions());
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
        try
        {
            room = await client.JoinById<MyRoomState>(targetRoomId, BuildRoomOptions());
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

    private Dictionary<string, object> BuildRoomOptions()
    {
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
        List<object> serializedNextbotConfigs = BuildSerializedNextbotConfigs();
        List<object> serializedObstacles = BuildSerializedObstacleBounds();
        List<object> serializedFloorSamples = BuildSerializedFloorSamples(nextbotSpawnPoints, playerSpawnPoints, nextbotPatrolPoints);

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
            ["username"] = string.IsNullOrWhiteSpace(AuthenticatedUsername) ? "Player" : AuthenticatedUsername,
        };
    }

    private static List<object> BuildSerializedObstacleBounds()
    {
        List<object> serializedObstacles = new List<object>();
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        int walkableLayer = LayerMask.NameToLayer("Walkable");

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (!ShouldIncludeServerObstacle(collider, walkableLayer))
            {
                continue;
            }

            Bounds bounds = collider.bounds;
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

    private static bool ShouldIncludeServerObstacle(Collider collider, int walkableLayer)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger)
        {
            return false;
        }

        if (walkableLayer >= 0 && collider.gameObject.layer == walkableLayer)
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
        if (bounds.size.x <= 0.1f || bounds.size.z <= 0.1f || bounds.size.y <= 0.25f)
        {
            return false;
        }

        // Skip very large flat surfaces like ground planes; the server only needs blockers.
        if (bounds.size.y <= 1.5f && (bounds.size.x >= 25f || bounds.size.z >= 25f))
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

        HashSet<string> dedup = new HashSet<string>();
        for (float x = minX; x <= maxX + 0.01f; x += spacing)
        {
            for (float z = minZ; z <= maxZ + 0.01f; z += spacing)
            {
                Vector3 grounded = ResolveGroundedSpawnPosition(new Vector3(x, 0f, z));
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
        }

        for (int i = 0; i < areaPoints.Count; i++)
        {
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

    private Vector3 ResolveGroundedSpawnPosition(Vector3 desiredPosition)
    {
        Vector3 rayOrigin = desiredPosition + Vector3.up * Mathf.Max(1f, spawnGroundProbeHeight);
        float rayDistance = Mathf.Max(10f, spawnGroundProbeDistance);
        int layerMask = GetSpawnGroundLayerMask();
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, layerMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * spawnGroundOffset;
        }

        int walkableLayer = LayerMask.NameToLayer("Walkable");
        if (walkableLayer >= 0)
        {
            int walkableMask = 1 << walkableLayer;
            if (Physics.SphereCast(rayOrigin, 0.35f, Vector3.down, out hit, rayDistance, walkableMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * spawnGroundOffset;
            }
        }

        return desiredPosition;
    }

    private int GetSpawnGroundLayerMask()
    {
        int configuredMask = spawnGroundLayers.value;
        int walkableLayer = LayerMask.NameToLayer("Walkable");
        if (walkableLayer >= 0)
        {
            int walkableMask = 1 << walkableLayer;
            bool usesEverything = configuredMask == ~0;
            bool usesNothing = configuredMask == 0;
            if (usesEverything || usesNothing)
            {
                return walkableMask;
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
        Debug.Log($"Connected! Room ID: {currentRoomId}");

        room.Send("syncNextbotConfigs", new Dictionary<string, object>
        {
            ["nextbotConfigs"] = BuildSerializedNextbotConfigs(),
        });
        
        // Setup Handlers 
        room.OnStateChange += OnStateChange;
        
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

        var events = Colyseus.Schema.Callbacks.Get(room);
        events.OnAdd(state => state.players, (key, player) => OnPlayerAdded(key, player));
        events.OnRemove(state => state.players, (key, player) => OnPlayerRemoved(key, player));
    }

    private IEnumerator ApplyLocalRoundResetFromState()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (room != null
                && players.TryGetValue(room.SessionId, out GameObject localPlayer)
                && localPlayer != null)
            {
                NetworkPlayer networkPlayer = localPlayer.GetComponent<NetworkPlayer>();
                if (networkPlayer != null && networkPlayer.ApplyAuthoritativeRoundReset())
                {
                    _localRoundResetCoroutine = null;
                    yield break;
                }
            }

            yield return null;
        }

        if (TryGetLocalPlayerSpawnPoint(out Vector3 fallbackSpawnPosition)
            && players.TryGetValue(room.SessionId, out GameObject fallbackLocalPlayer)
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
                room = null;
                currentRoomId = "";
                _hasReceivedRoundPhaseFromServer = false;
                StopFallbackRoundFlow();
                // Clear players
                foreach(var p in players.Values) Destroy(p);
                players.Clear();
                RoomLeftEvent?.Invoke();
            }
        }
    }

    // Called from index.html to keep the connection alive/active
    public void OnWindowBlur() { /* Keep running */ }
    public void OnWindowFocus() { /* Regain focus */ }
}
