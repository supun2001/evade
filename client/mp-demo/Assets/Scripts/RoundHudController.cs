using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[DefaultExecutionOrder(200)]
public class RoundHudController : MonoBehaviour
{
    private const string MainMenuMusicResourcePath = "SFX/MainMenu";
    private const string IntermissionMusicResourcePath = "SFX/InGame";
    private const string InGameMusicResourcePath = "SFX/InGame";
    private const string RoundStartSfxResourcePath = "SFX/RoundStart";
    private const string GreenScoreboardIconResourcePath = "UI/green_icon";
    private const string PinkScoreboardIconResourcePath = "UI/pink_icon";

    private NetworkManager _networkManager;
    private UIDocument _hudDocument;
    private VisualElement _hudRoot;
    private LobbyUI _lobbyUi;
    private AudioSource _musicAudioSource;
    private AudioSource _oneShotAudioSource;
    private AudioClip _mainMenuMusicClip;
    private AudioClip _intermissionMusicClip;
    private AudioClip _inGameMusicClip;
    private AudioClip _roundStartClip;
    private AudioClip _activeLoopClip;
    private Texture2D _greenScoreboardIcon;
    private Texture2D _pinkScoreboardIcon;

    private VisualElement _roundPhaseContainer;
    private Label _roundPhaseTitleLabel;
    private Label _roundPhaseTimerLabel;
    private VisualElement _roundAnnouncementContainer;
    private Label _roundAnnouncementTitleLabel;
    private Label _roundAnnouncementSubtitleLabel;
    private VisualElement _nextbotThreatContainer;
    private Label _nextbotThreatTitleLabel;
    private Label _nextbotThreatValueLabel;
    private VisualElement _roundResultsOverlay;
    private Label _roundResultsTitleLabel;
    private Label _roundResultsBestTimeValue;
    private Label _roundResultsRevivesValue;
    private Label _roundResultsDownedValue;
    private VisualElement _roundResultsLeaderboardList;
    private VisualElement _tabScoreboardOverlay;
    private ScrollView _tabScoreboardList;
    private Label _tabScoreboardMapLabel;
    private Label _tabScoreboardPhaseLabel;
    private Label _tabScoreboardModeLabel;
    private Label _tabScoreboardPlayerCountLabel;
    private VisualElement _mapVoteOverlay;
    private VisualElement _mapVoteContent;
    private Label _mapVoteTitleLabel;
    private Label _mapVoteTimerLabel;
    private VisualElement _mapVoteCardView;
    private VisualElement _mapVoteGrid;
    private VisualElement _mapVoteResultsView;
    private Label _mapVoteWinningLabel;
    private VisualElement _mapVoteResultsList;
    private Label _mapVoteStatusLabel;
    private readonly Dictionary<string, Button> _mapVoteButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, Label> _mapVoteCountLabels = new Dictionary<string, Label>();
    private readonly Dictionary<string, Label> _mapVoteVotedLabels = new Dictionary<string, Label>();
    private readonly Dictionary<string, Label> _mapVoteResultNameLabels = new Dictionary<string, Label>();
    private readonly Dictionary<string, Label> _mapVoteResultPercentLabels = new Dictionary<string, Label>();
    private readonly Dictionary<string, VisualElement> _mapVoteResultFillBars = new Dictionary<string, VisualElement>();
    private readonly Dictionary<string, Label> _mapVoteResultCountLabels = new Dictionary<string, Label>();

    private RoundPhaseMessageData _currentPhase;
    private float _phaseEndsAtUnscaledTime;
    private string _announcementTitle = string.Empty;
    private string _announcementSubtitle = string.Empty;
    private float _announcementHideAt;
    private RoundResultsMessageData _results;
    private bool _showResults;
    private MapVoteStateMessageData _currentMapVote;
    private MapVoteCandidateMessageData[] _lastMapVoteCandidates = Array.Empty<MapVoteCandidateMessageData>();
    private float _mapVoteEndsAtUnscaledTime;
    private string _localVotedMapId = string.Empty;
    private bool _showMapVoteResultsView;

    private void Awake()
    {
        _mainMenuMusicClip = Resources.Load<AudioClip>(MainMenuMusicResourcePath);
        _intermissionMusicClip = Resources.Load<AudioClip>(IntermissionMusicResourcePath);
        _inGameMusicClip = Resources.Load<AudioClip>(InGameMusicResourcePath);
        _roundStartClip = Resources.Load<AudioClip>(RoundStartSfxResourcePath);
        _greenScoreboardIcon = Resources.Load<Texture2D>(GreenScoreboardIconResourcePath);
        _pinkScoreboardIcon = Resources.Load<Texture2D>(PinkScoreboardIconResourcePath);

        _musicAudioSource = gameObject.AddComponent<AudioSource>();
        _musicAudioSource.playOnAwake = false;
        _musicAudioSource.loop = true;
        _musicAudioSource.spatialBlend = 0f;
        _musicAudioSource.dopplerLevel = 0f;
        _musicAudioSource.volume = 0.65f;

        _oneShotAudioSource = gameObject.AddComponent<AudioSource>();
        _oneShotAudioSource.playOnAwake = false;
        _oneShotAudioSource.loop = false;
        _oneShotAudioSource.spatialBlend = 0f;
        _oneShotAudioSource.dopplerLevel = 0f;
        _oneShotAudioSource.volume = 1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<RoundHudController>() != null)
        {
            return;
        }

        GameObject hudObject = new GameObject("RoundHudController");
        hudObject.AddComponent<RoundHudController>();
        DontDestroyOnLoad(hudObject);
    }

    private void Update()
    {
        if (_networkManager != NetworkManager.Instance)
        {
            RebindNetworkManager(NetworkManager.Instance);
        }

        if (_lobbyUi == null)
        {
            _lobbyUi = FindFirstObjectByType<LobbyUI>();
        }

        RefreshHudBindings();
        UpdateAnnouncementLifetime();
        HandleResultsCloseInput();
        RefreshTabScoreboardDisplay();
        RefreshBackgroundMusic();
        HandleMapVoteInput();
        RefreshPhaseDisplay();
        RefreshAnnouncementDisplay();
        RefreshNextbotThreatDisplay();
        RefreshResultsDisplay();
        RefreshMapVoteDisplay();
    }

    private void OnDestroy()
    {
        RebindNetworkManager(null);
    }

    private void RebindNetworkManager(NetworkManager manager)
    {
        if (_networkManager != null)
        {
            _networkManager.RoundPhaseChanged -= HandleRoundPhaseChanged;
            _networkManager.RoundAnnouncementReceived -= HandleRoundAnnouncementReceived;
            _networkManager.RoundResultsReceived -= HandleRoundResultsReceived;
            _networkManager.MapVoteStateReceived -= HandleMapVoteStateReceived;
            _networkManager.MapSelectedReceived -= HandleMapSelectedReceived;
            _networkManager.RoomLeftEvent -= HandleRoomLeft;
        }

        _networkManager = manager;

        if (_networkManager != null)
        {
            _networkManager.RoundPhaseChanged += HandleRoundPhaseChanged;
            _networkManager.RoundAnnouncementReceived += HandleRoundAnnouncementReceived;
            _networkManager.RoundResultsReceived += HandleRoundResultsReceived;
            _networkManager.MapVoteStateReceived += HandleMapVoteStateReceived;
            _networkManager.MapSelectedReceived += HandleMapSelectedReceived;
            _networkManager.RoomLeftEvent += HandleRoomLeft;
        }
    }

    private void RefreshHudBindings()
    {
        UIDocument currentDocument = GetLocalHudDocument();
        if (currentDocument == _hudDocument && _hudRoot != null)
        {
            return;
        }

        _hudDocument = currentDocument;
        _hudRoot = _hudDocument != null ? _hudDocument.rootVisualElement : null;

        _roundPhaseContainer = _hudRoot?.Q<VisualElement>("round-phase-container");
        _roundPhaseTitleLabel = _hudRoot?.Q<Label>("round-phase-title-label");
        _roundPhaseTimerLabel = _hudRoot?.Q<Label>("round-phase-timer-label");
        _roundAnnouncementContainer = _hudRoot?.Q<VisualElement>("round-announcement-container");
        _roundAnnouncementTitleLabel = _hudRoot?.Q<Label>("round-announcement-title-label");
        _roundAnnouncementSubtitleLabel = _hudRoot?.Q<Label>("round-announcement-subtitle-label");
        _nextbotThreatContainer = _hudRoot?.Q<VisualElement>("nextbot-threat-container");
        _nextbotThreatTitleLabel = _hudRoot?.Q<Label>("nextbot-threat-title-label");
        _nextbotThreatValueLabel = _hudRoot?.Q<Label>("nextbot-threat-value-label");
        _roundResultsOverlay = _hudRoot?.Q<VisualElement>("round-results-overlay");
        _roundResultsTitleLabel = _hudRoot?.Q<Label>("round-results-title-label");
        _roundResultsBestTimeValue = _hudRoot?.Q<Label>("round-results-best-time-value");
        _roundResultsRevivesValue = _hudRoot?.Q<Label>("round-results-revives-value");
        _roundResultsDownedValue = _hudRoot?.Q<Label>("round-results-downed-value");
        _roundResultsLeaderboardList = _hudRoot?.Q<VisualElement>("round-results-leaderboard-list");
        _tabScoreboardOverlay = _hudRoot?.Q<VisualElement>("tab-scoreboard-overlay");
        _tabScoreboardList = _hudRoot?.Q<ScrollView>("tab-scoreboard-list");
        _tabScoreboardMapLabel = _hudRoot?.Q<Label>("tab-scoreboard-map-label");
        _tabScoreboardPhaseLabel = _hudRoot?.Q<Label>("tab-scoreboard-phase-label");
        _tabScoreboardModeLabel = _hudRoot?.Q<Label>("tab-scoreboard-mode-label");
        _tabScoreboardPlayerCountLabel = _hudRoot?.Q<Label>("tab-scoreboard-player-count-label");
        EnsureMapVoteOverlay();

        RefreshPhaseDisplay();
        RefreshAnnouncementDisplay();
        RefreshNextbotThreatDisplay();
        RefreshResultsDisplay();
        RefreshTabScoreboardDisplay();
        RefreshMapVoteDisplay();
    }

    private UIDocument GetLocalHudDocument()
    {
        GameObject localPlayer = GameObject.Find("LocalPlayer");
        if (localPlayer == null)
        {
            return null;
        }

        UIDocument hudDocument = localPlayer.GetComponentInChildren<UIDocument>(true);
        if (hudDocument == null || !hudDocument.enabled)
        {
            return null;
        }

        return hudDocument;
    }

    private void HandleRoundPhaseChanged(RoundPhaseMessageData message)
    {
        _currentPhase = message;
        _phaseEndsAtUnscaledTime = Time.unscaledTime + Mathf.Max(0f, message.timeRemainingMs / 1000f);

        if (string.Equals(message.phase, "intermission", StringComparison.OrdinalIgnoreCase)
            && message.roundIndex > 0)
        {
            _showMapVoteResultsView = false;
            if (message.isMapVoteOpen)
            {
                EnsureFallbackMapVoteState(message);
                _networkManager?.RequestMapVoteState();
            }
            else if (_currentMapVote != null)
            {
                _currentMapVote.isOpen = false;
            }
        }

        if (string.Equals(message.phase, "round", StringComparison.OrdinalIgnoreCase))
        {
            _showResults = false;
        }

        RefreshPhaseDisplay();
        RefreshResultsDisplay();
    }

    private void HandleRoundAnnouncementReceived(RoundAnnouncementMessageData message)
    {
        if (message == null)
        {
            return;
        }

        _announcementTitle = message.title ?? string.Empty;
        _announcementSubtitle = message.subtitle ?? string.Empty;
        _announcementHideAt = Time.unscaledTime + Mathf.Max(0.5f, message.durationSeconds);

        if (string.Equals(_announcementTitle, "ROUND STARTED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(_announcementSubtitle, "SURVIVE FOR 3 MINUTES", StringComparison.OrdinalIgnoreCase))
        {
            PlayRoundStartSfx();
        }

        RefreshAnnouncementDisplay();
    }

    private void HandleRoundResultsReceived(RoundResultsMessageData message)
    {
        _results = message;
        _showResults = message != null
            && message.entries != null
            && message.entries.Length > 0;
        RefreshResultsDisplay();
    }

    private void HandleMapVoteStateReceived(MapVoteStateMessageData message)
    {
        _currentMapVote = message;
        if (message?.candidates != null && message.candidates.Length > 0)
        {
            _lastMapVoteCandidates = message.candidates;
        }

        if (message != null && message.isOpen)
        {
            _mapVoteEndsAtUnscaledTime = Time.unscaledTime + Mathf.Max(0f, message.timeRemainingMs / 1000f);
        }
        else
        {
            _mapVoteEndsAtUnscaledTime = 0f;
            _localVotedMapId = string.Empty;
            _showMapVoteResultsView = false;
        }

        RefreshResultsDisplay();
        RefreshMapVoteDisplay();
    }

    private void EnsureFallbackMapVoteState(RoundPhaseMessageData phaseMessage)
    {
        _mapVoteEndsAtUnscaledTime = Time.unscaledTime + Mathf.Max(0f, phaseMessage.timeRemainingMs / 1000f);

        if (_currentMapVote != null && _currentMapVote.isOpen)
        {
            return;
        }

        _currentMapVote = new MapVoteStateMessageData
        {
            isOpen = true,
            timeRemainingMs = phaseMessage.timeRemainingMs,
            selectedMapId = _currentMapVote?.selectedMapId ?? string.Empty,
            candidates = _lastMapVoteCandidates ?? Array.Empty<MapVoteCandidateMessageData>(),
            votes = Array.Empty<MapVoteCountMessageData>(),
        };
    }

    private void HandleMapSelectedReceived(MapSelectedMessageData message)
    {
        _showResults = false;
        _localVotedMapId = string.Empty;
        _currentMapVote = null;
        _mapVoteEndsAtUnscaledTime = 0f;
        _showMapVoteResultsView = false;
        RefreshResultsDisplay();
        RefreshMapVoteDisplay();
    }

    private void HandleRoomLeft()
    {
        _currentPhase = null;
        _phaseEndsAtUnscaledTime = 0f;
        _announcementTitle = string.Empty;
        _announcementSubtitle = string.Empty;
        _announcementHideAt = 0f;
        _results = null;
        _showResults = false;
        _currentMapVote = null;
        _mapVoteEndsAtUnscaledTime = 0f;
        _localVotedMapId = string.Empty;
        _showMapVoteResultsView = false;
        RefreshPhaseDisplay();
        RefreshAnnouncementDisplay();
        RefreshNextbotThreatDisplay();
        RefreshResultsDisplay();
        RefreshMapVoteDisplay();
    }

    private void UpdateAnnouncementLifetime()
    {
        if (_announcementHideAt > 0f && Time.unscaledTime >= _announcementHideAt)
        {
            _announcementTitle = string.Empty;
            _announcementSubtitle = string.Empty;
            _announcementHideAt = 0f;
        }
    }

    private void RefreshBackgroundMusic()
    {
        if (_musicAudioSource == null)
        {
            return;
        }

        AudioClip targetClip = null;
        bool isMenuVisible = _lobbyUi != null && _lobbyUi.menuPanel != null && _lobbyUi.menuPanel.activeInHierarchy;

        if (isMenuVisible)
        {
            targetClip = _mainMenuMusicClip;
        }
        else if (_currentPhase != null && string.Equals(_currentPhase.phase, "round", StringComparison.OrdinalIgnoreCase))
        {
            targetClip = _inGameMusicClip;
        }
        else if (_currentPhase != null && !string.Equals(_currentPhase.phase, "waiting", StringComparison.OrdinalIgnoreCase))
        {
            targetClip = _intermissionMusicClip;
        }

        if (_activeLoopClip == targetClip)
        {
            if (targetClip != null && !_musicAudioSource.isPlaying)
            {
                _musicAudioSource.Play();
            }

            return;
        }

        _activeLoopClip = targetClip;
        _musicAudioSource.Stop();
        _musicAudioSource.clip = targetClip;

        if (targetClip != null)
        {
            _musicAudioSource.Play();
        }
    }

    private void PlayRoundStartSfx()
    {
        if (_roundStartClip == null || _oneShotAudioSource == null)
        {
            return;
        }

        _oneShotAudioSource.Stop();
        _oneShotAudioSource.PlayOneShot(_roundStartClip);
    }

    private void HandleResultsCloseInput()
    {
        if (!_showResults || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.tabKey.wasPressedThisFrame
            || Keyboard.current.escapeKey.wasPressedThisFrame
            || Keyboard.current.enterKey.wasPressedThisFrame)
        {
            _showResults = false;
        }
    }

    private void RefreshTabScoreboardDisplay()
    {
        if (_tabScoreboardOverlay == null || _tabScoreboardList == null)
        {
            return;
        }

        bool visible = ShouldShowTabScoreboard();
        _tabScoreboardOverlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (!visible)
        {
            return;
        }

        RebuildTabScoreboardRows();
        RefreshTabScoreboardFooter();
    }

    private bool ShouldShowTabScoreboard()
    {
        if (Keyboard.current == null || !Keyboard.current.tabKey.isPressed)
        {
            return false;
        }

        if (_showResults)
        {
            return false;
        }

        bool isMenuVisible = _lobbyUi != null && _lobbyUi.menuPanel != null && _lobbyUi.menuPanel.activeInHierarchy;
        if (isMenuVisible)
        {
            return false;
        }

        return HasScoreboardPlayers();
    }

    private void RebuildTabScoreboardRows()
    {
        _tabScoreboardList.Clear();

        if (_networkManager == null)
        {
            return;
        }

        List<PlayerRowData> rows = new List<PlayerRowData>();
        Dictionary<string, Player> scoreboardPlayers = GetScoreboardPlayers();
        if (scoreboardPlayers == null)
        {
            return;
        }

        foreach (string sessionId in scoreboardPlayers.Keys)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                continue;
            }

            Player player = scoreboardPlayers[sessionId];
            if (player == null)
            {
                continue;
            }

            rows.Add(new PlayerRowData
            {
                SessionId = sessionId,
                Player = player,
                SortPriority = GetPlayerStatePriority(player),
                DisplayName = GetScoreboardDisplayName(player, sessionId)
            });
        }

        rows.Sort((a, b) =>
        {
            int priorityCompare = a.SortPriority.CompareTo(b.SortPriority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            if (string.Equals(a.SessionId, _networkManager.LocalSessionId, StringComparison.Ordinal))
            {
                return -1;
            }

            if (string.Equals(b.SessionId, _networkManager.LocalSessionId, StringComparison.Ordinal))
            {
                return 1;
            }

            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        for (int i = 0; i < rows.Count; i++)
        {
            _tabScoreboardList.Add(CreateTabScoreboardRow(rows[i], i));
        }
    }

    private void RefreshTabScoreboardFooter()
    {
        if (_tabScoreboardMapLabel != null)
        {
            _tabScoreboardMapLabel.text = $"Map: {SceneManager.GetActiveScene().name}";
        }

        if (_tabScoreboardPhaseLabel != null)
        {
            string phaseText = "Waiting";
            if (_currentPhase != null && !string.IsNullOrWhiteSpace(_currentPhase.phase))
            {
                phaseText = char.ToUpperInvariant(_currentPhase.phase[0]) + _currentPhase.phase.Substring(1).ToLowerInvariant();
            }

            _tabScoreboardPhaseLabel.text = $"Phase: {phaseText}";
        }

        if (_tabScoreboardModeLabel != null)
        {
            _tabScoreboardModeLabel.text = "Gamemode: Default";
        }

        if (_tabScoreboardPlayerCountLabel != null)
        {
            int playerCount = GetScoreboardPlayers()?.Count ?? 0;
            _tabScoreboardPlayerCountLabel.text = $"{playerCount} players";
        }
    }

    private bool HasScoreboardPlayers()
    {
        return GetScoreboardPlayers()?.Count > 0;
    }

    private Dictionary<string, Player> GetScoreboardPlayers()
    {
        if (_networkManager == null)
        {
            return null;
        }

        if (_networkManager.Room != null
            && _networkManager.Room.State != null
            && _networkManager.Room.State.players != null
            && _networkManager.Room.State.players.Count > 0)
        {
            Dictionary<string, Player> onlinePlayers = new Dictionary<string, Player>();
            foreach (string sessionId in _networkManager.Room.State.players.Keys)
            {
                onlinePlayers[sessionId] = _networkManager.Room.State.players[sessionId];
            }

            return onlinePlayers;
        }

        return _networkManager.HasSimulatedPlayerStates
            ? _networkManager.SimulatedPlayerStates
            : null;
    }

    private VisualElement CreateTabScoreboardRow(PlayerRowData rowData, int index)
    {
        bool isLocal = _networkManager != null && string.Equals(rowData.SessionId, _networkManager.LocalSessionId, StringComparison.Ordinal);

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.minHeight = 46f;
        row.style.paddingLeft = 10f;
        row.style.paddingRight = 10f;
        row.style.paddingTop = 5f;
        row.style.paddingBottom = 5f;
        row.style.backgroundColor = index % 2 == 0
            ? new Color(1f, 1f, 1f, 0.02f)
            : new Color(0f, 0f, 0f, 0.08f);
        if (isLocal)
        {
            row.style.backgroundColor = new Color(1f, 0.9f, 0.55f, 0.08f);
        }

        VisualElement nameCell = new VisualElement();
        nameCell.style.flexDirection = FlexDirection.Row;
        nameCell.style.alignItems = Align.Center;
        nameCell.style.flexGrow = 1f;

        VisualElement avatar = new VisualElement();
        avatar.style.width = 56f;
        avatar.style.height = 30f;
        avatar.style.marginRight = 10f;
        avatar.style.borderTopLeftRadius = 2f;
        avatar.style.borderTopRightRadius = 2f;
        avatar.style.borderBottomLeftRadius = 2f;
        avatar.style.borderBottomRightRadius = 2f;
        avatar.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
        avatar.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        Texture2D avatarTexture = GetScoreboardIconTexture(index);
        if (avatarTexture != null)
        {
            avatar.style.backgroundImage = new StyleBackground(avatarTexture);
        }

        Label nameLabel = new Label(rowData.DisplayName);
        nameLabel.style.color = isLocal ? new Color(1f, 0.95f, 0.75f) : Color.white;
        nameLabel.style.fontSize = 20f;
        nameLabel.style.unityFontStyleAndWeight = isLocal ? FontStyle.Bold : FontStyle.Normal;

        nameCell.Add(avatar);
        nameCell.Add(nameLabel);

        Label stateLabel = new Label(GetPlayerStateText(rowData.Player));
        stateLabel.style.width = 110f;
        stateLabel.style.fontSize = 18f;
        stateLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        stateLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        stateLabel.style.color = GetPlayerStateColor(rowData.Player);

        Label readyLabel = new Label(rowData.Player.isReady ? "IN" : "MENU");
        readyLabel.style.width = 88f;
        readyLabel.style.fontSize = 18f;
        readyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        readyLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        readyLabel.style.color = rowData.Player.isReady
            ? new Color(1f, 0.87f, 0.35f)
            : new Color(0.7f, 0.7f, 0.7f);

        row.Add(nameCell);
        row.Add(stateLabel);
        row.Add(readyLabel);
        return row;
    }

    private string GetScoreboardDisplayName(Player player, string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return "Player";
        }

        if (_networkManager != null && string.Equals(sessionId, _networkManager.LocalSessionId, StringComparison.Ordinal))
        {
            return "You";
        }

        if (player != null && !string.IsNullOrWhiteSpace(player.displayName))
        {
            return player.displayName;
        }

        int suffixLength = Mathf.Min(4, sessionId.Length);
        return $"Player {sessionId.Substring(sessionId.Length - suffixLength, suffixLength)}";
    }

    private static int GetPlayerStatePriority(Player player)
    {
        if (player == null)
        {
            return 3;
        }

        if (player.isEliminated)
        {
            return 2;
        }

        if (player.isInjured)
        {
            return 1;
        }

        return 0;
    }

    private static string GetPlayerStateText(Player player)
    {
        if (player == null)
        {
            return "UNKNOWN";
        }

        if (player.isEliminated)
        {
            return "DEAD";
        }

        if (player.isInjured)
        {
            return "DOWN";
        }

        return "ALIVE";
    }

    private static Color GetPlayerStateColor(Player player)
    {
        if (player == null)
        {
            return new Color(0.9f, 0.9f, 0.9f);
        }

        if (player.isEliminated)
        {
            return new Color(1f, 0.45f, 0.45f);
        }

        if (player.isInjured)
        {
            return new Color(1f, 0.8f, 0.4f);
        }

        return new Color(0.52f, 1f, 0.48f);
    }

    private Texture2D GetScoreboardIconTexture(int rowIndex)
    {
        bool usePinkIcon = rowIndex % 2 != 0;
        if (usePinkIcon && _pinkScoreboardIcon != null)
        {
            return _pinkScoreboardIcon;
        }

        if (!usePinkIcon && _greenScoreboardIcon != null)
        {
            return _greenScoreboardIcon;
        }

        return _greenScoreboardIcon != null ? _greenScoreboardIcon : _pinkScoreboardIcon;
    }

    private void RefreshPhaseDisplay()
    {
        if (_roundPhaseContainer == null || _roundPhaseTitleLabel == null || _roundPhaseTimerLabel == null)
        {
            return;
        }

        bool visible = _currentPhase != null && !string.Equals(_currentPhase.phase, "waiting", StringComparison.OrdinalIgnoreCase);
        _roundPhaseContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (!visible)
        {
            return;
        }

        float timeRemaining = Mathf.Max(0f, _phaseEndsAtUnscaledTime - Time.unscaledTime);
        _roundPhaseTitleLabel.text = string.Equals(_currentPhase.phase, "intermission", StringComparison.OrdinalIgnoreCase)
            ? "INTERMISSION"
            : (string.Equals(_currentPhase.phase, "map_vote", StringComparison.OrdinalIgnoreCase)
                ? "VOTING"
                : "ROUND");
        _roundPhaseTimerLabel.text = FormatTime(timeRemaining);
    }

    private void RefreshAnnouncementDisplay()
    {
        if (_roundAnnouncementContainer == null || _roundAnnouncementTitleLabel == null || _roundAnnouncementSubtitleLabel == null)
        {
            return;
        }

        bool visible = !string.IsNullOrEmpty(_announcementTitle) || !string.IsNullOrEmpty(_announcementSubtitle);
        _roundAnnouncementContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (!visible)
        {
            return;
        }

        _roundAnnouncementTitleLabel.text = _announcementTitle;
        _roundAnnouncementSubtitleLabel.text = _announcementSubtitle;
    }

    private void RefreshNextbotThreatDisplay()
    {
        if (_nextbotThreatContainer == null || _nextbotThreatValueLabel == null)
        {
            return;
        }

        if (_networkManager == null
            || _networkManager.Room == null
            || _networkManager.Room.State == null
            || _networkManager.Room.State.nextbots == null
            || string.IsNullOrWhiteSpace(_networkManager.LocalSessionId))
        {
            _nextbotThreatContainer.style.display = DisplayStyle.None;
            return;
        }

        List<string> hunterNames = new List<string>();
        foreach (string nextbotId in _networkManager.Room.State.nextbots.Keys)
        {
            if (string.IsNullOrWhiteSpace(nextbotId))
            {
                continue;
            }

            NextbotState nextbotState = _networkManager.Room.State.nextbots[nextbotId];
            if (nextbotState == null
                || !nextbotState.isActive
                || !string.Equals(nextbotState.targetSessionId, _networkManager.LocalSessionId, StringComparison.Ordinal))
            {
                continue;
            }

            hunterNames.Add(FormatNextbotDisplayName(nextbotId));
        }

        if (hunterNames.Count == 0)
        {
            _nextbotThreatContainer.style.display = DisplayStyle.None;
            return;
        }

        hunterNames.Sort(StringComparer.OrdinalIgnoreCase);
        _nextbotThreatContainer.style.display = DisplayStyle.Flex;
        if (_nextbotThreatTitleLabel != null)
        {
            _nextbotThreatTitleLabel.text = hunterNames.Count == 1 ? "HUNTED BY" : "HUNTED BY";
        }

        _nextbotThreatValueLabel.text = string.Join(", ", hunterNames);
    }

    private static string FormatNextbotDisplayName(string nextbotId)
    {
        if (string.IsNullOrWhiteSpace(nextbotId))
        {
            return "Unknown";
        }

        string[] parts = nextbotId
            .Replace("-", " ")
            .Replace("_", " ")
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return nextbotId;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length == 0)
            {
                continue;
            }

            parts[i] = part.Length == 1
                ? part.ToUpperInvariant()
                : char.ToUpperInvariant(part[0]) + part.Substring(1);
        }

        return string.Join(" ", parts);
    }

    private void EnsureMapVoteOverlay()
    {
        if (_hudRoot == null)
        {
            _mapVoteOverlay = null;
            _mapVoteCardView = null;
            _mapVoteGrid = null;
            _mapVoteResultsView = null;
            _mapVoteWinningLabel = null;
            _mapVoteResultsList = null;
            _mapVoteButtons.Clear();
            _mapVoteCountLabels.Clear();
            _mapVoteVotedLabels.Clear();
            _mapVoteResultNameLabels.Clear();
            _mapVoteResultPercentLabels.Clear();
            _mapVoteResultFillBars.Clear();
            _mapVoteResultCountLabels.Clear();
            return;
        }

        _mapVoteOverlay = _hudRoot.Q<VisualElement>("map-vote-overlay");
        if (_mapVoteOverlay != null)
        {
            return;
        }

        _mapVoteButtons.Clear();
        _mapVoteCountLabels.Clear();
        _mapVoteVotedLabels.Clear();
        _mapVoteResultNameLabels.Clear();
        _mapVoteResultPercentLabels.Clear();
        _mapVoteResultFillBars.Clear();
        _mapVoteResultCountLabels.Clear();

        _mapVoteOverlay = new VisualElement { name = "map-vote-overlay" };
        _mapVoteOverlay.style.position = Position.Absolute;
        _mapVoteOverlay.style.left = 0f;
        _mapVoteOverlay.style.right = 0f;
        _mapVoteOverlay.style.top = 0f;
        _mapVoteOverlay.style.bottom = 0f;
        _mapVoteOverlay.style.display = DisplayStyle.None;
        _mapVoteOverlay.style.justifyContent = Justify.Center;
        _mapVoteOverlay.style.alignItems = Align.Center;
        _mapVoteOverlay.style.paddingLeft = 20f;
        _mapVoteOverlay.style.paddingRight = 20f;
        _mapVoteOverlay.pickingMode = PickingMode.Position;

        _mapVoteContent = new VisualElement();
        _mapVoteContent.style.width = 820f;
        _mapVoteContent.style.maxWidth = new StyleLength(Length.Percent(94));
        _mapVoteContent.style.alignItems = Align.Center;

        _mapVoteTitleLabel = new Label("VOTING");
        _mapVoteTitleLabel.style.color = Color.white;
        _mapVoteTitleLabel.style.fontSize = 34f;
        _mapVoteTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _mapVoteTitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        _mapVoteTimerLabel = new Label("20s");
        _mapVoteTimerLabel.style.color = Color.white;
        _mapVoteTimerLabel.style.fontSize = 26f;
        _mapVoteTimerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _mapVoteTimerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _mapVoteTimerLabel.style.marginBottom = 24f;
        _mapVoteTimerLabel.style.display = DisplayStyle.None;

        _mapVoteCardView = new VisualElement { name = "map-vote-card-view" };
        _mapVoteCardView.style.alignItems = Align.Center;

        _mapVoteGrid = new VisualElement { name = "map-vote-grid" };
        _mapVoteGrid.style.flexDirection = FlexDirection.Row;
        _mapVoteGrid.style.flexWrap = Wrap.Wrap;
        _mapVoteGrid.style.justifyContent = Justify.Center;
        _mapVoteGrid.style.alignItems = Align.Center;
        _mapVoteCardView.Add(_mapVoteGrid);

        _mapVoteResultsView = new VisualElement { name = "map-vote-results-view" };
        _mapVoteResultsView.style.display = DisplayStyle.None;
        _mapVoteResultsView.style.width = 540f;
        _mapVoteResultsView.style.maxWidth = new StyleLength(Length.Percent(96));
        _mapVoteResultsView.style.paddingLeft = 20f;
        _mapVoteResultsView.style.paddingRight = 20f;
        _mapVoteResultsView.style.paddingTop = 18f;
        _mapVoteResultsView.style.paddingBottom = 18f;
        _mapVoteResultsView.style.backgroundColor = new Color(0.04f, 0.04f, 0.05f, 0.84f);
        _mapVoteResultsView.style.borderTopLeftRadius = 10f;
        _mapVoteResultsView.style.borderTopRightRadius = 10f;
        _mapVoteResultsView.style.borderBottomLeftRadius = 10f;
        _mapVoteResultsView.style.borderBottomRightRadius = 10f;
        _mapVoteResultsView.style.borderTopWidth = 1f;
        _mapVoteResultsView.style.borderRightWidth = 1f;
        _mapVoteResultsView.style.borderBottomWidth = 1f;
        _mapVoteResultsView.style.borderLeftWidth = 1f;
        _mapVoteResultsView.style.borderTopColor = new Color(1f, 1f, 1f, 0.12f);
        _mapVoteResultsView.style.borderRightColor = new Color(1f, 1f, 1f, 0.12f);
        _mapVoteResultsView.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
        _mapVoteResultsView.style.borderLeftColor = new Color(1f, 1f, 1f, 0.12f);

        _mapVoteWinningLabel = new Label(string.Empty);
        _mapVoteWinningLabel.style.color = Color.white;
        _mapVoteWinningLabel.style.fontSize = 28f;
        _mapVoteWinningLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _mapVoteWinningLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _mapVoteWinningLabel.style.marginBottom = 14f;

        _mapVoteResultsList = new VisualElement { name = "map-vote-results-list" };
        _mapVoteResultsList.style.flexDirection = FlexDirection.Column;

        _mapVoteResultsView.Add(_mapVoteWinningLabel);
        _mapVoteResultsView.Add(_mapVoteResultsList);

        _mapVoteStatusLabel = new Label(string.Empty);
        _mapVoteStatusLabel.style.color = new Color(1f, 0.9f, 0.35f);
        _mapVoteStatusLabel.style.fontSize = 18f;
        _mapVoteStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _mapVoteStatusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _mapVoteStatusLabel.style.marginTop = 14f;

        _mapVoteContent.Add(_mapVoteTitleLabel);
        _mapVoteContent.Add(_mapVoteTimerLabel);
        _mapVoteContent.Add(_mapVoteCardView);
        _mapVoteContent.Add(_mapVoteResultsView);
        _mapVoteContent.Add(_mapVoteStatusLabel);
        _mapVoteOverlay.Add(_mapVoteContent);
        _hudRoot.Add(_mapVoteOverlay);
    }

    private void RefreshMapVoteDisplay()
    {
        EnsureMapVoteOverlay();
        if (_mapVoteOverlay == null)
        {
            return;
        }

        bool visible = ShouldShowMapVote();
        _mapVoteOverlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (!visible)
        {
            RestoreGameplayCursorIfNeeded();
            return;
        }

        _mapVoteOverlay.BringToFront();
        bool showResultsHud = _showMapVoteResultsView;
        _mapVoteOverlay.style.justifyContent = showResultsHud ? Justify.FlexStart : Justify.Center;
        _mapVoteOverlay.style.paddingTop = showResultsHud ? 78f : 0f;
        _mapVoteOverlay.pickingMode = showResultsHud ? PickingMode.Ignore : PickingMode.Position;

        if (_mapVoteContent != null)
        {
            _mapVoteContent.style.width = showResultsHud ? 560f : 820f;
            _mapVoteContent.style.maxWidth = new StyleLength(Length.Percent(showResultsHud ? 96f : 94f));
        }

        if (showResultsHud)
        {
            RestoreGameplayCursorIfNeeded();
        }
        else
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        float timeRemaining = Mathf.Max(0f, _mapVoteEndsAtUnscaledTime - Time.unscaledTime);
        if (_mapVoteTitleLabel != null)
        {
            _mapVoteTitleLabel.text = "VOTING";
        }

        if (_mapVoteTimerLabel != null)
        {
            _mapVoteTimerLabel.text = $"{Mathf.CeilToInt(timeRemaining)}s";
            _mapVoteTimerLabel.style.display = DisplayStyle.None;
        }

        RebuildMapVoteCardsIfNeeded();
        RebuildMapVoteResultsIfNeeded();
        RefreshMapVoteCards();
        RefreshMapVoteResults();
    }

    private bool ShouldShowMapVote()
    {
        if (_showResults)
        {
            return false;
        }

        if (_currentMapVote == null || !_currentMapVote.isOpen)
        {
            return false;
        }

        bool isMenuVisible = _lobbyUi != null && _lobbyUi.menuPanel != null && _lobbyUi.menuPanel.activeInHierarchy;
        if (isMenuVisible)
        {
            return false;
        }

        return _currentMapVote.candidates != null && _currentMapVote.candidates.Length > 0;
    }

    private void RestoreGameplayCursorIfNeeded()
    {
        bool isMenuVisible = _lobbyUi != null && _lobbyUi.menuPanel != null && _lobbyUi.menuPanel.activeInHierarchy;
        if (isMenuVisible)
        {
            return;
        }

        bool shouldLock = _currentPhase != null && (
            string.Equals(_currentPhase.phase, "round", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_currentPhase.phase, "intermission", StringComparison.OrdinalIgnoreCase)
        );

        if (shouldLock)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }
    }

    private void RebuildMapVoteCardsIfNeeded()
    {
        if (_mapVoteGrid == null || _currentMapVote?.candidates == null)
        {
            return;
        }

        bool needsRebuild = _mapVoteButtons.Count != _currentMapVote.candidates.Length
            || _mapVoteGrid.childCount != _currentMapVote.candidates.Length;
        if (!needsRebuild)
        {
            foreach (MapVoteCandidateMessageData candidate in _currentMapVote.candidates)
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.mapId) || !_mapVoteButtons.ContainsKey(candidate.mapId))
                {
                    needsRebuild = true;
                    break;
                }
            }
        }

        if (!needsRebuild)
        {
            return;
        }

        _mapVoteGrid.Clear();
        _mapVoteButtons.Clear();
        _mapVoteCountLabels.Clear();
        _mapVoteVotedLabels.Clear();

        foreach (MapVoteCandidateMessageData candidate in _currentMapVote.candidates)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.mapId))
            {
                continue;
            }

            Button card = CreateMapVoteCard(candidate);
            _mapVoteButtons[candidate.mapId] = card;
            _mapVoteGrid.Add(card);
        }
    }

    private Button CreateMapVoteCard(MapVoteCandidateMessageData candidate)
    {
        string mapId = candidate.mapId;
        Button card = new Button(() => HandleMapVoteClicked(mapId));
        card.text = string.Empty;
        card.style.width = 360f;
        card.style.height = 150f;
        card.style.marginLeft = 10f;
        card.style.marginRight = 10f;
        card.style.marginTop = 10f;
        card.style.marginBottom = 10f;
        card.style.paddingLeft = 12f;
        card.style.paddingRight = 12f;
        card.style.paddingTop = 10f;
        card.style.paddingBottom = 10f;
        card.style.borderTopLeftRadius = 8f;
        card.style.borderTopRightRadius = 8f;
        card.style.borderBottomLeftRadius = 8f;
        card.style.borderBottomRightRadius = 8f;
        card.style.borderTopWidth = 2f;
        card.style.borderRightWidth = 2f;
        card.style.borderBottomWidth = 2f;
        card.style.borderLeftWidth = 2f;
        card.style.backgroundColor = GetMapCardBackground(candidate.mapId);
        card.style.flexDirection = FlexDirection.Column;
        card.style.justifyContent = Justify.SpaceBetween;

        VisualElement topRow = new VisualElement();
        topRow.style.flexDirection = FlexDirection.Row;
        topRow.style.justifyContent = Justify.SpaceBetween;

        Label difficultyLabel = new Label(string.IsNullOrWhiteSpace(candidate.difficulty) ? "NORMAL" : candidate.difficulty.ToUpperInvariant());
        difficultyLabel.style.color = GetDifficultyColor(candidate.difficulty);
        difficultyLabel.style.fontSize = 20f;
        difficultyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        Label voteCountLabel = new Label("0");
        voteCountLabel.style.color = Color.white;
        voteCountLabel.style.fontSize = 20f;
        voteCountLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        voteCountLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        _mapVoteCountLabels[mapId] = voteCountLabel;

        topRow.Add(difficultyLabel);
        topRow.Add(voteCountLabel);

        Label votedLabel = new Label("VOTED");
        votedLabel.style.display = DisplayStyle.None;
        votedLabel.style.alignSelf = Align.FlexEnd;
        votedLabel.style.color = new Color(1f, 0.92f, 0.35f);
        votedLabel.style.fontSize = 16f;
        votedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _mapVoteVotedLabels[mapId] = votedLabel;

        VisualElement icon = new VisualElement { name = "map-icon" };
        icon.style.position = Position.Absolute;
        icon.style.left = 0f;
        icon.style.right = 0f;
        icon.style.top = 0f;
        icon.style.bottom = 0f;
        icon.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        Texture2D iconTexture = Resources.Load<Texture2D>($"UI/{candidate.mapId}_map_icon");
        if (iconTexture == null && !string.IsNullOrWhiteSpace(candidate.displayName))
        {
            iconTexture = Resources.Load<Texture2D>($"UI/{candidate.displayName}_map_icon");
        }

        if (iconTexture != null)
        {
            icon.style.backgroundImage = new StyleBackground(iconTexture);
        }
        card.Add(icon);

        VisualElement overlay = new VisualElement { name = "overlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
        card.Add(overlay);

        Label nameLabel = new Label(string.IsNullOrWhiteSpace(candidate.displayName) ? candidate.mapId : candidate.displayName);
        nameLabel.style.color = Color.white;
        nameLabel.style.fontSize = 26f;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        card.Add(topRow);
        card.Add(votedLabel);
        card.Add(nameLabel);
        return card;
    }

    private void HandleMapVoteClicked(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId) || _networkManager == null)
        {
            return;
        }

        _localVotedMapId = mapId;
        _showMapVoteResultsView = true;
        _networkManager.SendMapVote(mapId);
        RefreshMapVoteDisplay();
    }

    private void RebuildMapVoteResultsIfNeeded()
    {
        if (_mapVoteResultsList == null || _currentMapVote?.candidates == null)
        {
            return;
        }

        bool needsRebuild = _mapVoteResultNameLabels.Count != _currentMapVote.candidates.Length
            || _mapVoteResultsList.childCount != _currentMapVote.candidates.Length;
        if (!needsRebuild)
        {
            foreach (MapVoteCandidateMessageData candidate in _currentMapVote.candidates)
            {
                if (candidate == null
                    || string.IsNullOrWhiteSpace(candidate.mapId)
                    || !_mapVoteResultNameLabels.ContainsKey(candidate.mapId))
                {
                    needsRebuild = true;
                    break;
                }
            }
        }

        if (!needsRebuild)
        {
            return;
        }

        _mapVoteResultsList.Clear();
        _mapVoteResultNameLabels.Clear();
        _mapVoteResultPercentLabels.Clear();
        _mapVoteResultFillBars.Clear();
        _mapVoteResultCountLabels.Clear();

        foreach (MapVoteCandidateMessageData candidate in _currentMapVote.candidates)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.mapId))
            {
                continue;
            }

            VisualElement row = CreateMapVoteResultRow(candidate);
            _mapVoteResultsList.Add(row);
        }
    }

    private void RefreshMapVoteCards()
    {
        if (_currentMapVote?.candidates == null)
        {
            return;
        }

        foreach (MapVoteCandidateMessageData candidate in _currentMapVote.candidates)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.mapId))
            {
                continue;
            }

            bool voted = string.Equals(_localVotedMapId, candidate.mapId, StringComparison.Ordinal);
            int voteCount = GetVoteCount(candidate.mapId);
            Color borderColor = voted
                ? new Color(1f, 0.88f, 0.25f)
                : GetDifficultyColor(candidate.difficulty);

            if (_mapVoteButtons.TryGetValue(candidate.mapId, out Button button) && button != null)
            {
                button.style.borderTopColor = borderColor;
                button.style.borderRightColor = borderColor;
                button.style.borderBottomColor = borderColor;
                button.style.borderLeftColor = borderColor;
                button.style.scale = voted ? new Scale(new Vector3(1.04f, 1.04f, 1f)) : new Scale(Vector3.one);
            }

            if (_mapVoteCountLabels.TryGetValue(candidate.mapId, out Label countLabel) && countLabel != null)
            {
                countLabel.text = voteCount == 1 ? "1 vote" : $"{voteCount} votes";
            }

            if (_mapVoteVotedLabels.TryGetValue(candidate.mapId, out Label votedLabel) && votedLabel != null)
            {
                votedLabel.style.display = voted ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        if (_mapVoteStatusLabel != null)
        {
            _mapVoteStatusLabel.text = _showMapVoteResultsView
                ? "Press [B] to change vote"
                : (string.IsNullOrWhiteSpace(_localVotedMapId)
                    ? string.Empty
                    : $"Voted for {GetMapDisplayName(_localVotedMapId)}");
        }
    }

    private VisualElement CreateMapVoteResultRow(MapVoteCandidateMessageData candidate)
    {
        string mapId = candidate.mapId;

        VisualElement row = new VisualElement();
        row.style.marginBottom = 10f;
        row.style.paddingLeft = 8f;
        row.style.paddingRight = 8f;
        row.style.paddingTop = 6f;
        row.style.paddingBottom = 6f;
        row.style.backgroundColor = new Color(1f, 1f, 1f, 0.02f);
        row.style.borderTopLeftRadius = 8f;
        row.style.borderTopRightRadius = 8f;
        row.style.borderBottomLeftRadius = 8f;
        row.style.borderBottomRightRadius = 8f;

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;

        Label nameLabel = new Label(string.IsNullOrWhiteSpace(candidate.displayName) ? mapId : candidate.displayName);
        nameLabel.style.color = Color.white;
        nameLabel.style.fontSize = 20f;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _mapVoteResultNameLabels[mapId] = nameLabel;

        Label percentLabel = new Label("0%");
        percentLabel.style.color = new Color(1f, 0.92f, 0.74f);
        percentLabel.style.fontSize = 19f;
        percentLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _mapVoteResultPercentLabels[mapId] = percentLabel;

        header.Add(nameLabel);
        header.Add(percentLabel);

        VisualElement track = new VisualElement();
        track.style.height = 12f;
        track.style.marginTop = 4f;
        track.style.backgroundColor = new Color(0f, 0f, 0f, 0.62f);
        track.style.borderTopLeftRadius = 999f;
        track.style.borderTopRightRadius = 999f;
        track.style.borderBottomLeftRadius = 999f;
        track.style.borderBottomRightRadius = 999f;

        VisualElement fill = new VisualElement();
        fill.style.height = 12f;
        fill.style.width = new StyleLength(Length.Percent(0f));
        fill.style.backgroundColor = GetMapResultBarColor(mapId);
        fill.style.borderTopLeftRadius = 999f;
        fill.style.borderTopRightRadius = 999f;
        fill.style.borderBottomLeftRadius = 999f;
        fill.style.borderBottomRightRadius = 999f;
        _mapVoteResultFillBars[mapId] = fill;
        track.Add(fill);

        Label countLabel = new Label("0 votes");
        countLabel.style.color = new Color(0.8f, 0.82f, 0.86f);
        countLabel.style.fontSize = 15f;
        countLabel.style.marginTop = 3f;
        _mapVoteResultCountLabels[mapId] = countLabel;

        row.Add(header);
        row.Add(track);
        row.Add(countLabel);
        return row;
    }

    private void RefreshMapVoteResults()
    {
        bool showResultsView = _showMapVoteResultsView && _currentMapVote?.candidates != null;
        if (_mapVoteCardView != null)
        {
            _mapVoteCardView.style.display = showResultsView ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (_mapVoteResultsView != null)
        {
            _mapVoteResultsView.style.display = showResultsView ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (!showResultsView || _currentMapVote?.candidates == null)
        {
            return;
        }

        int totalVotes = GetTotalVoteCount();
        string winningMapId = GetWinningMapId();
        if (_mapVoteWinningLabel != null)
        {
            string winningName = GetMapDisplayName(winningMapId);
            int winningPercent = GetVotePercent(winningMapId, totalVotes);
            _mapVoteWinningLabel.text = string.IsNullOrWhiteSpace(winningMapId)
                ? "Voting Active"
                : $"Winning Map\n{winningName} ({winningPercent}%)";
        }

        foreach (MapVoteCandidateMessageData candidate in _currentMapVote.candidates)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.mapId))
            {
                continue;
            }

            string mapId = candidate.mapId;
            int voteCount = GetVoteCount(mapId);
            int votePercent = GetVotePercent(mapId, totalVotes);
            bool isWinningMap = string.Equals(winningMapId, mapId, StringComparison.Ordinal);
            bool isLocalVote = string.Equals(_localVotedMapId, mapId, StringComparison.Ordinal);

            if (_mapVoteResultNameLabels.TryGetValue(mapId, out Label nameLabel) && nameLabel != null)
            {
                nameLabel.style.color = isWinningMap ? new Color(1f, 0.93f, 0.72f) : Color.white;
            }

            if (_mapVoteResultPercentLabels.TryGetValue(mapId, out Label percentLabel) && percentLabel != null)
            {
                percentLabel.text = $"{votePercent}%";
                percentLabel.style.color = isWinningMap ? new Color(1f, 0.93f, 0.72f) : new Color(0.88f, 0.9f, 0.94f);
            }

            if (_mapVoteResultFillBars.TryGetValue(mapId, out VisualElement fillBar) && fillBar != null)
            {
                fillBar.style.width = new StyleLength(Length.Percent(votePercent));
                fillBar.style.backgroundColor = isLocalVote
                    ? new Color(1f, 0.82f, 0.34f)
                    : GetMapResultBarColor(mapId);
            }

            if (_mapVoteResultCountLabels.TryGetValue(mapId, out Label countLabel) && countLabel != null)
            {
                countLabel.text = voteCount == 1 ? "1 vote" : $"{voteCount} votes";
                countLabel.style.color = isLocalVote ? new Color(1f, 0.9f, 0.62f) : new Color(0.8f, 0.82f, 0.86f);
            }
        }
    }

    private int GetVoteCount(string mapId)
    {
        if (_currentMapVote?.votes == null)
        {
            return 0;
        }

        foreach (MapVoteCountMessageData vote in _currentMapVote.votes)
        {
            if (vote != null && string.Equals(vote.mapId, mapId, StringComparison.Ordinal))
            {
                return vote.count;
            }
        }

        return 0;
    }

    private int GetTotalVoteCount()
    {
        if (_currentMapVote?.votes == null)
        {
            return 0;
        }

        int totalVotes = 0;
        foreach (MapVoteCountMessageData vote in _currentMapVote.votes)
        {
            if (vote != null)
            {
                totalVotes += Mathf.Max(0, vote.count);
            }
        }

        return totalVotes;
    }

    private int GetVotePercent(string mapId, int totalVotes)
    {
        int voteCount = GetVoteCount(mapId);
        if (totalVotes <= 0 || voteCount <= 0)
        {
            return 0;
        }

        return Mathf.RoundToInt((voteCount / (float)totalVotes) * 100f);
    }

    private string GetWinningMapId()
    {
        if (_currentMapVote?.candidates == null)
        {
            return string.Empty;
        }

        string winningMapId = string.Empty;
        int highestVoteCount = -1;
        foreach (MapVoteCandidateMessageData candidate in _currentMapVote.candidates)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.mapId))
            {
                continue;
            }

            int voteCount = GetVoteCount(candidate.mapId);
            if (voteCount > highestVoteCount)
            {
                highestVoteCount = voteCount;
                winningMapId = candidate.mapId;
            }
        }

        return winningMapId;
    }

    private string GetMapDisplayName(string mapId)
    {
        if (_currentMapVote?.candidates != null)
        {
            foreach (MapVoteCandidateMessageData candidate in _currentMapVote.candidates)
            {
                if (candidate != null && string.Equals(candidate.mapId, mapId, StringComparison.Ordinal))
                {
                    return string.IsNullOrWhiteSpace(candidate.displayName) ? candidate.mapId : candidate.displayName;
                }
            }
        }

        return mapId;
    }

    private static Color GetDifficultyColor(string difficulty)
    {
        if (string.Equals(difficulty, "HARD", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(1f, 0.28f, 0.24f);
        }

        return new Color(0.24f, 1f, 0.35f);
    }

    private static Color GetMapCardBackground(string mapId)
    {
        if (string.Equals(mapId, "backroom", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.38f, 0.28f, 0.08f, 0.92f);
        }

        if (string.Equals(mapId, "parkour", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.09f, 0.22f, 0.35f, 0.92f);
        }

        return new Color(0.16f, 0.27f, 0.19f, 0.92f);
    }

    private static Color GetMapResultBarColor(string mapId)
    {
        if (string.Equals(mapId, "backroom", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.95f, 0.73f, 0.4f);
        }

        if (string.Equals(mapId, "parkour", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.41f, 0.83f, 1f);
        }

        return new Color(0.47f, 0.93f, 0.54f);
    }

    private void HandleMapVoteInput()
    {
        if (!_showMapVoteResultsView || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            _showMapVoteResultsView = false;
            RefreshMapVoteDisplay();
        }
    }

    private void RefreshResultsDisplay()
    {
        if (_roundResultsOverlay == null
            || _roundResultsTitleLabel == null
            || _roundResultsBestTimeValue == null
            || _roundResultsRevivesValue == null
            || _roundResultsDownedValue == null
            || _roundResultsLeaderboardList == null)
        {
            return;
        }

        bool visible = _showResults
            && _results != null
            && _results.entries != null
            && _results.entries.Length > 0;
        _roundResultsOverlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (!visible)
        {
            return;
        }

        RoundResultEntryMessageData localEntry = GetLocalEntry();
        bool survivedFullRound = localEntry != null && localEntry.bestTimeMs >= Mathf.Max(0, _results.roundDurationMs - 250);
        _roundResultsTitleLabel.text = survivedFullRound ? "SURVIVED" : "ROUND RESULTS";
        _roundResultsBestTimeValue.text = FormatTime(localEntry != null ? localEntry.bestTimeMs / 1000f : 0f);
        _roundResultsRevivesValue.text = (localEntry?.revivesDone ?? 0).ToString();
        _roundResultsDownedValue.text = (localEntry?.downedCount ?? 0).ToString();

        RebuildLeaderboardRows();
    }

    private void RebuildLeaderboardRows()
    {
        _roundResultsLeaderboardList.Clear();

        for (int i = 0; i < _results.entries.Length; i++)
        {
            RoundResultEntryMessageData entry = _results.entries[i];
            bool isLocal = _networkManager != null && string.Equals(entry.sessionId, _networkManager.LocalSessionId, StringComparison.Ordinal);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 4f;
            row.style.paddingBottom = 4f;
            row.style.paddingLeft = 6f;
            row.style.paddingRight = 6f;
            row.style.marginBottom = 4f;
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = new Color(1f, 1f, 1f, 0.06f);
            row.style.backgroundColor = isLocal ? new Color(1f, 0.86f, 0.35f, 0.08f) : new Color(0f, 0f, 0f, 0f);

            Label leftLabel = new Label($"{entry.rank}. {entry.displayName}");
            leftLabel.style.color = isLocal ? new Color(1f, 0.9f, 0.55f) : new Color(0.92f, 0.92f, 0.92f);
            leftLabel.style.unityFontStyleAndWeight = isLocal ? FontStyle.Bold : FontStyle.Normal;
            leftLabel.style.fontSize = 18f;
            leftLabel.style.flexGrow = 1f;

            Label rightLabel = new Label($"{FormatTime(entry.bestTimeMs / 1000f)}   D:{entry.downedCount}   R:{entry.revivesDone}");
            rightLabel.style.color = isLocal ? new Color(1f, 0.9f, 0.55f) : new Color(0.92f, 0.92f, 0.92f);
            rightLabel.style.unityFontStyleAndWeight = isLocal ? FontStyle.Bold : FontStyle.Normal;
            rightLabel.style.fontSize = 18f;
            rightLabel.style.unityTextAlign = TextAnchor.MiddleRight;

            row.Add(leftLabel);
            row.Add(rightLabel);
            _roundResultsLeaderboardList.Add(row);
        }
    }

    private RoundResultEntryMessageData GetLocalEntry()
    {
        if (_results == null || _results.entries == null || _networkManager == null)
        {
            return null;
        }

        return _results.entries.FirstOrDefault(entry =>
            string.Equals(entry.sessionId, _networkManager.LocalSessionId, StringComparison.Ordinal));
    }

    private static string FormatTime(float totalSeconds)
    {
        int seconds = Mathf.Max(0, Mathf.CeilToInt(totalSeconds));
        int minutesPart = seconds / 60;
        int secondsPart = seconds % 60;
        return $"{minutesPart}:{secondsPart:00}";
    }

    private sealed class PlayerRowData
    {
        public string SessionId;
        public string DisplayName;
        public Player Player;
        public int SortPriority;
    }
}
