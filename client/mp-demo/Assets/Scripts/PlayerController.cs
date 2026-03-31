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
    [SerializeField] private float _injuredMoveSpeed = 1.75f;
    [SerializeField] private float _crouchMoveSpeed = 2f;
    public float autoSprintDelay = 5f;
    public float drag = 0.1f;
    public float gravity = 25f;
    public float jumpForce = 1f;
    public float fullSprintJumpSpeedBonus = 10f;

    [Header("Runner Movement Feel")]
    [SerializeField] private float _turnResponsiveness = 14f;
    [SerializeField] private float _sharpTurnBoost = 2.5f;
    [SerializeField] private float _sidewaysFriction = 16f;
    [SerializeField] private float _nonForwardSpeedMultiplier = 0.5f;
    [SerializeField] private float _injuredRotationSharpness = 12f;

    [Header("Bhop & Strafing")]
    [SerializeField] private bool _enableBunnyHop = true;
    [SerializeField] private float _groundFriction = 10f;
    [SerializeField] private float _groundControl = 8f;
    [SerializeField] private float _airAcceleration = 42f;
    [SerializeField] private float _airStrafeAccelerationMultiplier = 1.35f;
    [SerializeField] private float _airMaxSpeed = 42f;
    [SerializeField] private float _bunnyHopSpeedGain = 1.08f;
    [SerializeField] private float _bunnyHopMaxSpeed = 48f;

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
    [SerializeField] private float _firstPersonWalkBobAmplitude = 0.02f;
    [SerializeField] private float _firstPersonWalkBobFrequency = 6f;
    [SerializeField] private float _sprintBobAmplitude = 0.06f;
    [SerializeField] private float _sprintBobFrequency = 9f;
    [SerializeField] private float _sprintBobBlendSpeed = 10f;

    [Header("View Toggle")]
    [SerializeField] private CameraViewMode _startingViewMode = CameraViewMode.ThirdPerson;
    [SerializeField] private bool _useManualThirdPersonCamera = true;
    [SerializeField] private Vector3 _thirdPersonCameraOffset = new Vector3(1f, 0.55f, -3.2f);
    [SerializeField] private float _thirdPersonCameraPitch = 8f;
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
    private Transform _injuredVisualRoot;
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
    private bool _jumpedThisFrame;
    private bool _isCrouching;

    private CameraViewMode _currentViewMode;
    private float _defaultNearClipPlane;
    private float _defaultFieldOfView;
    private float _sprintArmWeight;
    private float _sprintFovWeight;
    private float _sprintBobWeight;
    private float _sprintBobTime;
    private float _firstPersonBobWeight;
    private Vector3 _cachedThirdPersonCameraLocalPosition;
    private Quaternion _cachedThirdPersonCameraLocalRotation;
    private Renderer[] _localRenderers;
    private ShadowCastingMode[] _defaultShadowCastingModes;
    private Renderer[] _firstPersonHiddenRenderers;
    private bool[] _defaultRendererEnabledStates;
    private bool[] _defaultHiddenRendererEnabledStates;
    private Transform _leftArmTransform;
    private Transform _rightArmTransform;
    private Quaternion _lastLeftArmSprintOffset = Quaternion.identity;
    private Quaternion _lastRightArmSprintOffset = Quaternion.identity;
    private Quaternion _injuredVisualRootBaseLocalRotation = Quaternion.identity;
    private Coroutine _cameraTransitionCoroutine;
    private bool _isPauseMenuOpen;
    private bool _hudEventsBound;
    private const float HIDE_HEAD_PROGRESS = 0.85f;
    private const float SHOW_HEAD_PROGRESS = 0.2f;
    private const string INJURED_VISUAL_ROOT_NAME = "player";
    private const string INJURED_VISUAL_PIVOT_NAME = "InjuredVisualPivot";

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
        CacheInjuredVisualRoot();
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

        _jumpedThisFrame = false;
        HandlePauseMenuToggle();
        EnforceInjuredCameraView();
        UpdateCrouchState();
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
        if (IsInjured()) return;

        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            CameraViewMode nextView =
                _currentViewMode == CameraViewMode.FirstPerson
                    ? CameraViewMode.ThirdPerson
                    : CameraViewMode.FirstPerson;

            SetCameraView(nextView);
        }
    }

    private void EnforceInjuredCameraView()
    {
        if (!IsInjured() || _currentViewMode == CameraViewMode.ThirdPerson)
        {
            return;
        }

        SetCameraView(CameraViewMode.ThirdPerson);
    }

    private void UpdateAutoSprint()
    {
        if (IsInjured() || IsCrouching())
        {
            _runHeldTime = 0f;
            return;
        }

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
        _cameraRotation.y = Mathf.Clamp(_cameraRotation.y - lookSenseV * lookInput.y, minPitch, maxPitch);

        _playerRotationY += lookSenseH * lookInput.x;

        _transform.rotation = Quaternion.Euler(0f, _playerRotationY, 0f);

        if (IsInjured())
        {
            UpdateInjuredFacing();
        }
        else if (IsCrouching())
        {
            UpdateCrouchFacing();
        }
        else
        {
            ResetInjuredVisualRootRotation();
        }

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

        _cachedThirdPersonCameraLocalPosition = _gameplayCameraTransform.localPosition;
        _cachedThirdPersonCameraLocalRotation = _gameplayCameraTransform.localRotation;
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

    private void CacheInjuredVisualRoot()
    {
        if (_injuredVisualRoot != null)
        {
            return;
        }

        for (int i = 0; i < _transform.childCount; i++)
        {
            Transform child = _transform.GetChild(i);
            if (string.Equals(child.name, INJURED_VISUAL_ROOT_NAME, StringComparison.OrdinalIgnoreCase))
            {
                _injuredVisualRoot = EnsureInjuredVisualPivot(child);
                _injuredVisualRootBaseLocalRotation = _injuredVisualRoot.localRotation;
                return;
            }
        }

        Transform bestChild = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < _transform.childCount; i++)
        {
            Transform child = _transform.GetChild(i);
            Animator childAnimator = child.GetComponentInChildren<Animator>(true);
            if (childAnimator == null)
            {
                continue;
            }

            int score = child.GetComponentsInChildren<Renderer>(true).Length * 10
                + child.GetComponentsInChildren<Transform>(true).Length;

            if (score > bestScore)
            {
                bestScore = score;
                bestChild = child;
            }
        }

        if (bestChild != null)
        {
            _injuredVisualRoot = EnsureInjuredVisualPivot(bestChild);
            _injuredVisualRootBaseLocalRotation = _injuredVisualRoot.localRotation;
            return;
        }

        if (_playerAnimation == null || _playerAnimation.VisualRootTransform == null)
        {
            return;
        }

        _injuredVisualRoot = EnsureInjuredVisualPivot(_playerAnimation.VisualRootTransform);
        _injuredVisualRootBaseLocalRotation = _injuredVisualRoot.localRotation;
    }

    private Transform EnsureInjuredVisualPivot(Transform visualTransform)
    {
        if (visualTransform == null)
        {
            return null;
        }

        if (visualTransform.parent == _transform && string.Equals(visualTransform.name, INJURED_VISUAL_PIVOT_NAME, StringComparison.OrdinalIgnoreCase))
        {
            return visualTransform;
        }

        if (visualTransform.parent != null
            && visualTransform.parent.parent == _transform
            && string.Equals(visualTransform.parent.name, INJURED_VISUAL_PIVOT_NAME, StringComparison.OrdinalIgnoreCase))
        {
            return visualTransform.parent;
        }

        Transform existingPivot = _transform.Find(INJURED_VISUAL_PIVOT_NAME);
        if (existingPivot == null)
        {
            GameObject pivotObject = new GameObject(INJURED_VISUAL_PIVOT_NAME);
            existingPivot = pivotObject.transform;
            existingPivot.SetParent(_transform, false);
            existingPivot.localPosition = visualTransform.localPosition;
            existingPivot.localRotation = visualTransform.localRotation;
            existingPivot.localScale = Vector3.one;
            existingPivot.SetSiblingIndex(visualTransform.GetSiblingIndex());
        }

        if (visualTransform.parent != existingPivot)
        {
            visualTransform.SetParent(existingPivot, true);
        }

        return existingPivot;
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
        if (IsInjured() && newViewMode == CameraViewMode.FirstPerson)
        {
            newViewMode = CameraViewMode.ThirdPerson;
        }

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
            SetThirdPersonCameraActive(!_useManualThirdPersonCamera);

            if (_gameplayCameraTransform != null)
            {
                GetThirdPersonTargetLocalPose(out Vector3 targetLocalPosition, out Quaternion targetLocalRotation);
                _gameplayCameraTransform.localPosition = targetLocalPosition;
                _gameplayCameraTransform.localRotation = targetLocalRotation;
            }
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
            SetThirdPersonCameraActive(!_useManualThirdPersonCamera);
        }

        _cameraTransitionCoroutine = null;
    }

    private void GetThirdPersonTargetLocalPose(out Vector3 targetLocalPosition, out Quaternion targetLocalRotation)
    {
        if (_useManualThirdPersonCamera)
        {
            targetLocalPosition = _thirdPersonCameraOffset;
            targetLocalRotation = Quaternion.Euler(_thirdPersonCameraPitch, 0f, 0f);
            return;
        }

        targetLocalPosition = _cachedThirdPersonCameraLocalPosition;
        targetLocalRotation = _cachedThirdPersonCameraLocalRotation;

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
        bool allowSprintFovKick = _currentViewMode == CameraViewMode.FirstPerson;

        float sprintTargetWeight = allowSprintFovKick && !zooming ? sprintProgress : 0f;
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

        float horizontalSpeed = GetHorizontalSpeed();
        float moveSpeedRatio = Mathf.Clamp01(horizontalSpeed / Mathf.Max(runSpeed, 0.01f));
        float sprintProgress = GetSprintProgress();
        bool movingInFirstPerson =
            _currentViewMode == CameraViewMode.FirstPerson
            && horizontalSpeed > 0.01f;

        float walkBobTargetWeight = movingInFirstPerson ? moveSpeedRatio : 0f;
        float bobTargetWeight = movingInFirstPerson ? sprintProgress : 0f;
        float blend = 1f - Mathf.Exp(-_sprintBobBlendSpeed * Time.deltaTime);
        _firstPersonBobWeight = Mathf.Lerp(_firstPersonBobWeight, walkBobTargetWeight, blend);
        _sprintBobWeight = Mathf.Lerp(_sprintBobWeight, bobTargetWeight, blend);

        if (_firstPersonBobWeight > 0.001f || _sprintBobWeight > 0.001f)
        {
            float bobFrequency = Mathf.Lerp(_firstPersonWalkBobFrequency, _sprintBobFrequency, _sprintBobWeight);
            _sprintBobTime += Time.deltaTime * bobFrequency;
        }
        else
        {
            _sprintBobTime = 0f;
        }

        if (_currentViewMode != CameraViewMode.FirstPerson)
        {
            return;
        }

        float bobAmplitude = Mathf.Lerp(_firstPersonWalkBobAmplitude, _sprintBobAmplitude, _sprintBobWeight) * _firstPersonBobWeight;
        float bobOffsetY = Mathf.Sin(_sprintBobTime) * bobAmplitude;
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
        float deltaTime = Time.deltaTime;
        bool treatAsAirborne = !isGrounded || _verticalVelocity > 0.01f;

        Vector3 cameraForward = _transform.forward;
        Vector3 cameraRight = _transform.right;
        
        Vector3 cameraForwardXZ = new Vector3(cameraForward.x, 0f, cameraForward.z).normalized;
        Vector3 cameraRightXZ = new Vector3(cameraRight.x, 0f, cameraRight.z).normalized;
        
        Vector3 movementDirection = cameraForwardXZ * movementInput.y + cameraRightXZ * movementInput.x;
        float inputMagnitude = Mathf.Clamp01(movementInput.magnitude);
        float targetSpeed = GetCurrentMoveSpeed() * GetDirectionalSpeedMultiplier(movementInput) * inputMagnitude;

        if (treatAsAirborne)
        {
            HandleAirMovement(movementInput, movementDirection, inputMagnitude, targetSpeed, deltaTime);
            return;
        }

        HandleGroundMovement(movementDirection, inputMagnitude, targetSpeed, deltaTime);
    }

    private void UpdateInjuredFacing()
    {
        UpdateDirectionalVisualFacing();
    }

    private void UpdateCrouchFacing()
    {
        UpdateDirectionalVisualFacing();
    }

    private void UpdateDirectionalVisualFacing()
    {
        CacheInjuredVisualRoot();

        if (_injuredVisualRoot == null)
        {
            return;
        }

        Vector2 movementInput = _playerLocomotionInput != null ? _playerLocomotionInput.MovementInput : Vector2.zero;
        if (movementInput.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float localYaw = Mathf.Atan2(movementInput.x, movementInput.y) * Mathf.Rad2Deg + 180f;
        float blend = 1f - Mathf.Exp(-_injuredRotationSharpness * Time.deltaTime);
        Quaternion targetLocalRotation = Quaternion.Euler(0f, localYaw, 0f) * _injuredVisualRootBaseLocalRotation;
        _injuredVisualRoot.localRotation = Quaternion.Slerp(_injuredVisualRoot.localRotation, targetLocalRotation, blend);
    }

    private void ResetInjuredVisualRootRotation()
    {
        CacheInjuredVisualRoot();

        if (_injuredVisualRoot == null)
        {
            return;
        }

        Quaternion correctedForwardRotation = Quaternion.Euler(0f, 180f, 0f) * _injuredVisualRootBaseLocalRotation;
        _injuredVisualRoot.localRotation = Quaternion.Slerp(
            _injuredVisualRoot.localRotation,
            correctedForwardRotation,
            1f - Mathf.Exp(-_injuredRotationSharpness * Time.deltaTime));
    }

    private void HandleGroundMovement(Vector3 movementDirection, float inputMagnitude, float targetSpeed, float deltaTime)
    {
        ApplyGroundFriction(deltaTime, _playerLocomotionInput.JumpPressed);

        if (movementDirection.sqrMagnitude < 0.001f)
        {
            _horizontalVelocity.y = 0f;

            if (_horizontalVelocity.sqrMagnitude < 0.0001f)
            {
                _horizontalVelocity = Vector3.zero;
            }

            return;
        }

        Vector3 desiredDirection = movementDirection.normalized;
        AccelerateHorizontal(desiredDirection, targetSpeed, runAcceleration, deltaTime);

        float currentSpeed = _horizontalVelocity.magnitude;
        if (currentSpeed > targetSpeed && targetSpeed > 0.001f)
        {
            float controlFactor = 1f - Mathf.Exp(-_groundControl * deltaTime);
            Vector3 controlledVelocity = desiredDirection * Mathf.Lerp(currentSpeed, targetSpeed, controlFactor);
            _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, controlledVelocity, controlFactor);
        }

        if (_horizontalVelocity.sqrMagnitude > 0.001f)
        {
            float turnDot = Vector3.Dot(_horizontalVelocity.normalized, desiredDirection);
            float sharpTurnAmount = (1f - turnDot) * 0.5f;
            float steerStrength = _turnResponsiveness * (1f + sharpTurnAmount * _sharpTurnBoost);
            float steerBlend = 1f - Mathf.Exp(-steerStrength * deltaTime);
            Vector3 steeredDirection = Vector3.Slerp(_horizontalVelocity.normalized, desiredDirection, steerBlend).normalized;
            _horizontalVelocity = steeredDirection * _horizontalVelocity.magnitude;

            Vector3 lateralVelocity = _horizontalVelocity - Vector3.Project(_horizontalVelocity, desiredDirection);
            float lateralBlend = 1f - Mathf.Exp(-_sidewaysFriction * deltaTime);
            _horizontalVelocity -= lateralVelocity * lateralBlend;
        }

        if (_horizontalVelocity.sqrMagnitude > targetSpeed * targetSpeed && targetSpeed > 0.001f)
        {
            _horizontalVelocity = _horizontalVelocity.normalized * targetSpeed;
        }

        _horizontalVelocity.y = 0f;
    }

    private void HandleAirMovement(Vector2 movementInput, Vector3 movementDirection, float inputMagnitude, float targetSpeed, float deltaTime)
    {
        float airSpeedLimit = Mathf.Max(_airMaxSpeed, _bunnyHopMaxSpeed + fullSprintJumpSpeedBonus);

        if (movementDirection.sqrMagnitude <= 0.001f || inputMagnitude <= 0.001f)
        {
            if (_horizontalVelocity.magnitude > airSpeedLimit)
            {
                _horizontalVelocity = _horizontalVelocity.normalized * airSpeedLimit;
            }

            _horizontalVelocity.y = 0f;
            return;
        }

        Vector3 desiredDirection = movementDirection.normalized;
        float airAcceleration = _airAcceleration;

        if (Mathf.Abs(movementInput.x) > 0.01f && movementInput.y <= 0.01f)
        {
            airAcceleration *= _airStrafeAccelerationMultiplier;
        }

        float airTargetSpeed = Mathf.Min(Mathf.Max(targetSpeed, GetCurrentMoveSpeed() * inputMagnitude), airSpeedLimit);
        AccelerateHorizontal(desiredDirection, airTargetSpeed, airAcceleration, deltaTime);

        if (_horizontalVelocity.magnitude > airSpeedLimit)
        {
            _horizontalVelocity = _horizontalVelocity.normalized * airSpeedLimit;
        }

        _horizontalVelocity.y = 0f;
    }

    private void ApplyGroundFriction(float deltaTime, bool preserveMomentumForJump)
    {
        if (preserveMomentumForJump || _horizontalVelocity.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float speed = _horizontalVelocity.magnitude;
        float drop = speed * Mathf.Max(_groundFriction, 0f) * deltaTime;
        float newSpeed = Mathf.Max(speed - drop, 0f);

        if (newSpeed <= 0.0001f)
        {
            _horizontalVelocity = Vector3.zero;
            return;
        }

        _horizontalVelocity *= newSpeed / speed;
    }

    private void AccelerateHorizontal(Vector3 desiredDirection, float targetSpeed, float acceleration, float deltaTime)
    {
        if (desiredDirection.sqrMagnitude <= 0.001f || targetSpeed <= 0.001f)
        {
            return;
        }

        float currentSpeedInDirection = Vector3.Dot(_horizontalVelocity, desiredDirection);
        float addSpeed = targetSpeed - currentSpeedInDirection;
        if (addSpeed <= 0f)
        {
            return;
        }

        float accelSpeed = acceleration * deltaTime * targetSpeed;
        if (accelSpeed > addSpeed)
        {
            accelSpeed = addSpeed;
        }

        _horizontalVelocity += desiredDirection * accelSpeed;
        _horizontalVelocity.y = 0f;
    }

    private float GetDirectionalSpeedMultiplier(Vector2 movementInput)
    {
        if (IsInjured())
        {
            return 1f;
        }

        if (movementInput.sqrMagnitude <= 0.0001f)
        {
            return 1f;
        }

        if (movementInput.y < -0.01f)
        {
            return _nonForwardSpeedMultiplier;
        }

        if (Mathf.Abs(movementInput.x) > 0.01f && movementInput.y <= 0.01f)
        {
            return _nonForwardSpeedMultiplier;
        }

        return 1f;
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

        if(!IsInjured() && !IsCrouching() && _playerLocomotionInput.JumpPressed && isGrounded){
            if (_horizontalVelocity.sqrMagnitude > 0.001f)
            {
                Vector3 horizontalDirection = _horizontalVelocity.normalized;
                float currentSpeed = _horizontalVelocity.magnitude;
                float boostedSpeed = currentSpeed;

                if (_enableBunnyHop)
                {
                    boostedSpeed = Mathf.Min(currentSpeed * _bunnyHopSpeedGain, _bunnyHopMaxSpeed);
                }

                if (IsSprinting())
                {
                    boostedSpeed += fullSprintJumpSpeedBonus;
                }

                _horizontalVelocity = horizontalDirection * boostedSpeed;
            }

            _jumpedThisFrame = true;
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

    public float GetInjuredMoveSpeed()
    {
        return _injuredMoveSpeed;
    }

    public float GetCrouchMoveSpeed()
    {
        return _crouchMoveSpeed;
    }

    public float GetVerticalVelocity()
    {
        return _verticalVelocity;
    }

    public bool DidJumpThisFrame()
    {
        return _jumpedThisFrame;
    }

    private float GetCurrentMoveSpeed()
    {
        if (IsInjured())
        {
            return _injuredMoveSpeed;
        }

        if (IsCrouching())
        {
            return _crouchMoveSpeed;
        }

        float baseSpeed = Mathf.Lerp(runSpeed, sprintSpeed, GetSprintProgress());
        return baseSpeed;
    }

    private float GetSprintProgress()
    {
        if (IsInjured())
        {
            return 0f;
        }

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

    public bool IsCrouching()
    {
        return _isCrouching;
    }

    private bool IsInjured()
    {
        return _playerAnimation != null && _playerAnimation.IsInjuredActive;
    }

    private void UpdateCrouchState()
    {
        _isCrouching =
            !IsInjured()
            && Keyboard.current != null
            && Keyboard.current.cKey.isPressed;
    }

    private bool IsZooming()
    {
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
    }
}
