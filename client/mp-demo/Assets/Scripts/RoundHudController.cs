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

    private RoundPhaseMessageData _currentPhase;
    private float _phaseEndsAtUnscaledTime;
    private string _announcementTitle = string.Empty;
    private string _announcementSubtitle = string.Empty;
    private float _announcementHideAt;
    private RoundResultsMessageData _results;
    private bool _showResults;

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
        RefreshPhaseDisplay();
        RefreshAnnouncementDisplay();
        RefreshResultsDisplay();
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
            _networkManager.RoomLeftEvent -= HandleRoomLeft;
        }

        _networkManager = manager;

        if (_networkManager != null)
        {
            _networkManager.RoundPhaseChanged += HandleRoundPhaseChanged;
            _networkManager.RoundAnnouncementReceived += HandleRoundAnnouncementReceived;
            _networkManager.RoundResultsReceived += HandleRoundResultsReceived;
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

        RefreshPhaseDisplay();
        RefreshAnnouncementDisplay();
        RefreshResultsDisplay();
        RefreshTabScoreboardDisplay();
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
        _showResults = message != null && message.entries != null && message.entries.Length > 0;
        RefreshResultsDisplay();
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
        RefreshPhaseDisplay();
        RefreshAnnouncementDisplay();
        RefreshResultsDisplay();
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
        Texture2D avatarTexture = GetScoreboardPreviewTexture(Mathf.RoundToInt(rowData.Player.skinIndex));
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

    private Texture2D GetScoreboardPreviewTexture(int skinIndex)
    {
        if (skinIndex == 0 && _greenScoreboardIcon != null)
        {
            return _greenScoreboardIcon;
        }

        if (skinIndex == 1 && _pinkScoreboardIcon != null)
        {
            return _pinkScoreboardIcon;
        }

        return null;
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
            : "ROUND";
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

        bool visible = _showResults && _results != null && _results.entries != null && _results.entries.Length > 0;
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
