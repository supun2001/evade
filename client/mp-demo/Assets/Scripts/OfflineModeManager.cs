using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class OfflineModeManager : MonoBehaviour
{
    private const float InteractionDistance = 6f;
    private const float NextbotStartGraceSeconds = 3.5f;
    private const int PlayerMaxDownsBeforeElimination = 3;
    private const int MaxRescuersPerDownedPlayer = 2;
    private const float PlayerSpawnRotationY = 180f;
    private const string WaitingPhase = "waiting";
    private const string IntermissionPhase = "intermission";
    private const string RoundPhase = "round";
    private static OfflineModeManager _instance;

    [SerializeField, Min(1)] private int _offlineTotalPlayerCount = 15;
    [SerializeField] private float _spawnRingRadius = 1.8f;

    private readonly Dictionary<string, GameObject> _offlinePlayers = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, OfflinePlayerRoundState> _offlinePlayerStates = new Dictionary<string, OfflinePlayerRoundState>();
    private readonly Dictionary<string, string> _rescueAssignments = new Dictionary<string, string>();
    private readonly List<Vector3> _spawnPositions = new List<Vector3>();
    private readonly List<Vector3> _nextbotSpawnPositions = new List<Vector3>();
    private readonly List<Vector3> _patrolPositions = new List<Vector3>();
    private Coroutine _roundFlowCoroutine;
    private string _currentPhase = WaitingPhase;
    private int _roundIndex;
    private float _phaseEndsAtUnscaledTime;
    private float _nextbotDamageEnabledAtUnscaledTime;

    public static OfflineModeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<OfflineModeManager>();
                if (_instance == null)
                {
                    GameObject root = new GameObject(nameof(OfflineModeManager));
                    _instance = root.AddComponent<OfflineModeManager>();
                }
            }

            return _instance;
        }
    }

    public static bool TryGetExisting(out OfflineModeManager manager)
    {
        manager = _instance != null ? _instance : FindFirstObjectByType<OfflineModeManager>();
        if (manager != null)
        {
            _instance = manager;
        }

        return manager != null;
    }

    public bool IsOfflineModeActive { get; private set; }
    public bool IsOfflineRoundActive => IsOfflineModeActive && string.Equals(_currentPhase, RoundPhase, System.StringComparison.OrdinalIgnoreCase);
    public bool CanOfflineNextbotsDamagePlayers => IsOfflineRoundActive && Time.unscaledTime >= _nextbotDamageEnabledAtUnscaledTime;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!IsOfflineModeActive)
        {
            return;
        }

        SyncOfflinePlayerStates();
        if (IsOfflineRoundActive)
        {
            PollRoundStats();
        }
    }

    public bool StartOfflineMode(string localDisplayName, int localSkinIndex)
    {
        NetworkManager networkManager = NetworkManager.Instance;
        if (networkManager == null || networkManager.PlayerPrefab == null)
        {
            Debug.LogError("OfflineModeManager: Missing NetworkManager or player prefab.");
            return false;
        }

        StopOfflineMode();

        _spawnPositions.Clear();
        _spawnPositions.AddRange(networkManager.GetConfiguredPlayerSpawnPositions());
        if (_spawnPositions.Count == 0)
        {
            _spawnPositions.Add(Vector3.zero);
        }

        _nextbotSpawnPositions.Clear();
        _nextbotSpawnPositions.AddRange(networkManager.GetConfiguredNextbotSpawnPositions());
        BuildOfflinePatrolPositions(networkManager);

        SpawnOfflinePlayer(
            sessionId: "offline_local",
            displayName: string.IsNullOrWhiteSpace(localDisplayName) ? "Offline Player" : localDisplayName,
            skinIndex: localSkinIndex,
            spawnIndex: 0,
            isLocalPlayer: true,
            botIndex: -1);

        int botCount = Mathf.Max(0, _offlineTotalPlayerCount - 1);
        for (int botIndex = 0; botIndex < botCount; botIndex++)
        {
            int spawnIndex = botIndex + 1;
            int botSkinIndex = ResolveBotSkinIndex(localSkinIndex, botIndex);
            SpawnOfflinePlayer(
                sessionId: $"offline_bot_{botIndex + 1}",
                displayName: $"Runner {botIndex + 1}",
                skinIndex: botSkinIndex,
                spawnIndex: spawnIndex,
                isLocalPlayer: false,
                botIndex: botIndex);
        }

        IsOfflineModeActive = _offlinePlayers.Count > 0;
        networkManager.SetSimulatedLocalSessionId("offline_local");
        EnsureOfflineNextbots();
        StartOfflineRoundFlow();
        return IsOfflineModeActive;
    }

    public void StopOfflineMode()
    {
        bool wasActive = IsOfflineModeActive;
        StopOfflineRoundFlow();

        NextbotFollowPlayer[] nextbots = FindObjectsByType<NextbotFollowPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < nextbots.Length; i++)
        {
            if (nextbots[i] != null)
            {
                nextbots[i].SetOfflineNextbotActive(true);
                nextbots[i].SetOfflineLocalAuthority(false);
            }
        }

        NetworkManager networkManager = NetworkManager.Instance;
        foreach (KeyValuePair<string, GameObject> pair in _offlinePlayers)
        {
            if (networkManager != null)
            {
                networkManager.UnregisterSimulatedPlayerObject(pair.Key);
            }

            if (pair.Value != null)
            {
                Destroy(pair.Value);
            }
        }

        _offlinePlayers.Clear();
        _offlinePlayerStates.Clear();
        _rescueAssignments.Clear();
        _nextbotSpawnPositions.Clear();
        _patrolPositions.Clear();
        IsOfflineModeActive = false;
        _currentPhase = WaitingPhase;
        _roundIndex = 0;
        _phaseEndsAtUnscaledTime = 0f;
        _nextbotDamageEnabledAtUnscaledTime = 0f;

        if (wasActive && networkManager != null)
        {
            networkManager.PublishSimulatedRoomLeft();
        }
    }

    public bool TryRevivePlayer(GameObject reviverObject, string targetSessionId)
    {
        if (!TryGetSessionId(reviverObject, out string reviverSessionId))
        {
            return false;
        }

        return TryRevivePlayer(reviverSessionId, targetSessionId);
    }

    public bool TryRevivePlayer(string reviverSessionId, string targetSessionId)
    {
        if (!TryGetPlayerController(reviverSessionId, out PlayerController reviver)
            || !TryGetPlayerController(targetSessionId, out PlayerController target)
            || reviver == null
            || target == null
            || reviver == target)
        {
            return false;
        }

        if (reviver.IsInjuredOrHitReacting()
            || reviver.IsCarrying()
            || reviver.IsBeingCarried()
            || IsOfflinePlayerEliminated(reviverSessionId)
            || IsOfflinePlayerEliminated(targetSessionId)
            || !target.IsInjuredOrHitReacting())
        {
            return false;
        }

        if (GetPlanarDistance(reviver.transform.position, target.transform.position) > InteractionDistance)
        {
            return false;
        }

        ClearCarryStateForPlayer(targetSessionId);
        target.ApplyNetworkRevive();
        RecordOfflineRevive(reviverSessionId, targetSessionId);
        return true;
    }

    public bool TryToggleCarryPlayer(GameObject carrierObject, string targetSessionId)
    {
        if (!TryGetSessionId(carrierObject, out string carrierSessionId))
        {
            return false;
        }

        return TryToggleCarryPlayer(carrierSessionId, targetSessionId);
    }

    public bool TryToggleCarryPlayer(string carrierSessionId, string targetSessionId)
    {
        if (!TryGetPlayerController(carrierSessionId, out PlayerController carrier)
            || !TryGetPlayerController(targetSessionId, out PlayerController target)
            || carrier == null
            || target == null
            || carrier == target)
        {
            return false;
        }

        if (carrier.IsCarrying())
        {
            if (string.Equals(carrier.GetCarriedPlayerSessionId(), targetSessionId, System.StringComparison.Ordinal))
            {
                ClearCarryStateForPlayer(carrierSessionId);
                return true;
            }

            return false;
        }

        if (carrier.IsInjuredOrHitReacting()
            || carrier.IsBeingCarried()
            || IsOfflinePlayerEliminated(carrierSessionId)
            || IsOfflinePlayerEliminated(targetSessionId)
            || target.IsHitReacting()
            || !target.IsInjuredOrHitReacting()
            || target.IsCarrying()
            || target.IsBeingCarried())
        {
            return false;
        }

        if (GetPlanarDistance(carrier.transform.position, target.transform.position) > InteractionDistance)
        {
            return false;
        }

        carrier.ApplyNetworkCarryState(true, false, targetSessionId, string.Empty);
        target.ApplyNetworkCarryState(false, true, string.Empty, carrierSessionId);
        return true;
    }

    public void ClearCarryStateForPlayer(string sessionId)
    {
        if (!TryGetPlayerController(sessionId, out PlayerController player) || player == null)
        {
            return;
        }

        if (player.IsCarrying())
        {
            string carriedPlayerSessionId = player.GetCarriedPlayerSessionId();
            if (TryGetPlayerController(carriedPlayerSessionId, out PlayerController carriedPlayer) && carriedPlayer != null)
            {
                carriedPlayer.ApplyNetworkCarryState(false, false, string.Empty, string.Empty);
            }
        }

        if (player.IsBeingCarried())
        {
            string carrierSessionId = player.GetCarrierSessionId();
            if (TryGetPlayerController(carrierSessionId, out PlayerController carrier) && carrier != null)
            {
                carrier.ApplyNetworkCarryState(false, false, string.Empty, string.Empty);
            }
        }

        player.ApplyNetworkCarryState(false, false, string.Empty, string.Empty);
    }

    public bool TryGetNearestDownedPlayer(
        string requesterSessionId,
        Vector3 origin,
        out OfflinePlayerIdentity targetIdentity,
        out PlayerController targetController,
        out float distance)
    {
        targetIdentity = null;
        targetController = null;
        distance = float.PositiveInfinity;

        foreach (KeyValuePair<string, GameObject> pair in _offlinePlayers)
        {
            if (pair.Key == requesterSessionId || pair.Value == null)
            {
                continue;
            }

            OfflinePlayerIdentity candidateIdentity = pair.Value.GetComponent<OfflinePlayerIdentity>();
            PlayerController candidateController = pair.Value.GetComponent<PlayerController>();
            if (candidateIdentity == null
                || candidateController == null
                || candidateController.IsBeingCarried()
                || candidateController.IsCarrying()
                || IsOfflinePlayerEliminated(pair.Key)
                || !candidateController.IsInjuredOrHitReacting())
            {
                continue;
            }

            float candidateDistance = GetPlanarDistance(origin, candidateController.transform.position);
            if (candidateDistance >= distance)
            {
                continue;
            }

            distance = candidateDistance;
            targetIdentity = candidateIdentity;
            targetController = candidateController;
        }

        return targetIdentity != null && targetController != null;
    }

    public bool TryGetAssignedDownedPlayer(
        string requesterSessionId,
        Vector3 origin,
        out OfflinePlayerIdentity targetIdentity,
        out PlayerController targetController,
        out float distance)
    {
        targetIdentity = null;
        targetController = null;
        distance = float.PositiveInfinity;

        if (string.IsNullOrWhiteSpace(requesterSessionId))
        {
            return false;
        }

        PruneRescueAssignments();

        if (_rescueAssignments.TryGetValue(requesterSessionId, out string assignedTargetSessionId)
            && TryResolveRescueTarget(assignedTargetSessionId, out targetIdentity, out targetController))
        {
            distance = GetPlanarDistance(origin, targetController.transform.position);
            return true;
        }

        float bestDistance = float.PositiveInfinity;
        string bestTargetSessionId = string.Empty;
        foreach (KeyValuePair<string, GameObject> pair in _offlinePlayers)
        {
            if (pair.Key == requesterSessionId
                || CountRescueAssignmentsForTarget(pair.Key) >= MaxRescuersPerDownedPlayer
                || !TryResolveRescueTarget(pair.Key, out OfflinePlayerIdentity candidateIdentity, out PlayerController candidateController))
            {
                continue;
            }

            float candidateDistance = GetPlanarDistance(origin, candidateController.transform.position);
            if (candidateDistance >= bestDistance)
            {
                continue;
            }

            bestDistance = candidateDistance;
            bestTargetSessionId = pair.Key;
            targetIdentity = candidateIdentity;
            targetController = candidateController;
        }

        if (string.IsNullOrWhiteSpace(bestTargetSessionId) || targetIdentity == null || targetController == null)
        {
            return false;
        }

        _rescueAssignments[requesterSessionId] = bestTargetSessionId;
        distance = bestDistance;
        return true;
    }

    public void ReleaseRescueAssignment(string requesterSessionId)
    {
        if (string.IsNullOrWhiteSpace(requesterSessionId))
        {
            return;
        }

        _rescueAssignments.Remove(requesterSessionId);
    }

    private bool TryResolveRescueTarget(string targetSessionId, out OfflinePlayerIdentity targetIdentity, out PlayerController targetController)
    {
        targetIdentity = null;
        targetController = null;

        if (string.IsNullOrWhiteSpace(targetSessionId)
            || !_offlinePlayers.TryGetValue(targetSessionId, out GameObject playerObject)
            || playerObject == null
            || IsOfflinePlayerEliminated(targetSessionId))
        {
            return false;
        }

        targetIdentity = playerObject.GetComponent<OfflinePlayerIdentity>();
        targetController = playerObject.GetComponent<PlayerController>();
        return targetIdentity != null
            && targetController != null
            && !targetController.IsBeingCarried()
            && !targetController.IsCarrying()
            && targetController.IsInjuredOrHitReacting();
    }

    private void PruneRescueAssignments()
    {
        if (_rescueAssignments.Count == 0)
        {
            return;
        }

        List<string> assignmentsToRemove = new List<string>();
        foreach (KeyValuePair<string, string> assignment in _rescueAssignments)
        {
            if (!IsValidRescueAssignee(assignment.Key)
                || !IsValidAssignedRescueTarget(assignment.Key, assignment.Value))
            {
                assignmentsToRemove.Add(assignment.Key);
            }
        }

        for (int i = 0; i < assignmentsToRemove.Count; i++)
        {
            _rescueAssignments.Remove(assignmentsToRemove[i]);
        }
    }

    private bool IsValidRescueAssignee(string sessionId)
    {
        return TryGetPlayerController(sessionId, out PlayerController controller)
            && controller != null
            && !controller.IsInjuredOrHitReacting()
            && !controller.IsBeingCarried()
            && !IsOfflinePlayerEliminated(sessionId);
    }

    private bool IsValidAssignedRescueTarget(string assigneeSessionId, string targetSessionId)
    {
        if (TryResolveRescueTarget(targetSessionId, out _, out _))
        {
            return true;
        }

        return TryGetPlayerController(targetSessionId, out PlayerController targetController)
            && targetController != null
            && targetController.IsBeingCarried()
            && string.Equals(targetController.GetCarrierSessionId(), assigneeSessionId, System.StringComparison.Ordinal)
            && !IsOfflinePlayerEliminated(targetSessionId);
    }

    private int CountRescueAssignmentsForTarget(string targetSessionId)
    {
        int count = 0;
        foreach (string assignedTargetSessionId in _rescueAssignments.Values)
        {
            if (string.Equals(assignedTargetSessionId, targetSessionId, System.StringComparison.Ordinal))
            {
                count += 1;
            }
        }

        return count;
    }

    private void ReleaseRescueAssignmentsForTarget(string targetSessionId)
    {
        if (string.IsNullOrWhiteSpace(targetSessionId) || _rescueAssignments.Count == 0)
        {
            return;
        }

        List<string> assignmentsToRemove = new List<string>();
        foreach (KeyValuePair<string, string> assignment in _rescueAssignments)
        {
            if (string.Equals(assignment.Value, targetSessionId, System.StringComparison.Ordinal))
            {
                assignmentsToRemove.Add(assignment.Key);
            }
        }

        for (int i = 0; i < assignmentsToRemove.Count; i++)
        {
            _rescueAssignments.Remove(assignmentsToRemove[i]);
        }
    }

    public bool TryGetNearestActivePlayer(
        string requesterSessionId,
        Vector3 origin,
        out Transform playerTransform,
        out float distance)
    {
        playerTransform = null;
        distance = float.PositiveInfinity;

        foreach (KeyValuePair<string, GameObject> pair in _offlinePlayers)
        {
            if (pair.Key == requesterSessionId || pair.Value == null || IsOfflinePlayerEliminated(pair.Key))
            {
                continue;
            }

            PlayerController candidateController = pair.Value.GetComponent<PlayerController>();
            if (candidateController == null
                || candidateController.IsInjuredOrHitReacting()
                || candidateController.IsBeingCarried())
            {
                continue;
            }

            float candidateDistance = GetPlanarDistance(origin, candidateController.transform.position);
            if (candidateDistance >= distance)
            {
                continue;
            }

            distance = candidateDistance;
            playerTransform = candidateController.transform;
        }

        return playerTransform != null;
    }

    public bool TryGetNearestThreat(Vector3 origin, out Transform threatTransform, out float distance)
    {
        threatTransform = null;
        distance = float.PositiveInfinity;

        if (!IsOfflineRoundActive)
        {
            return false;
        }

        NextbotFollowPlayer[] nextbots = FindObjectsByType<NextbotFollowPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < nextbots.Length; i++)
        {
            NextbotFollowPlayer nextbot = nextbots[i];
            if (nextbot == null || !nextbot.gameObject.activeInHierarchy)
            {
                continue;
            }

            float candidateDistance = GetPlanarDistance(origin, nextbot.transform.position);
            if (candidateDistance >= distance)
            {
                continue;
            }

            distance = candidateDistance;
            threatTransform = nextbot.transform;
        }

        return threatTransform != null;
    }

    public Vector3 GetRetreatPosition(Vector3 currentPosition)
    {
        Vector3 bestPosition = _spawnPositions.Count > 0 ? _spawnPositions[0] : currentPosition;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < _spawnPositions.Count; i++)
        {
            Vector3 candidate = _spawnPositions[i];
            float nearestThreatDistance = GetNearestThreatDistance(candidate);
            float score = nearestThreatDistance - Vector3.Distance(currentPosition, candidate) * 0.15f;
            if (score > bestScore)
            {
                bestScore = score;
                bestPosition = candidate;
            }
        }

        return bestPosition;
    }

    public Vector3 GetPatrolPosition(int botIndex)
    {
        return GetPatrolPosition(botIndex, Vector3.zero);
    }

    public Vector3 GetPatrolPosition(int botIndex, Vector3 currentPosition)
    {
        if (_patrolPositions.Count == 0)
        {
            return _spawnPositions.Count > 0 ? _spawnPositions[Mathf.Abs(botIndex) % _spawnPositions.Count] : Vector3.zero;
        }

        Vector3 selectedPosition = _patrolPositions[Mathf.Abs(botIndex) % _patrolPositions.Count];
        float selectedDistance = GetPlanarDistance(currentPosition, selectedPosition);

        for (int attempts = 0; attempts < 8; attempts++)
        {
            Vector3 candidate = _patrolPositions[Random.Range(0, _patrolPositions.Count)];
            float candidateDistance = GetPlanarDistance(currentPosition, candidate);
            if (candidateDistance > selectedDistance)
            {
                selectedPosition = candidate;
                selectedDistance = candidateDistance;
            }
        }

        return selectedPosition;
    }

    private void StartOfflineRoundFlow()
    {
        StopOfflineRoundFlow();
        _roundFlowCoroutine = StartCoroutine(RunOfflineRoundFlow());
    }

    private void StopOfflineRoundFlow()
    {
        if (_roundFlowCoroutine == null)
        {
            return;
        }

        StopCoroutine(_roundFlowCoroutine);
        _roundFlowCoroutine = null;
    }

    private IEnumerator RunOfflineRoundFlow()
    {
        BeginOfflineIntermission(null);

        while (IsOfflineModeActive)
        {
            yield return WaitForCurrentPhaseToEnd();
            if (!IsOfflineModeActive)
            {
                break;
            }

            BeginOfflineRound();
            yield return WaitForCurrentPhaseToEnd();
            if (!IsOfflineModeActive)
            {
                break;
            }

            RoundResultsMessageData results = BuildRoundResults();
            BeginOfflineIntermission(results);
        }

        _roundFlowCoroutine = null;
    }

    private IEnumerator WaitForCurrentPhaseToEnd()
    {
        while (IsOfflineModeActive && Time.unscaledTime < _phaseEndsAtUnscaledTime)
        {
            yield return null;
        }
    }

    private void BeginOfflineIntermission(RoundResultsMessageData pendingResults)
    {
        _currentPhase = IntermissionPhase;
        _phaseEndsAtUnscaledTime = Time.unscaledTime + GetIntermissionDurationSeconds();
        _nextbotDamageEnabledAtUnscaledTime = float.PositiveInfinity;

        ResetOfflinePlayersForPhase(roundStarted: false);
        ResetOfflineNextbots(active: false);
        PublishRoundPhase(IntermissionPhase, _roundIndex);

        if (pendingResults != null)
        {
            NetworkManager.Instance?.PublishSimulatedRoundResults(pendingResults);
        }
    }

    private void BeginOfflineRound()
    {
        _currentPhase = RoundPhase;
        _roundIndex += 1;
        _phaseEndsAtUnscaledTime = Time.unscaledTime + GetRoundDurationSeconds();
        _nextbotDamageEnabledAtUnscaledTime = Time.unscaledTime + NextbotStartGraceSeconds;

        ResetOfflinePlayersForPhase(roundStarted: true);
        ResetOfflineNextbots(active: true);
        PublishRoundPhase(RoundPhase, _roundIndex);
        NetworkManager.Instance?.PublishSimulatedRoundAnnouncement(new RoundAnnouncementMessageData
        {
            title = "ROUND STARTED",
            subtitle = "SURVIVE FOR 3 MINUTES",
            durationSeconds = 3f,
        });
    }

    private void PublishRoundPhase(string phase, int roundIndex)
    {
        NetworkManager.Instance?.PublishSimulatedRoundPhase(new RoundPhaseMessageData
        {
            phase = phase,
            roundIndex = roundIndex,
            timeRemainingMs = Mathf.Max(0, Mathf.RoundToInt((_phaseEndsAtUnscaledTime - Time.unscaledTime) * 1000f)),
            roundDurationMs = GetRoundDurationMs(),
            intermissionDurationMs = GetIntermissionDurationMs(),
        });
    }

    private void ResetOfflinePlayersForPhase(bool roundStarted)
    {
        _rescueAssignments.Clear();

        foreach (KeyValuePair<string, OfflinePlayerRoundState> pair in _offlinePlayerStates)
        {
            OfflinePlayerRoundState state = pair.Value;
            if (state == null || state.PlayerObject == null)
            {
                continue;
            }

            ClearCarryStateForPlayer(pair.Key);

            PlayerController controller = state.Controller != null
                ? state.Controller
                : state.PlayerObject.GetComponent<PlayerController>();
            Vector3 resetPosition = roundStarted ? state.PlayerObject.transform.position : ResolveSpawnPosition(state.SpawnIndex);
            float resetYaw = roundStarted ? state.PlayerObject.transform.eulerAngles.y : PlayerSpawnRotationY;
            if (controller != null)
            {
                controller.ApplyNetworkRoundReset(resetPosition, resetYaw);
                state.Controller = controller;
            }
            else
            {
                state.PlayerObject.transform.SetPositionAndRotation(resetPosition, Quaternion.Euler(0f, resetYaw, 0f));
            }

            PlayerLocomotionInput locomotionInput = state.LocomotionInput != null
                ? state.LocomotionInput
                : state.PlayerObject.GetComponent<PlayerLocomotionInput>();
            locomotionInput?.ResetSimulationState();
            state.LocomotionInput = locomotionInput;

            state.WasDowned = false;
            state.IsEliminated = false;
            state.CurrentLifeStartUnscaledTime = roundStarted ? Time.unscaledTime : -1f;
            if (roundStarted)
            {
                state.BestTimeMs = 0;
                state.DownedCount = 0;
                state.RevivesDone = 0;
            }

            ResetPlayerStateForPhase(state, resetPosition, resetYaw);
            SyncPlayerState(state);
        }
    }

    private void ResetPlayerStateForPhase(OfflinePlayerRoundState state, Vector3 resetPosition, float resetYaw)
    {
        if (state == null || state.PlayerState == null)
        {
            return;
        }

        state.PlayerState.x = resetPosition.x;
        state.PlayerState.y = resetPosition.y;
        state.PlayerState.z = resetPosition.z;
        state.PlayerState.rotationY = resetYaw;
        state.PlayerState.visualYaw = resetYaw;
        state.PlayerState.velocityX = 0f;
        state.PlayerState.velocityY = 0f;
        state.PlayerState.velocityZ = 0f;
        state.PlayerState.animInputX = 0f;
        state.PlayerState.animInputY = 0f;
        state.PlayerState.moveInputX = 0f;
        state.PlayerState.moveInputY = 0f;
        state.PlayerState.isGrounded = true;
        state.PlayerState.isJumping = false;
        state.PlayerState.isInjured = false;
        state.PlayerState.isEliminated = false;
        state.PlayerState.isHitReacting = false;
        state.PlayerState.hitReactionTimeRemaining = 0f;
        state.PlayerState.hitReactionPitch = 0f;
        state.PlayerState.hitReactionRoll = 0f;
        state.PlayerState.hitReactionSeed = 0f;
        state.PlayerState.hitTriggerId = 0f;
        state.PlayerState.hitSourceX = 0f;
        state.PlayerState.hitSourceY = 0f;
        state.PlayerState.hitSourceZ = 0f;
        state.PlayerState.isCrouching = false;
        state.PlayerState.isWallRunning = false;
        state.PlayerState.wallRunSide = 0f;
        state.PlayerState.isCarrying = false;
        state.PlayerState.isBeingCarried = false;
        state.PlayerState.carriedPlayerSessionId = string.Empty;
        state.PlayerState.carrierSessionId = string.Empty;
        state.PlayerState.speedBoostMultiplier = 1f;
        state.PlayerState.speedBoostTimeRemaining = 0f;
        state.PlayerState.jumpBoostMultiplier = 1f;
        state.PlayerState.jumpBoostTimeRemaining = 0f;
        state.PlayerState.isSpectator = false;
    }

    private void ResetOfflineNextbots(bool active)
    {
        NextbotFollowPlayer[] nextbots = FindObjectsByType<NextbotFollowPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < nextbots.Length; i++)
        {
            NextbotFollowPlayer nextbot = nextbots[i];
            if (nextbot == null)
            {
                continue;
            }

            if (!nextbot.gameObject.activeSelf)
            {
                nextbot.gameObject.SetActive(true);
            }

            nextbot.PrepareOfflineNextbot(ResolveNextbotSpawnPosition(i));
            nextbot.SetOfflineNextbotActive(active);
        }
    }

    private void PollRoundStats()
    {
        foreach (KeyValuePair<string, OfflinePlayerRoundState> pair in _offlinePlayerStates)
        {
            OfflinePlayerRoundState state = pair.Value;
            if (state == null || state.Controller == null)
            {
                continue;
            }

            bool isDowned = state.Controller.IsInjuredOrHitReacting();
            if (isDowned && !state.WasDowned)
            {
                RecordPlayerDowned(state);
            }
            else if (!isDowned && state.WasDowned && !state.IsEliminated)
            {
                state.WasDowned = false;
                state.CurrentLifeStartUnscaledTime = Time.unscaledTime;
            }
        }
    }

    private void RecordPlayerDowned(OfflinePlayerRoundState state)
    {
        if (state == null)
        {
            return;
        }

        if (state.CurrentLifeStartUnscaledTime >= 0f)
        {
            int runTimeMs = Mathf.Max(0, Mathf.RoundToInt((Time.unscaledTime - state.CurrentLifeStartUnscaledTime) * 1000f));
            state.BestTimeMs = Mathf.Max(state.BestTimeMs, runTimeMs);
        }

        state.DownedCount += 1;
        state.CurrentLifeStartUnscaledTime = -1f;
        state.WasDowned = true;

        if (state.DownedCount >= PlayerMaxDownsBeforeElimination)
        {
            state.IsEliminated = true;
            state.Controller?.ApplyNetworkEliminated();
            ReleaseRescueAssignmentsForTarget(state.SessionId);
        }
    }

    private void RecordOfflineRevive(string reviverSessionId, string targetSessionId)
    {
        if (!IsOfflineRoundActive)
        {
            return;
        }

        if (_offlinePlayerStates.TryGetValue(reviverSessionId, out OfflinePlayerRoundState reviverState) && reviverState != null)
        {
            reviverState.RevivesDone += 1;
        }

        if (_offlinePlayerStates.TryGetValue(targetSessionId, out OfflinePlayerRoundState targetState) && targetState != null)
        {
            targetState.WasDowned = false;
            targetState.IsEliminated = false;
            targetState.CurrentLifeStartUnscaledTime = Time.unscaledTime;
        }

        ReleaseRescueAssignment(reviverSessionId);
        ReleaseRescueAssignmentsForTarget(targetSessionId);
    }

    private RoundResultsMessageData BuildRoundResults()
    {
        List<RoundResultEntryMessageData> entries = new List<RoundResultEntryMessageData>();
        foreach (OfflinePlayerRoundState state in _offlinePlayerStates.Values)
        {
            if (state == null)
            {
                continue;
            }

            if (state.CurrentLifeStartUnscaledTime >= 0f)
            {
                int runTimeMs = Mathf.Max(0, Mathf.RoundToInt((Time.unscaledTime - state.CurrentLifeStartUnscaledTime) * 1000f));
                state.BestTimeMs = Mathf.Max(state.BestTimeMs, runTimeMs);
            }

            entries.Add(new RoundResultEntryMessageData
            {
                sessionId = state.SessionId,
                displayName = state.DisplayName,
                bestTimeMs = state.BestTimeMs,
                downedCount = state.DownedCount,
                revivesDone = state.RevivesDone,
                joinOrder = state.JoinOrder,
                rank = 0,
            });
        }

        entries.Sort(CompareRoundResultEntries);
        for (int i = 0; i < entries.Count; i++)
        {
            RoundResultEntryMessageData previous = i > 0 ? entries[i - 1] : null;
            RoundResultEntryMessageData current = entries[i];
            if (previous != null
                && previous.bestTimeMs == current.bestTimeMs
                && previous.downedCount == current.downedCount
                && previous.revivesDone == current.revivesDone)
            {
                current.rank = previous.rank;
            }
            else
            {
                current.rank = i + 1;
            }
        }

        return new RoundResultsMessageData
        {
            roundIndex = _roundIndex,
            roundDurationMs = GetRoundDurationMs(),
            entries = entries.ToArray(),
        };
    }

    private static int CompareRoundResultEntries(RoundResultEntryMessageData a, RoundResultEntryMessageData b)
    {
        int bestTimeCompare = b.bestTimeMs.CompareTo(a.bestTimeMs);
        if (bestTimeCompare != 0)
        {
            return bestTimeCompare;
        }

        int downedCompare = a.downedCount.CompareTo(b.downedCount);
        if (downedCompare != 0)
        {
            return downedCompare;
        }

        int revivesCompare = b.revivesDone.CompareTo(a.revivesDone);
        if (revivesCompare != 0)
        {
            return revivesCompare;
        }

        return a.joinOrder.CompareTo(b.joinOrder);
    }

    private void SyncOfflinePlayerStates()
    {
        foreach (OfflinePlayerRoundState state in _offlinePlayerStates.Values)
        {
            SyncPlayerState(state);
        }
    }

    private void SyncPlayerState(OfflinePlayerRoundState state)
    {
        if (state == null || state.PlayerState == null || state.PlayerObject == null)
        {
            return;
        }

        Transform playerTransform = state.PlayerObject.transform;
        PlayerController controller = state.Controller != null
            ? state.Controller
            : state.PlayerObject.GetComponent<PlayerController>();
        PlayerLocomotionInput locomotionInput = state.LocomotionInput != null
            ? state.LocomotionInput
            : state.PlayerObject.GetComponent<PlayerLocomotionInput>();
        PlayerAnimation animation = state.Animation != null
            ? state.Animation
            : state.PlayerObject.GetComponent<PlayerAnimation>();

        state.Controller = controller;
        state.LocomotionInput = locomotionInput;
        state.Animation = animation;

        Vector3 velocity = controller != null ? controller.GetVelocity() : Vector3.zero;
        Vector2 moveInput = locomotionInput != null ? locomotionInput.MovementInput : Vector2.zero;
        Vector2 animationInput = moveInput;
        bool isGrounded = controller == null || controller.IsGrounded();
        bool isJumping = controller != null && controller.DidJumpThisFrame();
        float verticalSpeed = velocity.y;
        bool isHitReacting = false;
        float hitReactionTimeRemaining = 0f;
        float hitReactionPitch = 0f;
        float hitReactionRoll = 0f;
        float hitReactionSeed = 0f;

        if (animation != null)
        {
            animation.GetAnimationSyncState(out animationInput, out isGrounded, out isJumping, out verticalSpeed);
        }

        if (controller != null)
        {
            controller.GetHitReactionSyncState(
                out isHitReacting,
                out hitReactionTimeRemaining,
                out hitReactionPitch,
                out hitReactionRoll,
                out hitReactionSeed);
        }

        bool isEliminated = state.IsEliminated;
        bool isInjured = controller != null && controller.IsInjuredOrHitReacting();
        if (!IsOfflineRoundActive && !isEliminated)
        {
            isInjured = false;
            isHitReacting = false;
            hitReactionTimeRemaining = 0f;
            hitReactionPitch = 0f;
            hitReactionRoll = 0f;
            hitReactionSeed = 0f;
        }

        if (isEliminated)
        {
            velocity = Vector3.zero;
            moveInput = Vector2.zero;
            animationInput = Vector2.zero;
            isJumping = false;
            isInjured = true;
            isHitReacting = false;
            hitReactionTimeRemaining = 0f;
            hitReactionPitch = 0f;
            hitReactionRoll = 0f;
            hitReactionSeed = 0f;
        }

        state.PlayerState.x = playerTransform.position.x;
        state.PlayerState.y = playerTransform.position.y;
        state.PlayerState.z = playerTransform.position.z;
        state.PlayerState.rotationY = playerTransform.eulerAngles.y;
        state.PlayerState.velocityX = velocity.x;
        state.PlayerState.velocityY = velocity.y;
        state.PlayerState.velocityZ = velocity.z;
        state.PlayerState.animInputX = animationInput.x;
        state.PlayerState.animInputY = animationInput.y;
        state.PlayerState.isGrounded = isGrounded;
        state.PlayerState.isJumping = isJumping;
        state.PlayerState.isInjured = isInjured;
        state.PlayerState.isEliminated = isEliminated;
        state.PlayerState.isCrouching = !isEliminated && controller != null && controller.IsCrouching();
        state.PlayerState.isWallRunning = !isEliminated && controller != null && controller.IsWallRunning();
        state.PlayerState.wallRunSide = !isEliminated && controller != null ? controller.GetWallRunSide() : 0f;
        state.PlayerState.moveInputX = moveInput.x;
        state.PlayerState.moveInputY = moveInput.y;
        state.PlayerState.visualYaw = controller != null ? controller.GetVisualYaw() : playerTransform.eulerAngles.y;
        Vector2 cameraRotation = controller != null ? controller.GetCameraRotation() : Vector2.zero;
        state.PlayerState.cameraRotationX = cameraRotation.x;
        state.PlayerState.cameraRotationY = cameraRotation.y;
        state.PlayerState.isHitReacting = isHitReacting;
        state.PlayerState.hitReactionTimeRemaining = hitReactionTimeRemaining;
        state.PlayerState.hitReactionPitch = hitReactionPitch;
        state.PlayerState.hitReactionRoll = hitReactionRoll;
        state.PlayerState.hitReactionSeed = hitReactionSeed;
        state.PlayerState.timestamp = Time.unscaledTime * 1000f;
        state.PlayerState.isReady = IsOfflineModeActive;
        state.PlayerState.isSpectator = false;
        state.PlayerState.isCarrying = !isEliminated && controller != null && controller.IsCarrying();
        state.PlayerState.isBeingCarried = !isEliminated && controller != null && controller.IsBeingCarried();
        state.PlayerState.carriedPlayerSessionId = !isEliminated && controller != null ? controller.GetCarriedPlayerSessionId() : string.Empty;
        state.PlayerState.carrierSessionId = !isEliminated && controller != null ? controller.GetCarrierSessionId() : string.Empty;
        state.PlayerState.speedBoostMultiplier = !isEliminated && IsOfflineRoundActive && controller != null ? controller.GetSyncedSpeedBoostMultiplier() : 1f;
        state.PlayerState.speedBoostTimeRemaining = !isEliminated && IsOfflineRoundActive && controller != null ? controller.GetSyncedSpeedBoostTimeRemaining() : 0f;
        state.PlayerState.jumpBoostMultiplier = !isEliminated && IsOfflineRoundActive && controller != null ? controller.GetSyncedJumpBoostMultiplier() : 1f;
        state.PlayerState.jumpBoostTimeRemaining = !isEliminated && IsOfflineRoundActive && controller != null ? controller.GetSyncedJumpBoostTimeRemaining() : 0f;
    }

    private int GetIntermissionDurationMs()
    {
        return NetworkManager.Instance != null
            ? NetworkManager.Instance.IntermissionDurationMilliseconds
            : 30000;
    }

    private int GetRoundDurationMs()
    {
        return NetworkManager.Instance != null
            ? NetworkManager.Instance.RoundDurationMilliseconds
            : 180000;
    }

    private float GetIntermissionDurationSeconds()
    {
        return GetIntermissionDurationMs() / 1000f;
    }

    private float GetRoundDurationSeconds()
    {
        return GetRoundDurationMs() / 1000f;
    }

    private void EnsureOfflineNextbots()
    {
        NextbotSpawner spawner = FindFirstObjectByType<NextbotSpawner>();
        if (spawner == null)
        {
            GameObject spawnerObject = new GameObject("NextbotSpawner");
            spawner = spawnerObject.AddComponent<NextbotSpawner>();
        }

        spawner.RefreshNow();

        NextbotFollowPlayer[] nextbots = FindObjectsByType<NextbotFollowPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (nextbots == null || nextbots.Length == 0)
        {
            Debug.LogWarning("OfflineModeManager: No nextbots were found or spawned for offline mode.");
            return;
        }

        for (int i = 0; i < nextbots.Length; i++)
        {
            NextbotFollowPlayer nextbot = nextbots[i];
            if (nextbot == null)
            {
                continue;
            }

            if (!nextbot.gameObject.activeSelf)
            {
                nextbot.gameObject.SetActive(true);
            }

            Vector3 spawnPosition = ResolveNextbotSpawnPosition(i);
            nextbot.PrepareOfflineNextbot(spawnPosition);
            nextbot.SetOfflineNextbotActive(IsOfflineRoundActive);
        }
    }

    private Vector3 ResolveNextbotSpawnPosition(int index)
    {
        if (_nextbotSpawnPositions.Count > 0)
        {
            return _nextbotSpawnPositions[Mathf.Abs(index) % _nextbotSpawnPositions.Count];
        }

        if (_patrolPositions.Count > 0)
        {
            return _patrolPositions[Mathf.Abs(index) % _patrolPositions.Count];
        }

        return _spawnPositions.Count > 0 ? _spawnPositions[0] : Vector3.zero;
    }

    private void BuildOfflinePatrolPositions(NetworkManager networkManager)
    {
        _patrolPositions.Clear();
        AddUniquePatrolPositions(networkManager.GetConfiguredNextbotPatrolPositions());
        AddUniquePatrolPositions(_nextbotSpawnPositions);
        AddUniquePatrolPositions(_spawnPositions);

        int originalCount = _patrolPositions.Count;
        for (int i = 0; i < originalCount; i++)
        {
            Vector3 origin = _patrolPositions[i];
            for (int sampleIndex = 0; sampleIndex < 4; sampleIndex++)
            {
                float angle = (sampleIndex * 90f + i * 37f) * Mathf.Deg2Rad;
                float distance = 10f + sampleIndex * 5f;
                Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (TrySampleNavMeshPosition(candidate, out Vector3 sampledPosition))
                {
                    AddUniquePatrolPosition(sampledPosition);
                }
            }
        }

        if (_patrolPositions.Count == 0)
        {
            _patrolPositions.Add(Vector3.zero);
        }
    }

    private void AddUniquePatrolPositions(List<Vector3> positions)
    {
        if (positions == null)
        {
            return;
        }

        for (int i = 0; i < positions.Count; i++)
        {
            AddUniquePatrolPosition(positions[i]);
        }
    }

    private void AddUniquePatrolPosition(Vector3 position)
    {
        for (int i = 0; i < _patrolPositions.Count; i++)
        {
            if (GetPlanarDistance(_patrolPositions[i], position) < 2f)
            {
                return;
            }
        }

        _patrolPositions.Add(position);
    }

    private static bool TrySampleNavMeshPosition(Vector3 candidate, out Vector3 sampledPosition)
    {
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 8f, NavMesh.AllAreas))
        {
            sampledPosition = hit.position;
            return true;
        }

        sampledPosition = candidate;
        return false;
    }

    private GameObject SpawnOfflinePlayer(
        string sessionId,
        string displayName,
        int skinIndex,
        int spawnIndex,
        bool isLocalPlayer,
        int botIndex)
    {
        NetworkManager networkManager = NetworkManager.Instance;
        Vector3 spawnPosition = ResolveSpawnPosition(spawnIndex);
        GameObject playerObject = Instantiate(networkManager.PlayerPrefab, spawnPosition, Quaternion.Euler(0f, PlayerSpawnRotationY, 0f));
        playerObject.name = isLocalPlayer ? "LocalPlayer" : $"OfflinePlayer_{displayName}";

        OfflinePlayerIdentity identity = playerObject.GetComponent<OfflinePlayerIdentity>();
        if (identity == null)
        {
            identity = playerObject.AddComponent<OfflinePlayerIdentity>();
        }
        identity.Initialize(sessionId, displayName, isLocalPlayer);

        NetworkPlayer networkPlayer = playerObject.GetComponent<NetworkPlayer>();
        if (networkPlayer != null)
        {
            networkPlayer.enabled = false;
        }

        PlayerLocomotionInput locomotionInput = playerObject.GetComponent<PlayerLocomotionInput>();
        if (locomotionInput != null)
        {
            locomotionInput.InputEnabled = !isLocalPlayer;
            locomotionInput.SetSimulatedInputEnabled(!isLocalPlayer);
            locomotionInput.ResetSimulationState();
        }

        PlayerController controller = playerObject.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.SetSimulationControlled(!isLocalPlayer);
        }

        Player simulatedState = CreateSimulatedPlayerState(
            sessionId,
            displayName,
            skinIndex,
            spawnPosition,
            PlayerSpawnRotationY);
        InitializeAppearance(playerObject, simulatedState, skinIndex);
        networkManager.RegisterSimulatedPlayerObject(sessionId, playerObject, simulatedState);
        _offlinePlayers[sessionId] = playerObject;
        _offlinePlayerStates[sessionId] = new OfflinePlayerRoundState
        {
            SessionId = sessionId,
            DisplayName = displayName,
            JoinOrder = _offlinePlayerStates.Count,
            SpawnIndex = spawnIndex,
            PlayerObject = playerObject,
            Controller = controller,
            LocomotionInput = locomotionInput,
            Animation = playerObject.GetComponent<PlayerAnimation>(),
            PlayerState = simulatedState,
        };

        if (!isLocalPlayer)
        {
            OfflinePlayerBotBrain botBrain = playerObject.GetComponent<OfflinePlayerBotBrain>();
            if (botBrain == null)
            {
                botBrain = playerObject.AddComponent<OfflinePlayerBotBrain>();
            }

            botBrain.Initialize(botIndex);
        }

        return playerObject;
    }

    private Player CreateSimulatedPlayerState(
        string sessionId,
        string displayName,
        int skinIndex,
        Vector3 spawnPosition,
        float rotationY)
    {
        return new Player
        {
            sessionId = sessionId,
            displayName = displayName,
            x = spawnPosition.x,
            y = spawnPosition.y,
            z = spawnPosition.z,
            rotationY = rotationY,
            visualYaw = rotationY,
            isReady = true,
            isSpectator = false,
            skinIndex = skinIndex,
            isGrounded = true,
            speedBoostMultiplier = 1f,
            jumpBoostMultiplier = 1f,
        };
    }

    private void ApplySkin(GameObject playerObject, int skinIndex)
    {
        PlayerAppearance appearance = playerObject.GetComponent<PlayerAppearance>();
        if (appearance == null || appearance.skinRegistry == null)
        {
            return;
        }

        Renderer[] renderers = appearance.GetTargetRenderers();
        int validSkinIndex = Mathf.Clamp(
            skinIndex,
            0,
            Mathf.Max(0, appearance.skinRegistry.skins != null ? appearance.skinRegistry.skins.Length - 1 : 0));

        PlayerAppearance.ApplySkinToRenderers(appearance.skinRegistry, validSkinIndex, renderers);
    }

    private void InitializeAppearance(GameObject playerObject, Player simulatedState, int fallbackSkinIndex)
    {
        PlayerAppearance appearance = playerObject.GetComponent<PlayerAppearance>();
        if (appearance != null && simulatedState != null)
        {
            appearance.Initialize(simulatedState);
            return;
        }

        ApplySkin(playerObject, fallbackSkinIndex);
    }

    private int ResolveBotSkinIndex(int localSkinIndex, int botIndex)
    {
        PlayerAppearance appearanceTemplate = NetworkManager.Instance != null && NetworkManager.Instance.PlayerPrefab != null
            ? NetworkManager.Instance.PlayerPrefab.GetComponent<PlayerAppearance>()
            : null;
        int skinCount = appearanceTemplate != null && appearanceTemplate.skinRegistry != null && appearanceTemplate.skinRegistry.skins != null
            ? appearanceTemplate.skinRegistry.skins.Length
            : 0;

        if (skinCount <= 0)
        {
            return 0;
        }

        return Mathf.Abs(localSkinIndex + botIndex + 1) % skinCount;
    }

    private Vector3 ResolveSpawnPosition(int spawnIndex)
    {
        if (_spawnPositions.Count == 0)
        {
            return Vector3.zero;
        }

        Vector3 basePosition = _spawnPositions[Mathf.Abs(spawnIndex) % _spawnPositions.Count];
        int ringIndex = Mathf.Max(0, spawnIndex / Mathf.Max(1, _spawnPositions.Count));
        if (ringIndex == 0)
        {
            return basePosition;
        }

        float angle = (spawnIndex * 137.5f) * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (_spawnRingRadius * ringIndex);
        return basePosition + offset;
    }

    private bool TryGetSessionId(GameObject playerObject, out string sessionId)
    {
        sessionId = string.Empty;
        if (playerObject == null)
        {
            return false;
        }

        OfflinePlayerIdentity identity = playerObject.GetComponent<OfflinePlayerIdentity>();
        if (identity == null || string.IsNullOrWhiteSpace(identity.SessionId))
        {
            return false;
        }

        sessionId = identity.SessionId;
        return true;
    }

    private bool TryGetPlayerController(string sessionId, out PlayerController controller)
    {
        controller = null;
        if (!_offlinePlayers.TryGetValue(sessionId, out GameObject playerObject) || playerObject == null)
        {
            return false;
        }

        controller = playerObject.GetComponent<PlayerController>();
        return controller != null;
    }

    private bool IsOfflinePlayerEliminated(string sessionId)
    {
        return !string.IsNullOrWhiteSpace(sessionId)
            && _offlinePlayerStates.TryGetValue(sessionId, out OfflinePlayerRoundState state)
            && state != null
            && state.IsEliminated;
    }

    private float GetNearestThreatDistance(Vector3 position)
    {
        if (TryGetNearestThreat(position, out _, out float distance))
        {
            return distance;
        }

        return 999f;
    }

    private static float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private sealed class OfflinePlayerRoundState
    {
        public string SessionId;
        public string DisplayName;
        public int JoinOrder;
        public int SpawnIndex;
        public GameObject PlayerObject;
        public PlayerController Controller;
        public PlayerLocomotionInput LocomotionInput;
        public PlayerAnimation Animation;
        public Player PlayerState;
        public int BestTimeMs;
        public int DownedCount;
        public int RevivesDone;
        public float CurrentLifeStartUnscaledTime = -1f;
        public bool WasDowned;
        public bool IsEliminated;
    }
}
