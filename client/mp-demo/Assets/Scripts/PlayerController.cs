using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using Unity.Cinemachine;
using PlayerCharacterController;
using System;
using System.Collections;

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
    [SerializeField] private float _firstPersonLookUpLimit = 80f;
    [SerializeField] private float _firstPersonLookDownLimit = 25f;

    [Header("Zoom Settings")]
    [SerializeField] private float _zoomFieldOfView = 35f;
    [SerializeField] private float _zoomSmoothSpeed = 10f;

    [Header("Sprint Arms")]
    [SerializeField] private string _leftArmBoneName = "arm-left";
    [SerializeField] private string _rightArmBoneName = "arm-right";
    [SerializeField] private Vector3 _leftArmSprintRotation = new Vector3(18f, -12f, 16f);
    [SerializeField] private Vector3 _rightArmSprintRotation = new Vector3(18f, 12f, -16f);
    [SerializeField] private float _sprintArmBlendSpeed = 10f;

    [Header("Sprint Camera Feel")]
    [SerializeField] private float _sprintFovBonus = 8f;
    [SerializeField] private float _sprintFovBlendSpeed = 8f;
    [SerializeField] private float _sprintBobAmplitude = 0.06f;
    [SerializeField] private float _sprintBobFrequency = 9f;
    [SerializeField] private float _sprintBobBlendSpeed = 10f;

    [Header("View Toggle")]
    [SerializeField] private CameraViewMode _startingViewMode = CameraViewMode.ThirdPerson;
    [SerializeField] private Vector3 _firstPersonCameraLocalPosition = Vector3.zero;
    [SerializeField] private float _firstPersonNearClipPlane = 0.01f;
    [SerializeField] private float _cameraTransitionDuration = 0.3f;
    [SerializeField] private string[] _firstPersonHiddenBoneNames = { "head", "torso" };

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
    private float _defaultFieldOfView;
    private float _sprintArmWeight;
    private float _sprintFovWeight;
    private float _sprintBobWeight;
    private float _sprintBobTime;
    private Vector3 _thirdPersonCameraLocalPosition;
    private Quaternion _thirdPersonCameraLocalRotation;
    private Renderer[] _localRenderers;
    private ShadowCastingMode[] _defaultShadowCastingModes;
    private Renderer[] _firstPersonHiddenRenderers;
    private bool[] _defaultRendererEnabledStates;
    private bool[] _defaultHiddenRendererEnabledStates;
    private Transform _leftArmTransform;
    private Transform _rightArmTransform;
    private Quaternion _lastLeftArmSprintOffset = Quaternion.identity;
    private Quaternion _lastRightArmSprintOffset = Quaternion.identity;
    private Coroutine _cameraTransitionCoroutine;
    private const float HIDE_HEAD_PROGRESS = 0.85f;
    private const float SHOW_HEAD_PROGRESS = 0.2f;

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
        _defaultFieldOfView = _gameplayCamera != null ? _gameplayCamera.fieldOfView : 60f;
        CacheThirdPersonCameraSettings();
        CacheLocalRenderers();
        CacheArmTransforms();
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
        UpdateZoom();
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
        float minPitch = _currentViewMode == CameraViewMode.FirstPerson ? -_firstPersonLookUpLimit : -lookLimitV;
        float maxPitch = _currentViewMode == CameraViewMode.FirstPerson ? _firstPersonLookDownLimit : lookLimitV;
        
        _cameraRotation.x += lookSenseH * lookInput.x;
        _cameraRotation.y = Mathf.Clamp(_cameraRotation.y + lookSenseV * lookInput.y, minPitch, maxPitch);
        
        _playerRotationY += lookSenseH * lookInput.x;
        _transform.rotation = Quaternion.Euler(0f, _playerRotationY, 0f);

        _cameraTransform.localRotation = Quaternion.Euler(_cameraRotation.y, 0f, 0f);
        UpdateSprintCameraBob();
        UpdateSprintArmPose();
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
        _defaultRendererEnabledStates = new bool[_localRenderers.Length];

        for (int i = 0; i < _localRenderers.Length; i++)
        {
            _defaultShadowCastingModes[i] = _localRenderers[i].shadowCastingMode;
            _defaultRendererEnabledStates[i] = _localRenderers[i].enabled;
        }

        CacheFirstPersonHiddenRenderers();
    }

    private void CacheFirstPersonHiddenRenderers()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        System.Collections.Generic.HashSet<Renderer> hiddenRenderers = new();

        foreach (Transform child in transforms)
        {
            if (!ShouldHideInFirstPerson(child.name))
            {
                continue;
            }

            foreach (Renderer renderer in child.GetComponents<Renderer>())
            {
                if (renderer != null)
                {
                    hiddenRenderers.Add(renderer);
                }
            }
        }

        _firstPersonHiddenRenderers = new Renderer[hiddenRenderers.Count];
        hiddenRenderers.CopyTo(_firstPersonHiddenRenderers);
        _defaultHiddenRendererEnabledStates = new bool[_firstPersonHiddenRenderers.Length];

        for (int i = 0; i < _firstPersonHiddenRenderers.Length; i++)
        {
            _defaultHiddenRendererEnabledStates[i] = _firstPersonHiddenRenderers[i] != null && _firstPersonHiddenRenderers[i].enabled;
        }
    }

    private bool ShouldHideInFirstPerson(string transformName)
    {
        if (_firstPersonHiddenBoneNames == null)
        {
            return false;
        }

        for (int i = 0; i < _firstPersonHiddenBoneNames.Length; i++)
        {
            if (string.Equals(transformName, _firstPersonHiddenBoneNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void CacheArmTransforms()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in transforms)
        {
            if (_leftArmTransform == null && string.Equals(child.name, _leftArmBoneName, StringComparison.OrdinalIgnoreCase))
            {
                _leftArmTransform = child;
            }

            if (_rightArmTransform == null && string.Equals(child.name, _rightArmBoneName, StringComparison.OrdinalIgnoreCase))
            {
                _rightArmTransform = child;
            }

            if (_leftArmTransform != null && _rightArmTransform != null)
            {
                break;
            }
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

        if (_cameraTransitionCoroutine != null)
        {
            StopCoroutine(_cameraTransitionCoroutine);
            _cameraTransitionCoroutine = null;
        }

        _currentViewMode = newViewMode;

        if (force || _gameplayCameraTransform == null)
        {
            ApplyCameraViewInstant(newViewMode);
            return;
        }

        _cameraTransitionCoroutine = StartCoroutine(TransitionCameraView(newViewMode));
    }

    private void ApplyCameraViewInstant(CameraViewMode newViewMode)
    {
        bool firstPerson = newViewMode == CameraViewMode.FirstPerson;

        if (firstPerson)
        {
            SetThirdPersonCameraActive(false);

            if (_gameplayCameraTransform != null)
            {
                _gameplayCameraTransform.localPosition = _firstPersonCameraLocalPosition;
                _gameplayCameraTransform.localRotation = Quaternion.identity;
            }
        }
        else
        {
            SetThirdPersonCameraActive(true);
        }

        if (_gameplayCamera != null)
        {
            _gameplayCamera.nearClipPlane = firstPerson ? _firstPersonNearClipPlane : _defaultNearClipPlane;
        }

        SetLocalRenderMode(firstPerson);
    }

    private IEnumerator TransitionCameraView(CameraViewMode newViewMode)
    {
        bool firstPerson = newViewMode == CameraViewMode.FirstPerson;

        Transform parentTransform = _gameplayCameraTransform.parent;
        Vector3 currentWorldPosition = _gameplayCameraTransform.position;
        Quaternion currentWorldRotation = _gameplayCameraTransform.rotation;

        SetThirdPersonCameraActive(false);

        if (parentTransform != null)
        {
            _gameplayCameraTransform.localPosition = parentTransform.InverseTransformPoint(currentWorldPosition);
            _gameplayCameraTransform.localRotation = Quaternion.Inverse(parentTransform.rotation) * currentWorldRotation;
        }
        else
        {
            _gameplayCameraTransform.position = currentWorldPosition;
            _gameplayCameraTransform.rotation = currentWorldRotation;
        }

        Vector3 startLocalPosition = _gameplayCameraTransform.localPosition;
        Quaternion startLocalRotation = _gameplayCameraTransform.localRotation;

        Vector3 targetLocalPosition;
        Quaternion targetLocalRotation;

        if (firstPerson)
        {
            targetLocalPosition = _firstPersonCameraLocalPosition;
            targetLocalRotation = Quaternion.identity;

            if (_gameplayCamera != null)
            {
                _gameplayCamera.nearClipPlane = _firstPersonNearClipPlane;
            }

            SetLocalRenderMode(false);
        }
        else
        {
            if (_gameplayCamera != null)
            {
                _gameplayCamera.nearClipPlane = _defaultNearClipPlane;
            }

            SetLocalRenderMode(true);
            GetThirdPersonTargetLocalPose(out targetLocalPosition, out targetLocalRotation);
        }

        float duration = Mathf.Max(0.01f, _cameraTransitionDuration);
        float elapsed = 0f;
        bool headVisibilitySwitched = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);

            _gameplayCameraTransform.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, easedT);
            _gameplayCameraTransform.localRotation = Quaternion.Slerp(startLocalRotation, targetLocalRotation, easedT);

            if (!headVisibilitySwitched)
            {
                if (firstPerson && t >= HIDE_HEAD_PROGRESS)
                {
                    SetFirstPersonHeadHidden(true);
                    headVisibilitySwitched = true;
                }
                else if (!firstPerson && t >= SHOW_HEAD_PROGRESS)
                {
                    SetFirstPersonHeadHidden(false);
                    headVisibilitySwitched = true;
                }
            }

            yield return null;
        }

        _gameplayCameraTransform.localPosition = targetLocalPosition;
        _gameplayCameraTransform.localRotation = targetLocalRotation;
        SetFirstPersonHeadHidden(firstPerson);

        if (!firstPerson)
        {
            SetThirdPersonCameraActive(true);
        }

        _cameraTransitionCoroutine = null;
    }

    private void GetThirdPersonTargetLocalPose(out Vector3 targetLocalPosition, out Quaternion targetLocalRotation)
    {
        targetLocalPosition = _thirdPersonCameraLocalPosition;
        targetLocalRotation = _thirdPersonCameraLocalRotation;

        if (_cinemachineCamera == null || _gameplayCameraTransform == null)
        {
            return;
        }

        if (_thirdPersonFollow != null)
        {
            _thirdPersonFollow.enabled = true;
        }

        _cinemachineCamera.enabled = true;
        _cinemachineCamera.PreviousStateIsValid = false;
        _cinemachineCamera.InternalUpdateCameraState(Vector3.up, -1f);

        Vector3 targetWorldPosition = _cinemachineCamera.State.GetFinalPosition();
        Quaternion targetWorldRotation = _cinemachineCamera.State.GetFinalOrientation();
        Transform parentTransform = _gameplayCameraTransform.parent;

        if (parentTransform != null)
        {
            targetLocalPosition = parentTransform.InverseTransformPoint(targetWorldPosition);
            targetLocalRotation = Quaternion.Inverse(parentTransform.rotation) * targetWorldRotation;
        }
        else
        {
            targetLocalPosition = targetWorldPosition;
            targetLocalRotation = targetWorldRotation;
        }
    }

    private void SetThirdPersonCameraActive(bool isActive)
    {
        if (_thirdPersonFollow != null)
        {
            _thirdPersonFollow.enabled = isActive;
        }

        if (_cinemachineCamera != null)
        {
            _cinemachineCamera.enabled = isActive;
            _cinemachineCamera.PreviousStateIsValid = false;
        }

        if (_cinemachineBrain != null)
        {
            _cinemachineBrain.enabled = isActive;
        }
    }

    private void UpdateZoom()
    {
        bool zooming = IsZooming();
        bool sprinting = IsSprinting();

        float sprintTargetWeight = !zooming && sprinting ? 1f : 0f;
        float sprintBlend = 1f - Mathf.Exp(-_sprintFovBlendSpeed * Time.deltaTime);
        _sprintFovWeight = Mathf.Lerp(_sprintFovWeight, sprintTargetWeight, sprintBlend);

        float targetFieldOfView = zooming
            ? _zoomFieldOfView
            : _defaultFieldOfView + _sprintFovBonus * _sprintFovWeight;

        float zoomLerp = 1f - Mathf.Exp(-_zoomSmoothSpeed * Time.deltaTime);

        if (_gameplayCamera != null)
        {
            _gameplayCamera.fieldOfView = Mathf.Lerp(_gameplayCamera.fieldOfView, targetFieldOfView, zoomLerp);
        }

        if (_cinemachineCamera != null)
        {
            LensSettings lens = _cinemachineCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFieldOfView, zoomLerp);
            _cinemachineCamera.Lens = lens;
        }
    }

    private void UpdateSprintCameraBob()
    {
        if (_gameplayCameraTransform == null || _cameraTransitionCoroutine != null)
        {
            return;
        }

        bool sprintingInFirstPerson =
            _currentViewMode == CameraViewMode.FirstPerson
            && IsSprinting()
            && _horizontalVelocity.sqrMagnitude > 0.01f;

        float bobTargetWeight = sprintingInFirstPerson ? 1f : 0f;
        float blend = 1f - Mathf.Exp(-_sprintBobBlendSpeed * Time.deltaTime);
        _sprintBobWeight = Mathf.Lerp(_sprintBobWeight, bobTargetWeight, blend);

        if (_sprintBobWeight > 0.001f)
        {
            _sprintBobTime += Time.deltaTime * _sprintBobFrequency;
        }
        else
        {
            _sprintBobTime = 0f;
        }

        if (_currentViewMode != CameraViewMode.FirstPerson)
        {
            return;
        }

        float bobOffsetY = Mathf.Sin(_sprintBobTime) * _sprintBobAmplitude * _sprintBobWeight;
        Vector3 bobbedPosition = _firstPersonCameraLocalPosition + new Vector3(0f, bobOffsetY, 0f);
        _gameplayCameraTransform.localPosition = bobbedPosition;
    }

    private void UpdateSprintArmPose()
    {
        float targetWeight =
            _currentViewMode == CameraViewMode.FirstPerson && IsSprinting()
                ? 1f
                : 0f;

        float blend = 1f - Mathf.Exp(-_sprintArmBlendSpeed * Time.deltaTime);
        _sprintArmWeight = Mathf.Lerp(_sprintArmWeight, targetWeight, blend);

        if (_leftArmTransform != null)
        {
            Quaternion leftBaseRotation = _leftArmTransform.localRotation * Quaternion.Inverse(_lastLeftArmSprintOffset);
            Quaternion leftOffset = Quaternion.Euler(_leftArmSprintRotation * _sprintArmWeight);
            _leftArmTransform.localRotation = leftBaseRotation * leftOffset;
            _lastLeftArmSprintOffset = leftOffset;
        }

        if (_rightArmTransform != null)
        {
            Quaternion rightBaseRotation = _rightArmTransform.localRotation * Quaternion.Inverse(_lastRightArmSprintOffset);
            Quaternion rightOffset = Quaternion.Euler(_rightArmSprintRotation * _sprintArmWeight);
            _rightArmTransform.localRotation = rightBaseRotation * rightOffset;
            _lastRightArmSprintOffset = rightOffset;
        }
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

            renderer.shadowCastingMode = _defaultShadowCastingModes[i];
            renderer.enabled = _defaultRendererEnabledStates[i];
        }

        SetFirstPersonHeadHidden(firstPerson);
    }

    private void SetFirstPersonHeadHidden(bool hidden)
    {
        if (_firstPersonHiddenRenderers == null)
        {
            return;
        }

        for (int i = 0; i < _firstPersonHiddenRenderers.Length; i++)
        {
            Renderer renderer = _firstPersonHiddenRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = hidden ? false : _defaultHiddenRendererEnabledStates[i];
        }
    }
    #endregion

    #region Movement
    private void HandleHorizontalMovement() {
        Vector2 movementInput = _playerLocomotionInput.MovementInput;
        float targetSpeed = IsSprinting() ? sprintSpeed : runSpeed;
        float deltaTime = Time.deltaTime;
        
        Vector3 cameraForward = _transform.forward;
        Vector3 cameraRight = _transform.right;
        
        Vector3 cameraForwardXZ = new Vector3(cameraForward.x, 0f, cameraForward.z).normalized;
        Vector3 cameraRightXZ = new Vector3(cameraRight.x, 0f, cameraRight.z).normalized;
        
        Vector3 movementDirection = cameraForwardXZ * movementInput.y + cameraRightXZ * movementInput.x;
        float inputMagnitude = Mathf.Clamp01(movementInput.magnitude);

        if (movementDirection.sqrMagnitude < 0.001f)
        {
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, Vector3.zero, drag * deltaTime);
            _horizontalVelocity.y = 0f;

            if (_horizontalVelocity.sqrMagnitude < 0.0001f)
            {
                _horizontalVelocity = Vector3.zero;
            }

            return;
        }

        Vector3 desiredVelocity = movementDirection.normalized * (targetSpeed * inputMagnitude);
        float turnDot = _horizontalVelocity.sqrMagnitude > 0.001f
            ? Vector3.Dot(_horizontalVelocity.normalized, desiredVelocity.normalized)
            : 1f;
        float turnBoost = turnDot < 0.5f ? 1.75f : 1f;
        float accelerationStep = runAcceleration * turnBoost * deltaTime;

        _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desiredVelocity, accelerationStep);

        if (_horizontalVelocity.sqrMagnitude > targetSpeed * targetSpeed)
        {
            _horizontalVelocity = _horizontalVelocity.normalized * targetSpeed;
        }

        if (inputMagnitude <= 0.001f)
        {
            _horizontalVelocity = Vector3.zero;
        }

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

    private bool IsZooming()
    {
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
    }
}
