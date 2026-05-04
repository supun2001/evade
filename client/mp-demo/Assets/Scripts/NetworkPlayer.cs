using UnityEngine;
using Colyseus.Schema;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class NetworkPlayer : MonoBehaviour
{
    private const float MaxAcceptedServerHitDistance = 1.85f;
    private const float DebugClickLogCooldown = 0.2f;

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
    private float _lastProcessedShotTriggerId = -1f;
    private bool _isInitialized;
    private float _nextDebugClickLogTime;

    public bool IsLocalPlayer => isLocal;

    public void Initialize(Player state, bool isLocalPlayer)
    {
        playerState = state;
        isLocal = isLocalPlayer;
        _lastProcessedHitTriggerId = state != null ? state.hitTriggerId : 0f;
        _lastProcessedShotTriggerId = state != null ? state.shotTriggerId : 0f;
        
        controller = GetComponent<PlayerController>();
        input = GetComponent<PlayerLocomotionInput>();
        anim = GetComponent<PlayerAnimation>();
        
        // Try to get the animator from the PlayerAnimation script first (most reliable)
        if (anim != null) animator = anim.Animator;
        // Fallback checks
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        bool useShootingPresentation = NetworkManager.Instance != null
            && (NetworkManager.Instance.JoinGameUsesShootingMode
                || NetworkManager.Instance.IsMultiplayerShootingPresentationEnabled);

        if (controller != null)
        {
            controller.SetSimulationControlled(!isLocal);
            controller.SetCombatModeActive(useShootingPresentation);
            if (useShootingPresentation)
            {
                controller.SetCombatHealth(controller.MaxHealth);
            }
            controller.SetLocalCharacterAudio(isLocal);
        }

        if (anim != null)
        {
            anim.SetShootingModeActive(useShootingPresentation);
        }

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

    public void BeginRespawnCountdown(float durationSeconds)
    {
        controller?.BeginRespawnCountdown(durationSeconds);
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
    private float _lastKillsCount = 0f;

    private void Update()
    {
        if (!_isInitialized || playerState == null)
        {
            return;
        }

        if (isLocal)
        {
            if (controller != null && playerState.maxCombatHealth > 0f)
            {
                controller.SetCombatHealth(playerState.combatHealth);
            }

            NetworkManager manager = NetworkManager.Instance;
            bool fireInputRequested = Mouse.current != null
                && (Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasPressedThisFrame);
            bool useShootingPresentation = manager != null
                && (manager.JoinGameUsesShootingMode
                    || manager.IsMultiplayerShootingPresentationEnabled);

            if (!useShootingPresentation
                && fireInputRequested
                && controller != null
                && !controller.IsSpectating())
            {
                useShootingPresentation = true;
                manager?.SetMultiplayerShootingPresentationEnabled(true);
                controller.SetSimulationControlled(false);
                controller.SetCombatModeActive(true);
                anim?.SetShootingModeActive(true);
                Debug.Log("[JoinShootDebug] Local fire input forced shooting mode on because join flags were false.");
            }

            if (Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame
                && Time.unscaledTime >= _nextDebugClickLogTime)
            {
                _nextDebugClickLogTime = Time.unscaledTime + DebugClickLogCooldown;
                Debug.Log(
                    $"[JoinShootDebug] NetworkPlayer local click seen | " +
                    $"name={gameObject.name}, shootingPresentation={useShootingPresentation}, " +
                    $"controller={(controller != null)}, input={(input != null)}, anim={(anim != null)}, " +
                    $"combat={(controller != null && controller.IsCombatModeActive)}, " +
                    $"spectating={(controller != null && controller.IsSpectating())}, " +
                    $"inputEnabled={(input != null && input.InputEnabled)}, " +
                    $"mousePressed={Mouse.current.leftButton.isPressed}, mousePressedThisFrame={Mouse.current.leftButton.wasPressedThisFrame}, " +
                    $"nmNull={(manager == null)}, " +
                    $"joinShoot={(manager != null && manager.JoinGameUsesShootingMode)}, " +
                    $"presentationFlag={(manager != null && manager.IsMultiplayerShootingPresentationEnabled)}, " +
                    $"roomId={(manager != null ? manager.currentRoomId : "null")}, " +
                    $"localSession={(manager != null ? manager.LocalSessionId : "null")}");
            }

            if (useShootingPresentation)
            {
                if (input != null)
                {
                    input.enabled = true;
                    input.InputEnabled = true;
                    if (input.Controls != null)
                    {
                        input.Controls.PlayerLocomotionMap.Enable();
                    }
                }

                if (controller != null && controller.IsSpectating())
                {
                    controller.ExitSpectateMode();
                }

                if (controller != null && !controller.IsCombatModeActive)
                {
                    controller.SetCombatModeActive(true);
                }

                controller?.EnsureSingleLocalAudioListener();

                if (anim != null)
                {
                    anim.SetShootingModeActive(true);
                }

                if (Mouse.current != null
                    && (Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasPressedThisFrame)
                    && controller != null)
                {
                    controller.TryFireCombatShotFromExternalInput();
                }
            }

            if (isLocal)
            {
                if (playerState != null && controller != null)
                {
                    if (playerState.maxCombatHealth > 0f)
                    {
                        controller.SetCombatHealth(playerState.combatHealth);
                    }
                }
            }

            if (controller != null && controller.IsSpectating())
            {
                return;
            }

            if (playerState.isEliminated && controller != null)
            {
                controller.ApplyNetworkEliminated();
            }
            else if (playerState.isInjured && controller != null && !controller.IsInjuredOrHitReacting())
            {
                controller.ApplyNetworkInjured();
            }
            else if (!playerState.isInjured
                && !playerState.isEliminated
                && controller != null
                && controller.IsInjuredOrHitReacting()
                && !controller.IsAwaitingAuthoritativeNextbotHit())
            {
                controller.ApplyNetworkRevive();
            }

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
            Debug.LogWarning($"Server nextbot hit arrived outside accepted shove range. Applying injured fallback. Distance: {hitDistance:F2}");
            controller.ApplyNetworkInjured();
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
        float speedBoostMultiplier = controller != null ? controller.GetSyncedSpeedBoostMultiplier() : 1f;
        float speedBoostTimeRemaining = controller != null ? controller.GetSyncedSpeedBoostTimeRemaining() : 0f;
        float jumpBoostMultiplier = controller != null ? controller.GetSyncedJumpBoostMultiplier() : 1f;
        float jumpBoostTimeRemaining = controller != null ? controller.GetSyncedJumpBoostTimeRemaining() : 0f;
        bool isShootingMode = (anim != null && anim.IsShootingModeActive)
            || (controller != null && controller.IsCombatModeActive);
        float shotTriggerId = controller != null ? controller.GetCombatShotTriggerId() : 0f;

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
            hitReactionSeed,
            speedBoostMultiplier,
            speedBoostTimeRemaining,
            jumpBoostMultiplier,
            jumpBoostTimeRemaining,
            isShootingMode,
            shotTriggerId
        );
    }

    private void UpdateRemoteState()
    {
        if (playerState == null)
        {
            return;
        }

        if (controller != null)
        {
            if (playerState.maxCombatHealth > 0f)
            {
                controller.SetCombatHealth(playerState.combatHealth);
            }

            controller.EnsureRemoteFullBodyVisible();
        }

        _remoteVelocity = new Vector3(playerState.velocityX, playerState.velocityY, playerState.velocityZ);
        Vector3 extrapolatedOffset = new Vector3(_remoteVelocity.x, 0f, _remoteVelocity.z) * sendInterval;
        targetPos = new Vector3(playerState.x, playerState.y, playerState.z) + extrapolatedOffset;
        targetRot = Quaternion.Euler(0, playerState.rotationY, 0);

        if (animator)
        {
            if (anim != null)
            {
                bool remoteShootingMode = playerState.isShootingMode;
                anim.SetShootingModeActive(remoteShootingMode);
                if (controller != null && controller.IsCombatModeActive != remoteShootingMode)
                {
                    controller.SetCombatModeActive(remoteShootingMode);
                }

                if (remoteShootingMode && playerState.shotTriggerId > 0f && playerState.shotTriggerId > _lastProcessedShotTriggerId)
                {
                    anim.PlayShootAnimation();
                    _lastProcessedShotTriggerId = playerState.shotTriggerId;
                }

                anim.ApplyNetworkState(
                    playerState.animInputX,
                    playerState.animInputY,
                    playerState.isGrounded,
                    playerState.isJumping,
                    playerState.velocityY,
                    playerState.isInjured || playerState.isEliminated,
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
                playerState.isInjured || playerState.isEliminated,
                playerState.isCrouching);

            controller.ApplyRemoteHitReactionState(
                playerState.isHitReacting,
                playerState.hitReactionTimeRemaining,
                playerState.hitReactionPitch,
                playerState.hitReactionRoll,
                playerState.hitReactionSeed,
                playerState.hitTriggerId,
                new Vector3(playerState.hitSourceX, playerState.hitSourceY, playerState.hitSourceZ));

            if (isLocal && playerState.kills > _lastKillsCount)
            {
                controller.PlayGetKillSound();
            }
            _lastKillsCount = playerState.kills;

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

    public bool ApplyAuthoritativeRoundReset()
    {
        if (!isLocal || controller == null || playerState == null)
        {
            return false;
        }

        Vector3 authoritativePosition = new Vector3(playerState.x, playerState.y, playerState.z);
        controller.ApplyNetworkRoundReset(authoritativePosition, playerState.rotationY);
        transform.SetPositionAndRotation(authoritativePosition, Quaternion.Euler(0f, playerState.rotationY, 0f));
        _jumpQueued = false;
        nextSendTime = Time.time + sendInterval;
        return true;
    }

    public void ApplyImmediateRoundReset(Vector3 worldPosition, float rotationY)
    {
        if (!isLocal || controller == null)
        {
            return;
        }

        controller.ApplyNetworkRoundReset(worldPosition, rotationY);
        transform.SetPositionAndRotation(worldPosition, Quaternion.Euler(0f, rotationY, 0f));
        _jumpQueued = false;
        nextSendTime = Time.time + sendInterval;
    }
}
