using System;
using System.Collections.Generic;
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
    private UIToolkitButton _settingsButton;
    private UIToolkitButton _graphicsLowButton;
    private UIToolkitButton _graphicsMediumButton;
    private UIToolkitButton _shopButton;
    private UIToolkitButton _inventoryButton;
    private UIToolkitButton _spectateButton;
    private Label _graphicsCurrentLabel;
    private Label _menuHoverLabel;
    private VisualElement _graphicsSettingsPanel;
    private bool _menuEventsBound;
    private readonly List<UIToolkitButton> _hoverButtons = new();
    private const string DefaultMenuHoverText = "Pick what you want to do next";
    private static readonly Scale LargeHoverButtonScale = new Scale(new Vector3(1.08f, 1.08f, 1f));
    private static readonly Scale HoverButtonScale = new Scale(new Vector3(1.02f, 1.02f, 1f));
    private static readonly Scale DefaultButtonScale = new Scale(Vector3.one);
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

        RefreshMenuUiBindings();

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
        RefreshMenuUiBindings();
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
        _settingsButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("settings-button");
        _graphicsLowButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("graphics-low-button");
        _graphicsMediumButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("graphics-medium-button");
        _shopButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("shop-button");
        _inventoryButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("inventory-button");
        _spectateButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("spectate-button");
        _graphicsCurrentLabel = _menuDocument.rootVisualElement?.Q<Label>("graphics-current-label");
        _menuHoverLabel = _menuDocument.rootVisualElement?.Q<Label>("menu-hover-label");
        _graphicsSettingsPanel = _menuDocument.rootVisualElement?.Q<VisualElement>("graphics-settings-panel");
        if (_startButton == null)
        {
            Debug.LogWarning("LobbyUI: Start button was not found in MainMenu.uxml.");
        }

        if (_graphicsSettingsPanel != null)
        {
            _graphicsSettingsPanel.style.display = DisplayStyle.None;
        }

        ConfigureMenuButtonDescriptions();
        RefreshGraphicsSettingsUi();
    }

    private void RefreshMenuUiBindings()
    {
        UnbindMenuEvents();
        CacheMenuUi();
        BindMenuEvents();
    }

    private void BindMenuEvents()
    {
        if (_menuEventsBound || _startButton == null)
        {
            return;
        }

        _startButton.clicked += HandleStartButtonClicked;
        if (_settingsButton != null)
        {
            _settingsButton.clicked += HandleSettingsButtonClicked;
        }
        if (_graphicsLowButton != null)
        {
            _graphicsLowButton.clicked += HandleGraphicsLowButtonClicked;
        }
        if (_graphicsMediumButton != null)
        {
            _graphicsMediumButton.clicked += HandleGraphicsMediumButtonClicked;
        }
        BindHoverEffects();
        BindPlaceholderActions();
        _menuEventsBound = true;
    }

    private void UnbindMenuEvents()
    {
        if (!_menuEventsBound || _startButton == null)
        {
            return;
        }

        _startButton.clicked -= HandleStartButtonClicked;
        if (_settingsButton != null)
        {
            _settingsButton.clicked -= HandleSettingsButtonClicked;
        }
        if (_graphicsLowButton != null)
        {
            _graphicsLowButton.clicked -= HandleGraphicsLowButtonClicked;
        }
        if (_graphicsMediumButton != null)
        {
            _graphicsMediumButton.clicked -= HandleGraphicsMediumButtonClicked;
        }
        UnbindPlaceholderActions();
        UnbindHoverEffects();
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

    private void HandleSettingsButtonClicked()
    {
        if (_graphicsSettingsPanel == null)
        {
            return;
        }

        bool showSettings = _graphicsSettingsPanel.style.display == DisplayStyle.None;
        _graphicsSettingsPanel.style.display = showSettings ? DisplayStyle.Flex : DisplayStyle.None;
        RefreshGraphicsSettingsUi();
    }

    private void HandleGraphicsLowButtonClicked()
    {
        ApplyGraphicsQuality(WebGLPerformanceBootstrap.LowQualityName);
    }

    private void HandleGraphicsMediumButtonClicked()
    {
        ApplyGraphicsQuality(WebGLPerformanceBootstrap.MediumQualityName);
    }

    private void HandlePlaceholderButtonClicked()
    {
        ShowNotification("This menu item is not wired yet.");
    }

    private void BindPlaceholderActions()
    {
        BindPlaceholderAction(_shopButton);
        BindPlaceholderAction(_inventoryButton);
        BindPlaceholderAction(_spectateButton);
    }

    private void UnbindPlaceholderActions()
    {
        UnbindPlaceholderAction(_shopButton);
        UnbindPlaceholderAction(_inventoryButton);
        UnbindPlaceholderAction(_spectateButton);
    }

    private void BindPlaceholderAction(UIToolkitButton button)
    {
        if (button == null)
        {
            return;
        }

        button.clicked += HandlePlaceholderButtonClicked;
    }

    private void UnbindPlaceholderAction(UIToolkitButton button)
    {
        if (button == null)
        {
            return;
        }

        button.clicked -= HandlePlaceholderButtonClicked;
    }

    private void ConfigureMenuButtonDescriptions()
    {
        SetMenuButtonDescription(_startButton, "Join the current game");
        SetMenuButtonDescription(_shopButton, "Browse the shop");
        SetMenuButtonDescription(_inventoryButton, "Open your inventory");
        SetMenuButtonDescription(_spectateButton, "Watch the current match");
        SetMenuButtonDescription(_settingsButton, "Adjust graphics and menu settings");

        if (_menuHoverLabel != null)
        {
            _menuHoverLabel.text = DefaultMenuHoverText;
        }
    }

    private static void SetMenuButtonDescription(UIToolkitButton button, string description)
    {
        if (button == null)
        {
            return;
        }

        button.userData = description;
    }

    private void BindHoverEffects()
    {
        _hoverButtons.Clear();
        RegisterHoverButton(_startButton);
        RegisterHoverButton(_settingsButton);
        RegisterHoverButton(_graphicsLowButton);
        RegisterHoverButton(_graphicsMediumButton);
        RegisterHoverButton(_shopButton);
        RegisterHoverButton(_inventoryButton);
        RegisterHoverButton(_spectateButton);
    }

    private void UnbindHoverEffects()
    {
        for (int i = 0; i < _hoverButtons.Count; i++)
        {
            UIToolkitButton button = _hoverButtons[i];
            if (button == null)
            {
                continue;
            }

            button.UnregisterCallback<PointerEnterEvent>(HandleMenuButtonPointerEnter);
            button.UnregisterCallback<PointerLeaveEvent>(HandleMenuButtonPointerLeave);
            button.style.scale = new StyleScale(DefaultButtonScale);
        }

        _hoverButtons.Clear();

        if (_menuHoverLabel != null)
        {
            _menuHoverLabel.text = DefaultMenuHoverText;
        }
    }

    private void RegisterHoverButton(UIToolkitButton button)
    {
        if (button == null)
        {
            return;
        }

        button.style.scale = new StyleScale(DefaultButtonScale);
        button.RegisterCallback<PointerEnterEvent>(HandleMenuButtonPointerEnter);
        button.RegisterCallback<PointerLeaveEvent>(HandleMenuButtonPointerLeave);
        _hoverButtons.Add(button);
    }

    private void HandleMenuButtonPointerEnter(PointerEnterEvent evt)
    {
        UIToolkitButton button = evt.currentTarget as UIToolkitButton;
        if (button == null)
        {
            return;
        }

        button.style.scale = new StyleScale(GetHoverScale(button));

        if (_menuHoverLabel != null)
        {
            _menuHoverLabel.text = button.userData as string ?? DefaultMenuHoverText;
        }
    }

    private void HandleMenuButtonPointerLeave(PointerLeaveEvent evt)
    {
        UIToolkitButton button = evt.currentTarget as UIToolkitButton;
        if (button == null)
        {
            return;
        }

        button.style.scale = new StyleScale(DefaultButtonScale);

        if (_menuHoverLabel != null)
        {
            _menuHoverLabel.text = DefaultMenuHoverText;
        }
    }

    private static Scale GetHoverScale(UIToolkitButton button)
    {
        if (button == null)
        {
            return HoverButtonScale;
        }

        return string.Equals(button.name, "start-button", StringComparison.Ordinal)
            ? LargeHoverButtonScale
            : HoverButtonScale;
    }

    private void ApplyGraphicsQuality(string qualityName)
    {
        if (!WebGLPerformanceBootstrap.TryApplyManualGraphicsQuality(qualityName))
        {
            ShowNotification($"Graphics preset '{qualityName}' is not available.");
            return;
        }

        RefreshGraphicsSettingsUi();
        ShowNotification($"Graphics set to {FormatGraphicsQualityName(qualityName)}");
    }

    private void RefreshGraphicsSettingsUi()
    {
        string activeQualityName = WebGLPerformanceBootstrap.GetActiveGraphicsQualityName();
        string formattedName = FormatGraphicsQualityName(activeQualityName);

        if (_graphicsCurrentLabel != null)
        {
            _graphicsCurrentLabel.text = $"Current: {formattedName}";
        }

        if (_graphicsLowButton != null)
        {
            bool isLowSelected = string.Equals(activeQualityName, WebGLPerformanceBootstrap.LowQualityName, StringComparison.Ordinal);
            _graphicsLowButton.text = isLowSelected ? "Low Selected" : "Low";
            _graphicsLowButton.SetEnabled(!isLowSelected);
        }

        if (_graphicsMediumButton != null)
        {
            bool isMediumSelected = string.Equals(activeQualityName, WebGLPerformanceBootstrap.MediumQualityName, StringComparison.Ordinal);
            _graphicsMediumButton.text = isMediumSelected ? "Medium Selected" : "Medium";
            _graphicsMediumButton.SetEnabled(!isMediumSelected);
        }
    }

    private static string FormatGraphicsQualityName(string qualityName)
    {
        if (string.IsNullOrEmpty(qualityName))
        {
            return "Unknown";
        }

        if (qualityName.StartsWith("WebGL ", StringComparison.Ordinal))
        {
            return qualityName.Substring("WebGL ".Length);
        }

        return qualityName;
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
