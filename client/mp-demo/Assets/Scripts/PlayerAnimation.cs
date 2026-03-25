using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    #region Class Variables
    [SerializeField] private Animator _animator;
    public Animator Animator => _animator;
    [SerializeField] private float rotationSpeed = 10f; 
    [SerializeField] private float animationSmoothTime = 0.1f; 
    
    private PlayerLocomotionInput _playerLocomotionInput;
    private PlayerController _playerController;

    private static readonly int _inputXHash = Animator.StringToHash("inputX");
    private static readonly int _inputYHash = Animator.StringToHash("inputY");
    private static readonly int _groundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int _jumpHash = Animator.StringToHash("IsJumping");

    private float _currentInputX = 0f;
    private float _currentInputY = 0f;
    private float _smoothSpeed;
    private bool _wasGrounded;
    
    private const float INPUT_THRESHOLD = 0.001f;
    private const float DEFAULT_SMOOTH_SPEED = 10f;
    #endregion

    #region Setup
    private void Awake() {
        _playerLocomotionInput = GetComponent<PlayerLocomotionInput>();
        _playerController = GetComponent<PlayerController>();
    }
    
    private void Start() {
        _smoothSpeed = animationSmoothTime > 0 ? 1f / animationSmoothTime : DEFAULT_SMOOTH_SPEED;
        _wasGrounded = _playerController.IsGrounded();
    }
    #endregion

    #region Update
    private void Update() {
        UpdateAnimation();
    }
    #endregion

    #region Animation
    private void UpdateAnimation() {
        Vector2 input = _playerLocomotionInput.MovementInput;
        float deltaTime = Time.deltaTime;

        float targetX = input.x;
        float targetY = input.y;
        
        _currentInputX = Mathf.Lerp(_currentInputX, targetX, _smoothSpeed * deltaTime);
        _currentInputY = Mathf.Lerp(_currentInputY, targetY, _smoothSpeed * deltaTime);

        _animator.SetFloat(_inputXHash, _currentInputX);
        _animator.SetFloat(_inputYHash, _currentInputY);

        bool isGrounded = _playerController.IsGrounded();
        if (isGrounded != _wasGrounded) {
            _animator.SetBool(_groundedHash, isGrounded);
            _wasGrounded = isGrounded;
        }
        
        _animator.SetBool(_jumpHash, _playerLocomotionInput.JumpPressed);
    }
    #endregion
}
