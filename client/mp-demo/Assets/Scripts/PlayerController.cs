using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using Unity.Cinemachine;
using PlayerCharacterController;
using System;

[DefaultExecutionOrder(-1)]
public class PlayerController : MonoBehaviour
{
    private enum CameraViewMode
    {
        FirstPerson,
        ThirdPerson
    }

    #region Class Variables
    [Header("Components")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private Camera _playerCamera;

    [Header("Base Movement")]
    public float runAcceleration = 0.25f;
    public float runSpeed = 4f;
    public float sprintSpeed = 7f;
    public float drag = 0.1f;
    public float gravity = 25f;
    public float jumpForce = 1f;

    [Header("Camera Settings")]
    public float lookSenseH = 0.1f;
    public float lookSenseV = 0.1f;
    public float lookLimitV = 89f;

    [Header("View Toggle")]
    [SerializeField] private CameraViewMode _startingViewMode = CameraViewMode.ThirdPerson;
    [SerializeField] private Vector3 _firstPersonCameraLocalPosition = Vector3.zero;
    [SerializeField] private float _firstPersonNearClipPlane = 0.01f;

    private PlayerLocomotionInput _playerLocomotionInput;
    private Transform _transform;
    private Transform _cameraTransform;
    private Transform _gameplayCameraTransform;
    private Camera _gameplayCamera;
    private CinemachineBrain _cinemachineBrain;
    private CinemachineCamera _cinemachineCamera;
    private CinemachineThirdPersonFollow _thirdPersonFollow;
    
    private Vector2 _cameraRotation = Vector2.zero;
    private float _playerRotationY = 0f;
    private float _verticalVelocity = 0f;
    private Vector3 _horizontalVelocity = Vector3.zero;

    private CameraViewMode _currentViewMode;
    private float _defaultNearClipPlane;
    private Vector3 _thirdPersonCameraLocalPosition;
    private Quaternion _thirdPersonCameraLocalRotation;
    private Renderer[] _localRenderers;
    private ShadowCastingMode[] _defaultShadowCastingModes;

    private const float JUMP_VELOCITY_MULTIPLIER = 3f;
    #endregion

    #region Setup
    private void Awake() {
        _playerLocomotionInput = GetComponent<PlayerLocomotionInput>();
        _transform = transform;
        _cameraTransform = _playerCamera.transform;
        _thirdPersonFollow = GetComponentInChildren<CinemachineThirdPersonFollow>(true);
        _cinemachineCamera = GetComponentInChildren<CinemachineCamera>(true);
        _gameplayCamera = FindGameplayCamera();
        _gameplayCameraTransform = _gameplayCamera != null ? _gameplayCamera.transform : null;
        _cinemachineBrain = _gameplayCamera != null ? _gameplayCamera.GetComponent<CinemachineBrain>() : null;
        _defaultNearClipPlane = _gameplayCamera != null ? _gameplayCamera.nearClipPlane : 0.3f;
        CacheThirdPersonCameraSettings();
        CacheLocalRenderers();
    }
    
    private void Start() {
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;

        SetCameraView(_startingViewMode, true);
    }
    #endregion

    #region Update
    private void Update() {
        if (!_playerLocomotionInput.InputEnabled) return;

        HandleCursorLock();
        HandleViewToggle();
        HandleVerticalMovement();
        HandleHorizontalMovement();

        Vector3 finalVelocity = _horizontalVelocity;
        finalVelocity.y = _verticalVelocity;

        _characterController.Move(finalVelocity * Time.deltaTime);
    }
    
    private void HandleCursorLock()
    {
        if (!_playerLocomotionInput.InputEnabled) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Only lock if we are NOT clicking on a UI element
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (Cursor.lockState == CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void HandleViewToggle()
    {
        if (!_playerLocomotionInput.InputEnabled) return;

        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            CameraViewMode nextView =
                _currentViewMode == CameraViewMode.FirstPerson
                    ? CameraViewMode.ThirdPerson
                    : CameraViewMode.FirstPerson;

            SetCameraView(nextView);
        }
    }

    private void LateUpdate() {
        Vector2 lookInput = _playerLocomotionInput.LookInput;
        
        _cameraRotation.x += lookSenseH * lookInput.x;
        _cameraRotation.y = Mathf.Clamp(_cameraRotation.y + lookSenseV * lookInput.y, -lookLimitV, lookLimitV);
        
        _playerRotationY += lookSenseH * lookInput.x;
        _transform.rotation = Quaternion.Euler(0f, _playerRotationY, 0f);

        _cameraTransform.localRotation = Quaternion.Euler(_cameraRotation.y, 0f, 0f);
    }
    #endregion

    #region Camera View
    private void CacheThirdPersonCameraSettings()
    {
        if (_gameplayCameraTransform == null)
        {
            return;
        }

        _thirdPersonCameraLocalPosition = _gameplayCameraTransform.localPosition;
        _thirdPersonCameraLocalRotation = _gameplayCameraTransform.localRotation;
    }

    private void CacheLocalRenderers()
    {
        _localRenderers = GetComponentsInChildren<Renderer>(true);
        _defaultShadowCastingModes = new ShadowCastingMode[_localRenderers.Length];

        for (int i = 0; i < _localRenderers.Length; i++)
        {
            _defaultShadowCastingModes[i] = _localRenderers[i].shadowCastingMode;
        }
    }

    private Camera FindGameplayCamera()
    {
        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        foreach (Camera childCamera in cameras)
        {
            if (childCamera != _playerCamera)
            {
                return childCamera;
            }
        }

        return _playerCamera;
    }

    private void SetCameraView(CameraViewMode newViewMode, bool force = false)
    {
        if (!force && _currentViewMode == newViewMode)
        {
            return;
        }

        _currentViewMode = newViewMode;

        bool firstPerson = newViewMode == CameraViewMode.FirstPerson;

        if (_gameplayCameraTransform != null)
        {
            _gameplayCameraTransform.localPosition = firstPerson
                ? _firstPersonCameraLocalPosition
                : _thirdPersonCameraLocalPosition;
            _gameplayCameraTransform.localRotation = firstPerson
                ? Quaternion.identity
                : _thirdPersonCameraLocalRotation;
        }

        if (_cinemachineCamera != null)
        {
            _cinemachineCamera.enabled = !firstPerson;
            _cinemachineCamera.PreviousStateIsValid = false;
        }

        if (_thirdPersonFollow != null)
        {
            _thirdPersonFollow.enabled = !firstPerson;
        }

        if (_cinemachineBrain != null)
        {
            _cinemachineBrain.enabled = !firstPerson;
        }

        if (_gameplayCamera != null)
        {
            _gameplayCamera.nearClipPlane = firstPerson ? _firstPersonNearClipPlane : _defaultNearClipPlane;
        }

        SetLocalRenderMode(firstPerson);
    }

    private void SetLocalRenderMode(bool firstPerson)
    {
        if (_localRenderers == null)
        {
            return;
        }

        for (int i = 0; i < _localRenderers.Length; i++)
        {
            Renderer renderer = _localRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.shadowCastingMode = firstPerson
                ? ShadowCastingMode.ShadowsOnly
                : _defaultShadowCastingModes[i];
        }
    }
    #endregion

    #region Movement
    private void HandleHorizontalMovement() {
        Vector2 movementInput = _playerLocomotionInput.MovementInput;
        float targetSpeed = IsSprinting() ? sprintSpeed : runSpeed;
        
        if (movementInput.sqrMagnitude < 0.001f && _horizontalVelocity.sqrMagnitude < 0.001f) {
            _horizontalVelocity = Vector3.zero;
            return;
        }
        
        Vector3 cameraForward = _transform.forward;
        Vector3 cameraRight = _transform.right;
        
        Vector3 cameraForwardXZ = new Vector3(cameraForward.x, 0f, cameraForward.z).normalized;
        Vector3 cameraRightXZ = new Vector3(cameraRight.x, 0f, cameraRight.z).normalized;
        
        Vector3 movementDirection = cameraForwardXZ * movementInput.y + cameraRightXZ * movementInput.x;

        // Apply acceleration
        float deltaTime = Time.deltaTime;
        Vector3 movementDelta = movementDirection * runAcceleration * deltaTime;
        _horizontalVelocity += movementDelta;

        // Apply drag
        float dragThreshold = drag * deltaTime;
        float velocitySqrMag = _horizontalVelocity.sqrMagnitude;
        
        if (velocitySqrMag > dragThreshold * dragThreshold) {
            Vector3 currentDrag = _horizontalVelocity.normalized * dragThreshold;
            _horizontalVelocity -= currentDrag;
        } else {
            _horizontalVelocity = Vector3.zero;
        }
        
        _horizontalVelocity = Vector3.ClampMagnitude(_horizontalVelocity, targetSpeed);
        
        _horizontalVelocity.y = 0f;
    }
    #endregion

    #region Vertical Movement
    private void HandleVerticalMovement()
    {
        bool isGrounded = IsGrounded();
        float deltaTime = Time.deltaTime;
        
        if (isGrounded && _verticalVelocity < 0f){
            _verticalVelocity = 0f;
        }
        
        _verticalVelocity -= gravity * deltaTime;

        if(_playerLocomotionInput.JumpPressed && isGrounded){
            _verticalVelocity += MathF.Sqrt(jumpForce * JUMP_VELOCITY_MULTIPLIER * gravity);
        }
    }

    public bool IsGrounded() {
        return _characterController.isGrounded;   
    }
    #endregion

    public Vector3 GetVelocity()
    {
        return _horizontalVelocity + Vector3.up * _verticalVelocity;
    }

    private bool IsSprinting()
    {
        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
    }
}
