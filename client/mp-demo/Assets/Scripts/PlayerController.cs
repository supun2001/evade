using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Unity.Cinemachine;
using PlayerCharacterController;
using System;
using System.Collections;

[DefaultExecutionOrder(100)]
public class PlayerController : MonoBehaviour
{
    private enum CameraViewMode
    {
        FirstPerson,
        ThirdPerson
    }

    private struct SpectateTarget
    {
        public string Key;
        public string Label;
        public Transform Transform;
        public bool IsNextbot;
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
    [SerializeField, Min(0.01f)] private float _crouchRunHoldDuration = 3f;
    [SerializeField, Range(0f, 1f)] private float _crouchRunAnimationExitSpeedRatio = 0.2f;
    [SerializeField, Min(0f)] private float _crouchRunEnterMinSpeed = 4f;
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

    [Header("Temporary Buffs")]
    [SerializeField] private float _maxSpeedBoostMultiplier = 2.5f;
    [SerializeField] private float _maxJumpBoostMultiplier = 2f;
    [SerializeField] private Color _pickupFadeColor = new Color(1f, 0.24f, 0.24f, 1f);
    [SerializeField] private Color _jumpPickupFadeColor = new Color(0.24f, 0.5f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float _pickupFadePeakOpacity = 0.18f;
    [SerializeField, Min(0.05f)] private float _pickupFadeDuration = 0.35f;

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

    [Header("Injured Interaction Prompt")]
    [SerializeField] private float _injuredInteractionPromptDistance = 8f;
    [SerializeField] private float _reviveInteractionPromptDistance = 8f;
    [SerializeField] private float _carryInteractionPromptDistance = 8f;
    [SerializeField] private float _thirdPersonInteractionRayHeight = 1.15f;
    [SerializeField] private float _thirdPersonInteractionRayRadius = 0.65f;
    [SerializeField] private float _injuredInteractionPromptHeightTolerance = 1.75f;
    [SerializeField] private float _reviveHoldDuration = 2.5f;
    [SerializeField, Min(0.05f)] private float _reviveRetryInterval = 0.25f;
    [SerializeField] private float _carryMoveSpeed = 2.1f;
    [SerializeField, Min(1f)] private float _carrySlideSpeedMultiplier = 1.35f;
    [SerializeField] private Vector3 _carriedPlayerOffset = new Vector3(0.45f, 1.05f, -0.15f);
    [SerializeField] private string _carryLeftAnchorBoneName = "L_Arm";
    [SerializeField] private string _carryRightAnchorBoneName = "R_Arm";
    [SerializeField] private Vector3 _carriedPlayerAnchorOffset = new Vector3(0f, 1.08f, 0.02f);

    [Header("Bhop & Strafing")]
    [SerializeField] private bool _enableBunnyHop = true;
    [SerializeField] private float _groundFriction = 10f;
    [SerializeField] private float _groundControl = 8f;
    [SerializeField, Min(0.05f)] private float _groundProbeDistance = 0.85f;
    [SerializeField, Range(0.2f, 1f)] private float _groundProbeRadiusScale = 0.82f;
    [SerializeField, Min(0f)] private float _groundStickVelocity = 2.5f;
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

    [Header("Ramp Boost")]
    [SerializeField] private string _rampLayerName = "Ramp";
    [SerializeField, Min(1f)] private float _rampLipSpeedMultiplier = 1.18f;
    [SerializeField, Min(1f)] private float _rampLipJumpForceMultiplier = 1.22f;
    [SerializeField, Min(0.01f)] private float _rampLipGraceTime = 0.14f;
    [SerializeField, Min(0f)] private float _rampLipMinHorizontalSpeed = 6f;
    [SerializeField, Min(0.05f)] private float _rampGroundCheckDistance = 0.45f;

    [Header("Camera Settings")]
    public float lookSenseH = 0.1f;
    public float lookSenseV = 0.1f;
    public float lookLimitV = 89f;
    [SerializeField] private float _firstPersonLookUpLimit = 80f;
    [SerializeField] private float _firstPersonLookDownLimit = 25f;

    [Header("Zoom Settings")]
    [SerializeField] private float _zoomFieldOfView = 35f;
    [SerializeField] private float _zoomSmoothSpeed = 10f;

    [Header("Spectate")]
    [SerializeField] private Vector3 _spectatePlayerOffset = new Vector3(0f, 2.1f, -4.75f);
    [SerializeField] private Vector3 _spectateNextbotOffset = new Vector3(0f, 2.4f, -5.5f);
    [SerializeField] private float _spectateCameraMoveSpeed = 10f;
    [SerializeField] private float _spectateCameraRotateSpeed = 12f;
    [SerializeField] private float _spectateTargetRefreshInterval = 0.35f;
    [SerializeField] private string _spectateCameraName = "SpecCamera";

    [Header("Model Roots")]
    [SerializeField] private GameObject _firstPersonArmRoot;
    [SerializeField] private GameObject _thirdPersonBodyRoot;
    [SerializeField] private Vector3 _thirdPersonBodyRotationOffset = new Vector3(0f, 180f, 0f);

    [Header("Sprint Arms")]
    [SerializeField] private string[] _firstPersonOnlyRootNames = { "arms", "arm", "R_Arm", "L_Arm" };
    [SerializeField] private Vector3 _firstPersonArmsBasePositionOffset = new Vector3(0f, -0.22f, 0.1f);
    [SerializeField] private Vector3 _firstPersonArmsBaseRotationOffset = new Vector3(8f, 0f, 0f);
    [SerializeField] private Vector3 _firstPersonArmsLookDownPositionOffset = new Vector3(0f, -0.42f, 0.24f);
    [SerializeField] private Vector3 _firstPersonArmsLookDownRotationOffset = new Vector3(18f, 0f, 0f);
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

    [Header("Camera Visibility")]
    [SerializeField] private float _minimumGameplayFarClipPlane = 2000f;
    [SerializeField] private bool _disableGameplayOcclusionCulling = true;

    [Header("Nextbot Warning Indicator")]
    [SerializeField] private float _nextbotWarningRange = 35f;
    [SerializeField, Min(0f)] private float _nextbotHitWarningMemorySeconds = 1.25f;
    [SerializeField] private float _nextbotWarningRingRadius = 260f;
    [SerializeField] private float _nextbotWarningRingVerticalOffset = 48f;
    [SerializeField] private float _nextbotWarningMinOpacity = 0.42f;
    [SerializeField] private float _nextbotWarningMaxOpacity = 0.95f;
    [SerializeField] private float _nextbotWarningMinScale = 0.86f;
    [SerializeField] private float _nextbotWarningMaxScale = 1.08f;
    [SerializeField] private float _nextbotWarningArrowLeftOffset = 18f;
    [SerializeField] private float _nextbotWarningArrowVerticalOffset = 14f;
    [SerializeField] private Texture2D _nextbotWarningArrowTexture;
    [SerializeField] private Texture2D _nextbotWarningSkullTexture;

    [Header("Footsteps")]
    [SerializeField] private bool _useAnimationEventFootsteps;
    [SerializeField] private AudioClip[] _footstepClips;
    [SerializeField, Min(0f)] private float _footstepVolume = 0.6f;
    [SerializeField, Range(0f, 0.3f)] private float _footstepPitchRandomness = 0.04f;
    [SerializeField, Min(0f)] private float _footstepMinHorizontalSpeed = 0.5f;
    [SerializeField, Min(0f)] private float _remoteFootstepMinDistance = 1.5f;
    [SerializeField, Min(0.1f)] private float _remoteFootstepMaxDistance = 18f;
    [SerializeField] private AudioClip[] _crouchFootstepClips;
    [SerializeField, Min(0f)] private float _crouchFootstepVolume = 0.4f;
    [SerializeField] private AudioClip[] _jumpStartFootstepClips;
    [SerializeField, Min(0f)] private float _jumpStartFootstepVolume = 0.7f;
    [SerializeField] private AudioClip[] _landingFootstepClips;
    [SerializeField, Min(0f)] private float _landingFootstepVolume = 0.8f;

    [Header("Hit Audio")]
    [SerializeField] private AudioClip _playerGotHitClip;
    [SerializeField, Range(0f, 1f)] private float _playerGotHitVolume = 1f;

    [Header("Shooting Mode")]
    [SerializeField, Min(1f)] private float _maxHealth = 100f;
    [SerializeField, Min(1f)] private float _ak47Damage = 25f;
    [SerializeField, Min(0.01f)] private float _ak47FireInterval = 0.12f;
    [SerializeField, Min(1f)] private float _ak47Range = 220f;
    [SerializeField] private LayerMask _ak47HitLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private string _gunshotResourceFolder = "SFX/Gunshots";
    [SerializeField, Range(0f, 1f)] private float _gunshotVolume = 0.9f;
    [SerializeField] private string _bulletVfxResourcePath = "VFX/Bullet";
    [SerializeField] private string _ak47AttachPointName = "AK47 Attach point";
    [SerializeField] private Transform _muzzleFlashSpawnPoint;
    [SerializeField] private Transform _bulletParticleSpawnPoint;
    [SerializeField] private Vector3 _bulletParticleLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 _bulletParticleLocalEuler = Vector3.zero;
    [SerializeField, Min(0f)] private float _bulletParticleMuzzleForwardOffset = 0.03f;
    [SerializeField] private bool _forceStraightBulletParticle = true;
    [SerializeField] private string _muzzleFlashVfxResourcePath = "VFX/MuzzleFlash";
    [SerializeField] private Vector3 _muzzleFlashLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 _muzzleFlashLocalEuler = Vector3.zero;
    [SerializeField, Min(0f)] private float _muzzleFlashForwardOffset = 0.015f;

    private PlayerLocomotionInput _playerLocomotionInput;
    private Transform _transform;
    private Transform _cameraTransform;
    private Transform _gameplayCameraTransform;
    private Camera _gameplayCamera;
    private Camera _spectateCamera;
    private Transform _spectateCameraTransform;
    private PlayerAnimation _playerAnimation;
    private Transform _injuredVisualRoot;
    private UIDocument _playerHudDocument;
    private Label _speedLabel;
    private Label _animationDebugLabel;
    private Label _localHealthLabelElement;
    private VisualElement _crosshairDotElement;
    private VisualElement _localHealthContainerElement;
    private VisualElement _localHealthFillElement;
    private VisualElement _enemyHealthOverlayElement;
    private VisualElement _pickupFadeOverlayElement;
    private VisualElement _nextbotWarningIndicatorElement;
    private VisualElement _nextbotWarningArrowElement;
    private VisualElement _nextbotWarningSkullElement;
    private VisualElement _injuredInteractionPromptElement;
    private VisualElement _reviveActionRowElement;
    private VisualElement _carryActionRowElement;
    private VisualElement _reviveActionFillElement;
    private VisualElement _carryActionFillElement;
    private VisualElement _pauseMenuElement;
    private Button _continueButton;
    private Button _mainMenuButton;
    private Button _respawnButton;
    private Button _pauseGraphicsLowButton;
    private Button _pauseGraphicsMediumButton;
    private SliderInt _pauseVolumeSlider;
    private Label _pauseGraphicsValueLabel;
    private CinemachineBrain _cinemachineBrain;
    private CinemachineCamera _cinemachineCamera;
    private CinemachineThirdPersonFollow _thirdPersonFollow;
    private NetworkPlayer _networkPlayer;
    private AudioSource _footstepAudioSource;
    private AudioSource _pickupAudioSource;
    private AudioSource _hurtAudioSource;
    private AudioSource _gunshotAudioSource;
    private AudioListener _gameplayAudioListener;
    private AudioListener _spectateAudioListener;
    private Coroutine _pickupAudioStopCoroutine;
    
    private Vector2 _cameraRotation = Vector2.zero;
    private float _playerRotationY = 0f;
    private float _verticalVelocity = 0f;
    private Vector3 _horizontalVelocity = Vector3.zero;
    private float _runHeldTime = 0f;
    private float _crouchRunTimer;
    private float _crouchRunStartMoveSpeed;
    private bool _isCrouchRunning;
    private float _speedBoostMultiplier = 1f;
    private float _speedBoostExpiresAt = -1f;
    private float _jumpBoostMultiplier = 1f;
    private float _jumpBoostExpiresAt = -1f;
    private Coroutine _pickupFadeCoroutine;
    private float _reviveHoldTimer;
    private float _reviveHoldStartedAt = -1f;
    private string _reviveHoldTargetSessionId;
    private bool _reviveHoldTriggered;
    private float _nextReviveRequestAt = -1f;
    private bool _isCarryingPlayer;
    private bool _isBeingCarried;
    private string _carriedPlayerSessionId;
    private string _carrierSessionId;
    private GameObject _ignoredCarryCollisionObject;
    private readonly System.Collections.Generic.List<Collider> _ignoredCarryCollisionColliders = new();
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
    private float _injuredFacingYaw = 180f;
    private Vector3 _recentNextbotHitSource = Vector3.zero;
    private float _recentNextbotHitSourceExpiresAt = float.NegativeInfinity;
    private float _currentHealth;
    private bool _combatModeActive;
    private bool _isEliminatedState;
    private float _nextAllowedShotTime;
    private AudioClip[] _gunshotClips = Array.Empty<AudioClip>();
    private int _lastGunshotClipIndex = -1;
    private readonly System.Collections.Generic.Dictionary<string, EnemyHealthBarView> _enemyHealthBarViews = new();
    private GameObject _bulletVfxPrefab;
    private Quaternion _bulletParticlePrefabLocalRotation = Quaternion.identity;
    private GameObject _muzzleFlashVfxPrefab;
    private Quaternion _muzzleFlashPrefabLocalRotation = Quaternion.identity;

    private CameraViewMode _currentViewMode;
    private CameraViewMode _preferredViewMode;
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
    private Renderer[] _firstPersonOnlyRenderers;
    private ShadowCastingMode[] _defaultShadowCastingModes;
    private Renderer[] _firstPersonHiddenRenderers;
    private Renderer[] _firstPersonWallHideRenderers;
    private bool[] _defaultRendererEnabledStates;
    private bool[] _defaultFirstPersonOnlyRendererEnabledStates;
    private bool[] _defaultHiddenRendererEnabledStates;
    private bool[] _defaultWallHideRendererEnabledStates;
    private Transform[] _firstPersonOnlyRoots = Array.Empty<Transform>();
    private Vector3[] _firstPersonOnlyRootLocalPositions = Array.Empty<Vector3>();
    private Quaternion[] _firstPersonOnlyRootLocalRotations = Array.Empty<Quaternion>();
    private Vector3[] _firstPersonOnlyRootLocalScales = Array.Empty<Vector3>();
    private Transform _leftArmTransform;
    private Transform _rightArmTransform;
    private Transform _carryLeftAnchorTransform;
    private Transform _carryRightAnchorTransform;
    private Transform _nextbotHitLeftArmTransform;
    private Transform _nextbotHitRightArmTransform;
    private Transform _nextbotHitLeftLegTransform;
    private Transform _nextbotHitRightLegTransform;
    private int _lastFootstepClipIndex = -1;
    private float _footstepStepTimer;
    private float _lastAnimationEventFootstepTime = float.NegativeInfinity;
    private int _lastCrouchFootstepClipIndex = -1;
    private int _lastJumpStartClipIndex = -1;
    private int _lastLandingClipIndex = -1;
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
    private bool _isTemporaryThirdPersonForced;
    private bool _isSimulationControlled;
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
    private const float FOOTSTEP_WALK_INTERVAL = 0.42f;
    private const float FOOTSTEP_RUN_INTERVAL = 0.28f;
    private const float FOOTSTEP_MAX_UPWARD_SPEED = 0.15f;
    private const float FOOTSTEP_ANIMATION_EVENT_COOLDOWN = 0.08f;

    private sealed class EnemyHealthBarView
    {
        public VisualElement Root;
        public VisualElement Fill;
        public Label Label;
    }

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
    private int _rampLayer = -1;
    private float _lastRampTouchTime = float.NegativeInfinity;
    private bool _groundProbeGrounded;
    private RaycastHit _groundProbeHit;
    private bool _isSpectating;
    private string _spectateTargetKey;
    private float _nextSpectateRefreshTime;
    private readonly System.Collections.Generic.List<SpectateTarget> _spectateTargets = new();
    private static readonly System.Collections.Generic.List<PlayerController> RegisteredPlayerCollisionControllers = new();
    private bool _spectateCharacterControllerWasEnabled;
    private bool _spectateVisualRootWasActive = true;
    private static readonly Vector3 SpectatorHiddenPosition = new Vector3(0f, -500f, 0f);
    #endregion

    #region Setup
    private void Awake() {
        _playerLocomotionInput = GetComponent<PlayerLocomotionInput>();
        _playerAnimation = GetComponent<PlayerAnimation>();
        _networkPlayer = GetComponent<NetworkPlayer>();
        _rampLayer = LayerMask.NameToLayer(_rampLayerName);
        _transform = transform;
        _cameraTransform = _playerCamera.transform;
        _thirdPersonFollow = GetComponentInChildren<CinemachineThirdPersonFollow>(true);
        _cinemachineCamera = GetComponentInChildren<CinemachineCamera>(true);
        _gameplayCamera = FindGameplayCamera();
        _gameplayCameraTransform = _gameplayCamera != null ? _gameplayCamera.transform : null;
        
        // Ensure FPS arms can't pick up physics jitter from imported child bodies/colliders
        if (_firstPersonArmRoot != null)
        {
            Rigidbody[] rigidbodies = _firstPersonArmRoot.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody rb = rigidbodies[i];
                if (rb == null)
                {
                    continue;
                }

                rb.isKinematic = true;
                rb.useGravity = false;
            }

            Collider[] colliders = _firstPersonArmRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                collider.enabled = false;
            }
        }
        _spectateCamera = FindSpectateCamera();
        _spectateCameraTransform = _spectateCamera != null ? _spectateCamera.transform : null;
        _playerHudDocument = GetComponentInChildren<UIDocument>(true);
        _cinemachineBrain = _gameplayCamera != null ? _gameplayCamera.GetComponent<CinemachineBrain>() : null;
        _gameplayAudioListener = _gameplayCamera != null ? _gameplayCamera.GetComponent<AudioListener>() : null;
        _spectateAudioListener = _spectateCamera != null ? _spectateCamera.GetComponent<AudioListener>() : null;
        ConfigureGameplayVisibilityCamera(_playerCamera);
        ConfigureGameplayVisibilityCamera(_gameplayCamera);
        ConfigureGameplayVisibilityCamera(_spectateCamera);
        _defaultNearClipPlane = _gameplayCamera != null ? _gameplayCamera.nearClipPlane : 0.3f;
        _defaultFieldOfView = _gameplayCamera != null ? _gameplayCamera.fieldOfView : 60f;
        SetSpectateCameraActive(false);
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
            RegisterPlayerCollisionIgnore();
        }

        _footstepAudioSource = GetComponent<AudioSource>();
        if (_footstepAudioSource == null)
        {
            _footstepAudioSource = gameObject.AddComponent<AudioSource>();
        }

        _footstepAudioSource.playOnAwake = false;
        _footstepAudioSource.loop = false;
        _footstepAudioSource.spatialBlend = 0f;
        _footstepAudioSource.dopplerLevel = 0f;
        _footstepAudioSource.volume = _footstepVolume;

        _pickupAudioSource = gameObject.AddComponent<AudioSource>();
        _pickupAudioSource.playOnAwake = false;
        _pickupAudioSource.loop = false;
        _pickupAudioSource.spatialBlend = 0f;
        _pickupAudioSource.dopplerLevel = 0f;

        _hurtAudioSource = gameObject.AddComponent<AudioSource>();
        _hurtAudioSource.playOnAwake = false;
        _hurtAudioSource.loop = false;
        _hurtAudioSource.spatialBlend = 0f;
        _hurtAudioSource.dopplerLevel = 0f;

        _gunshotAudioSource = gameObject.AddComponent<AudioSource>();
        _gunshotAudioSource.playOnAwake = false;
        _gunshotAudioSource.loop = false;
        _gunshotAudioSource.spatialBlend = 0f;
        _gunshotAudioSource.dopplerLevel = 0f;
        _gunshotAudioSource.volume = _gunshotVolume;

        _gunshotClips = Resources.LoadAll<AudioClip>(_gunshotResourceFolder) ?? Array.Empty<AudioClip>();
        _currentHealth = _maxHealth;

        SetLocalCharacterAudio(true);
    }
    
    private void Start() {
        // UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        // UnityEngine.Cursor.visible = false;

        if (_isSimulationControlled)
        {
            ApplySimulationPresentationState();
            return;
        }

        _preferredViewMode = _startingViewMode;
        SetCameraView(_startingViewMode, true);
    }

    private void OnDestroy()
    {
        UnregisterPlayerCollisionIgnore();
        RestoreCarryCollisionIgnore();
    }
    #endregion

    #region Update
    private void Update() {
        _jumpedThisFrame = false;
        if (!_isSimulationControlled)
        {
            HandlePauseMenuToggle();
            CheckMapBounds();
        }

        if (_isSpectating)
        {
            UpdateSpectateMode();
            return;
        }

        if (!_playerLocomotionInput.InputEnabled) return;

        UpdateForcedCameraViewState();
        UpdateCrouchState();
        UpdateDownedCollisionShape();
        if (!_isSimulationControlled)
        {
            UpdateSpeedHud();
            UpdateAnimationDebugHud();
            UpdateCrosshairVisibility();
            UpdateCombatHud();
            UpdateNextbotWarningIndicator();
            UpdateInjuredInteractionPrompt();
            HandleInjuredInteractionInput();
            UpdateInjuredInteractionPromptPressedState();
            HandleCursorLock();
            HandleViewToggle();
        }

        UpdateAutoSprint();
        UpdateWallRunEligibility();
        UpdateWallRunState();
        if (!_isSimulationControlled)
        {
            UpdateZoom();
        }

        if (!_isSimulationControlled)
        {
            HandleCombatInput();
        }

        RefreshGroundProbeState();
        UpdateRampState();

        if (_isBeingCarried)
        {
            UpdateCarriedFollow();
            return;
        }

        if (HandleNextbotHitReaction())
        {
            return;
        }

        HandleVerticalMovement();
        HandleHorizontalMovement();
        ApplyCarryMovementClamp();

        Vector3 finalVelocity = _horizontalVelocity;
        finalVelocity.y = _verticalVelocity;

        _characterController.Move(finalVelocity * Time.deltaTime);
        RefreshGroundProbeState();
        UpdateFootstepAudio();
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

    private void CheckMapBounds()
    {
        if (_transform.position.y < -100f)
        {
            RespawnAtStart();
        }
    }

    private void RespawnAtStart()
    {
        if (NetworkManager.Instance != null)
        {
            var spawns = NetworkManager.Instance.GetConfiguredPlayerSpawnPositions();
            if (spawns != null && spawns.Count > 0)
            {
                int spawnIndex = UnityEngine.Random.Range(0, spawns.Count);
                Vector3 spawnPoint = spawns[spawnIndex];
                
                if (_characterController != null)
                {
                    bool wasEnabled = _characterController.enabled;
                    _characterController.enabled = false;
                    _transform.position = spawnPoint;
                    _characterController.enabled = wasEnabled;
                }
                else
                {
                    _transform.position = spawnPoint;
                }

                _verticalVelocity = 0f;
                _horizontalVelocity = Vector3.zero;
            }
        }
    }

    private void UpdateSpectateMode()
    {
        SetLocalSpectatorBodyVisible(false);
        UpdateSpectateTargetsIfNeeded();
        HandleSpectateTargetCyclingInput();
        UpdateSpectateLook();
        UpdateSpectateCameraFollow();
        UpdateSpeedHud();
        UpdateCrosshairVisibility();
        UpdateNextbotWarningIndicator();
    }

    private void UpdateSpectateLook()
    {
        if (_playerLocomotionInput == null)
        {
            return;
        }

        Vector2 lookInput = _playerLocomotionInput.LookInput;
        _cameraRotation.x += lookSenseH * lookInput.x;
        _cameraRotation.y = Mathf.Clamp(_cameraRotation.y - lookSenseV * lookInput.y, -lookLimitV, lookLimitV);
    }

    private void HandleViewToggle()
    {
        if (!_playerLocomotionInput.InputEnabled) return;
        if (ShouldForceThirdPersonView()) return;
        if (ShouldForceFirstPersonView()) return; // Prevent switching in combat mode

        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            CameraViewMode nextView =
                _currentViewMode == CameraViewMode.FirstPerson
                    ? CameraViewMode.ThirdPerson
                    : CameraViewMode.FirstPerson;

            _preferredViewMode = nextView;
            SetCameraView(nextView);
        }
    }

    private void HandleInjuredInteractionInput()
    {
        if (_isSimulationControlled)
        {
            ResetReviveHoldState();
            return;
        }

        if (_combatModeActive)
        {
            ResetReviveHoldState();
            return;
        }

        if (_isPauseMenuOpen || IsInjuredOrHitReacting() || Keyboard.current == null)
        {
            ResetReviveHoldState();
            return;
        }

        if (_isCarryingPlayer)
        {
            ResetReviveHoldState();

            if (Keyboard.current.qKey.wasPressedThisFrame && !string.IsNullOrEmpty(_carriedPlayerSessionId))
            {
                SendCarryRequest(_carriedPlayerSessionId);
            }

            return;
        }

        bool hasReviveTarget = TryGetLookedAtRevivePlayer(out _, out string reviveTargetSessionId);
        bool hasCarryTarget = TryGetLookedAtCarryPlayer(out _, out string carryTargetSessionId);
        if (!hasReviveTarget && !hasCarryTarget)
        {
            ResetReviveHoldState();
            return;
        }

        if (hasReviveTarget)
        {
            UpdateReviveHoldState(reviveTargetSessionId, Keyboard.current.eKey.isPressed);
        }
        else
        {
            ResetReviveHoldState();
        }

        if (hasCarryTarget && Keyboard.current.qKey.wasPressedThisFrame)
        {
            SendCarryRequest(carryTargetSessionId);
        }
    }

    private void UpdateCarriedFollow()
    {
        _horizontalVelocity = Vector3.zero;
        _verticalVelocity = 0f;
        _runHeldTime = 0f;
        _isCrouching = false;
        _isWallRunning = false;
        _wallRunSide = 0;
        _wallRunNormal = Vector3.zero;
        _wallRunContactHoldTimer = 0f;
        _wallRunSprintGraceTimer = 0f;

        if (!TryGetCarriedFollowPose(out Vector3 targetPosition, out Quaternion targetRotation))
        {
            return;
        }

        Vector3 nextPosition = targetPosition;
        _transform.position = nextPosition;
        _transform.rotation = targetRotation;
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
            UpdateForcedCameraViewState(forceImmediate: true);
        }
        else
        {
            UpdateForcedCameraViewState(forceImmediate: true);
        }
    }

    private void UpdateForcedCameraViewState(bool forceImmediate = false)
    {
        if (_isSpectating)
        {
            SetThirdPersonCameraActive(false);
            SetSpectateCameraActive(true);
            SetLocalSpectatorBodyVisible(false);
            return;
        }

        bool shouldForceThirdPerson = ShouldForceThirdPersonView();
        bool shouldForceFirstPerson = ShouldForceFirstPersonView();

        if (shouldForceThirdPerson)
        {
            if (!_isTemporaryThirdPersonForced)
            {
                _preferredViewMode = _currentViewMode;
                _isTemporaryThirdPersonForced = true;
            }

            if (_currentViewMode != CameraViewMode.ThirdPerson)
            {
                SetCameraView(CameraViewMode.ThirdPerson, forceImmediate);
            }
            return;
        }
        else if (shouldForceFirstPerson)
        {
            if (_currentViewMode != CameraViewMode.FirstPerson)
            {
                SetCameraView(CameraViewMode.FirstPerson, forceImmediate);
            }
            return;
        }

        if (!_isTemporaryThirdPersonForced)
        {
            return;
        }

        _isTemporaryThirdPersonForced = false;
        SetCameraView(_preferredViewMode, forceImmediate);
    }

    private bool ShouldForceThirdPersonView()
    {
        return IsInjuredOrHitReacting() || _isCarryingPlayer || _isBeingCarried;
    }

    private bool ShouldForceFirstPersonView()
    {
        return _combatModeActive;
    }

    private void UpdateAutoSprint()
    {
        if (IsInjured() || (IsCrouching() && !_isCrouchRunning) || _isCarryingPlayer || _isBeingCarried)
        {
            _runHeldTime = 0f;
            return;
        }

        if (_isCrouchRunning)
        {
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

        if (_isSpectating)
        {
            return;
        }

        if (_isSimulationControlled)
        {
            UpdateSimulationVisuals();
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
            SyncFirstPersonOnlyRootsToGameplayCamera();
            UpdateSprintArmPose();
            UpdateArmWallClipVisibility();
            return;
        }

        Vector2 lookInput = _playerLocomotionInput.LookInput;
        float minPitch = _currentViewMode == CameraViewMode.FirstPerson ? -_firstPersonLookUpLimit : -lookLimitV;
        float maxPitch = _currentViewMode == CameraViewMode.FirstPerson ? _firstPersonLookDownLimit : lookLimitV;
        bool lookBackHeld = !_isSimulationControlled && Keyboard.current != null && Keyboard.current.rKey.isPressed;
        
        _cameraRotation.x += lookSenseH * lookInput.x;
        _cameraRotation.y = Mathf.Clamp(_cameraRotation.y - lookSenseV * lookInput.y, minPitch, maxPitch);

        bool allowBodyLookRotation = !IsInjured() && !_isBeingCarried;
        if (allowBodyLookRotation)
        {
            _playerRotationY += lookSenseH * lookInput.x;
            _transform.rotation = Quaternion.Euler(0f, _playerRotationY, 0f);
        }

        if (_isBeingCarried)
        {
            ResetInjuredVisualRootRotation();
        }
        else if (IsInjured())
        {
            UpdateInjuredFacing();
        }
        else if (IsCrouching() || _isCarryingPlayer)
        {
            UpdateCrouchFacing();
        }
        else
        {
            ResetInjuredVisualRootRotation();
        }

        UpdateDownedVisualRootPosition();
        float cameraYawOffset = allowBodyLookRotation ? 0f : _cameraRotation.x;
        if (lookBackHeld)
        {
            cameraYawOffset += 180f;
        }
        _cameraTransform.localRotation = Quaternion.Euler(_cameraRotation.y, cameraYawOffset, 0f);
        UpdateSprintCameraBob();
        UpdateFirstPersonWallRunCameraPose();
        ResolveCameraWallCollision();
        SyncFirstPersonOnlyRootsToGameplayCamera();
        UpdateSprintArmPose();
        UpdateArmWallClipVisibility();
    }

    private void UpdateSimulationVisuals()
    {
        Vector2 lookInput = _playerLocomotionInput != null ? _playerLocomotionInput.LookInput : Vector2.zero;
        _cameraRotation.x += lookSenseH * lookInput.x;
        _cameraRotation.y = Mathf.Clamp(_cameraRotation.y - lookSenseV * lookInput.y, -lookLimitV, lookLimitV);

        if (!_isBeingCarried && !IsInjured())
        {
            _playerRotationY += lookSenseH * lookInput.x;
            _transform.rotation = Quaternion.Euler(0f, _playerRotationY, 0f);
        }

        if (_isHitReacting)
        {
            UpdateNextbotHitReactionVisual();
        }
        else if (_isBeingCarried)
        {
            ResetInjuredVisualRootRotation();
        }
        else if (IsInjured())
        {
            UpdateInjuredFacing();
        }
        else if (IsCrouching() || _isCarryingPlayer)
        {
            UpdateCrouchFacing();
        }
        else
        {
            ResetInjuredVisualRootRotation();
        }

        UpdateDownedVisualRootPosition();
        SyncFirstPersonOnlyRootsToGameplayCamera();
        UpdateSprintArmPose();
        EnsureRemoteFullBodyVisible();
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
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        System.Collections.Generic.List<Renderer> standardRenderers = new();
        System.Collections.Generic.List<Renderer> firstPersonOnlyRenderers = new();

        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer renderer = allRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (IsFirstPersonOnlyRenderer(renderer))
            {
                firstPersonOnlyRenderers.Add(renderer);
            }
            else
            {
                standardRenderers.Add(renderer);
            }
        }

        _localRenderers = standardRenderers.ToArray();
        _firstPersonOnlyRenderers = firstPersonOnlyRenderers.ToArray();
        _defaultShadowCastingModes = new ShadowCastingMode[_localRenderers.Length];
        _defaultRendererEnabledStates = new bool[_localRenderers.Length];
        _defaultFirstPersonOnlyRendererEnabledStates = new bool[_firstPersonOnlyRenderers.Length];

        for (int i = 0; i < _localRenderers.Length; i++)
        {
            _defaultShadowCastingModes[i] = _localRenderers[i].shadowCastingMode;
            _defaultRendererEnabledStates[i] = _localRenderers[i].enabled;
        }

        for (int i = 0; i < _firstPersonOnlyRenderers.Length; i++)
        {
            _defaultFirstPersonOnlyRendererEnabledStates[i] = _firstPersonOnlyRenderers[i] != null && _firstPersonOnlyRenderers[i].enabled;
        }

        CacheFirstPersonOnlyRoots();
        CacheFirstPersonHiddenRenderers();
    }

    private void CacheFirstPersonOnlyRoots()
    {
        if (_firstPersonOnlyRootNames == null || _firstPersonOnlyRootNames.Length == 0)
        {
            _firstPersonOnlyRoots = Array.Empty<Transform>();
            _firstPersonOnlyRootLocalPositions = Array.Empty<Vector3>();
            _firstPersonOnlyRootLocalRotations = Array.Empty<Quaternion>();
            _firstPersonOnlyRootLocalScales = Array.Empty<Vector3>();
            return;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        System.Collections.Generic.List<Transform> roots = new();

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null)
            {
                continue;
            }

            for (int j = 0; j < _firstPersonOnlyRootNames.Length; j++)
            {
                string rootName = _firstPersonOnlyRootNames[j];
                if (string.IsNullOrWhiteSpace(rootName))
                {
                    continue;
                }

                if (string.Equals(candidate.name, rootName, StringComparison.OrdinalIgnoreCase))
                {
                    roots.Add(candidate);
                    break;
                }
            }
        }

        _firstPersonOnlyRoots = roots.ToArray();
        _firstPersonOnlyRootLocalPositions = new Vector3[_firstPersonOnlyRoots.Length];
        _firstPersonOnlyRootLocalRotations = new Quaternion[_firstPersonOnlyRoots.Length];
        _firstPersonOnlyRootLocalScales = new Vector3[_firstPersonOnlyRoots.Length];

        for (int i = 0; i < _firstPersonOnlyRoots.Length; i++)
        {
            Transform root = _firstPersonOnlyRoots[i];
            _firstPersonOnlyRootLocalPositions[i] = root.localPosition;
            _firstPersonOnlyRootLocalRotations[i] = root.localRotation;
            _firstPersonOnlyRootLocalScales[i] = root.localScale;
        }
    }

    private bool IsFirstPersonOnlyRenderer(Renderer renderer)
    {
        if (renderer == null || _firstPersonOnlyRootNames == null)
        {
            return false;
        }

        Transform current = renderer.transform;
        while (current != null && current != _transform)
        {
            for (int i = 0; i < _firstPersonOnlyRootNames.Length; i++)
            {
                string rootName = _firstPersonOnlyRootNames[i];
                if (string.IsNullOrWhiteSpace(rootName))
                {
                    continue;
                }

                if (string.Equals(current.name, rootName, StringComparison.OrdinalIgnoreCase))
                {
                    // Specifically exclude the body mesh even if it's under an "arm" root
                    if (renderer.gameObject.name.Contains("Body", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    return true;
                }
            }

            current = current.parent;
        }

        return false;
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

            if (_carryLeftAnchorTransform == null && string.Equals(child.name, _carryLeftAnchorBoneName, StringComparison.OrdinalIgnoreCase))
            {
                _carryLeftAnchorTransform = child;
            }

            if (_carryRightAnchorTransform == null && string.Equals(child.name, _carryRightAnchorBoneName, StringComparison.OrdinalIgnoreCase))
            {
                _carryRightAnchorTransform = child;
            }

            if (_leftArmTransform != null
                && _rightArmTransform != null
                && _carryLeftAnchorTransform != null
                && _carryRightAnchorTransform != null)
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
        _localHealthContainerElement = root.Q<VisualElement>("local-health-container");
        _localHealthLabelElement = root.Q<Label>("local-health-label");
        _localHealthFillElement = root.Q<VisualElement>("local-health-fill");
        _enemyHealthOverlayElement = root.Q<VisualElement>("enemy-health-overlay");
        _crosshairDotElement = root.Q<VisualElement>("crosshair-dot");
        _nextbotWarningIndicatorElement = root.Q<VisualElement>("nextbot-warning-indicator");
        _nextbotWarningArrowElement = root.Q<VisualElement>("nextbot-warning-arrow");
        _nextbotWarningSkullElement = root.Q<VisualElement>("nextbot-warning-skull");
        _injuredInteractionPromptElement = root.Q<VisualElement>("injured-interaction-prompt");
        _reviveActionRowElement = root.Q<VisualElement>("revive-action-row");
        _carryActionRowElement = root.Q<VisualElement>("carry-action-row");
        _reviveActionFillElement = root.Q<VisualElement>("revive-action-fill");
        _carryActionFillElement = root.Q<VisualElement>("carry-action-fill");
        _pauseMenuElement = root.Q<VisualElement>("pause-menu");
        _continueButton = root.Q<Button>("continue-button");
        _mainMenuButton = root.Q<Button>("main-menu-button");
        _respawnButton = root.Q<Button>("respawn-button");
        _pauseGraphicsLowButton = root.Q<Button>("pause-graphics-low-button");
        _pauseGraphicsMediumButton = root.Q<Button>("pause-graphics-medium-button");
        _pauseVolumeSlider = root.Q<SliderInt>("pause-volume-slider");
        _pauseGraphicsValueLabel = root.Q<Label>("pause-graphics-value-label");

        EnsurePickupFadeOverlay(root);

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

            if (_pauseGraphicsLowButton != null)
            {
                _pauseGraphicsLowButton.clicked += OnPauseGraphicsLowButtonClicked;
            }

            if (_pauseGraphicsMediumButton != null)
            {
                _pauseGraphicsMediumButton.clicked += OnPauseGraphicsMediumButtonClicked;
            }

            if (_pauseVolumeSlider != null)
            {
                _pauseVolumeSlider.RegisterValueChangedCallback(OnPauseVolumeSliderChanged);
            }

            _hudEventsBound = true;
        }

        SetPauseMenuDisplay(_isPauseMenuOpen);
        RefreshPauseMenuSettingsUi();
        ConfigureNextbotWarningVisuals();
    }

    private void EnsurePickupFadeOverlay(VisualElement root)
    {
        if (root == null)
        {
            return;
        }

        _pickupFadeOverlayElement = root.Q<VisualElement>("pickup-fade-overlay");
        if (_pickupFadeOverlayElement != null)
        {
            return;
        }

        _pickupFadeOverlayElement = new VisualElement
        {
            name = "pickup-fade-overlay",
            pickingMode = PickingMode.Ignore
        };

        _pickupFadeOverlayElement.style.position = Position.Absolute;
        _pickupFadeOverlayElement.style.left = 0f;
        _pickupFadeOverlayElement.style.right = 0f;
        _pickupFadeOverlayElement.style.top = 0f;
        _pickupFadeOverlayElement.style.bottom = 0f;
        _pickupFadeOverlayElement.style.backgroundColor = _pickupFadeColor;
        _pickupFadeOverlayElement.style.opacity = 0f;
        _pickupFadeOverlayElement.style.display = DisplayStyle.None;

        root.Add(_pickupFadeOverlayElement);
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

        if (_isSpectating)
        {
            SpectateTarget? activeTarget = GetActiveSpectateTarget();
            _speedLabel.text = activeTarget.HasValue
                ? $"Spectating {activeTarget.Value.Label}"
                : "Spectating";
            return;
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

        if (_combatModeActive)
        {
            SetInjuredInteractionPromptVisible(false);
            return;
        }

        if (_isSpectating || _isPauseMenuOpen || IsInjuredOrHitReacting() || _isCarryingPlayer || _isBeingCarried)
        {
            SetInjuredInteractionPromptVisible(false);
            return;
        }

        if (TryGetLookedAtRevivePlayer(out _, out _) || TryGetLookedAtCarryPlayer(out _, out _))
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

        bool shouldShowCrosshair = !_isSpectating && !_isPauseMenuOpen && !IsInjuredOrHitReacting();
        _crosshairDotElement.style.display = shouldShowCrosshair ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void UpdateCombatHud()
    {
        if (_localHealthContainerElement == null || _enemyHealthOverlayElement == null)
        {
            CacheHudElements();
            if (_localHealthContainerElement == null || _enemyHealthOverlayElement == null)
            {
                return;
            }
        }

        bool showCombatHud = _combatModeActive && !_isSpectating;
        _localHealthContainerElement.style.display = showCombatHud ? DisplayStyle.Flex : DisplayStyle.None;
        _enemyHealthOverlayElement.style.display = showCombatHud ? DisplayStyle.Flex : DisplayStyle.None;

        if (!showCombatHud)
        {
            HideAllEnemyHealthBars();
            return;
        }

        float maxHealth = Mathf.Max(1f, _maxHealth);
        float normalizedHealth = Mathf.Clamp01(_currentHealth / maxHealth);
        if (_localHealthLabelElement != null)
        {
            _localHealthLabelElement.text = $"{Mathf.CeilToInt(_currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        }

        if (_localHealthFillElement != null)
        {
            _localHealthFillElement.style.width = Length.Percent(normalizedHealth * 100f);
            _localHealthFillElement.style.backgroundColor = GetHealthColor(normalizedHealth);
        }

        UpdateEnemyHealthBars();
    }

    private void UpdateEnemyHealthBars()
    {
        if (_enemyHealthOverlayElement == null || _gameplayCamera == null)
        {
            return;
        }

        OfflinePlayerIdentity[] identities = FindObjectsByType<OfflinePlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        System.Collections.Generic.HashSet<string> activeSessionIds = new();

        for (int i = 0; i < identities.Length; i++)
        {
            OfflinePlayerIdentity identity = identities[i];
            if (identity == null
                || identity.IsLocalPlayer
                || string.IsNullOrWhiteSpace(identity.SessionId))
            {
                continue;
            }

            PlayerController targetController = identity.GetComponent<PlayerController>();
            if (targetController == null
                || !targetController.IsCombatModeActive
                || targetController.IsEliminatedStateActive)
            {
                continue;
            }

            Vector3 worldAnchor = identity.transform.position + Vector3.up * 2.75f;
            Vector3 screenPoint = _gameplayCamera.WorldToScreenPoint(worldAnchor);
            if (screenPoint.z <= 0f)
            {
                continue;
            }

            float rootWidth = _enemyHealthOverlayElement.resolvedStyle.width;
            float rootHeight = _enemyHealthOverlayElement.resolvedStyle.height;
            if (rootWidth <= 1f || rootHeight <= 1f)
            {
                return;
            }

            float uiX = screenPoint.x;
            float uiY = rootHeight - screenPoint.y;
            if (uiX < -140f || uiX > rootWidth + 140f || uiY < -40f || uiY > rootHeight + 40f)
            {
                continue;
            }

            activeSessionIds.Add(identity.SessionId);
            EnemyHealthBarView view = GetOrCreateEnemyHealthBar(identity.SessionId);
            if (view == null)
            {
                continue;
            }

            float normalizedHealth = Mathf.Clamp01(targetController.CurrentHealth / Mathf.Max(1f, targetController.MaxHealth));
            view.Root.style.display = DisplayStyle.Flex;
            view.Root.style.left = uiX;
            view.Root.style.top = uiY;
            view.Label.text = $"{identity.DisplayName}  {Mathf.CeilToInt(targetController.CurrentHealth)}";
            view.Fill.style.width = Length.Percent(normalizedHealth * 100f);
            view.Fill.style.backgroundColor = GetHealthColor(normalizedHealth);
        }

        foreach (System.Collections.Generic.KeyValuePair<string, EnemyHealthBarView> pair in _enemyHealthBarViews)
        {
            if (!activeSessionIds.Contains(pair.Key) && pair.Value?.Root != null)
            {
                pair.Value.Root.style.display = DisplayStyle.None;
            }
        }
    }

    private EnemyHealthBarView GetOrCreateEnemyHealthBar(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || _enemyHealthOverlayElement == null)
        {
            return null;
        }

        if (_enemyHealthBarViews.TryGetValue(sessionId, out EnemyHealthBarView existingView) && existingView?.Root != null)
        {
            return existingView;
        }

        VisualElement root = new VisualElement
        {
            pickingMode = PickingMode.Ignore
        };
        root.style.position = Position.Absolute;
        root.style.width = 148f;
        root.style.height = 32f;
        root.style.translate = new Translate(new Length(-50f, LengthUnit.Percent), new Length(0f, LengthUnit.Pixel));
        root.style.paddingLeft = 8f;
        root.style.paddingRight = 8f;
        root.style.paddingTop = 6f;
        root.style.paddingBottom = 6f;
        root.style.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.76f);
        root.style.borderTopLeftRadius = 8f;
        root.style.borderTopRightRadius = 8f;
        root.style.borderBottomLeftRadius = 8f;
        root.style.borderBottomRightRadius = 8f;
        root.style.display = DisplayStyle.None;

        Label label = new Label();
        label.style.color = Color.white;
        label.style.fontSize = 11f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.marginBottom = 4f;
        root.Add(label);

        VisualElement track = new VisualElement();
        track.style.height = 8f;
        track.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
        track.style.borderTopLeftRadius = 999f;
        track.style.borderTopRightRadius = 999f;
        track.style.borderBottomLeftRadius = 999f;
        track.style.borderBottomRightRadius = 999f;

        VisualElement fill = new VisualElement();
        fill.style.height = Length.Percent(100f);
        fill.style.width = Length.Percent(100f);
        fill.style.backgroundColor = new Color(0.37f, 0.87f, 0.43f, 1f);
        fill.style.borderTopLeftRadius = 999f;
        fill.style.borderTopRightRadius = 999f;
        fill.style.borderBottomLeftRadius = 999f;
        fill.style.borderBottomRightRadius = 999f;
        track.Add(fill);
        root.Add(track);

        _enemyHealthOverlayElement.Add(root);

        EnemyHealthBarView view = new EnemyHealthBarView
        {
            Root = root,
            Fill = fill,
            Label = label,
        };
        _enemyHealthBarViews[sessionId] = view;
        return view;
    }

    private void HideAllEnemyHealthBars()
    {
        foreach (System.Collections.Generic.KeyValuePair<string, EnemyHealthBarView> pair in _enemyHealthBarViews)
        {
            if (pair.Value?.Root != null)
            {
                pair.Value.Root.style.display = DisplayStyle.None;
            }
        }
    }

    private static Color GetHealthColor(float normalizedHealth)
    {
        if (normalizedHealth > 0.6f)
        {
            return new Color(94f / 255f, 222f / 255f, 109f / 255f, 1f);
        }

        if (normalizedHealth > 0.3f)
        {
            return new Color(242f / 255f, 196f / 255f, 82f / 255f, 1f);
        }

        return new Color(239f / 255f, 84f / 255f, 84f / 255f, 1f);
    }

    private void UpdateNextbotWarningIndicator()
    {
        if (_nextbotWarningIndicatorElement == null)
        {
            CacheHudElements();
            if (_nextbotWarningIndicatorElement == null)
            {
                return;
            }
        }

        if (_isSpectating
            || _isPauseMenuOpen
            || _gameplayCamera == null
            || _playerHudDocument == null
            || !TryGetActiveNextbotWarningData(out Vector2 warningDirection, out float nearestDistance))
        {
            SetNextbotWarningIndicatorVisible(false);
            return;
        }

        VisualElement root = _playerHudDocument.rootVisualElement;
        if (root == null)
        {
            SetNextbotWarningIndicatorVisible(false);
            return;
        }

        float rootWidth = root.resolvedStyle.width;
        float rootHeight = root.resolvedStyle.height;
        if (rootWidth <= 1f || rootHeight <= 1f)
        {
            SetNextbotWarningIndicatorVisible(false);
            return;
        }

        if (warningDirection.sqrMagnitude <= 0.0001f)
        {
            SetNextbotWarningIndicatorVisible(false);
            return;
        }

        Vector2 ringDirection = warningDirection.normalized;
        Vector2 ringCenter = new Vector2(rootWidth * 0.5f, rootHeight * 0.5f + _nextbotWarningRingVerticalOffset);
        float ringRadius = Mathf.Min(_nextbotWarningRingRadius, Mathf.Min(rootWidth, rootHeight) * 0.42f);
        Vector2 ringPosition = ringCenter + new Vector2(ringDirection.x, -ringDirection.y) * ringRadius;

        float indicatorWidth = Mathf.Max(1f, _nextbotWarningIndicatorElement.resolvedStyle.width);
        float indicatorHeight = Mathf.Max(1f, _nextbotWarningIndicatorElement.resolvedStyle.height);
        _nextbotWarningIndicatorElement.style.left = ringPosition.x - indicatorWidth * 0.5f;
        _nextbotWarningIndicatorElement.style.top = ringPosition.y - indicatorHeight * 0.5f;

        float proximity = 1f - Mathf.Clamp01(nearestDistance / Mathf.Max(0.01f, _nextbotWarningRange));
        float opacity = Mathf.Lerp(_nextbotWarningMinOpacity, _nextbotWarningMaxOpacity, proximity);
        float scaleValue = Mathf.Lerp(_nextbotWarningMinScale, _nextbotWarningMaxScale, proximity);
        _nextbotWarningIndicatorElement.style.opacity = opacity;
        _nextbotWarningIndicatorElement.style.scale = new StyleScale(new Scale(new Vector3(scaleValue, scaleValue, 1f)));

        if (_nextbotWarningArrowElement != null)
        {
            float arrowAngle = Mathf.Atan2(-ringDirection.y, ringDirection.x) * Mathf.Rad2Deg;
            float indicatorCenterX = indicatorWidth * 0.5f;
            float indicatorCenterY = indicatorHeight * 0.5f;
            Vector2 arrowDirection = new Vector2(ringDirection.x, -ringDirection.y).normalized;
            float arrowOffsetDistance = Mathf.Max(Mathf.Abs(_nextbotWarningArrowLeftOffset), Mathf.Abs(_nextbotWarningArrowVerticalOffset));
            _nextbotWarningArrowElement.style.left = indicatorCenterX + arrowDirection.x * arrowOffsetDistance;
            _nextbotWarningArrowElement.style.top = indicatorCenterY + arrowDirection.y * arrowOffsetDistance;
            _nextbotWarningArrowElement.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(arrowAngle)));
        }

        SetNextbotWarningIndicatorVisible(true);
    }

    private void ConfigureNextbotWarningVisuals()
    {
        if (_nextbotWarningArrowElement != null && _nextbotWarningArrowTexture != null)
        {
            _nextbotWarningArrowElement.style.backgroundImage = new StyleBackground(_nextbotWarningArrowTexture);
            _nextbotWarningArrowElement.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }

        if (_nextbotWarningSkullElement != null)
        {
            _nextbotWarningSkullElement.style.display = DisplayStyle.None;
        }
    }

    private void HandleCombatInput()
    {
        if (!_combatModeActive
            || _isPauseMenuOpen
            || _isSpectating
            || _isEliminatedState
            || IsInjuredOrHitReacting()
            || _isBeingCarried
            || _isCarryingPlayer
            || Mouse.current == null)
        {
            return;
        }

        if (Time.time < _nextAllowedShotTime || !Mouse.current.leftButton.isPressed)
        {
            return;
        }

        if (EventSystem.current != null
            && EventSystem.current.IsPointerOverGameObject()
            && UnityEngine.Cursor.lockState == CursorLockMode.None)
        {
            return;
        }

        FireCombatShot();
        _nextAllowedShotTime = Time.time + Mathf.Max(0.01f, _ak47FireInterval);
    }

    private void FireCombatShot()
    {
        PlayGunshotSound();

        if (!OfflineModeManager.TryGetExisting(out OfflineModeManager offlineModeManager)
            || !offlineModeManager.IsOfflineModeActive
            || offlineModeManager.CurrentPresentationMode != OfflineModeManager.OfflinePresentationMode.Shooting)
        {
            return;
        }

        Camera sourceCamera = _gameplayCamera != null ? _gameplayCamera : _playerCamera;
        if (sourceCamera == null)
        {
            return;
        }

        Ray shotRay = sourceCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        PlayMuzzleFlashEffect(shotRay.direction);
        PlayBulletParticleEffect(shotRay.direction);
        RaycastHit[] hits = Physics.RaycastAll(
            shotRay,
            Mathf.Max(1f, _ak47Range),
            _ak47HitLayers,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return;
        }

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        string attackerSessionId = GetComponent<OfflinePlayerIdentity>()?.SessionId ?? string.Empty;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            PlayerController hitController = hitCollider.GetComponentInParent<PlayerController>();
            if (hitController == null || hitController == this || hitController.IsEliminatedStateActive)
            {
                continue;
            }

            OfflinePlayerIdentity targetIdentity = hitController.GetComponent<OfflinePlayerIdentity>();
            if (targetIdentity == null
                || targetIdentity.IsLocalPlayer
                || string.IsNullOrWhiteSpace(targetIdentity.SessionId)
                || string.Equals(targetIdentity.SessionId, attackerSessionId, StringComparison.Ordinal))
            {
                continue;
            }

            offlineModeManager.TryApplyCombatDamage(targetIdentity.SessionId, _ak47Damage, attackerSessionId);
            return;
        }
    }

    private void PlayGunshotSound()
    {
        if (_gunshotAudioSource == null || _gunshotClips == null || _gunshotClips.Length == 0)
        {
            return;
        }

        int clipIndex = UnityEngine.Random.Range(0, _gunshotClips.Length);
        if (_gunshotClips.Length > 1 && clipIndex == _lastGunshotClipIndex)
        {
            clipIndex = (clipIndex + 1) % _gunshotClips.Length;
        }

        _lastGunshotClipIndex = clipIndex;
        AudioClip clip = _gunshotClips[clipIndex];
        if (clip == null)
        {
            return;
        }

        _gunshotAudioSource.PlayOneShot(clip, _gunshotVolume);
    }

    private void PlayBulletParticleEffect(Vector3 shotDirection)
    {
        if (_bulletVfxPrefab == null)
        {
            _bulletVfxPrefab = Resources.Load<GameObject>(_bulletVfxResourcePath);
            if (_bulletVfxPrefab != null)
            {
                _bulletParticlePrefabLocalRotation = _bulletVfxPrefab.transform.localRotation;
            }
        }

        if (_bulletVfxPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition;
        Quaternion spawnRotation;
        Vector3 direction;

        if (_bulletParticleSpawnPoint != null)
        {
            spawnPosition = _bulletParticleSpawnPoint.position;
            direction = shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : _bulletParticleSpawnPoint.forward;
            spawnRotation = Quaternion.LookRotation(direction, Vector3.up) * _bulletParticlePrefabLocalRotation;
        }
        else
        {
            Transform attachPoint = FindChildRecursive(transform, _ak47AttachPointName);
            if (attachPoint == null)
            {
                return;
            }

            direction = shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : attachPoint.forward;
            spawnPosition = GetMuzzleWorldPosition(attachPoint, direction, _bulletParticleLocalPosition, _bulletParticleMuzzleForwardOffset);
            spawnRotation = Quaternion.LookRotation(direction, Vector3.up) * _bulletParticlePrefabLocalRotation * Quaternion.Euler(_bulletParticleLocalEuler);
        }

        SpawnOneShotParticleEffect(_bulletVfxPrefab, spawnPosition, spawnRotation, true, direction);
    }

    private void PlayMuzzleFlashEffect(Vector3 shotDirection)
    {
        if (_muzzleFlashVfxPrefab == null)
        {
            _muzzleFlashVfxPrefab = Resources.Load<GameObject>(_muzzleFlashVfxResourcePath);
            if (_muzzleFlashVfxPrefab != null)
            {
                _muzzleFlashPrefabLocalRotation = _muzzleFlashVfxPrefab.transform.localRotation;
            }
        }

        if (_muzzleFlashVfxPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition;
        Quaternion spawnRotation;

        if (_muzzleFlashSpawnPoint != null)
        {
            spawnPosition = _muzzleFlashSpawnPoint.position;
            Vector3 direction = shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : _muzzleFlashSpawnPoint.forward;
            spawnRotation = Quaternion.LookRotation(direction, Vector3.up) * _muzzleFlashPrefabLocalRotation;
        }
        else
        {
            Transform attachPoint = FindChildRecursive(transform, _ak47AttachPointName);
            if (attachPoint == null)
            {
                return;
            }

            Vector3 direction = shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : attachPoint.forward;
            spawnPosition = GetMuzzleWorldPosition(attachPoint, direction, _muzzleFlashLocalPosition, _muzzleFlashForwardOffset);
            spawnRotation = Quaternion.LookRotation(direction, Vector3.up) * _muzzleFlashPrefabLocalRotation * Quaternion.Euler(_muzzleFlashLocalEuler);
        }

        SpawnOneShotParticleEffect(_muzzleFlashVfxPrefab, spawnPosition, spawnRotation, false, null);
    }

    private void SpawnOneShotParticleEffect(
        GameObject prefab,
        Vector3 worldPosition,
        Quaternion worldRotation,
        bool forceStraight,
        Vector3? explicitDirection)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject particleClone = Instantiate(prefab, worldPosition, worldRotation);
        particleClone.name = $"{prefab.name}_Shot";
        particleClone.transform.localScale = Vector3.one;

        ParticleSystem particleSystem = particleClone.GetComponent<ParticleSystem>();
        if (particleSystem == null)
        {
            Destroy(particleClone);
            return;
        }

        var main = particleSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        if (forceStraight)
        {
            var shape = particleSystem.shape;
            shape.alignToDirection = true;
            shape.angle = 0f;
            shape.radius = 0f;
            shape.radiusThickness = 0f;
            shape.randomDirectionAmount = 0f;
            shape.randomPositionAmount = 0f;
            shape.sphericalDirectionAmount = 0f;
        }

        particleSystem.Clear(true);
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (explicitDirection.HasValue)
        {
            Vector3 direction = explicitDirection.Value.sqrMagnitude > 0.0001f
                ? explicitDirection.Value.normalized
                : particleClone.transform.forward;
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = worldPosition,
                velocity = direction * GetParticleStartSpeed(main),
                applyShapeToPosition = false,
            };
            particleSystem.Emit(emitParams, 1);
        }
        else
        {
            particleSystem.Emit(1);
        }

        float cleanupDelay = main.duration + Mathf.Max(main.startLifetime.constantMax, 0.15f) + 0.25f;
        Destroy(particleClone, cleanupDelay);
    }

    private static float GetParticleStartSpeed(ParticleSystem.MainModule mainModule)
    {
        float speed = mainModule.startSpeed.constantMax;
        return speed > 0.01f ? speed : 1f;
    }

    private Vector3 GetMuzzleWorldPosition(
        Transform attachPoint,
        Vector3 shotDirection,
        Vector3 localOffset,
        float forwardOffset)
    {
        Vector3 direction = shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : attachPoint.forward;
        Vector3 muzzleAnchorWorld = attachPoint.TransformPoint(localOffset);
        Vector3 bestPosition = muzzleAnchorWorld;
        float bestDistance = Vector3.Dot(bestPosition - attachPoint.position, direction);

        Renderer[] gunRenderers = attachPoint.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < gunRenderers.Length; i++)
        {
            Renderer renderer = gunRenderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            Vector3 candidate = bounds.center + Vector3.Scale(
                bounds.extents,
                new Vector3(
                    Mathf.Sign(direction.x),
                    Mathf.Sign(direction.y),
                    Mathf.Sign(direction.z)));
            float candidateDistance = Vector3.Dot(candidate - attachPoint.position, direction);
            if (candidateDistance > bestDistance)
            {
                bestDistance = candidateDistance;
                bestPosition = candidate;
            }
        }

        return bestPosition + direction * Mathf.Max(0f, forwardOffset);
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, childName, StringComparison.Ordinal))
            {
                return child;
            }

            Transform nestedChild = FindChildRecursive(child, childName);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }

    private bool TryGetActiveNextbotWarningData(out Vector2 warningDirection, out float nearestDistance)
    {
        warningDirection = Vector2.zero;
        nearestDistance = float.PositiveInfinity;

        NetworkManager networkManager = NetworkManager.Instance;
        if (networkManager == null
            || networkManager.Room == null
            || networkManager.Room.State == null
            || networkManager.Room.State.nextbots == null)
        {
            return false;
        }

        bool foundTargetingNextbot = false;
        bool foundNearbyNextbot = false;
        string localSessionId = networkManager.LocalSessionId;
        Vector2 targetedDirection = Vector2.zero;
        Vector2 nearbyDirection = Vector2.zero;
        float nearestTargetedDistance = float.PositiveInfinity;
        float nearestNearbyDistance = float.PositiveInfinity;

        foreach (string key in networkManager.Room.State.nextbots.Keys)
        {
            NextbotState nextbotState = networkManager.Room.State.nextbots[key];
            if (nextbotState == null || !nextbotState.isActive)
            {
                continue;
            }

            Vector3 candidatePosition = new Vector3(nextbotState.x, nextbotState.y, nextbotState.z);
            Vector3 worldOffset = candidatePosition - _transform.position;
            worldOffset.y = 0f;

            float distance = worldOffset.magnitude;
            if (distance > _nextbotWarningRange || distance <= 0.001f)
            {
                continue;
            }

            Vector3 cameraRelativeOffset = _gameplayCamera.transform.InverseTransformDirection(worldOffset.normalized);
            Vector2 candidateDirection = new Vector2(cameraRelativeOffset.x, cameraRelativeOffset.z);
            if (candidateDirection.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            bool isTargetingLocalPlayer = !string.IsNullOrEmpty(localSessionId)
                && string.Equals(nextbotState.targetSessionId, localSessionId, StringComparison.Ordinal);

            if (isTargetingLocalPlayer && distance < nearestTargetedDistance)
            {
                nearestTargetedDistance = distance;
                targetedDirection = candidateDirection.normalized;
                foundTargetingNextbot = true;
            }

            if (distance < nearestNearbyDistance)
            {
                nearestNearbyDistance = distance;
                nearbyDirection = candidateDirection.normalized;
                foundNearbyNextbot = true;
            }
        }

        if (foundTargetingNextbot && targetedDirection.sqrMagnitude > 0.0001f)
        {
            warningDirection = targetedDirection;
            nearestDistance = nearestTargetedDistance;
            return true;
        }

        if (foundNearbyNextbot && nearbyDirection.sqrMagnitude > 0.0001f)
        {
            warningDirection = nearbyDirection;
            nearestDistance = nearestNearbyDistance;
            return true;
        }

        if (Time.time < _recentNextbotHitSourceExpiresAt
            && TryGetWarningDirectionFromWorldPosition(_recentNextbotHitSource, out Vector2 hitDirection, out float hitDistance))
        {
            warningDirection = hitDirection;
            nearestDistance = hitDistance;
            return true;
        }

        warningDirection = Vector2.zero;
        nearestDistance = float.PositiveInfinity;
        return false;
    }

    private bool TryGetWarningDirectionFromWorldPosition(Vector3 worldPosition, out Vector2 warningDirection, out float distance)
    {
        warningDirection = Vector2.zero;
        distance = float.PositiveInfinity;

        if (_transform == null || _gameplayCamera == null)
        {
            return false;
        }

        Vector3 worldOffset = worldPosition - _transform.position;
        worldOffset.y = 0f;

        distance = worldOffset.magnitude;
        if (distance <= 0.001f || distance > _nextbotWarningRange)
        {
            return false;
        }

        Vector3 cameraRelativeOffset = _gameplayCamera.transform.InverseTransformDirection(worldOffset.normalized);
        Vector2 candidateDirection = new Vector2(cameraRelativeOffset.x, cameraRelativeOffset.z);
        if (candidateDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        warningDirection = candidateDirection.normalized;
        return true;
    }

    private void SetNextbotWarningIndicatorVisible(bool visible)
    {
        if (_nextbotWarningIndicatorElement == null)
        {
            return;
        }

        _nextbotWarningIndicatorElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetInjuredInteractionPromptVisible(bool visible)
    {
        if (_injuredInteractionPromptElement == null)
        {
            return;
        }

        _injuredInteractionPromptElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (!visible)
        {
            ResetReviveHoldState();
            ApplyPromptActionRowStyle(_reviveActionRowElement, _reviveActionFillElement, false, 0f);
            ApplyPromptActionRowStyle(_carryActionRowElement, _carryActionFillElement, false, 0f);
        }
    }

    private void UpdateInjuredInteractionPromptPressedState()
    {
        if (_reviveActionRowElement == null || _carryActionRowElement == null)
        {
            CacheHudElements();
            if (_reviveActionRowElement == null || _carryActionRowElement == null)
            {
                return;
            }
        }

        if (_combatModeActive)
        {
            ApplyPromptActionRowStyle(_reviveActionRowElement, _reviveActionFillElement, false, 0f);
            ApplyPromptActionRowStyle(_carryActionRowElement, _carryActionFillElement, false, 0f);
            return;
        }

        bool promptVisible = _injuredInteractionPromptElement != null
            && _injuredInteractionPromptElement.resolvedStyle.display != DisplayStyle.None;
        bool canHighlight = (promptVisible || _isCarryingPlayer) && Keyboard.current != null;

        float reviveProgress = canHighlight ? Mathf.Clamp01(_reviveHoldTimer / Mathf.Max(_reviveHoldDuration, 0.01f)) : 0f;
        float carryProgress = 0f;

        ApplyPromptActionRowStyle(_reviveActionRowElement, _reviveActionFillElement, canHighlight && Keyboard.current.eKey.isPressed, reviveProgress);
        ApplyPromptActionRowStyle(_carryActionRowElement, _carryActionFillElement, canHighlight && Keyboard.current.qKey.isPressed, carryProgress);
    }

    private void ApplyPromptActionRowStyle(VisualElement rowElement, VisualElement fillElement, bool isPressed, float progress)
    {
        if (rowElement == null)
        {
            return;
        }

        StyleColor borderColor = isPressed
            ? new StyleColor(new Color(1f, 0.95f, 0.84f, 1f))
            : new StyleColor(new Color(224f / 255f, 173f / 255f, 98f / 255f, 0.95f));

        rowElement.style.backgroundColor = isPressed
            ? new StyleColor(new Color(1f, 1f, 1f, 0.2f))
            : new StyleColor(new Color(12f / 255f, 12f / 255f, 12f / 255f, 0.55f));
        rowElement.style.borderLeftColor = borderColor;
        rowElement.style.borderRightColor = borderColor;
        rowElement.style.borderTopColor = borderColor;
        rowElement.style.borderBottomColor = borderColor;
        rowElement.style.scale = new StyleScale(new Scale(isPressed ? new Vector3(1.05f, 1.05f, 1f) : Vector3.one));

        if (fillElement != null)
        {
            fillElement.style.width = new StyleLength(Length.Percent(Mathf.Clamp01(progress) * 100f));
        }
    }

    private void UpdateReviveHoldState(string targetSessionId, bool isHeld)
    {
        if (!isHeld)
        {
            ResetReviveHoldState();
            return;
        }

        float now = Time.unscaledTime;
        if (!string.Equals(_reviveHoldTargetSessionId, targetSessionId, StringComparison.Ordinal))
        {
            _reviveHoldTargetSessionId = targetSessionId;
            _reviveHoldStartedAt = now;
            _reviveHoldTimer = 0f;
            _reviveHoldTriggered = false;
            _nextReviveRequestAt = -1f;
        }

        if (_reviveHoldStartedAt < 0f)
        {
            _reviveHoldStartedAt = now;
        }

        _reviveHoldTimer = Mathf.Min(_reviveHoldDuration, now - _reviveHoldStartedAt);
        if (_reviveHoldTimer < _reviveHoldDuration)
        {
            return;
        }

        if (_reviveHoldTriggered && now < _nextReviveRequestAt)
        {
            return;
        }

        if (SendReviveRequest(targetSessionId))
        {
            _reviveHoldTriggered = true;
            _nextReviveRequestAt = now + Mathf.Max(0.05f, _reviveRetryInterval);
        }
    }

    private void ResetReviveHoldState()
    {
        _reviveHoldTimer = 0f;
        _reviveHoldStartedAt = -1f;
        _reviveHoldTargetSessionId = null;
        _reviveHoldTriggered = false;
        _nextReviveRequestAt = -1f;
    }

    private bool SendReviveRequest(string targetSessionId)
    {
        if (string.IsNullOrWhiteSpace(targetSessionId))
        {
            return false;
        }

        if (OfflineModeManager.TryGetExisting(out OfflineModeManager offlineModeManager) && offlineModeManager.IsOfflineModeActive)
        {
            return offlineModeManager.TryRevivePlayer(gameObject, targetSessionId);
        }

        NetworkManager.Instance?.SendReviveRequest(targetSessionId);
        return NetworkManager.Instance != null;
    }

    private void SendCarryRequest(string targetSessionId)
    {
        if (string.IsNullOrWhiteSpace(targetSessionId))
        {
            return;
        }

        if (OfflineModeManager.TryGetExisting(out OfflineModeManager offlineModeManager) && offlineModeManager.IsOfflineModeActive)
        {
            offlineModeManager.TryToggleCarryPlayer(gameObject, targetSessionId);
            return;
        }

        NetworkManager.Instance?.SendCarryRequest(targetSessionId);
    }

    public void ApplyNetworkCarryState(bool isCarrying, bool isBeingCarried, string carriedPlayerSessionId, string carrierSessionId)
    {
        bool wasCarryingPlayer = _isCarryingPlayer;
        bool wasBeingCarried = _isBeingCarried;
        bool carryStateChanged =
            wasCarryingPlayer != isCarrying
            || wasBeingCarried != isBeingCarried
            || !string.Equals(_carriedPlayerSessionId, carriedPlayerSessionId, StringComparison.Ordinal)
            || !string.Equals(_carrierSessionId, carrierSessionId, StringComparison.Ordinal);

        _isCarryingPlayer = isCarrying;
        _isBeingCarried = isBeingCarried;
        _carriedPlayerSessionId = carriedPlayerSessionId;
        _carrierSessionId = carrierSessionId;

        if (!_isBeingCarried)
        {
          _carrierSessionId = string.Empty;
        }

        if (!_isCarryingPlayer)
        {
          _carriedPlayerSessionId = string.Empty;
        }

        if (_isCarryingPlayer && (!wasCarryingPlayer || carryStateChanged))
        {
            _runHeldTime = 0f;
            _isCrouching = false;
            StopWallRun();
            _wallRunSprintGraceTimer = 0f;
            _horizontalVelocity = Vector3.Project(_horizontalVelocity, _transform.forward);
            if (_horizontalVelocity.magnitude > _carryMoveSpeed)
            {
                _horizontalVelocity = _horizontalVelocity.normalized * _carryMoveSpeed;
            }
        }

        if (_isBeingCarried && (!wasBeingCarried || carryStateChanged))
        {
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _runHeldTime = 0f;
            _isCrouching = false;
            _jumpedThisFrame = false;
            StopWallRun();
            _wallRunSprintGraceTimer = 0f;
        }

        if (carryStateChanged)
        {
            UpdateCarryCollisionIgnore();
            UpdateForcedCameraViewState(forceImmediate: true);
        }
        else
        {
            UpdateCarryCollisionIgnore();
        }
    }

    private void UpdateCarryCollisionIgnore()
    {
        if (_characterController == null)
        {
            return;
        }

        GameObject linkedPlayerObject = ResolveCarryLinkedPlayerObject();
        if (_ignoredCarryCollisionObject == linkedPlayerObject && _ignoredCarryCollisionColliders.Count > 0)
        {
            return;
        }

        RestoreCarryCollisionIgnore();

        if (linkedPlayerObject == null)
        {
            return;
        }

        Collider[] linkedColliders = linkedPlayerObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < linkedColliders.Length; i++)
        {
            Collider linkedCollider = linkedColliders[i];
            if (linkedCollider == null
                || linkedCollider == _characterController
                || !linkedCollider.enabled
                || !linkedCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            Physics.IgnoreCollision(_characterController, linkedCollider, true);
            _ignoredCarryCollisionColliders.Add(linkedCollider);
        }

        if (_ignoredCarryCollisionColliders.Count > 0)
        {
            _ignoredCarryCollisionObject = linkedPlayerObject;
        }
    }

    private void RestoreCarryCollisionIgnore()
    {
        bool restoredAnyCollision = false;
        if (_characterController != null)
        {
            for (int i = 0; i < _ignoredCarryCollisionColliders.Count; i++)
            {
                Collider linkedCollider = _ignoredCarryCollisionColliders[i];
                if (linkedCollider == null)
                {
                    continue;
                }

                Physics.IgnoreCollision(_characterController, linkedCollider, false);
                restoredAnyCollision = true;
            }
        }

        _ignoredCarryCollisionColliders.Clear();
        _ignoredCarryCollisionObject = null;

        if (restoredAnyCollision)
        {
            RefreshRegisteredPlayerCollisionIgnores();
        }
    }

    private void RegisterPlayerCollisionIgnore()
    {
        PruneRegisteredPlayerCollisionControllers();
        if (RegisteredPlayerCollisionControllers.Contains(this))
        {
            return;
        }

        for (int i = 0; i < RegisteredPlayerCollisionControllers.Count; i++)
        {
            SetPlayerPairCollisionIgnored(this, RegisteredPlayerCollisionControllers[i], true);
        }

        RegisteredPlayerCollisionControllers.Add(this);
    }

    private void UnregisterPlayerCollisionIgnore()
    {
        RegisteredPlayerCollisionControllers.Remove(this);
    }

    private static void RefreshRegisteredPlayerCollisionIgnores()
    {
        PruneRegisteredPlayerCollisionControllers();
        for (int i = 0; i < RegisteredPlayerCollisionControllers.Count; i++)
        {
            for (int j = i + 1; j < RegisteredPlayerCollisionControllers.Count; j++)
            {
                SetPlayerPairCollisionIgnored(
                    RegisteredPlayerCollisionControllers[i],
                    RegisteredPlayerCollisionControllers[j],
                    true);
            }
        }
    }

    private static void PruneRegisteredPlayerCollisionControllers()
    {
        for (int i = RegisteredPlayerCollisionControllers.Count - 1; i >= 0; i--)
        {
            if (RegisteredPlayerCollisionControllers[i] == null)
            {
                RegisteredPlayerCollisionControllers.RemoveAt(i);
            }
        }
    }

    private static void SetPlayerPairCollisionIgnored(PlayerController left, PlayerController right, bool ignored)
    {
        if (left == null || right == null || left == right)
        {
            return;
        }

        Collider[] leftColliders = left.GetComponentsInChildren<Collider>(true);
        Collider[] rightColliders = right.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < leftColliders.Length; i++)
        {
            Collider leftCollider = leftColliders[i];
            if (!CanIgnorePlayerCollider(leftCollider))
            {
                continue;
            }

            for (int j = 0; j < rightColliders.Length; j++)
            {
                Collider rightCollider = rightColliders[j];
                if (!CanIgnorePlayerCollider(rightCollider) || leftCollider == rightCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(leftCollider, rightCollider, ignored);
            }
        }
    }

    private static bool CanIgnorePlayerCollider(Collider playerCollider)
    {
        return playerCollider != null
            && playerCollider.enabled
            && playerCollider.gameObject.activeInHierarchy;
    }

    private GameObject ResolveCarryLinkedPlayerObject()
    {
        string linkedSessionId = _isCarryingPlayer
            ? _carriedPlayerSessionId
            : (_isBeingCarried ? _carrierSessionId : string.Empty);

        if (string.IsNullOrEmpty(linkedSessionId) || NetworkManager.Instance == null)
        {
            return null;
        }

        if (!NetworkManager.Instance.TryGetPlayerObject(linkedSessionId, out GameObject linkedPlayerObject) || linkedPlayerObject == null)
        {
            return null;
        }

        return linkedPlayerObject;
    }

    public bool TryGetCarriedFollowPose(out Vector3 targetPosition, out Quaternion targetRotation)
    {
        targetPosition = _transform.position;
        targetRotation = _transform.rotation;

        if (string.IsNullOrEmpty(_carrierSessionId) || NetworkManager.Instance == null)
        {
            return false;
        }

        if (!NetworkManager.Instance.TryGetPlayerObject(_carrierSessionId, out GameObject carrierObject) || carrierObject == null)
        {
            return false;
        }

        Transform carrierTransform = carrierObject.transform;
        PlayerController carrierController = carrierObject.GetComponent<PlayerController>();
        if (carrierController != null && carrierController.TryGetCarryAnchorPose(out Vector3 carryAnchorPosition))
        {
            targetPosition = carryAnchorPosition;
        }
        else
        {
            targetPosition = carrierTransform.TransformPoint(_carriedPlayerOffset);
        }

        if (carrierController != null && carrierController.TryGetCarryFacingRotation(out Quaternion carryFacingRotation))
        {
            targetRotation = carryFacingRotation;
        }
        else
        {
            targetRotation = Quaternion.Euler(0f, carrierTransform.eulerAngles.y, 0f);
        }
        return true;
    }

    public bool TryGetCarryAnchorPose(out Vector3 targetPosition)
    {
        Transform anchorTransform = _playerAnimation != null && _playerAnimation.VisualRootTransform != null
            ? _playerAnimation.VisualRootTransform
            : _transform;

        if (anchorTransform == null)
        {
            targetPosition = _transform.TransformPoint(_carriedPlayerOffset);
            return false;
        }

        targetPosition =
            anchorTransform.position
            + anchorTransform.right * _carriedPlayerAnchorOffset.x
            + anchorTransform.up * _carriedPlayerAnchorOffset.y
            + anchorTransform.forward * _carriedPlayerAnchorOffset.z;
        return true;
    }

    public bool TryGetCarryFacingRotation(out Quaternion targetRotation)
    {
        CacheInjuredVisualRoot();

        Transform facingTransform = _injuredVisualRoot != null
            ? _injuredVisualRoot
            : (_playerAnimation != null ? _playerAnimation.VisualRootTransform : _transform);

        if (facingTransform == null)
        {
            targetRotation = Quaternion.Euler(0f, _transform.eulerAngles.y, 0f);
            return false;
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(facingTransform.forward, Vector3.up);
        if (flatForward.sqrMagnitude <= 0.0001f)
        {
            flatForward = Vector3.ProjectOnPlane(_transform.forward, Vector3.up);
        }

        if (flatForward.sqrMagnitude <= 0.0001f)
        {
            targetRotation = Quaternion.Euler(0f, _transform.eulerAngles.y, 0f);
            return false;
        }

        targetRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        return true;
    }

    private bool TryGetLookedAtInjuredPlayer(out PlayerAnimation injuredPlayerAnimation, out string targetSessionId)
    {
        float interactionDistance = Mathf.Max(
            _injuredInteractionPromptDistance,
            Mathf.Max(_reviveInteractionPromptDistance, _carryInteractionPromptDistance));
        return TryGetInjuredInteractionTarget(interactionDistance, out injuredPlayerAnimation, out targetSessionId);
    }

    private bool TryGetLookedAtRevivePlayer(out PlayerAnimation injuredPlayerAnimation, out string targetSessionId)
    {
        float interactionDistance = Mathf.Max(_injuredInteractionPromptDistance, _reviveInteractionPromptDistance);
        return TryGetInjuredInteractionTarget(interactionDistance, out injuredPlayerAnimation, out targetSessionId);
    }

    private bool TryGetLookedAtCarryPlayer(out PlayerAnimation injuredPlayerAnimation, out string targetSessionId)
    {
        float interactionDistance = Mathf.Max(_injuredInteractionPromptDistance, _carryInteractionPromptDistance);
        return TryGetInjuredInteractionTarget(interactionDistance, out injuredPlayerAnimation, out targetSessionId);
    }

    private bool TryGetInjuredInteractionTarget(float interactionDistance, out PlayerAnimation injuredPlayerAnimation, out string targetSessionId)
    {
        injuredPlayerAnimation = null;
        targetSessionId = null;

        Transform rayOriginTransform = _gameplayCameraTransform != null ? _gameplayCameraTransform : _cameraTransform;
        if (rayOriginTransform == null || _transform == null)
        {
            return false;
        }

        float queryDistance = Mathf.Max(0.1f, interactionDistance);
        if (TryGetInjuredInteractionTargetFromCast(
            rayOriginTransform.position,
            rayOriginTransform.forward,
            queryDistance,
            0f,
            out injuredPlayerAnimation,
            out targetSessionId))
        {
            return true;
        }

        if (_currentViewMode != CameraViewMode.ThirdPerson)
        {
            return false;
        }

        Vector3 thirdPersonOrigin = _transform.position + Vector3.up * Mathf.Max(0f, _thirdPersonInteractionRayHeight);
        Vector3 thirdPersonDirection = _transform.forward;
        thirdPersonDirection.y = 0f;
        if (thirdPersonDirection.sqrMagnitude <= 0.0001f)
        {
            thirdPersonDirection = rayOriginTransform.forward;
            thirdPersonDirection.y = 0f;
        }

        if (thirdPersonDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        return TryGetInjuredInteractionTargetFromCast(
            thirdPersonOrigin,
            thirdPersonDirection.normalized,
            queryDistance,
            _thirdPersonInteractionRayRadius,
            out injuredPlayerAnimation,
            out targetSessionId);
    }

    private bool TryGetInjuredInteractionTargetFromCast(
        Vector3 origin,
        Vector3 direction,
        float distance,
        float radius,
        out PlayerAnimation injuredPlayerAnimation,
        out string targetSessionId)
    {
        injuredPlayerAnimation = null;
        targetSessionId = null;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 localPosition = _transform.position;
        RaycastHit[] hits = radius > 0f
            ? Physics.SphereCastAll(
                origin,
                radius,
                direction.normalized,
                distance,
                _cameraCollisionLayers,
                QueryTriggerInteraction.Ignore)
            : Physics.RaycastAll(
                origin,
                direction.normalized,
                distance,
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
                continue;
            }

            PlayerController candidateController = candidate.GetComponentInParent<PlayerController>();
            if (candidateController != null && candidateController.IsEliminatedStateActive)
            {
                continue;
            }

            NetworkPlayer candidateNetworkPlayer = candidate.GetComponent<NetworkPlayer>();
            if (candidateNetworkPlayer == null || !candidateNetworkPlayer.TryGetSessionId(out targetSessionId))
            {
                OfflinePlayerIdentity offlineIdentity = candidate.GetComponentInParent<OfflinePlayerIdentity>();
                if (offlineIdentity == null || string.IsNullOrWhiteSpace(offlineIdentity.SessionId))
                {
                    continue;
                }

                targetSessionId = offlineIdentity.SessionId;
            }

            Transform candidateTransform = candidate.transform;
            Vector3 offset = candidateTransform.position - localPosition;
            if (Mathf.Abs(offset.y) > _injuredInteractionPromptHeightTolerance)
            {
                continue;
            }

            injuredPlayerAnimation = candidate;
            return true;
        }

        return false;
    }

    public void ApplyNetworkRevive()
    {
        ApplyNetworkCarryState(false, false, string.Empty, string.Empty);
        _isEliminatedState = false;
        if (_combatModeActive)
        {
            _currentHealth = _maxHealth;
        }

        if (!_isSimulationControlled && _playerLocomotionInput != null)
        {
            _playerLocomotionInput.InputEnabled = true;
        }

        SetDebugInjuredState(false);
        ResetInjuredVisualRootRotation();
        UpdateDownedCollisionShape();
        UpdateDownedVisualRootPosition();
        UpdateForcedCameraViewState(forceImmediate: true);
    }

    public void ApplyNetworkInjured()
    {
        ApplyNetworkCarryState(false, false, string.Empty, string.Empty);
        _isEliminatedState = false;
        PlayPlayerGotHitSound();
        SetDebugInjuredState(true);
        _horizontalVelocity = Vector3.zero;
        _verticalVelocity = Mathf.Min(_verticalVelocity, 0f);
        _jumpedThisFrame = false;
        _isCrouching = false;
        _isWallRunning = false;
        _wallRunSide = 0;
        _wallRunNormal = Vector3.zero;
        _wallRunContactHoldTimer = 0f;
        _wallRunSprintGraceTimer = 0f;
        UpdateDownedCollisionShape();
        UpdateDownedVisualRootPosition();
        UpdateForcedCameraViewState(forceImmediate: true);
    }

    public void ApplyNetworkEliminated()
    {
        ApplyNetworkCarryState(false, false, string.Empty, string.Empty);
        _isEliminatedState = true;
        _currentHealth = 0f;
        if (!_isSimulationControlled && _playerLocomotionInput != null)
        {
            _playerLocomotionInput.InputEnabled = false;
        }

        SetDebugInjuredState(true);
        _horizontalVelocity = Vector3.zero;
        _verticalVelocity = 0f;
        _jumpedThisFrame = false;
        _isCrouching = false;
        _isWallRunning = false;
        _wallRunSide = 0;
        _wallRunNormal = Vector3.zero;
        _wallRunContactHoldTimer = 0f;
        _wallRunSprintGraceTimer = 0f;
        UpdateDownedCollisionShape();
        UpdateDownedVisualRootPosition();
        UpdateForcedCameraViewState(forceImmediate: true);
    }

    public void ApplyNetworkRoundReset(Vector3 worldPosition, float rotationY)
    {
        ApplyNetworkRevive();
        _currentHealth = _maxHealth;
        _isEliminatedState = false;

        _horizontalVelocity = Vector3.zero;
        _verticalVelocity = 0f;
        _jumpedThisFrame = false;
        _isWallRunning = false;
        _wallRunSide = 0;
        _wallRunNormal = Vector3.zero;
        _wallRunSprintGraceTimer = 0f;
        _wallRunContactHoldTimer = 0f;
        _nextbotHitImpactVelocity = Vector3.zero;
        _nextbotHitImpactTimer = 0f;

        Transform targetTransform = _transform != null ? _transform : transform;
        Quaternion targetRotation = Quaternion.Euler(0f, rotationY, 0f);

        if (_characterController != null)
        {
            bool wasEnabled = _characterController.enabled;
            _characterController.enabled = false;
            targetTransform.SetPositionAndRotation(worldPosition, targetRotation);
            _characterController.enabled = wasEnabled;
        }
        else
        {
            targetTransform.SetPositionAndRotation(worldPosition, targetRotation);
        }

        _playerRotationY = rotationY;
        UpdateForcedCameraViewState(forceImmediate: true);
    }

    public bool IsCarrying()
    {
        return _isCarryingPlayer;
    }

    public bool IsBeingCarried()
    {
        return _isBeingCarried;
    }

    public string GetCarriedPlayerSessionId()
    {
        return _carriedPlayerSessionId;
    }

    public string GetCarrierSessionId()
    {
        return _carrierSessionId;
    }

    private void SetPauseMenuVisible(bool visible)
    {
        if (_pauseMenuElement == null)
        {
            CacheHudElements();
        }

        _isPauseMenuOpen = visible;
        SetPauseMenuDisplay(visible);
        RefreshPauseMenuSettingsUi();

        bool shouldLockCursor = !visible;
        UnityEngine.Cursor.lockState = shouldLockCursor ? CursorLockMode.Locked : CursorLockMode.None;
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

    private void OnPauseGraphicsLowButtonClicked()
    {
        ApplyPauseMenuGraphicsQuality(WebGLPerformanceBootstrap.LowQualityName);
    }

    private void OnPauseGraphicsMediumButtonClicked()
    {
        ApplyPauseMenuGraphicsQuality(WebGLPerformanceBootstrap.MediumQualityName);
    }

    private void OnPauseVolumeSliderChanged(ChangeEvent<int> evt)
    {
        AudioListener.volume = Mathf.Clamp01(evt.newValue / 10f);
    }

    private void ApplyPauseMenuGraphicsQuality(string qualityName)
    {
        if (!WebGLPerformanceBootstrap.TryApplyManualGraphicsQuality(qualityName))
        {
            return;
        }

        RefreshPauseMenuSettingsUi();
    }

    private void RefreshPauseMenuSettingsUi()
    {
        if (_pauseVolumeSlider != null)
        {
            int volumeValue = Mathf.RoundToInt(Mathf.Clamp01(AudioListener.volume) * 10f);
            _pauseVolumeSlider.SetValueWithoutNotify(volumeValue);
        }

        string activeQualityName = WebGLPerformanceBootstrap.GetActiveGraphicsQualityName();
        string formattedQualityName = FormatPauseMenuQualityName(activeQualityName);

        if (_pauseGraphicsValueLabel != null)
        {
            _pauseGraphicsValueLabel.text = formattedQualityName;
        }

        if (_pauseGraphicsLowButton != null)
        {
            bool isLowSelected = string.Equals(activeQualityName, WebGLPerformanceBootstrap.LowQualityName, StringComparison.Ordinal);
            _pauseGraphicsLowButton.text = isLowSelected ? "Low Selected" : "Low";
            _pauseGraphicsLowButton.SetEnabled(!isLowSelected);
        }

        if (_pauseGraphicsMediumButton != null)
        {
            bool isMediumSelected = string.Equals(activeQualityName, WebGLPerformanceBootstrap.MediumQualityName, StringComparison.Ordinal);
            _pauseGraphicsMediumButton.text = isMediumSelected ? "Medium Selected" : "Medium";
            _pauseGraphicsMediumButton.SetEnabled(!isMediumSelected);
        }

        if (_respawnButton != null)
        {
            _respawnButton.SetEnabled(false);
        }
    }

    private static string FormatPauseMenuQualityName(string qualityName)
    {
        if (string.IsNullOrEmpty(qualityName))
        {
            return "Unknown";
        }

        if (qualityName.StartsWith("WebGL ", StringComparison.Ordinal))
        {
            return qualityName.Substring("WebGL ".Length);
        }

        return qualityName;
    }

    private Camera FindGameplayCamera()
    {
        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        foreach (Camera childCamera in cameras)
        {
            if (childCamera == null || childCamera == _playerCamera)
            {
                continue;
            }

            if (string.Equals(childCamera.gameObject.name, _spectateCameraName, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(childCamera.gameObject.tag, "MainCamera", StringComparison.Ordinal) || string.Equals(childCamera.gameObject.name, "Main Camera", StringComparison.Ordinal))
            {
                return childCamera;
            }
        }

        foreach (Camera childCamera in cameras)
        {
            if (childCamera == null || childCamera == _playerCamera)
            {
                continue;
            }

            if (string.Equals(childCamera.gameObject.name, _spectateCameraName, StringComparison.Ordinal))
            {
                continue;
            }

            return childCamera;
        }

        return _playerCamera;
    }

    private Camera FindSpectateCamera()
    {
        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        foreach (Camera childCamera in cameras)
        {
            if (childCamera != null && string.Equals(childCamera.gameObject.name, _spectateCameraName, StringComparison.Ordinal))
            {
                return childCamera;
            }
        }

        return null;
    }

    private void ConfigureGameplayVisibilityCamera(Camera targetCamera)
    {
        if (targetCamera == null)
        {
            return;
        }

        if (_minimumGameplayFarClipPlane > 0f && targetCamera.farClipPlane < _minimumGameplayFarClipPlane)
        {
            targetCamera.farClipPlane = _minimumGameplayFarClipPlane;
        }

        if (_disableGameplayOcclusionCulling)
        {
            targetCamera.useOcclusionCulling = false;
        }
    }

    private void SetCameraView(CameraViewMode newViewMode, bool force = false)
    {
        if (_isSpectating)
        {
            newViewMode = CameraViewMode.ThirdPerson;
        }

        if (ShouldForceThirdPersonView() && newViewMode == CameraViewMode.FirstPerson)
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

    private void SetSpectateCameraActive(bool isActive)
    {
        if (_spectateCamera != null)
        {
            _spectateCamera.enabled = isActive;
        }

        if (_spectateAudioListener != null)
        {
            _spectateAudioListener.enabled = isActive;
        }

        if (_gameplayCamera != null)
        {
            _gameplayCamera.enabled = !isActive;
        }

        if (_gameplayAudioListener != null)
        {
            _gameplayAudioListener.enabled = !isActive;
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

        return !hitTransform.IsChildOf(_transform) && !IsCarryLinkedTransform(hitTransform);
    }

    private bool IsCarryLinkedTransform(Transform candidate)
    {
        if (candidate == null || NetworkManager.Instance == null)
        {
            return false;
        }

        if (IsTransformLinkedToSession(candidate, _carriedPlayerSessionId))
        {
            return true;
        }

        return IsTransformLinkedToSession(candidate, _carrierSessionId);
    }

    private bool IsTransformLinkedToSession(Transform candidate, string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || NetworkManager.Instance == null)
        {
            return false;
        }

        if (!NetworkManager.Instance.TryGetPlayerObject(sessionId, out GameObject playerObject) || playerObject == null)
        {
            return false;
        }

        Transform linkedTransform = playerObject.transform;
        return candidate == linkedTransform || candidate.IsChildOf(linkedTransform);
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
            if (hitTransform == null || hitTransform.IsChildOf(_transform) || IsCarryLinkedTransform(hitTransform))
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
        // Handle explicit roots if assigned
        if (_firstPersonArmRoot != null)
        {
            _firstPersonArmRoot.SetActive(firstPerson);
        }

        if (_thirdPersonBodyRoot != null)
        {
            _thirdPersonBodyRoot.SetActive(!firstPerson);
            
            // Apply rotation offset to ensure the model faces the correct way
            if (!firstPerson)
            {
                _thirdPersonBodyRoot.transform.localRotation = Quaternion.Euler(_thirdPersonBodyRotationOffset);
            }
        }

        // Fallback to renderer caching for secondary parts (heads, accessories)
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
            
            // If we have explicit roots, we don't need to hide individual renderers here
            // unless they aren't part of those roots.
            if (_thirdPersonBodyRoot == null)
            {
                bool shouldBeEnabled = !firstPerson && _defaultRendererEnabledStates[i];
                renderer.enabled = shouldBeEnabled;
            }
        }

        SetFirstPersonHeadHidden(firstPerson);
        
        // Handle legacy arm syncing if explicit root isn't used
        if (_firstPersonArmRoot == null)
        {
            SetFirstPersonOnlyRenderersVisible(firstPerson);
        }
    }

    private void SetFirstPersonOnlyRenderersVisible(bool visible)
    {
        if (_firstPersonOnlyRenderers == null)
        {
            return;
        }

        for (int i = 0; i < _firstPersonOnlyRenderers.Length; i++)
        {
            Renderer renderer = _firstPersonOnlyRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = visible && _defaultFirstPersonOnlyRendererEnabledStates[i];
        }
    }

    private void SyncFirstPersonOnlyRootsToGameplayCamera()
    {
        if (_gameplayCameraTransform == null || _firstPersonOnlyRoots == null)
        {
            return;
        }

        float lookDownWeight = 0f;
        if (_currentViewMode == CameraViewMode.FirstPerson && _firstPersonLookDownLimit > 0.001f)
        {
            lookDownWeight = Mathf.Clamp01(Mathf.Max(0f, _cameraRotation.y) / _firstPersonLookDownLimit);
        }

        // Apply procedural offsets (look-down tilt and position adjustment)
        Vector3 basePositionOffset = _firstPersonArmsBasePositionOffset + (_firstPersonArmsLookDownPositionOffset * lookDownWeight);
        Quaternion baseRotationOffset = Quaternion.Euler(_firstPersonArmsBaseRotationOffset + (_firstPersonArmsLookDownRotationOffset * lookDownWeight));

        for (int i = 0; i < _firstPersonOnlyRoots.Length; i++)
        {
            Transform root = _firstPersonOnlyRoots[i];
            if (root == null)
            {
                continue;
            }

            // Force parentage to the current gameplay camera if it's lost
            if (root.parent != _gameplayCameraTransform)
            {
                root.SetParent(_gameplayCameraTransform, false);
            }

            // By explicitly setting localPosition every LateUpdate, we override any 
            // drifting caused by animators, physics, or floating point errors.
            root.localPosition = _firstPersonOnlyRootLocalPositions[i] + basePositionOffset;
            root.localRotation = _firstPersonOnlyRootLocalRotations[i] * baseRotationOffset;
            root.localScale = _firstPersonOnlyRootLocalScales[i];
        }
    }

    private void SetLocalSpectatorBodyVisible(bool visible)
    {
        if (_playerAnimation != null && _playerAnimation.VisualRootTransform != null)
        {
            if (!visible)
            {
                _spectateVisualRootWasActive = _playerAnimation.VisualRootTransform.gameObject.activeSelf;
                _playerAnimation.VisualRootTransform.gameObject.SetActive(false);
            }
            else
            {
                _playerAnimation.VisualRootTransform.gameObject.SetActive(_spectateVisualRootWasActive);
            }
        }

        if (_localRenderers != null)
        {
            for (int i = 0; i < _localRenderers.Length; i++)
            {
                Renderer renderer = _localRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = visible && _defaultRendererEnabledStates[i];
            }
        }

        if (_firstPersonHiddenRenderers != null)
        {
            for (int i = 0; i < _firstPersonHiddenRenderers.Length; i++)
            {
                Renderer renderer = _firstPersonHiddenRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = visible && _defaultHiddenRendererEnabledStates[i];
            }
        }

        SetFirstPersonOnlyRenderersVisible(false);

        if (_firstPersonWallHideRenderers != null)
        {
            for (int i = 0; i < _firstPersonWallHideRenderers.Length; i++)
            {
                Renderer renderer = _firstPersonWallHideRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = visible && _defaultWallHideRendererEnabledStates[i];
            }
        }

        if (_characterController != null)
        {
            if (!visible)
            {
                _spectateCharacterControllerWasEnabled = _characterController.enabled;
                _characterController.enabled = false;
            }
            else
            {
                _characterController.enabled = _spectateCharacterControllerWasEnabled;
            }
        }
    }

    private void MoveSpectatorBodyToHiddenPosition()
    {
        Transform targetTransform = _transform != null ? _transform : transform;

        if (_characterController != null)
        {
            bool wasEnabled = _characterController.enabled;
            _characterController.enabled = false;
            targetTransform.SetPositionAndRotation(SpectatorHiddenPosition, Quaternion.identity);
            _characterController.enabled = wasEnabled;
            return;
        }

        targetTransform.SetPositionAndRotation(SpectatorHiddenPosition, Quaternion.identity);
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

        float movementBasisYaw = GetMovementBasisYaw();
        Quaternion movementBasisRotation = Quaternion.Euler(0f, movementBasisYaw, 0f);
        Vector3 cameraForward = movementBasisRotation * Vector3.forward;
        Vector3 cameraRight = movementBasisRotation * Vector3.right;
        
        Vector3 cameraForwardXZ = new Vector3(cameraForward.x, 0f, cameraForward.z).normalized;
        Vector3 cameraRightXZ = new Vector3(cameraRight.x, 0f, cameraRight.z).normalized;
        
        Vector3 movementDirection = cameraForwardXZ * movementInput.y + cameraRightXZ * movementInput.x;
        float inputMagnitude = Mathf.Clamp01(movementInput.magnitude);
        float targetSpeed = GetCurrentMoveSpeed() * GetDirectionalSpeedMultiplier(movementInput) * inputMagnitude;

        if (_isCarryingPlayer)
        {
            HandleCarryMovement(movementDirection, inputMagnitude, targetSpeed, deltaTime);
            return;
        }

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

    private void HandleCarryMovement(Vector3 movementDirection, float inputMagnitude, float targetSpeed, float deltaTime)
    {
        _ = deltaTime;

        Vector3 desiredVelocity = Vector3.zero;
        if (movementDirection.sqrMagnitude > 0.001f && inputMagnitude > 0.001f)
        {
            desiredVelocity = movementDirection.normalized * targetSpeed;
        }

        _horizontalVelocity = desiredVelocity;
        _horizontalVelocity.y = 0f;
    }

    private void UpdateInjuredFacing()
    {
        Vector2 movementInput = GetInjuredFacingInput();
        if (movementInput.sqrMagnitude > 0.0001f)
        {
            _injuredFacingYaw = NormalizeSignedAngle(Mathf.Atan2(movementInput.x, movementInput.y) * Mathf.Rad2Deg + 180f);
        }

        UpdateVisualFacingYaw(_injuredFacingYaw);
    }

    private Vector2 GetInjuredFacingInput()
    {
        Vector2 movementInput = _playerLocomotionInput != null ? _playerLocomotionInput.MovementInput : Vector2.zero;
        if (movementInput.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        Vector3 worldMovementDirection = GetPlanarMovementDirection(movementInput);
        if (worldMovementDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        Vector3 localMovementDirection = _transform.InverseTransformDirection(worldMovementDirection);
        Vector2 localPlanarDirection = new Vector2(localMovementDirection.x, localMovementDirection.z);
        return localPlanarDirection.sqrMagnitude > 0.0001f
            ? localPlanarDirection.normalized
            : Vector2.zero;
    }

    private void UpdateCrouchFacing()
    {
        if (_currentViewMode == CameraViewMode.FirstPerson)
        {
            ResetInjuredVisualRootRotation();
            return;
        }

        UpdateDirectionalVisualFacing(GetDirectionalVisualFacingInput());
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
        UpdateVisualFacingYaw(localYaw);
    }

    private void UpdateVisualFacingFromMouse()
    {
        CacheInjuredVisualRoot();

        if (_injuredVisualRoot == null)
        {
            return;
        }

        float targetLocalYaw = GetMouseDrivenVisualYaw();
        UpdateVisualFacingYaw(targetLocalYaw);
    }

    private void UpdateVisualFacingYaw(float targetLocalYaw)
    {
        CacheInjuredVisualRoot();

        if (_injuredVisualRoot == null)
        {
            return;
        }

        float blend = 1f - Mathf.Exp(-_injuredRotationSharpness * Time.deltaTime);
        Quaternion targetLocalRotation = Quaternion.Euler(0f, targetLocalYaw, 0f) * _injuredVisualRootBaseLocalRotation;
        _injuredVisualRoot.localRotation = Quaternion.Slerp(_injuredVisualRoot.localRotation, targetLocalRotation, blend);
    }

    private float GetMouseDrivenVisualYaw()
    {
        float cameraYawOffset = _cameraRotation.x;
        bool lookBackHeld = Keyboard.current != null && Keyboard.current.rKey.isPressed;
        if (lookBackHeld)
        {
            cameraYawOffset += 180f;
        }

        return NormalizeSignedAngle(cameraYawOffset + 180f);
    }

    private Vector2 GetDirectionalVisualFacingInput()
    {
        Vector3 planarVelocity = _horizontalVelocity;
        planarVelocity.y = 0f;

        if (planarVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 localVelocity = _transform.InverseTransformDirection(planarVelocity);
            Vector2 localPlanarVelocity = new Vector2(localVelocity.x, localVelocity.z);
            if (localPlanarVelocity.sqrMagnitude > 0.0001f)
            {
                return localPlanarVelocity.normalized;
            }
        }

        return _playerLocomotionInput != null ? _playerLocomotionInput.MovementInput : Vector2.zero;
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
        else if (IsInjured() && !_isBeingCarried)
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
        if (_isBeingCarried || (!_isHitReacting && !IsInjured()) || _injuredVisualRoot == null || _characterController == null)
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
        EnsureRemoteFullBodyVisible();

        if (_isBeingCarried)
        {
            ResetInjuredVisualRootRotation();
        }
        else if (injured || crouching || _isCarryingPlayer)
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

    public void EnsureRemoteFullBodyVisible()
    {
        if (_playerAnimation != null && _playerAnimation.VisualRootTransform != null)
        {
            GameObject visualRootObject = _playerAnimation.VisualRootTransform.gameObject;
            if (visualRootObject != null && !visualRootObject.activeSelf)
            {
                visualRootObject.SetActive(true);
            }
        }

        SetLocalRenderMode(false);
        SetFirstPersonWallClipHidden(false);
    }

    public float GetVisualYaw()
    {
        CacheInjuredVisualRoot();

        if (_injuredVisualRoot == null)
        {
            return 180f;
        }

        if (IsInjured() && !_isBeingCarried)
        {
            return NormalizeSignedAngle(_injuredFacingYaw);
        }

        if ((IsInjured() || IsCrouching() || _isCarryingPlayer) && !_isBeingCarried)
        {
            Quaternion relativeFacing = Quaternion.Inverse(_injuredVisualRootBaseLocalRotation) * _injuredVisualRoot.localRotation;
            return NormalizeSignedAngle(relativeFacing.eulerAngles.y);
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

        if ((_isHitReacting || IsInjured()) && !_isBeingCarried)
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

        _recentNextbotHitSource = sourcePosition;
        _recentNextbotHitSourceExpiresAt = Time.time + Mathf.Max(0f, _nextbotHitWarningMemorySeconds);

        ClearOfflineCarryStateBeforeNextbotHit();
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

        PlayPlayerGotHitSound();
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
        UpdateForcedCameraViewState(forceImmediate: true);

        return true;
    }

    private void ClearOfflineCarryStateBeforeNextbotHit()
    {
        if ((!_isCarryingPlayer && !_isBeingCarried)
            || !OfflineModeManager.TryGetExisting(out OfflineModeManager offlineModeManager)
            || !offlineModeManager.IsOfflineModeActive)
        {
            return;
        }

        OfflinePlayerIdentity identity = GetComponent<OfflinePlayerIdentity>();
        if (identity == null || string.IsNullOrWhiteSpace(identity.SessionId))
        {
            return;
        }

        offlineModeManager.ClearCarryStateForPlayer(identity.SessionId);
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

    private void ApplyCarryMovementClamp()
    {
        if (!_isCarryingPlayer)
        {
            return;
        }

        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(_horizontalVelocity, Vector3.up);
        if (horizontalVelocity.sqrMagnitude <= 0.0001f)
        {
            _horizontalVelocity = Vector3.zero;
        }
        else
        {
            _horizontalVelocity = horizontalVelocity.normalized * Mathf.Min(horizontalVelocity.magnitude, GetCarryMoveSpeedLimit());
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
        if (_isCarryingPlayer)
        {
            return 1f;
        }

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
            _verticalVelocity = -Mathf.Max(0f, _groundStickVelocity);
        }
        
        float gravityMultiplier = _isWallRunning ? _wallRunGravityMultiplier : 1f;
        _verticalVelocity -= gravity * gravityMultiplier * deltaTime;

        if (_isWallRunning)
        {
            _verticalVelocity = Mathf.Max(_verticalVelocity, -_wallRunMaxFallSpeed);
        }

        if(!IsInjured() && !IsCrouching() && !_isCarryingPlayer && _playerLocomotionInput.JumpPressed && isGrounded){
            bool applyRampLipBoost = CanApplyRampLipBoost();

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

                if (applyRampLipBoost)
                {
                    boostedSpeed *= _rampLipSpeedMultiplier;
                }

                _horizontalVelocity = horizontalDirection * boostedSpeed;
            }

            Vector2 movementInput = _playerLocomotionInput != null ? _playerLocomotionInput.MovementInput : Vector2.zero;
            bool canPrimeWallRun =
                IsSprinting()
                && movementInput.y > 0.1f;

            _wallRunSprintGraceTimer = canPrimeWallRun ? _wallRunGroundSprintGraceTime : 0f;

            _jumpedThisFrame = true;
            float effectiveJumpForce = jumpForce * GetActiveJumpBoostMultiplier();
            if (applyRampLipBoost)
            {
                effectiveJumpForce *= _rampLipJumpForceMultiplier;
            }
            _verticalVelocity += MathF.Sqrt(effectiveJumpForce * JUMP_VELOCITY_MULTIPLIER * gravity);
        }
    }

    public bool IsGrounded() {
        if (_characterController != null && _characterController.isGrounded)
        {
            return true;
        }

        return _groundProbeGrounded && _verticalVelocity <= 0.1f && !_jumpedThisFrame;
    }

    private void RefreshGroundProbeState()
    {
        _groundProbeGrounded = false;
        _groundProbeHit = default;

        if (_characterController == null || !_characterController.enabled || _transform == null)
        {
            return;
        }

        Vector3 origin = _transform.position + Vector3.up * Mathf.Max(0.05f, _characterController.skinWidth + 0.15f);
        float radius = Mathf.Max(0.05f, _characterController.radius * _groundProbeRadiusScale);
        float distance = Mathf.Max(_groundProbeDistance, _characterController.skinWidth + 0.2f);
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            radius,
            Vector3.down,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return;
        }

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidGroundProbeHit(hit))
            {
                continue;
            }

            _groundProbeHit = hit;
            _groundProbeGrounded = true;
            return;
        }
    }

    private bool IsValidGroundProbeHit(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return false;
        }

        Transform hitTransform = hit.collider.transform;
        if (hitTransform == null || hitTransform.IsChildOf(_transform) || IsCarryLinkedTransform(hitTransform))
        {
            return false;
        }

        if (_rampLayer >= 0 && hit.collider.gameObject.layer == _rampLayer)
        {
            return true;
        }

        float maxSlope = _characterController != null ? _characterController.slopeLimit + 5f : 65f;
        return Vector3.Angle(hit.normal, Vector3.up) <= maxSlope;
    }

    private void UpdateRampState()
    {
        if (IsStandingOnRamp())
        {
            _lastRampTouchTime = Time.time;
        }
    }

    private bool CanApplyRampLipBoost()
    {
        if (_rampLayer < 0 || _rampLipSpeedMultiplier <= 1f || _rampLipJumpForceMultiplier <= 1f)
        {
            return false;
        }

        if (Time.time - _lastRampTouchTime > _rampLipGraceTime)
        {
            return false;
        }

        return GetHorizontalSpeed() >= _rampLipMinHorizontalSpeed;
    }

    private bool IsStandingOnRamp()
    {
        if (_rampLayer < 0 || !IsGrounded() || _playerLocomotionInput == null)
        {
            return false;
        }

        if (_playerLocomotionInput.MovementInput.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        if (_groundProbeGrounded && _groundProbeHit.collider != null && _groundProbeHit.collider.gameObject.layer == _rampLayer)
        {
            return true;
        }

        Vector3 origin = _transform.position + Vector3.up * 0.2f;
        float sphereRadius = _characterController != null
            ? Mathf.Max(0.05f, _characterController.radius * 0.85f)
            : 0.25f;
        float checkDistance = _characterController != null
            ? Mathf.Max(_rampGroundCheckDistance, _characterController.skinWidth + 0.1f)
            : _rampGroundCheckDistance;

        if (!Physics.SphereCast(origin, sphereRadius, Vector3.down, out RaycastHit hit, checkDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return hit.collider != null && hit.collider.gameObject.layer == _rampLayer;
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

    public bool IsSpectating()
    {
        return _isSpectating;
    }

    public void EnterSpectateMode()
    {
        _isSpectating = true;
        _spectateTargetKey = null;
        _nextSpectateRefreshTime = 0f;
        _isPauseMenuOpen = false;
        SetPauseMenuDisplay(false);
        _preferredViewMode = CameraViewMode.ThirdPerson;
        SetCameraView(CameraViewMode.ThirdPerson, true);
        SetThirdPersonCameraActive(false);
        SetSpectateCameraActive(true);
        SetLocalRenderMode(true);
        SetLocalSpectatorBodyVisible(false);
        MoveSpectatorBodyToHiddenPosition();
        SetFirstPersonWallClipHidden(false);
        RefreshSpectateTargets(forceReselect: true);
        UpdateSpectateCameraFollow(forceSnap: true);
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
    }

    public void ExitSpectateMode()
    {
        if (!_isSpectating)
        {
            return;
        }

        _isSpectating = false;
        _spectateTargets.Clear();
        _spectateTargetKey = null;
        _nextSpectateRefreshTime = 0f;
        SetSpectateCameraActive(false);
        if (_networkPlayer != null)
        {
            _networkPlayer.ApplyAuthoritativeRoundReset();
        }
        SetLocalSpectatorBodyVisible(true);
        SetCameraView(_preferredViewMode, true);
        UpdateForcedCameraViewState(forceImmediate: true);
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

    public bool IsAwaitingAuthoritativeNextbotHit()
    {
        return _isHitReacting && Time.time < _recentNextbotHitSourceExpiresAt;
    }

    public CharacterController GetCharacterController()
    {
        return _characterController;
    }

    public int GetWallRunSide()
    {
        return _wallRunSide;
    }

    public bool IsSimulationControlled()
    {
        return _isSimulationControlled;
    }

    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;
    public bool IsCombatModeActive => _combatModeActive;
    public bool IsEliminatedStateActive => _isEliminatedState;

    public void SetCombatModeActive(bool isActive)
    {
        _combatModeActive = isActive;
        _nextAllowedShotTime = 0f;
        _currentHealth = _maxHealth;
        _isEliminatedState = false;

        if (!_isSimulationControlled && _playerLocomotionInput != null)
        {
            _playerLocomotionInput.InputEnabled = true;
        }

        // Refresh visibility and camera state when combat mode changes
        UpdateForcedCameraViewState(true);
        SetLocalRenderMode(_currentViewMode == CameraViewMode.FirstPerson);
    }

    public void SetCombatHealth(float health)
    {
        _currentHealth = Mathf.Clamp(health, 0f, _maxHealth);
        if (_currentHealth > 0f)
        {
            _isEliminatedState = false;
        }
    }

    public void SetSimulationControlled(bool isSimulationControlled)
    {
        _isSimulationControlled = isSimulationControlled;
        SetLocalCharacterAudio(!isSimulationControlled);

        if (_playerLocomotionInput != null)
        {
            _playerLocomotionInput.SetSimulatedInputEnabled(isSimulationControlled);
            _playerLocomotionInput.InputEnabled = true;
        }

        if (!isSimulationControlled)
        {
            if (_playerHudDocument != null)
            {
                _playerHudDocument.enabled = true;
            }

            return;
        }

        if (_playerHudDocument != null)
        {
            _playerHudDocument.enabled = false;
        }

        ApplySimulationPresentationState();
    }

    private void ApplySimulationPresentationState()
    {
        EnsureRemoteFullBodyVisible();

        // For remote players, always show the body and hide the FPS arms
        if (_firstPersonArmRoot != null)
        {
            _firstPersonArmRoot.SetActive(false);
        }

        if (_thirdPersonBodyRoot != null)
        {
            _thirdPersonBodyRoot.SetActive(true);
            _thirdPersonBodyRoot.transform.localRotation = Quaternion.Euler(_thirdPersonBodyRotationOffset);
        }

        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
            {
                cameras[i].gameObject.SetActive(false);
            }
        }

        AudioListener[] audioListeners = GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < audioListeners.Length; i++)
        {
            if (audioListeners[i] != null)
            {
                audioListeners[i].enabled = false;
            }
        }
    }

    public void SetLocalCharacterAudio(bool isLocalCharacter)
    {
        ConfigureCharacterAudioSource(_footstepAudioSource, isLocalCharacter, _remoteFootstepMinDistance, _remoteFootstepMaxDistance);
        ConfigureCharacterAudioSource(_hurtAudioSource, isLocalCharacter, _remoteFootstepMinDistance, _remoteFootstepMaxDistance);
    }

    private static void ConfigureCharacterAudioSource(AudioSource audioSource, bool isLocalCharacter, float minDistance, float maxDistance)
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.spatialBlend = isLocalCharacter ? 0f : 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = Mathf.Max(0f, minDistance);
        audioSource.maxDistance = Mathf.Max(audioSource.minDistance + 0.1f, maxDistance);
        audioSource.dopplerLevel = 0f;
    }

    private float GetCurrentMoveSpeed()
    {
        float baseSpeed;

        if (_isBeingCarried)
        {
            return 0f;
        }

        if (_isCarryingPlayer)
        {
            baseSpeed = GetCarryBaseMoveSpeed();
            return baseSpeed * GetActiveSpeedBoostMultiplier();
        }

        if (IsInjured())
        {
            baseSpeed = _injuredMoveSpeed;
            return baseSpeed * GetActiveSpeedBoostMultiplier();
        }

        if (_isCrouchRunning)
        {
            baseSpeed = GetCurrentCrouchRunBaseSpeed();
            return baseSpeed * GetActiveSpeedBoostMultiplier();
        }

        if (IsCrouching())
        {
            baseSpeed = _crouchMoveSpeed;
            return baseSpeed * GetActiveSpeedBoostMultiplier();
        }

        baseSpeed = Mathf.Lerp(runSpeed, sprintSpeed, GetSprintProgress());
        return baseSpeed * GetActiveSpeedBoostMultiplier();
    }

    private float GetCarryBaseMoveSpeed()
    {
        if (_isCrouchRunning)
        {
            return Mathf.Max(_crouchMoveSpeed, GetCurrentCrouchRunBaseSpeed());
        }

        if (IsCrouching())
        {
            return Mathf.Min(_carryMoveSpeed, _crouchMoveSpeed);
        }

        return _carryMoveSpeed;
    }

    private float GetCarryMoveSpeedLimit()
    {
        return GetCarryBaseMoveSpeed() * GetActiveSpeedBoostMultiplier();
    }

    public void ApplyTemporarySpeedBoost(float multiplier, float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return;
        }

        float clampedMultiplier = Mathf.Clamp(multiplier, 1f, Mathf.Max(1f, _maxSpeedBoostMultiplier));
        if (clampedMultiplier <= 1f)
        {
            return;
        }

        _speedBoostMultiplier = Mathf.Max(_speedBoostMultiplier, clampedMultiplier);
        _speedBoostExpiresAt = Mathf.Max(_speedBoostExpiresAt, Time.time + durationSeconds);
    }

    public void ApplyTemporaryJumpBoost(float multiplier, float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return;
        }

        float clampedMultiplier = Mathf.Clamp(multiplier, 1f, Mathf.Max(1f, _maxJumpBoostMultiplier));
        if (clampedMultiplier <= 1f)
        {
            return;
        }

        _jumpBoostMultiplier = Mathf.Max(_jumpBoostMultiplier, clampedMultiplier);
        _jumpBoostExpiresAt = Mathf.Max(_jumpBoostExpiresAt, Time.time + durationSeconds);
    }

    public float GetSyncedSpeedBoostMultiplier()
    {
        return GetActiveSpeedBoostMultiplier();
    }

    public float GetSyncedSpeedBoostTimeRemaining()
    {
        float multiplier = GetActiveSpeedBoostMultiplier();
        if (multiplier <= 1f || _speedBoostExpiresAt < 0f)
        {
            return 0f;
        }

        return Mathf.Max(0f, _speedBoostExpiresAt - Time.time);
    }

    public float GetSyncedJumpBoostMultiplier()
    {
        return GetActiveJumpBoostMultiplier();
    }

    public float GetSyncedJumpBoostTimeRemaining()
    {
        float multiplier = GetActiveJumpBoostMultiplier();
        if (multiplier <= 1f || _jumpBoostExpiresAt < 0f)
        {
            return 0f;
        }

        return Mathf.Max(0f, _jumpBoostExpiresAt - Time.time);
    }

    public void PlayPickupFade()
    {
        PlayPickupFade(_pickupFadeColor);
    }

    public void PlayPickupFade(Color fadeColor)
    {
        if (_pickupFadeOverlayElement == null)
        {
            CacheHudElements();
            if (_pickupFadeOverlayElement == null)
            {
                return;
            }
        }

        _pickupFadeOverlayElement.style.backgroundColor = fadeColor;
        _pickupFadeOverlayElement.style.display = DisplayStyle.Flex;

        if (_pickupFadeCoroutine != null)
        {
            StopCoroutine(_pickupFadeCoroutine);
        }

        _pickupFadeCoroutine = StartCoroutine(AnimatePickupFade());
    }

    public void PlayJumpPickupFade()
    {
        PlayPickupFade(_jumpPickupFadeColor);
    }

    public void PlayLocalAbilitySound(AudioClip clip, float volume = 1f, float maxDuration = -1f)
    {
        if (clip == null || _pickupAudioSource == null)
        {
            return;
        }

        if (_pickupAudioStopCoroutine != null)
        {
            StopCoroutine(_pickupAudioStopCoroutine);
            _pickupAudioStopCoroutine = null;
        }

        _pickupAudioSource.Stop();
        _pickupAudioSource.volume = Mathf.Clamp01(volume);
        _pickupAudioSource.pitch = 1f;
        _pickupAudioSource.PlayOneShot(clip);

        if (maxDuration > 0f)
        {
            _pickupAudioStopCoroutine = StartCoroutine(StopPickupAudioAfterDelay(maxDuration));
        }
    }

    private IEnumerator StopPickupAudioAfterDelay(float delaySeconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, delaySeconds));

        if (_pickupAudioSource != null)
        {
            _pickupAudioSource.Stop();
        }

        _pickupAudioStopCoroutine = null;
    }

    public void PlayPlayerGotHitSound()
    {
        if (_playerGotHitClip == null || _hurtAudioSource == null)
        {
            return;
        }

        _hurtAudioSource.Stop();
        _hurtAudioSource.volume = Mathf.Clamp01(_playerGotHitVolume);
        _hurtAudioSource.pitch = 1f;
        _hurtAudioSource.PlayOneShot(_playerGotHitClip);
    }

    private float GetActiveSpeedBoostMultiplier()
    {
        if (_speedBoostExpiresAt <= Time.time)
        {
            _speedBoostMultiplier = 1f;
            _speedBoostExpiresAt = -1f;
            return 1f;
        }

        return Mathf.Max(1f, _speedBoostMultiplier);
    }

    private float GetActiveJumpBoostMultiplier()
    {
        if (_jumpBoostExpiresAt <= Time.time)
        {
            _jumpBoostMultiplier = 1f;
            _jumpBoostExpiresAt = -1f;
            return 1f;
        }

        return Mathf.Max(1f, _jumpBoostMultiplier);
    }

    private IEnumerator AnimatePickupFade()
    {
        if (_pickupFadeOverlayElement == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0.05f, _pickupFadeDuration);
        float halfDuration = duration * 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float opacity;
            if (elapsed <= halfDuration)
            {
                float inT = Mathf.Clamp01(elapsed / halfDuration);
                opacity = Mathf.Lerp(0f, _pickupFadePeakOpacity, inT);
            }
            else
            {
                float outT = Mathf.Clamp01((elapsed - halfDuration) / halfDuration);
                opacity = Mathf.Lerp(_pickupFadePeakOpacity, 0f, outT);
            }

            _pickupFadeOverlayElement.style.opacity = opacity;
            yield return null;
        }

        _pickupFadeOverlayElement.style.opacity = 0f;
        _pickupFadeOverlayElement.style.display = DisplayStyle.None;
        _pickupFadeCoroutine = null;
    }

    private float GetSprintProgress()
    {
        if (IsInjured() || _isCarryingPlayer || _isBeingCarried)
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

    public float GetSprintProgressForAudio()
    {
        return GetSprintProgress();
    }

    private void UpdateFootstepAudio()
    {
        if (_useAnimationEventFootsteps)
        {
            return;
        }

        if (_networkPlayer != null && !_networkPlayer.IsLocalPlayer)
        {
            return;
        }

        if (!CanPlayFootsteps())
        {
            _footstepStepTimer = 0f;
            return;
        }

        float stepInterval = Mathf.Lerp(FOOTSTEP_WALK_INTERVAL, FOOTSTEP_RUN_INTERVAL, GetSprintProgress());
        _footstepStepTimer += Time.deltaTime;

        if (_footstepStepTimer < stepInterval)
        {
            return;
        }

        _footstepStepTimer -= stepInterval;
        PlayRandomFootstepClip();
    }

    public void AnimationEvent_PlayFootstep()
    {
        if (!_useAnimationEventFootsteps)
        {
            return;
        }

        if (!CanPlayFootsteps())
        {
            return;
        }

        if (Time.time - _lastAnimationEventFootstepTime < FOOTSTEP_ANIMATION_EVENT_COOLDOWN)
        {
            return;
        }

        _lastAnimationEventFootstepTime = Time.time;
        PlayRandomClip(_footstepClips, _footstepVolume, ref _lastFootstepClipIndex);
    }

    public void AnimationEvent_PlayCrouchFootstep()
    {
        if (!_useAnimationEventFootsteps)
        {
            return;
        }

        if (!CanPlayFootsteps())
        {
            return;
        }

        if (Time.time - _lastAnimationEventFootstepTime < FOOTSTEP_ANIMATION_EVENT_COOLDOWN)
        {
            return;
        }

        _lastAnimationEventFootstepTime = Time.time;
        AudioClip[] clipsToUse = _crouchFootstepClips != null && _crouchFootstepClips.Length > 0 ? _crouchFootstepClips : _footstepClips;
        float volumeToUse = _crouchFootstepClips != null && _crouchFootstepClips.Length > 0 ? _crouchFootstepVolume : _footstepVolume * 0.7f;
        PlayRandomClip(clipsToUse, volumeToUse, ref _lastCrouchFootstepClipIndex);
    }

    public void AnimationEvent_PlayJumpStartFootstep()
    {
        if (!_useAnimationEventFootsteps || !CanPlayLocalCharacterAudio())
        {
            return;
        }

        AudioClip[] clipsToUse = _jumpStartFootstepClips != null && _jumpStartFootstepClips.Length > 0 ? _jumpStartFootstepClips : _footstepClips;
        PlayRandomClip(clipsToUse, _jumpStartFootstepVolume, ref _lastJumpStartClipIndex);
    }

    public void AnimationEvent_PlayLandingFootstep()
    {
        if (!_useAnimationEventFootsteps || !CanPlayLocalCharacterAudio())
        {
            return;
        }

        AudioClip[] clipsToUse = _landingFootstepClips != null && _landingFootstepClips.Length > 0 ? _landingFootstepClips : _footstepClips;
        PlayRandomClip(clipsToUse, _landingFootstepVolume, ref _lastLandingClipIndex);
    }

    private bool CanPlayFootsteps()
    {
        if (!CanPlayLocalCharacterAudio())
        {
            return false;
        }

        return IsGrounded()
            && !IsInjuredOrHitReacting()
            && !_isBeingCarried
            && !_isCarryingPlayer
            && !DidJumpThisFrame()
            && GetVerticalVelocity() <= FOOTSTEP_MAX_UPWARD_SPEED
            && GetHorizontalSpeed() >= _footstepMinHorizontalSpeed;
    }

    private void PlayRandomFootstepClip()
    {
        PlayRandomClip(_footstepClips, _footstepVolume, ref _lastFootstepClipIndex);
    }

    private bool CanPlayLocalCharacterAudio()
    {
        if (_footstepAudioSource == null)
        {
            return false;
        }

        return !IsInjuredOrHitReacting() && !_isBeingCarried;
    }

    private void PlayRandomClip(AudioClip[] clips, float volume, ref int lastClipIndex)
    {
        if (clips == null || clips.Length == 0 || _footstepAudioSource == null)
        {
            return;
        }

        int clipIndex = GetRandomClipIndex(clips, lastClipIndex);
        AudioClip clip = clips[clipIndex];
        if (clip == null)
        {
            return;
        }

        lastClipIndex = clipIndex;
        _footstepAudioSource.volume = volume;
        _footstepAudioSource.pitch = 1f + UnityEngine.Random.Range(-_footstepPitchRandomness, _footstepPitchRandomness);
        _footstepAudioSource.PlayOneShot(clip);
    }

    private int GetRandomFootstepClipIndex()
    {
        return GetRandomClipIndex(_footstepClips, _lastFootstepClipIndex);
    }

    private int GetRandomClipIndex(AudioClip[] clips, int lastClipIndex)
    {
        if (clips == null || clips.Length <= 1)
        {
            return 0;
        }

        int clipIndex = UnityEngine.Random.Range(0, clips.Length);
        if (clipIndex == lastClipIndex)
        {
            clipIndex = (clipIndex + 1) % clips.Length;
        }

        return clipIndex;
    }

    public bool IsCrouching()
    {
        return _isCrouching;
    }

    private void HandleSpectateTargetCyclingInput()
    {
        bool previousPressed = false;
        bool nextPressed = false;

        if (Keyboard.current != null)
        {
            previousPressed = Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame;
            nextPressed = Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame;
        }

        if (Gamepad.current != null)
        {
            previousPressed |= Gamepad.current.leftShoulder.wasPressedThisFrame || Gamepad.current.dpad.left.wasPressedThisFrame;
            nextPressed |= Gamepad.current.rightShoulder.wasPressedThisFrame || Gamepad.current.dpad.right.wasPressedThisFrame;
        }

        if (previousPressed)
        {
            CycleSpectateTarget(-1);
        }
        else if (nextPressed)
        {
            CycleSpectateTarget(1);
        }
    }

    private void UpdateSpectateTargetsIfNeeded()
    {
        if (Time.unscaledTime < _nextSpectateRefreshTime)
        {
            return;
        }

        RefreshSpectateTargets(forceReselect: false);
        _nextSpectateRefreshTime = Time.unscaledTime + _spectateTargetRefreshInterval;
    }

    private void RefreshSpectateTargets(bool forceReselect)
    {
        _spectateTargets.Clear();

        NetworkManager networkManager = NetworkManager.Instance;
        MyRoomState roomState = networkManager != null && networkManager.Room != null ? networkManager.Room.State : null;
        string localSessionId = networkManager != null ? networkManager.LocalSessionId : string.Empty;

        if (roomState != null && roomState.players != null)
        {
            foreach (string sessionId in roomState.players.Keys)
            {
                if (string.IsNullOrEmpty(sessionId) || string.Equals(sessionId, localSessionId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!networkManager.TryGetPlayerObject(sessionId, out GameObject playerObject) || playerObject == null)
                {
                    continue;
                }

                _spectateTargets.Add(new SpectateTarget
                {
                    Key = $"player:{sessionId}",
                    Label = $"Player {sessionId.Substring(0, Mathf.Min(4, sessionId.Length))}",
                    Transform = playerObject.transform,
                    IsNextbot = false,
                });
            }
        }

        NextbotFollowPlayer[] nextbots = FindObjectsByType<NextbotFollowPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < nextbots.Length; i++)
        {
            NextbotFollowPlayer nextbot = nextbots[i];
            if (nextbot == null || !nextbot.gameObject.activeInHierarchy)
            {
                continue;
            }

            string nextbotId = string.IsNullOrWhiteSpace(nextbot.NetworkNextbotId) ? $"nextbot_{i}" : nextbot.NetworkNextbotId;
            _spectateTargets.Add(new SpectateTarget
            {
                Key = $"nextbot:{nextbotId}",
                Label = nextbotId,
                Transform = nextbot.transform,
                IsNextbot = true,
            });
        }

        _spectateTargets.Sort(CompareSpectateTargets);

        if (_spectateTargets.Count == 0)
        {
            _spectateTargetKey = null;
            return;
        }

        if (forceReselect || string.IsNullOrEmpty(_spectateTargetKey))
        {
            _spectateTargetKey = GetPreferredInitialSpectateTargetKey();
            return;
        }

        for (int i = 0; i < _spectateTargets.Count; i++)
        {
            if (string.Equals(_spectateTargets[i].Key, _spectateTargetKey, StringComparison.Ordinal))
            {
                return;
            }
        }

        _spectateTargetKey = GetPreferredInitialSpectateTargetKey();
    }

    private void CycleSpectateTarget(int direction)
    {
        if (_spectateTargets.Count == 0 || direction == 0)
        {
            return;
        }

        int currentIndex = 0;
        for (int i = 0; i < _spectateTargets.Count; i++)
        {
            if (string.Equals(_spectateTargets[i].Key, _spectateTargetKey, StringComparison.Ordinal))
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = (currentIndex + direction) % _spectateTargets.Count;
        if (nextIndex < 0)
        {
            nextIndex += _spectateTargets.Count;
        }

        _spectateTargetKey = _spectateTargets[nextIndex].Key;
        UpdateSpectateCameraFollow(forceSnap: true);
    }

    private void UpdateSpectateCameraFollow(bool forceSnap = false)
    {
        if (!_isSpectating || _spectateCameraTransform == null)
        {
            return;
        }

        SpectateTarget? activeTarget = GetActiveSpectateTarget();
        if (activeTarget == null)
        {
            return;
        }

        SpectateTarget target = activeTarget.Value;
        if (target.Transform == null)
        {
            return;
        }

        Transform targetTransform = target.Transform;
        Vector3 targetOffset = target.IsNextbot ? _spectateNextbotOffset : _spectatePlayerOffset;
        Vector3 lookTarget = targetTransform.position + Vector3.up * (target.IsNextbot ? 1.2f : 1.4f);
        Quaternion orbitRotation = Quaternion.Euler(_cameraRotation.y, _cameraRotation.x, 0f);
        Vector3 desiredPosition = lookTarget + orbitRotation * targetOffset;
        Quaternion desiredRotation = Quaternion.LookRotation((lookTarget - desiredPosition).normalized, Vector3.up);

        SetThirdPersonCameraActive(false);
        SetSpectateCameraActive(true);

        if (forceSnap)
        {
            _spectateCameraTransform.position = desiredPosition;
            _spectateCameraTransform.rotation = desiredRotation;
            return;
        }

        float positionBlend = 1f - Mathf.Exp(-_spectateCameraMoveSpeed * Time.unscaledDeltaTime);
        float rotationBlend = 1f - Mathf.Exp(-_spectateCameraRotateSpeed * Time.unscaledDeltaTime);
        _spectateCameraTransform.position = Vector3.Lerp(_spectateCameraTransform.position, desiredPosition, positionBlend);
        _spectateCameraTransform.rotation = Quaternion.Slerp(_spectateCameraTransform.rotation, desiredRotation, rotationBlend);
    }

    private SpectateTarget? GetActiveSpectateTarget()
    {
        if (_spectateTargets.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < _spectateTargets.Count; i++)
        {
            if (string.Equals(_spectateTargets[i].Key, _spectateTargetKey, StringComparison.Ordinal))
            {
                return _spectateTargets[i];
            }
        }

        return _spectateTargets[0];
    }

    private int CompareSpectateTargets(SpectateTarget left, SpectateTarget right)
    {
        if (left.IsNextbot != right.IsNextbot)
        {
            return left.IsNextbot ? 1 : -1;
        }

        return string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
    }

    private string GetPreferredInitialSpectateTargetKey()
    {
        for (int i = 0; i < _spectateTargets.Count; i++)
        {
            if (!_spectateTargets[i].IsNextbot)
            {
                return _spectateTargets[i].Key;
            }
        }

        return _spectateTargets[0].Key;
    }

    public bool IsCrouchRunning()
    {
        return _isCrouchRunning;
    }

    public bool IsCrouchRunAnimationActive()
    {
        if (!_isCrouchRunning)
        {
            return false;
        }

        if (_crouchRunStartMoveSpeed <= _crouchMoveSpeed + 0.01f)
        {
            return false;
        }

        float currentCrouchRunSpeed = GetCurrentCrouchRunBaseSpeed();
        float normalizedCrouchRunSpeed = Mathf.InverseLerp(_crouchMoveSpeed, _crouchRunStartMoveSpeed, currentCrouchRunSpeed);
        return normalizedCrouchRunSpeed > _crouchRunAnimationExitSpeedRatio;
    }

    private bool IsInjured()
    {
        return _playerAnimation != null && _playerAnimation.IsInjuredActive;
    }

    private void UpdateCrouchState()
    {
        bool crouchPressedThisFrame = _playerLocomotionInput != null
            ? _playerLocomotionInput.CrouchPressedThisFrame
            : (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame);
        bool crouchHeld = _playerLocomotionInput != null
            ? _playerLocomotionInput.CrouchHeld
            : (Keyboard.current != null && Keyboard.current.cKey.isPressed);
        Vector2 movementInput = _playerLocomotionInput != null
            ? _playerLocomotionInput.MovementInput
            : Vector2.zero;
        float crouchRunEnterSpeed = _isCarryingPlayer
            ? Mathf.Max(0.01f, Mathf.Min(_crouchRunEnterMinSpeed, _carryMoveSpeed * 0.75f))
            : Mathf.Max(_crouchMoveSpeed, _crouchRunEnterMinSpeed);
        bool canStartCrouchRun = crouchPressedThisFrame
            && !IsInjured()
            && !_isBeingCarried
            && IsGrounded()
            && (!_isCarryingPlayer || movementInput.sqrMagnitude > 0.01f)
            && GetHorizontalSpeed() >= crouchRunEnterSpeed;

        if (canStartCrouchRun)
        {
            float uncrouchedMoveSpeed = _isCarryingPlayer
                ? Mathf.Max(_carryMoveSpeed * _carrySlideSpeedMultiplier, _crouchMoveSpeed + 0.1f)
                : Mathf.Lerp(runSpeed, sprintSpeed, GetSprintProgress());
            _crouchRunStartMoveSpeed = Mathf.Max(uncrouchedMoveSpeed, GetHorizontalSpeed());
            _crouchRunTimer = Mathf.Max(0.01f, _crouchRunHoldDuration);
            _isCrouchRunning = true;
        }

        if (_isCrouchRunning)
        {
            if (!crouchHeld)
            {
                _isCrouchRunning = false;
                _crouchRunTimer = 0f;
                _isCrouching = false;
                _crouchRunStartMoveSpeed = 0f;
                return;
            }

            _crouchRunTimer = Mathf.Max(0f, _crouchRunTimer - Time.deltaTime);
            if (_crouchRunTimer <= 0f)
            {
                _isCrouchRunning = false;
                _crouchRunTimer = 0f;
                _crouchRunStartMoveSpeed = 0f;
            }
        }

        bool shouldCrouch =
            !IsInjured()
            && !_isBeingCarried
            && (crouchHeld || _isCrouchRunning);

        _isCrouching = shouldCrouch;

        if (!_isCrouching)
        {
            _isCrouchRunning = false;
            _crouchRunTimer = 0f;
            _crouchRunStartMoveSpeed = 0f;
        }
    }

    private float GetCurrentCrouchRunBaseSpeed()
    {
        float crouchRunProgress = _crouchRunHoldDuration <= 0.01f
            ? 1f
            : 1f - Mathf.Clamp01(_crouchRunTimer / _crouchRunHoldDuration);
        return Mathf.Lerp(_crouchRunStartMoveSpeed, _crouchMoveSpeed, crouchRunProgress);
    }

    private void UpdateWallRunState()
    {
        if (_isCarryingPlayer || _isBeingCarried)
        {
            StopWallRun();
            _wallRunSprintGraceTimer = 0f;
            return;
        }

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
        if (_isCarryingPlayer || _isBeingCarried)
        {
            _wallRunSprintGraceTimer = 0f;
            return;
        }

        if (IsGrounded() && !_isWallRunning)
        {
            _wallRunSprintGraceTimer = 0f;
            return;
        }

        _wallRunSprintGraceTimer = Mathf.Max(0f, _wallRunSprintGraceTimer - Time.deltaTime);
    }

    private bool CanMaintainWallRun()
    {
        if (_playerLocomotionInput == null || IsGrounded() || IsInjured() || IsCrouching() || _isCarryingPlayer || _isBeingCarried)
        {
            return false;
        }

        Vector2 movementInput = _playerLocomotionInput.MovementInput;
        if (movementInput.y <= 0.1f)
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
        return intoWallAmount >= -0.1f;
    }

    private Vector3 GetPlanarMovementDirection(Vector2 movementInput)
    {
        float movementBasisYaw = GetMovementBasisYaw();
        Quaternion movementBasisRotation = Quaternion.Euler(0f, movementBasisYaw, 0f);
        Vector3 forward = movementBasisRotation * Vector3.forward;
        Vector3 right = movementBasisRotation * Vector3.right;
        Vector3 forwardXZ = new Vector3(forward.x, 0f, forward.z).normalized;
        Vector3 rightXZ = new Vector3(right.x, 0f, right.z).normalized;
        return (forwardXZ * movementInput.y + rightXZ * movementInput.x).normalized;
    }

    private float GetMovementBasisYaw()
    {
        float movementBasisYaw = _transform.eulerAngles.y;
        if (IsInjured() && !_isBeingCarried)
        {
            movementBasisYaw += _cameraRotation.x;

            bool lookBackHeld = Keyboard.current != null && Keyboard.current.rKey.isPressed;
            if (lookBackHeld)
            {
                movementBasisYaw += 180f;
            }
        }

        return movementBasisYaw;
    }

    private bool TryGetWallRunContact(out int wallSide, out Vector3 wallNormal)
    {
        wallSide = 0;
        wallNormal = Vector3.zero;

        if (_playerLocomotionInput == null)
        {
            return false;
        }

        Vector3 rayOrigin = _transform.position + Vector3.up * (_characterController.height * 0.5f);
        float effectiveCheckDistance = _wallRunCheckDistance;

        if (_characterController != null)
        {
            effectiveCheckDistance = Mathf.Max(
                effectiveCheckDistance,
                _characterController.radius + _characterController.skinWidth + _wallRunCheckDistance);
        }

        bool hitRight = Physics.Raycast(
            rayOrigin,
            _transform.right,
            out RaycastHit rightHit,
            effectiveCheckDistance,
            _wallRunLayers,
            QueryTriggerInteraction.Ignore);

        bool hitLeft = Physics.Raycast(
            rayOrigin,
            -_transform.right,
            out RaycastHit leftHit,
            effectiveCheckDistance,
            _wallRunLayers,
            QueryTriggerInteraction.Ignore);

        if (!hitRight && !hitLeft)
        {
            return false;
        }

        bool useRightHit;
        if (hitRight && hitLeft)
        {
            float inputX = _playerLocomotionInput.MovementInput.x;
            useRightHit = inputX > 0.05f ? true : inputX < -0.05f ? false : rightHit.distance <= leftHit.distance;
        }
        else
        {
            useRightHit = hitRight;
        }

        RaycastHit hit = useRightHit ? rightHit : leftHit;

        if (Mathf.Abs(hit.normal.y) > 0.2f)
        {
            return false;
        }

        wallSide = useRightHit ? 1 : -1;
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
