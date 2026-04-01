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

    [Header("Nextbot Hit Reaction")]
    [SerializeField] private float _nextbotHitReactionDuration = 3f;
    [SerializeField] private float _nextbotHitShoveForce = 8f;
    [SerializeField] private float _nextbotHitUpwardForce = 4f;
    [SerializeField, Range(0f, 1f)] private float _nextbotHitRandomness = 0.55f;
    [SerializeField, Range(0f, 1f)] private float _nextbotHitTumble = 0.65f;
    [SerializeField, Range(0f, 1f)] private float _nextbotHitLimbFlail = 0.45f;

    [Header("Downed Pose")]
    [SerializeField] private Vector3 _injuredVisualPositionOffset = new Vector3(0f, 0f, 0.06f);
    [SerializeField] private Vector3 _hitReactionVisualPositionOffset = new Vector3(0f, 0f, 0.12f);
    [SerializeField] private float _downedVisualPositionBlend = 10f;
    [SerializeField] private float _downedVisualGroundClearance = 0.04f;
    [SerializeField] private float _downedVisualMaxAutoLift = 1.4f;
    [SerializeField] private string[] _downedGroundReferenceBoneNames = { "Head", "head", "Cube.011" };
    [SerializeField] private float _downedControllerHeight = 1.6f;
    [SerializeField] private float _downedControllerRadius = 0.7f;
    [SerializeField] private Vector3 _downedControllerCenter = new Vector3(0f, 0.82f, 0f);
    [SerializeField] private float _downedControllerBlend = 12f;
    [SerializeField] private bool _enableDebugInjureHotkey = true;

    [Header("Injured Interaction Prompt")]
    [SerializeField] private float _injuredInteractionPromptDistance = 5f;
    [SerializeField] private float _injuredInteractionPromptHeightTolerance = 1.75f;

    [Header("Bhop & Strafing")]
    [SerializeField] private bool _enableBunnyHop = true;
    [SerializeField] private float _groundFriction = 10f;
    [SerializeField] private float _groundControl = 8f;
    [SerializeField] private float _airAcceleration = 42f;
    [SerializeField] private float _airStrafeAccelerationMultiplier = 1.35f;
    [SerializeField] private float _airMaxSpeed = 42f;
    [SerializeField] private float _bunnyHopSpeedGain = 1.08f;
    [SerializeField] private float _bunnyHopMaxSpeed = 48f;

    [Header("Wall Run")]
    [SerializeField] private LayerMask _wallRunLayers = ~0;
    [SerializeField] private float _wallRunCheckDistance = 0.8f;
    [SerializeField] private float _wallRunMinSpeed = 5.5f;
    [SerializeField] private float _wallRunGravityMultiplier = 0.35f;
    [SerializeField] private float _wallRunMaxFallSpeed = 2.5f;
    [SerializeField] private float _wallRunSpeed = 8.5f;
    [SerializeField] private float _wallRunTurnBlend = 12f;
    [SerializeField] private float _wallRunGroundSprintGraceTime = 0.25f;
    [SerializeField] private float _wallRunContactLossBuffer = 0.18f;
    [SerializeField] private float _wallRunStartIntoWallThreshold = 0.2f;

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
    [SerializeField] private Vector3 _firstPersonCrouchCameraOffset = new Vector3(0f, -0.45f, 0f);
    [SerializeField] private float _firstPersonCrouchCameraBlend = 12f;
    [SerializeField] private Vector3 _firstPersonWallRunCameraOffset = new Vector3(0.16f, -0.08f, 0f);
    [SerializeField] private float _firstPersonWallRunCameraRoll = 12f;
    [SerializeField] private float _firstPersonWallRunCameraBlend = 10f;
    [SerializeField] private float _firstPersonWallRunCameraRotationBlend = 14f;
    [SerializeField] private float _firstPersonNearClipPlane = 0.01f;
    [SerializeField] private float _cameraTransitionDuration = 0.3f;
    [SerializeField] private string[] _firstPersonHiddenBoneNames = { "head", "torso" };

    [Header("Camera Collision")]
    [SerializeField] private LayerMask _cameraCollisionLayers = ~0;
    [SerializeField] private float _thirdPersonCameraCollisionRadius = 0.2f;
    [SerializeField] private float _thirdPersonCameraCollisionPadding = 0.08f;
    [SerializeField] private float _firstPersonWallCheckDistance = 0.45f;
    [SerializeField] private float _firstPersonWallRetreatDistance = 0.22f;
    [SerializeField] private float _firstPersonWallRetreatSmooth = 14f;
    [SerializeField] private float _firstPersonWallHideDistance = 0.12f;
    [SerializeField] private float _firstPersonWallMaxSurfaceUp = 0.35f;
    [SerializeField] private float _armWallHideCheckRadius = 0.16f;
    [SerializeField] private float _armWallHideDistance = 0.08f;

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
    private VisualElement _crosshairDotElement;
    private VisualElement _injuredInteractionPromptElement;
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
    private bool _isWallRunning;
    private int _wallRunSide;
    private Vector3 _wallRunNormal = Vector3.zero;
    private float _wallRunSprintGraceTimer;
    private float _wallRunContactHoldTimer;
    private bool _isHitReacting;
    private float _nextbotHitReactionTimer;
    private float _nextbotHitReactionPitch;
    private float _nextbotHitReactionRoll;
    private float _nextbotHitAngularVelocityPitch;
    private float _nextbotHitAngularVelocityRoll;
    private Vector3 _nextbotHitImpactVelocity = Vector3.zero;
    private float _nextbotHitImpactTimer;
    private float _nextbotHitReactionSeed;

    private CameraViewMode _currentViewMode;
    private float _defaultNearClipPlane;
    private float _defaultFieldOfView;
    private float _sprintArmWeight;
    private float _sprintFovWeight;
    private float _sprintBobWeight;
    private float _sprintBobTime;
    private float _firstPersonBobWeight;
    private float _firstPersonCrouchCameraWeight;
    private float _firstPersonWallRunCameraWeight;
    private int _lastWallRunCameraSide;
    private Vector3 _cachedThirdPersonCameraLocalPosition;
    private Quaternion _cachedThirdPersonCameraLocalRotation;
    private Renderer[] _localRenderers;
    private ShadowCastingMode[] _defaultShadowCastingModes;
    private Renderer[] _firstPersonHiddenRenderers;
    private Renderer[] _firstPersonWallHideRenderers;
    private bool[] _defaultRendererEnabledStates;
    private bool[] _defaultHiddenRendererEnabledStates;
    private bool[] _defaultWallHideRendererEnabledStates;
    private Transform _leftArmTransform;
    private Transform _rightArmTransform;
    private Transform _nextbotHitLeftArmTransform;
    private Transform _nextbotHitRightArmTransform;
    private Transform _nextbotHitLeftLegTransform;
    private Transform _nextbotHitRightLegTransform;
    private Quaternion _lastLeftArmSprintOffset = Quaternion.identity;
    private Quaternion _lastRightArmSprintOffset = Quaternion.identity;
    private Quaternion _nextbotHitLeftArmBaseLocalRotation = Quaternion.identity;
    private Quaternion _nextbotHitRightArmBaseLocalRotation = Quaternion.identity;
    private Quaternion _nextbotHitLeftLegBaseLocalRotation = Quaternion.identity;
    private Quaternion _nextbotHitRightLegBaseLocalRotation = Quaternion.identity;
    private Quaternion _nextbotHitLeftArmTargetLocalRotation = Quaternion.identity;
    private Quaternion _nextbotHitRightArmTargetLocalRotation = Quaternion.identity;
    private Quaternion _nextbotHitLeftLegTargetLocalRotation = Quaternion.identity;
    private Quaternion _nextbotHitRightLegTargetLocalRotation = Quaternion.identity;
    private Quaternion _injuredVisualRootBaseLocalRotation = Quaternion.identity;
    private Vector3 _injuredVisualRootBaseLocalPosition = Vector3.zero;
    private float _firstPersonWallRetreat;
    private float _defaultCharacterControllerHeight;
    private float _defaultCharacterControllerRadius;
    private Vector3 _defaultCharacterControllerCenter;
    private Transform[] _downedGroundReferenceTransforms = Array.Empty<Transform>();
    private Renderer[] _downedGroundReferenceRenderers = Array.Empty<Renderer>();
    private readonly Collider[] _armWallHitBuffer = new Collider[8];
    private Coroutine _cameraTransitionCoroutine;
    private bool _isPauseMenuOpen;
    private bool _hudEventsBound;
    private const float HIDE_HEAD_PROGRESS = 0.85f;
    private const float SHOW_HEAD_PROGRESS = 0.2f;
    private const string INJURED_VISUAL_ROOT_NAME = "player";
    private const string INJURED_VISUAL_PIVOT_NAME = "InjuredVisualPivot";
    private const float NEXTBOT_HIT_HORIZONTAL_DAMPING = 10f;
    private const float NEXTBOT_HIT_IMPACT_DURATION = 0.18f;
    private const float NEXTBOT_HIT_IMPACT_DAMPING = 22f;
    private const float NEXTBOT_HIT_VISUAL_BLEND = 14f;
    private const float NEXTBOT_HIT_ARM_BLEND = 12f;
    private const float NEXTBOT_HIT_LEG_BLEND = 10f;
    private const string NEXTBOT_HIT_LEFT_ARM_BONE_NAME = "ArmL1";
    private const string NEXTBOT_HIT_RIGHT_ARM_BONE_NAME = "ArmR1";
    private const string NEXTBOT_HIT_LEFT_LEG_BONE_NAME = "LegL1";
    private const string NEXTBOT_HIT_RIGHT_LEG_BONE_NAME = "LegR1";

    private const float JUMP_VELOCITY_MULTIPLIER = 3f;
    private float NextbotHitImpactForce => Mathf.Lerp(_nextbotHitShoveForce * 1.15f, _nextbotHitShoveForce * 1.75f, _nextbotHitTumble);
    private float NextbotHitDirectionRandomAngle => Mathf.Lerp(12f, 40f, _nextbotHitRandomness);
    private float NextbotHitHorizontalImpulseRandomness => Mathf.Lerp(0.1f, 0.35f, _nextbotHitRandomness);
    private float NextbotHitVerticalImpulseRandomness => Mathf.Lerp(0.08f, 0.3f, _nextbotHitRandomness);
    private float NextbotHitImpactForceRandomness => Mathf.Lerp(0.12f, 0.4f, _nextbotHitRandomness);
    private float NextbotHitVisualPitch => Mathf.Lerp(34f, 54f, _nextbotHitTumble);
    private float NextbotHitVisualRoll => Mathf.Lerp(8f, 16f, _nextbotHitTumble);
    private float NextbotHitAngularDamping => Mathf.Lerp(6f, 4f, _nextbotHitTumble);
    private float NextbotHitSettlePitch => Mathf.Lerp(42f, 62f, _nextbotHitTumble);
    private float NextbotHitMaxPitch => Mathf.Lerp(52f, 72f, _nextbotHitTumble);
    private float NextbotHitMaxRoll => Mathf.Lerp(12f, 22f, _nextbotHitTumble);
    private float NextbotHitWobblePitch => Mathf.Lerp(2f, 8f, _nextbotHitTumble);
    private float NextbotHitWobbleRoll => Mathf.Lerp(4f, 12f, _nextbotHitTumble);
    private float NextbotHitWobbleFrequency => Mathf.Lerp(5f, 8f, _nextbotHitTumble);
    private Vector2 NextbotHitArmPitchRange => Vector2.Lerp(new Vector2(-10f, 20f), new Vector2(-32f, 54f), _nextbotHitLimbFlail);
    private float NextbotHitArmYawRange => Mathf.Lerp(4f, 12f, _nextbotHitLimbFlail);
    private float NextbotHitArmRollRange => Mathf.Lerp(6f, 18f, _nextbotHitLimbFlail);
    private Vector2 NextbotHitLegPitchRange => Vector2.Lerp(new Vector2(-18f, 12f), new Vector2(-48f, 28f), _nextbotHitLimbFlail);
    private float NextbotHitLegYawRange => Mathf.Lerp(4f, 16f, _nextbotHitLimbFlail);
    private float NextbotHitLegRollRange => Mathf.Lerp(6f, 22f, _nextbotHitLimbFlail);
    private float _lastAppliedRemoteHitReactionSeed = float.NaN;
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
        CacheNextbotHitLimbTransforms();
        CacheFirstPersonWallHideRenderers();
        CacheInjuredVisualRoot();
        CacheDownedGroundReferenceTransforms();
        CacheHudElements();

        if (_characterController != null)
        {
            _defaultCharacterControllerHeight = _characterController.height;
            _defaultCharacterControllerRadius = _characterController.radius;
            _defaultCharacterControllerCenter = _characterController.center;
        }
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
        HandleDebugInjureHotkey();
        EnforceInjuredCameraView();
        UpdateCrouchState();
        UpdateDownedCollisionShape();
        UpdateSpeedHud();
        UpdateAnimationDebugHud();
        UpdateCrosshairVisibility();
        UpdateInjuredInteractionPrompt();

        HandleCursorLock();
        HandleViewToggle();
        UpdateAutoSprint();
        UpdateWallRunEligibility();
        UpdateWallRunState();
        UpdateZoom();

        if (HandleNextbotHitReaction())
        {
            return;
        }

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

    private void HandleDebugInjureHotkey()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!_enableDebugInjureHotkey || _isPauseMenuOpen || Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame)
        {
            return;
        }

        SetDebugInjuredState(!IsInjured());
#endif
    }

    private void SetDebugInjuredState(bool injured)
    {
        if (_playerAnimation == null)
        {
            return;
        }

        _isHitReacting = false;
        _nextbotHitReactionTimer = 0f;
        _nextbotHitReactionPitch = 0f;
        _nextbotHitReactionRoll = 0f;
        _nextbotHitAngularVelocityPitch = 0f;
        _nextbotHitAngularVelocityRoll = 0f;
        _nextbotHitImpactVelocity = Vector3.zero;
        _nextbotHitImpactTimer = 0f;
        _nextbotHitReactionSeed = 0f;
        _lastAppliedRemoteHitReactionSeed = float.NaN;
        _runHeldTime = 0f;
        _isCrouching = false;
        _isWallRunning = false;
        _wallRunSide = 0;
        _wallRunNormal = Vector3.zero;
        _wallRunContactHoldTimer = 0f;
        _wallRunSprintGraceTimer = 0f;
        _playerAnimation.SetInjured(injured);
        ClearHitReactionTiltPreservingVisualYaw();

        if (injured)
        {
            SetCameraView(CameraViewMode.ThirdPerson, true);
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

        if (_isHitReacting)
        {
            UpdateNextbotHitReactionVisual();
            UpdateDownedVisualRootPosition();
            _cameraTransform.localRotation = Quaternion.Euler(_cameraRotation.y, 0f, 0f);
            UpdateSprintCameraBob();
            UpdateFirstPersonWallRunCameraPose();
            ResolveCameraWallCollision();
            UpdateSprintArmPose();
            UpdateArmWallClipVisibility();
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

        UpdateDownedVisualRootPosition();
        _cameraTransform.localRotation = Quaternion.Euler(_cameraRotation.y, 0f, 0f);
        UpdateSprintCameraBob();
        UpdateFirstPersonWallRunCameraPose();
        ResolveCameraWallCollision();
        UpdateSprintArmPose();
        UpdateArmWallClipVisibility();
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

    private void CacheFirstPersonWallHideRenderers()
    {
        System.Collections.Generic.HashSet<Renderer> wallHideRenderers = new();

        if (_leftArmTransform != null)
        {
            foreach (Renderer renderer in _leftArmTransform.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null)
                {
                    wallHideRenderers.Add(renderer);
                }
            }
        }

        if (_rightArmTransform != null)
        {
            foreach (Renderer renderer in _rightArmTransform.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null)
                {
                    wallHideRenderers.Add(renderer);
                }
            }
        }

        _firstPersonWallHideRenderers = new Renderer[wallHideRenderers.Count];
        wallHideRenderers.CopyTo(_firstPersonWallHideRenderers);
        _defaultWallHideRendererEnabledStates = new bool[_firstPersonWallHideRenderers.Length];

        for (int i = 0; i < _firstPersonWallHideRenderers.Length; i++)
        {
            _defaultWallHideRendererEnabledStates[i] = _firstPersonWallHideRenderers[i] != null && _firstPersonWallHideRenderers[i].enabled;
        }
    }

    private void CacheNextbotHitLimbTransforms()
    {
        if (_nextbotHitLeftArmTransform != null
            && _nextbotHitRightArmTransform != null
            && _nextbotHitLeftLegTransform != null
            && _nextbotHitRightLegTransform != null)
        {
            return;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in transforms)
        {
            if (_nextbotHitLeftArmTransform == null && string.Equals(child.name, NEXTBOT_HIT_LEFT_ARM_BONE_NAME, StringComparison.OrdinalIgnoreCase))
            {
                _nextbotHitLeftArmTransform = child;
                _nextbotHitLeftArmBaseLocalRotation = child.localRotation;
                _nextbotHitLeftArmTargetLocalRotation = child.localRotation;
            }

            if (_nextbotHitRightArmTransform == null && string.Equals(child.name, NEXTBOT_HIT_RIGHT_ARM_BONE_NAME, StringComparison.OrdinalIgnoreCase))
            {
                _nextbotHitRightArmTransform = child;
                _nextbotHitRightArmBaseLocalRotation = child.localRotation;
                _nextbotHitRightArmTargetLocalRotation = child.localRotation;
            }

            if (_nextbotHitLeftLegTransform == null && string.Equals(child.name, NEXTBOT_HIT_LEFT_LEG_BONE_NAME, StringComparison.OrdinalIgnoreCase))
            {
                _nextbotHitLeftLegTransform = child;
                _nextbotHitLeftLegBaseLocalRotation = child.localRotation;
                _nextbotHitLeftLegTargetLocalRotation = child.localRotation;
            }

            if (_nextbotHitRightLegTransform == null && string.Equals(child.name, NEXTBOT_HIT_RIGHT_LEG_BONE_NAME, StringComparison.OrdinalIgnoreCase))
            {
                _nextbotHitRightLegTransform = child;
                _nextbotHitRightLegBaseLocalRotation = child.localRotation;
                _nextbotHitRightLegTargetLocalRotation = child.localRotation;
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
                _injuredVisualRootBaseLocalPosition = _injuredVisualRoot.localPosition;
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
            _injuredVisualRootBaseLocalPosition = _injuredVisualRoot.localPosition;
            return;
        }

        if (_playerAnimation == null || _playerAnimation.VisualRootTransform == null)
        {
            return;
        }

        _injuredVisualRoot = EnsureInjuredVisualPivot(_playerAnimation.VisualRootTransform);
        _injuredVisualRootBaseLocalRotation = _injuredVisualRoot.localRotation;
        _injuredVisualRootBaseLocalPosition = _injuredVisualRoot.localPosition;
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
        _crosshairDotElement = root.Q<VisualElement>("crosshair-dot");
        _injuredInteractionPromptElement = root.Q<VisualElement>("injured-interaction-prompt");
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

    private void CacheDownedGroundReferenceTransforms()
    {
        if (_downedGroundReferenceTransforms.Length > 0 || _downedGroundReferenceRenderers.Length > 0)
        {
            return;
        }

        if (_downedGroundReferenceBoneNames == null || _downedGroundReferenceBoneNames.Length == 0)
        {
            return;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        var matches = new System.Collections.Generic.List<Transform>();
        var referenceRenderers = new System.Collections.Generic.List<Renderer>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null)
            {
                continue;
            }

            for (int nameIndex = 0; nameIndex < _downedGroundReferenceBoneNames.Length; nameIndex++)
            {
                string referenceName = _downedGroundReferenceBoneNames[nameIndex];
                if (!string.IsNullOrWhiteSpace(referenceName)
                    && string.Equals(candidate.name, referenceName, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(candidate);
                    Renderer[] candidateRenderers = candidate.GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < candidateRenderers.Length; rendererIndex++)
                    {
                        Renderer renderer = candidateRenderers[rendererIndex];
                        if (renderer != null && !referenceRenderers.Contains(renderer))
                        {
                            referenceRenderers.Add(renderer);
                        }
                    }

                    break;
                }
            }
        }

        _downedGroundReferenceTransforms = matches.ToArray();
        _downedGroundReferenceRenderers = referenceRenderers.ToArray();
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
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        if (_animationDebugLabel != null)
        {
            _animationDebugLabel.style.display = DisplayStyle.None;
        }

        return;
#else
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
#endif
    }

    private void UpdateInjuredInteractionPrompt()
    {
        if (_injuredInteractionPromptElement == null)
        {
            CacheHudElements();
            if (_injuredInteractionPromptElement == null)
            {
                return;
            }
        }

        if (_isPauseMenuOpen || IsInjuredOrHitReacting())
        {
            SetInjuredInteractionPromptVisible(false);
            return;
        }

        if (TryGetLookedAtInjuredPlayer(out _))
        {
            SetInjuredInteractionPromptVisible(true);
            return;
        }

        SetInjuredInteractionPromptVisible(false);
    }

    private void UpdateCrosshairVisibility()
    {
        if (_crosshairDotElement == null)
        {
            CacheHudElements();
            if (_crosshairDotElement == null)
            {
                return;
            }
        }

        bool shouldShowCrosshair = !_isPauseMenuOpen && !IsInjuredOrHitReacting();
        _crosshairDotElement.style.display = shouldShowCrosshair ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetInjuredInteractionPromptVisible(bool visible)
    {
        if (_injuredInteractionPromptElement == null)
        {
            return;
        }

        _injuredInteractionPromptElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private bool TryGetLookedAtInjuredPlayer(out PlayerAnimation injuredPlayerAnimation)
    {
        injuredPlayerAnimation = null;

        Transform rayOriginTransform = _gameplayCameraTransform != null ? _gameplayCameraTransform : _cameraTransform;
        if (rayOriginTransform == null || _transform == null)
        {
            return false;
        }

        Vector3 localPosition = _transform.position;
        RaycastHit[] hits = Physics.RaycastAll(
            rayOriginTransform.position,
            rayOriginTransform.forward,
            _injuredInteractionPromptDistance,
            _cameraCollisionLayers,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidCameraCollisionHit(hit) || hit.collider == null)
            {
                continue;
            }

            PlayerAnimation candidate = hit.collider.GetComponentInParent<PlayerAnimation>();
            if (candidate == null || candidate == _playerAnimation || !candidate.IsInjuredActive)
            {
                return false;
            }

            Transform candidateTransform = candidate.transform;
            Vector3 offset = candidateTransform.position - localPosition;
            if (Mathf.Abs(offset.y) > _injuredInteractionPromptHeightTolerance)
            {
                return false;
            }

            injuredPlayerAnimation = candidate;
            return true;
        }

        return false;
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
                _gameplayCameraTransform.localPosition = GetFirstPersonTargetLocalPosition();
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

        _firstPersonWallRetreat = 0f;
        SetLocalRenderMode(firstPerson);
        SetFirstPersonWallClipHidden(false);
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
            targetLocalPosition = GetFirstPersonTargetLocalPosition();
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
        _firstPersonWallRetreat = 0f;
        SetFirstPersonWallClipHidden(false);

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

        UpdateFirstPersonCrouchCameraOffset();
        float bobAmplitude = Mathf.Lerp(_firstPersonWalkBobAmplitude, _sprintBobAmplitude, _sprintBobWeight) * _firstPersonBobWeight;
        float bobOffsetY = Mathf.Sin(_sprintBobTime) * bobAmplitude;
        Vector3 bobbedPosition = GetFirstPersonTargetLocalPosition() + new Vector3(0f, bobOffsetY, 0f);
        _gameplayCameraTransform.localPosition = bobbedPosition;
    }

    private void UpdateFirstPersonCrouchCameraOffset()
    {
        float targetWeight = _currentViewMode == CameraViewMode.FirstPerson && IsCrouching() ? 1f : 0f;
        float blend = 1f - Mathf.Exp(-_firstPersonCrouchCameraBlend * Time.deltaTime);
        _firstPersonCrouchCameraWeight = Mathf.Lerp(_firstPersonCrouchCameraWeight, targetWeight, blend);
    }

    private Vector3 GetFirstPersonTargetLocalPosition()
    {
        return _firstPersonCameraLocalPosition + _firstPersonCrouchCameraOffset * _firstPersonCrouchCameraWeight;
    }

    private void UpdateFirstPersonWallRunCameraPose()
    {
        if (_gameplayCameraTransform == null || _cameraTransitionCoroutine != null)
        {
            return;
        }

        bool useWallRunPose = _currentViewMode == CameraViewMode.FirstPerson && _isWallRunning && _wallRunSide != 0;
        float targetWeight = useWallRunPose ? 1f : 0f;
        float blend = 1f - Mathf.Exp(-_firstPersonWallRunCameraBlend * Time.deltaTime);
        _firstPersonWallRunCameraWeight = Mathf.Lerp(_firstPersonWallRunCameraWeight, targetWeight, blend);

        if (useWallRunPose)
        {
            _lastWallRunCameraSide = _wallRunSide;
        }

        if (_firstPersonWallRunCameraWeight <= 0.001f)
        {
            if (_currentViewMode == CameraViewMode.FirstPerson)
            {
                float resetBlend = 1f - Mathf.Exp(-_firstPersonWallRunCameraRotationBlend * Time.deltaTime);
                _gameplayCameraTransform.localRotation = Quaternion.Slerp(_gameplayCameraTransform.localRotation, Quaternion.identity, resetBlend);
            }
            return;
        }

        float side = useWallRunPose ? _wallRunSide : (_lastWallRunCameraSide == 0 ? 1f : _lastWallRunCameraSide);
        float cameraSide = -side;
        Vector3 sideOffset = new Vector3(_firstPersonWallRunCameraOffset.x * cameraSide, _firstPersonWallRunCameraOffset.y, _firstPersonWallRunCameraOffset.z);
        _gameplayCameraTransform.localPosition += sideOffset * _firstPersonWallRunCameraWeight;

        float targetRoll = -_firstPersonWallRunCameraRoll * cameraSide;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetRoll);
        float rotationBlend = 1f - Mathf.Exp(-_firstPersonWallRunCameraRotationBlend * Time.deltaTime);
        _gameplayCameraTransform.localRotation = Quaternion.Slerp(
            _gameplayCameraTransform.localRotation,
            targetRotation,
            rotationBlend);
    }

    private void ResolveCameraWallCollision()
    {
        if (_gameplayCameraTransform == null || _cameraTransitionCoroutine != null)
        {
            return;
        }

        if (_currentViewMode == CameraViewMode.FirstPerson)
        {
            ResolveFirstPersonWallCollision();
            return;
        }

        SetFirstPersonWallClipHidden(false);

        if (_useManualThirdPersonCamera)
        {
            ResolveThirdPersonCameraCollision();
        }
    }

    private void ResolveFirstPersonWallCollision()
    {
        float targetRetreat = 0f;

        if (TryGetNearestCameraCollisionHit(
                _gameplayCameraTransform.position,
                _gameplayCameraTransform.forward,
                _firstPersonWallCheckDistance,
                0f,
                out RaycastHit hit))
        {
            bool isWallLikeSurface = Mathf.Abs(hit.normal.y) <= _firstPersonWallMaxSurfaceUp;
            if (isWallLikeSurface)
            {
                targetRetreat = Mathf.Clamp(
                    _firstPersonWallCheckDistance - hit.distance + _thirdPersonCameraCollisionPadding,
                    0f,
                    _firstPersonWallRetreatDistance);
            }
        }

        float retreatBlend = 1f - Mathf.Exp(-_firstPersonWallRetreatSmooth * Time.deltaTime);
        _firstPersonWallRetreat = Mathf.Lerp(_firstPersonWallRetreat, targetRetreat, retreatBlend);

        Vector3 baseLocalPosition = _gameplayCameraTransform.localPosition;
        _gameplayCameraTransform.localPosition = baseLocalPosition + Vector3.back * _firstPersonWallRetreat;
        SetFirstPersonWallClipHidden(_firstPersonWallRetreat > _firstPersonWallHideDistance);
    }

    private void ResolveThirdPersonCameraCollision()
    {
        if (_cameraTransform == null)
        {
            return;
        }

        Vector3 desiredWorldPosition = _cameraTransform.TransformPoint(_thirdPersonCameraOffset);
        Vector3 rayOrigin = _cameraTransform.position;
        Vector3 toCamera = desiredWorldPosition - rayOrigin;
        float distance = toCamera.magnitude;

        if (distance <= 0.001f)
        {
            return;
        }

        Vector3 direction = toCamera / distance;

        if (TryGetNearestCameraCollisionHit(
                rayOrigin,
                direction,
                distance,
                _thirdPersonCameraCollisionRadius,
                out RaycastHit hit))
        {
            float safeDistance = Mathf.Max(0f, hit.distance - _thirdPersonCameraCollisionPadding);
            _gameplayCameraTransform.position = rayOrigin + direction * safeDistance;
        }
        else
        {
            _gameplayCameraTransform.position = desiredWorldPosition;
        }
    }

    private bool TryGetNearestCameraCollisionHit(Vector3 origin, Vector3 direction, float distance, float radius, out RaycastHit nearestHit)
    {
        nearestHit = default;

        RaycastHit[] hits = radius > 0f
            ? Physics.SphereCastAll(origin, radius, direction, distance, _cameraCollisionLayers, QueryTriggerInteraction.Ignore)
            : Physics.RaycastAll(origin, direction, distance, _cameraCollisionLayers, QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (!IsValidCameraCollisionHit(hits[i]))
            {
                continue;
            }

            nearestHit = hits[i];
            return true;
        }

        return false;
    }

    private bool IsValidCameraCollisionHit(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return false;
        }

        Transform hitTransform = hit.collider.transform;
        if (hitTransform == null)
        {
            return false;
        }

        return !hitTransform.IsChildOf(_transform);
    }

    private void UpdateArmWallClipVisibility()
    {
        bool hideArms = IsWallNearArm(_leftArmTransform) || IsWallNearArm(_rightArmTransform);
        SetFirstPersonWallClipHidden(hideArms);
    }

    private bool IsWallNearArm(Transform armTransform)
    {
        if (armTransform == null)
        {
            return false;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(
            armTransform.position,
            _armWallHideCheckRadius,
            _armWallHitBuffer,
            _cameraCollisionLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = _armWallHitBuffer[i];
            if (collider == null)
            {
                continue;
            }

            Transform hitTransform = collider.transform;
            if (hitTransform == null || hitTransform.IsChildOf(_transform))
            {
                continue;
            }

            Vector3 closestPoint = collider.ClosestPoint(armTransform.position);
            float distance = Vector3.Distance(closestPoint, armTransform.position);
            if (distance <= _armWallHideDistance)
            {
                return true;
            }
        }

        return false;
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

    private void SetFirstPersonWallClipHidden(bool hidden)
    {
        if (_firstPersonWallHideRenderers == null)
        {
            return;
        }

        for (int i = 0; i < _firstPersonWallHideRenderers.Length; i++)
        {
            Renderer renderer = _firstPersonWallHideRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = hidden ? false : _defaultWallHideRendererEnabledStates[i];
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

        if (_isWallRunning)
        {
            HandleWallRunMovement(movementDirection, targetSpeed, deltaTime);
            return;
        }

        if (treatAsAirborne)
        {
            HandleAirMovement(movementInput, movementDirection, inputMagnitude, targetSpeed, deltaTime);
            return;
        }

        HandleGroundMovement(movementDirection, inputMagnitude, targetSpeed, deltaTime);
    }

    private void UpdateInjuredFacing()
    {
        Vector2 movementInput = _playerLocomotionInput != null ? _playerLocomotionInput.MovementInput : Vector2.zero;
        if (movementInput.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        UpdateDirectionalVisualFacing(movementInput);
    }

    private void UpdateCrouchFacing()
    {
        if (_currentViewMode == CameraViewMode.FirstPerson)
        {
            ResetInjuredVisualRootRotation();
            return;
        }

        UpdateDirectionalVisualFacing(_playerLocomotionInput != null ? _playerLocomotionInput.MovementInput : Vector2.zero);
    }

    private void UpdateDirectionalVisualFacing(Vector2 movementInput)
    {
        CacheInjuredVisualRoot();

        if (_injuredVisualRoot == null)
        {
            return;
        }

        if (movementInput.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float localYaw = Mathf.Atan2(movementInput.x, movementInput.y) * Mathf.Rad2Deg + 180f;
        float blend = 1f - Mathf.Exp(-_injuredRotationSharpness * Time.deltaTime);
        Quaternion targetLocalRotation = Quaternion.Euler(0f, localYaw, 0f) * _injuredVisualRootBaseLocalRotation;
        _injuredVisualRoot.localRotation = Quaternion.Slerp(_injuredVisualRoot.localRotation, targetLocalRotation, blend);
    }

    private void UpdateNextbotHitReactionVisual()
    {
        CacheInjuredVisualRoot();

        if (_injuredVisualRoot == null)
        {
            return;
        }

        Quaternion correctedForwardRotation = Quaternion.Euler(0f, 180f, 0f) * _injuredVisualRootBaseLocalRotation;
        float wobbleTime = (_nextbotHitReactionDuration - _nextbotHitReactionTimer) * NextbotHitWobbleFrequency + _nextbotHitReactionSeed;
        float wobblePitch = Mathf.Sin(wobbleTime) * NextbotHitWobblePitch * (_nextbotHitReactionTimer / Mathf.Max(0.01f, _nextbotHitReactionDuration));
        float wobbleRoll = Mathf.Cos(wobbleTime * 1.13f) * NextbotHitWobbleRoll * (_nextbotHitReactionTimer / Mathf.Max(0.01f, _nextbotHitReactionDuration));
        Quaternion hitReactionRotation = correctedForwardRotation * Quaternion.Euler(
            _nextbotHitReactionPitch + wobblePitch,
            0f,
            _nextbotHitReactionRoll + wobbleRoll);
        float blend = 1f - Mathf.Exp(-NEXTBOT_HIT_VISUAL_BLEND * Time.deltaTime);
        _injuredVisualRoot.localRotation = Quaternion.Slerp(_injuredVisualRoot.localRotation, hitReactionRotation, blend);
        UpdateNextbotHitReactionLimbPose();
    }

    private void UpdateNextbotHitReactionLimbPose()
    {
        CacheNextbotHitLimbTransforms();
        float armBlend = 1f - Mathf.Exp(-NEXTBOT_HIT_ARM_BLEND * Time.deltaTime);
        float legBlend = 1f - Mathf.Exp(-NEXTBOT_HIT_LEG_BLEND * Time.deltaTime);

        if (_nextbotHitLeftArmTransform != null)
        {
            _nextbotHitLeftArmTransform.localRotation = Quaternion.Slerp(_nextbotHitLeftArmTransform.localRotation, _nextbotHitLeftArmTargetLocalRotation, armBlend);
        }

        if (_nextbotHitRightArmTransform != null)
        {
            _nextbotHitRightArmTransform.localRotation = Quaternion.Slerp(_nextbotHitRightArmTransform.localRotation, _nextbotHitRightArmTargetLocalRotation, armBlend);
        }

        if (_nextbotHitLeftLegTransform != null)
        {
            _nextbotHitLeftLegTransform.localRotation = Quaternion.Slerp(_nextbotHitLeftLegTransform.localRotation, _nextbotHitLeftLegTargetLocalRotation, legBlend);
        }

        if (_nextbotHitRightLegTransform != null)
        {
            _nextbotHitRightLegTransform.localRotation = Quaternion.Slerp(_nextbotHitRightLegTransform.localRotation, _nextbotHitRightLegTargetLocalRotation, legBlend);
        }
    }

    private void SeedNextbotHitReactionLimbTargets(float seed)
    {
        CacheNextbotHitLimbTransforms();

        _nextbotHitLeftArmTargetLocalRotation = _nextbotHitLeftArmBaseLocalRotation * Quaternion.Euler(
            GetSeededRange(seed, 11f, NextbotHitArmPitchRange.x, NextbotHitArmPitchRange.y),
            GetSeededRange(seed, 23f, -NextbotHitArmYawRange, NextbotHitArmYawRange),
            GetSeededRange(seed, 37f, -NextbotHitArmRollRange, NextbotHitArmRollRange));
        _nextbotHitRightArmTargetLocalRotation = _nextbotHitRightArmBaseLocalRotation * Quaternion.Euler(
            GetSeededRange(seed, 53f, NextbotHitArmPitchRange.x, NextbotHitArmPitchRange.y),
            GetSeededRange(seed, 67f, -NextbotHitArmYawRange, NextbotHitArmYawRange),
            GetSeededRange(seed, 79f, -NextbotHitArmRollRange, NextbotHitArmRollRange));
        _nextbotHitLeftLegTargetLocalRotation = _nextbotHitLeftLegBaseLocalRotation * Quaternion.Euler(
            GetSeededRange(seed, 97f, NextbotHitLegPitchRange.x, NextbotHitLegPitchRange.y),
            GetSeededRange(seed, 109f, -NextbotHitLegYawRange, NextbotHitLegYawRange),
            GetSeededRange(seed, 127f, -NextbotHitLegRollRange, NextbotHitLegRollRange));
        _nextbotHitRightLegTargetLocalRotation = _nextbotHitRightLegBaseLocalRotation * Quaternion.Euler(
            GetSeededRange(seed, 149f, NextbotHitLegPitchRange.x, NextbotHitLegPitchRange.y),
            GetSeededRange(seed, 163f, -NextbotHitLegYawRange, NextbotHitLegYawRange),
            GetSeededRange(seed, 181f, -NextbotHitLegRollRange, NextbotHitLegRollRange));
    }

    private void ResetNextbotHitReactionLimbPose()
    {
        CacheNextbotHitLimbTransforms();
        float armBlend = 1f - Mathf.Exp(-NEXTBOT_HIT_ARM_BLEND * Time.deltaTime);
        float legBlend = 1f - Mathf.Exp(-NEXTBOT_HIT_LEG_BLEND * Time.deltaTime);

        if (_nextbotHitLeftArmTransform != null)
        {
            _nextbotHitLeftArmTransform.localRotation = Quaternion.Slerp(_nextbotHitLeftArmTransform.localRotation, _nextbotHitLeftArmBaseLocalRotation, armBlend);
        }

        if (_nextbotHitRightArmTransform != null)
        {
            _nextbotHitRightArmTransform.localRotation = Quaternion.Slerp(_nextbotHitRightArmTransform.localRotation, _nextbotHitRightArmBaseLocalRotation, armBlend);
        }

        if (_nextbotHitLeftLegTransform != null)
        {
            _nextbotHitLeftLegTransform.localRotation = Quaternion.Slerp(_nextbotHitLeftLegTransform.localRotation, _nextbotHitLeftLegBaseLocalRotation, legBlend);
        }

        if (_nextbotHitRightLegTransform != null)
        {
            _nextbotHitRightLegTransform.localRotation = Quaternion.Slerp(_nextbotHitRightLegTransform.localRotation, _nextbotHitRightLegBaseLocalRotation, legBlend);
        }
    }

    private static float GetSeededRange(float seed, float salt, float min, float max)
    {
        float noise = Mathf.Sin(seed * 12.9898f + salt * 78.233f) * 43758.5453f;
        float normalized = noise - Mathf.Floor(noise);
        return Mathf.Lerp(min, max, normalized);
    }

    private void UpdateDownedVisualRootPosition()
    {
        CacheInjuredVisualRoot();

        if (_injuredVisualRoot == null)
        {
            return;
        }

        Vector3 targetLocalPosition = _injuredVisualRootBaseLocalPosition;
        if (_isHitReacting)
        {
            targetLocalPosition += _hitReactionVisualPositionOffset;
        }
        else if (IsInjured())
        {
            targetLocalPosition += _injuredVisualPositionOffset;
        }

        float autoLift = GetDownedVisualAutoLift();
        targetLocalPosition.y += autoLift;

        float blend = 1f - Mathf.Exp(-_downedVisualPositionBlend * Time.deltaTime);
        _injuredVisualRoot.localPosition = Vector3.Lerp(_injuredVisualRoot.localPosition, targetLocalPosition, blend);
    }

    private float GetDownedVisualAutoLift()
    {
        if ((!_isHitReacting && !IsInjured()) || _injuredVisualRoot == null || _characterController == null)
        {
            return 0f;
        }

        CacheDownedGroundReferenceTransforms();

        Renderer[] renderers = _injuredVisualRoot.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        float lowestPoint = float.PositiveInfinity;
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                lowestPoint = Mathf.Min(lowestPoint, renderer.bounds.min.y);
                hasBounds = true;
            }
        }

        for (int i = 0; i < _downedGroundReferenceRenderers.Length; i++)
        {
            Renderer referenceRenderer = _downedGroundReferenceRenderers[i];
            if (referenceRenderer == null || !referenceRenderer.enabled)
            {
                continue;
            }

            lowestPoint = Mathf.Min(lowestPoint, referenceRenderer.bounds.min.y);
            hasBounds = true;
        }

        for (int i = 0; i < _downedGroundReferenceTransforms.Length; i++)
        {
            Transform reference = _downedGroundReferenceTransforms[i];
            if (reference == null)
            {
                continue;
            }

            lowestPoint = Mathf.Min(lowestPoint, reference.position.y);
            hasBounds = true;
        }

        if (!hasBounds)
        {
            return 0f;
        }

        float controllerBottom = _transform.position.y + _characterController.center.y - (_characterController.height * 0.5f);
        float requiredLift = (controllerBottom + _downedVisualGroundClearance) - lowestPoint;
        return Mathf.Clamp(requiredLift, 0f, _downedVisualMaxAutoLift);
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
        ResetNextbotHitReactionLimbPose();
    }

    private void ClearHitReactionTiltPreservingVisualYaw()
    {
        CacheInjuredVisualRoot();

        if (_injuredVisualRoot == null)
        {
            return;
        }

        Quaternion relativeRotation = Quaternion.Inverse(_injuredVisualRootBaseLocalRotation) * _injuredVisualRoot.localRotation;
        float preservedYaw = NormalizeSignedAngle(relativeRotation.eulerAngles.y);
        Quaternion targetLocalRotation = Quaternion.Euler(0f, preservedYaw, 0f) * _injuredVisualRootBaseLocalRotation;
        _injuredVisualRoot.localRotation = targetLocalRotation;
        ResetNextbotHitReactionLimbPose();
    }

    public void ApplyRemoteVisualState(Vector2 movementInput, bool injured, bool crouching)
    {
        if (injured || crouching)
        {
            UpdateDirectionalVisualFacing(movementInput);
            ResetNextbotHitReactionLimbPose();
        }
        else
        {
            ResetInjuredVisualRootRotation();
        }

        UpdateDownedCollisionShape();
        UpdateDownedVisualRootPosition();
    }

    public float GetVisualYaw()
    {
        CacheInjuredVisualRoot();

        if (_injuredVisualRoot == null)
        {
            return 180f;
        }

        Vector2 movementInput = _playerLocomotionInput != null ? _playerLocomotionInput.MovementInput : Vector2.zero;
        if ((IsInjured() || IsCrouching()) && movementInput.sqrMagnitude > 0.0001f)
        {
            return NormalizeSignedAngle(Mathf.Atan2(movementInput.x, movementInput.y) * Mathf.Rad2Deg + 180f);
        }

        Quaternion relativeRotation = Quaternion.Inverse(_injuredVisualRootBaseLocalRotation) * _injuredVisualRoot.localRotation;
        return NormalizeSignedAngle(relativeRotation.eulerAngles.y);
    }

    public void ApplyRemoteVisualYaw(float visualYaw)
    {
        CacheInjuredVisualRoot();

        if (_injuredVisualRoot == null)
        {
            return;
        }

        Quaternion targetLocalRotation = Quaternion.Euler(0f, visualYaw, 0f) * _injuredVisualRootBaseLocalRotation;
        float blend = 1f - Mathf.Exp(-_injuredRotationSharpness * Time.deltaTime);
        _injuredVisualRoot.localRotation = Quaternion.Slerp(_injuredVisualRoot.localRotation, targetLocalRotation, blend);
    }

    public void GetHitReactionSyncState(
        out bool isHitReacting,
        out float reactionTimeRemaining,
        out float reactionPitch,
        out float reactionRoll,
        out float reactionSeed)
    {
        isHitReacting = _isHitReacting;
        reactionTimeRemaining = _nextbotHitReactionTimer;
        reactionPitch = _nextbotHitReactionPitch;
        reactionRoll = _nextbotHitReactionRoll;
        reactionSeed = _nextbotHitReactionSeed;
    }

    public void ApplyRemoteHitReactionState(
        bool isHitReacting,
        float reactionTimeRemaining,
        float reactionPitch,
        float reactionRoll,
        float reactionSeed)
    {
        if (isHitReacting)
        {
            if (!Mathf.Approximately(_lastAppliedRemoteHitReactionSeed, reactionSeed))
            {
                SeedNextbotHitReactionLimbTargets(reactionSeed);
                _lastAppliedRemoteHitReactionSeed = reactionSeed;
            }

            _isHitReacting = true;
            _nextbotHitReactionTimer = reactionTimeRemaining;
            _nextbotHitReactionPitch = reactionPitch;
            _nextbotHitReactionRoll = reactionRoll;
            _nextbotHitReactionSeed = reactionSeed;
            UpdateNextbotHitReactionVisual();
            UpdateDownedVisualRootPosition();
            return;
        }

        if (_isHitReacting)
        {
            _isHitReacting = false;
            _nextbotHitReactionTimer = 0f;
            _nextbotHitReactionPitch = 0f;
            _nextbotHitReactionRoll = 0f;
            _nextbotHitReactionSeed = 0f;
            _lastAppliedRemoteHitReactionSeed = float.NaN;
            ClearHitReactionTiltPreservingVisualYaw();
            UpdateDownedVisualRootPosition();
        }
    }

    private static float NormalizeSignedAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }

        if (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
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

    private bool HandleNextbotHitReaction()
    {
        if (!_isHitReacting)
        {
            return false;
        }

        float deltaTime = Time.deltaTime;
        bool isGrounded = IsGrounded();

        if (isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -1f;
        }
        else
        {
            _verticalVelocity -= gravity * deltaTime;
        }

        _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, Vector3.zero, NEXTBOT_HIT_HORIZONTAL_DAMPING * deltaTime);
        if (_nextbotHitImpactTimer > 0f)
        {
            _nextbotHitImpactTimer = Mathf.Max(0f, _nextbotHitImpactTimer - deltaTime);
            _nextbotHitImpactVelocity = Vector3.MoveTowards(_nextbotHitImpactVelocity, Vector3.zero, NEXTBOT_HIT_IMPACT_DAMPING * deltaTime);
        }
        else
        {
            _nextbotHitImpactVelocity = Vector3.zero;
        }

        _nextbotHitReactionPitch += _nextbotHitAngularVelocityPitch * deltaTime;
        _nextbotHitReactionRoll += _nextbotHitAngularVelocityRoll * deltaTime;
        _nextbotHitAngularVelocityPitch = Mathf.Lerp(_nextbotHitAngularVelocityPitch, 0f, 1f - Mathf.Exp(-NextbotHitAngularDamping * deltaTime));
        _nextbotHitAngularVelocityRoll = Mathf.Lerp(_nextbotHitAngularVelocityRoll, 0f, 1f - Mathf.Exp(-NextbotHitAngularDamping * deltaTime));
        float settleBlend = 1f - Mathf.Exp(-NextbotHitAngularDamping * 0.65f * deltaTime);
        _nextbotHitReactionPitch = Mathf.Lerp(_nextbotHitReactionPitch, NextbotHitSettlePitch, settleBlend);
        _nextbotHitReactionRoll = Mathf.Lerp(_nextbotHitReactionRoll, 0f, settleBlend);
        _nextbotHitReactionPitch = Mathf.Clamp(_nextbotHitReactionPitch, 0f, NextbotHitMaxPitch);
        _nextbotHitReactionRoll = Mathf.Clamp(_nextbotHitReactionRoll, -NextbotHitMaxRoll, NextbotHitMaxRoll);
        _nextbotHitReactionTimer = Mathf.Max(0f, _nextbotHitReactionTimer - deltaTime);

        Vector3 finalVelocity = _horizontalVelocity + _nextbotHitImpactVelocity;
        finalVelocity.y = _verticalVelocity;
        _characterController.Move(finalVelocity * deltaTime);

        if (_nextbotHitReactionTimer <= 0f)
        {
            _isHitReacting = false;
            _nextbotHitReactionPitch = 0f;
            _nextbotHitReactionRoll = 0f;
            _nextbotHitAngularVelocityPitch = 0f;
            _nextbotHitAngularVelocityRoll = 0f;
            ClearHitReactionTiltPreservingVisualYaw();
            _playerAnimation?.SetInjured(true);
        }

        return true;
    }

    private void UpdateDownedCollisionShape()
    {
        if (_characterController == null)
        {
            return;
        }

        float targetHeight = _defaultCharacterControllerHeight;
        float targetRadius = _defaultCharacterControllerRadius;
        Vector3 targetCenter = _defaultCharacterControllerCenter;

        if (_isHitReacting || IsInjured())
        {
            targetHeight = Mathf.Max(_downedControllerHeight, _downedControllerRadius * 2f);
            targetRadius = Mathf.Min(_downedControllerRadius, targetHeight * 0.5f);
            targetCenter = _downedControllerCenter;
        }

        float blend = 1f - Mathf.Exp(-_downedControllerBlend * Time.deltaTime);
        _characterController.height = Mathf.Lerp(_characterController.height, targetHeight, blend);
        _characterController.radius = Mathf.Lerp(_characterController.radius, targetRadius, blend);
        _characterController.center = Vector3.Lerp(_characterController.center, targetCenter, blend);
    }

    public bool TriggerNextbotHit(Vector3 sourcePosition)
    {
        if (!enabled || _playerAnimation == null || _isHitReacting || IsInjured())
        {
            return false;
        }

        CacheNextbotHitLimbTransforms();

        Vector3 awayDirection = _transform.position - sourcePosition;
        awayDirection.y = 0f;
        if (awayDirection.sqrMagnitude <= 0.0001f)
        {
            awayDirection = -_transform.forward;
        }

        awayDirection.Normalize();
        awayDirection = Quaternion.Euler(0f, UnityEngine.Random.Range(-NextbotHitDirectionRandomAngle, NextbotHitDirectionRandomAngle), 0f) * awayDirection;
        Vector3 lateralDirection = Vector3.Cross(Vector3.up, awayDirection).normalized;
        awayDirection = (awayDirection + lateralDirection * UnityEngine.Random.Range(-0.25f, 0.25f)).normalized;

        float horizontalImpulse = _nextbotHitShoveForce * UnityEngine.Random.Range(
            1f - NextbotHitHorizontalImpulseRandomness,
            1f + NextbotHitHorizontalImpulseRandomness);
        float verticalImpulse = _nextbotHitUpwardForce * UnityEngine.Random.Range(
            1f - NextbotHitVerticalImpulseRandomness,
            1f + NextbotHitVerticalImpulseRandomness);
        float impactForce = NextbotHitImpactForce * UnityEngine.Random.Range(
            1f - NextbotHitImpactForceRandomness,
            1f + NextbotHitImpactForceRandomness);

        _isHitReacting = true;
        _nextbotHitReactionTimer = _nextbotHitReactionDuration;
        _nextbotHitReactionSeed = UnityEngine.Random.Range(0f, 10000f);
        _nextbotHitReactionPitch = GetSeededRange(_nextbotHitReactionSeed, 211f, NextbotHitVisualPitch * 0.7f, NextbotHitVisualPitch);
        _nextbotHitReactionRoll = GetSeededRange(_nextbotHitReactionSeed, 223f, -NextbotHitVisualRoll, NextbotHitVisualRoll);
        _nextbotHitAngularVelocityPitch = GetSeededRange(_nextbotHitReactionSeed, 239f, -24f, 18f);
        _nextbotHitAngularVelocityRoll = GetSeededRange(_nextbotHitReactionSeed, 251f, -52f, 52f);
        SeedNextbotHitReactionLimbTargets(_nextbotHitReactionSeed);
        _horizontalVelocity = awayDirection * horizontalImpulse;
        _nextbotHitImpactVelocity = awayDirection * impactForce;
        _nextbotHitImpactTimer = NEXTBOT_HIT_IMPACT_DURATION;
        _verticalVelocity = Mathf.Max(_verticalVelocity, verticalImpulse);
        _runHeldTime = 0f;
        _isCrouching = false;
        _isWallRunning = false;
        _wallRunContactHoldTimer = 0f;
        _wallRunSprintGraceTimer = 0f;
        _playerAnimation.SetInjured(false);
        SetCameraView(CameraViewMode.ThirdPerson, true);

        return true;
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

    private void HandleWallRunMovement(Vector3 movementDirection, float targetSpeed, float deltaTime)
    {
        if (_wallRunNormal.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 alongWall = Vector3.Cross(Vector3.up, _wallRunNormal).normalized;
        if (Vector3.Dot(alongWall, _transform.forward) < 0f)
        {
            alongWall = -alongWall;
        }

        if (movementDirection.sqrMagnitude > 0.001f && Vector3.Dot(alongWall, movementDirection.normalized) < 0f)
        {
            alongWall = -alongWall;
        }

        float desiredSpeed = Mathf.Max(targetSpeed, _wallRunSpeed);
        float blend = 1f - Mathf.Exp(-_wallRunTurnBlend * deltaTime);
        Vector3 targetVelocity = alongWall * desiredSpeed;
        _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, targetVelocity, blend);
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
        
        float gravityMultiplier = _isWallRunning ? _wallRunGravityMultiplier : 1f;
        _verticalVelocity -= gravity * gravityMultiplier * deltaTime;

        if (_isWallRunning)
        {
            _verticalVelocity = Mathf.Max(_verticalVelocity, -_wallRunMaxFallSpeed);
        }

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

            Vector2 movementInput = _playerLocomotionInput != null ? _playerLocomotionInput.MovementInput : Vector2.zero;
            bool canPrimeWallRun =
                IsSprinting()
                && movementInput.y > 0.1f
                && Mathf.Abs(movementInput.x) > 0.1f;

            _wallRunSprintGraceTimer = canPrimeWallRun ? _wallRunGroundSprintGraceTime : 0f;

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

    public Vector2 GetCameraRotation()
    {
        return _cameraRotation;
    }

    public bool DidJumpThisFrame()
    {
        return _jumpedThisFrame;
    }

    public bool IsWallRunning()
    {
        return _isWallRunning;
    }

    public bool IsHitReacting()
    {
        return _isHitReacting;
    }

    public bool IsInjuredOrHitReacting()
    {
        return _isHitReacting || IsInjured();
    }

    public CharacterController GetCharacterController()
    {
        return _characterController;
    }

    public int GetWallRunSide()
    {
        return _wallRunSide;
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

    private void UpdateWallRunState()
    {
        bool canMaintainWallRun = CanMaintainWallRun();
        bool foundWallContact = TryGetWallRunContact(out int detectedWallSide, out Vector3 detectedWallNormal);

        if (!_isWallRunning)
        {
            bool canStartWallRun = canMaintainWallRun && _wallRunSprintGraceTimer > 0f;
            if (canStartWallRun && foundWallContact && IsPushingIntoWall(detectedWallNormal))
            {
                _isWallRunning = true;
                _wallRunSide = detectedWallSide;
                _wallRunNormal = detectedWallNormal;
                _wallRunContactHoldTimer = _wallRunContactLossBuffer;
                return;
            }

            _wallRunSide = 0;
            _wallRunNormal = Vector3.zero;
            _wallRunContactHoldTimer = 0f;
            return;
        }

        if (!canMaintainWallRun)
        {
            StopWallRun();
            return;
        }

        if (foundWallContact)
        {
            _wallRunSide = detectedWallSide;
            _wallRunNormal = detectedWallNormal;
            _wallRunContactHoldTimer = _wallRunContactLossBuffer;
            return;
        }

        if (_wallRunContactHoldTimer > 0f)
        {
            _wallRunContactHoldTimer = Mathf.Max(0f, _wallRunContactHoldTimer - Time.deltaTime);
            return;
        }

        StopWallRun();
    }

    private void UpdateWallRunEligibility()
    {
        if (IsGrounded() && !_isWallRunning)
        {
            _wallRunSprintGraceTimer = 0f;
            return;
        }

        _wallRunSprintGraceTimer = Mathf.Max(0f, _wallRunSprintGraceTimer - Time.deltaTime);
    }

    private bool CanMaintainWallRun()
    {
        if (_playerLocomotionInput == null || IsGrounded() || IsInjured() || IsCrouching())
        {
            return false;
        }

        if (!_playerLocomotionInput.JumpHeld)
        {
            return false;
        }

        Vector2 movementInput = _playerLocomotionInput.MovementInput;
        if (movementInput.y <= 0.1f || Mathf.Abs(movementInput.x) <= 0.1f)
        {
            return false;
        }

        if (GetHorizontalSpeed() < _wallRunMinSpeed)
        {
            return false;
        }

        return true;
    }

    private bool IsPushingIntoWall(Vector3 wallNormal)
    {
        if (_playerLocomotionInput == null || wallNormal.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 desiredMovementDirection = GetPlanarMovementDirection(_playerLocomotionInput.MovementInput);
        if (desiredMovementDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        float intoWallAmount = Vector3.Dot(desiredMovementDirection, -wallNormal.normalized);
        return intoWallAmount >= _wallRunStartIntoWallThreshold;
    }

    private Vector3 GetPlanarMovementDirection(Vector2 movementInput)
    {
        Vector3 forward = _transform.forward;
        Vector3 right = _transform.right;
        Vector3 forwardXZ = new Vector3(forward.x, 0f, forward.z).normalized;
        Vector3 rightXZ = new Vector3(right.x, 0f, right.z).normalized;
        return (forwardXZ * movementInput.y + rightXZ * movementInput.x).normalized;
    }

    private bool TryGetWallRunContact(out int wallSide, out Vector3 wallNormal)
    {
        wallSide = 0;
        wallNormal = Vector3.zero;

        if (_playerLocomotionInput == null)
        {
            return false;
        }

        Vector2 movementInput = _playerLocomotionInput.MovementInput;

        int desiredSide = movementInput.x > 0f ? 1 : -1;
        Vector3 rayDirection = desiredSide > 0 ? _transform.right : -_transform.right;
        Vector3 rayOrigin = _transform.position + Vector3.up * (_characterController.height * 0.5f);
        float effectiveCheckDistance = _wallRunCheckDistance;

        if (_characterController != null)
        {
            effectiveCheckDistance = Mathf.Max(
                effectiveCheckDistance,
                _characterController.radius + _characterController.skinWidth + _wallRunCheckDistance);
        }

        if (!Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, effectiveCheckDistance, _wallRunLayers, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (Mathf.Abs(hit.normal.y) > 0.2f)
        {
            return false;
        }

        wallSide = desiredSide;
        wallNormal = hit.normal;
        return true;
    }

    private void StopWallRun()
    {
        _isWallRunning = false;
        _wallRunSide = 0;
        _wallRunNormal = Vector3.zero;
        _wallRunContactHoldTimer = 0f;
    }

    private bool IsZooming()
    {
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
    }
}
