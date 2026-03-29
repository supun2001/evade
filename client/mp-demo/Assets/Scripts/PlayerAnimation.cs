using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    #region Class Variables
    [SerializeField] private Animator _animator;
    [SerializeField] private RuntimeAnimatorController _childAnimatorControllerOverride;
    public Animator Animator => _animator;
    [SerializeField] private float animationSmoothTime = 0.1f;
    [SerializeField] private bool _useSingleForwardRunAnimation = false;

    private PlayerLocomotionInput _playerLocomotionInput;
    private PlayerController _playerController;
    private Animator[] _childAnimators = System.Array.Empty<Animator>();

    private static readonly int _inputXHash = Animator.StringToHash("inputX");
    private static readonly int _inputYHash = Animator.StringToHash("inputY");
    private static readonly int _groundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int _jumpHash = Animator.StringToHash("IsJumping");

    private float _currentInputX;
    private float _currentInputY;
    private float _smoothSpeed;
    private bool _wasGrounded;
    private bool _useNetworkAnimationState;
    private Vector2 _networkAnimationInput;
    private bool _networkIsGrounded = true;
    private bool _networkIsJumping;

    private const float DEFAULT_SMOOTH_SPEED = 10f;
    #endregion

    #region Setup
    private void Awake()
    {
        _playerLocomotionInput = GetComponent<PlayerLocomotionInput>();
        _playerController = GetComponent<PlayerController>();
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

        UpdateAnimation();
    }
    #endregion

    #region Animation
    public void SetUseNetworkAnimationState(bool useNetworkAnimationState)
    {
        _useNetworkAnimationState = useNetworkAnimationState;
    }

    public void ApplyNetworkState(float inputX, float inputY, bool isGrounded, bool isJumping)
    {
        _useNetworkAnimationState = true;
        _networkAnimationInput = new Vector2(inputX, inputY);
        _networkIsGrounded = isGrounded;
        _networkIsJumping = isJumping;
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
        if (targetAnimator == null || _childAnimatorControllerOverride == null)
        {
            return;
        }

        if (targetAnimator.gameObject == gameObject)
        {
            return;
        }

        if (targetAnimator.runtimeAnimatorController == _childAnimatorControllerOverride)
        {
            return;
        }

        targetAnimator.runtimeAnimatorController = _childAnimatorControllerOverride;
        targetAnimator.applyRootMotion = false;
        targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        targetAnimator.speed = 1f;
        targetAnimator.Rebind();
        targetAnimator.Update(0f);
    }

    private void ApplyAnimationStateToAllAnimators(float inputX, float inputY, bool isGrounded, bool isJumping)
    {
        if (_childAnimators == null || _childAnimators.Length == 0)
        {
            if (_animator != null)
            {
                ApplyAnimationState(_animator, inputX, inputY, isGrounded, isJumping);
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
            ApplyAnimationState(targetAnimator, inputX, inputY, isGrounded, isJumping);
        }
    }

    private void ApplyAnimationState(Animator targetAnimator, float inputX, float inputY, bool isGrounded, bool isJumping)
    {
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

        Vector2 input = _useNetworkAnimationState
            ? _networkAnimationInput
            : (_playerLocomotionInput != null ? _playerLocomotionInput.MovementInput : Vector2.zero);
        float deltaTime = Time.deltaTime;
        bool isGrounded = _useNetworkAnimationState
            ? _networkIsGrounded
            : (_playerController != null && _playerController.IsGrounded());
        bool isJumping = _useNetworkAnimationState
            ? _networkIsJumping
            : (_playerLocomotionInput != null && _playerLocomotionInput.JumpPressed);

        Vector2 targetAnimationInput = UsesSingleForwardRunController()
            ? new Vector2(0f, GetAnimationSpeedFactor(input))
            : GetDirectionalAnimationInput(input);

        _currentInputX = Mathf.Lerp(_currentInputX, targetAnimationInput.x, _smoothSpeed * deltaTime);
        _currentInputY = Mathf.Lerp(_currentInputY, targetAnimationInput.y, _smoothSpeed * deltaTime);

        ApplyAnimationStateToAllAnimators(_currentInputX, _currentInputY, isGrounded, isJumping);
        _wasGrounded = isGrounded;
    }

    private Vector2 GetDirectionalAnimationInput(Vector2 fallbackInput)
    {
        float speedFactor = GetAnimationSpeedFactor(fallbackInput);

        if (_useNetworkAnimationState || _playerController == null)
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
        if (_useNetworkAnimationState || _playerController == null)
        {
            return Mathf.Clamp01(fallbackInput.magnitude);
        }

        float maxAnimationSpeed = Mathf.Max(_playerController.sprintSpeed, 0.01f);
        return Mathf.Clamp01(_playerController.GetHorizontalSpeed() / maxAnimationSpeed);
    }

    private void OnAnimatorMove()
    {
        // Ignore root motion so character motion stays controller-driven.
    }
    #endregion
}
