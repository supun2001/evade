using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using TMPro;
using UIButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;
using UIToolkitImage = UnityEngine.UIElements.Image;
using UIToolkitButton = UnityEngine.UIElements.Button;

public class LobbyUI : MonoBehaviour
{
    private enum ShopPage
    {
        Home,
        Skins,
    }

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
    [SerializeField] private GameObject shopPreviewPrefab;
    [SerializeField] private Vector3 shopPreviewModelPosition = new Vector3(0f, -20f, 0f);
    [SerializeField] private Vector3 shopPreviewModelEuler = new Vector3(0f, 205f, 0f);
    [SerializeField] private Vector3 shopPreviewCameraPosition = new Vector3(0f, 1.1f, 6.1f);
    [SerializeField] private Color shopPreviewCameraBackground = new Color(0.03f, 0.05f, 0.06f, 0f);
    [SerializeField] private float shopPreviewDragSensitivity = 0.35f;
    private int currentSkinIndex = 0;
    private int _shopSelectedSkinIndex;
    private bool hasAutoReadiedCurrentRoom = false;
    private UIDocument _menuDocument;
    private UIToolkitButton _startButton;
    private UIToolkitButton _settingsButton;
    private UIToolkitButton _settingsCloseButton;
    private UIToolkitButton _graphicsLowButton;
    private UIToolkitButton _graphicsMediumButton;
    private UIToolkitButton _shopButton;
    private UIToolkitButton _shopDailyStoreButton;
    private UIToolkitButton _shopEquipmentButton;
    private UIToolkitButton _shopSkinsMenuButton;
    private UIToolkitButton _shopBackButton;
    private UIToolkitButton _shopCloseButton;
    private UIToolkitButton _shopActionButton;
    private UIToolkitButton _shopEmotesButton;
    private UIToolkitButton _shopRobuxButton;
    private UIToolkitButton _inventoryButton;
    private UIToolkitButton _spectateButton;
    private UIToolkitButton _comingSoonCloseButton;
    private Label _graphicsCurrentLabel;
    private Label _menuHoverLabel;
    private Label _comingSoonMessageLabel;
    private Label _moneyLabel;
    private Label _shopSelectedSkinLabel;
    private Label _shopSelectedSkinSubtitle;
    private Label _shopSelectedPriceLabel;
    private Label _shopSelectedStateLabel;
    private VisualElement _menuCenterColumn;
    private VisualElement _menuRightRail;
    private VisualElement _walletPill;
    private VisualElement _graphicsSettingsPanel;
    private VisualElement _settingsCard;
    private VisualElement _comingSoonOverlay;
    private VisualElement _shopOverlay;
    private VisualElement _shopHomeView;
    private VisualElement _shopSkinsView;
    private VisualElement _shopPreviewPanel;
    private UIToolkitImage _shopPreviewFrame;
    private SliderInt _graphicsVolumeSlider;
    private bool _settingsPopupVisible;
    private bool _shopVisible;
    private ShopPage _shopPage = ShopPage.Home;
    private bool _menuEventsBound;
    private bool _pendingSpectateJoin;
    private bool _isSpectatingFromMenu;
    private readonly List<UIToolkitButton> _hoverButtons = new();
    private readonly List<UIToolkitButton> _shopSkinButtons = new();
    private readonly List<Label> _shopSkinNameLabels = new();
    private readonly List<Label> _shopSkinDescriptionLabels = new();
    private readonly List<Label> _shopSkinStatusLabels = new();
    private readonly List<Label> _shopSkinPriceLabels = new();
    private readonly Dictionary<UIToolkitButton, Vector3> _buttonCurrentScales = new();
    private readonly Dictionary<UIToolkitButton, Vector3> _buttonTargetScales = new();
    private const string DefaultMenuHoverText = "Pick what you want to do next";
    private const float MenuHoverLabelFadeSpeed = 7f;
    private const float MenuButtonScaleLerpSpeed = 12f;
    private const string JoinGameCardResourcePath = "UI/JoinGameCard";
    private const string ShopCardResourcePath = "UI/ShopCard";
    private const string InventoryCardResourcePath = "UI/InventoryCard";
    private static readonly Scale LargeHoverButtonScale = new Scale(new Vector3(1.03f, 1.03f, 1f));
    private static readonly Scale FeaturedSideHoverButtonScale = new Scale(new Vector3(1.18f, 1.18f, 1f));
    private static readonly Scale HoverButtonScale = new Scale(new Vector3(1.02f, 1.02f, 1f));
    private static readonly Scale DefaultButtonScale = new Scale(Vector3.one);
    private static Texture2D s_joinGameCardTexture;
    private static Texture2D s_shopCardTexture;
    private static Texture2D s_inventoryCardTexture;
    private string _pendingHoverLabelText = DefaultMenuHoverText;
    private float _currentHoverLabelOpacity;
    private float _targetHoverLabelOpacity;
    private const int StartingMoneyAmount = 100;
    private const string SelectedSkinPlayerPrefsKey = "SelectedSkin";
    private const string PlayerMoneyPlayerPrefsKey = "PlayerMoney";
    private const string OwnedSkinPlayerPrefsPrefix = "OwnedSkin_";
    private const int DefaultOwnedSkinIndex = 0;
    private RenderTexture _shopPreviewRenderTexture;
    private Camera _shopPreviewCamera;
    private GameObject _shopPreviewRoot;
    private GameObject _shopPreviewInstance;
    private Renderer[] _shopPreviewRenderers = Array.Empty<Renderer>();
    private Animator _shopPreviewAnimator;
    private bool _isDraggingShopPreview;
    private Vector2 _shopPreviewLastPointerPosition;
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

        EnsureEconomyDefaults();
        if (PlayerPrefs.HasKey(SelectedSkinPlayerPrefsKey))
        {
            currentSkinIndex = PlayerPrefs.GetInt(SelectedSkinPlayerPrefsKey);
        }
        currentSkinIndex = GetValidOwnedSkinIndex(currentSkinIndex);
        _shopSelectedSkinIndex = currentSkinIndex;
        UpdateSkinUI();
        UpdateMoneyUI();

        if (notificationText != null) notificationText.text = "";
    }

    private void Update()
    {
        if (_isSpectatingFromMenu && menuPanel != null && !menuPanel.activeSelf)
        {
            SetLocalPlayerSpectating(true);
        }

        if (NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.currentRoomId))
        {
            if (_pendingSpectateJoin)
            {
                ActivateSpectateMode();
            }
            else if (menuPanel.activeSelf)
            {
                AutoStartJoinedRoom();
            }
        }

        UpdateHoverLabelFade();
        UpdateMenuButtonScaleAnimation();
        UpdateShopPreviewAnimation();
    }
    
    private void OnDestroy()
    {
        UnbindMenuEvents();
        DestroyShopPreviewObjects();

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
        _pendingSpectateJoin = false;
        _isSpectatingFromMenu = false;
        if (NetworkManager.Instance != null && NetworkManager.Instance.Room != null)
        {
            NetworkManager.Instance.SendReadyState(false);
            NetworkManager.Instance.SendSpectatorState(true);
        }

        menuPanel.SetActive(true);
        RefreshMenuUiBindings();
        hasAutoReadiedCurrentRoom = false;
        SetLocalPlayerInput(false);
        SetLocalPlayerSpectating(false);
        SetStartButtonEnabled(true);
        
        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(true);
        
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        
        if (joinButton != null) joinButton.interactable = true;
        if (createButton != null) createButton.interactable = true;
    }

    private void AutoStartJoinedRoom()
    {
        if (_pendingSpectateJoin || _isSpectatingFromMenu)
        {
            ActivateSpectateMode();
            return;
        }

        menuPanel.SetActive(false);

        if (lobbyCamera != null) lobbyCamera.gameObject.SetActive(false);

        if (!hasAutoReadiedCurrentRoom && NetworkManager.Instance != null && NetworkManager.Instance.Room != null)
        {
            NetworkManager.Instance.SendSpectatorState(false);
            NetworkManager.Instance.SendReadyState(true);
            hasAutoReadiedCurrentRoom = true;
        }

        OnGameStarted();
    }

    #region Button Clicks
    private void OnLobbyStateChange(MyRoomState state, bool isFirstState)
    {
        if (state.isGameStarted && menuPanel.activeSelf && !_pendingSpectateJoin && !_isSpectatingFromMenu)
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
        _pendingSpectateJoin = false;
        _isSpectatingFromMenu = false;
        SetShopVisible(false);
        if (menuPanel != null) menuPanel.SetActive(false);
        hasAutoReadiedCurrentRoom = true;

        if (NetworkManager.Instance != null && NetworkManager.Instance.Room != null)
        {
            NetworkManager.Instance.SendSpectatorState(false);
            NetworkManager.Instance.SendReadyState(true);
        }

        SetLocalPlayerSpectating(false);
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
        _settingsCloseButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("settings-close-button");
        _graphicsLowButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("graphics-low-button");
        _graphicsMediumButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("graphics-medium-button");
        _shopButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("shop-button");
        _shopDailyStoreButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("shop-daily-store-button");
        _shopEquipmentButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("shop-equipment-button");
        _shopSkinsMenuButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("shop-skins-menu-button");
        _shopBackButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("shop-back-button");
        _shopCloseButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("shop-close-button");
        _shopActionButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("shop-action-button");
        _shopEmotesButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("shop-emotes-button");
        _shopRobuxButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("shop-robux-button");
        _inventoryButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("inventory-button");
        _spectateButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("spectate-button");
        _comingSoonCloseButton = _menuDocument.rootVisualElement?.Q<UIToolkitButton>("coming-soon-close-button");
        _graphicsCurrentLabel = _menuDocument.rootVisualElement?.Q<Label>("graphics-current-label");
        _graphicsVolumeSlider = _menuDocument.rootVisualElement?.Q<SliderInt>("graphics-volume-slider");
        _menuHoverLabel = _menuDocument.rootVisualElement?.Q<Label>("menu-hover-label");
        _comingSoonMessageLabel = _menuDocument.rootVisualElement?.Q<Label>("coming-soon-message-label");
        _moneyLabel = _menuDocument.rootVisualElement?.Q<Label>("money-label");
        _shopSelectedSkinLabel = _menuDocument.rootVisualElement?.Q<Label>("shop-selected-skin-label");
        _shopSelectedSkinSubtitle = _menuDocument.rootVisualElement?.Q<Label>("shop-selected-skin-subtitle");
        _shopSelectedPriceLabel = _menuDocument.rootVisualElement?.Q<Label>("shop-selected-price-label");
        _shopSelectedStateLabel = _menuDocument.rootVisualElement?.Q<Label>("shop-selected-state-label");
        _menuCenterColumn = _menuDocument.rootVisualElement?.Q<VisualElement>("menu-center-column");
        _menuRightRail = _menuDocument.rootVisualElement?.Q<VisualElement>("menu-right-rail");
        _walletPill = _menuDocument.rootVisualElement?.Q<VisualElement>("wallet-pill");
        _graphicsSettingsPanel = _menuDocument.rootVisualElement?.Q<VisualElement>("graphics-settings-panel");
        _settingsCard = _menuDocument.rootVisualElement?.Q<VisualElement>("settings-card");
        _comingSoonOverlay = _menuDocument.rootVisualElement?.Q<VisualElement>("coming-soon-overlay");
        _shopOverlay = _menuDocument.rootVisualElement?.Q<VisualElement>("shop-overlay");
        _shopHomeView = _menuDocument.rootVisualElement?.Q<VisualElement>("shop-home-view");
        _shopSkinsView = _menuDocument.rootVisualElement?.Q<VisualElement>("shop-skins-view");
        _shopPreviewPanel = _menuDocument.rootVisualElement?.Q<VisualElement>("shop-preview-panel");
        _shopPreviewFrame = _menuDocument.rootVisualElement?.Q<UIToolkitImage>("shop-preview-frame");
        CacheShopSkinElements();
        if (_startButton == null)
        {
            Debug.LogWarning("LobbyUI: Start button was not found in MainMenu.uxml.");
        }

        _settingsPopupVisible = false;
        _shopVisible = false;
        _shopPage = ShopPage.Home;
        SetSettingsPopupVisible(false);
        SetShopVisible(false);
        BindShopPreviewEvents();

        if (_comingSoonOverlay != null)
        {
            _comingSoonOverlay.style.display = DisplayStyle.None;
        }

        ConfigureMenuButtonDescriptions();
        RefreshGraphicsSettingsUi();
        ApplyMenuArt();
        ResetHoverLabelVisual();
        UpdateMoneyUI();
        RefreshShopUi();
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
        if (_settingsCloseButton != null)
        {
            _settingsCloseButton.clicked += HandleSettingsCloseButtonClicked;
        }
        if (_graphicsSettingsPanel != null)
        {
            _graphicsSettingsPanel.RegisterCallback<ClickEvent>(HandleSettingsOverlayClicked);
        }
        if (_settingsCard != null)
        {
            _settingsCard.RegisterCallback<ClickEvent>(HandleSettingsCardClicked);
        }
        if (_graphicsLowButton != null)
        {
            _graphicsLowButton.clicked += HandleGraphicsLowButtonClicked;
        }
        if (_graphicsMediumButton != null)
        {
            _graphicsMediumButton.clicked += HandleGraphicsMediumButtonClicked;
        }
        if (_shopButton != null)
        {
            _shopButton.clicked += HandleShopButtonClicked;
        }
        if (_shopSkinsMenuButton != null)
        {
            _shopSkinsMenuButton.clicked += HandleShopSkinsMenuButtonClicked;
        }
        if (_shopBackButton != null)
        {
            _shopBackButton.clicked += HandleShopBackButtonClicked;
        }
        if (_shopCloseButton != null)
        {
            _shopCloseButton.clicked += HandleShopCloseButtonClicked;
        }
        if (_shopActionButton != null)
        {
            _shopActionButton.clicked += HandleShopActionButtonClicked;
        }
        if (_graphicsVolumeSlider != null)
        {
            _graphicsVolumeSlider.RegisterValueChangedCallback(HandleGraphicsVolumeSliderChanged);
        }
        if (_comingSoonCloseButton != null)
        {
            _comingSoonCloseButton.clicked += HandleComingSoonCloseButtonClicked;
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
        if (_settingsCloseButton != null)
        {
            _settingsCloseButton.clicked -= HandleSettingsCloseButtonClicked;
        }
        if (_graphicsSettingsPanel != null)
        {
            _graphicsSettingsPanel.UnregisterCallback<ClickEvent>(HandleSettingsOverlayClicked);
        }
        if (_settingsCard != null)
        {
            _settingsCard.UnregisterCallback<ClickEvent>(HandleSettingsCardClicked);
        }
        if (_graphicsLowButton != null)
        {
            _graphicsLowButton.clicked -= HandleGraphicsLowButtonClicked;
        }
        if (_graphicsMediumButton != null)
        {
            _graphicsMediumButton.clicked -= HandleGraphicsMediumButtonClicked;
        }
        if (_shopButton != null)
        {
            _shopButton.clicked -= HandleShopButtonClicked;
        }
        if (_shopSkinsMenuButton != null)
        {
            _shopSkinsMenuButton.clicked -= HandleShopSkinsMenuButtonClicked;
        }
        if (_shopBackButton != null)
        {
            _shopBackButton.clicked -= HandleShopBackButtonClicked;
        }
        if (_shopCloseButton != null)
        {
            _shopCloseButton.clicked -= HandleShopCloseButtonClicked;
        }
        if (_shopActionButton != null)
        {
            _shopActionButton.clicked -= HandleShopActionButtonClicked;
        }
        if (_graphicsVolumeSlider != null)
        {
            _graphicsVolumeSlider.UnregisterValueChangedCallback(HandleGraphicsVolumeSliderChanged);
        }
        if (_comingSoonCloseButton != null)
        {
            _comingSoonCloseButton.clicked -= HandleComingSoonCloseButtonClicked;
        }
        UnbindShopPreviewEvents();
        UnbindPlaceholderActions();
        UnbindHoverEffects();
        _menuEventsBound = false;
    }

    private void HandleStartButtonClicked()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.Room != null)
        {
            OnGameStarted();
            return;
        }

        OnStartClicked();
    }

    private async void HandleSpectateButtonClicked()
    {
        _pendingSpectateJoin = true;

        if (NetworkManager.Instance != null && NetworkManager.Instance.Room != null)
        {
            ActivateSpectateMode();
            return;
        }

        string error = await NetworkManager.Instance.JoinOrCreateGame();
        if (string.IsNullOrEmpty(error))
        {
            SaveAndSyncSkin();
            ActivateSpectateMode();
            return;
        }

        _pendingSpectateJoin = false;
        ShowNotification($"Spectate Failed: {error}");
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
        SetSettingsPopupVisible(!_settingsPopupVisible);
    }

    private void HandleSettingsCloseButtonClicked()
    {
        SetSettingsPopupVisible(false);
    }

    private void HandleSettingsOverlayClicked(ClickEvent evt)
    {
        if (evt == null)
        {
            return;
        }

        SetSettingsPopupVisible(false);
    }

    private void HandleSettingsCardClicked(ClickEvent evt)
    {
        evt?.StopPropagation();
    }

    private void HandleGraphicsLowButtonClicked()
    {
        ApplyGraphicsQuality(WebGLPerformanceBootstrap.LowQualityName);
    }

    private void HandleGraphicsMediumButtonClicked()
    {
        ApplyGraphicsQuality(WebGLPerformanceBootstrap.MediumQualityName);
    }

    private void HandleGraphicsVolumeSliderChanged(ChangeEvent<int> evt)
    {
        AudioListener.volume = Mathf.Clamp01(evt.newValue / 10f);
    }

    private void HandlePlaceholderButtonClicked()
    {
        ShowNotification("This menu item is not wired yet.");
    }

    private void HandleComingSoonButtonClicked()
    {
        ShowComingSoonPopup("This feature is still being built.");
    }

    private void HandleComingSoonCloseButtonClicked()
    {
        HideComingSoonPopup();
    }

    private void HandleShopButtonClicked()
    {
        int skinCount = GetSkinCount();
        if (skinCount <= 0)
        {
            ShowNotification("No skins are configured for the shop.");
            return;
        }

        _shopSelectedSkinIndex = Mathf.Clamp(currentSkinIndex, 0, skinCount - 1);
        _shopPage = ShopPage.Home;
        SetShopVisible(true);
    }

    private void HandleShopSkinsMenuButtonClicked()
    {
        _shopPage = ShopPage.Skins;
        RefreshShopUi();
    }

    private void HandleShopBackButtonClicked()
    {
        _shopPage = ShopPage.Home;
        RefreshShopUi();
    }

    private void HandleShopCloseButtonClicked()
    {
        _shopPage = ShopPage.Home;
        SetShopVisible(false);
    }

    private void HandleShopActionButtonClicked()
    {
        TryPurchaseOrEquipSelectedSkin();
    }

    private void BindPlaceholderActions()
    {
        BindComingSoonAction(_inventoryButton);
        BindComingSoonAction(_shopDailyStoreButton);
        BindComingSoonAction(_shopEquipmentButton);
        BindComingSoonAction(_shopEmotesButton);
        BindComingSoonAction(_shopRobuxButton);
        BindSpectateAction();
        BindShopSkinActions();
    }

    private void UnbindPlaceholderActions()
    {
        UnbindComingSoonAction(_inventoryButton);
        UnbindComingSoonAction(_shopDailyStoreButton);
        UnbindComingSoonAction(_shopEquipmentButton);
        UnbindComingSoonAction(_shopEmotesButton);
        UnbindComingSoonAction(_shopRobuxButton);
        UnbindSpectateAction();
        UnbindShopSkinActions();
    }

    private void BindSpectateAction()
    {
        if (_spectateButton == null)
        {
            return;
        }

        _spectateButton.clicked += HandleSpectateButtonClicked;
    }

    private void UnbindSpectateAction()
    {
        if (_spectateButton == null)
        {
            return;
        }

        _spectateButton.clicked -= HandleSpectateButtonClicked;
    }

    private void BindComingSoonAction(UIToolkitButton button)
    {
        if (button == null)
        {
            return;
        }

        button.clicked += HandleComingSoonButtonClicked;
    }

    private void UnbindComingSoonAction(UIToolkitButton button)
    {
        if (button == null)
        {
            return;
        }

        button.clicked -= HandleComingSoonButtonClicked;
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

    private void BindShopPreviewEvents()
    {
        if (_shopPreviewFrame == null)
        {
            return;
        }

        _shopPreviewFrame.RegisterCallback<PointerDownEvent>(HandleShopPreviewPointerDown);
        _shopPreviewFrame.RegisterCallback<PointerMoveEvent>(HandleShopPreviewPointerMove);
        _shopPreviewFrame.RegisterCallback<PointerUpEvent>(HandleShopPreviewPointerUp);
        _shopPreviewFrame.RegisterCallback<PointerLeaveEvent>(HandleShopPreviewPointerLeave);
    }

    private void UnbindShopPreviewEvents()
    {
        if (_shopPreviewFrame == null)
        {
            return;
        }

        _shopPreviewFrame.UnregisterCallback<PointerDownEvent>(HandleShopPreviewPointerDown);
        _shopPreviewFrame.UnregisterCallback<PointerMoveEvent>(HandleShopPreviewPointerMove);
        _shopPreviewFrame.UnregisterCallback<PointerUpEvent>(HandleShopPreviewPointerUp);
        _shopPreviewFrame.UnregisterCallback<PointerLeaveEvent>(HandleShopPreviewPointerLeave);
    }

    private void HandleShopPreviewPointerDown(PointerDownEvent evt)
    {
        if (_shopPreviewFrame == null || !_shopVisible || _shopPreviewInstance == null)
        {
            return;
        }

        _isDraggingShopPreview = true;
        _shopPreviewLastPointerPosition = new Vector2(evt.position.x, evt.position.y);
        _shopPreviewFrame.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void HandleShopPreviewPointerMove(PointerMoveEvent evt)
    {
        if (!_isDraggingShopPreview || _shopPreviewInstance == null)
        {
            return;
        }

        Vector2 currentPointerPosition = new Vector2(evt.position.x, evt.position.y);
        Vector2 pointerDelta = currentPointerPosition - _shopPreviewLastPointerPosition;
        _shopPreviewLastPointerPosition = currentPointerPosition;
        _shopPreviewInstance.transform.Rotate(Vector3.up, -pointerDelta.x * shopPreviewDragSensitivity, Space.World);

        if (_shopPreviewCamera != null)
        {
            _shopPreviewCamera.Render();
        }

        evt.StopPropagation();
    }

    private void HandleShopPreviewPointerUp(PointerUpEvent evt)
    {
        ReleaseShopPreviewDrag(evt.pointerId);
    }

    private void HandleShopPreviewPointerLeave(PointerLeaveEvent evt)
    {
        ReleaseShopPreviewDrag(evt.pointerId);
    }

    private void ReleaseShopPreviewDrag(int pointerId)
    {
        _isDraggingShopPreview = false;
        if (_shopPreviewFrame != null && _shopPreviewFrame.HasPointerCapture(pointerId))
        {
            _shopPreviewFrame.ReleasePointer(pointerId);
        }
    }

    private void CacheShopSkinElements()
    {
        _shopSkinButtons.Clear();
        _shopSkinNameLabels.Clear();
        _shopSkinDescriptionLabels.Clear();
        _shopSkinStatusLabels.Clear();
        _shopSkinPriceLabels.Clear();

        int skinCount = GetSkinCount();
        for (int index = 0; index < skinCount; index++)
        {
            _shopSkinButtons.Add(_menuDocument.rootVisualElement?.Q<UIToolkitButton>($"shop-skin-card-{index}"));
            _shopSkinNameLabels.Add(_menuDocument.rootVisualElement?.Q<Label>($"shop-skin-name-{index}"));
            _shopSkinDescriptionLabels.Add(_menuDocument.rootVisualElement?.Q<Label>($"shop-skin-description-{index}"));
            _shopSkinStatusLabels.Add(_menuDocument.rootVisualElement?.Q<Label>($"shop-skin-status-{index}"));
            _shopSkinPriceLabels.Add(_menuDocument.rootVisualElement?.Q<Label>($"shop-skin-price-{index}"));
        }
    }

    private void BindShopSkinActions()
    {
        for (int index = 0; index < _shopSkinButtons.Count; index++)
        {
            UIToolkitButton button = _shopSkinButtons[index];
            if (button == null)
            {
                continue;
            }

            int capturedIndex = index;
            EventCallback<ClickEvent> callback = evt => HandleShopSkinCardClicked(capturedIndex);
            button.userData = callback;
            button.RegisterCallback<ClickEvent>(callback);
        }
    }

    private void UnbindShopSkinActions()
    {
        for (int index = 0; index < _shopSkinButtons.Count; index++)
        {
            UIToolkitButton button = _shopSkinButtons[index];
            if (button == null)
            {
                continue;
            }

            if (!(button.userData is EventCallback<ClickEvent> callback))
            {
                continue;
            }

            button.UnregisterCallback<ClickEvent>(callback);
            button.userData = null;
        }
    }

    private void HandleShopSkinCardClicked(int skinIndex)
    {
        _shopSelectedSkinIndex = skinIndex;
        RefreshShopUi();
    }

    private void ConfigureMenuButtonDescriptions()
    {
        SetMenuButtonDescription(_startButton, "Join the current game");
        SetMenuButtonDescription(_shopButton, "Browse the shop");
        SetMenuButtonDescription(_shopDailyStoreButton, "Browse the daily store");
        SetMenuButtonDescription(_shopEquipmentButton, "Check equipment");
        SetMenuButtonDescription(_shopSkinsMenuButton, "Open the character shop");
        SetMenuButtonDescription(_shopEmotesButton, "Browse emotes");
        SetMenuButtonDescription(_shopRobuxButton, "Open robux offers");
        SetMenuButtonDescription(_inventoryButton, "Open your inventory");
        SetMenuButtonDescription(_spectateButton, "Watch the current match");
        SetMenuButtonDescription(_settingsButton, "Adjust graphics and menu settings");

        _pendingHoverLabelText = DefaultMenuHoverText;
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
        RegisterHoverButton(_shopDailyStoreButton);
        RegisterHoverButton(_shopEquipmentButton);
        RegisterHoverButton(_shopSkinsMenuButton);
        RegisterHoverButton(_shopEmotesButton);
        RegisterHoverButton(_shopRobuxButton);
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
            ApplyButtonHoverVisual(button, false);
        }

        _hoverButtons.Clear();
        _buttonCurrentScales.Clear();
        _buttonTargetScales.Clear();
        _pendingHoverLabelText = DefaultMenuHoverText;
        _targetHoverLabelOpacity = 0f;
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
        _buttonCurrentScales[button] = Vector3.one;
        _buttonTargetScales[button] = Vector3.one;
    }

    private void HandleMenuButtonPointerEnter(PointerEnterEvent evt)
    {
        UIToolkitButton button = evt.currentTarget as UIToolkitButton;
        if (button == null)
        {
            return;
        }

        _buttonTargetScales[button] = GetHoverScaleVector(button);
        ApplyButtonHoverVisual(button, true);

        _pendingHoverLabelText = button.userData as string ?? DefaultMenuHoverText;
        if (_menuHoverLabel != null)
        {
            _menuHoverLabel.text = _pendingHoverLabelText;
        }
        _targetHoverLabelOpacity = 1f;
    }

    private void HandleMenuButtonPointerLeave(PointerLeaveEvent evt)
    {
        UIToolkitButton button = evt.currentTarget as UIToolkitButton;
        if (button == null)
        {
            return;
        }

        _buttonTargetScales[button] = Vector3.one;
        ApplyButtonHoverVisual(button, false);
        _pendingHoverLabelText = DefaultMenuHoverText;
        _targetHoverLabelOpacity = 0f;
    }

    private static Scale GetHoverScale(UIToolkitButton button)
    {
        if (button == null)
        {
            return HoverButtonScale;
        }

        if (string.Equals(button.name, "start-button", StringComparison.Ordinal))
        {
            return LargeHoverButtonScale;
        }

        if (string.Equals(button.name, "shop-button", StringComparison.Ordinal)
            || string.Equals(button.name, "inventory-button", StringComparison.Ordinal))
        {
            return FeaturedSideHoverButtonScale;
        }

        return HoverButtonScale;
    }

    private static Vector3 GetHoverScaleVector(UIToolkitButton button)
    {
        if (button == null)
        {
            return Vector3.one;
        }

        if (string.Equals(button.name, "start-button", StringComparison.Ordinal))
        {
            return new Vector3(1.03f, 1.03f, 1f);
        }

        if (string.Equals(button.name, "shop-button", StringComparison.Ordinal)
            || string.Equals(button.name, "inventory-button", StringComparison.Ordinal))
        {
            return new Vector3(1.18f, 1.18f, 1f);
        }

        return new Vector3(1.02f, 1.02f, 1f);
    }

    private void ResetHoverLabelVisual()
    {
        _currentHoverLabelOpacity = 0f;
        _targetHoverLabelOpacity = 0f;

        if (_menuHoverLabel != null)
        {
            _menuHoverLabel.text = _pendingHoverLabelText;
            _menuHoverLabel.style.opacity = 0f;
        }
    }

    private void ShowComingSoonPopup(string message)
    {
        if (_comingSoonMessageLabel != null)
        {
            _comingSoonMessageLabel.text = string.IsNullOrWhiteSpace(message)
                ? "This feature is still being built."
                : message;
        }

        if (_comingSoonOverlay != null)
        {
            _comingSoonOverlay.style.display = DisplayStyle.Flex;
        }
    }

    private void HideComingSoonPopup()
    {
        if (_comingSoonOverlay != null)
        {
            _comingSoonOverlay.style.display = DisplayStyle.None;
        }
    }

    private void ApplyMenuArt()
    {
        if (_startButton == null)
        {
            return;
        }

        if (s_joinGameCardTexture == null)
        {
            s_joinGameCardTexture = Resources.Load<Texture2D>(JoinGameCardResourcePath);
        }

        if (s_shopCardTexture == null)
        {
            s_shopCardTexture = Resources.Load<Texture2D>(ShopCardResourcePath);
        }

        if (s_inventoryCardTexture == null)
        {
            s_inventoryCardTexture = Resources.Load<Texture2D>(InventoryCardResourcePath);
        }

        if (s_joinGameCardTexture != null)
        {
            _startButton.style.backgroundImage = new StyleBackground(s_joinGameCardTexture);
            _startButton.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        }

        if (_shopButton != null && s_shopCardTexture != null)
        {
            _shopButton.style.backgroundImage = new StyleBackground(s_shopCardTexture);
            _shopButton.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        }

        if (_inventoryButton != null && s_inventoryCardTexture != null)
        {
            _inventoryButton.style.backgroundImage = new StyleBackground(s_inventoryCardTexture);
            _inventoryButton.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        }
    }

    private void UpdateHoverLabelFade()
    {
        if (_menuHoverLabel == null)
        {
            return;
        }

        float nextOpacity = Mathf.MoveTowards(_currentHoverLabelOpacity, _targetHoverLabelOpacity, Time.unscaledDeltaTime * MenuHoverLabelFadeSpeed);
        if (Mathf.Approximately(nextOpacity, _currentHoverLabelOpacity))
        {
            return;
        }

        _currentHoverLabelOpacity = nextOpacity;

        if (_currentHoverLabelOpacity <= 0.0001f)
        {
            _menuHoverLabel.text = _pendingHoverLabelText;
        }

        _menuHoverLabel.style.opacity = _currentHoverLabelOpacity;
    }

    private void UpdateMenuButtonScaleAnimation()
    {
        if (_hoverButtons.Count == 0)
        {
            return;
        }

        float t = 1f - Mathf.Exp(-MenuButtonScaleLerpSpeed * Time.unscaledDeltaTime);
        for (int i = 0; i < _hoverButtons.Count; i++)
        {
            UIToolkitButton button = _hoverButtons[i];
            if (button == null)
            {
                continue;
            }

            Vector3 currentScale = _buttonCurrentScales.TryGetValue(button, out Vector3 cachedCurrentScale)
                ? cachedCurrentScale
                : Vector3.one;
            Vector3 targetScale = _buttonTargetScales.TryGetValue(button, out Vector3 cachedTargetScale)
                ? cachedTargetScale
                : Vector3.one;

            Vector3 nextScale = Vector3.Lerp(currentScale, targetScale, t);
            if ((targetScale - nextScale).sqrMagnitude <= 0.00001f)
            {
                nextScale = targetScale;
            }

            _buttonCurrentScales[button] = nextScale;
            button.style.scale = new StyleScale(new Scale(nextScale));
        }
    }

    private static void ApplyButtonHoverVisual(UIToolkitButton button, bool hovered)
    {
        if (button == null)
        {
            return;
        }

        Color borderColor = hovered ? new Color(1f, 1f, 1f, 0.95f) : GetDefaultButtonBorderColor(button.name);
        Color backgroundColor = hovered ? GetHoveredButtonBackgroundColor(button.name) : GetDefaultButtonBackgroundColor(button.name);

        button.style.borderLeftColor = borderColor;
        button.style.borderRightColor = borderColor;
        button.style.borderTopColor = borderColor;
        button.style.borderBottomColor = borderColor;
        button.style.backgroundColor = backgroundColor;
    }

    private static Color GetDefaultButtonBorderColor(string buttonName)
    {
        return buttonName switch
        {
            "start-button" => new Color(239f / 255f, 186f / 255f, 101f / 255f, 0.84f),
            "shop-button" => new Color(84f / 255f, 223f / 255f, 83f / 255f, 0.72f),
            "inventory-button" => new Color(240f / 255f, 101f / 255f, 111f / 255f, 0.72f),
            "spectate-button" => new Color(154f / 255f, 124f / 255f, 1f, 0.5f),
            "settings-button" => new Color(1f, 1f, 1f, 0.18f),
            "graphics-low-button" => new Color(1f, 1f, 1f, 0f),
            "graphics-medium-button" => new Color(1f, 1f, 1f, 0f),
            _ => new Color(1f, 1f, 1f, 0.2f),
        };
    }

    private static Color GetDefaultButtonBackgroundColor(string buttonName)
    {
        return buttonName switch
        {
            "start-button" => new Color(0f, 0f, 0f, 0.08f),
            "shop-button" => new Color(16f / 255f, 24f / 255f, 20f / 255f, 0.9f),
            "inventory-button" => new Color(26f / 255f, 14f / 255f, 16f / 255f, 0.9f),
            "spectate-button" => new Color(18f / 255f, 18f / 255f, 20f / 255f, 0.88f),
            "settings-button" => new Color(8f / 255f, 12f / 255f, 18f / 255f, 0.9f),
            "graphics-low-button" => new Color(58f / 255f, 92f / 255f, 44f / 255f, 0.95f),
            "graphics-medium-button" => new Color(67f / 255f, 84f / 255f, 122f / 255f, 0.95f),
            _ => new Color(0.1f, 0.1f, 0.1f, 0.9f),
        };
    }

    private static Color GetHoveredButtonBackgroundColor(string buttonName)
    {
        return buttonName switch
        {
            "start-button" => new Color(0f, 0f, 0f, 0.02f),
            "shop-button" => new Color(20f / 255f, 30f / 255f, 24f / 255f, 0.96f),
            "inventory-button" => new Color(31f / 255f, 18f / 255f, 21f / 255f, 0.96f),
            "spectate-button" => new Color(24f / 255f, 22f / 255f, 30f / 255f, 0.95f),
            "settings-button" => new Color(16f / 255f, 18f / 255f, 24f / 255f, 0.95f),
            "graphics-low-button" => new Color(78f / 255f, 118f / 255f, 62f / 255f, 0.98f),
            "graphics-medium-button" => new Color(86f / 255f, 103f / 255f, 142f / 255f, 0.98f),
            _ => new Color(0.16f, 0.16f, 0.16f, 0.95f),
        };
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

        if (_graphicsVolumeSlider != null)
        {
            int volumeValue = Mathf.RoundToInt(Mathf.Clamp01(AudioListener.volume) * 10f);
            _graphicsVolumeSlider.SetValueWithoutNotify(volumeValue);
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

    private void SetSettingsPopupVisible(bool visible)
    {
        _settingsPopupVisible = visible;

        if (_graphicsSettingsPanel != null)
        {
            _graphicsSettingsPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (visible)
        {
            RefreshGraphicsSettingsUi();
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

    private void SetLocalPlayerSpectating(bool enabled)
    {
        GameObject localPlayer = GameObject.Find("LocalPlayer");
        if (localPlayer == null)
        {
            return;
        }

        PlayerController controller = localPlayer.GetComponent<PlayerController>();
        if (controller == null)
        {
            return;
        }

        PlayerLocomotionInput input = localPlayer.GetComponent<PlayerLocomotionInput>();

        if (enabled)
        {
            if (input != null)
            {
                input.InputEnabled = true;
            }

            if (!controller.IsSpectating())
            {
                controller.EnterSpectateMode();
            }
        }
        else
        {
            if (input != null)
            {
                input.InputEnabled = false;
            }

            if (controller.IsSpectating())
            {
                controller.ExitSpectateMode();
            }
        }
    }

    private void ActivateSpectateMode()
    {
        _pendingSpectateJoin = false;
        _isSpectatingFromMenu = true;
        hasAutoReadiedCurrentRoom = false;
        SetShopVisible(false);

        if (NetworkManager.Instance != null && NetworkManager.Instance.Room != null)
        {
            NetworkManager.Instance.SendSpectatorState(true);
            NetworkManager.Instance.SendReadyState(false);
        }

        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }

        if (lobbyCamera != null)
        {
            lobbyCamera.gameObject.SetActive(false);
        }

        SetLocalPlayerInput(true);
        SetLocalPlayerSpectating(true);

        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
    }

    public void HandleStartGameSignal()
    {
        if (_pendingSpectateJoin || _isSpectatingFromMenu)
        {
            ActivateSpectateMode();
            return;
        }

        if (menuPanel != null && menuPanel.activeSelf && !hasAutoReadiedCurrentRoom)
        {
            return;
        }

        OnGameStarted();
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
        currentSkinIndex = GetValidOwnedSkinIndex(currentSkinIndex);
        PlayerPrefs.SetInt(SelectedSkinPlayerPrefsKey, currentSkinIndex);
        PlayerPrefs.Save();
       
       _shopSelectedSkinIndex = currentSkinIndex;
       UpdateSkinUI();
       RefreshShopUi();

       if (NetworkManager.Instance != null && NetworkManager.Instance.Room != null)
       {
           NetworkManager.Instance.Room.Send("setSkin", currentSkinIndex);
       }
    }

    private void UpdateSkinUI()
    {
        if (skinRegistry == null || skinRegistry.skins.Length == 0) return;

       currentSkinIndex = GetValidOwnedSkinIndex(currentSkinIndex);

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

       RefreshShopPreviewVisual();
    }

    private void EnsureEconomyDefaults()
    {
        if (!PlayerPrefs.HasKey(PlayerMoneyPlayerPrefsKey))
        {
            PlayerPrefs.SetInt(PlayerMoneyPlayerPrefsKey, StartingMoneyAmount);
        }

        int skinCount = GetSkinCount();
        for (int index = 0; index < skinCount; index++)
        {
            string key = GetOwnedSkinKey(index);
            if (!PlayerPrefs.HasKey(key))
            {
                bool defaultOwned = index == DefaultOwnedSkinIndex
                    || (skinRegistry != null
                        && skinRegistry.skins != null
                        && index < skinRegistry.skins.Length
                        && skinRegistry.skins[index].unlockedByDefault);
                PlayerPrefs.SetInt(key, defaultOwned ? 1 : 0);
            }
        }

        PlayerPrefs.Save();
    }

    private int GetPlayerMoney()
    {
        return PlayerPrefs.GetInt(PlayerMoneyPlayerPrefsKey, StartingMoneyAmount);
    }

    private void SetPlayerMoney(int amount)
    {
        PlayerPrefs.SetInt(PlayerMoneyPlayerPrefsKey, Mathf.Max(0, amount));
        PlayerPrefs.Save();
        UpdateMoneyUI();
    }

    private void UpdateMoneyUI()
    {
        if (_moneyLabel != null)
        {
            _moneyLabel.text = $"${GetPlayerMoney()}";
        }
    }

    private int GetSkinCount()
    {
        return skinRegistry != null && skinRegistry.skins != null ? skinRegistry.skins.Length : 0;
    }

    private string GetOwnedSkinKey(int skinIndex)
    {
        return $"{OwnedSkinPlayerPrefsPrefix}{skinIndex}";
    }

    private bool IsSkinOwned(int skinIndex)
    {
        if (skinIndex < 0 || skinIndex >= GetSkinCount())
        {
            return false;
        }

        return PlayerPrefs.GetInt(GetOwnedSkinKey(skinIndex), skinIndex == DefaultOwnedSkinIndex ? 1 : 0) == 1;
    }

    private void SetSkinOwned(int skinIndex, bool owned)
    {
        if (skinIndex < 0 || skinIndex >= GetSkinCount())
        {
            return;
        }

        PlayerPrefs.SetInt(GetOwnedSkinKey(skinIndex), owned ? 1 : 0);
        PlayerPrefs.Save();
    }

    private int GetValidOwnedSkinIndex(int preferredIndex)
    {
        int skinCount = GetSkinCount();
        if (skinCount <= 0)
        {
            return 0;
        }

        if (preferredIndex >= 0 && preferredIndex < skinCount && IsSkinOwned(preferredIndex))
        {
            return preferredIndex;
        }

        for (int index = 0; index < skinCount; index++)
        {
            if (IsSkinOwned(index))
            {
                return index;
            }
        }

        return 0;
    }

    private void SetShopVisible(bool visible)
    {
        _shopVisible = visible;
        _isDraggingShopPreview = false;

        if (_shopOverlay != null)
        {
            _shopOverlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (_menuCenterColumn != null)
        {
            _menuCenterColumn.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (_menuRightRail != null)
        {
            _menuRightRail.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (_walletPill != null)
        {
            _walletPill.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (!visible)
        {
            return;
        }

        SetSettingsPopupVisible(false);
        _shopSelectedSkinIndex = Mathf.Clamp(_shopSelectedSkinIndex, 0, Mathf.Max(0, GetSkinCount() - 1));
        RefreshShopUi();
        EnsureShopPreviewObjects();
        RefreshShopPreviewVisual();
    }

    private void RefreshShopUi()
    {
        if (skinRegistry == null || skinRegistry.skins == null || skinRegistry.skins.Length == 0)
        {
            return;
        }

        _shopSelectedSkinIndex = Mathf.Clamp(_shopSelectedSkinIndex, 0, skinRegistry.skins.Length - 1);
        UpdateMoneyUI();
        bool showingSkinPage = _shopPage == ShopPage.Skins;

        if (_shopHomeView != null)
        {
            _shopHomeView.style.display = showingSkinPage ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (_shopSkinsView != null)
        {
            _shopSkinsView.style.display = showingSkinPage ? DisplayStyle.Flex : DisplayStyle.None;
        }

        for (int index = 0; index < skinRegistry.skins.Length && index < _shopSkinButtons.Count; index++)
        {
            SkinEntry entry = skinRegistry.skins[index];
            bool owned = IsSkinOwned(index);
            bool equipped = currentSkinIndex == index;
            bool selected = _shopSelectedSkinIndex == index;

            if (_shopSkinNameLabels[index] != null)
            {
                _shopSkinNameLabels[index].text = string.IsNullOrWhiteSpace(entry.skinName) ? $"Skin {index + 1}" : entry.skinName;
            }

            if (_shopSkinDescriptionLabels[index] != null)
            {
                _shopSkinDescriptionLabels[index].text = owned
                    ? (equipped ? "Currently equipped" : "Ready to equip")
                    : "Purchase to unlock";
            }

            if (_shopSkinStatusLabels[index] != null)
            {
                _shopSkinStatusLabels[index].text = equipped ? "Equipped" : (owned ? "Owned" : "Locked");
                _shopSkinStatusLabels[index].style.color = equipped
                    ? new Color(255f / 255f, 235f / 255f, 168f / 255f)
                    : (owned ? new Color(114f / 255f, 228f / 255f, 123f / 255f) : new Color(240f / 255f, 154f / 255f, 173f / 255f));
            }

            if (_shopSkinPriceLabels[index] != null)
            {
                _shopSkinPriceLabels[index].text = entry.price <= 0 ? "FREE" : $"${entry.price}";
            }

            if (_shopSkinButtons[index] != null)
            {
                Color selectedBorderColor = GetShopCardBorderColor(index, selected);
                _shopSkinButtons[index].style.borderLeftColor = selectedBorderColor;
                _shopSkinButtons[index].style.borderRightColor = selectedBorderColor;
                _shopSkinButtons[index].style.borderTopColor = selectedBorderColor;
                _shopSkinButtons[index].style.borderBottomColor = selectedBorderColor;
                _shopSkinButtons[index].style.backgroundColor = GetShopCardBackgroundColor(index, selected);
            }
        }

        SkinEntry selectedSkin = skinRegistry.skins[_shopSelectedSkinIndex];
        bool selectedOwned = IsSkinOwned(_shopSelectedSkinIndex);
        bool selectedEquipped = currentSkinIndex == _shopSelectedSkinIndex;
        int selectedPrice = Mathf.Max(0, selectedSkin.price);
        bool canAfford = GetPlayerMoney() >= selectedPrice;

        if (_shopSelectedSkinLabel != null)
        {
            _shopSelectedSkinLabel.text = string.IsNullOrWhiteSpace(selectedSkin.skinName) ? $"Skin {_shopSelectedSkinIndex + 1}" : selectedSkin.skinName;
        }

        if (_shopSelectedSkinSubtitle != null)
        {
            _shopSelectedSkinSubtitle.text = selectedOwned
                ? (selectedEquipped ? "This skin is active on your player." : "You already own this skin.")
                : (canAfford ? "You can buy this skin right now." : "You need more cash to unlock this skin.");
        }

        if (_shopSelectedPriceLabel != null)
        {
            _shopSelectedPriceLabel.text = selectedPrice <= 0 ? "FREE" : $"${selectedPrice}";
        }

        if (_shopSelectedStateLabel != null)
        {
            _shopSelectedStateLabel.text = selectedEquipped ? "Equipped" : (selectedOwned ? "Owned" : "Locked");
            _shopSelectedStateLabel.style.color = selectedEquipped
                ? new Color(255f / 255f, 235f / 255f, 168f / 255f)
                : (selectedOwned ? new Color(114f / 255f, 228f / 255f, 123f / 255f) : new Color(240f / 255f, 154f / 255f, 173f / 255f));
        }

        if (_shopActionButton != null)
        {
            _shopActionButton.style.display = showingSkinPage ? DisplayStyle.Flex : DisplayStyle.None;
            if (selectedEquipped)
            {
                _shopActionButton.text = "EQUIPPED";
                _shopActionButton.SetEnabled(false);
            }
            else if (selectedOwned)
            {
                _shopActionButton.text = "EQUIP";
                _shopActionButton.SetEnabled(true);
            }
            else
            {
                _shopActionButton.text = canAfford ? $"BUY ${selectedPrice}" : "NOT ENOUGH CASH";
                _shopActionButton.SetEnabled(canAfford);
            }
        }

        RefreshShopPreviewVisual();
    }

    private void TryPurchaseOrEquipSelectedSkin()
    {
        if (skinRegistry == null || skinRegistry.skins == null || _shopSelectedSkinIndex < 0 || _shopSelectedSkinIndex >= skinRegistry.skins.Length)
        {
            return;
        }

        if (IsSkinOwned(_shopSelectedSkinIndex))
        {
            currentSkinIndex = _shopSelectedSkinIndex;
            SaveAndSyncSkin();
            ShowNotification($"Equipped {skinRegistry.skins[_shopSelectedSkinIndex].skinName}");
            return;
        }

        SkinEntry selectedSkin = skinRegistry.skins[_shopSelectedSkinIndex];
        int price = Mathf.Max(0, selectedSkin.price);
        int currentMoney = GetPlayerMoney();
        if (currentMoney < price)
        {
            ShowNotification("Not enough cash for that skin.");
            RefreshShopUi();
            return;
        }

        SetPlayerMoney(currentMoney - price);
        SetSkinOwned(_shopSelectedSkinIndex, true);
        currentSkinIndex = _shopSelectedSkinIndex;
        SaveAndSyncSkin();
        ShowNotification($"Bought {selectedSkin.skinName} for ${price}");
    }

    private Color GetShopCardBorderColor(int index, bool selected)
    {
        if (selected)
        {
            return new Color(1f, 245f / 255f, 216f / 255f, 0.95f);
        }

        return index == 0
            ? new Color(110f / 255f, 201f / 255f, 106f / 255f, 0.7f)
            : new Color(222f / 255f, 123f / 255f, 182f / 255f, 0.7f);
    }

    private Color GetShopCardBackgroundColor(int index, bool selected)
    {
        if (selected)
        {
            return index == 0
                ? new Color(28f / 255f, 41f / 255f, 30f / 255f, 0.96f)
                : new Color(40f / 255f, 26f / 255f, 35f / 255f, 0.96f);
        }

        return index == 0
            ? new Color(18f / 255f, 20f / 255f, 18f / 255f, 0.9f)
            : new Color(24f / 255f, 17f / 255f, 22f / 255f, 0.9f);
    }

    private void EnsureShopPreviewObjects()
    {
        if (_shopPreviewFrame == null || _shopPreviewRenderTexture != null)
        {
            return;
        }

        _shopPreviewRenderTexture = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGB32)
        {
            name = "ShopPreviewRT"
        };
        _shopPreviewRenderTexture.Create();
        _shopPreviewFrame.image = _shopPreviewRenderTexture;

        _shopPreviewRoot = new GameObject("ShopPreviewRoot");
        _shopPreviewRoot.hideFlags = HideFlags.HideAndDontSave;
        _shopPreviewRoot.transform.position = new Vector3(1000f, -1000f, 1000f);

        GameObject cameraObject = new GameObject("ShopPreviewCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(_shopPreviewRoot.transform, false);
        cameraObject.transform.localPosition = shopPreviewCameraPosition;
        cameraObject.transform.LookAt(_shopPreviewRoot.transform.position + Vector3.up * 1.0f);
        _shopPreviewCamera = cameraObject.AddComponent<Camera>();
        _shopPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
        _shopPreviewCamera.backgroundColor = shopPreviewCameraBackground;
        _shopPreviewCamera.cullingMask = ~0;
        _shopPreviewCamera.nearClipPlane = 0.01f;
        _shopPreviewCamera.farClipPlane = 20f;
        _shopPreviewCamera.fieldOfView = 28f;
        _shopPreviewCamera.targetTexture = _shopPreviewRenderTexture;
        _shopPreviewCamera.enabled = false;

        GameObject previewPrefab = shopPreviewPrefab;
        if (previewPrefab == null && NetworkManager.Instance != null)
        {
            previewPrefab = NetworkManager.Instance.playerPrefab;
        }

        if (previewPrefab == null)
        {
            return;
        }

        _shopPreviewInstance = Instantiate(previewPrefab, _shopPreviewRoot.transform);
        _shopPreviewInstance.name = "ShopPreviewPlayer";
        _shopPreviewInstance.transform.localPosition = shopPreviewModelPosition;
        _shopPreviewInstance.transform.localRotation = Quaternion.Euler(shopPreviewModelEuler);
        _shopPreviewInstance.transform.localScale = Vector3.one;

        DisablePreviewComponents(_shopPreviewInstance);

        PlayerAppearance previewAppearance = _shopPreviewInstance.GetComponent<PlayerAppearance>();
        if (previewAppearance != null)
        {
            _shopPreviewRenderers = previewAppearance.GetTargetRenderers();
        }
        else
        {
            _shopPreviewRenderers = _shopPreviewInstance.GetComponentsInChildren<Renderer>(true);
        }

        _shopPreviewAnimator = _shopPreviewInstance.GetComponentInChildren<Animator>(true);
        if (_shopPreviewAnimator != null)
        {
            _shopPreviewAnimator.enabled = true;
            _shopPreviewAnimator.speed = 1f;
            _shopPreviewAnimator.Update(0f);
        }
    }

    private void DisablePreviewComponents(GameObject previewObject)
    {
        if (previewObject == null)
        {
            return;
        }

        Behaviour[] behaviours = previewObject.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour is Animator)
            {
                continue;
            }

            behaviour.enabled = false;
        }

        Rigidbody[] rigidbodies = previewObject.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (rigidbodies[i] != null)
            {
                rigidbodies[i].isKinematic = true;
            }
        }

        Collider[] colliders = previewObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        Camera[] cameras = previewObject.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
            {
                cameras[i].gameObject.SetActive(false);
            }
        }

        AudioListener[] listeners = previewObject.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != null)
            {
                listeners[i].enabled = false;
            }
        }
    }

    private void RefreshShopPreviewVisual()
    {
        if (_shopPreviewInstance == null || skinRegistry == null || skinRegistry.skins == null || skinRegistry.skins.Length == 0)
        {
            return;
        }

        int previewSkinIndex = Mathf.Clamp(_shopSelectedSkinIndex, 0, skinRegistry.skins.Length - 1);
        PlayerAppearance.ApplySkinToRenderers(skinRegistry, previewSkinIndex, _shopPreviewRenderers);

        if (_shopPreviewAnimator != null)
        {
            _shopPreviewAnimator.enabled = true;
            _shopPreviewAnimator.speed = 1f;
            _shopPreviewAnimator.Update(0f);
        }

        if (_shopPreviewCamera != null)
        {
            _shopPreviewCamera.Render();
        }
    }

    private void UpdateShopPreviewAnimation()
    {
        if (!_shopVisible || _shopPreviewInstance == null || _shopPreviewAnimator == null)
        {
            return;
        }

        _shopPreviewAnimator.enabled = true;
        _shopPreviewAnimator.Update(Time.unscaledDeltaTime);

        if (_shopPreviewCamera != null)
        {
            _shopPreviewCamera.Render();
        }
    }

    private void DestroyShopPreviewObjects()
    {
        if (_shopPreviewInstance != null)
        {
            Destroy(_shopPreviewInstance);
        }

        if (_shopPreviewRoot != null)
        {
            Destroy(_shopPreviewRoot);
        }

        if (_shopPreviewRenderTexture != null)
        {
            _shopPreviewRenderTexture.Release();
            Destroy(_shopPreviewRenderTexture);
        }

        _shopPreviewInstance = null;
        _shopPreviewRoot = null;
        _shopPreviewCamera = null;
        _shopPreviewRenderTexture = null;
        _shopPreviewRenderers = Array.Empty<Renderer>();
        _shopPreviewAnimator = null;
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
