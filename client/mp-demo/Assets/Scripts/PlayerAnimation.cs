using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private enum PresentationMode
    {
        Default,
        Shooting
    }

    #region Class Variables
    [SerializeField] private Animator _animator;
    [SerializeField] private RuntimeAnimatorController _childAnimatorControllerOverride;
    [SerializeField] private string[] _runtimeChildAnimatorNameHints = { "arms" };
    [SerializeField] private bool _createRuntimeAnimationPathShims = true;
    [Header("Shooting Mode")]
    [SerializeField] private AnimationClip _shootingIdleClip;
    [SerializeField] private AnimationClip _shootingRunningClip;
    [SerializeField] private AnimationClip _shootingBackwardClip;
    [SerializeField] private AnimationClip _shootingLeftClip;
    [SerializeField] private AnimationClip _shootingRightClip;
    [SerializeField] private AnimationClip _shootingShootClip;
    [SerializeField] private AnimationClip _shootingJumpStartClip;
    [SerializeField] private AnimationClip _shootingFallingClip;
    [SerializeField] private AnimationClip _shootingInAirClip;
    [SerializeField] private AnimationClip _shootingWallRunLeftClip;
    [SerializeField] private AnimationClip _shootingWallRunRightClip;
    public Animator Animator => _animator;
    public Transform VisualRootTransform => _animator != null ? _animator.transform : null;
    public bool IsInjuredActive => _debugForceInjured || (_useNetworkAnimationState ? _networkIsInjured : _isInjured);
    [SerializeField] private float animationSmoothTime = 0.1f;
    [SerializeField] private bool _useSingleForwardRunAnimation = false;
    [SerializeField] private bool _debugForceInjured = false;
    [SerializeField] private bool _debugForceCrouching = false;
    [SerializeField] private float _jumpAnimationMinAirTime = 0.12f;
    [SerializeField] private float _landingGroundedBuffer = 0.05f;
    [SerializeField] private float _injuredReleaseBlendDuration = 0.16f;
    [SerializeField] private float _crouchReleaseBlendDuration = 0.12f;
    [SerializeField] private float _crouchRunEnterTransitionDuration = 0.06f;
    [SerializeField] private float _crouchRunExitTransitionDuration = 0.16f;
    [SerializeField] private float _wallRunAnimationExitBuffer = 0.04f;
    [SerializeField] private float _networkAnimationBlendSpeed = 18f;
    [SerializeField] private float _networkAnimationReleaseSpeed = 32f;
    [SerializeField, Min(0.01f)] private float _shootAnimationDuration = 0.09f;
    [SerializeField, Min(0f)] private float _shootAnimationTransitionDuration = 0.03f;

    private PlayerLocomotionInput _playerLocomotionInput;
    private PlayerController _playerController;
    private Animator[] _childAnimators = System.Array.Empty<Animator>();

    private static readonly int _inputXHash = Animator.StringToHash("inputX");
    private static readonly int _inputYHash = Animator.StringToHash("inputY");
    private static readonly int _groundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int _jumpHash = Animator.StringToHash("IsJumping");
    private static readonly int _verticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int _injuredHash = Animator.StringToHash("IsInjured");
    private static readonly int _crouchHash = Animator.StringToHash("IsCrouching");
    private static readonly int _shootingModeHash = Animator.StringToHash("IsShootingMode");
    private static readonly int _idleRunStateHash = Animator.StringToHash("Base Layer.Idle/Run");
    private static readonly int _shootingIdleRunStateHash = Animator.StringToHash("Base Layer.ShootingIdle/Run");
    private static readonly int _wallSlideLeftStateHash = Animator.StringToHash("Base Layer.WallSlideLeft");
    private static readonly int _wallSlideRightStateHash = Animator.StringToHash("Base Layer.WallSlideRight");
    private static readonly int _fallingStateHash = Animator.StringToHash("Base Layer.Falling");
    private static readonly int _inAirStateHash = Animator.StringToHash("Base Layer.InAir");
    private static readonly int _crouchStateHash = Animator.StringToHash("Base Layer.Crouch");
    private static readonly int _crouchRunningStateHash = Animator.StringToHash("Base Layer.CrouchRunning");
    private static readonly int _carryingMeStateHash = Animator.StringToHash("Base Layer.CarryingMe");
    private static readonly int _carryingIdleStateHash = Animator.StringToHash("Base Layer.CarryingIdle");
    private static readonly int _carryingRunStateHash = Animator.StringToHash("Base Layer.CarryingRun");
    private static readonly int _ak47ShootStateHash = Animator.StringToHash("Base Layer.ak47_shooting");
    private static readonly int _ak47RunningShootStateHash = Animator.StringToHash("Base Layer.ak47_runningShoot");
    private static readonly int _ak47WallRunLeftStateHash = Animator.StringToHash("Base Layer.ak47_walRunLeft_arms");
    private static readonly int _ak47WallRunRightStateHash = Animator.StringToHash("Base Layer.ak47_walRunRight_arms");
    private static readonly int _ak47ShootArmsStateHash = Animator.StringToHash("Base Layer.ak47_shooting_arms");
    private static readonly int _ak47ReloadArmsStateHash = Animator.StringToHash("Base Layer.ak47_reload_arms");
    private const string WALL_SLIDE_LEFT_STATE = "Base Layer.WallSlideLeft";
    private const string WALL_SLIDE_RIGHT_STATE = "Base Layer.WallSlideRight";
    private const string FALLING_STATE = "Base Layer.Falling";
    private const string IN_AIR_STATE = "Base Layer.InAir";
    private const string IDLE_RUN_STATE = "Base Layer.Idle/Run";
    private const string SHOOTING_IDLE_RUN_STATE = "Base Layer.ShootingIdle/Run";
    private const string CROUCH_STATE = "Base Layer.Crouch";
    private const string CROUCH_RUNNING_STATE = "Base Layer.CrouchRunning";
    private const string CARRYING_ME_STATE = "Base Layer.CarryingMe";
    private const string CARRYING_IDLE_STATE = "Base Layer.CarryingIdle";
    private const string CARRYING_RUN_STATE = "Base Layer.CarryingRun";
    private const string AK47_RUNNING_SHOOT_STATE = "Base Layer.ak47_runningShoot";
    private const string AK47_SHOOT_ARMS_STATE = "Base Layer.ak47_shooting_arms";
    private const string AK47_RELOAD_ARMS_STATE = "Base Layer.ak47_reload_arms";
    private const string AK47_WALL_RUN_LEFT_STATE = "Base Layer.ak47_walRunLeft_arms";
    private const string AK47_WALL_RUN_RIGHT_STATE = "Base Layer.ak47_walRunRight_arms";

    private float _currentInputX;
    private float _currentInputY;
    private float _smoothSpeed;
    private bool _wasGrounded;
    private bool _useNetworkAnimationState;
    private Vector2 _networkAnimationInput;
    private bool _networkIsGrounded = true;
    private bool _networkIsJumping;
    private float _networkVerticalSpeed;
    private bool _networkIsInjured;
    private bool _networkIsCrouching;
    private bool _networkIsWallRunning;
    private int _networkWallRunSide;
    private float _jumpAnimationLatchTimer;
    private float _groundedStableTimer;
    private bool _isInjured;
    private float _injuredMoveAmount;
    private float _injuredReleaseTimer;
    private float _crouchMoveAmount;
    private float _crouchReleaseTimer;
    private Vector2 _crouchMoveDirection = Vector2.up;
    private float _wallRunAnimationHoldTimer;
    private string _lastWallRunState = WALL_SLIDE_LEFT_STATE;
    private Vector2 _lastAppliedAnimationInput;
    private bool _lastAppliedGrounded = true;
    private bool _lastAppliedJumping;
    private float _lastAppliedVerticalSpeed;
    private PresentationMode _presentationMode = PresentationMode.Shooting;
    private float _shootAnimationTimer;
    private float _reloadAnimationTimer;
    private readonly System.Collections.Generic.Dictionary<RuntimeAnimatorController, AnimatorOverrideController> _shootingAnimatorOverrides
        = new System.Collections.Generic.Dictionary<RuntimeAnimatorController, AnimatorOverrideController>();
    private bool _hasInitializedChildAnimators;

    private const float DEFAULT_SMOOTH_SPEED = 10f;
    #endregion

    #region Setup
    private void Awake()
    {
        _playerLocomotionInput = GetComponent<PlayerLocomotionInput>();
        _playerController = GetComponent<PlayerController>();
        ResolveAnimatorReference();
        EnsureRuntimeChildAnimators();
        ResolveAnimatorReference();

        if (_animator != null)
        {
            _animator.applyRootMotion = false;
        }
    }

    private void Start()
    {
        _smoothSpeed = animationSmoothTime > 0 ? 1f / animationSmoothTime : DEFAULT_SMOOTH_SPEED;
        _wasGrounded = _playerController != null && _playerController.IsGrounded();
    }
    #endregion

    #region Update
    private void Update()
    {
        if (_animator == null || !_animator.gameObject.activeInHierarchy)
        {
            ResolveAnimatorReference();
        }

        if (_animator != null && _animator.applyRootMotion)
        {
            _animator.applyRootMotion = false;
        }

        EnsureRuntimeChildAnimators();
        UpdateAnimation();
    }
    #endregion

    #region Animation
    public void SetUseNetworkAnimationState(bool useNetworkAnimationState)
    {
        _useNetworkAnimationState = useNetworkAnimationState;
    }

    public void ApplyNetworkState(
        float inputX,
        float inputY,
        bool isGrounded,
        bool isJumping,
        float verticalSpeed,
        bool isInjured,
        bool isCrouching,
        bool isWallRunning,
        int wallRunSide)
    {
        _useNetworkAnimationState = true;
        _networkAnimationInput = new Vector2(inputX, inputY);
        _networkIsGrounded = isGrounded;
        _networkIsJumping = isJumping;
        _networkVerticalSpeed = verticalSpeed;
        _networkIsInjured = isInjured;
        _networkIsCrouching = isCrouching;
        _networkIsWallRunning = isWallRunning;
        _networkWallRunSide = wallRunSide;
    }

    public void GetAnimationSyncState(out Vector2 animationInput, out bool isGrounded, out bool isJumping, out float verticalSpeed)
    {
        animationInput = _lastAppliedAnimationInput;
        isGrounded = _lastAppliedGrounded;
        isJumping = _lastAppliedJumping;
        verticalSpeed = _lastAppliedVerticalSpeed;
    }

    public void SetInjured(bool isInjured)
    {
        _isInjured = isInjured;
    }

    public void SetShootingModeActive(bool isActive)
    {
        PresentationMode targetMode = isActive ? PresentationMode.Shooting : PresentationMode.Default;
        if (_presentationMode == targetMode)
        {
            return;
        }

        _presentationMode = targetMode;
        ReapplyAnimatorControllers();
    }

    public bool IsCrouchingActive => _debugForceCrouching || (_useNetworkAnimationState ? _networkIsCrouching : (_playerController != null && _playerController.IsCrouching()));
    public bool IsCarryingActive => _playerController != null && _playerController.IsCarrying();
    public bool IsBeingCarriedActive => _playerController != null && _playerController.IsBeingCarried();
    public bool IsShootingModeActive => _presentationMode == PresentationMode.Shooting;
    
    private string GetActiveLocomotionState(Animator targetAnimator)
    {
        // Always prefer the shooting locomotion state to ensure AK47 animations are used by default
        if (targetAnimator.HasState(0, _shootingIdleRunStateHash))
        {
            return SHOOTING_IDLE_RUN_STATE;
        }
        return IDLE_RUN_STATE;
    }

    public void PlayShootAnimation()
    {
        if (_presentationMode != PresentationMode.Shooting)
        {
            return;
        }

        _shootAnimationTimer = Mathf.Max(_shootAnimationDuration, 0.01f);
    }

    public void PlayReloadAnimation(float durationSeconds)
    {
        if (_presentationMode != PresentationMode.Shooting)
        {
            return;
        }

        _shootAnimationTimer = 0f;
        _reloadAnimationTimer = Mathf.Max(durationSeconds, 0.01f);
    }

    public string GetAnimatorDebugInfo()
    {
        if (_animator == null)
        {
            return "Animator: missing";
        }

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        AnimatorClipInfo[] clipInfos = _animator.GetCurrentAnimatorClipInfo(0);
        string clipName = clipInfos.Length > 0 && clipInfos[0].clip != null ? clipInfos[0].clip.name : "none";
        string animatorName = _animator.gameObject.name;
        string controllerName = _animator.runtimeAnimatorController != null
            ? _animator.runtimeAnimatorController.name
            : "none";
        string avatarName = _animator.avatar != null ? _animator.avatar.name : "none";

        return $"Animator: {animatorName}\nController: {controllerName}\nAvatar: {avatarName}\nState Hash: {stateInfo.fullPathHash}\nClip: {clipName}\nTime: {stateInfo.normalizedTime:0.00}";
    }

    private void ResolveAnimatorReference()
    {
        Animator[] animators = GetComponentsInChildren<Animator>(true);
        _childAnimators = animators;
        Animator fallbackAnimator = null;
        Animator bestAnimator = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate == null)
            {
                continue;
            }

            if (fallbackAnimator == null)
            {
                fallbackAnimator = candidate;
            }

            if (candidate.gameObject == gameObject)
            {
                _animator = candidate;
                ApplyAnimatorOverrideIfNeeded(candidate);
                return;
            }

            int candidateScore = ScoreAnimatorCandidate(candidate);
            if (candidateScore > bestScore)
            {
                bestScore = candidateScore;
                bestAnimator = candidate;
            }
        }

        _animator = bestAnimator != null ? bestAnimator : fallbackAnimator;
        ApplyAnimatorOverrideIfNeeded(_animator);
    }

    private void EnsureRuntimeChildAnimators()
    {
        if (_hasInitializedChildAnimators || _animator == null)
        {
            return;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        bool addedAnimator = false;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            EnsureAnimationBindingPathShim(candidate);

            if (!ShouldCreateRuntimeChildAnimator(candidate))
            {
                continue;
            }

            Animator childAnimator = candidate.gameObject.AddComponent<Animator>();
            childAnimator.avatar = _animator.avatar;
            childAnimator.applyRootMotion = false;
            childAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            childAnimator.speed = 1f;
            ApplyAnimatorOverrideIfNeeded(childAnimator);
            addedAnimator = true;
        }

        if (addedAnimator)
        {
            _childAnimators = GetComponentsInChildren<Animator>(true);
        }
        
        _hasInitializedChildAnimators = true;
    }

    private bool ShouldCreateRuntimeChildAnimator(Transform candidate)
    {
        if (candidate == null
            || candidate == transform
            || candidate.GetComponent<Animator>() != null
            || candidate.GetComponentInChildren<Animator>(true) != null)
        {
            return false;
        }

        if (candidate.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
        {
            return false;
        }

        if (_runtimeChildAnimatorNameHints == null || _runtimeChildAnimatorNameHints.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < _runtimeChildAnimatorNameHints.Length; i++)
        {
            string hint = _runtimeChildAnimatorNameHints[i];
            if (string.IsNullOrWhiteSpace(hint))
            {
                continue;
            }

            if (candidate.name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureAnimationBindingPathShim(Transform candidate)
    {
        if (!_createRuntimeAnimationPathShims || !IsRuntimeChildAnimatorTarget(candidate))
        {
            return;
        }

        EnsureChildBindingParent(candidate, "R_Arm", "ArmL_Offset");
        EnsureChildBindingParent(candidate, "L_Arm", "ArmR_Offset");
        EnsureDummyBindingPath(candidate, "MainBody/Rig1/Spine1/Spine2/Neck1");
    }

    private bool IsRuntimeChildAnimatorTarget(Transform candidate)
    {
        if (candidate == null || _runtimeChildAnimatorNameHints == null)
        {
            return false;
        }

        for (int i = 0; i < _runtimeChildAnimatorNameHints.Length; i++)
        {
            string hint = _runtimeChildAnimatorNameHints[i];
            if (string.IsNullOrWhiteSpace(hint))
            {
                continue;
            }

            if (candidate.name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureChildBindingParent(Transform root, string parentName, string animatedChildName)
    {
        if (root == null)
        {
            return;
        }

        Transform animatedChild = root.Find(animatedChildName);
        if (animatedChild == null)
        {
            Transform existingParent = root.Find(parentName);
            if (existingParent != null && existingParent.Find(animatedChildName) != null)
            {
                return;
            }

            return;
        }

        Transform bindingParent = root.Find(parentName);
        if (bindingParent == null)
        {
            GameObject parentObject = new GameObject(parentName);
            bindingParent = parentObject.transform;
            bindingParent.SetParent(root, false);
            bindingParent.localPosition = Vector3.zero;
            bindingParent.localRotation = Quaternion.identity;
            bindingParent.localScale = Vector3.one;
        }

        if (animatedChild.parent == bindingParent)
        {
            return;
        }

        animatedChild.SetParent(bindingParent, false);
    }

    private static void EnsureDummyBindingPath(Transform root, string relativePath)
    {
        if (root == null || string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        string[] segments = relativePath.Split('/');
        Transform current = root;

        for (int i = 0; i < segments.Length; i++)
        {
            string segment = segments[i];
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            Transform next = current.Find(segment);
            if (next == null)
            {
                GameObject segmentObject = new GameObject(segment);
                next = segmentObject.transform;
                next.SetParent(current, false);
                next.localPosition = Vector3.zero;
                next.localRotation = Quaternion.identity;
                next.localScale = Vector3.one;
            }

            current = next;
        }
    }

    private int ScoreAnimatorCandidate(Animator candidate)
    {
        if (candidate == null)
        {
            return int.MinValue;
        }

        int score = 0;
        if (candidate.gameObject.activeInHierarchy)
        {
            score += 1000;
        }

        if (candidate.enabled)
        {
            score += 500;
        }

        if (candidate.runtimeAnimatorController != null)
        {
            score += 200;
        }

        if (candidate.avatar != null)
        {
            score += 100;
        }

        score += candidate.GetComponentsInChildren<Renderer>(true).Length * 10;
        score += candidate.GetComponentsInChildren<Transform>(true).Length;

        return score;
    }

    private void ApplyAnimatorOverrideIfNeeded(Animator targetAnimator)
    {
        if (targetAnimator == null)
        {
            return;
        }

        // We want to allow the root animator to receive overrides as well,
        // as it often drives the third-person model visibility and animations.
        /*
        if (targetAnimator.gameObject == gameObject)
        {
            return;
        }
        */

        RuntimeAnimatorController desiredController = GetDesiredAnimatorController(targetAnimator);
        if (desiredController == null || targetAnimator.runtimeAnimatorController == desiredController)
        {
            return;
        }

        targetAnimator.runtimeAnimatorController = desiredController;
        targetAnimator.applyRootMotion = false;
        targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        targetAnimator.speed = 1f;
        targetAnimator.Rebind();
        targetAnimator.Update(0f);
    }

    private RuntimeAnimatorController GetDesiredAnimatorController(Animator targetAnimator)
    {
        RuntimeAnimatorController baseController = GetBaseAnimatorController(targetAnimator);
        if (_presentationMode != PresentationMode.Shooting)
        {
            return baseController;
        }

        AnimatorOverrideController shootingOverride = GetOrCreateShootingAnimatorOverride(baseController);
        return shootingOverride != null ? shootingOverride : baseController;
    }

    private RuntimeAnimatorController GetBaseAnimatorController(Animator targetAnimator)
    {
        if (targetAnimator == null)
        {
            return null;
        }

        RuntimeAnimatorController assignedController = targetAnimator.runtimeAnimatorController;
        if (assignedController is AnimatorOverrideController overrideController
            && overrideController.runtimeAnimatorController != null)
        {
            assignedController = overrideController.runtimeAnimatorController;
        }

        bool isPrimaryAnimator = targetAnimator == _animator || targetAnimator.gameObject == gameObject;
        if (isPrimaryAnimator)
        {
            return assignedController;
        }

        if (assignedController != null
            && (_childAnimatorControllerOverride == null || assignedController != _childAnimatorControllerOverride))
        {
            return assignedController;
        }

        return _childAnimatorControllerOverride != null
            ? _childAnimatorControllerOverride
            : assignedController;
    }

    private AnimatorOverrideController GetOrCreateShootingAnimatorOverride(RuntimeAnimatorController baseController)
    {
        if (baseController == null
            || _shootingIdleClip == null
            || _shootingRunningClip == null
            || _shootingBackwardClip == null
            || _shootingLeftClip == null
            || _shootingRightClip == null)
        {
            return null;
        }

        if (_shootingAnimatorOverrides.TryGetValue(baseController, out AnimatorOverrideController cachedOverride)
            && cachedOverride != null
            && cachedOverride.runtimeAnimatorController == baseController)
        {
            return cachedOverride;
        }

        AnimatorOverrideController shootingOverrideController = new AnimatorOverrideController(baseController);
        var overrides = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
        shootingOverrideController.GetOverrides(overrides);

        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip sourceClip = overrides[i].Key;
            AnimationClip replacementClip = GetShootingReplacementClip(sourceClip);
            if (replacementClip != null)
            {
                overrides[i] = new System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>(sourceClip, replacementClip);
            }
        }

        shootingOverrideController.ApplyOverrides(overrides);
        _shootingAnimatorOverrides[baseController] = shootingOverrideController;
        return shootingOverrideController;
    }

    private AnimationClip GetShootingReplacementClip(AnimationClip sourceClip)
    {
        if (sourceClip == null)
        {
            return null;
        }

        string clipName = sourceClip.name.Replace(" ", string.Empty).ToLowerInvariant();
        return clipName switch
        {
            "idle" => _shootingIdleClip,
            "running" => _shootingRunningClip,
            "walkback" => _shootingBackwardClip,
            "walkleft" => _shootingLeftClip,
            "walkright" => _shootingRightClip,
            "runningstrafeleft" => _shootingLeftClip,
            "runningstraferight" => _shootingRightClip,
            "jumpstart" => _shootingJumpStartClip,
            "falling" => _shootingFallingClip,
            "inair" => _shootingInAirClip,
            "wallslideleft" => _shootingWallRunLeftClip,
            "wallslideright" => _shootingWallRunRightClip,
            "ak47_walrunleft_arms" => _shootingWallRunLeftClip,
            "ak47_walrunright_arms" => _shootingWallRunRightClip,
            "ak47_shooting" => _shootingShootClip,
            "ak47_shooting_arms" => _shootingShootClip,
            _ => null,
        };
    }

    private void ReapplyAnimatorControllers()
    {
        Animator[] animators = GetComponentsInChildren<Animator>(true);
        _childAnimators = animators;

        for (int i = 0; i < animators.Length; i++)
        {
            Animator targetAnimator = animators[i];
            if (targetAnimator == null)
            {
                continue;
            }

            ApplyAnimatorOverrideIfNeeded(targetAnimator);
        }
    }

    private void ApplyAnimationStateToAllAnimators(float inputX, float inputY, bool isGrounded, bool isJumping, float verticalSpeed)
    {
        if (_childAnimators == null || _childAnimators.Length == 0)
        {
            if (_animator != null)
            {
                ApplyAnimationState(_animator, inputX, inputY, isGrounded, isJumping, verticalSpeed);
            }

            return;
        }

        for (int i = 0; i < _childAnimators.Length; i++)
        {
            Animator targetAnimator = _childAnimators[i];
            if (targetAnimator == null)
            {
                continue;
            }

            ApplyAnimatorOverrideIfNeeded(targetAnimator);
            ApplyAnimationState(targetAnimator, inputX, inputY, isGrounded, isJumping, verticalSpeed);
        }
    }

    private void ApplyAnimationState(Animator targetAnimator, float inputX, float inputY, bool isGrounded, bool isJumping, float verticalSpeed)
    {
        // Procedural animation state handler
        if (targetAnimator == null)
        {
            return;
        }

        if (targetAnimator.applyRootMotion)
        {
            targetAnimator.applyRootMotion = false;
        }

        targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        targetAnimator.speed = 1f;
        targetAnimator.SetFloat(_inputXHash, inputX);
        targetAnimator.SetFloat(_inputYHash, inputY);
        targetAnimator.SetBool(_groundedHash, isGrounded);
        targetAnimator.SetBool(_jumpHash, isJumping);
        targetAnimator.SetFloat(_verticalSpeedHash, verticalSpeed);
        bool isCarryingActive = IsCarryingActive;
        bool isBeingCarriedActive = IsBeingCarriedActive;
        bool isInjuredActive = IsInjuredActive && !isCarryingActive && !isBeingCarriedActive && !(_playerController != null && _playerController.IsEliminated());
        bool isCrouchingActive = IsCrouchingActive && !isCarryingActive && !isBeingCarriedActive;
        bool isCrouchRunningActive = !_useNetworkAnimationState && _playerController != null && _playerController.IsCrouchRunAnimationActive();
        targetAnimator.SetBool(_injuredHash, isInjuredActive);
        targetAnimator.SetBool(_crouchHash, !isInjuredActive && isCrouchingActive && !isCrouchRunningActive);
        targetAnimator.SetBool(_shootingModeHash, _presentationMode == PresentationMode.Shooting);

        if (isInjuredActive)
        {
            // If we are injured, let the animator handle the state transitions via the IsInjured parameter.
            // Do not force locomotion states which would override the downed pose.
            return;
        }

        if (_shootAnimationTimer > 0f && _presentationMode == PresentationMode.Shooting)
        {
            bool isMoving = Mathf.Abs(inputX) > 0.05f || Mathf.Abs(inputY) > 0.05f;
            bool wallRunning = _useNetworkAnimationState ? _networkIsWallRunning : (_playerController != null && _playerController.IsWallRunning());

            // If we are wall running, we prefer to stay in the wall run pose to avoid "twicking" 
            // and instead rely on procedural recoil for the shooting feel.
            if (!wallRunning)
            {
                if (isMoving && targetAnimator.HasState(0, _ak47RunningShootStateHash))
                {
                    CrossFadeIfNeeded(targetAnimator, AK47_RUNNING_SHOOT_STATE, 0.1f);
                }
                else if (targetAnimator.HasState(0, _ak47ShootStateHash))
                {
                    CrossFadeIfNeeded(targetAnimator, "Base Layer.ak47_shooting", 0.1f);
                }
                else if (targetAnimator.HasState(0, _ak47ShootArmsStateHash))
                {
                    CrossFadeIfNeeded(targetAnimator, AK47_SHOOT_ARMS_STATE, 0.1f);
                }
            }
        }

        if (_reloadAnimationTimer > 0f && _presentationMode == PresentationMode.Shooting)
        {
            if (targetAnimator.HasState(0, _ak47ReloadArmsStateHash))
            {
                CrossFadeIfNeeded(targetAnimator, AK47_RELOAD_ARMS_STATE, 0.08f);
            }
        }
        
        if (isBeingCarriedActive)
        {
            CrossFadeIfNeeded(targetAnimator, CARRYING_ME_STATE, 0.08f);
            return;
        }

        if (isCarryingActive)
        {
            bool carryMoving = Mathf.Abs(inputX) > 0.05f || Mathf.Abs(inputY) > 0.05f;
            if (!_useNetworkAnimationState && _playerController != null)
            {
                carryMoving = _playerController.GetHorizontalSpeed() > 0.05f;
            }

            string carryState = carryMoving
                ? CARRYING_RUN_STATE
                : CARRYING_IDLE_STATE;
            CrossFadeIfNeeded(targetAnimator, carryState, 0.08f);
            return;
        }

        if (ApplyCarryRecoveryState(targetAnimator, isGrounded, verticalSpeed, isInjuredActive, isCrouchingActive))
        {
            return;
        }

        if (isGrounded && isCrouchRunningActive)
        {
            CrossFadeIfNeeded(targetAnimator, CROUCH_RUNNING_STATE, _crouchRunEnterTransitionDuration);
            return;
        }

        if (isGrounded && isCrouchingActive)
        {
            float crouchTransitionDuration = targetAnimator.GetCurrentAnimatorStateInfo(0).IsName(CROUCH_RUNNING_STATE)
                ? _crouchRunExitTransitionDuration
                : 0.05f;
            CrossFadeIfNeeded(targetAnimator, CROUCH_STATE, crouchTransitionDuration);
            return;
        }

        AnimatorStateInfo currentState = targetAnimator.GetCurrentAnimatorStateInfo(0);
        if (currentState.IsName(CROUCH_RUNNING_STATE))
        {
            string recoveryState = isGrounded
                ? GetActiveLocomotionState(targetAnimator)
                : (verticalSpeed < -0.1f ? FALLING_STATE : IN_AIR_STATE);
            CrossFadeIfNeeded(targetAnimator, recoveryState, _crouchRunExitTransitionDuration);
            return;
        }

        // Keep grounded locomotion pinned to the presentation-specific state
        // so join-mode shooting does not drift back to the default run tree.
        if (isGrounded)
        {
            CrossFadeIfNeeded(targetAnimator, GetActiveLocomotionState(targetAnimator), 0.08f);
            return;
        }

        ApplyWallRunState(targetAnimator, isGrounded, verticalSpeed);
    }

    public bool UsesSingleForwardRunController()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            return _useSingleForwardRunAnimation;
        }

        string controllerName = _animator.runtimeAnimatorController.name;
        return _useSingleForwardRunAnimation || controllerName.StartsWith("AC_NewPlayer2");
    }

    private void UpdateAnimation()
    {
        if (_animator == null)
        {
            return;
        }

        if (_shootAnimationTimer > 0f)
        {
            _shootAnimationTimer = Mathf.Max(0f, _shootAnimationTimer - Time.deltaTime);
        }

        if (_reloadAnimationTimer > 0f)
        {
            _reloadAnimationTimer = Mathf.Max(0f, _reloadAnimationTimer - Time.deltaTime);
        }

        if (!IsInjuredActive)
        {
            _injuredMoveAmount = 0f;
            _injuredReleaseTimer = 0f;
        }

        if (!IsCrouchingActive)
        {
            _crouchMoveAmount = 0f;
            _crouchReleaseTimer = 0f;
            _crouchMoveDirection = Vector2.up;
        }

        Vector2 input = _useNetworkAnimationState
            ? _networkAnimationInput
            : (_playerLocomotionInput != null ? _playerLocomotionInput.MovementInput : Vector2.zero);
        float deltaTime = Time.deltaTime;
        bool isGrounded = _useNetworkAnimationState
            ? _networkIsGrounded
            : (_playerController != null && _playerController.IsGrounded());
        bool isJumpTriggered = _useNetworkAnimationState
            ? _networkIsJumping
            : (_playerController != null ? _playerController.DidJumpThisFrame() : (_playerLocomotionInput != null && _playerLocomotionInput.JumpPressed));
        float verticalSpeed = _useNetworkAnimationState
            ? _networkVerticalSpeed
            : (_playerController != null ? _playerController.GetVerticalVelocity() : 0f);
        bool visualGrounded = GetStableGroundedState(isGrounded, verticalSpeed, isJumpTriggered, deltaTime);

        Vector2 targetAnimationInput = _useNetworkAnimationState
            ? Vector2.ClampMagnitude(input, 1f)
            : (UsesSingleForwardRunController()
                ? new Vector2(0f, GetAnimationSpeedFactor(input))
                : GetDirectionalAnimationInput(input));

        bool carryingActive = IsCarryingActive;
        bool beingCarriedActive = IsBeingCarriedActive;

        if (_useNetworkAnimationState)
        {
            bool releasingToIdle =
                targetAnimationInput.sqrMagnitude <= 0.0001f
                && visualGrounded
                && Mathf.Abs(verticalSpeed) <= 0.01f;

            float moveSpeed = releasingToIdle ? _networkAnimationReleaseSpeed : _networkAnimationBlendSpeed;
            _currentInputX = Mathf.MoveTowards(_currentInputX, targetAnimationInput.x, moveSpeed * deltaTime);
            _currentInputY = Mathf.MoveTowards(_currentInputY, targetAnimationInput.y, moveSpeed * deltaTime);
        }
        else if (carryingActive || beingCarriedActive)
        {
            float carryBlendSpeed = _networkAnimationReleaseSpeed;
            _currentInputX = Mathf.MoveTowards(_currentInputX, targetAnimationInput.x, carryBlendSpeed * deltaTime);
            _currentInputY = Mathf.MoveTowards(_currentInputY, targetAnimationInput.y, carryBlendSpeed * deltaTime);

            if (targetAnimationInput.sqrMagnitude <= 0.0001f)
            {
                _currentInputX = 0f;
                _currentInputY = 0f;
            }
        }
        else
        {
            _currentInputX = Mathf.Lerp(_currentInputX, targetAnimationInput.x, _smoothSpeed * deltaTime);
            _currentInputY = Mathf.Lerp(_currentInputY, targetAnimationInput.y, _smoothSpeed * deltaTime);
        }

        _lastAppliedAnimationInput = new Vector2(_currentInputX, _currentInputY);
        _lastAppliedGrounded = visualGrounded;
        _lastAppliedJumping = isJumpTriggered;
        _lastAppliedVerticalSpeed = verticalSpeed;

        ApplyAnimationStateToAllAnimators(_currentInputX, _currentInputY, visualGrounded, isJumpTriggered, verticalSpeed);
        _wasGrounded = visualGrounded;
    }

    private bool GetStableGroundedState(bool rawGrounded, float verticalSpeed, bool isJumpTriggered, float deltaTime)
    {
        if (isJumpTriggered)
        {
            _jumpAnimationLatchTimer = _jumpAnimationMinAirTime;
            _groundedStableTimer = 0f;
            return false;
        }

        if (_jumpAnimationLatchTimer > 0f)
        {
            _jumpAnimationLatchTimer = Mathf.Max(0f, _jumpAnimationLatchTimer - deltaTime);
        }

        bool canBeGrounded = rawGrounded && verticalSpeed <= 0.01f && _jumpAnimationLatchTimer <= 0f;
        if (canBeGrounded)
        {
            _groundedStableTimer += deltaTime;
        }
        else
        {
            _groundedStableTimer = 0f;
        }

        return canBeGrounded && _groundedStableTimer >= _landingGroundedBuffer;
    }

    private Vector2 GetDirectionalAnimationInput(Vector2 fallbackInput)
    {
        if (IsInjuredActive)
        {
            return GetInjuredAnimationInput(fallbackInput, Time.deltaTime);
        }

        if (IsCrouchingActive)
        {
            return GetCrouchAnimationInput(fallbackInput, Time.deltaTime);
        }

        float speedFactor = GetAnimationSpeedFactor(fallbackInput);

        if (_useNetworkAnimationState)
        {
            return Vector2.ClampMagnitude(fallbackInput, 1f);
        }

        if (_playerController == null)
        {
            return Vector2.ClampMagnitude(fallbackInput, 1f) * speedFactor;
        }

        Vector3 localVelocity = transform.InverseTransformDirection(_playerController.GetVelocity());
        Vector2 planarVelocity = new Vector2(localVelocity.x, localVelocity.z);

        if (planarVelocity.sqrMagnitude <= 0.0001f)
        {
            return Vector2.ClampMagnitude(fallbackInput, 1f) * speedFactor;
        }

        return Vector2.ClampMagnitude(planarVelocity.normalized * speedFactor, 1f);
    }

    private float GetAnimationSpeedFactor(Vector2 fallbackInput)
    {
        if (_useNetworkAnimationState)
        {
            return 1f;
        }

        if (_playerController == null)
        {
            return Mathf.Clamp01(fallbackInput.magnitude);
        }

        float maxAnimationSpeed = IsInjuredActive
            ? Mathf.Max(_playerController.GetInjuredMoveSpeed(), 0.01f)
            : IsCrouchingActive
                ? Mathf.Max(_playerController.GetCrouchMoveSpeed(), 0.01f)
            : Mathf.Max(_playerController.sprintSpeed, 0.01f);
        return Mathf.Clamp01(_playerController.GetHorizontalSpeed() / maxAnimationSpeed);
    }

    private Vector2 GetInjuredAnimationInput(Vector2 fallbackInput, float deltaTime)
    {
        if (_useNetworkAnimationState || _playerController == null)
        {
            float networkMoveAmount = Mathf.Clamp01(fallbackInput.magnitude);
            if (networkMoveAmount > 0.0001f)
            {
                _injuredMoveAmount = networkMoveAmount;
                _injuredReleaseTimer = _injuredReleaseBlendDuration;
            }
            else
            {
                _injuredMoveAmount = GetInjuredReleaseAmount(deltaTime);
            }

            return new Vector2(0f, _injuredMoveAmount);
        }

        float injuredSpeedFactor = Mathf.Clamp01(
            _playerController.GetHorizontalSpeed() / Mathf.Max(_playerController.GetInjuredMoveSpeed(), 0.01f));

        if (injuredSpeedFactor > 0.0001f)
        {
            _injuredMoveAmount = injuredSpeedFactor;
            _injuredReleaseTimer = _injuredReleaseBlendDuration;
        }
        else
        {
            _injuredMoveAmount = GetInjuredReleaseAmount(deltaTime);
        }

        return new Vector2(0f, _injuredMoveAmount);
    }

    private float GetInjuredReleaseAmount(float deltaTime)
    {
        if (_injuredMoveAmount <= 0.0001f)
        {
            _injuredReleaseTimer = 0f;
            return 0f;
        }

        _injuredReleaseTimer = Mathf.Max(0f, _injuredReleaseTimer - deltaTime);
        if (_injuredReleaseTimer <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(_injuredReleaseTimer / Mathf.Max(_injuredReleaseBlendDuration, 0.001f));
    }

    private Vector2 GetCrouchAnimationInput(Vector2 fallbackInput, float deltaTime)
    {
        Vector2 fallbackDirection = fallbackInput.sqrMagnitude > 0.0001f
            ? fallbackInput.normalized
            : _crouchMoveDirection;

        if (fallbackInput.sqrMagnitude > 0.0001f)
        {
            _crouchMoveAmount = 1f;
            _crouchReleaseTimer = _crouchReleaseBlendDuration;
            _crouchMoveDirection = fallbackDirection;
        }
        else
        {
            _crouchMoveAmount = GetCrouchReleaseAmount(deltaTime);
        }

        return Vector2.ClampMagnitude(_crouchMoveDirection * _crouchMoveAmount, 1f);
    }

    private float GetCrouchReleaseAmount(float deltaTime)
    {
        if (_crouchMoveAmount <= 0.0001f)
        {
            _crouchReleaseTimer = 0f;
            return 0f;
        }

        _crouchReleaseTimer = Mathf.Max(0f, _crouchReleaseTimer - deltaTime);
        if (_crouchReleaseTimer <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(_crouchReleaseTimer / Mathf.Max(_crouchReleaseBlendDuration, 0.001f));
    }

    private void ApplyWallRunState(Animator targetAnimator, bool isGrounded, float verticalSpeed)
    {
        if (targetAnimator == null)
        {
            return;
        }

        bool wallRunning = _useNetworkAnimationState
            ? _networkIsWallRunning
            : (_playerController != null && _playerController.IsWallRunning());
        if (wallRunning)
        {
            int wallRunSide = _useNetworkAnimationState
                ? _networkWallRunSide
                : (_playerController != null ? _playerController.GetWallRunSide() : 0);
            
            string targetState = wallRunSide < 0 ? WALL_SLIDE_RIGHT_STATE : WALL_SLIDE_LEFT_STATE;
            
            // Allow arm-specific wall run states to take precedence when in shooting mode
            if (_presentationMode == PresentationMode.Shooting)
            {
                if (wallRunSide < 0 && targetAnimator.HasState(0, _ak47WallRunRightStateHash))
                {
                    targetState = AK47_WALL_RUN_RIGHT_STATE;
                }
                else if (wallRunSide >= 0 && targetAnimator.HasState(0, _ak47WallRunLeftStateHash))
                {
                    targetState = AK47_WALL_RUN_LEFT_STATE;
                }
            }

            _lastWallRunState = targetState;
            _wallRunAnimationHoldTimer = _wallRunAnimationExitBuffer;
            CrossFadeIfNeeded(targetAnimator, targetState, 0.08f);
            return;
        }

        AnimatorStateInfo currentState = targetAnimator.GetCurrentAnimatorStateInfo(0);
        if (!currentState.IsName(WALL_SLIDE_LEFT_STATE) && !currentState.IsName(WALL_SLIDE_RIGHT_STATE))
        {
            return;
        }

        bool shouldHoldWallRunAnimation;
        if (_useNetworkAnimationState)
        {
            shouldHoldWallRunAnimation =
                !isGrounded
                && _networkAnimationInput.y > 0.1f
                && Mathf.Abs(_networkAnimationInput.x) > 0.1f;
        }
        else
        {
            shouldHoldWallRunAnimation =
                !isGrounded
                && _playerLocomotionInput != null
                && _playerLocomotionInput.JumpHeld
                && _playerLocomotionInput.MovementInput.y > 0.1f
                && Mathf.Abs(_playerLocomotionInput.MovementInput.x) > 0.1f;
        }

        if (_wallRunAnimationHoldTimer > 0f && shouldHoldWallRunAnimation)
        {
            _wallRunAnimationHoldTimer = Mathf.Max(0f, _wallRunAnimationHoldTimer - Time.deltaTime);
            CrossFadeIfNeeded(targetAnimator, _lastWallRunState, 0.05f);
            return;
        }

        _wallRunAnimationHoldTimer = 0f;

        string recoveryState = isGrounded
            ? (IsCrouchingActive ? CROUCH_STATE : GetActiveLocomotionState(targetAnimator))
            : (verticalSpeed < -0.1f ? FALLING_STATE : IN_AIR_STATE);

        CrossFadeIfNeeded(targetAnimator, recoveryState, 0.08f);
    }

    private bool ApplyCarryRecoveryState(Animator targetAnimator, bool isGrounded, float verticalSpeed, bool isInjuredActive, bool isCrouchingActive)
    {
        if (targetAnimator == null)
        {
            return false;
        }

        AnimatorStateInfo currentState = targetAnimator.GetCurrentAnimatorStateInfo(0);
        bool isCarryState =
            currentState.IsName(CARRYING_ME_STATE)
            || currentState.IsName(CARRYING_IDLE_STATE)
            || currentState.IsName(CARRYING_RUN_STATE);

        if (!isCarryState)
        {
            return false;
        }
        
        string activeLocomotion = GetActiveLocomotionState(targetAnimator);

        string recoveryState = isGrounded
            ? (isCrouchingActive ? CROUCH_STATE : activeLocomotion)
            : (verticalSpeed < -0.1f ? FALLING_STATE : IN_AIR_STATE);

        CrossFadeIfNeeded(targetAnimator, recoveryState, 0.06f);
        return true;
    }

    private static void CrossFadeIfNeeded(Animator targetAnimator, string stateName, float duration)
    {
        int targetStateHash = Animator.StringToHash(stateName);
        if (targetAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash == targetStateHash)
        {
            return;
        }

        if (targetAnimator.IsInTransition(0) && targetAnimator.GetNextAnimatorStateInfo(0).fullPathHash == targetStateHash)
        {
            return;
        }

        targetAnimator.CrossFadeInFixedTime(targetStateHash, duration, 0);
    }

    private void OnAnimatorMove()
    {
        // Ignore root motion so character motion stays controller-driven.
    }
    #endregion
}
