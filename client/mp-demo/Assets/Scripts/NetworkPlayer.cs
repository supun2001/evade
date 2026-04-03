using UnityEngine;
using Colyseus.Schema;
using System.Collections.Generic;
using UnityEngine.UIElements;

public class NetworkPlayer : MonoBehaviour
{
    private const float MaxAcceptedServerHitDistance = 1.85f;

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
    private float _lastProcessedHitTriggerId = -1f;
    private bool _isInitialized;

    public void Initialize(Player state, bool isLocalPlayer)
    {
        playerState = state;
        isLocal = isLocalPlayer;
        _lastProcessedHitTriggerId = state != null ? state.hitTriggerId : 0f;
        
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

        _isInitialized = true;
    }

    public bool TryGetSessionId(out string sessionId)
    {
        sessionId = playerState != null ? playerState.sessionId : null;
        return !string.IsNullOrEmpty(sessionId);
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
        if (!_isInitialized || playerState == null)
        {
            return;
        }

        if (isLocal)
        {
            HandleServerHitTrigger();
            ApplyCarryStateFromServer();

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

    private void HandleServerHitTrigger()
    {
        if (!isLocal || controller == null || playerState == null)
        {
            return;
        }

        if (playerState.hitTriggerId <= 0f || playerState.hitTriggerId <= _lastProcessedHitTriggerId)
        {
            return;
        }

        Vector3 hitSource = new Vector3(playerState.hitSourceX, playerState.hitSourceY, playerState.hitSourceZ);
        Vector3 playerPosition = transform.position;
        float hitDistance = Vector2.Distance(
            new Vector2(playerPosition.x, playerPosition.z),
            new Vector2(hitSource.x, hitSource.z));

        if (hitDistance > MaxAcceptedServerHitDistance)
        {
            Debug.LogWarning($"Ignoring remote nextbot hit outside accepted range. Distance: {hitDistance:F2}");
            _lastProcessedHitTriggerId = playerState.hitTriggerId;
            return;
        }

        if (controller.TriggerNextbotHit(hitSource))
        {
            _lastProcessedHitTriggerId = playerState.hitTriggerId;
            return;
        }

        if (controller.IsInjuredOrHitReacting())
        {
            _lastProcessedHitTriggerId = playerState.hitTriggerId;
        }
    }

    private void SendLocalState()
    {
        if (NetworkManager.Instance == null) return;

        Vector2 cameraRotation = controller != null ? controller.GetCameraRotation() : Vector2.zero;

        Vector2 animationInput = input ? input.MovementInput : Vector2.zero;
        bool isGrounded = controller != null && controller.IsGrounded();
        bool isJumping = _jumpQueued;
        float verticalSpeed = controller != null ? controller.GetVerticalVelocity() : 0f;
        bool isInjured = anim != null && anim.IsInjuredActive;
        bool isCrouching = controller != null && controller.IsCrouching();
        bool isWallRunning = controller != null && controller.IsWallRunning();
        int wallRunSide = controller != null ? controller.GetWallRunSide() : 0;
        bool isHitReacting = false;
        float hitReactionTimeRemaining = 0f;
        float hitReactionPitch = 0f;
        float hitReactionRoll = 0f;
        float hitReactionSeed = 0f;

        if (anim != null)
        {
            anim.GetAnimationSyncState(out Vector2 syncedAnimationInput, out bool syncedGrounded, out bool syncedJumping, out float syncedVerticalSpeed);
            animationInput = syncedAnimationInput;
            isGrounded = syncedGrounded;
            isJumping = isJumping || syncedJumping;
            verticalSpeed = syncedVerticalSpeed;
        }

        Vector3 velocity = controller != null ? controller.GetVelocity() : Vector3.up * verticalSpeed;
        if (controller != null)
        {
            controller.GetHitReactionSyncState(
                out isHitReacting,
                out hitReactionTimeRemaining,
                out hitReactionPitch,
                out hitReactionRoll,
                out hitReactionSeed);
        }

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
            cameraRotation,
            isHitReacting,
            hitReactionTimeRemaining,
            hitReactionPitch,
            hitReactionRoll,
            hitReactionSeed
        );
    }

    private void UpdateRemoteState()
    {
        if (playerState == null)
        {
            return;
        }

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
        }

        if (controller != null)
        {
            controller.ApplyNetworkCarryState(
                playerState.isCarrying,
                playerState.isBeingCarried,
                playerState.carriedPlayerSessionId,
                playerState.carrierSessionId);

            controller.ApplyRemoteVisualState(
                new Vector2(playerState.moveInputX, playerState.moveInputY),
                playerState.isInjured,
                playerState.isCrouching);

            controller.ApplyRemoteHitReactionState(
                playerState.isHitReacting,
                playerState.hitReactionTimeRemaining,
                playerState.hitReactionPitch,
                playerState.hitReactionRoll,
                playerState.hitReactionSeed);

            if (!playerState.isHitReacting)
            {
                controller.ApplyRemoteVisualYaw(playerState.visualYaw);
            }

            if (playerState.isBeingCarried && controller.TryGetCarriedFollowPose(out Vector3 carriedTargetPosition, out Quaternion carriedTargetRotation))
            {
                targetPos = carriedTargetPosition;
                targetRot = carriedTargetRotation;
                _remoteVelocity = Vector3.zero;
            }
        }
    }

    private void InterpolateRemotePlayer()
    {
        if (playerState != null
            && playerState.isBeingCarried
            && controller != null
            && controller.TryGetCarriedFollowPose(out Vector3 carriedTargetPosition, out Quaternion carriedTargetRotation))
        {
            transform.position = carriedTargetPosition;
            transform.rotation = carriedTargetRotation;
            return;
        }

        float positionBlend = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
        float rotationBlend = 1f - Mathf.Exp(-(lerpSpeed + 4f) * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, targetPos, positionBlend);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationBlend);
    }

    private void ApplyCarryStateFromServer()
    {
        if (!isLocal || controller == null || playerState == null)
        {
            return;
        }

        controller.ApplyNetworkCarryState(
            playerState.isCarrying,
            playerState.isBeingCarried,
            playerState.carriedPlayerSessionId,
            playerState.carrierSessionId);
    }
}
