using UnityEngine;
using System.Collections.Generic;

public class NextbotFollowPlayer : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private float _moveSpeed = 8f;
    [SerializeField] private float _acceleration = 18f;
    [SerializeField] private float _rotationSpeed = 14f;
    [SerializeField] private float _stoppingDistance = 1.4f;
    [SerializeField] private float _targetRefreshInterval = 0.25f;
    [SerializeField] private bool _followNearestPlayer = true;

    [Header("Grounding")]
    [SerializeField] private bool _lockToStartingHeight = true;
    [SerializeField] private float _gravity = 20f;

    [Header("Hit")]
    [SerializeField] private float _hitDistance = 1.6f;
    [SerializeField] private float _hitCooldown = 1.25f;

    [Header("Billboard")]
    [SerializeField] private bool _faceTargetPlayer = false;
    [SerializeField] private bool _billboardToCamera = true;
    [SerializeField] private Vector3 _billboardRotationOffsetEuler = new Vector3(0f, 90f, -90f);

    private CharacterController _characterController;
    private Transform _target;
    private Vector3 _horizontalVelocity;
    private float _verticalVelocity;
    private float _lockedHeight;
    private float _nextTargetRefreshTime;
    private float _nextHitTime;
    private Camera _targetCamera;
    private Collider[] _nextbotColliders = System.Array.Empty<Collider>();
    private readonly HashSet<CharacterController> _ignoredInjuredTargets = new HashSet<CharacterController>();
    private Renderer[] _renderers = System.Array.Empty<Renderer>();
    private Collider[] _colliders = System.Array.Empty<Collider>();
    private bool _hasAppliedServerState;
    private Transform _visualTransform;
    private MeshRenderer _rootMeshRenderer;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _lockedHeight = transform.position.y;
        EnsureVisualBillboardChild();
        _nextbotColliders = GetComponentsInChildren<Collider>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
    }

    private void Update()
    {
        if (UpdateFromServerState())
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

    private void RefreshTargetIfNeeded()
    {
        if (!_followNearestPlayer)
        {
            return;
        }

        if (_target != null)
        {
            PlayerController currentTargetController = ResolvePlayerController(_target);
            if (currentTargetController == null || currentTargetController.IsInjuredOrHitReacting())
            {
                _target = null;
            }
        }

        if (Time.time < _nextTargetRefreshTime && _target != null)
        {
            return;
        }

        _nextTargetRefreshTime = Time.time + Mathf.Max(0.05f, _targetRefreshInterval);
        _target = FindNearestPlayer();
    }

    private bool UpdateFromServerState()
    {
        NextbotState nextbotState = GetNextbotState();
        if (nextbotState == null)
        {
            _hasAppliedServerState = false;
            SetServerVisualState(true);
            return false;
        }

        bool isActive = nextbotState.isActive;
        SetServerVisualState(isActive);

        if (!isActive)
        {
            _hasAppliedServerState = false;
            _horizontalVelocity = Vector3.zero;
            _target = null;
            return true;
        }

        _target = ResolveServerTargetTransform(nextbotState.targetSessionId);

        Vector3 targetPosition = new Vector3(nextbotState.x, nextbotState.y, nextbotState.z);
        if (_lockToStartingHeight)
        {
            targetPosition.y = _lockedHeight;
        }

        if (!_hasAppliedServerState)
        {
            transform.position = targetPosition;
            _hasAppliedServerState = true;
        }
        else
        {
            float moveBlend = 1f - Mathf.Exp(-_acceleration * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, moveBlend);
        }

        return true;
    }

    private NextbotState GetNextbotState()
    {
        NetworkManager networkManager = NetworkManager.Instance;
        if (networkManager == null || networkManager.Room == null || networkManager.Room.State == null)
        {
            return null;
        }

        return networkManager.Room.State.nextbot;
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
    }

    private void UpdateMovement()
    {
        if (_target == null)
        {
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, Vector3.zero, _acceleration * Time.deltaTime);
            ApplyMovement(Vector3.zero);
            return;
        }

        Vector3 targetPosition = _target.position;
        Vector3 planarOffset = targetPosition - transform.position;
        planarOffset.y = 0f;

        float distance = planarOffset.magnitude;
        Vector3 moveDirection = distance > 0.001f ? planarOffset / distance : Vector3.zero;
        Vector3 desiredVelocity = distance > _stoppingDistance ? moveDirection * _moveSpeed : Vector3.zero;

        _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desiredVelocity, _acceleration * Time.deltaTime);

        ApplyMovement(_horizontalVelocity);
        TryHitTarget(distance);
    }

    private void ApplyMovement(Vector3 horizontalVelocity)
    {
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

    private Transform FindNearestPlayer()
    {
        Transform nearestPlayer = null;
        float nearestDistanceSqr = float.PositiveInfinity;

        NetworkPlayer[] networkPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        for (int i = 0; i < networkPlayers.Length; i++)
        {
            NetworkPlayer networkPlayer = networkPlayers[i];
            if (networkPlayer == null || networkPlayer.transform == transform)
            {
                continue;
            }

            PlayerController networkPlayerController = ResolvePlayerController(networkPlayer.transform);
            if (networkPlayerController != null && (!networkPlayerController.enabled || networkPlayerController.IsInjuredOrHitReacting()))
            {
                continue;
            }

            float distanceSqr = GetPlanarDistanceSqr(networkPlayer.transform.position, transform.position);
            if (distanceSqr >= nearestDistanceSqr)
            {
                continue;
            }

            nearestDistanceSqr = distanceSqr;
            nearestPlayer = networkPlayer.transform;
        }

        if (nearestPlayer != null)
        {
            return nearestPlayer;
        }

        PlayerController[] playerControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        for (int i = 0; i < playerControllers.Length; i++)
        {
            PlayerController playerController = playerControllers[i];
            if (playerController == null || !playerController.enabled || playerController.transform == transform || playerController.IsInjuredOrHitReacting())
            {
                continue;
            }

            float distanceSqr = GetPlanarDistanceSqr(playerController.transform.position, transform.position);
            if (distanceSqr >= nearestDistanceSqr)
            {
                continue;
            }

            nearestDistanceSqr = distanceSqr;
            nearestPlayer = playerController.transform;
        }

        return nearestPlayer;
    }

    private void TryHitTarget(float distanceToTarget)
    {
        if (_target == null || Time.time < _nextHitTime || distanceToTarget > _hitDistance)
        {
            return;
        }

        PlayerController targetController = ResolvePlayerController(_target);

        if (targetController == null || !targetController.enabled || targetController.IsInjuredOrHitReacting())
        {
            return;
        }

        if (targetController.TriggerNextbotHit(transform.position))
        {
            IgnoreCollisionWithPlayer(targetController);
            _target = null;
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

    private static float GetPlanarDistanceSqr(Vector3 a, Vector3 b)
    {
        float deltaX = a.x - b.x;
        float deltaZ = a.z - b.z;
        return deltaX * deltaX + deltaZ * deltaZ;
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
            rootMeshRenderer.enabled = false;
            return;
        }

        GameObject visualObject = new GameObject("NextbotVisual");
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = Vector3.zero;
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

    private Transform ResolveServerTargetTransform(string targetSessionId)
    {
        if (string.IsNullOrEmpty(targetSessionId))
        {
            return null;
        }

        NetworkPlayer[] networkPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        for (int i = 0; i < networkPlayers.Length; i++)
        {
            NetworkPlayer networkPlayer = networkPlayers[i];
            if (networkPlayer == null)
            {
                continue;
            }

            if (!networkPlayer.TryGetSessionId(out string sessionId))
            {
                continue;
            }

            if (sessionId == targetSessionId)
            {
                return networkPlayer.transform;
            }
        }

        return null;
    }
}
