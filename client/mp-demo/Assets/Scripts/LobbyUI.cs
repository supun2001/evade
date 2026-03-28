using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using TMPro;
using UIButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;
using UIToolkitButton = UnityEngine.UIElements.Button;

public class LobbyUI : MonoBehaviour
{
    #region Class Variables
    [Header("Menu UI")]
    public GameObject menuPanel;
    public TMP_InputField joinCodeInput;
    public UIButton createButton;
    public UIButton joinButton;

    [Header("General")]
    public Camera lobbyCamera;
    public TextMeshProUGUI notificationText;

    [Header("Skin Selection")]
    public SkinRegistry skinRegistry;
    public UIImage skinPreviewImage;
    public TextMeshProUGUI skinNameText; 
    private int currentSkinIndex = 0;
    private bool hasAutoReadiedCurrentRoom = false;
    private UIDocument _menuDocument;
    private UIToolkitButton _startButton;
    private bool _menuEventsBound;
    #endregion

    #region Class Methods
    private void Start()
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("LobbyUI: Menu Panel is not assigned in the Inspector!");
        }

        CacheMenuUi();
        BindMenuEvents();

        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(true);

        if (PlayerPrefs.HasKey("SelectedSkin"))
        {
            currentSkinIndex = PlayerPrefs.GetInt("SelectedSkin");
        }
        UpdateSkinUI();

        if (notificationText != null) notificationText.text = "";
    }

    private void Update()
    {
        if (NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.currentRoomId))
        {
            if (menuPanel.activeSelf)
            {
                AutoStartJoinedRoom();
            }
        }
    }
    
    private void OnDestroy()
    {
        UnbindMenuEvents();

        if (NetworkManager.Instance != null && NetworkManager.Instance.Room != null)
        {
             NetworkManager.Instance.Room.OnStateChange -= OnLobbyStateChange;
        }
    }
    #endregion

    public async void LeaveRoom()
    {
        if (NetworkManager.Instance != null)
        {
            await NetworkManager.Instance.LeaveGame();
        }
        
        SwitchToMenu();
    }

    private void SwitchToMenu()
    {
        menuPanel.SetActive(true);
        hasAutoReadiedCurrentRoom = false;
        SetLocalPlayerInput(false);
        SetStartButtonEnabled(true);
        
        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(true);
        
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        
        if (joinButton != null) joinButton.interactable = true;
        if (createButton != null) createButton.interactable = true;
    }

    private void AutoStartJoinedRoom()
    {
        menuPanel.SetActive(false);

        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(false);

        if (!hasAutoReadiedCurrentRoom && NetworkManager.Instance != null && NetworkManager.Instance.Room != null)
        {
            NetworkManager.Instance.SendReadyState(true);
            hasAutoReadiedCurrentRoom = true;
        }

        OnGameStarted();
    }

    #region Button Clicks
    private void OnLobbyStateChange(MyRoomState state, bool isFirstState)
    {
        if (state.isGameStarted && menuPanel.activeSelf)
        {
            Debug.Log("LobbyUI: Room is already in-game. Starting for late joiner...");
            OnGameStarted();
        }
    }

    public async void OnCreateClicked()
    {
        SetStartButtonEnabled(false);

        if (createButton != null) createButton.interactable = false;
        string error = await NetworkManager.Instance.CreateGame();
        
        if (string.IsNullOrEmpty(error))
        {
            // Sync Skin Immediately on Join
            SaveAndSyncSkin();
        }
        else
        {
            ShowNotification($"Create Failed: {error}");
        }
        
        if (createButton != null) createButton.interactable = true;
        if (!hasAutoReadiedCurrentRoom)
        {
            SetStartButtonEnabled(true);
        }
    }

    public async void OnStartClicked()
    {
        SetStartButtonEnabled(false);

        if (createButton != null) createButton.interactable = false;
        if (joinButton != null) joinButton.interactable = false;

        string error = await NetworkManager.Instance.JoinOrCreateGame();

        if (string.IsNullOrEmpty(error))
        {
            SaveAndSyncSkin();
        }
        else
        {
            ShowNotification($"Start Failed: {error}");
        }

        if (createButton != null) createButton.interactable = true;
        if (joinButton != null) joinButton.interactable = true;

        if (!hasAutoReadiedCurrentRoom)
        {
            SetStartButtonEnabled(true);
        }
    }

    public async void OnJoinClicked()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.Room != null)
        {
            Debug.LogWarning("LobbyUI: Already in a room. Ignoring Join request.");
            return;
        }

        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            ShowNotification("Please enter a room code");
            return;
        }

        if (joinButton != null) joinButton.interactable = false;
        string error = await NetworkManager.Instance.JoinGame(code);

        if (string.IsNullOrEmpty(error))
        {
            // Sync Skin Immediately on Join
            SaveAndSyncSkin();
        }
        else
        {
            if (error.Contains("full")) ShowNotification("Room is full!");
            else if (error.Contains("not found")) ShowNotification("Room not found!");
            else ShowNotification($"Join Failed: {error}");
        }

        if (joinButton != null) joinButton.interactable = true;
    }

    public void OnGameStarted()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        hasAutoReadiedCurrentRoom = true;

        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(false);
        
        // Lock cursor for gameplay
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;

        // Ensure movement is enabled
        SetLocalPlayerInput(true);
    }

    private void CacheMenuUi()
    {
        if (menuPanel == null)
        {
            return;
        }

        _menuDocument = menuPanel.GetComponent<UIDocument>();
        if (_menuDocument == null)
        {
            return;
        }

        _startButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("start-button");
        if (_startButton == null)
        {
            Debug.LogWarning("LobbyUI: Start button was not found in MainMenu.uxml.");
        }
    }

    private void BindMenuEvents()
    {
        if (_menuEventsBound || _startButton == null)
        {
            return;
        }

        _startButton.clicked += HandleStartButtonClicked;
        _menuEventsBound = true;
    }

    private void UnbindMenuEvents()
    {
        if (!_menuEventsBound || _startButton == null)
        {
            return;
        }

        _startButton.clicked -= HandleStartButtonClicked;
        _menuEventsBound = false;
    }

    private void HandleStartButtonClicked()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.Room != null)
        {
            Debug.LogWarning("LobbyUI: Already in a room. Ignoring Start request.");
            return;
        }

        OnStartClicked();
    }

    private void SetStartButtonEnabled(bool enabled)
    {
        if (_startButton == null)
        {
            return;
        }

        _startButton.SetEnabled(enabled);
    }

    private void SetLocalPlayerInput(bool enabled)
    {
        GameObject localPlayer = GameObject.Find("LocalPlayer");
        if (localPlayer != null)
        {
            var input = localPlayer.GetComponent<PlayerLocomotionInput>();
            if (input != null)
            {
                input.InputEnabled = enabled;
            }
        }
    }




    // Skin Selection
    public void OnNextSkinClicked()
    {
        if (skinRegistry == null || skinRegistry.skins.Length == 0) return;

        currentSkinIndex = (currentSkinIndex + 1) % skinRegistry.skins.Length;
        SaveAndSyncSkin();
    }

    public void OnPrevSkinClicked()
    {
        if (skinRegistry == null || skinRegistry.skins.Length == 0) return;

        currentSkinIndex--;
        if (currentSkinIndex < 0) currentSkinIndex = skinRegistry.skins.Length - 1;
        SaveAndSyncSkin();
    }

    private void SaveAndSyncSkin()
    {
        PlayerPrefs.SetInt("SelectedSkin", currentSkinIndex);
        PlayerPrefs.Save();
       
       UpdateSkinUI();

       if (NetworkManager.Instance != null && NetworkManager.Instance.Room != null)
       {
           NetworkManager.Instance.Room.Send("setSkin", currentSkinIndex);
       }
    }

    private void UpdateSkinUI()
    {
        if (skinRegistry == null || skinRegistry.skins.Length == 0) return;

       if (currentSkinIndex >= skinRegistry.skins.Length) currentSkinIndex = 0;

       // Get the current skin entry
       var skinEntry = skinRegistry.skins[currentSkinIndex];

       if (skinPreviewImage != null)
       {
           // Use the distinct UI preview sprite if available, otherwise fallback or leave null
           if (skinEntry.uiPreview != null)
           {
               skinPreviewImage.sprite = skinEntry.uiPreview;
           }
       }
       
       if (skinNameText != null)
       {
           // Use the custom name if set, otherwise default to "Skin X"
           if (!string.IsNullOrEmpty(skinEntry.skinName))
           {
               skinNameText.text = skinEntry.skinName;
           }
           else
           {
               skinNameText.text = $"Skin {currentSkinIndex + 1}";
           }
       }
    }
    public void ShowNotification(string message, float duration = 3f)
    {
        if (notificationText == null) return;

        notificationText.text = message;
        CancelInvoke(nameof(ClearNotification));
        Invoke(nameof(ClearNotification), duration);
    }

    private void ClearNotification()
    {
        if (notificationText != null) notificationText.text = "";
    }
    #endregion
}
