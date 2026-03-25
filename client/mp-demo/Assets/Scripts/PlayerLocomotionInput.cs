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
    }

    #endregion

    #region Late Update

    private void LateUpdate() {
        JumpPressed = false;
    }

    #endregion

    #region Input Actions

    public bool InputEnabled = true;

    public void OnMovement(InputAction.CallbackContext context)
    {
        if (!InputEnabled)
        {
            MovementInput = Vector2.zero;
            return;
        }
        MovementInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if (!InputEnabled)
        {
            LookInput = Vector2.zero;
            return;
        }
        LookInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.performed || !InputEnabled)
        {
            return;
        }

        JumpPressed = true;
    }

 

    #endregion
}
