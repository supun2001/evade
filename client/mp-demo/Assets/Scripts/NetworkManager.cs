using UnityEngine;
using Colyseus;
using System.Collections.Generic;
using System.Threading.Tasks;
using Colyseus.Schema;
public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;
    
    [Header("Network Configuration")]
    [Tooltip("Local development server URL")]
    [SerializeField] private string localServerUrl = "ws://localhost:2567";

    [Tooltip("Production server URL (Render/Railway). Must use wss:// for WebGL.")]
    [SerializeField] private string productionServerUrl = "wss://unity6-demo-new-gmc8axbnc2fugtdw.canadacentral-01.azurewebsites.net";

    public string serverUrl 
    {
        get 
        {
            #if UNITY_EDITOR
                return localServerUrl;
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
        
        // Ensure the game keeps running and syncing when focus is lost (e.g., when testing multiple instances)
        Application.runInBackground = true;
    }

    private async void Start()
    {
        client = new ColyseusClient(serverUrl);
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

    public void SendPlayerUpdate(Vector3 pos, float rotY, Vector3 vel, float aX, float aY, bool g, bool j, Vector2 camRot)
    {
        if (room == null) return;
        
        room.Send("playerUpdate", new {
            x = pos.x, y = pos.y, z = pos.z,
            rotationY = rotY,
            velocityX = vel.x, velocityY = vel.y, velocityZ = vel.z,
            animInputX = aX, animInputY = aY,
            isGrounded = g, isJumping = j,
            cameraRotationX = camRot.x, cameraRotationY = camRot.y
        });
    }

    public void SendReadyState(bool isReady)
    {
        if (room == null) return;
        room.Send("playerReady", isReady);
    }

    public async Task<string> CreateGame(){
        InitializeClient();
        try{
            room = await client.Create<MyRoomState>(roomName);
            OnRoomJoined();
            return null; // Success

        }catch(System.Exception e){
            Debug.LogError($"Matchmaking Failed: {e.Message}");
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
        if (client == null) client = new ColyseusClient(serverUrl);
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