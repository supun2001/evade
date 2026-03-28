using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
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
    public float autoSprintDelay = 5f;
    public float drag = 0.1f;
    public float gravity = 25f;
    public float jumpForce = 1f;
    public float fullSprintJumpSpeedBonus = 10f;

    [Header("Runner Movement Feel")]
    [SerializeField] private float _turnResponsiveness = 14f;
    [SerializeField] private float _sharpTurnBoost = 2.5f;
    [SerializeField] private float _sidewaysFriction = 16f;

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
    private PlayerAnimation _playerAnimation;
    private UIDocument _playerHudDocument;
    private Label _speedLabel;
    private Label _animationDebugLabel;
    private VisualElement _pauseMenuElement;
    private Button _continueButton;
    private Button _mainMenuButton;
    private CinemachineBrain _cinemachineBrain;
    private CinemachineCamera _cinemachineCamera;
    private CinemachineThirdPersonFollow _thirdPersonFollow;
    
    private Vector2 _cameraRotation = Vector2.zero;
    private float _playerRotationY = 0f;
    private float _verticalVelocity = 0f;
    private Vector3 _horizontalVelocity = Vector3.zero;
    private float _runHeldTime = 0f;

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
    private bool _isPauseMenuOpen;
    private bool _hudEventsBound;
    private const float HIDE_HEAD_PROGRESS = 0.85f;
    private const float SHOW_HEAD_PROGRESS = 0.2f;

    private const float JUMP_VELOCITY_MULTIPLIER = 3f;
    #endregion

    #region Setup
    private void Awake() {
        _playerLocomotionInput = GetComponent<PlayerLocomotionInput>();
        _playerAnimation = GetComponent<PlayerAnimation>();
        _transform = transform;
        _cameraTransform = _playerCamera.transform;
        _thirdPersonFollow = GetComponentInChildren<CinemachineThirdPersonFollow>(true);
        _cinemachineCamera = GetComponentInChildren<CinemachineCamera>(true);
        _gameplayCamera = FindGameplayCamera();
        _gameplayCameraTransform = _gameplayCamera != null ? _gameplayCamera.transform : null;
        _playerHudDocument = GetComponentInChildren<UIDocument>(true);
        _cinemachineBrain = _gameplayCamera != null ? _gameplayCamera.GetComponent<CinemachineBrain>() : null;
        _defaultNearClipPlane = _gameplayCamera != null ? _gameplayCamera.nearClipPlane : 0.3f;
        _defaultFieldOfView = _gameplayCamera != null ? _gameplayCamera.fieldOfView : 60f;
        CacheThirdPersonCameraSettings();
        CacheLocalRenderers();
        CacheArmTransforms();
        CacheHudElements();
    }
    
    private void Start() {
        // UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        // UnityEngine.Cursor.visible = false;

        SetCameraView(_startingViewMode, true);
    }
    #endregion

    #region Update
    private void Update() {
        if (!_playerLocomotionInput.InputEnabled) return;

        HandlePauseMenuToggle();
        UpdateSpeedHud();
        UpdateAnimationDebugHud();

        HandleCursorLock();
        HandleViewToggle();
        UpdateAutoSprint();
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
        if (_isPauseMenuOpen) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Only lock if we are NOT clicking on a UI element
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (UnityEngine.Cursor.lockState == CursorLockMode.None)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }
    }

    private void HandlePauseMenuToggle()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetPauseMenuVisible(!_isPauseMenuOpen);
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

    private void UpdateAutoSprint()
    {
        bool hasMovementInput = _playerLocomotionInput.MovementInput.sqrMagnitude > 0.01f;

        if (hasMovementInput)
        {
            _runHeldTime += Time.deltaTime;
        }
        else
        {
            _runHeldTime = 0f;
        }
    }

    private void LateUpdate() {
        if (_isPauseMenuOpen)
        {
            return;
        }

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

    private void CacheHudElements()
    {
        if (_playerHudDocument == null)
        {
            return;
        }

        VisualElement root = _playerHudDocument.rootVisualElement;
        if (root == null)
        {
            return;
        }

        _speedLabel = root.Q<Label>("speed-label");
        _animationDebugLabel = root.Q<Label>("animation-debug-label");
        _pauseMenuElement = root.Q<VisualElement>("pause-menu");
        _continueButton = root.Q<Button>("continue-button");
        _mainMenuButton = root.Q<Button>("main-menu-button");

        if (!_hudEventsBound)
        {
            if (_continueButton != null)
            {
                _continueButton.clicked += OnContinueButtonClicked;
            }

            if (_mainMenuButton != null)
            {
                _mainMenuButton.clicked += OnMainMenuButtonClicked;
            }

            _hudEventsBound = true;
        }

        SetPauseMenuDisplay(_isPauseMenuOpen);
    }

    private void UpdateSpeedHud()
    {
        if (_speedLabel == null)
        {
            CacheHudElements();
            if (_speedLabel == null)
            {
                return;
            }
        }

        _speedLabel.text = $"Speed {GetHorizontalSpeed():0.0}";
    }

    private void UpdateAnimationDebugHud()
    {
        if (_animationDebugLabel == null)
        {
            CacheHudElements();
            if (_animationDebugLabel == null)
            {
                return;
            }
        }

        if (_playerAnimation == null)
        {
            _animationDebugLabel.text = "Animator: missing";
            return;
        }

        _animationDebugLabel.text = _playerAnimation.GetAnimatorDebugInfo();
    }

    private void SetPauseMenuVisible(bool visible)
    {
        if (_pauseMenuElement == null)
        {
            CacheHudElements();
        }

        _isPauseMenuOpen = visible;
        SetPauseMenuDisplay(visible);

        UnityEngine.Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        UnityEngine.Cursor.visible = visible;
    }

    private void SetPauseMenuDisplay(bool visible)
    {
        if (_pauseMenuElement != null)
        {
            _pauseMenuElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void OnContinueButtonClicked()
    {
        SetPauseMenuVisible(false);
    }

    private void OnMainMenuButtonClicked()
    {
        SetPauseMenuVisible(false);

        LobbyUI lobbyUI = FindFirstObjectByType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.LeaveRoom();
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
        float sprintProgress = GetSprintProgress();

        float sprintTargetWeight = !zooming ? sprintProgress : 0f;
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

        float sprintProgress = GetSprintProgress();
        bool sprintingInFirstPerson =
            _currentViewMode == CameraViewMode.FirstPerson
            && sprintProgress > 0.001f
            && _horizontalVelocity.sqrMagnitude > 0.01f;

        float bobTargetWeight = sprintingInFirstPerson ? sprintProgress : 0f;
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
        float sprintProgress = GetSprintProgress();
        float targetWeight =
            _currentViewMode == CameraViewMode.FirstPerson
                ? sprintProgress
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
        bool isGrounded = IsGrounded();

        float targetSpeed = GetCurrentMoveSpeed();
        bool treatAsAirborne = !isGrounded || _verticalVelocity > 0.01f;
        if (treatAsAirborne)
        {
            movementInput.x = 0f;
        }
        float maxHorizontalSpeed = treatAsAirborne ? Mathf.Max(targetSpeed, _horizontalVelocity.magnitude) : targetSpeed;
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

        Vector3 desiredVelocity = movementDirection.normalized * (maxHorizontalSpeed * inputMagnitude);
        float turnDot = _horizontalVelocity.sqrMagnitude > 0.001f
            ? Vector3.Dot(_horizontalVelocity.normalized, desiredVelocity.normalized)
            : 1f;
        float turnBoost = turnDot < 0.5f ? 1.75f : 1f;
        float accelerationStep = runAcceleration * turnBoost * deltaTime;

        _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desiredVelocity, accelerationStep);

        if (_horizontalVelocity.sqrMagnitude > 0.001f)
        {
            float sharpTurnAmount = (1f - turnDot) * 0.5f;
            float steerStrength = _turnResponsiveness * (1f + sharpTurnAmount * _sharpTurnBoost);
            float steerBlend = 1f - Mathf.Exp(-steerStrength * deltaTime);
            Vector3 steeredDirection = Vector3.Slerp(_horizontalVelocity.normalized, desiredVelocity.normalized, steerBlend).normalized;
            float steeredSpeed = Mathf.Min(_horizontalVelocity.magnitude, maxHorizontalSpeed);

            _horizontalVelocity = steeredDirection * steeredSpeed;

            Vector3 lateralVelocity = _horizontalVelocity - Vector3.Project(_horizontalVelocity, desiredVelocity.normalized);
            float lateralBlend = 1f - Mathf.Exp(-_sidewaysFriction * deltaTime);
            _horizontalVelocity -= lateralVelocity * lateralBlend;
        }

        if (_horizontalVelocity.sqrMagnitude > maxHorizontalSpeed * maxHorizontalSpeed)
        {
            _horizontalVelocity = _horizontalVelocity.normalized * maxHorizontalSpeed;
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
            if (IsSprinting() && _horizontalVelocity.sqrMagnitude > 0.001f)
            {
                Vector3 horizontalDirection = _horizontalVelocity.normalized;
                float boostedSpeed = _horizontalVelocity.magnitude + fullSprintJumpSpeedBonus;
                _horizontalVelocity = horizontalDirection * boostedSpeed;
            }

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

    public float GetHorizontalSpeed()
    {
        return _horizontalVelocity.magnitude;
    }

    private float GetCurrentMoveSpeed()
    {
        float baseSpeed = Mathf.Lerp(runSpeed, sprintSpeed, GetSprintProgress());
        return baseSpeed;
    }

    private float GetSprintProgress()
    {
        if (_playerLocomotionInput == null || _playerLocomotionInput.MovementInput.sqrMagnitude <= 0.01f)
        {
            return 0f;
        }

        if (autoSprintDelay <= 0f)
        {
            return 1f;
        }

        return Mathf.Clamp01(_runHeldTime / autoSprintDelay);
    }

    private bool IsSprinting()
    {
        return GetSprintProgress() >= 0.999f;
    }

    private bool IsZooming()
    {
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
    }
}
