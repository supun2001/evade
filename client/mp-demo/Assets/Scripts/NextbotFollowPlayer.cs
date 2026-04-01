using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NextbotFollowPlayer : MonoBehaviour
{
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

    [Header("Grounding")]
    [SerializeField] private bool _lockToStartingHeight = true;
    [SerializeField] private float _gravity = 20f;

    [Header("Hit")]
    [SerializeField] private float _hitDistance = 1.6f;
    [SerializeField] private float _hitCooldown = 1.25f;

    [Header("Billboard")]
    [SerializeField] private bool _faceTargetPlayer = false;
    [SerializeField] private bool _billboardToCamera = true;
    [SerializeField] private Vector3 _visualLocalOffset = new Vector3(0f, 2.1f, 0f);
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
    private NavMeshPath _pathBuffer;
    private float _targetLockedUntil;
    private Transform _pendingSwitchTarget;
    private float _pendingSwitchStartedAt;
    private float _agentVisualOffset;

    private void Awake()
    {
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
        bool isActive = roomState == null || roomState.isGameStarted;
        SetServerVisualState(isActive);

        if (!isActive)
        {
            ClearTarget();
            StopAgent();
            _horizontalVelocity = Vector3.zero;
            return true;
        }

        EnsureAgentOnNavMesh();

        return false;
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

    private void RefreshTargetIfNeeded()
    {
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
            return float.PositiveInfinity;
        }

        if (_pathBuffer.status != NavMeshPathStatus.PathComplete || _pathBuffer.corners.Length < 2)
        {
            return float.PositiveInfinity;
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

            if (TryGetNearestNavMeshPosition(targetPosition, out Vector3 navMeshTargetPosition))
            {
                _navMeshAgent.SetDestination(navMeshTargetPosition);
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
            _visualTransform.localPosition = _visualLocalOffset;
            rootMeshRenderer.enabled = false;
            return;
        }

        GameObject visualObject = new GameObject("NextbotVisual");
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = _visualLocalOffset;
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

        rootMeshRenderer.enabled = false;
        _visualTransform = visualObject.transform;
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
        int areaMask = _navMeshAgent != null ? _navMeshAgent.areaMask : NavMesh.AllAreas;
        if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, _navMeshSnapDistance, areaMask))
        {
            navMeshPosition = hit.position;
            return true;
        }

        navMeshPosition = default;
        return false;
    }
}
