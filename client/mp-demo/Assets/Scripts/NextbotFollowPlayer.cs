using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NextbotFollowPlayer : MonoBehaviour
{
    [Header("Networking")]
    [SerializeField] private bool _useRoomStateAuthority = true;
    [SerializeField] private string _networkNextbotId = "nextbot_0";
    [SerializeField] private float _roomStatePositionLerpSpeed = 12f;
    [SerializeField] private float _roomStateRotationLerpSpeed = 14f;
    [SerializeField] private float _roomStateSnapDistance = 1.1f;
    [SerializeField] private float _roomStateChaseResyncDistance = 12f;

    [Header("Follow")]
    [SerializeField] private float _moveSpeed = 10f;
    [SerializeField] private float _acceleration = 18f;
    [SerializeField] private float _rotationSpeed = 10f;
    [SerializeField] private float _stoppingDistance = 1.4f;
    [SerializeField] private float _targetRefreshInterval = 0.2f;
    [SerializeField] private bool _followNearestPlayer = true;

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

    [Header("Detection")]
    [SerializeField] private LayerMask _wallDetectionLayers = 1 << 6;
    [SerializeField] private float _eyeHeight = 1.25f;
    [SerializeField] private float _targetEyeHeight = 1.0f;

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
    private readonly HashSet<CharacterController> _ignoredInjuredTargets = new HashSet<CharacterController>();
    private readonly List<CharacterController> _ignoredTargetsToRestore = new List<CharacterController>();
    private Renderer[] _renderers = System.Array.Empty<Renderer>();
    private Collider[] _colliders = System.Array.Empty<Collider>();
    private Transform _visualTransform;
    private MeshRenderer _rootMeshRenderer;
    private MeshRenderer _visualMeshRenderer;
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
    private Texture _defaultBaseMap;
    private Color _defaultBaseColor = Color.white;
    private AudioClip _defaultLoopClip;
    private float _defaultLoopPitch = 1f;
    private float _defaultMoveSpeed = 10f;
    private int _walkableAreaMask = NavMesh.AllAreas;

    public string NetworkNextbotId => _networkNextbotId;

    private void Awake()
    {
        _defaultLoopClip = _loopClip;
        _defaultMoveSpeed = _moveSpeed;
        _characterController = GetComponent<CharacterController>();
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _pathBuffer = new NavMeshPath();
        _lockedHeight = transform.position.y;
        EnsureVisualBillboardChild();
        EnsureLoopAudioSource();
        _nextbotColliders = GetComponentsInChildren<Collider>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);

        if (_navMeshAgent != null)
        {
            ConfigureAgentFromCurrentPosition();
            _navMeshAgent.updateRotation = false;
            _navMeshAgent.updateUpAxis = true;
            _navMeshAgent.speed = _moveSpeed;
            _navMeshAgent.acceleration = _acceleration;
            _navMeshAgent.stoppingDistance = _stoppingDistance;
            _navMeshAgent.angularSpeed = Mathf.Max(120f, _rotationSpeed * 45f);
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
        if (_faceTargetPlayer && UpdateTargetFacingRotation())
        {
            return;
        }

        if (_billboardToCamera)
        {
            UpdateBillboardRotation();
        }
    }

    private bool UpdateActivationState()
    {
        MyRoomState roomState = GetRoomState();
        NextbotState assignedNextbotState = null;
        bool useRoomStateAuthority = _useRoomStateAuthority
            && roomState != null
            && TryGetAssignedNextbotState(roomState, out assignedNextbotState);
        SetRoomStateAuthorityActive(useRoomStateAuthority);

        if (useRoomStateAuthority)
        {
            return UpdateFromRoomState(assignedNextbotState);
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
        _networkNextbotId = nextbotId;
        _hasAppliedRoomState = false;
    }

    public void ApplyRegistryEntry(NextbotRegistryEntry entry)
    {
        EnsureVisualBillboardChild();

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

    private void SetRoomStateAuthorityActive(bool isActive)
    {
        if (_roomStateAuthorityActive == isActive)
        {
            if (isActive && _navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
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

        _navMeshAgent.updatePosition = !isActive;
        if (_navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = isActive;
            _navMeshAgent.ResetPath();
            _navMeshAgent.nextPosition = transform.position;
        }
    }

    private bool UpdateFromRoomState(NextbotState nextbotState)
    {
        bool isActive = nextbotState != null && nextbotState.isActive;
        SetServerVisualState(isActive);

        if (!isActive || nextbotState == null)
        {
            ClearTarget();
            StopAgent();
            _isJumping = false;
            _jumpVelocity = Vector3.zero;
            _isTraversingOffMeshLink = false;
            _hasAppliedRoomState = false;
            return true;
        }

        Vector3 targetPosition = ResolveGroundedRoomStatePosition(nextbotState);
        Quaternion targetRotation = Quaternion.Euler(0f, nextbotState.rotationY, 0f);

        if (TryGetServerAssignedTarget(nextbotState.targetSessionId, out Transform targetTransform, out PlayerController targetController))
        {
            AssignTarget(targetTransform, targetController);
        }
        else
        {
            ClearTarget();
        }

        StopAgent();
        _isJumping = false;
        _jumpVelocity = Vector3.zero;
        _isTraversingOffMeshLink = false;

        if (!_hasAppliedRoomState)
        {
            ApplyRoomStatePosition(targetPosition);
            transform.rotation = targetRotation;
            _hasAppliedRoomState = true;
            return true;
        }

        float positionError = Vector3.Distance(transform.position, targetPosition);
        if (positionError >= _roomStateSnapDistance)
        {
            ApplyRoomStatePosition(targetPosition);
            transform.rotation = targetRotation;
            return true;
        }

        float positionBlend = 1f - Mathf.Exp(-_roomStatePositionLerpSpeed * Time.deltaTime);
        float rotationBlend = 1f - Mathf.Exp(-_roomStateRotationLerpSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, positionBlend);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationBlend);
        if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.nextPosition = transform.position;
        }

        return true;
    }

    private Vector3 ResolveGroundedRoomStatePosition(NextbotState nextbotState)
    {
        Vector3 targetPosition = new Vector3(nextbotState.x, nextbotState.y, nextbotState.z);
        float upwardProbeDistance = Mathf.Max(1f, _navMeshSnapDistance * 0.5f);
        Vector3 elevatedProbePosition = targetPosition + Vector3.up * upwardProbeDistance;

        // Probe from above first so positions inside ramp volumes prefer the walkable ramp surface.
        if (TryGetNearestNavMeshPosition(elevatedProbePosition, out Vector3 elevatedTargetPosition)
            && elevatedTargetPosition.y >= targetPosition.y - 0.05f)
        {
            return elevatedTargetPosition;
        }

        if (TryGetNearestNavMeshPosition(targetPosition, out Vector3 groundedTargetPosition))
        {
            return groundedTargetPosition;
        }

        return targetPosition;
    }

    private void ApplyRoomStatePosition(Vector3 targetPosition)
    {
        transform.position = targetPosition;

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

    private void RefreshTargetIfNeeded()
    {
        if (_roomStateAuthorityActive)
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

        float pathDistance = GetPathDistance(candidateTransform.position);
        if (float.IsInfinity(pathDistance) || pathDistance > _maxChaseRange)
        {
            return null;
        }

        bool hasLineOfSight = HasLineOfSight(candidateTransform);
        float distanceScore = Mathf.Max(0f, _distanceScoreBase - pathDistance);
        float visibleBonus = hasLineOfSight && pathDistance <= _visibleRange ? _visibleBonus : 0f;
        float frontBonus = IsTargetInFront(candidateTransform.position) ? _frontBonus : 0f;
        float currentTargetBonus = isCurrentTarget ? _currentTargetBonus : 0f;

        return new ScoredTarget
        {
            Transform = candidateTransform,
            Controller = candidateController,
            PathDistance = pathDistance,
            HasLineOfSight = hasLineOfSight,
            Score = distanceScore + visibleBonus + frontBonus + currentTargetBonus,
        };
    }

    private bool CanKeepCurrentTarget()
    {
        if (_target == null || _targetController == null || !_targetController.enabled || _targetController.IsInjuredOrHitReacting())
        {
            return false;
        }

        float pathDistance = GetPathDistance(_target.position);
        return !float.IsInfinity(pathDistance) && pathDistance <= _maxChaseRange;
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
        _targetLockedUntil = Time.time + _targetLockDuration;
        _pendingSwitchTarget = null;
        _pendingSwitchStartedAt = 0f;
    }

    private void ClearTarget()
    {
        _target = null;
        _targetController = null;
        _targetLockedUntil = 0f;
        _pendingSwitchTarget = null;
        _pendingSwitchStartedAt = 0f;
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
            StopAgent();
            return;
        }

        Vector3 targetPosition = _target.position;

        if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
        {
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
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, velocity, _acceleration * Time.deltaTime);

            UpdateBodyRotation(_horizontalVelocity);
            TryHitTarget(Vector3.Distance(new Vector3(targetPosition.x, 0f, targetPosition.z), new Vector3(transform.position.x, 0f, transform.position.z)));
            return;
        }

        if (_lockToStartingHeight)
        {
            targetPosition.y = _lockedHeight;
        }

        Vector3 planarOffset = targetPosition - transform.position;
        planarOffset.y = 0f;
        float distance = planarOffset.magnitude;
        Vector3 moveDirection = distance > 0.001f ? planarOffset / distance : Vector3.zero;
        Vector3 desiredVelocity = distance > _stoppingDistance ? moveDirection * _moveSpeed : Vector3.zero;
        _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desiredVelocity, _acceleration * Time.deltaTime);

        ApplyFallbackMovement(_horizontalVelocity);
        TryHitTarget(distance);
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

        if (Mathf.Abs(transform.position.y - navMeshPosition.y) > _landingSnapDistance)
        {
            return;
        }

        if (_navMeshAgent != null && _navMeshAgent.enabled)
        {
            _navMeshAgent.Warp(navMeshPosition);
            _navMeshAgent.isStopped = false;
        }
        else
        {
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

        _isTraversingOffMeshLink = false;
        _horizontalVelocity = Vector3.zero;
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

            if (_lockToStartingHeight)
            {
                Vector3 position = transform.position;
                position.y = _lockedHeight;
                transform.position = position;
                _verticalVelocity = 0f;
            }

            return;
        }

        Vector3 nextPosition = transform.position + horizontalVelocity * Time.deltaTime;
        if (_lockToStartingHeight)
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
        if (_target == null || Time.time < _nextHitTime || distanceToTarget > _hitDistance)
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

        float targetYaw = Mathf.Atan2(flattenedDirection.x, flattenedDirection.z) * Mathf.Rad2Deg;
        Vector3 targetEuler = new Vector3(
            _billboardRotationOffsetEuler.x,
            targetYaw,
            _billboardRotationOffsetEuler.z);
        Quaternion targetRotation = Quaternion.Euler(targetEuler);
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

        rootMeshRenderer.enabled = false;
        _visualTransform = visualObject.transform;
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
}
