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

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _lockedHeight = transform.position.y;
        _nextbotColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Update()
    {
        RefreshTargetIfNeeded();
        UpdateMovement();
    }

    private void LateUpdate()
    {
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
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationBlend);
    }

    private Camera ResolveTargetCamera()
    {
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
}
