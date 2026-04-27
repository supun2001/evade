using UnityEngine;
using UnityEngine.InputSystem;
using PlayerCharacterController;

[DefaultExecutionOrder(-2)]
public class PlayerLocomotionInput : MonoBehaviour,
    PlayerControls.IPlayerLocomotionMapActions
{
    #region  Class Variables
    public PlayerControls Controls { get; private set; }
    public Vector2 MovementInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool CrouchPressedThisFrame { get; private set; }
    public bool CrouchHeld { get; private set; }
    public bool IsUsingSimulatedInput => _useSimulatedInput;

    private bool _useSimulatedInput;
    #endregion

    #region Setup

    private void OnEnable()
    {
        Controls = new PlayerControls();

        Controls.PlayerLocomotionMap.SetCallbacks(this);
        Controls.PlayerLocomotionMap.Enable();
    }

    private void OnDisable()
    {
        Controls.PlayerLocomotionMap.RemoveCallbacks(this);
        Controls.PlayerLocomotionMap.Disable();
        JumpHeld = false;
    }

    #endregion

    #region Late Update

    private void Update()
    {
        if (_useSimulatedInput)
        {
            return;
        }

        if (!InputEnabled)
        {
            CrouchHeld = false;
            CrouchPressedThisFrame = false;
            return;
        }

        CrouchPressedThisFrame = Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
        CrouchHeld = Keyboard.current != null && Keyboard.current.cKey.isPressed;
    }

    private void LateUpdate() {
        JumpPressed = false;
        CrouchPressedThisFrame = false;

        if (_useSimulatedInput)
        {
            return;
        }
    }

    #endregion

    public void SetSimulatedInputEnabled(bool useSimulatedInput)
    {
        _useSimulatedInput = useSimulatedInput;

        if (!useSimulatedInput)
        {
            ResetSimulationState();
        }
    }

    public void ApplySimulatedInput(
        Vector2 movementInput,
        Vector2 lookInput,
        bool jumpHeld,
        bool jumpPressed,
        bool crouchHeld,
        bool crouchPressedThisFrame)
    {
        _useSimulatedInput = true;
        MovementInput = InputEnabled ? movementInput : Vector2.zero;
        LookInput = InputEnabled ? lookInput : Vector2.zero;
        JumpHeld = InputEnabled && jumpHeld;
        JumpPressed = InputEnabled && jumpPressed;
        CrouchHeld = InputEnabled && crouchHeld;
        CrouchPressedThisFrame = InputEnabled && crouchPressedThisFrame;
    }

    public void ResetSimulationState()
    {
        MovementInput = Vector2.zero;
        LookInput = Vector2.zero;
        JumpHeld = false;
        JumpPressed = false;
        CrouchHeld = false;
        CrouchPressedThisFrame = false;
    }

    #region Input Actions

    public bool InputEnabled = true;

    public void OnMovement(InputAction.CallbackContext context)
    {
        if (_useSimulatedInput)
        {
            return;
        }

        if (!InputEnabled)
        {
            MovementInput = Vector2.zero;
            return;
        }
        MovementInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if (_useSimulatedInput)
        {
            return;
        }

        if (!InputEnabled)
        {
            LookInput = Vector2.zero;
            return;
        }
        LookInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (_useSimulatedInput)
        {
            return;
        }

        if (!InputEnabled)
        {
            JumpHeld = false;
            return;
        }

        if (context.canceled)
        {
            JumpHeld = false;
            return;
        }

        if (context.started || context.performed)
        {
            JumpHeld = true;
        }

        if (context.performed)
        {
            JumpPressed = true;
        }
    }

 

    #endregion
}
