using UnityEngine;
using Colyseus.Schema;
using System.Collections.Generic;
using UnityEngine.UIElements;

public class NetworkPlayer : MonoBehaviour
{
    private Player playerState;
    private bool isLocal;
    
    private Vector3 targetPos;
    private Quaternion targetRot;
    private float lerpSpeed = 15f;
    private Vector3 _remoteVelocity;

    // References
    private PlayerController controller;
    private PlayerLocomotionInput input;
    private PlayerAnimation anim; 
    private Animator animator;

    public void Initialize(Player state, bool isLocalPlayer)
    {
        playerState = state;
        isLocal = isLocalPlayer;
        
        controller = GetComponent<PlayerController>();
        input = GetComponent<PlayerLocomotionInput>();
        anim = GetComponent<PlayerAnimation>();
        
        // Try to get the animator from the PlayerAnimation script first (most reliable)
        if (anim != null) animator = anim.Animator;
        // Fallback checks
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (!isLocal)
        {
            if (controller) controller.enabled = false;
            
            if (input) 
            {
                input.enabled = false;
                if (input.Controls != null) input.Controls.Disable(); 
            }

            if (anim) anim.SetUseNetworkAnimationState(true);

            UIDocument hudDocument = GetComponentInChildren<UIDocument>(true);
            if (hudDocument != null)
            {
                hudDocument.enabled = false;
            }
        }
    }

    private float nextSendTime = 0f;
    public float sendInterval = 0.05f; // 20 times per second
    
    // Jump latching
    private bool _jumpQueued;

    // Animator Hashes
    private static readonly int InputXHash = Animator.StringToHash("inputX");
    private static readonly int InputYHash = Animator.StringToHash("inputY");
    private static readonly int GroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int JumpHash = Animator.StringToHash("IsJumping");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");

    private void Update()
    {
        if (isLocal)
        {
            // Latch Jump input so we don't miss it between network ticks
            if (input && input.JumpPressed) 
            {
                _jumpQueued = true;
            }

            if (Time.time >= nextSendTime)
            {
                SendLocalState();
                _jumpQueued = false; // Reset callback
                nextSendTime = Time.time + sendInterval;
            }
        }
        else
        {
            UpdateRemoteState();
            InterpolateRemotePlayer();
        }
    }

    private void SendLocalState()
    {
        if (NetworkManager.Instance == null) return;

        float camRx = Camera.main ? Camera.main.transform.localEulerAngles.x : 0;
        float camRy = Camera.main ? Camera.main.transform.localEulerAngles.y : 0;

        Vector2 animationInput = input ? input.MovementInput : Vector2.zero;
        bool isGrounded = controller != null && controller.IsGrounded();
        bool isJumping = _jumpQueued;
        float verticalSpeed = controller != null ? controller.GetVerticalVelocity() : 0f;
        bool isInjured = anim != null && anim.IsInjuredActive;
        bool isCrouching = controller != null && controller.IsCrouching();
        bool isWallRunning = controller != null && controller.IsWallRunning();
        int wallRunSide = controller != null ? controller.GetWallRunSide() : 0;

        if (anim != null)
        {
            anim.GetAnimationSyncState(out Vector2 syncedAnimationInput, out bool syncedGrounded, out bool syncedJumping, out float syncedVerticalSpeed);
            animationInput = syncedAnimationInput;
            isGrounded = syncedGrounded;
            isJumping = isJumping || syncedJumping;
            verticalSpeed = syncedVerticalSpeed;
        }

        Vector3 velocity = controller != null ? controller.GetVelocity() : Vector3.up * verticalSpeed;

        NetworkManager.Instance.SendPlayerUpdate(
            transform.position,
            transform.eulerAngles.y,
            velocity,
            animationInput.x,
            animationInput.y,
            isGrounded,
            isJumping,
            isInjured,
            isCrouching,
            isWallRunning,
            wallRunSide,
            input ? input.MovementInput : Vector2.zero,
            controller != null ? controller.GetVisualYaw() : 180f,
            new Vector2(camRx, camRy)
        );
    }

    private void UpdateRemoteState()
    {
        _remoteVelocity = new Vector3(playerState.velocityX, playerState.velocityY, playerState.velocityZ);
        Vector3 extrapolatedOffset = new Vector3(_remoteVelocity.x, 0f, _remoteVelocity.z) * sendInterval;
        targetPos = new Vector3(playerState.x, playerState.y, playerState.z) + extrapolatedOffset;
        targetRot = Quaternion.Euler(0, playerState.rotationY, 0);

        if (animator)
        {
            if (anim != null)
            {
                anim.ApplyNetworkState(
                    playerState.animInputX,
                    playerState.animInputY,
                    playerState.isGrounded,
                    playerState.isJumping,
                    playerState.velocityY,
                    playerState.isInjured,
                    playerState.isCrouching,
                    playerState.isWallRunning,
                    Mathf.RoundToInt(playerState.wallRunSide));
            }
            else
            {
                animator.SetFloat(InputXHash, playerState.animInputX);
                animator.SetFloat(InputYHash, playerState.animInputY);
                animator.SetBool(GroundedHash, playerState.isGrounded);
                animator.SetBool(JumpHash, playerState.isJumping);
                animator.SetFloat(VerticalSpeedHash, playerState.velocityY);
            }

            if (controller != null)
            {
                controller.ApplyRemoteVisualYaw(playerState.visualYaw);
            }
        }
    }

    private void InterpolateRemotePlayer()
    {
        float positionBlend = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
        float rotationBlend = 1f - Mathf.Exp(-(lerpSpeed + 4f) * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, targetPos, positionBlend);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationBlend);
    }
}
