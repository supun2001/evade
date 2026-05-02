using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NextbotFollowPlayer : MonoBehaviour
{
    [Serializable]
    private class MapNextbotSettings
    {
        public string mapId = string.Empty;
        public bool useLocalRoomStateNavMesh = true;
        public float targetHoldSeconds = 1f;
        public float maxChaseRange = 1000f;
        public float navMeshRejoinWarpDistance = 0.3f;
    }

    private const int MaxGroundHitBufferSize = 16;
    private const float MultiplayerNextbotStoppingDistance = 0.7f;
    private const float MultiplayerNextbotInjuryDistance = 0.95f;
    private const float MultiplayerNextbotInjuryCooldownSeconds = 1.2f;
    private const float MultiplayerNextbotScanIntervalSeconds = 0.25f;
    private const float MultiplayerNextbotTargetLockSeconds = 1.4f;
    private const float MultiplayerNextbotSwitchScoreThreshold = 20f;
    private const float MultiplayerNextbotSwitchConfirmSeconds = 0.3f;
    private const float OfflineLocalPlayerPressureBonus = 10000f;
    private const float OfflinePatrolReachedDistance = 1.1f;
    private const float OfflinePatrolRetargetMinSeconds = 1f;
    private const float OfflinePatrolRetargetMaxSeconds = 2f;
    private const float ParkourFallbackPatrolRadius = 8f;
    private static readonly MapNextbotSettings DefaultMapNextbotSettings = new MapNextbotSettings();

    [Header("Networking")]
    [SerializeField] private bool _useRoomStateAuthority = true;
    [SerializeField] private string _networkNextbotId = "nextbot_0";
    [SerializeField] private float _roomStatePositionLerpSpeed = 12f;
    [SerializeField] private float _roomStateRotationLerpSpeed = 14f;
    [SerializeField] private float _roomStateSnapDistance = 1.1f;
    [SerializeField] private float _roomStateChaseResyncDistance = 12f;
    [SerializeField] private float _roomStatePredictionTime = 0.1f;
    [SerializeField] private float _remoteRoomStatePositionLerpSpeed = 12f;
    [SerializeField] private float _remoteRoomStateRotationLerpSpeed = 14f;
    [SerializeField] private float _remoteRoomStateSnapDistance = 100f;
    [SerializeField] private float _remoteRoomStatePredictionTime = 0.18f;
    [SerializeField] private float _remoteRoomStateCatchUpBoost = 1.75f;
    [SerializeField] private float _remoteRoomStateInterpolationBackTime = 0.3f;
    [SerializeField] private float _remoteRoomStateMaxExtrapolationTime = 0.01f;
    [SerializeField] private float _remoteRoomStatePresentationSmoothTime = 0.12f;
    [SerializeField] private float _remoteRoomStateMaxBufferedSpeed = 30f;
    [SerializeField] private float _remoteRoomStateMaxVisualSpeed = 35f;
    [SerializeField] private float _remoteRoomStateVerticalLerpSpeed = 10f;
    [SerializeField] private float _remoteRoomStateVerticalAscentLerpSpeed = 18f;
    [SerializeField] private float _remoteRoomStateClimbHeightBias = 0.04f;
    [SerializeField] private bool _logRemoteRoomStateDiagnostics = true;

    [Header("Follow")]
    [SerializeField] private float _moveSpeed = 10f;
    [SerializeField] private float _acceleration = 18f;
    [SerializeField] private float _rotationSpeed = 10f;
    [SerializeField] private float _stoppingDistance = 1.4f;
    [SerializeField] private float _targetRefreshInterval = 0.2f;
    [SerializeField] private bool _followNearestPlayer = true;
    [SerializeField] private MapNextbotSettings[] _mapSpecificNextbotSettings =
    {
        new MapNextbotSettings
        {
            mapId = "parkour",
            useLocalRoomStateNavMesh = true,
            targetHoldSeconds = 1f,
            maxChaseRange = 1000f,
            navMeshRejoinWarpDistance = 0.3f,
        },
    };

    [Header("Target Score")]
    [SerializeField] private float _maxChaseRange = 70f;
    [SerializeField] private float _visibleRange = 18f;
    [SerializeField] private float _distanceScoreBase = 120f;
    [SerializeField] private float _visibleBonus = 15f;
    [SerializeField] private float _frontBonus = 10f;
    [SerializeField] private float _currentTargetBonus = 30f;
    [SerializeField] private float _switchScoreThreshold = 20f;
    [SerializeField] private float _targetLockDuration = 1.4f;
    [SerializeField] private float _switchConfirmDuration = 0.3f;
    [SerializeField] private float _frontAngleThreshold = 85f;
    [SerializeField] private float _sameTargetScorePenalty = 65f;

    [Header("Detection")]
    [SerializeField] private LayerMask _wallDetectionLayers = 1 << 6;
    [SerializeField] private float _eyeHeight = 1.25f;
    [SerializeField] private float _targetEyeHeight = 1.0f;
    [SerializeField, Min(0f)] private float _lastSeenTargetMemorySeconds = 2.5f;
    [SerializeField, Min(0f)] private float _lastSeenTargetLeadDistance = 3f;
    [SerializeField, Min(0f)] private float _lastSeenTargetReachedDistance = 1.1f;

    [Header("Combat")]
    [SerializeField, Min(1f)] private float _maxCombatHealth = 100f;
    [SerializeField] private bool _autoCreateCombatHitbox = true;
    [SerializeField, Min(0.1f)] private float _combatHitboxFallbackHeight = 5f;
    [SerializeField, Min(0.05f)] private float _combatHitboxFallbackRadius = 1.15f;

    [Header("NavMesh")]
    [SerializeField] private float _navMeshSnapDistance = 8f;
    [SerializeField] private string _walkableAreaName = "Walkable";
    [SerializeField] private bool _useOffMeshLinks = true;
    [SerializeField] private float _offMeshLinkDuration = 0.35f;
    [SerializeField] private float _offMeshLinkArcHeight = 1.2f;

    [Header("Jump")]
    [SerializeField] private bool _allowLedgeJump = true;
    [SerializeField] private float _jumpForwardSpeed = 8f;
    [SerializeField] private float _jumpUpwardSpeed = 5f;
    [SerializeField] private float _maxJumpDistance = 6f;
    [SerializeField] private float _maxJumpUpHeight = 2.5f;
    [SerializeField] private float _maxJumpDownHeight = 8f;
    [SerializeField] private float _landingSnapDistance = 1.5f;
    [SerializeField] private float _edgeJumpMinDrop = 0.75f;
    [SerializeField] private float _edgeStuckVelocity = 0.2f;

    [Header("Grounding")]
    [SerializeField] private bool _lockToStartingHeight = true;
    [SerializeField] private float _gravity = 20f;
    [SerializeField] private LayerMask _groundCheckLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private float _groundProbeStartHeight = 3f;
    [SerializeField] private float _groundProbeDistance = 8f;
    [SerializeField] private float _groundSnapOffset = 0.02f;
    [SerializeField] private float _groundRiseSmoothTime = 0.08f;
    [SerializeField] private float _groundDropSmoothTime = 0.03f;
    [SerializeField] private float _groundHardSnapDistance = 0.35f;
    [SerializeField] private float _groundDropSnapDistance = 0.08f;
    [SerializeField] private float _roomStateGroundProbeHeight = 5f;
    [SerializeField] private float _roomStateGroundMaxRise = 1.5f;
    [SerializeField] private float _roomStateGroundMaxDrop = 10f;

    [Header("Collision")]
    [SerializeField] private LayerMask _roomStateCollisionLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private float _roomStateCollisionSkinWidth = 0.05f;
    [Tooltip("Layers treated as solid walls the nextbot cannot pass through (e.g. Ramp). Each frame the nextbot is depenetrated from any overlapping colliders on these layers.")]
    [SerializeField] private LayerMask _solidObstacleLayers = 0;

    [Header("Hit")]
    [SerializeField] private float _hitDistance = 1.6f;
    [SerializeField] private float _hitCooldown = 1.25f;

    [Header("Billboard")]
    [SerializeField] private bool _faceTargetPlayer = false;
    [SerializeField] private bool _billboardToCamera = true;
    [SerializeField] private Vector3 _visualLocalOffset = Vector3.zero;
    [SerializeField] private float _visualGroundPadding = 0.02f;
    [SerializeField] private Vector3 _billboardRotationOffsetEuler = new Vector3(0f, 90f, -90f);

    [Header("Audio")]
    [SerializeField] private AudioSource _loopAudioSource;
    [SerializeField] private AudioClip _loopClip;
    [SerializeField] private bool _playLoopWhileActive = true;
    [SerializeField, Range(0f, 1f)] private float _loopVolume = 1f;
    [SerializeField] private float _soundMinDistance = 3f;
    [SerializeField] private float _soundMaxDistance = 24f;
    [SerializeField] private AudioRolloffMode _soundRolloffMode = AudioRolloffMode.Linear;

    private struct ScoredTarget
    {
        public Transform Transform;
        public PlayerController Controller;
        public float Score;
        public float PathDistance;
        public bool HasLineOfSight;
    }

    private struct RoomStateSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public float ArrivalTime;
        public float ServerTime;
        public bool IsValid;
    }

    private CharacterController _characterController;
    private NavMeshAgent _navMeshAgent;
    private Transform _target;
    private PlayerController _targetController;
    private Vector3 _horizontalVelocity;
    private float _verticalVelocity;
    private float _lockedHeight;
    private float _nextTargetRefreshTime;
    private float _nextHitTime;
    private Camera _targetCamera;
    private Collider[] _nextbotColliders = System.Array.Empty<Collider>();
    private readonly RaycastHit[] _groundHitBuffer = new RaycastHit[MaxGroundHitBufferSize];
    private readonly Collider[] _roomStateOverlapBuffer = new Collider[16];
    private readonly HashSet<CharacterController> _ignoredInjuredTargets = new HashSet<CharacterController>();
    private readonly List<CharacterController> _ignoredTargetsToRestore = new List<CharacterController>();
    private Renderer[] _renderers = System.Array.Empty<Renderer>();
    private Collider[] _colliders = System.Array.Empty<Collider>();
    private Transform _visualTransform;
    private MeshRenderer _rootMeshRenderer;
    private MeshRenderer _visualMeshRenderer;
    private CapsuleCollider _combatHitboxCollider;
    private NavMeshPath _pathBuffer;
    private float _targetLockedUntil;
    private Transform _pendingSwitchTarget;
    private float _pendingSwitchStartedAt;
    private float _agentVisualOffset;
    private bool _isJumping;
    private Vector3 _jumpVelocity;
    private bool _isTraversingOffMeshLink;
    private Vector3 _offMeshLinkStart;
    private Vector3 _offMeshLinkEnd;
    private float _offMeshLinkProgress;
    private bool _hasAppliedRoomState;
    private bool _roomStateAuthorityActive;
    private bool _forceOfflineLocalAuthority;
    private bool _offlineNextbotActive = true;
    private bool _isLocalNavMeshAuthority;
    private float _nextLocalNavMeshUploadTime;
    private bool _hasRoomStateParkourPatrolTarget;
    private Vector3 _roomStateParkourPatrolTarget;
    private float _roomStateParkourPatrolWaitUntil;
    private float _mapTargetHoldUntil;
    private bool _hasLastSeenTargetPosition;
    private Vector3 _lastSeenTargetPosition;
    private Vector3 _lastSeenTargetDirection = Vector3.forward;
    private float _lastSeenTargetExpiresAt = float.NegativeInfinity;
    private bool _hasOfflinePatrolTarget;
    private Vector3 _offlinePatrolTarget;
    private float _offlinePatrolWaitUntil;
    private float _groundHeightVelocity;
    private Texture _defaultBaseMap;
    private Color _defaultBaseColor = Color.white;
    private AudioClip _defaultLoopClip;
    private float _defaultLoopPitch = 1f;
    private float _defaultMoveSpeed = 10f;
    private int _walkableAreaMask = NavMesh.AllAreas;
    private Material _visualRuntimeMaterial;
    private readonly List<RoomStateSnapshot> _roomStateSnapshotBuffer = new List<RoomStateSnapshot>(10);
    private float _lastRoomStateArrivalTime = -1f;
    private float _lastRoomStateServerTime = -1f;
    private float _roomStateArrivalGapSum;
    private float _roomStateServerGapSum;
    private float _roomStateArrivalGapMin = float.PositiveInfinity;
    private float _roomStateArrivalGapMax;
    private float _roomStateServerGapMin = float.PositiveInfinity;
    private float _roomStateServerGapMax;
    private int _roomStateDiagnosticSampleCount;
    private float _nextRoomStateDiagnosticLogTime;
    private Vector3 _remotePresentationVelocity;
    private float _offlineCombatHealth;
    private string _displayName = string.Empty;

    public string NetworkNextbotId => _networkNextbotId;
    public string CombatDisplayName => string.IsNullOrWhiteSpace(_displayName) ? _networkNextbotId : _displayName;
    public float CurrentCombatHealth => GetCurrentCombatHealth();
    public float MaxCombatHealth => GetMaxCombatHealth();
    public bool IsCombatActive => GetIsCombatActive();

    private void Awake()
    {
        _offlineCombatHealth = _maxCombatHealth;
        _defaultLoopClip = _loopClip;
        _defaultMoveSpeed = _moveSpeed;
        _characterController = GetComponent<CharacterController>();
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _pathBuffer = new NavMeshPath();
        _lockedHeight = transform.position.y;
        EnsureVisualBillboardChild();
        EnsureCombatHitbox();
        EnsureLoopAudioSource();
        _nextbotColliders = GetComponentsInChildren<Collider>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);

        if (_navMeshAgent != null)
        {
            ConfigureAgentFromCurrentPosition();
            _navMeshAgent.updatePosition = false;
            _navMeshAgent.updateRotation = false;
            _navMeshAgent.updateUpAxis = true;
            _navMeshAgent.speed = _moveSpeed;
            _navMeshAgent.acceleration = _acceleration;
            _navMeshAgent.stoppingDistance = _stoppingDistance;
            _navMeshAgent.angularSpeed = Mathf.Max(120f, _rotationSpeed * 45f);

            if (_characterController != null)
            {
                // Give the agent a slightly smaller radius than the physical body (80%)
                // to prevent it from getting stuck on the very edge of walls/gaps.
                _navMeshAgent.radius = _characterController.radius * 0.8f;
                _navMeshAgent.height = _characterController.height;
            }
        }

        if (TryResolveGroundedPosition(transform.position, out Vector3 groundedStartPosition))
        {
            transform.position = groundedStartPosition;
            _lockedHeight = groundedStartPosition.y;
            _groundHeightVelocity = 0f;
        }
    }

    private void Update()
    {
        SyncIgnoredTargets();

        if (UpdateActivationState())
        {
            return;
        }

        if (_isJumping)
        {
            UpdateJumpMovement();
            return;
        }

        if (_isTraversingOffMeshLink)
        {
            UpdateOffMeshLinkTraversal();
            return;
        }

        RefreshTargetIfNeeded();
        UpdateMovement();
    }

    private void LateUpdate()
    {
        if (ShouldUseTargetFacingVisuals() && UpdateTargetFacingRotation())
        {
            return;
        }

        if (_billboardToCamera)
        {
            UpdateBillboardRotation();
        }
    }

    private bool ShouldUseTargetFacingVisuals()
    {
        return _target != null && (_faceTargetPlayer || _roomStateAuthorityActive);
    }

    private bool UpdateActivationState()
    {
        MyRoomState roomState = GetRoomState();
        NextbotState assignedNextbotState = null;
        bool useRoomStateAuthority = !_forceOfflineLocalAuthority
            && _useRoomStateAuthority
            && roomState != null
            && TryGetAssignedNextbotState(roomState, out assignedNextbotState);
        SetRoomStateAuthorityActive(useRoomStateAuthority);

        if (useRoomStateAuthority)
        {
            return UpdateFromRoomState(assignedNextbotState);
        }

        if (_forceOfflineLocalAuthority)
        {
            SetServerVisualState(_offlineNextbotActive);
            if (!_offlineNextbotActive)
            {
                ClearTarget();
                StopAgent();
                _horizontalVelocity = Vector3.zero;
                _verticalVelocity = 0f;
                _isJumping = false;
                _jumpVelocity = Vector3.zero;
                _isTraversingOffMeshLink = false;
                return true;
            }

            EnsureAgentOnNavMesh();
            return false;
        }

        if (_useRoomStateAuthority && roomState != null)
        {
            SetServerVisualState(false);
            ClearTarget();
            StopAgent();
            _horizontalVelocity = Vector3.zero;
            _isJumping = false;
            _jumpVelocity = Vector3.zero;
            _isTraversingOffMeshLink = false;
            _hasAppliedRoomState = false;
            return true;
        }

        bool isActive = roomState == null || roomState.isGameStarted;
        SetServerVisualState(isActive);

        if (!isActive)
        {
            ClearTarget();
            StopAgent();
            _horizontalVelocity = Vector3.zero;
            _isJumping = false;
            _jumpVelocity = Vector3.zero;
            _isTraversingOffMeshLink = false;
            return true;
        }

        EnsureAgentOnNavMesh();

        return false;
    }

    public void AssignNetworkNextbotId(string nextbotId)
    {
        if (string.Equals(_networkNextbotId, nextbotId, System.StringComparison.Ordinal))
        {
            return;
        }

        _networkNextbotId = nextbotId;
        _hasAppliedRoomState = false;
        ResetOfflinePatrolTarget();
    }

    public void SetOfflineLocalAuthority(bool isOfflineLocalAuthority)
    {
        _forceOfflineLocalAuthority = isOfflineLocalAuthority;

        if (!isOfflineLocalAuthority)
        {
            return;
        }

        SetRoomStateAuthorityActive(false);
        SetServerVisualState(true);
        ApplyOfflineMultiplayerTuning();
        _hasAppliedRoomState = false;
        ResetOfflinePatrolTarget();
        _isJumping = false;
        _jumpVelocity = Vector3.zero;
        _isTraversingOffMeshLink = false;
        EnsureAgentOnNavMesh();
    }

    public void SetOfflineNextbotActive(bool isActive)
    {
        _offlineNextbotActive = isActive;
        if (isActive)
        {
            _offlineCombatHealth = _maxCombatHealth;
        }

        if (!_forceOfflineLocalAuthority)
        {
            return;
        }

        SetServerVisualState(isActive);
        if (isActive)
        {
            EnsureAgentOnNavMesh();
            return;
        }

        ClearTarget();
        ResetOfflinePatrolTarget();
        StopAgent();
        _horizontalVelocity = Vector3.zero;
        _verticalVelocity = 0f;
        _isJumping = false;
        _jumpVelocity = Vector3.zero;
        _isTraversingOffMeshLink = false;
    }

    public void PrepareOfflineNextbot(Vector3 spawnPosition)
    {
        SetOfflineLocalAuthority(true);
        _offlineNextbotActive = true;
        _offlineCombatHealth = _maxCombatHealth;

        if (_characterController != null)
        {
            bool wasEnabled = _characterController.enabled;
            _characterController.enabled = false;
            transform.position = spawnPosition;
            _characterController.enabled = wasEnabled;
        }
        else
        {
            transform.position = spawnPosition;
        }

        _lockedHeight = spawnPosition.y;
        _horizontalVelocity = Vector3.zero;
        _verticalVelocity = 0f;
        ClearTarget();
        ResetOfflinePatrolTarget();
        StopAgent();
        EnsureAgentOnNavMesh();
    }

    public void ApplyRegistryEntry(NextbotRegistryEntry entry)
    {
        EnsureVisualBillboardChild();
        _displayName = entry != null && !string.IsNullOrWhiteSpace(entry.displayName)
            ? entry.displayName
            : _networkNextbotId;

        _moveSpeed = entry != null && entry.speed > 0f ? entry.speed : _defaultMoveSpeed;
        _loopClip = entry != null && entry.loopClip != null ? entry.loopClip : _defaultLoopClip;
        float loopPitch = entry != null ? Mathf.Max(0.5f, entry.loopPitch) : _defaultLoopPitch;
        if (_loopAudioSource != null)
        {
            _loopAudioSource.clip = _loopClip;
            _loopAudioSource.pitch = loopPitch;
        }

        if (_navMeshAgent != null)
        {
            _navMeshAgent.speed = _moveSpeed;
        }

        MeshRenderer targetRenderer = _visualMeshRenderer != null ? _visualMeshRenderer : _rootMeshRenderer;
        if (targetRenderer == null)
        {
            return;
        }

        Material[] materials = targetRenderer.materials;
        Texture iconTexture = entry != null && entry.iconTexture != null ? entry.iconTexture : _defaultBaseMap;
        Color tint = entry != null ? entry.tint : _defaultBaseColor;
        if (tint.a <= 0.01f)
        {
            tint.a = 1f;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", iconTexture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", iconTexture);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", tint);
            }
        }
    }

    public bool TryApplyOfflineCombatDamage(float damage)
    {
        if (!_forceOfflineLocalAuthority || !_offlineNextbotActive || damage <= 0f)
        {
            return false;
        }

        _offlineCombatHealth = Mathf.Max(0f, _offlineCombatHealth - damage);
        if (_offlineCombatHealth <= 0f)
        {
            SetOfflineNextbotActive(false);
        }

        return true;
    }

    private void SetRoomStateAuthorityActive(bool isActive)
    {
        if (_roomStateAuthorityActive == isActive)
        {
            if (isActive
                && !ShouldUseMapLocalNavMeshPresentation()
                && _navMeshAgent != null
                && _navMeshAgent.enabled
                && _navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.nextPosition = transform.position;
            }

            return;
        }

        _roomStateAuthorityActive = isActive;

        if (_navMeshAgent == null || !_navMeshAgent.enabled)
        {
            return;
        }

        bool useParkourLocalPresentation = isActive && CurrentMapUsesLocalRoomStateNavMesh();
        _navMeshAgent.updatePosition = !isActive;
        if (_navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = isActive && !useParkourLocalPresentation;
            if (!useParkourLocalPresentation)
            {
                _navMeshAgent.ResetPath();
            }
            _navMeshAgent.nextPosition = transform.position;
        }
    }

    private bool UpdateFromRoomState(NextbotState nextbotState)
    {
        bool isActive = nextbotState != null && nextbotState.isActive;
        SetServerVisualState(isActive);

        if (!isActive || nextbotState == null)
        {
            _isLocalNavMeshAuthority = false;
            if (nextbotState != null)
            {
                Vector3 hiddenPosition = ResolveGroundedRoomStatePosition(nextbotState);
                ApplyRoomStatePosition(hiddenPosition, false);
                transform.rotation = Quaternion.Euler(0f, nextbotState.rotationY, 0f);
            }

            ClearTarget();
            StopAgent();
            _isJumping = false;
            _jumpVelocity = Vector3.zero;
            _isTraversingOffMeshLink = false;
            _hasAppliedRoomState = false;
            return true;
        }

        bool hadLocalNavMeshAuthority = _isLocalNavMeshAuthority;
        _isLocalNavMeshAuthority = false;
        Vector3 rawTargetPosition = new Vector3(nextbotState.x, nextbotState.y, nextbotState.z);
        Vector3 targetPosition = ResolveGroundedRoomStatePosition(nextbotState);
        Quaternion targetRotation = Quaternion.Euler(0f, nextbotState.rotationY, 0f);
        Vector3 targetVelocity = new Vector3(nextbotState.velocityX, nextbotState.velocityY, nextbotState.velocityZ);

        bool shouldUseParkourLocalPresentation = ShouldUseMapLocalNavMeshPresentation();
        if (TryGetServerAssignedTarget(nextbotState.targetSessionId, out Transform targetTransform, out PlayerController targetController))
        {
            if (shouldUseParkourLocalPresentation
                && _target != targetTransform
                && IsTargetClaimedByOtherNextbot(targetTransform))
            {
                ClearTarget();
            }
            else if (_target != targetTransform || _targetController != targetController)
            {
                AssignTarget(targetTransform, targetController);
            }
            else
            {
                _mapTargetHoldUntil = Time.time + GetCurrentMapTargetHoldSeconds();
            }
        }
        else
        {
            bool canHoldParkourTarget = shouldUseParkourLocalPresentation
                && _target != null
                && _targetController != null
                && _targetController.enabled
                && !_targetController.IsInjuredOrHitReacting()
                && Time.time <= _mapTargetHoldUntil;
            if (!canHoldParkourTarget)
            {
                ClearTarget();
            }
        }

        _isLocalNavMeshAuthority = shouldUseParkourLocalPresentation;
        if (shouldUseParkourLocalPresentation)
        {
            bool shouldResyncToServer = !_hasAppliedRoomState;
            if (shouldResyncToServer)
            {
                ApplyRoomStatePosition(targetPosition, true);
                transform.rotation = targetRotation;
                _remotePresentationVelocity = Vector3.zero;
                if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.nextPosition = transform.position;
                }
            }

            _hasAppliedRoomState = true;
            _isJumping = false;
            _jumpVelocity = Vector3.zero;
            _isTraversingOffMeshLink = false;
            EnsureAgentOnNavMesh();

            return false;
        }

        if (hadLocalNavMeshAuthority)
        {
            _hasAppliedRoomState = false;
            _remotePresentationVelocity = Vector3.zero;
        }

        StopAgent();
        _isJumping = false;
        _jumpVelocity = Vector3.zero;
        _isTraversingOffMeshLink = false;

        if (!_hasAppliedRoomState)
        {
            PushRoomStateSnapshot(rawTargetPosition, targetRotation, targetVelocity, nextbotState.sampleTimeMs, true);
            ApplyRoomStatePosition(targetPosition, true);
            transform.rotation = targetRotation;
            _hasAppliedRoomState = true;
            return true;
        }

        PushRoomStateSnapshot(rawTargetPosition, targetRotation, targetVelocity, nextbotState.sampleTimeMs, false);

        if (IsUsingRemoteRoomStateProfile() && TryEvaluateBufferedRoomState(out Vector3 bufferedPosition, out Quaternion bufferedRotation))
        {
            float bufferedPositionError = Vector3.Distance(transform.position, bufferedPosition);
            float bufferedCatchUpDistance = Mathf.Max(1f, GetEffectiveRoomStateSnapDistance());
            float catchUpT = Mathf.Clamp01(bufferedPositionError / bufferedCatchUpDistance);
            float presentationSmoothTime = Mathf.Max(0.01f, _remoteRoomStatePresentationSmoothTime);
            float bufferedMaxSpeed = Mathf.Max(1f, GetEffectiveRoomStatePositionLerpSpeed() * Mathf.Lerp(1f, _remoteRoomStateCatchUpBoost, catchUpT));
            Vector3 currentHorizontalPosition = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 targetHorizontalPosition = new Vector3(bufferedPosition.x, 0f, bufferedPosition.z);
            Vector3 dampedBufferedHorizontalPosition = Vector3.SmoothDamp(
                currentHorizontalPosition,
                targetHorizontalPosition,
                ref _remotePresentationVelocity,
                presentationSmoothTime,
                bufferedMaxSpeed,
                Time.deltaTime);
            float maxVisualStep = Mathf.Max(0.25f, GetEffectiveRemoteVisualSpeedLimit()) * Time.deltaTime;
            Vector3 currentPlanarPosition = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 smoothedBufferedPlanarPosition = Vector3.MoveTowards(currentPlanarPosition, dampedBufferedHorizontalPosition, maxVisualStep);
            float targetVerticalLerpSpeed = bufferedPosition.y >= transform.position.y
                ? Mathf.Max(_remoteRoomStateVerticalLerpSpeed, _remoteRoomStateVerticalAscentLerpSpeed)
                : Mathf.Max(0.01f, _remoteRoomStateVerticalLerpSpeed);
            float verticalBlend = 1f - Mathf.Exp(-targetVerticalLerpSpeed * Time.deltaTime);
            float smoothedBufferedY = Mathf.Lerp(transform.position.y, bufferedPosition.y, verticalBlend);
            if (bufferedPosition.y > transform.position.y)
            {
                smoothedBufferedY = bufferedPosition.y + Mathf.Max(0f, _remoteRoomStateClimbHeightBias);
            }
            Vector3 smoothedBufferedPosition = new Vector3(
                smoothedBufferedPlanarPosition.x,
                smoothedBufferedY,
                smoothedBufferedPlanarPosition.z);
            smoothedBufferedPosition = ConstrainRoomStateMovement(transform.position, smoothedBufferedPosition);
            float bufferedRotationBlend = 1f - Mathf.Exp(-GetEffectiveRoomStateRotationLerpSpeed() * Time.deltaTime);

            transform.position = smoothedBufferedPosition;
            ApplySolidObstaclePush();
            transform.rotation = Quaternion.Slerp(transform.rotation, bufferedRotation, bufferedRotationBlend);
            if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.nextPosition = transform.position;
            }

            return true;
        }

        float positionError = Vector3.Distance(transform.position, targetPosition);
        float snapDistance = GetEffectiveRoomStateSnapDistance();
        if (positionError >= snapDistance)
        {
            ApplyRoomStatePosition(targetPosition, true);
            transform.rotation = targetRotation;
            _remotePresentationVelocity = Vector3.zero;
            return true;
        }

        float basePositionLerpSpeed = GetEffectiveRoomStatePositionLerpSpeed();
        float effectivePositionLerpSpeed = basePositionLerpSpeed;
        if (IsUsingRemoteRoomStateProfile())
        {
            float catchUpT = Mathf.Clamp01(positionError / Mathf.Max(0.001f, snapDistance));
            effectivePositionLerpSpeed *= Mathf.Lerp(1f, _remoteRoomStateCatchUpBoost, catchUpT);
        }

        float positionBlend = 1f - Mathf.Exp(-effectivePositionLerpSpeed * Time.deltaTime);
        float rotationBlend = 1f - Mathf.Exp(-GetEffectiveRoomStateRotationLerpSpeed() * Time.deltaTime);
        Vector3 blendedPosition = Vector3.Lerp(transform.position, targetPosition, positionBlend);
        blendedPosition = ConstrainRoomStateMovement(transform.position, blendedPosition);

        transform.position = blendedPosition;
        ApplySolidObstaclePush();
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationBlend);
        if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.nextPosition = transform.position;
        }

        return true;
    }

    private MapNextbotSettings GetCurrentMapNextbotSettings()
    {
        string currentMapId = NetworkManager.Instance != null
            ? NetworkManager.Instance.CurrentMapId
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(currentMapId) && _mapSpecificNextbotSettings != null)
        {
            for (int i = 0; i < _mapSpecificNextbotSettings.Length; i++)
            {
                MapNextbotSettings candidate = _mapSpecificNextbotSettings[i];
                if (candidate != null
                    && !string.IsNullOrWhiteSpace(candidate.mapId)
                    && string.Equals(candidate.mapId, currentMapId, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        return DefaultMapNextbotSettings;
    }

    private bool CurrentMapUsesLocalRoomStateNavMesh()
    {
        return GetCurrentMapNextbotSettings().useLocalRoomStateNavMesh;
    }

    private bool ShouldUseMapLocalNavMeshChase(NextbotState nextbotState)
    {
        if (nextbotState == null || _target == null)
        {
            return false;
        }

        return CurrentMapUsesLocalRoomStateNavMesh();
    }

    private bool ShouldUseMapLocalNavMeshChase()
    {
        return _target != null
            && CurrentMapUsesLocalRoomStateNavMesh();
    }

    private bool ShouldUseMapLocalNavMeshPresentation()
    {
        return _roomStateAuthorityActive
            && CurrentMapUsesLocalRoomStateNavMesh();
    }

    private float GetCurrentMapTargetHoldSeconds()
    {
        return Mathf.Max(0f, GetCurrentMapNextbotSettings().targetHoldSeconds);
    }

    private float GetCurrentMapMaxChaseRange()
    {
        return Mathf.Max(_maxChaseRange, GetCurrentMapNextbotSettings().maxChaseRange);
    }

    private float GetCurrentMapNavMeshRejoinWarpDistance()
    {
        return Mathf.Max(0.05f, GetCurrentMapNextbotSettings().navMeshRejoinWarpDistance);
    }

    private Vector3 ResolveGroundedRoomStatePosition(NextbotState nextbotState)
    {
        float predictionTime = Mathf.Max(0f, GetEffectiveRoomStatePredictionTime());
        Vector3 predictedPosition = new Vector3(
            nextbotState.x + nextbotState.velocityX * predictionTime,
            nextbotState.y,
            nextbotState.z + nextbotState.velocityZ * predictionTime);

        // ALWAYS trust the local NavMesh or physics ground height over the server's Y.
        // The server often doesn't know about ramps, so it incorrectly pathfinds at Y=0.
        // If we limit this by height, the nextbot will suddenly drop through the ramp halfway up.
        if (TryResolveGroundedPosition(predictedPosition, out Vector3 groundedPosition))
        {
            predictedPosition.y = groundedPosition.y;
        }

        return predictedPosition;
    }

    private void PushRoomStateSnapshot(Vector3 position, Quaternion rotation, Vector3 velocity, float serverTimeMs, bool forceReset)
    {
        float arrivalTime = Time.unscaledTime;
        RecordRoomStateDiagnostic(arrivalTime, serverTimeMs, forceReset);
        Vector3 bufferedPosition = position;
        Vector3 bufferedVelocity = velocity;
        if (forceReset)
        {
            _roomStateSnapshotBuffer.Clear();
            _remotePresentationVelocity = Vector3.zero;
        }

        if (_roomStateSnapshotBuffer.Count > 0)
        {
            RoomStateSnapshot latestSnapshot = _roomStateSnapshotBuffer[_roomStateSnapshotBuffer.Count - 1];
            bool isOutOfOrder = serverTimeMs < latestSnapshot.ServerTime;
            if (isOutOfOrder)
            {
                return;
            }

            if (IsUsingRemoteRoomStateProfile())
            {
                float serverDeltaSeconds = Mathf.Max(0.001f, (serverTimeMs - latestSnapshot.ServerTime) / 1000f);
                float maxBufferedSpeed = GetEffectiveRemoteBufferedSpeedLimit();
                float maxBufferedDistance = Mathf.Max(0.25f, maxBufferedSpeed * serverDeltaSeconds * 1.5f);
                Vector3 snapshotDelta = bufferedPosition - latestSnapshot.Position;
                float snapshotDistance = snapshotDelta.magnitude;
                if (snapshotDistance > maxBufferedDistance)
                {
                    bufferedPosition = latestSnapshot.Position + (snapshotDelta / snapshotDistance) * maxBufferedDistance;
                    bufferedVelocity = (bufferedPosition - latestSnapshot.Position) / serverDeltaSeconds;
                }
            }

            bool unchanged = Vector3.Distance(latestSnapshot.Position, bufferedPosition) <= 0.0001f
                && Quaternion.Angle(latestSnapshot.Rotation, rotation) <= 0.01f
                && Vector3.Distance(latestSnapshot.Velocity, bufferedVelocity) <= 0.0001f
                && Mathf.Abs(latestSnapshot.ServerTime - serverTimeMs) <= 0.01f;
            if (unchanged)
            {
                return;
            }
        }

        _roomStateSnapshotBuffer.Add(new RoomStateSnapshot
        {
            Position = bufferedPosition,
            Rotation = rotation,
            Velocity = bufferedVelocity,
            ArrivalTime = arrivalTime,
            ServerTime = serverTimeMs,
            IsValid = true,
        });

        const int maxBufferedSnapshots = 10;
        int excessSnapshotCount = _roomStateSnapshotBuffer.Count - maxBufferedSnapshots;
        if (excessSnapshotCount > 0)
        {
            _roomStateSnapshotBuffer.RemoveRange(0, excessSnapshotCount);
        }
    }

    private bool TryEvaluateBufferedRoomState(out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;

        int snapshotCount = _roomStateSnapshotBuffer.Count;
        if (snapshotCount <= 0)
        {
            return false;
        }

        RoomStateSnapshot latestSnapshot = _roomStateSnapshotBuffer[snapshotCount - 1];
        float interpolationBackTimeMs = Mathf.Max(0f, _remoteRoomStateInterpolationBackTime) * 1000f;
        float timeSinceLatestArrivalMs = Mathf.Max(0f, (Time.unscaledTime - latestSnapshot.ArrivalTime) * 1000f);
        float renderServerTime = latestSnapshot.ServerTime + timeSinceLatestArrivalMs - interpolationBackTimeMs;

        if (snapshotCount == 1)
        {
            float singleExtrapolationTimeMs = renderServerTime - latestSnapshot.ServerTime;
            float singleExtrapolationTime = Mathf.Clamp(Mathf.Max(0f, singleExtrapolationTimeMs) / 1000f, 0f, Mathf.Max(0f, _remoteRoomStateMaxExtrapolationTime));
            position = latestSnapshot.Position + latestSnapshot.Velocity * singleExtrapolationTime;
            rotation = latestSnapshot.Rotation;
            return true;
        }

        RoomStateSnapshot earlierSnapshot = _roomStateSnapshotBuffer[0];
        RoomStateSnapshot laterSnapshot = latestSnapshot;
        bool foundBracket = false;
        for (int i = 1; i < snapshotCount; i++)
        {
            RoomStateSnapshot previousSnapshot = _roomStateSnapshotBuffer[i - 1];
            RoomStateSnapshot currentSnapshot = _roomStateSnapshotBuffer[i];
            if (renderServerTime >= previousSnapshot.ServerTime && renderServerTime <= currentSnapshot.ServerTime)
            {
                earlierSnapshot = previousSnapshot;
                laterSnapshot = currentSnapshot;
                foundBracket = true;
                break;
            }
        }

        if (foundBracket)
        {
            float interpolationFactor = Mathf.InverseLerp(earlierSnapshot.ServerTime, laterSnapshot.ServerTime, renderServerTime);
            position = Vector3.Lerp(earlierSnapshot.Position, laterSnapshot.Position, interpolationFactor);
            rotation = Quaternion.Slerp(earlierSnapshot.Rotation, laterSnapshot.Rotation, interpolationFactor);
            return true;
        }

        if (renderServerTime < _roomStateSnapshotBuffer[0].ServerTime)
        {
            position = _roomStateSnapshotBuffer[0].Position;
            rotation = _roomStateSnapshotBuffer[0].Rotation;
            return true;
        }

        float extrapolationTimeMs = renderServerTime - latestSnapshot.ServerTime;
        float extrapolationTime = Mathf.Clamp(Mathf.Max(0f, extrapolationTimeMs) / 1000f, 0f, Mathf.Max(0f, _remoteRoomStateMaxExtrapolationTime));
        position = latestSnapshot.Position + latestSnapshot.Velocity * extrapolationTime;
        rotation = latestSnapshot.Rotation;
        return true;
    }

    private void RecordRoomStateDiagnostic(float arrivalTime, float serverTimeMs, bool forceReset)
    {
        if (!_logRemoteRoomStateDiagnostics || !IsUsingRemoteRoomStateProfile())
        {
            return;
        }

        if (forceReset)
        {
            _lastRoomStateArrivalTime = arrivalTime;
            _lastRoomStateServerTime = serverTimeMs;
            _roomStateArrivalGapSum = 0f;
            _roomStateServerGapSum = 0f;
            _roomStateArrivalGapMin = float.PositiveInfinity;
            _roomStateArrivalGapMax = 0f;
            _roomStateServerGapMin = float.PositiveInfinity;
            _roomStateServerGapMax = 0f;
            _roomStateDiagnosticSampleCount = 0;
            _nextRoomStateDiagnosticLogTime = Time.unscaledTime + 5f;
            return;
        }

        if (_lastRoomStateArrivalTime >= 0f)
        {
            float arrivalGapMs = (arrivalTime - _lastRoomStateArrivalTime) * 1000f;
            float serverGapMs = serverTimeMs - _lastRoomStateServerTime;
            _roomStateArrivalGapSum += arrivalGapMs;
            _roomStateServerGapSum += serverGapMs;
            _roomStateArrivalGapMin = Mathf.Min(_roomStateArrivalGapMin, arrivalGapMs);
            _roomStateArrivalGapMax = Mathf.Max(_roomStateArrivalGapMax, arrivalGapMs);
            _roomStateServerGapMin = Mathf.Min(_roomStateServerGapMin, serverGapMs);
            _roomStateServerGapMax = Mathf.Max(_roomStateServerGapMax, serverGapMs);
            _roomStateDiagnosticSampleCount += 1;
        }

        _lastRoomStateArrivalTime = arrivalTime;
        _lastRoomStateServerTime = serverTimeMs;

        if (Time.unscaledTime < _nextRoomStateDiagnosticLogTime || _roomStateDiagnosticSampleCount <= 0 || !ShouldEmitRoomStateDiagnostic())
        {
            return;
        }

        float averageArrivalGapMs = _roomStateArrivalGapSum / _roomStateDiagnosticSampleCount;
        float averageServerGapMs = _roomStateServerGapSum / _roomStateDiagnosticSampleCount;
        Debug.Log(
            $"[NextbotDiag][Client] id={_networkNextbotId} samples={_roomStateDiagnosticSampleCount} arrivalAvg={averageArrivalGapMs:F1}ms arrivalMin={_roomStateArrivalGapMin:F1}ms arrivalMax={_roomStateArrivalGapMax:F1}ms serverAvg={averageServerGapMs:F1}ms serverMin={_roomStateServerGapMin:F1}ms serverMax={_roomStateServerGapMax:F1}ms");

        _roomStateArrivalGapSum = 0f;
        _roomStateServerGapSum = 0f;
        _roomStateArrivalGapMin = float.PositiveInfinity;
        _roomStateArrivalGapMax = 0f;
        _roomStateServerGapMin = float.PositiveInfinity;
        _roomStateServerGapMax = 0f;
        _roomStateDiagnosticSampleCount = 0;
        _nextRoomStateDiagnosticLogTime = Time.unscaledTime + 5f;
    }

    private bool ShouldEmitRoomStateDiagnostic()
    {
        return string.IsNullOrWhiteSpace(_networkNextbotId)
            || string.Equals(_networkNextbotId, "nextbot_0", System.StringComparison.Ordinal);
    }

    private float GetEffectiveRoomStatePositionLerpSpeed()
    {
        return IsUsingRemoteRoomStateProfile() ? _remoteRoomStatePositionLerpSpeed : _roomStatePositionLerpSpeed;
    }

    private float GetEffectiveRoomStateRotationLerpSpeed()
    {
        return IsUsingRemoteRoomStateProfile() ? _remoteRoomStateRotationLerpSpeed : _roomStateRotationLerpSpeed;
    }

    private float GetEffectiveRoomStateSnapDistance()
    {
        return IsUsingRemoteRoomStateProfile() ? _remoteRoomStateSnapDistance : _roomStateSnapDistance;
    }

    private float GetEffectiveRoomStatePredictionTime()
    {
        return IsUsingRemoteRoomStateProfile() ? _remoteRoomStatePredictionTime : _roomStatePredictionTime;
    }

    private float GetEffectiveRemoteBufferedSpeedLimit()
    {
        return Mathf.Clamp(_remoteRoomStateMaxBufferedSpeed, 6f, 30f);
    }

    private float GetEffectiveRemoteVisualSpeedLimit()
    {
        return Mathf.Clamp(_remoteRoomStateMaxVisualSpeed, 8f, 35f);
    }

    private bool IsUsingRemoteRoomStateProfile()
    {
        return !_isLocalNavMeshAuthority;
    }

    private void ApplyRoomStatePosition(Vector3 targetPosition, bool constrainMovement = true)
    {
        if (constrainMovement)
        {
            targetPosition = ConstrainRoomStateMovement(transform.position, targetPosition);
        }

        _groundHeightVelocity = 0f;

        transform.position = targetPosition;
        ApplySolidObstaclePush();

        if (_navMeshAgent == null || !_navMeshAgent.enabled)
        {
            return;
        }

        EnsureAgentOnNavMesh();
        if (_navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.Warp(targetPosition);
            _navMeshAgent.nextPosition = targetPosition;
        }
    }

    private MyRoomState GetRoomState()
    {
        NetworkManager networkManager = NetworkManager.Instance;
        if (networkManager == null || networkManager.Room == null || networkManager.Room.State == null)
        {
            return null;
        }

        return networkManager.Room.State;
    }

    private bool TryGetAssignedNextbotState(MyRoomState roomState, out NextbotState nextbotState)
    {
        nextbotState = null;
        if (roomState == null || roomState.nextbots == null || string.IsNullOrEmpty(_networkNextbotId))
        {
            return false;
        }

        return roomState.nextbots.TryGetValue(_networkNextbotId, out nextbotState) && nextbotState != null;
    }

    private bool GetIsCombatActive()
    {
        if (_forceOfflineLocalAuthority)
        {
            return _offlineNextbotActive;
        }

        MyRoomState roomState = GetRoomState();
        return roomState != null
            && TryGetAssignedNextbotState(roomState, out NextbotState nextbotState)
            && nextbotState != null
            && nextbotState.isActive;
    }

    private float GetCurrentCombatHealth()
    {
        if (_forceOfflineLocalAuthority)
        {
            return _offlineCombatHealth;
        }

        MyRoomState roomState = GetRoomState();
        if (roomState != null
            && TryGetAssignedNextbotState(roomState, out NextbotState nextbotState)
            && nextbotState != null)
        {
            return nextbotState.currentHealth;
        }

        return _maxCombatHealth;
    }

    private float GetMaxCombatHealth()
    {
        if (_forceOfflineLocalAuthority)
        {
            return _maxCombatHealth;
        }

        MyRoomState roomState = GetRoomState();
        if (roomState != null
            && TryGetAssignedNextbotState(roomState, out NextbotState nextbotState)
            && nextbotState != null
            && nextbotState.maxHealth > 0f)
        {
            return nextbotState.maxHealth;
        }

        return _maxCombatHealth;
    }

    private void RefreshTargetIfNeeded()
    {
        if (_roomStateAuthorityActive && !ShouldUseMapLocalNavMeshPresentation())
        {
            return;
        }

        if (!_followNearestPlayer)
        {
            return;
        }

        if (_target != null && !CanKeepCurrentTarget())
        {
            ClearTarget();
        }

        if (Time.time < _nextTargetRefreshTime && _target != null)
        {
            return;
        }

        _nextTargetRefreshTime = Time.time + Mathf.Max(0.05f, _targetRefreshInterval);
        EvaluateTargetSelection();
    }

    private void EvaluateTargetSelection()
    {
        ScoredTarget? bestTarget = FindBestTarget();
        if (!bestTarget.HasValue && ShouldUseMapLocalNavMeshPresentation())
        {
            bestTarget = FindBestParkourFallbackTarget();
        }

        if (!bestTarget.HasValue)
        {
            ClearTarget();
            return;
        }

        if (_target == null)
        {
            AssignTarget(bestTarget.Value.Transform, bestTarget.Value.Controller);
            return;
        }

        if (Time.time < _targetLockedUntil && bestTarget.Value.Transform == _target)
        {
            return;
        }

        ScoredTarget? currentScore = BuildScoredTarget(_target, _targetController, true);
        if (!currentScore.HasValue)
        {
            AssignTarget(bestTarget.Value.Transform, bestTarget.Value.Controller);
            return;
        }

        if (bestTarget.Value.Transform == _target)
        {
            _pendingSwitchTarget = null;
            _pendingSwitchStartedAt = 0f;
            return;
        }

        if (bestTarget.Value.Score <= currentScore.Value.Score + _switchScoreThreshold)
        {
            _pendingSwitchTarget = null;
            _pendingSwitchStartedAt = 0f;
            return;
        }

        if (_forceOfflineLocalAuthority && IsOfflineLocalPlayerTarget(bestTarget.Value.Transform))
        {
            AssignTarget(bestTarget.Value.Transform, bestTarget.Value.Controller);
            return;
        }

        if (_pendingSwitchTarget != bestTarget.Value.Transform)
        {
            _pendingSwitchTarget = bestTarget.Value.Transform;
            _pendingSwitchStartedAt = Time.time;
            return;
        }

        if (Time.time - _pendingSwitchStartedAt >= _switchConfirmDuration)
        {
            AssignTarget(bestTarget.Value.Transform, bestTarget.Value.Controller);
        }
    }

    private ScoredTarget? FindBestParkourFallbackTarget()
    {
        Transform bestTransform = null;
        PlayerController bestController = null;
        float bestDistance = float.PositiveInfinity;

        PlayerController[] playerControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        for (int i = 0; i < playerControllers.Length; i++)
        {
            PlayerController playerController = playerControllers[i];
            if (playerController == null
                || !playerController.enabled
                || playerController.transform == transform
                || playerController.IsInjuredOrHitReacting()
                || IsTargetClaimedByOtherNextbot(playerController.transform))
            {
                continue;
            }

            float planarDistance = GetPlanarDistance(transform.position, playerController.transform.position);
            if (planarDistance < bestDistance)
            {
                bestDistance = planarDistance;
                bestTransform = playerController.transform;
                bestController = playerController;
            }
        }

        if (bestTransform == null || bestController == null)
        {
            return null;
        }

        return new ScoredTarget
        {
            Transform = bestTransform,
            Controller = bestController,
            PathDistance = bestDistance,
            HasLineOfSight = true,
            Score = Mathf.Max(0f, _distanceScoreBase - bestDistance),
        };
    }

    private ScoredTarget? FindBestTarget()
    {
        Transform bestTransform = null;
        PlayerController bestController = null;
        float bestScore = float.NegativeInfinity;
        float bestDistance = float.PositiveInfinity;
        bool bestLineOfSight = false;

        PlayerController[] playerControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        for (int i = 0; i < playerControllers.Length; i++)
        {
            PlayerController playerController = playerControllers[i];
            if (playerController == null || !playerController.enabled || playerController.transform == transform)
            {
                continue;
            }

            ScoredTarget? scoredTarget = BuildScoredTarget(playerController.transform, playerController, playerController.transform == _target);
            if (!scoredTarget.HasValue)
            {
                continue;
            }

            if (scoredTarget.Value.Score > bestScore)
            {
                bestTransform = scoredTarget.Value.Transform;
                bestController = scoredTarget.Value.Controller;
                bestScore = scoredTarget.Value.Score;
                bestDistance = scoredTarget.Value.PathDistance;
                bestLineOfSight = scoredTarget.Value.HasLineOfSight;
            }
        }

        if (bestTransform == null)
        {
            return null;
        }

        return new ScoredTarget
        {
            Transform = bestTransform,
            Controller = bestController,
            Score = bestScore,
            PathDistance = bestDistance,
            HasLineOfSight = bestLineOfSight,
        };
    }

    private ScoredTarget? BuildScoredTarget(Transform candidateTransform, PlayerController candidateController, bool isCurrentTarget)
    {
        if (candidateTransform == null || candidateController == null || !candidateController.enabled || candidateController.IsInjuredOrHitReacting())
        {
            return null;
        }

        if ((_forceOfflineLocalAuthority || ShouldUseMapLocalNavMeshPresentation())
            && !isCurrentTarget
            && IsTargetClaimedByOtherNextbot(candidateTransform))
        {
            return null;
        }

        float pathDistance = GetTargetSelectionDistance(candidateTransform.position);
        float maxChaseRange = GetCurrentMapMaxChaseRange();
        if (float.IsInfinity(pathDistance) || pathDistance > maxChaseRange)
        {
            return null;
        }

        bool hasLineOfSight = HasLineOfSight(candidateTransform);
        float distanceScore = Mathf.Max(0f, _distanceScoreBase - pathDistance);
        float visibleBonus = hasLineOfSight && pathDistance <= _visibleRange ? _visibleBonus : 0f;
        float frontBonus = IsTargetInFront(candidateTransform.position) ? _frontBonus : 0f;
        float currentTargetBonus = isCurrentTarget ? _currentTargetBonus : 0f;
        float localPlayerPressureBonus = _forceOfflineLocalAuthority && IsOfflineLocalPlayerTarget(candidateTransform)
            ? OfflineLocalPlayerPressureBonus
            : 0f;
        float targetPressurePenalty = _forceOfflineLocalAuthority
            ? 0f
            : CountOtherNextbotsTargeting(candidateTransform) * _sameTargetScorePenalty;

        return new ScoredTarget
        {
            Transform = candidateTransform,
            Controller = candidateController,
            PathDistance = pathDistance,
            HasLineOfSight = hasLineOfSight,
            Score = distanceScore + visibleBonus + frontBonus + currentTargetBonus + localPlayerPressureBonus - targetPressurePenalty,
        };
    }

    private int CountOtherNextbotsTargeting(Transform candidateTransform)
    {
        if (candidateTransform == null || _sameTargetScorePenalty <= 0f)
        {
            return 0;
        }

        int count = 0;
        NextbotFollowPlayer[] nextbots = FindObjectsByType<NextbotFollowPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < nextbots.Length; i++)
        {
            NextbotFollowPlayer nextbot = nextbots[i];
            if (nextbot == null || nextbot == this)
            {
                continue;
            }

            if (nextbot._target == candidateTransform)
            {
                count++;
            }
        }

        return count;
    }

    private bool CanKeepCurrentTarget()
    {
        if (_target == null || _targetController == null || !_targetController.enabled || _targetController.IsInjuredOrHitReacting())
        {
            return false;
        }

        if ((_forceOfflineLocalAuthority || ShouldUseMapLocalNavMeshPresentation())
            && IsTargetClaimedByOtherNextbot(_target))
        {
            return false;
        }

        float pathDistance = GetTargetSelectionDistance(_target.position);
        float maxChaseRange = GetCurrentMapMaxChaseRange();
        return !float.IsInfinity(pathDistance) && pathDistance <= maxChaseRange;
    }

    private float GetTargetSelectionDistance(Vector3 destination)
    {
        if (ShouldUseMapLocalNavMeshPresentation())
        {
            return GetPlanarDistance(transform.position, destination);
        }

        return GetPathDistance(destination);
    }

    private void ApplyOfflineMultiplayerTuning()
    {
        _stoppingDistance = MultiplayerNextbotStoppingDistance;
        _targetRefreshInterval = MultiplayerNextbotScanIntervalSeconds;
        _targetLockDuration = MultiplayerNextbotTargetLockSeconds;
        _switchScoreThreshold = MultiplayerNextbotSwitchScoreThreshold;
        _switchConfirmDuration = MultiplayerNextbotSwitchConfirmSeconds;
        _hitDistance = MultiplayerNextbotInjuryDistance;
        _hitCooldown = MultiplayerNextbotInjuryCooldownSeconds;

        if (_navMeshAgent != null)
        {
            _navMeshAgent.stoppingDistance = _stoppingDistance;
        }
    }

    private bool IsOfflineTargetClaimedByOtherNextbot(Transform candidateTransform)
    {
        return IsTargetClaimedByOtherNextbot(candidateTransform);
    }

    private bool IsTargetClaimedByOtherNextbot(Transform candidateTransform)
    {
        if (candidateTransform == null)
        {
            return false;
        }

        NextbotFollowPlayer[] nextbots = FindObjectsByType<NextbotFollowPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < nextbots.Length; i++)
        {
            NextbotFollowPlayer nextbot = nextbots[i];
            if (nextbot == null || nextbot == this || !nextbot.enabled || nextbot._target != candidateTransform)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsOfflineLocalPlayerTarget(Transform candidateTransform)
    {
        if (candidateTransform == null)
        {
            return false;
        }

        OfflinePlayerIdentity identity = candidateTransform.GetComponent<OfflinePlayerIdentity>();
        if (identity == null)
        {
            identity = candidateTransform.GetComponentInParent<OfflinePlayerIdentity>();
        }

        return identity != null && identity.IsLocalPlayer;
    }

    private float GetPathDistance(Vector3 destination)
    {
        if (_navMeshAgent == null)
        {
            return Vector3.Distance(transform.position, destination);
        }

        if (!TryGetNearestNavMeshPosition(transform.position, out Vector3 sourcePosition)
            || !TryGetNearestNavMeshPosition(destination, out Vector3 destinationPosition))
        {
            return float.PositiveInfinity;
        }

        if (!NavMesh.CalculatePath(sourcePosition, destinationPosition, _navMeshAgent.areaMask, _pathBuffer))
        {
            return TryGetJumpDistance(destination, out float jumpDistance) ? jumpDistance : float.PositiveInfinity;
        }

        if (_pathBuffer.status != NavMeshPathStatus.PathComplete || _pathBuffer.corners.Length < 2)
        {
            return TryGetJumpDistance(destination, out float jumpDistance) ? jumpDistance : float.PositiveInfinity;
        }

        float totalDistance = 0f;
        for (int i = 1; i < _pathBuffer.corners.Length; i++)
        {
            totalDistance += Vector3.Distance(_pathBuffer.corners[i - 1], _pathBuffer.corners[i]);
        }

        return totalDistance;
    }

    private bool HasLineOfSight(Transform candidateTransform)
    {
        Vector3 origin = transform.position + Vector3.up * _eyeHeight;
        Vector3 target = candidateTransform.position + Vector3.up * _targetEyeHeight;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        int mask = _wallDetectionLayers.value != 0 ? _wallDetectionLayers.value : Physics.DefaultRaycastLayers;
        return !Physics.Raycast(origin, direction / distance, distance, mask, QueryTriggerInteraction.Ignore);
    }

    private bool IsTargetInFront(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float angle = Vector3.Angle(transform.forward, toTarget.normalized);
        return angle <= _frontAngleThreshold;
    }

    private void AssignTarget(Transform targetTransform, PlayerController controller)
    {
        _target = targetTransform;
        _targetController = controller;
        _mapTargetHoldUntil = Time.time + GetCurrentMapTargetHoldSeconds();
        _targetLockedUntil = Time.time + _targetLockDuration;
        _pendingSwitchTarget = null;
        _pendingSwitchStartedAt = 0f;
        ResetOfflinePatrolTarget();
    }

    private void ClearTarget()
    {
        bool hadTarget = _target != null;
        _target = null;
        _targetController = null;
        _hasLastSeenTargetPosition = false;
        _lastSeenTargetExpiresAt = float.NegativeInfinity;
        _mapTargetHoldUntil = 0f;
        _targetLockedUntil = 0f;
        _pendingSwitchTarget = null;
        _pendingSwitchStartedAt = 0f;

        if (hadTarget)
        {
            ResetOfflinePatrolTarget();
        }
    }

    private void ResetRoomStateParkourPatrolTarget()
    {
        _hasRoomStateParkourPatrolTarget = false;
        _roomStateParkourPatrolTarget = Vector3.zero;
        _roomStateParkourPatrolWaitUntil = 0f;
    }

    private void ResetOfflinePatrolTarget()
    {
        _hasOfflinePatrolTarget = false;
        _offlinePatrolTarget = Vector3.zero;
        _offlinePatrolWaitUntil = 0f;
    }

    private bool TryUpdateOfflinePatrolMovement()
    {
        if (!_forceOfflineLocalAuthority || !_offlineNextbotActive)
        {
            ResetOfflinePatrolTarget();
            return false;
        }

        if (!OfflineModeManager.TryGetExisting(out OfflineModeManager offlineModeManager)
            || !offlineModeManager.IsOfflineRoundActive)
        {
            ResetOfflinePatrolTarget();
            return false;
        }

        if (!_hasOfflinePatrolTarget)
        {
            SetNextOfflinePatrolTarget(offlineModeManager);
        }

        float distance = GetPlanarDistance(transform.position, _offlinePatrolTarget);
        if (distance <= OfflinePatrolReachedDistance)
        {
            if (_offlinePatrolWaitUntil <= 0f)
            {
                _offlinePatrolWaitUntil = Time.time + UnityEngine.Random.Range(OfflinePatrolRetargetMinSeconds, OfflinePatrolRetargetMaxSeconds);
                StopAgent();
                return true;
            }

            if (Time.time < _offlinePatrolWaitUntil)
            {
                StopAgent();
                return true;
            }

            SetNextOfflinePatrolTarget(offlineModeManager);
            distance = GetPlanarDistance(transform.position, _offlinePatrolTarget);
        }

        MoveTowardOfflinePatrolTarget(_offlinePatrolTarget);
        return true;
    }

    private bool TryUpdateRoomStateParkourPatrolMovement()
    {
        if (!ShouldUseMapLocalNavMeshPresentation() || _target != null)
        {
            ResetRoomStateParkourPatrolTarget();
            return false;
        }

        NetworkManager networkManager = NetworkManager.Instance;
        if (networkManager == null)
        {
            ResetRoomStateParkourPatrolTarget();
            return false;
        }

        List<Vector3> patrolPositions = GetRoomStateParkourPatrolPositions(networkManager);
        if (patrolPositions == null || patrolPositions.Count == 0)
        {
            ResetRoomStateParkourPatrolTarget();
            return false;
        }

        if (!_hasRoomStateParkourPatrolTarget)
        {
            SetNextRoomStateParkourPatrolTarget(patrolPositions);
        }

        float distance = GetPlanarDistance(transform.position, _roomStateParkourPatrolTarget);
        if (distance <= OfflinePatrolReachedDistance)
        {
            if (_roomStateParkourPatrolWaitUntil <= 0f)
            {
                _roomStateParkourPatrolWaitUntil = Time.time + UnityEngine.Random.Range(OfflinePatrolRetargetMinSeconds, OfflinePatrolRetargetMaxSeconds);
                StopAgent();
                return true;
            }

            if (Time.time < _roomStateParkourPatrolWaitUntil)
            {
                StopAgent();
                return true;
            }

            SetNextRoomStateParkourPatrolTarget(patrolPositions);
        }

        MoveTowardOfflinePatrolTarget(_roomStateParkourPatrolTarget);
        return true;
    }

    private List<Vector3> GetRoomStateParkourPatrolPositions(NetworkManager networkManager)
    {
        if (networkManager == null)
        {
            return null;
        }

        List<Vector3> patrolPositions = networkManager.GetConfiguredNextbotPatrolPositions();
        if (patrolPositions != null && patrolPositions.Count > 0)
        {
            return patrolPositions;
        }

        List<Vector3> playerSpawnPositions = networkManager.GetConfiguredPlayerSpawnPositions();
        if (playerSpawnPositions != null && playerSpawnPositions.Count > 0)
        {
            return playerSpawnPositions;
        }

        List<Vector3> nextbotSpawnPositions = networkManager.GetConfiguredNextbotSpawnPositions();
        if (nextbotSpawnPositions != null && nextbotSpawnPositions.Count > 0)
        {
            return nextbotSpawnPositions;
        }

        return null;
    }

    private void SetNextRoomStateParkourPatrolTarget(List<Vector3> patrolPositions)
    {
        if (patrolPositions == null || patrolPositions.Count == 0)
        {
            ResetRoomStateParkourPatrolTarget();
            return;
        }

        int botIndex = Mathf.Max(0, GetNextbotIndex());
        int patrolCount = patrolPositions.Count;
        int selectedIndex = botIndex % patrolCount;
        Vector3 selectedPosition = patrolPositions[selectedIndex];
        float selectedScore = GetRoomStateParkourPatrolScore(selectedPosition);
        bool foundUsefulTarget = GetPlanarDistance(transform.position, selectedPosition) > OfflinePatrolReachedDistance;

        int sampleCount = Mathf.Min(patrolCount, 6);
        for (int offset = 1; offset < sampleCount; offset++)
        {
            int candidateIndex = (selectedIndex + offset + botIndex) % patrolCount;
            Vector3 candidate = patrolPositions[candidateIndex];
            float candidateScore = GetRoomStateParkourPatrolScore(candidate) + UnityEngine.Random.Range(0f, 6f);
            bool candidateIsUseful = GetPlanarDistance(transform.position, candidate) > OfflinePatrolReachedDistance;
            if ((!foundUsefulTarget && candidateIsUseful) || candidateScore > selectedScore)
            {
                selectedIndex = candidateIndex;
                selectedPosition = candidate;
                selectedScore = candidateScore;
                foundUsefulTarget = candidateIsUseful;
            }
        }

        if (!foundUsefulTarget)
        {
            selectedPosition = GetSyntheticParkourPatrolTarget(botIndex);
        }

        if (TryResolveGroundedPosition(selectedPosition, out Vector3 groundedPatrolTarget))
        {
            selectedPosition = groundedPatrolTarget;
        }

        _roomStateParkourPatrolTarget = selectedPosition;
        _roomStateParkourPatrolWaitUntil = 0f;
        _hasRoomStateParkourPatrolTarget = true;
    }

    private Vector3 GetSyntheticParkourPatrolTarget(int botIndex)
    {
        float angle = (botIndex * 137.5f + Time.time * 35f) * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ParkourFallbackPatrolRadius;
        return transform.position + offset;
    }

    private float GetRoomStateParkourPatrolScore(Vector3 candidate)
    {
        float distanceScore = GetPlanarDistance(transform.position, candidate);
        float wallPenalty = IsRoomStatePatrolPathBlocked(candidate) ? 12f : 0f;
        return distanceScore - wallPenalty;
    }

    private bool IsRoomStatePatrolPathBlocked(Vector3 candidate)
    {
        Vector3 origin = transform.position + Vector3.up * _eyeHeight;
        Vector3 destination = candidate + Vector3.up * Mathf.Min(_targetEyeHeight, 0.5f);
        Vector3 direction = destination - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            return false;
        }

        int wallMask = GetEffectiveWallDetectionMask();
        return wallMask != 0
            && Physics.Raycast(origin, direction / distance, distance, wallMask, QueryTriggerInteraction.Ignore);
    }

    private void SetNextOfflinePatrolTarget(OfflineModeManager offlineModeManager)
    {
        if (offlineModeManager == null)
        {
            ResetOfflinePatrolTarget();
            return;
        }

        Vector3 patrolTarget = offlineModeManager.GetPatrolPosition(GetOfflinePatrolIndex(), transform.position);
        if (TryResolveGroundedPosition(patrolTarget, out Vector3 groundedPatrolTarget))
        {
            patrolTarget = groundedPatrolTarget;
        }

        _offlinePatrolTarget = patrolTarget;
        _offlinePatrolWaitUntil = 0f;
        _hasOfflinePatrolTarget = true;
    }

    private void MoveTowardOfflinePatrolTarget(Vector3 patrolTarget)
    {
        if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.speed = _moveSpeed;
            _navMeshAgent.acceleration = _acceleration;
            _navMeshAgent.stoppingDistance = _stoppingDistance;
            _navMeshAgent.isStopped = false;

            Vector3 navMeshPatrolPosition = patrolTarget;
            bool hasNavMeshPatrolPosition = TryGetNearestNavMeshPosition(patrolTarget, out navMeshPatrolPosition);
            if (hasNavMeshPatrolPosition)
            {
                _navMeshAgent.SetDestination(navMeshPatrolPosition);
            }

            bool pathBlocked = !_navMeshAgent.pathPending
                && (_navMeshAgent.pathStatus == NavMeshPathStatus.PathPartial
                    || _navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid);
            bool edgeJumpNeeded = ShouldJumpFromEdge(patrolTarget, hasNavMeshPatrolPosition ? navMeshPatrolPosition : patrolTarget);
            if ((pathBlocked || edgeJumpNeeded) && TryStartJumpToward(patrolTarget))
            {
                return;
            }

            if (_useOffMeshLinks && _navMeshAgent.isOnOffMeshLink)
            {
                BeginOffMeshLinkTraversal();
                return;
            }

            Vector3 velocity = _navMeshAgent.desiredVelocity;
            velocity.y = 0f;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, velocity, _acceleration * Time.deltaTime);

            float planarDistance = GetPlanarDistance(transform.position, patrolTarget);
            if (ShouldUseMapLocalNavMeshPresentation()
                && _horizontalVelocity.sqrMagnitude < 0.05f
                && planarDistance > _stoppingDistance + 0.5f)
            {
                ApplySmoothFallbackMovement(patrolTarget);
                return;
            }

            Vector3 agentGroundPos = _navMeshAgent.nextPosition;
            Vector3 syncedPos = agentGroundPos;
            transform.position = syncedPos;
            _navMeshAgent.nextPosition = syncedPos;
            _lockedHeight = syncedPos.y;

            UpdateBodyRotation(_horizontalVelocity);
            ApplySolidObstaclePush();
            return;
        }

        if (_lockToStartingHeight)
        {
            patrolTarget.y = _lockedHeight;
        }

        Vector3 planarOffset = patrolTarget - transform.position;
        planarOffset.y = 0f;
        float currentDistance = planarOffset.magnitude;
        Vector3 moveDirection = currentDistance > 0.001f ? planarOffset / currentDistance : Vector3.zero;
        Vector3 desiredVelocity = currentDistance > _stoppingDistance ? moveDirection * _moveSpeed : Vector3.zero;
        _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desiredVelocity, _acceleration * Time.deltaTime);

        ApplyFallbackMovement(_horizontalVelocity);
    }

    private int GetOfflinePatrolIndex()
    {
        if (TryParseTrailingIndex(_networkNextbotId, 0, out int networkIndex))
        {
            return networkIndex;
        }

        if (gameObject != null && TryParseTrailingIndex(gameObject.name, -1, out int objectNameIndex))
        {
            return objectNameIndex;
        }

        return Mathf.Abs(GetInstanceID());
    }

    private static bool TryParseTrailingIndex(string value, int offset, out int index)
    {
        index = 0;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        int end = value.Length - 1;
        while (end >= 0 && !char.IsDigit(value[end]))
        {
            end--;
        }

        if (end < 0)
        {
            return false;
        }

        int start = end;
        while (start >= 0 && char.IsDigit(value[start]))
        {
            start--;
        }

        if (!int.TryParse(value.Substring(start + 1, end - start), out int parsedIndex))
        {
            return false;
        }

        index = Mathf.Max(0, parsedIndex + offset);
        return true;
    }

    private static float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private int GetNextbotIndex()
    {
        // e.g. "nextbot_0" -> 0, "nextbot_2" -> 2
        if (TryParseTrailingIndex(_networkNextbotId, 0, out int index))
        {
            return index;
        }
        return -1;
    }

    private bool TryGetServerAssignedTarget(string targetSessionId, out Transform targetTransform, out PlayerController targetController)
    {
        targetTransform = null;
        targetController = null;

        if (string.IsNullOrWhiteSpace(targetSessionId))
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Instance;
        if (networkManager == null || !networkManager.TryGetPlayerObject(targetSessionId, out GameObject playerObject) || playerObject == null)
        {
            return false;
        }

        targetController = playerObject.GetComponent<PlayerController>();
        if (targetController == null || !targetController.enabled || targetController.IsInjuredOrHitReacting())
        {
            return false;
        }

        targetTransform = targetController.transform;
        return targetTransform != null;
    }

    private void UpdateMovement()
    {
        if (_target == null)
        {
            if (TryUpdateRoomStateParkourPatrolMovement())
            {
                return;
            }

            if (TryUpdateOfflinePatrolMovement())
            {
                return;
            }

            StopAgent();
            return;
        }

        Vector3 targetPosition = ResolveTargetChasePosition();
        bool shouldUseParkourLocalPresentation = ShouldUseMapLocalNavMeshPresentation();

        if (_navMeshAgent != null && _navMeshAgent.enabled)
        {
            if (shouldUseParkourLocalPresentation && !_navMeshAgent.isOnNavMesh)
            {
                if (!TryRecoverParkourLocalNavMeshPresentation())
                {
                    ApplySmoothFallbackMovement(targetPosition);
                    return;
                }
            }

            if (!_navMeshAgent.isOnNavMesh)
            {
                ApplySmoothFallbackMovement(targetPosition);
                return;
            }

            _navMeshAgent.speed = _moveSpeed;
            _navMeshAgent.acceleration = _acceleration;
            _navMeshAgent.stoppingDistance = _stoppingDistance;
            _navMeshAgent.isStopped = false;

            Vector3 navMeshTargetPosition = targetPosition;
            bool hasNavMeshTargetPosition = TryGetNearestNavMeshPosition(targetPosition, out navMeshTargetPosition);
            if (hasNavMeshTargetPosition)
            {
                _navMeshAgent.SetDestination(navMeshTargetPosition);
            }

            bool pathBlocked = !_navMeshAgent.pathPending
                && (_navMeshAgent.pathStatus == NavMeshPathStatus.PathPartial
                    || _navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid);
            bool edgeJumpNeeded = ShouldJumpFromEdge(targetPosition, hasNavMeshTargetPosition ? navMeshTargetPosition : targetPosition);
            if ((pathBlocked || edgeJumpNeeded) && TryStartJumpToward(targetPosition))
            {
                return;
            }

            if (_useOffMeshLinks && _navMeshAgent.isOnOffMeshLink)
            {
                BeginOffMeshLinkTraversal();
                return;
            }

            Vector3 velocity = _navMeshAgent.desiredVelocity;
            velocity.y = 0f;

            // --- STUCK DETECTION ---
            // If the agent is trying to move but its velocity is nearly zero (stuck on a wall/corner),
            // and we are still far from the target, force some direct movement.
            float planarDistance = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(targetPosition.x, 0f, targetPosition.z));
            if (velocity.sqrMagnitude < 0.5f && planarDistance > _stoppingDistance + 0.5f)
            {
                Vector3 directDir = (targetPosition - transform.position).normalized;
                directDir.y = 0f;
                // Use Move() to nudge the agent position directly, respecting NavMesh boundaries.
                _navMeshAgent.Move(directDir * _moveSpeed * 0.5f * Time.deltaTime);
                velocity = directDir * _moveSpeed * 0.5f;
            }

            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, velocity, _acceleration * Time.deltaTime);

            // Sync the transform to the agent's next position.
            Vector3 agentPos = _navMeshAgent.nextPosition;
            transform.position = agentPos;
            _lockedHeight = agentPos.y;

            UpdateBodyRotation(_horizontalVelocity);
            ApplySolidObstaclePush();
            EnsureAgentOnNavMesh();
            TryHitTarget(planarDistance);
            return;
        }

        // --- OFF-MESH FALLBACK ---
        // If we are off the NavMesh, calculate a simple direct velocity toward the target
        // so we can at least move toward the valid navigation area or hit the player.
        ApplySmoothFallbackMovement(targetPosition);
    }

    private void ApplySmoothFallbackMovement(Vector3 targetPosition)
    {
        Vector3 fallbackPlanarOffset = targetPosition - transform.position;
        fallbackPlanarOffset.y = 0f;
        float fallbackDistance = fallbackPlanarOffset.magnitude;
        Vector3 fallbackDir = fallbackDistance > 0.001f ? fallbackPlanarOffset / fallbackDistance : Vector3.zero;
        Vector3 fallbackDesiredVelocity = fallbackDistance > _stoppingDistance ? fallbackDir * _moveSpeed : Vector3.zero;
        _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, fallbackDesiredVelocity, _acceleration * Time.deltaTime);

        if (_lockToStartingHeight)
        {
            targetPosition.y = _lockedHeight;
        }

        ApplyFallbackMovement(_horizontalVelocity);
        TryHitTarget(fallbackDistance);
    }

    private bool TryRecoverParkourLocalNavMeshPresentation()
    {
        if (_navMeshAgent == null || !_navMeshAgent.enabled)
        {
            return false;
        }

        if (_navMeshAgent.isOnNavMesh)
        {
            return true;
        }

        if (!TryGetNearestNavMeshPosition(transform.position, out Vector3 navMeshPosition))
        {
            return false;
        }

        float planarDistance = GetPlanarDistance(transform.position, navMeshPosition);
        if (planarDistance > GetCurrentMapNavMeshRejoinWarpDistance())
        {
            Vector3 nextPosition = Vector3.MoveTowards(transform.position, navMeshPosition, _moveSpeed * Time.deltaTime);
            if (TryResolveGroundedPosition(nextPosition, out Vector3 groundedNextPosition))
            {
                nextPosition = groundedNextPosition;
            }

            transform.position = nextPosition;
            return false;
        }

        _navMeshAgent.Warp(navMeshPosition);
        return _navMeshAgent.isOnNavMesh;
    }

    private Vector3 ResolveTargetChasePosition()
    {
        if (_target == null)
        {
            return transform.position;
        }

        Vector3 targetPosition = _target.position;
        if (!ShouldUseMapLocalNavMeshChase())
        {
            return targetPosition;
        }

        if (TryGetVisibleTargetPosition(out Vector3 visibleTargetPosition))
        {
            CacheLastSeenTarget(visibleTargetPosition);
            return visibleTargetPosition;
        }

        if (_hasLastSeenTargetPosition && Time.time <= _lastSeenTargetExpiresAt)
        {
            Vector3 lastSeenDestination = _lastSeenTargetPosition;
            if (_lastSeenTargetDirection.sqrMagnitude > 0.0001f)
            {
                lastSeenDestination += _lastSeenTargetDirection.normalized * _lastSeenTargetLeadDistance;
            }

            if (GetPlanarDistance(transform.position, lastSeenDestination) <= _lastSeenTargetReachedDistance)
            {
                lastSeenDestination = _lastSeenTargetPosition;
            }

            return lastSeenDestination;
        }

        return targetPosition;
    }

    private bool TryGetVisibleTargetPosition(out Vector3 targetPosition)
    {
        targetPosition = Vector3.zero;
        if (_target == null)
        {
            return false;
        }

        targetPosition = _target.position;
        Vector3 origin = transform.position + Vector3.up * _eyeHeight;
        Vector3 destination = targetPosition + Vector3.up * _targetEyeHeight;
        Vector3 direction = destination - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        int wallMask = GetEffectiveWallDetectionMask();
        if (wallMask == 0)
        {
            return true;
        }

        return !Physics.Raycast(origin, direction / distance, distance, wallMask, QueryTriggerInteraction.Ignore);
    }

    private void CacheLastSeenTarget(Vector3 visibleTargetPosition)
    {
        _lastSeenTargetPosition = visibleTargetPosition;

        Vector3 targetVelocity = _targetController != null ? _targetController.GetVelocity() : Vector3.zero;
        targetVelocity.y = 0f;
        if (targetVelocity.sqrMagnitude > 0.0001f)
        {
            _lastSeenTargetDirection = targetVelocity.normalized;
        }
        else if (_target != null)
        {
            Vector3 targetForward = _target.forward;
            targetForward.y = 0f;
            if (targetForward.sqrMagnitude > 0.0001f)
            {
                _lastSeenTargetDirection = targetForward.normalized;
            }
        }

        _hasLastSeenTargetPosition = true;
        _lastSeenTargetExpiresAt = Time.time + Mathf.Max(0f, _lastSeenTargetMemorySeconds);
    }

    private int GetEffectiveWallDetectionMask()
    {
        int mask = _wallDetectionLayers.value;
        int wallLayer = LayerMask.NameToLayer("Wall");
        if (wallLayer >= 0)
        {
            mask |= 1 << wallLayer;
        }

        return mask;
    }

    private bool TryStartJumpToward(Vector3 targetPosition)
    {
        if (!_allowLedgeJump || !TryGetJumpDirection(targetPosition, out Vector3 jumpDirection))
        {
            return false;
        }

        _isJumping = true;
        _jumpVelocity = jumpDirection * _jumpForwardSpeed;
        _jumpVelocity.y = _jumpUpwardSpeed;
        _horizontalVelocity = jumpDirection * _jumpForwardSpeed;

        if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.ResetPath();
        }

        return true;
    }

    private void UpdateJumpMovement()
    {
        _jumpVelocity.y -= _gravity * Time.deltaTime;
        transform.position += _jumpVelocity * Time.deltaTime;

        Vector3 horizontalVelocity = new Vector3(_jumpVelocity.x, 0f, _jumpVelocity.z);
        _horizontalVelocity = horizontalVelocity;
        UpdateBodyRotation(horizontalVelocity);

        if (_target != null)
        {
            Vector3 targetPosition = _target.position;
            TryHitTarget(Vector3.Distance(
                new Vector3(targetPosition.x, 0f, targetPosition.z),
                new Vector3(transform.position.x, 0f, transform.position.z)));
        }

        if (_jumpVelocity.y > 0f)
        {
            return;
        }

        if (!TryGetNearestNavMeshPosition(transform.position, out Vector3 navMeshPosition))
        {
            return;
        }

        if (TryResolveGroundedPosition(navMeshPosition, out Vector3 groundedNavMeshPosition))
        {
            navMeshPosition = groundedNavMeshPosition;
        }

        if (Mathf.Abs(transform.position.y - navMeshPosition.y) > _landingSnapDistance)
        {
            return;
        }

        if (_navMeshAgent != null && _navMeshAgent.enabled)
        {
            _groundHeightVelocity = 0f;
            _navMeshAgent.Warp(navMeshPosition);
            _navMeshAgent.isStopped = false;
        }
        else
        {
            _groundHeightVelocity = 0f;
            transform.position = navMeshPosition;
        }

        _isJumping = false;
        _jumpVelocity = Vector3.zero;
        _horizontalVelocity = Vector3.zero;
    }

    private void StopAgent()
    {
        _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, Vector3.zero, _acceleration * Time.deltaTime);
        if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
            return;
        }

        ApplyFallbackMovement(Vector3.zero);
    }

    private void BeginOffMeshLinkTraversal()
    {
        if (_navMeshAgent == null || !_navMeshAgent.enabled || !_navMeshAgent.isOnOffMeshLink)
        {
            return;
        }

        OffMeshLinkData linkData = _navMeshAgent.currentOffMeshLinkData;
        _offMeshLinkStart = transform.position;
        _offMeshLinkEnd = linkData.endPos;
        _offMeshLinkProgress = 0f;
        _isTraversingOffMeshLink = true;
        _navMeshAgent.isStopped = true;
        _navMeshAgent.updatePosition = false;
    }

    private void UpdateOffMeshLinkTraversal()
    {
        if (_navMeshAgent == null || !_navMeshAgent.enabled)
        {
            _isTraversingOffMeshLink = false;
            return;
        }

        float duration = Mathf.Max(0.01f, _offMeshLinkDuration);
        _offMeshLinkProgress = Mathf.Clamp01(_offMeshLinkProgress + Time.deltaTime / duration);

        Vector3 nextPosition = Vector3.Lerp(_offMeshLinkStart, _offMeshLinkEnd, _offMeshLinkProgress);
        float arc = Mathf.Sin(_offMeshLinkProgress * Mathf.PI) * _offMeshLinkArcHeight;
        nextPosition.y += arc;
        transform.position = nextPosition;

        Vector3 planarVelocity = _offMeshLinkEnd - _offMeshLinkStart;
        planarVelocity.y = 0f;
        _horizontalVelocity = planarVelocity.normalized * _jumpForwardSpeed;
        UpdateBodyRotation(_horizontalVelocity);

        if (_offMeshLinkProgress < 1f)
        {
            return;
        }

        if (TryResolveGroundedPosition(_offMeshLinkEnd, out Vector3 groundedOffMeshLinkEnd))
        {
            _offMeshLinkEnd = groundedOffMeshLinkEnd;
        }

        _isTraversingOffMeshLink = false;
        _horizontalVelocity = Vector3.zero;
        _groundHeightVelocity = 0f;
        _navMeshAgent.Warp(_offMeshLinkEnd);
        _navMeshAgent.CompleteOffMeshLink();
        _navMeshAgent.updatePosition = true;
        _navMeshAgent.isStopped = false;
    }

    private void ApplyFallbackMovement(Vector3 horizontalVelocity)
    {
        UpdateBodyRotation(horizontalVelocity);

        if (_characterController != null)
        {
            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }
            else
            {
                _verticalVelocity -= _gravity * Time.deltaTime;
            }

            Vector3 movement = horizontalVelocity;
            movement.y = _verticalVelocity;
            _characterController.Move(movement * Time.deltaTime);

            if (TryResolveGroundedPosition(transform.position, out Vector3 groundedPosition)
                && (_characterController.isGrounded || _verticalVelocity <= 0f))
            {
                Vector3 smoothedGroundedPosition = SmoothGroundedPosition(transform.position, groundedPosition);
                transform.position = smoothedGroundedPosition;
                _lockedHeight = smoothedGroundedPosition.y;
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -1f;
                }
            }
            else if (_lockToStartingHeight)
            {
                Vector3 position = transform.position;
                position.y = _lockedHeight;
                transform.position = position;
                _verticalVelocity = 0f;
            }

            return;
        }

        Vector3 nextPosition = transform.position + horizontalVelocity * Time.deltaTime;
        if (TryResolveGroundedPosition(nextPosition, out Vector3 groundedNextPosition))
        {
            nextPosition = SmoothGroundedPosition(transform.position, groundedNextPosition);
            _lockedHeight = nextPosition.y;
        }
        else if (_lockToStartingHeight)
        {
            nextPosition.y = _lockedHeight;
        }

        transform.position = nextPosition;
    }

    private void UpdateBodyRotation(Vector3 horizontalVelocity)
    {
        Vector3 facingDirection = horizontalVelocity;
        facingDirection.y = 0f;

        if (facingDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
        float rotationBlend = 1f - Mathf.Exp(-_rotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationBlend);
    }

    private bool TryGetJumpDistance(Vector3 targetPosition, out float jumpDistance)
    {
        if (!TryGetJumpDirection(targetPosition, out Vector3 jumpDirection))
        {
            jumpDistance = 0f;
            return false;
        }

        Vector3 delta = targetPosition - transform.position;
        delta.y = 0f;
        jumpDistance = delta.magnitude;
        return jumpDistance > 0.001f;
    }

    private bool TryGetJumpDirection(Vector3 targetPosition, out Vector3 jumpDirection)
    {
        jumpDirection = Vector3.zero;
        if (!_allowLedgeJump)
        {
            return false;
        }

        Vector3 delta = targetPosition - transform.position;
        float verticalDelta = delta.y;
        delta.y = 0f;
        float planarDistance = delta.magnitude;

        if (planarDistance <= _stoppingDistance || planarDistance > _maxJumpDistance)
        {
            return false;
        }

        if (verticalDelta > _maxJumpUpHeight || verticalDelta < -_maxJumpDownHeight)
        {
            return false;
        }

        bool isDropJump = verticalDelta <= -_edgeJumpMinDrop;
        int mask = _wallDetectionLayers.value != 0 ? _wallDetectionLayers.value : Physics.DefaultRaycastLayers;
        Vector3 origin = transform.position + Vector3.up * _eyeHeight;
        Vector3 target = targetPosition + Vector3.up * Mathf.Min(_targetEyeHeight, 0.5f);
        Vector3 rayDirection = target - origin;
        float rayDistance = rayDirection.magnitude;
        if (!isDropJump
            && rayDistance > 0.001f
            && Physics.Raycast(origin, rayDirection / rayDistance, rayDistance, mask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        jumpDirection = delta / planarDistance;
        return true;
    }

    private bool ShouldJumpFromEdge(Vector3 targetPosition, Vector3 navMeshTargetPosition)
    {
        if (!_allowLedgeJump || _navMeshAgent == null || !_navMeshAgent.enabled || !_navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        Vector3 toTarget = targetPosition - transform.position;
        float verticalDelta = toTarget.y;
        toTarget.y = 0f;
        float planarDistanceToTarget = toTarget.magnitude;
        if (planarDistanceToTarget <= _stoppingDistance || planarDistanceToTarget > _maxJumpDistance)
        {
            return false;
        }

        if (verticalDelta > -_edgeJumpMinDrop || verticalDelta < -_maxJumpDownHeight)
        {
            return false;
        }

        Vector3 toNavMeshTarget = navMeshTargetPosition - transform.position;
        toNavMeshTarget.y = 0f;
        float planarDistanceToNavMeshTarget = toNavMeshTarget.magnitude;
        bool navMeshStopsAtEdge = planarDistanceToNavMeshTarget <= _stoppingDistance + 0.35f;
        bool agentStalled = _navMeshAgent.desiredVelocity.sqrMagnitude <= _edgeStuckVelocity * _edgeStuckVelocity
            && _navMeshAgent.velocity.sqrMagnitude <= _edgeStuckVelocity * _edgeStuckVelocity;

        return navMeshStopsAtEdge && agentStalled;
    }

    private void TryHitTarget(float distanceToTarget)
    {
        if (_roomStateAuthorityActive && !ShouldUseMapLocalNavMeshPresentation())
        {
            return;
        }

        if (_target == null || Time.time < _nextHitTime || distanceToTarget > _hitDistance)
        {
            return;
        }

        if (_forceOfflineLocalAuthority
            && OfflineModeManager.TryGetExisting(out OfflineModeManager offlineModeManager)
            && !offlineModeManager.CanOfflineNextbotsDamagePlayers)
        {
            return;
        }

        PlayerController targetController = _targetController ?? ResolvePlayerController(_target);
        if (targetController == null || !targetController.enabled || targetController.IsInjuredOrHitReacting())
        {
            return;
        }

        if (targetController.TriggerNextbotHit(transform.position))
        {
            if (!_forceOfflineLocalAuthority && NetworkManager.Instance != null && targetController.IsSimulationControlled() == false)
            {
                if (!string.IsNullOrWhiteSpace(_networkNextbotId))
                {
                    NetworkManager.Instance.SendNextbotHit(_networkNextbotId, transform.position);
                }
            }

            IgnoreCollisionWithPlayer(targetController);
            ClearTarget();
            _horizontalVelocity = Vector3.zero;
            _nextTargetRefreshTime = 0f;
            _nextHitTime = Time.time + Mathf.Max(0.1f, _hitCooldown);
        }
    }

    private PlayerController ResolvePlayerController(Transform targetTransform)
    {
        if (targetTransform == null)
        {
            return null;
        }

        PlayerController targetController = targetTransform.GetComponent<PlayerController>();
        if (targetController == null)
        {
            targetController = targetTransform.GetComponentInParent<PlayerController>();
        }

        return targetController;
    }

    private void IgnoreCollisionWithPlayer(PlayerController targetController)
    {
        if (targetController == null)
        {
            return;
        }

        CharacterController targetCharacterController = targetController.GetCharacterController();
        if (targetCharacterController == null || _ignoredInjuredTargets.Contains(targetCharacterController))
        {
            return;
        }

        for (int i = 0; i < _nextbotColliders.Length; i++)
        {
            Collider nextbotCollider = _nextbotColliders[i];
            if (nextbotCollider == null)
            {
                continue;
            }

            Physics.IgnoreCollision(nextbotCollider, targetCharacterController, true);
        }

        _ignoredInjuredTargets.Add(targetCharacterController);
    }

    private void SyncIgnoredTargets()
    {
        PlayerController[] playerControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        for (int i = 0; i < playerControllers.Length; i++)
        {
            PlayerController playerController = playerControllers[i];
            if (playerController == null || !playerController.enabled)
            {
                continue;
            }

            CharacterController targetCharacterController = playerController.GetCharacterController();
            if (targetCharacterController == null)
            {
                continue;
            }

            bool shouldIgnore = playerController.IsInjuredOrHitReacting();
            bool isAlreadyIgnored = _ignoredInjuredTargets.Contains(targetCharacterController);

            if (shouldIgnore && !isAlreadyIgnored)
            {
                for (int colliderIndex = 0; colliderIndex < _nextbotColliders.Length; colliderIndex++)
                {
                    Collider nextbotCollider = _nextbotColliders[colliderIndex];
                    if (nextbotCollider == null)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(nextbotCollider, targetCharacterController, true);
                }

                _ignoredInjuredTargets.Add(targetCharacterController);
            }
        }

        if (_ignoredInjuredTargets.Count == 0)
        {
            return;
        }

        _ignoredTargetsToRestore.Clear();

        foreach (CharacterController ignoredController in _ignoredInjuredTargets)
        {
            if (ignoredController == null)
            {
                _ignoredTargetsToRestore.Add(ignoredController);
                continue;
            }

            PlayerController playerController = ignoredController.GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = ignoredController.GetComponentInParent<PlayerController>();
            }

            if (playerController != null && playerController.IsInjuredOrHitReacting())
            {
                continue;
            }

            for (int i = 0; i < _nextbotColliders.Length; i++)
            {
                Collider nextbotCollider = _nextbotColliders[i];
                if (nextbotCollider == null || ignoredController == null)
                {
                    continue;
                }

                Physics.IgnoreCollision(nextbotCollider, ignoredController, false);
            }

            _ignoredTargetsToRestore.Add(ignoredController);
        }

        for (int i = 0; i < _ignoredTargetsToRestore.Count; i++)
        {
            _ignoredInjuredTargets.Remove(_ignoredTargetsToRestore[i]);
        }
    }

    private bool UpdateTargetFacingRotation()
    {
        Transform facingTarget = _target;
        if (facingTarget == null)
        {
            return false;
        }

        Vector3 flattenedDirection = facingTarget.position - transform.position;
        flattenedDirection.y = 0f;

        if (flattenedDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Quaternion targetRotation = Quaternion.LookRotation(-flattenedDirection.normalized, Vector3.up)
            * Quaternion.Euler(_billboardRotationOffsetEuler);
        float rotationBlend = 1f - Mathf.Exp(-_rotationSpeed * Time.deltaTime);
        ApplyVisualRotation(targetRotation, rotationBlend);
        return true;
    }

    private void UpdateBillboardRotation()
    {
        Camera targetCamera = ResolveTargetCamera();
        if (targetCamera == null)
        {
            return;
        }

        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 flattenedDirection = cameraPosition - transform.position;
        flattenedDirection.y = 0f;

        if (flattenedDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(-flattenedDirection.normalized, Vector3.up)
            * Quaternion.Euler(_billboardRotationOffsetEuler);
        float rotationBlend = 1f - Mathf.Exp(-_rotationSpeed * Time.deltaTime);
        ApplyVisualRotation(targetRotation, rotationBlend);
    }

    private void ApplyVisualRotation(Quaternion targetRotation, float rotationBlend)
    {
        Transform rotationTarget = _visualTransform != null ? _visualTransform : transform;
        rotationTarget.rotation = Quaternion.Slerp(rotationTarget.rotation, targetRotation, rotationBlend);
    }

    private void EnsureVisualBillboardChild()
    {
        MeshRenderer rootMeshRenderer = GetComponent<MeshRenderer>();
        MeshFilter rootMeshFilter = GetComponent<MeshFilter>();
        _rootMeshRenderer = rootMeshRenderer;

        if (rootMeshRenderer == null || rootMeshFilter == null || rootMeshFilter.sharedMesh == null)
        {
            _visualTransform = null;
            return;
        }

        Transform existingVisual = transform.Find("NextbotVisual");
        if (existingVisual != null)
        {
            _visualTransform = existingVisual;
            _visualTransform.localPosition = GetVisualLocalPosition(rootMeshFilter.sharedMesh);
            _visualMeshRenderer = existingVisual.GetComponent<MeshRenderer>();
            if (_defaultBaseMap == null && _visualMeshRenderer != null && _visualMeshRenderer.sharedMaterial != null)
            {
                _defaultBaseMap = _visualMeshRenderer.sharedMaterial.HasProperty("_BaseMap")
                    ? _visualMeshRenderer.sharedMaterial.GetTexture("_BaseMap")
                    : _visualMeshRenderer.sharedMaterial.mainTexture;
                _defaultBaseColor = _visualMeshRenderer.sharedMaterial.HasProperty("_BaseColor")
                    ? _visualMeshRenderer.sharedMaterial.GetColor("_BaseColor")
                    : (_visualMeshRenderer.sharedMaterial.HasProperty("_Color")
                        ? _visualMeshRenderer.sharedMaterial.GetColor("_Color")
                        : Color.white);
            }
            ConfigureVisualRendererMaterial(_visualMeshRenderer, rootMeshRenderer.sharedMaterial);
            rootMeshRenderer.enabled = false;
            return;
        }

        GameObject visualObject = new GameObject("NextbotVisual");
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = GetVisualLocalPosition(rootMeshFilter.sharedMesh);
        visualObject.transform.localRotation = Quaternion.identity;
        visualObject.transform.localScale = Vector3.one;

        MeshFilter visualMeshFilter = visualObject.AddComponent<MeshFilter>();
        visualMeshFilter.sharedMesh = rootMeshFilter.sharedMesh;

        MeshRenderer visualMeshRenderer = visualObject.AddComponent<MeshRenderer>();
        visualMeshRenderer.sharedMaterials = rootMeshRenderer.sharedMaterials;
        visualMeshRenderer.shadowCastingMode = rootMeshRenderer.shadowCastingMode;
        visualMeshRenderer.receiveShadows = rootMeshRenderer.receiveShadows;
        visualMeshRenderer.lightProbeUsage = rootMeshRenderer.lightProbeUsage;
        visualMeshRenderer.reflectionProbeUsage = rootMeshRenderer.reflectionProbeUsage;
        _visualMeshRenderer = visualMeshRenderer;

        if (_defaultBaseMap == null && visualMeshRenderer.sharedMaterial != null)
        {
            _defaultBaseMap = visualMeshRenderer.sharedMaterial.HasProperty("_BaseMap")
                ? visualMeshRenderer.sharedMaterial.GetTexture("_BaseMap")
                : visualMeshRenderer.sharedMaterial.mainTexture;
            _defaultBaseColor = visualMeshRenderer.sharedMaterial.HasProperty("_BaseColor")
                ? visualMeshRenderer.sharedMaterial.GetColor("_BaseColor")
                : (visualMeshRenderer.sharedMaterial.HasProperty("_Color")
                    ? visualMeshRenderer.sharedMaterial.GetColor("_Color")
                    : Color.white);
        }

        ConfigureVisualRendererMaterial(visualMeshRenderer, rootMeshRenderer.sharedMaterial);

        rootMeshRenderer.enabled = false;
        _visualTransform = visualObject.transform;
    }

    private void ConfigureVisualRendererMaterial(MeshRenderer targetRenderer, Material sourceMaterial)
    {
        if (targetRenderer == null)
        {
            return;
        }

        Shader preferredShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (preferredShader == null)
        {
            preferredShader = Shader.Find("Unlit/Texture");
        }

        if (preferredShader == null)
        {
            return;
        }

        if (_visualRuntimeMaterial == null || _visualRuntimeMaterial.shader != preferredShader)
        {
            _visualRuntimeMaterial = new Material(preferredShader)
            {
                name = "NextbotVisualRuntimeMaterial"
            };
        }

        Texture baseTexture = _defaultBaseMap;
        Color baseColor = _defaultBaseColor;
        if (sourceMaterial != null)
        {
            if (baseTexture == null)
            {
                baseTexture = sourceMaterial.HasProperty("_BaseMap")
                    ? sourceMaterial.GetTexture("_BaseMap")
                    : sourceMaterial.mainTexture;
            }

            if (baseColor == Color.white)
            {
                baseColor = sourceMaterial.HasProperty("_BaseColor")
                    ? sourceMaterial.GetColor("_BaseColor")
                    : (sourceMaterial.HasProperty("_Color")
                        ? sourceMaterial.GetColor("_Color")
                        : Color.white);
            }
        }

        if (_visualRuntimeMaterial.HasProperty("_BaseMap"))
        {
            _visualRuntimeMaterial.SetTexture("_BaseMap", baseTexture);
        }

        if (_visualRuntimeMaterial.HasProperty("_MainTex"))
        {
            _visualRuntimeMaterial.SetTexture("_MainTex", baseTexture);
        }

        if (_visualRuntimeMaterial.HasProperty("_BaseColor"))
        {
            _visualRuntimeMaterial.SetColor("_BaseColor", baseColor);
        }

        if (_visualRuntimeMaterial.HasProperty("_Color"))
        {
            _visualRuntimeMaterial.SetColor("_Color", baseColor);
        }

        targetRenderer.sharedMaterial = _visualRuntimeMaterial;
        targetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        targetRenderer.receiveShadows = false;
    }

    private Vector3 GetVisualLocalPosition(Mesh mesh)
    {
        if (mesh == null)
        {
            return _visualLocalOffset;
        }

        Bounds bounds = mesh.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        Quaternion baseRotation = Quaternion.Euler(_billboardRotationOffsetEuler);
        float lowestY = float.PositiveInfinity;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 rotatedCorner = baseRotation * corner;
                    if (rotatedCorner.y < lowestY)
                    {
                        lowestY = rotatedCorner.y;
                    }
                }
            }
        }

        float liftFromPivot = -lowestY + _visualGroundPadding;
        return new Vector3(
            _visualLocalOffset.x,
            liftFromPivot + _visualLocalOffset.y,
            _visualLocalOffset.z);
    }

    private void EnsureCombatHitbox()
    {
        if (!_autoCreateCombatHitbox)
        {
            return;
        }

        Transform hitboxTransform = transform.Find("NextbotCombatHitbox");
        if (hitboxTransform == null)
        {
            GameObject hitboxObject = new GameObject("NextbotCombatHitbox");
            hitboxObject.transform.SetParent(transform, false);
            hitboxObject.layer = gameObject.layer;
            hitboxTransform = hitboxObject.transform;
        }

        hitboxTransform.localRotation = Quaternion.identity;
        hitboxTransform.localScale = Vector3.one;

        if (!hitboxTransform.TryGetComponent(out _combatHitboxCollider))
        {
            _combatHitboxCollider = hitboxTransform.gameObject.AddComponent<CapsuleCollider>();
        }

        Bounds visualBounds = default;
        bool hasVisualBounds = _visualMeshRenderer != null && _visualMeshRenderer.enabled;
        if (hasVisualBounds)
        {
            visualBounds = _visualMeshRenderer.bounds;
        }

        Vector3 localCenter = hasVisualBounds
            ? transform.InverseTransformPoint(visualBounds.center)
            : (_visualTransform != null ? _visualTransform.localPosition : Vector3.up * (_combatHitboxFallbackHeight * 0.5f));
        Vector3 lossyScale = transform.lossyScale;
        float scaleX = Mathf.Max(0.001f, Mathf.Abs(lossyScale.x));
        float scaleY = Mathf.Max(0.001f, Mathf.Abs(lossyScale.y));
        float scaleZ = Mathf.Max(0.001f, Mathf.Abs(lossyScale.z));
        float localHeight = hasVisualBounds
            ? Mathf.Max(_combatHitboxFallbackHeight, visualBounds.size.y / scaleY)
            : _combatHitboxFallbackHeight;
        float localRadius = hasVisualBounds
            ? Mathf.Max(_combatHitboxFallbackRadius, Mathf.Max(visualBounds.size.x / scaleX, visualBounds.size.z / scaleZ) * 0.45f)
            : _combatHitboxFallbackRadius;

        hitboxTransform.localPosition = localCenter;
        _combatHitboxCollider.center = Vector3.zero;
        _combatHitboxCollider.direction = 1;
        _combatHitboxCollider.height = Mathf.Max(localHeight, localRadius * 2f);
        _combatHitboxCollider.radius = localRadius;
        _combatHitboxCollider.isTrigger = false;
        _combatHitboxCollider.enabled = true;
    }

    private Camera ResolveTargetCamera()
    {
        PlayerController[] playerControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        for (int i = 0; i < playerControllers.Length; i++)
        {
            PlayerController playerController = playerControllers[i];
            if (playerController == null || !playerController.enabled)
            {
                continue;
            }

            Camera[] playerCameras = playerController.GetComponentsInChildren<Camera>(true);
            for (int cameraIndex = 0; cameraIndex < playerCameras.Length; cameraIndex++)
            {
                Camera playerCamera = playerCameras[cameraIndex];
                if (playerCamera == null || !playerCamera.isActiveAndEnabled)
                {
                    continue;
                }

                _targetCamera = playerCamera;
                return _targetCamera;
            }
        }

        if (_targetCamera != null && _targetCamera.isActiveAndEnabled)
        {
            return _targetCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            _targetCamera = mainCamera;
            return _targetCamera;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || !camera.isActiveAndEnabled)
            {
                continue;
            }

            _targetCamera = camera;
            return _targetCamera;
        }

        return null;
    }

    private void EnsureLoopAudioSource()
    {
        if (_loopAudioSource == null)
        {
            _loopAudioSource = GetComponent<AudioSource>();
        }

        if (_loopAudioSource == null)
        {
            _loopAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (_loopClip != null)
        {
            _loopAudioSource.clip = _loopClip;
        }

        _loopAudioSource.playOnAwake = false;
        _loopAudioSource.loop = true;
        _loopAudioSource.spatialBlend = 1f;
        _loopAudioSource.rolloffMode = _soundRolloffMode;
        _loopAudioSource.minDistance = _soundMinDistance;
        _loopAudioSource.maxDistance = _soundMaxDistance;
        _loopAudioSource.volume = _loopVolume;
        _loopAudioSource.pitch = _defaultLoopPitch;
        _loopAudioSource.dopplerLevel = 0f;
    }

    private void UpdateLoopAudioState(bool isActive)
    {
        if (_loopAudioSource == null || !_playLoopWhileActive || _loopAudioSource.clip == null)
        {
            return;
        }

        if (isActive)
        {
            if (!_loopAudioSource.isPlaying)
            {
                _loopAudioSource.Play();
            }
        }
        else if (_loopAudioSource.isPlaying)
        {
            _loopAudioSource.Stop();
        }
    }

    private void SetServerVisualState(bool visible)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                if (_renderers[i] == _rootMeshRenderer)
                {
                    _renderers[i].enabled = false;
                }
                else
                {
                    _renderers[i].enabled = visible;
                }
            }
        }

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
            {
                _colliders[i].enabled = visible;

                // If we want it visible/active, ensure the GameObject itself is active
                // so the collider is actually registered in the physics world.
                if (visible && !_colliders[i].gameObject.activeSelf)
                {
                    _colliders[i].gameObject.SetActive(true);
                }
            }
        }

        UpdateLoopAudioState(visible);
    }

    private void ConfigureAgentFromCurrentPosition()
    {
        if (_navMeshAgent == null)
        {
            return;
        }

        int walkableAreaIndex = NavMesh.GetAreaFromName(_walkableAreaName);
        if (walkableAreaIndex >= 0)
        {
            _walkableAreaMask = 1 << walkableAreaIndex;
            _navMeshAgent.areaMask = _walkableAreaMask;
        }
        else
        {
            _walkableAreaMask = _navMeshAgent.areaMask;
        }

        _agentVisualOffset = _navMeshAgent.baseOffset;
    }

    private void EnsureAgentOnNavMesh()
    {
        if (_navMeshAgent == null || !_navMeshAgent.enabled || _navMeshAgent.isOnNavMesh)
        {
            return;
        }

        if (ShouldUseMapLocalNavMeshPresentation())
        {
            return;
        }

        if (!TryGetNearestNavMeshPosition(transform.position, out Vector3 navMeshPosition))
        {
            return;
        }

        _navMeshAgent.Warp(navMeshPosition);
    }

    private bool TryGetNearestNavMeshPosition(Vector3 worldPosition, out Vector3 navMeshPosition)
    {
        int areaMask = _navMeshAgent != null ? _navMeshAgent.areaMask : _walkableAreaMask;
        if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, _navMeshSnapDistance, areaMask))
        {
            navMeshPosition = hit.position;
            return true;
        }

        navMeshPosition = default;
        return false;
    }

    private bool TryGetRoomStateGroundedPosition(Vector3 worldPosition, out Vector3 groundedPosition)
    {
        if (TryResolveRoomStateGroundFromPhysics(worldPosition, out groundedPosition))
        {
            return true;
        }

        if (TryGetNearestNavMeshPosition(worldPosition, out Vector3 navMeshPosition)
            && IsAcceptableRoomStateGroundCandidate(worldPosition, navMeshPosition))
        {
            groundedPosition = navMeshPosition;
            groundedPosition.y += _groundSnapOffset;
            return true;
        }

        groundedPosition = default;
        return false;
    }

    private bool TryResolveRoomStateGroundFromPhysics(Vector3 worldPosition, out Vector3 groundedPosition)
    {
        Vector3 rayOrigin = worldPosition + Vector3.up * Mathf.Max(0.1f, _roomStateGroundProbeHeight);
        float rayDistance = Mathf.Max(0.1f, _roomStateGroundProbeHeight + _groundProbeDistance);
        int groundMask = _groundCheckLayers.value != 0 ? _groundCheckLayers.value : Physics.DefaultRaycastLayers;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            _groundHitBuffer,
            rayDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        RaycastHit bestHit = default;
        bool foundHit = false;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _groundHitBuffer[i];
            if (IsIgnoredGroundHit(hit))
            {
                continue;
            }

            if (!IsAcceptableRoomStateGroundCandidate(worldPosition, hit.point))
            {
                continue;
            }

            float score = Mathf.Abs(hit.point.y - worldPosition.y);
            if (score < bestScore)
            {
                bestHit = hit;
                bestScore = score;
                foundHit = true;
            }
        }

        if (foundHit)
        {
            groundedPosition = worldPosition;
            groundedPosition.y = bestHit.point.y + _groundSnapOffset;
            return true;
        }

        groundedPosition = default;
        return false;
    }

    private bool IsAcceptableRoomStateGroundCandidate(Vector3 worldPosition, Vector3 candidatePosition)
    {
        float verticalDelta = candidatePosition.y - worldPosition.y;
        if (verticalDelta > _roomStateGroundMaxRise || verticalDelta < -_roomStateGroundMaxDrop)
        {
            return false;
        }

        float planarDistance = Vector2.Distance(
            new Vector2(worldPosition.x, worldPosition.z),
            new Vector2(candidatePosition.x, candidatePosition.z));
        float maxPlanarSnapDistance = _navMeshAgent != null
            ? Mathf.Max(0.35f, _navMeshAgent.radius * 1.5f)
            : 0.75f;
        return planarDistance <= maxPlanarSnapDistance;
    }

    private Vector3 ConstrainRoomStateMovement(Vector3 currentPosition, Vector3 targetPosition)
    {
        Vector3 movement = new Vector3(
            targetPosition.x - currentPosition.x,
            0f,
            targetPosition.z - currentPosition.z);
        float distance = movement.magnitude;
        if (distance <= 0.0001f)
        {
            return ResolveRoomStateOverlap(currentPosition, targetPosition);
        }

        int collisionMask = _roomStateCollisionLayers.value != 0 ? _roomStateCollisionLayers.value : Physics.DefaultRaycastLayers;
        GetCollisionCapsule(currentPosition, out Vector3 capsuleStart, out Vector3 capsuleEnd, out float capsuleRadius);
        Vector3 direction = movement / distance;

        int hitCount = Physics.CapsuleCastNonAlloc(
                capsuleStart,
                capsuleEnd,
                capsuleRadius,
                direction,
                _groundHitBuffer,
                distance,
                collisionMask,
                QueryTriggerInteraction.Ignore);

        RaycastHit nearestHit = default;
        bool foundHit = false;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _groundHitBuffer[i];
            if (IsIgnoredRoomStateCollisionHit(hit))
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestHit = hit;
                nearestDistance = hit.distance;
                foundHit = true;
            }
        }

        if (foundHit)
        {
            float safeDistance = Mathf.Max(0f, nearestHit.distance - _roomStateCollisionSkinWidth);
            Vector3 constrainedPlanarPosition = currentPosition + direction * safeDistance;
            Vector3 remainingPlanarMovement = new Vector3(
                targetPosition.x - constrainedPlanarPosition.x,
                0f,
                targetPosition.z - constrainedPlanarPosition.z);
            Vector3 slide = Vector3.ProjectOnPlane(remainingPlanarMovement, nearestHit.normal);
            if (slide.sqrMagnitude > 0.0001f)
            {
                float slideDistance = Mathf.Min(slide.magnitude, distance);
                Vector3 slideDirection = slide.normalized;
                GetCollisionCapsule(constrainedPlanarPosition, out Vector3 slideCapsuleStart, out Vector3 slideCapsuleEnd, out float slideCapsuleRadius);
                int slideHitCount = Physics.CapsuleCastNonAlloc(
                    slideCapsuleStart,
                    slideCapsuleEnd,
                    slideCapsuleRadius,
                    slideDirection,
                    _groundHitBuffer,
                    slideDistance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore);
                float safeSlideDistance = slideDistance;
                for (int i = 0; i < slideHitCount; i++)
                {
                    RaycastHit slideHit = _groundHitBuffer[i];
                    if (IsIgnoredRoomStateCollisionHit(slideHit))
                    {
                        continue;
                    }

                    safeSlideDistance = Mathf.Min(safeSlideDistance, Mathf.Max(0f, slideHit.distance - _roomStateCollisionSkinWidth));
                }

                constrainedPlanarPosition += slideDirection * safeSlideDistance;
            }

            Vector3 constrainedPosition = new Vector3(constrainedPlanarPosition.x, targetPosition.y, constrainedPlanarPosition.z);
            return ResolveRoomStateOverlap(currentPosition, constrainedPosition);
        }

        return ResolveRoomStateOverlap(currentPosition, targetPosition);
    }

    private bool IsIgnoredRoomStateCollisionHit(RaycastHit hit)
    {
        if (IsIgnoredGroundHit(hit))
        {
            return true;
        }

        return hit.normal.y > 0.65f;
    }

    private Vector3 ResolveRoomStateOverlap(Vector3 fallbackPosition, Vector3 candidatePosition)
    {
        if (!IsRoomStatePositionOverlapping(candidatePosition))
        {
            return candidatePosition;
        }

        return fallbackPosition;
    }

    private bool IsRoomStatePositionOverlapping(Vector3 position)
    {
        int collisionMask = _roomStateCollisionLayers.value != 0 ? _roomStateCollisionLayers.value : Physics.DefaultRaycastLayers;
        GetCollisionCapsule(position, out Vector3 capsuleStart, out Vector3 capsuleEnd, out float capsuleRadius);
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            capsuleStart,
            capsuleEnd,
            capsuleRadius,
            _roomStateOverlapBuffer,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider overlap = _roomStateOverlapBuffer[i];
            if (IsIgnoredRoomStateOverlap(overlap))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool IsIgnoredRoomStateOverlap(Collider overlap)
    {
        if (overlap == null)
        {
            return true;
        }

        Transform overlapTransform = overlap.transform;
        if (overlapTransform == null)
        {
            return true;
        }

        if (overlapTransform == transform || overlapTransform.IsChildOf(transform))
        {
            return true;
        }

        if (overlap.GetComponentInParent<NextbotFollowPlayer>() != null)
        {
            return true;
        }

        if (overlap.GetComponentInParent<PlayerController>() != null)
        {
            return true;
        }

        return false;
    }

    private void GetCollisionCapsule(Vector3 centerPosition, out Vector3 capsuleStart, out Vector3 capsuleEnd, out float capsuleRadius)
    {
        float agentRadius = _navMeshAgent != null ? _navMeshAgent.radius : 0.55f;
        float agentHeight = _navMeshAgent != null ? _navMeshAgent.height : 2f;
        capsuleRadius = Mathf.Max(0.05f, agentRadius - _roomStateCollisionSkinWidth);
        float cylinderHeight = Mathf.Max(0f, agentHeight - (capsuleRadius * 2f));
        Vector3 center = centerPosition + Vector3.up * (agentHeight * 0.5f);
        Vector3 halfCylinder = Vector3.up * (cylinderHeight * 0.5f);
        capsuleStart = center - halfCylinder;
        capsuleEnd = center + halfCylinder;
    }

    private bool TryResolveGroundedPosition(Vector3 worldPosition, out Vector3 groundedPosition)
    {
        if (TryGetNearestNavMeshPosition(worldPosition, out Vector3 navMeshPosition))
        {
            float planarDistanceToNavMesh = Vector2.Distance(
                new Vector2(worldPosition.x, worldPosition.z),
                new Vector2(navMeshPosition.x, navMeshPosition.z));
            float maxPlanarSnapDistance = _navMeshAgent != null
                ? Mathf.Max(0.35f, _navMeshAgent.radius * 1.5f)
                : 0.75f;

            if (planarDistanceToNavMesh <= maxPlanarSnapDistance)
            {
                groundedPosition = navMeshPosition;
                groundedPosition.y += _groundSnapOffset;
                return true;
            }
        }

        Vector3 rayOrigin = worldPosition + Vector3.up * Mathf.Max(0.1f, _groundProbeStartHeight);
        float rayDistance = Mathf.Max(0.1f, _groundProbeStartHeight + _groundProbeDistance);
        int groundMask = _groundCheckLayers.value != 0 ? _groundCheckLayers.value : Physics.DefaultRaycastLayers;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            _groundHitBuffer,
            rayDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        RaycastHit bestHit = default;
        bool foundHit = false;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _groundHitBuffer[i];
            if (IsIgnoredGroundHit(hit))
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestHit = hit;
                bestDistance = hit.distance;
                foundHit = true;
            }
        }

        if (foundHit)
        {
            groundedPosition = worldPosition;
            groundedPosition.y = bestHit.point.y + _groundSnapOffset;
            return true;
        }

        groundedPosition = default;
        return false;
    }

    private bool IsIgnoredGroundHit(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return true;
        }

        Transform hitTransform = hit.collider.transform;
        if (hitTransform == null)
        {
            return true;
        }

        if (hitTransform == transform || hitTransform.IsChildOf(transform))
        {
            return true;
        }

        if (hit.collider.GetComponentInParent<NextbotFollowPlayer>() != null)
        {
            return true;
        }

        if (hit.collider.GetComponentInParent<PlayerController>() != null)
        {
            return true;
        }

        return false;
    }

    // Pushes the nextbot out of any solid-obstacle colliders it is overlapping.
    // Assign the Ramp layer to _solidObstacleLayers to prevent pass-through.
    private void ApplySolidObstaclePush()
    {
        if (_solidObstacleLayers.value == 0)
        {
            return;
        }

        CharacterController cc = _characterController;
        float radius = cc != null ? Mathf.Max(0.05f, cc.radius - 0.01f) : 0.35f;
        float height = cc != null ? Mathf.Max(radius * 2f + 0.01f, cc.height) : 1.8f;
        float halfHeight = height * 0.5f - radius;
        Vector3 center = transform.position + Vector3.up * (cc != null ? cc.center.y : height * 0.5f);
        Vector3 p1 = center + Vector3.up * halfHeight;
        Vector3 p2 = center - Vector3.up * halfHeight;

        Collider[] hits = Physics.OverlapCapsule(p1, p2, radius, _solidObstacleLayers.value, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider obstacle = hits[i];
            if (obstacle == null)
            {
                continue;
            }

            // Skip self-colliders
            if (obstacle.transform == transform || obstacle.transform.IsChildOf(transform))
            {
                continue;
            }

            if (Physics.ComputePenetration(
                cc != null ? (Collider)cc : hits[i],
                transform.position,
                transform.rotation,
                obstacle,
                obstacle.transform.position,
                obstacle.transform.rotation,
                out Vector3 pushDirection,
                out float pushDistance))
            {
                transform.position += pushDirection * (pushDistance + 0.005f);
                if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.nextPosition = transform.position;
                }
            }
        }
    }

    private Vector3 SmoothGroundedPosition(Vector3 currentPosition, Vector3 candidatePosition)
    {
        if (!TryResolveGroundedPosition(candidatePosition, out Vector3 groundedCandidatePosition))
        {
            return candidatePosition;
        }

        float verticalDelta = groundedCandidatePosition.y - currentPosition.y;
        if (verticalDelta <= -_groundDropSnapDistance || Mathf.Abs(verticalDelta) >= _groundHardSnapDistance)
        {
            _groundHeightVelocity = 0f;
            return groundedCandidatePosition;
        }

        float smoothTime = verticalDelta < 0f ? _groundDropSmoothTime : _groundRiseSmoothTime;
        candidatePosition.y = Mathf.SmoothDamp(
            currentPosition.y,
            groundedCandidatePosition.y,
            ref _groundHeightVelocity,
            Mathf.Max(0.0001f, smoothTime),
            Mathf.Infinity,
            Time.deltaTime);
        return candidatePosition;
    }
}
