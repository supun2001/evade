using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-3)]
public class OfflinePlayerBotBrain : MonoBehaviour
{
    [SerializeField] private float _rescueDistance = 2.5f;
    [SerializeField] private float _evadeDistance = 18f;
    [SerializeField] private float _jumpCheckDistance = 1.3f;
    [SerializeField] private float _sideWallCheckDistance = 0.8f;
    [SerializeField] private LayerMask _movementProbeLayers = ~0;
    [SerializeField] private float _wanderRetargetInterval = 5.5f;
    [SerializeField] private float _carryReviveThreatDistance = 12f;
    [SerializeField] private float _carryReviveRetreatArrivalDistance = 2.5f;
    [SerializeField] private float _rescueEvadeDistance = 10f;
    [SerializeField] private float _rescueTargetThreatDistance = 9f;
    [SerializeField] private float _reviveHoldDuration = 2.5f;
    [SerializeField] private float _playerAvoidanceRadius = 2.25f;
    [SerializeField] private float _playerAvoidanceStrength = 3.5f;
    [SerializeField] private float _evadeFanAngle = 55f;
    [SerializeField] private float _lowObstacleJumpProbeHeight = 0.35f;
    [SerializeField] private float _midObstacleJumpProbeHeight = 0.9f;
    [SerializeField] private float _edgeJumpProbeRadius = 0.32f;
    [SerializeField] private float _stuckJumpDelay = 0.45f;
    [SerializeField] private float _stuckJumpMaxSpeed = 0.45f;
    [SerializeField] private float _stuckJumpInputThreshold = 0.3f;
    [SerializeField] private float _stuckRecoveryTriggerDelay = 0.9f;
    [SerializeField] private float _stuckRecoveryMinTravelDistance = 0.45f;
    [SerializeField] private float _stuckRecoveryDurationMin = 0.55f;
    [SerializeField] private float _stuckRecoveryDurationMax = 1.1f;
    [SerializeField] private float _stuckRecoveryProbeDistance = 1.4f;
    [SerializeField] private float _movementInputSharpness = 6.5f;
    [SerializeField] private float _lookInputSharpness = 9f;
    [SerializeField] private float _destinationArrivalRadius = 1.15f;
    [SerializeField] private float _destinationSlowRadius = 4.5f;

    private PlayerController _controller;
    private PlayerLocomotionInput _locomotionInput;
    private OfflinePlayerIdentity _identity;
    private int _botIndex;
    private float _nextJumpAllowedAt;
    private float _crouchHeldUntil;
    private float _nextWanderRetargetAt;
    private Vector3 _wanderTarget;
    private NavMeshPath _path;
    private string _reviveHoldTargetSessionId;
    private float _reviveHoldStartedAt = -1f;
    private bool _hasInitialWanderTarget;
    private float _movementBlockedSince = -1f;
    private float _stuckProgressSampleStartedAt = -1f;
    private Vector3 _stuckProgressSamplePosition;
    private float _stuckRecoveryUntil = -1f;
    private Vector3 _stuckRecoveryDirection = Vector3.zero;
    private Vector2 _smoothedMovementInput = Vector2.zero;
    private Vector2 _smoothedLookInput = Vector2.zero;
    private float _personalityAggression;
    private float _personalityCaution;
    private float _personalitySociability;
    private float _personalityWanderRadiusScale;
    private float _idlePauseUntil;
    private float _nextSocialRetargetAt;
    private Vector3 _socialTarget;
    private bool _hasSocialTarget;

    public void Initialize(int botIndex)
    {
        _botIndex = botIndex;
        _hasInitialWanderTarget = false;
        float seed = Mathf.Repeat((botIndex + 1) * 0.173f, 1f);
        _personalityAggression = Mathf.Lerp(0.85f, 1.25f, seed);
        _personalityCaution = Mathf.Lerp(0.8f, 1.3f, Mathf.Repeat(seed * 1.73f, 1f));
        _personalitySociability = Mathf.Lerp(0.75f, 1.35f, Mathf.Repeat(seed * 2.31f, 1f));
        _personalityWanderRadiusScale = Mathf.Lerp(0.8f, 1.25f, Mathf.Repeat(seed * 2.87f, 1f));
        _idlePauseUntil = 0f;
        _nextSocialRetargetAt = 0f;
        _socialTarget = Vector3.zero;
        _hasSocialTarget = false;
    }

    public void ResetOpeningRoute()
    {
        _hasInitialWanderTarget = false;
        _wanderTarget = transform.position;
        _nextWanderRetargetAt = 0f;
        _movementBlockedSince = -1f;
        _stuckProgressSampleStartedAt = -1f;
        _stuckRecoveryUntil = -1f;
        _stuckRecoveryDirection = Vector3.zero;
        _smoothedMovementInput = Vector2.zero;
        _smoothedLookInput = Vector2.zero;
        _idlePauseUntil = 0f;
        _nextSocialRetargetAt = 0f;
        _hasSocialTarget = false;
        ResetReviveHold();
        ReleaseCurrentPatrolAssignment();
    }

    private void ReleaseCurrentPatrolAssignment()
    {
        if (_identity != null && OfflineModeManager.TryGetExisting(out OfflineModeManager offlineModeManager))
        {
            offlineModeManager.ReleasePatrolAssignment(_identity.SessionId);
        }

        _nextWanderRetargetAt = 0f;
    }

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _locomotionInput = GetComponent<PlayerLocomotionInput>();
        _identity = GetComponent<OfflinePlayerIdentity>();
        _path = new NavMeshPath();
        _wanderTarget = transform.position;
    }

    private void Update()
    {
        if (OfflineModeManager.Instance == null
            || !OfflineModeManager.Instance.IsOfflineModeActive
            || _controller == null
            || _locomotionInput == null
            || _identity == null)
        {
            return;
        }

        if (_controller.IsBeingCarried())
        {
            ResetReviveHold();
            _movementBlockedSince = -1f;
            ReleaseCurrentPatrolAssignment();
            OfflineModeManager.Instance.ReleaseRescueAssignment(_identity.SessionId);
            ClearStuckRecovery();
            _locomotionInput.ApplySimulatedInput(Vector2.zero, Vector2.zero, false, false, false, false);
            return;
        }

        Vector3 origin = transform.position;
        EnsureInitialWanderTarget(origin);

        if (_controller.IsInjuredOrHitReacting())
        {
            ResetReviveHold();
            _movementBlockedSince = -1f;
            ReleaseCurrentPatrolAssignment();
            OfflineModeManager.Instance.ReleaseRescueAssignment(_identity.SessionId);
            ClearStuckRecovery();
            Vector2 injuredLookInput = Vector2.zero;
            if (OfflineModeManager.Instance.TryGetNearestActivePlayer(
                _identity.SessionId,
                origin,
                out Transform helperTransform,
                out _)
                && helperTransform != null)
            {
                Vector3 toHelper = helperTransform.position - origin;
                toHelper.y = 0f;
                injuredLookInput = BuildLookInput(toHelper);
            }

            _locomotionInput.ApplySimulatedInput(Vector2.zero, injuredLookInput, false, false, false, false);
            return;
        }

        bool hasThreat = OfflineModeManager.Instance.TryGetNearestThreat(origin, out Transform threatTransform, out float threatDistance);
        bool hasDownedTarget = false;
        OfflinePlayerIdentity downedIdentity = null;
        PlayerController downedController = null;
        float downedDistance = float.PositiveInfinity;
        if (!_controller.IsCarrying())
        {
            hasDownedTarget = OfflineModeManager.Instance.TryGetAssignedDownedPlayer(
                _identity.SessionId,
                origin,
                out downedIdentity,
                out downedController,
                out downedDistance);
        }

        Vector3 destination = DetermineDestination(
            origin,
            hasThreat,
            threatTransform,
            threatDistance,
            hasDownedTarget,
            downedIdentity,
            downedController,
            downedDistance);

        Vector3 steeringTarget = ResolveSteeringTarget(origin, destination);
        Vector3 toDestination = steeringTarget - origin;
        toDestination.y = 0f;

        bool isHoldingRevive = !string.IsNullOrWhiteSpace(_reviveHoldTargetSessionId);
        if (!isHoldingRevive)
        {
            toDestination = ApplyPlayerAvoidance(origin, toDestination);
        }

        UpdateStuckRecovery(origin, toDestination);
        if (IsInStuckRecovery())
        {
            toDestination = _stuckRecoveryDirection;
        }

        Vector2 movementInput = BuildMovementInput(toDestination);
        Vector2 lookInput = BuildLookInput(toDestination);
        if (isHoldingRevive
            && hasDownedTarget
            && downedController != null
            && downedDistance <= _rescueDistance
            && string.Equals(_reviveHoldTargetSessionId, downedIdentity != null ? downedIdentity.SessionId : null, System.StringComparison.Ordinal))
        {
            movementInput = Vector2.zero;
            lookInput = BuildLookInput(downedController.transform.position - origin);
        }
        else if (isHoldingRevive)
        {
            movementInput = Vector2.zero;
        }

        ApplyIdlePauseVariation(origin, ref movementInput, ref lookInput, isHoldingRevive, hasThreat, hasDownedTarget);

        movementInput = SmoothMovementInput(movementInput);
        lookInput = SmoothLookInput(lookInput);

        bool crouchHeld = Time.time < _crouchHeldUntil;
        bool crouchPressedThisFrame = false;
        bool jumpPressed = false;
        bool jumpHeld = false;

        if (!_controller.IsCarrying()
            && !_controller.IsInjuredOrHitReacting()
            && hasThreat
            && threatDistance <= _evadeDistance * 0.75f
            && _controller.GetHorizontalSpeed() >= _controller.GetCrouchMoveSpeed()
            && !crouchHeld)
        {
            crouchHeld = true;
            crouchPressedThisFrame = true;
            _crouchHeldUntil = Time.time + Random.Range(0.35f, 0.9f);
        }

        if (ShouldJump(movementInput, hasThreat, threatDistance))
        {
            jumpPressed = true;
            jumpHeld = true;
            _nextJumpAllowedAt = Time.time + Random.Range(0.55f, 1.1f);
        }

        _locomotionInput.ApplySimulatedInput(movementInput, lookInput, jumpHeld, jumpPressed, crouchHeld, crouchPressedThisFrame);
    }

    private Vector3 DetermineDestination(
        Vector3 origin,
        bool hasThreat,
        Transform threatTransform,
        float threatDistance,
        bool hasDownedTarget,
        OfflinePlayerIdentity downedIdentity,
        PlayerController downedController,
        float downedDistance)
    {
        if (_controller.IsCarrying())
        {
            ReleaseCurrentPatrolAssignment();
            return DetermineCarryingDestination(origin, hasThreat, threatTransform, threatDistance);
        }

        if (hasDownedTarget && downedIdentity != null && downedController != null)
        {
            ReleaseCurrentPatrolAssignment();
            Vector3 downedPosition = downedController.transform.position;
            bool targetHasThreat = OfflineModeManager.Instance.TryGetNearestThreat(
                downedPosition,
                out Transform targetThreatTransform,
                out float targetThreatDistance);
            bool rescuerInDanger = hasThreat && threatDistance <= _rescueEvadeDistance;
            bool targetInDanger = targetHasThreat && targetThreatDistance <= _rescueTargetThreatDistance;
            bool shouldCarry = rescuerInDanger || targetInDanger;

            if (downedDistance <= _rescueDistance)
            {
                if (shouldCarry)
                {
                    ResetReviveHold();
                    if (OfflineModeManager.Instance.TryToggleCarryPlayer(_identity.SessionId, downedIdentity.SessionId))
                    {
                        return OfflineModeManager.Instance.GetRetreatPosition(origin);
                    }
                }
                else
                {
                    UpdateReviveHold(downedIdentity.SessionId);
                    return origin;
                }
            }

            ResetReviveHold();
            if (rescuerInDanger && threatTransform != null)
            {
                return BuildEvadeDestination(origin, threatTransform);
            }

            if (targetInDanger && targetThreatTransform != null)
            {
                return BuildRescueStagingDestination(origin, downedPosition, targetThreatTransform.position);
            }

            return downedPosition;
        }

        ResetReviveHold();
        OfflineModeManager.Instance.ReleaseRescueAssignment(_identity.SessionId);
        if (hasThreat && threatTransform != null && threatDistance <= _evadeDistance * _personalityCaution)
        {
            ReleaseCurrentPatrolAssignment();
            return BuildEvadeDestination(origin, threatTransform);
        }

        if (TryGetSocialDestination(origin, out Vector3 socialDestination))
        {
            ReleaseCurrentPatrolAssignment();
            return socialDestination;
        }

        if (Time.time >= _nextWanderRetargetAt || Vector3.Distance(origin, _wanderTarget) <= 1.6f)
        {
            _wanderTarget = OfflineModeManager.Instance.GetPatrolPosition(_identity.SessionId, _botIndex + 1, origin);
            _nextWanderRetargetAt = Time.time + Random.Range(_wanderRetargetInterval * 0.75f, _wanderRetargetInterval * 1.35f);
        }

        return origin + (_wanderTarget - origin) * _personalityWanderRadiusScale;
    }

    private Vector3 DetermineCarryingDestination(Vector3 origin, bool hasThreat, Transform threatTransform, float threatDistance)
    {
        string carriedSessionId = _controller.GetCarriedPlayerSessionId();
        if (string.IsNullOrWhiteSpace(carriedSessionId))
        {
            ResetReviveHold();
            return origin;
        }

        Vector3 retreatPosition = OfflineModeManager.Instance.GetRetreatPosition(origin);
        bool reachedRetreat = GetPlanarDistance(origin, retreatPosition) <= _carryReviveRetreatArrivalDistance;
        bool safeEnoughToRevive =
            (!hasThreat || threatDistance >= _carryReviveThreatDistance * _personalityCaution || reachedRetreat)
            && (!hasThreat || threatDistance > _rescueEvadeDistance * _personalityCaution);
        if (!safeEnoughToRevive)
        {
            ResetReviveHold();
            if (hasThreat && threatTransform != null && threatDistance <= _rescueEvadeDistance)
            {
                return BuildEvadeDestination(origin, threatTransform);
            }

            return retreatPosition;
        }

        OfflineModeManager.Instance.ClearCarryStateForPlayer(_identity.SessionId);
        UpdateReviveHold(carriedSessionId);
        return origin;
    }

    private Vector3 BuildEvadeDestination(Vector3 origin, Transform threatTransform)
    {
        Vector3 away = origin - threatTransform.position;
        away.y = 0f;
        if (away.sqrMagnitude <= 0.001f)
        {
            away = -transform.forward;
        }

        Vector3 evadeDirection = ApplyBotFan(away.normalized, _evadeFanAngle);
        return origin + evadeDirection * Mathf.Lerp(6.5f, 10.5f, Mathf.Clamp01(_personalityCaution - 0.75f));
    }

    private Vector3 BuildRescueStagingDestination(Vector3 origin, Vector3 downedPosition, Vector3 threatPosition)
    {
        Vector3 awayFromThreat = downedPosition - threatPosition;
        awayFromThreat.y = 0f;
        if (awayFromThreat.sqrMagnitude <= 0.001f)
        {
            awayFromThreat = origin - threatPosition;
            awayFromThreat.y = 0f;
        }

        if (awayFromThreat.sqrMagnitude <= 0.001f)
        {
            awayFromThreat = -transform.forward;
        }

        Vector3 stagingDirection = ApplyBotFan(awayFromThreat.normalized, _evadeFanAngle * 0.55f);
        return downedPosition + stagingDirection * Mathf.Max(_rescueDistance + 1f, 4f);
    }

    private void EnsureInitialWanderTarget(Vector3 origin)
    {
        if (_hasInitialWanderTarget || OfflineModeManager.Instance == null)
        {
            return;
        }

        _wanderTarget = OfflineModeManager.Instance.GetOpeningPatrolPosition(_identity.SessionId, _botIndex + 1, origin);
        _nextWanderRetargetAt = Time.time + Random.Range(_wanderRetargetInterval * 1.25f, _wanderRetargetInterval * 2.1f);
        _hasInitialWanderTarget = true;
    }

    private Vector3 ApplyBotFan(Vector3 direction, float maxAngle)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f || maxAngle <= 0f)
        {
            return direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        }

        float normalizedSlot = Mathf.Repeat((_botIndex + 1) * 0.6180339f, 1f);
        float fanAngle = Mathf.Lerp(-maxAngle, maxAngle, normalizedSlot);
        Vector3 fannedDirection = Quaternion.Euler(0f, fanAngle, 0f) * direction.normalized;
        return fannedDirection.normalized;
    }

    private bool TryGetSocialDestination(Vector3 origin, out Vector3 socialDestination)
    {
        socialDestination = Vector3.zero;
        if (OfflineModeManager.Instance == null || _personalitySociability <= 0.85f)
        {
            _hasSocialTarget = false;
            return false;
        }

        if (Time.time >= _nextSocialRetargetAt || !_hasSocialTarget || GetPlanarDistance(origin, _socialTarget) <= 2.2f)
        {
            _hasSocialTarget = false;
            if (OfflineModeManager.Instance.TryGetNearestActivePlayer(_identity.SessionId, origin, out Transform teammateTransform, out float teammateDistance)
                && teammateTransform != null
                && teammateDistance >= Mathf.Lerp(6f, 13f, Mathf.Clamp01(_personalitySociability - 0.85f)))
            {
                Vector3 teammatePosition = teammateTransform.position;
                Vector3 offsetDirection = ApplyBotFan((origin - teammatePosition).sqrMagnitude > 0.001f ? (origin - teammatePosition).normalized : transform.right, 85f);
                float offsetDistance = Random.Range(2.5f, 6.5f);
                _socialTarget = teammatePosition + offsetDirection * offsetDistance;
                _hasSocialTarget = true;
            }

            _nextSocialRetargetAt = Time.time + Random.Range(2.4f, 5.8f);
        }

        if (!_hasSocialTarget)
        {
            return false;
        }

        socialDestination = _socialTarget;
        return true;
    }

    private void UpdateReviveHold(string targetSessionId)
    {
        if (string.IsNullOrWhiteSpace(targetSessionId))
        {
            ResetReviveHold();
            return;
        }

        if (!string.Equals(_reviveHoldTargetSessionId, targetSessionId, System.StringComparison.Ordinal))
        {
            _reviveHoldTargetSessionId = targetSessionId;
            _reviveHoldStartedAt = Time.time;
        }

        if (_reviveHoldStartedAt < 0f)
        {
            _reviveHoldStartedAt = Time.time;
        }

        if (Time.time - _reviveHoldStartedAt < _reviveHoldDuration)
        {
            return;
        }

        if (OfflineModeManager.Instance.TryRevivePlayer(_identity.SessionId, targetSessionId))
        {
            ResetReviveHold();
            OfflineModeManager.Instance.ReleaseRescueAssignment(_identity.SessionId);
        }
    }

    private void ResetReviveHold()
    {
        _reviveHoldTargetSessionId = null;
        _reviveHoldStartedAt = -1f;
    }

    private Vector3 ResolveSteeringTarget(Vector3 origin, Vector3 destination)
    {
        if (_path == null)
        {
            _path = new NavMeshPath();
        }

        if (!NavMesh.SamplePosition(origin, out NavMeshHit originHit, 4f, NavMesh.AllAreas)
            || !NavMesh.SamplePosition(destination, out NavMeshHit destinationHit, 8f, NavMesh.AllAreas)
            || !NavMesh.CalculatePath(originHit.position, destinationHit.position, NavMesh.AllAreas, _path)
            || _path.corners == null
            || _path.corners.Length < 2)
        {
            return destination;
        }

        int cornerIndex = 1;
        while (cornerIndex < _path.corners.Length - 1 && GetPlanarDistance(origin, _path.corners[cornerIndex]) < 1.2f)
        {
            cornerIndex++;
        }

        return _path.corners[Mathf.Clamp(cornerIndex, 1, _path.corners.Length - 1)];
    }

    private Vector3 ApplyPlayerAvoidance(Vector3 origin, Vector3 desiredOffset)
    {
        Vector3 separation = OfflineModeManager.Instance.GetActivePlayerSeparationVector(
            _identity.SessionId,
            origin,
            _playerAvoidanceRadius);
        if (separation.sqrMagnitude <= 0.0001f)
        {
            return desiredOffset;
        }

        desiredOffset.y = 0f;
        Vector3 adjustedOffset = desiredOffset + separation * _playerAvoidanceStrength;
        if (adjustedOffset.sqrMagnitude <= 0.04f)
        {
            return separation.normalized * Mathf.Max(1f, _playerAvoidanceRadius);
        }

        return adjustedOffset;
    }

    private Vector2 BuildMovementInput(Vector3 toDestination)
    {
        float planarDistance = toDestination.magnitude;
        if (planarDistance <= _destinationArrivalRadius)
        {
            return Vector2.zero;
        }

        Vector3 localDirection = transform.InverseTransformDirection(toDestination.normalized);
        float speedScale = planarDistance >= _destinationSlowRadius
            ? 1f
            : Mathf.Clamp01(planarDistance / Mathf.Max(_destinationArrivalRadius + 0.01f, _destinationSlowRadius));
        speedScale = Mathf.Lerp(0.35f, 1f, speedScale);
        return new Vector2(
            Mathf.Clamp(localDirection.x, -1f, 1f) * speedScale,
            Mathf.Clamp(localDirection.z, -1f, 1f) * speedScale);
    }

    private Vector2 BuildLookInput(Vector3 toDestination)
    {
        if (toDestination.sqrMagnitude <= 0.04f)
        {
            return Vector2.zero;
        }

        float targetYaw = Quaternion.LookRotation(toDestination.normalized, Vector3.up).eulerAngles.y;
        float yawDelta = Mathf.DeltaAngle(transform.eulerAngles.y, targetYaw);
        float lookX = Mathf.Clamp(yawDelta / Mathf.Max(0.01f, _controller.lookSenseH), -35f, 35f);
        return new Vector2(lookX, 0f);
    }

    private bool ShouldJump(Vector2 movementInput, bool hasThreat, float threatDistance)
    {
        if (_controller.IsCarrying()
            || _controller.IsInjuredOrHitReacting()
            || _controller.IsBeingCarried()
            || !_controller.IsGrounded()
            || movementInput.sqrMagnitude < _stuckJumpInputThreshold * _stuckJumpInputThreshold
            || Time.time < _nextJumpAllowedAt)
        {
            _movementBlockedSince = -1f;
            return false;
        }

        Vector3 movementDirection = GetWorldMovementDirection(movementInput);
        if (movementDirection.sqrMagnitude <= 0.0001f)
        {
            _movementBlockedSince = -1f;
            return false;
        }

        bool obstacleAhead = HasJumpableObstacleAhead(movementDirection);
        Vector3 probeOrigin = transform.position + Vector3.up * _midObstacleJumpProbeHeight;

        bool sideWall =
            Physics.Raycast(probeOrigin, transform.right, _sideWallCheckDistance, _movementProbeLayers, QueryTriggerInteraction.Ignore)
            || Physics.Raycast(probeOrigin, -transform.right, _sideWallCheckDistance, _movementProbeLayers, QueryTriggerInteraction.Ignore);

        if (obstacleAhead)
        {
            _movementBlockedSince = -1f;
            return true;
        }

        if (ShouldJumpBecauseBlocked())
        {
            _movementBlockedSince = -1f;
            return true;
        }

        bool shouldJumpForThreatSideWall = hasThreat && threatDistance <= _evadeDistance * 0.7f && sideWall;
        if (shouldJumpForThreatSideWall)
        {
            _movementBlockedSince = -1f;
        }

        return shouldJumpForThreatSideWall;
    }

    private Vector3 GetWorldMovementDirection(Vector2 movementInput)
    {
        Vector3 localDirection = new Vector3(movementInput.x, 0f, movementInput.y);
        if (localDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 worldDirection = transform.TransformDirection(localDirection.normalized);
        worldDirection.y = 0f;
        return worldDirection.sqrMagnitude > 0.0001f ? worldDirection.normalized : Vector3.zero;
    }

    private bool HasJumpableObstacleAhead(Vector3 movementDirection)
    {
        float probeDistance = Mathf.Max(_jumpCheckDistance, 0.4f);
        float probeRadius = Mathf.Max(0.05f, _edgeJumpProbeRadius);
        Vector3 lowProbeOrigin = transform.position + Vector3.up * Mathf.Max(0.05f, _lowObstacleJumpProbeHeight);
        Vector3 midProbeOrigin = transform.position + Vector3.up * Mathf.Max(_lowObstacleJumpProbeHeight, _midObstacleJumpProbeHeight);

        return HasValidJumpProbeHit(lowProbeOrigin, probeRadius, movementDirection, probeDistance)
            || HasValidJumpProbeHit(midProbeOrigin, probeRadius * 0.75f, movementDirection, probeDistance * 0.85f);
    }

    private bool HasValidJumpProbeHit(Vector3 origin, float radius, Vector3 direction, float distance)
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            radius,
            direction,
            distance,
            _movementProbeLayers,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            if (IsValidJumpObstacle(hits[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsValidJumpObstacle(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return false;
        }

        Transform hitTransform = hit.collider.transform;
        if (hitTransform == null || hitTransform.IsChildOf(transform))
        {
            return false;
        }

        if (hit.collider.GetComponentInParent<PlayerController>() != null
            || hit.collider.GetComponentInParent<NextbotFollowPlayer>() != null)
        {
            return false;
        }

        return hit.normal.y < 0.65f;
    }

    private bool ShouldJumpBecauseBlocked()
    {
        if (_controller.GetHorizontalSpeed() > _stuckJumpMaxSpeed)
        {
            _movementBlockedSince = -1f;
            return false;
        }

        if (_movementBlockedSince < 0f)
        {
            _movementBlockedSince = Time.time;
            return false;
        }

        return Time.time - _movementBlockedSince >= _stuckJumpDelay;
    }

    private void UpdateStuckRecovery(Vector3 origin, Vector3 toDestination)
    {
        if (toDestination.sqrMagnitude <= _destinationArrivalRadius * _destinationArrivalRadius
            || _controller.IsCarrying()
            || _controller.IsInjuredOrHitReacting()
            || _controller.IsBeingCarried())
        {
            ClearStuckRecovery();
            return;
        }

        if (IsInStuckRecovery())
        {
            return;
        }

        float inputMagnitude = _smoothedMovementInput.magnitude;
        if (inputMagnitude < 0.35f)
        {
            _stuckProgressSampleStartedAt = -1f;
            return;
        }

        if (_stuckProgressSampleStartedAt < 0f)
        {
            _stuckProgressSampleStartedAt = Time.time;
            _stuckProgressSamplePosition = origin;
            return;
        }

        if (Time.time - _stuckProgressSampleStartedAt < _stuckRecoveryTriggerDelay)
        {
            return;
        }

        float traveledDistance = GetPlanarDistance(origin, _stuckProgressSamplePosition);
        bool movingTooSlow = _controller.GetHorizontalSpeed() <= _stuckJumpMaxSpeed * 1.5f;
        if (traveledDistance >= _stuckRecoveryMinTravelDistance || !movingTooSlow)
        {
            _stuckProgressSampleStartedAt = Time.time;
            _stuckProgressSamplePosition = origin;
            return;
        }

        StartStuckRecovery(origin, toDestination);
    }

    private void ApplyIdlePauseVariation(
        Vector3 origin,
        ref Vector2 movementInput,
        ref Vector2 lookInput,
        bool isHoldingRevive,
        bool hasThreat,
        bool hasDownedTarget)
    {
        if (isHoldingRevive || hasThreat || hasDownedTarget || IsInStuckRecovery())
        {
            _idlePauseUntil = 0f;
            return;
        }

        if (movementInput.magnitude <= 0.22f && Time.time >= _idlePauseUntil && Random.value < 0.0055f * _personalitySociability)
        {
            _idlePauseUntil = Time.time + Random.Range(0.2f, 0.65f);
        }

        if (Time.time < _idlePauseUntil)
        {
            movementInput = Vector2.zero;
            Vector3 lookOffset = ApplyBotFan(transform.forward, 120f) * Random.Range(2f, 4f);
            lookInput = BuildLookInput(lookOffset);
        }
    }

    private void StartStuckRecovery(Vector3 origin, Vector3 toDestination)
    {
        _stuckRecoveryDirection = ChooseRecoveryDirection(origin, toDestination);
        _stuckRecoveryUntil = Time.time + Random.Range(_stuckRecoveryDurationMin, _stuckRecoveryDurationMax);
        _stuckProgressSampleStartedAt = -1f;
        _movementBlockedSince = -1f;
        _nextJumpAllowedAt = Mathf.Max(_nextJumpAllowedAt, Time.time + 0.2f);
    }

    private Vector3 ChooseRecoveryDirection(Vector3 origin, Vector3 toDestination)
    {
        Vector3 preferredDirection = toDestination.sqrMagnitude > 0.001f ? toDestination.normalized : transform.forward;
        preferredDirection.y = 0f;
        if (preferredDirection.sqrMagnitude <= 0.001f)
        {
            preferredDirection = transform.forward;
        }

        Vector3 left = Quaternion.Euler(0f, -75f, 0f) * preferredDirection;
        Vector3 right = Quaternion.Euler(0f, 75f, 0f) * preferredDirection;
        Vector3 back = -preferredDirection;

        Vector3[] candidates =
        {
            left.normalized,
            right.normalized,
            back.normalized,
            transform.right.normalized,
            (-transform.right).normalized
        };

        float bestScore = float.NegativeInfinity;
        Vector3 bestDirection = back.normalized;
        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 candidate = candidates[i];
            float clearance = MeasureDirectionClearance(origin, candidate);
            float forwardBias = Vector3.Dot(candidate, preferredDirection) * 0.35f;
            float score = clearance + forwardBias + Random.Range(-0.08f, 0.08f);
            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = candidate;
            }
        }

        return bestDirection;
    }

    private float MeasureDirectionClearance(Vector3 origin, Vector3 direction)
    {
        Vector3 probeOrigin = origin + Vector3.up * Mathf.Max(_lowObstacleJumpProbeHeight, 0.25f);
        if (Physics.SphereCast(
            probeOrigin,
            Mathf.Max(0.08f, _edgeJumpProbeRadius * 0.7f),
            direction,
            out RaycastHit hit,
            _stuckRecoveryProbeDistance,
            _movementProbeLayers,
            QueryTriggerInteraction.Ignore)
            && IsMovementBlockingHit(hit))
        {
            return Mathf.Max(0f, hit.distance);
        }

        return _stuckRecoveryProbeDistance;
    }

    private bool IsMovementBlockingHit(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return false;
        }

        Transform hitTransform = hit.collider.transform;
        if (hitTransform == null || hitTransform.IsChildOf(transform))
        {
            return false;
        }

        if (hit.collider.GetComponentInParent<PlayerController>() != null
            || hit.collider.GetComponentInParent<NextbotFollowPlayer>() != null)
        {
            return false;
        }

        return true;
    }

    private bool IsInStuckRecovery()
    {
        return _stuckRecoveryUntil > Time.time && _stuckRecoveryDirection.sqrMagnitude > 0.001f;
    }

    private void ClearStuckRecovery()
    {
        _stuckRecoveryUntil = -1f;
        _stuckRecoveryDirection = Vector3.zero;
        _stuckProgressSampleStartedAt = -1f;
    }

    private Vector2 SmoothMovementInput(Vector2 targetInput)
    {
        float blend = 1f - Mathf.Exp(-_movementInputSharpness * Time.deltaTime);
        _smoothedMovementInput = Vector2.Lerp(_smoothedMovementInput, targetInput, blend);
        return _smoothedMovementInput;
    }

    private Vector2 SmoothLookInput(Vector2 targetInput)
    {
        float blend = 1f - Mathf.Exp(-_lookInputSharpness * Time.deltaTime);
        _smoothedLookInput = Vector2.Lerp(_smoothedLookInput, targetInput, blend);
        return _smoothedLookInput;
    }

    private static float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
