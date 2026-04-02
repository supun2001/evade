using UnityEngine;
using Colyseus;
using System.Collections.Generic;
using System.Threading.Tasks;
using Colyseus.Schema;
using System;

[Serializable]
public struct NextbotSpawnPointConfig
{
    public Vector3 position;
}

public class NetworkManager : MonoBehaviour
{
    private const string HostedServerUrl = "wss://evade-6o6d.onrender.com";

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
    };

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

    private ColyseusClient client;
    private ColyseusRoom<MyRoomState> room;
    public ColyseusRoom<MyRoomState> Room => room;
    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();

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

    private void OnPlayerAdded(string id, Player player)
    {
        Debug.Log($"Player added: {id}");
        Vector3 pos = new Vector3(player.x, player.y, player.z);
        GameObject obj = Instantiate(playerPrefab, pos, Quaternion.identity);
        
        bool isLocal = id == room.SessionId;
        
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
        // Occurs when the room state is updated
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
        float hitReactionSeed)
    {
        if (room == null) return;
        
        room.Send("playerUpdate", new {
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
            hitReactionSeed = hitReactionSeed
        });
    }

    public void SendReadyState(bool isReady)
    {
        if (room == null) return;
        room.Send("playerReady", isReady);
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
            room = await client.JoinById<MyRoomState>(targetRoomId);
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
            Vector3 point = nextbotSpawnPoints[i].position;
            serializedSpawnPoints.Add(new Dictionary<string, object>
            {
                ["x"] = point.x,
                ["y"] = point.y,
                ["z"] = point.z,
            });
        }

        return new Dictionary<string, object>
        {
            ["nextbotSpawnPoints"] = serializedSpawnPoints
        };
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
        
        // Setup Handlers 
        room.OnStateChange += OnStateChange;
        
        // Listen for Start Game
        room.OnMessage<string>("startGame", (message) => {
            Debug.Log("Game Started!");
            
            // Notify LobbyUI to hide HUD
            LobbyUI lobby = FindObjectOfType<LobbyUI>();
            if (lobby != null)
            {
                lobby.OnGameStarted();
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

        var events = Colyseus.Schema.Callbacks.Get(room);
        events.OnAdd(state => state.players, (key, player) => OnPlayerAdded(key, player));
        events.OnRemove(state => state.players, (key, player) => OnPlayerRemoved(key, player));
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
                // Clear players
                foreach(var p in players.Values) Destroy(p);
                players.Clear();
            }
        }
    }

    // Called from index.html to keep the connection alive/active
    public void OnWindowBlur() { /* Keep running */ }
    public void OnWindowFocus() { /* Regain focus */ }
}
