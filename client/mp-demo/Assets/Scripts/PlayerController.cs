using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using PlayerCharacterController;
using System;

[DefaultExecutionOrder(-1)]
public class PlayerController : MonoBehaviour
{
    #region Class Variables
    [Header("Components")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private Camera _playerCamera;

    [Header("Base Movement")]
    public float runAcceleration = 0.25f;
    public float runSpeed = 4f;
    public float drag = 0.1f;
    public float gravity = 25f;
    public float jumpForce = 1f;

    [Header("Camera Settings")]
    public float lookSenseH = 0.1f;
    public float lookSenseV = 0.1f;
    public float lookLimitV = 89f;

    private PlayerLocomotionInput _playerLocomotionInput;
    private Transform _transform;
    private Transform _cameraTransform;
    
    private Vector2 _cameraRotation = Vector2.zero;
    private float _playerRotationY = 0f;
    private float _verticalVelocity = 0f;
    private Vector3 _horizontalVelocity = Vector3.zero;
    
    private float _dragSqr;
    private const float JUMP_VELOCITY_MULTIPLIER = 3f;
    #endregion

    #region Setup
    private void Awake() {
        _playerLocomotionInput = GetComponent<PlayerLocomotionInput>();
        _transform = transform;
        _cameraTransform = _playerCamera.transform;
    }
    
    private void Start() {
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;
        
        _dragSqr = drag * drag;
    }
    #endregion

    #region Update
    private void Update() {
        if (!_playerLocomotionInput.InputEnabled) return;

        HandleCursorLock();
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

    private void LateUpdate() {
        Vector2 lookInput = _playerLocomotionInput.LookInput;
        
        // Update camera rotation
        _cameraRotation.x += lookSenseH * lookInput.x;
        _cameraRotation.y = Mathf.Clamp(_cameraRotation.y + lookSenseV * lookInput.y, -lookLimitV, lookLimitV);
        
        _playerRotationY += lookSenseH * lookInput.x;
        _transform.rotation = Quaternion.Euler(0f, _playerRotationY, 0f);

        _cameraTransform.rotation = Quaternion.Euler(_cameraRotation.y, _cameraRotation.x, 0f);
    }
    #endregion

    #region Movement
    private void HandleHorizontalMovement() {
        Vector2 movementInput = _playerLocomotionInput.MovementInput;
        
        if (movementInput.sqrMagnitude < 0.001f && _horizontalVelocity.sqrMagnitude < 0.001f) {
            _horizontalVelocity = Vector3.zero;
            return;
        }
        
        Vector3 cameraForward = _cameraTransform.forward;
        Vector3 cameraRight = _cameraTransform.right;
        
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
        
        _horizontalVelocity = Vector3.ClampMagnitude(_horizontalVelocity, runSpeed);
        
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
}
