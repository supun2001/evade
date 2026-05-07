using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(10000)]
public class OfflineNextbotDirectDriver : MonoBehaviour
{
    private const float HitDistance = 1.05f;
    private const float StoppingDistance = 0.85f;
    private const float GroundProbeHeight = 6f;
    private const float GroundProbeDistance = 14f;

    [SerializeField] private float _moveSpeed = 9.5f;
    [SerializeField] private float _acceleration = 30f;

    private NextbotFollowPlayer _nextbot;
    private CharacterController _characterController;
    private NavMeshAgent _navMeshAgent;
    private Vector3 _velocity;
    private int _driverIndex;
    private bool _active;

    public void Configure(NextbotFollowPlayer nextbot, int driverIndex)
    {
        _nextbot = nextbot != null ? nextbot : GetComponent<NextbotFollowPlayer>();
        _driverIndex = Mathf.Max(0, driverIndex);
        CacheBlockingComponents();
    }

    public void SetDriverActive(bool active)
    {
        _active = active;
        enabled = active;
        if (!active)
        {
            _velocity = Vector3.zero;
        }
    }

    private void Awake()
    {
        _nextbot = GetComponent<NextbotFollowPlayer>();
        CacheBlockingComponents();
    }

    private void CacheBlockingComponents()
    {
        if (_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }

        if (_navMeshAgent == null)
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
        }
    }

    private void LateUpdate()
    {
        if (!_active
            || _nextbot == null
            || !OfflineModeManager.TryGetExisting(out OfflineModeManager offlineModeManager)
            || !offlineModeManager.IsOfflineModeActive
            || !_nextbot.IsCombatActive)
        {
            _velocity = Vector3.zero;
            return;
        }

        DisableBlockingComponents();

        Vector3 destination = ResolveDestination(offlineModeManager, out PlayerController targetController);
        MoveDirectly(destination);

        if (targetController != null
            && offlineModeManager.CanOfflineNextbotsDamagePlayers
            && GetPlanarDistance(transform.position, targetController.transform.position) <= HitDistance)
        {
            targetController.TriggerNextbotHit(transform.position);
        }
    }

    private void DisableBlockingComponents()
    {
        if (_characterController != null && _characterController.enabled)
        {
            _characterController.enabled = false;
        }

        if (_navMeshAgent != null && _navMeshAgent.enabled)
        {
            if (_navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.ResetPath();
            }

            _navMeshAgent.enabled = false;
        }
    }

    private Vector3 ResolveDestination(OfflineModeManager offlineModeManager, out PlayerController targetController)
    {
        targetController = null;
        if (offlineModeManager.TryGetAssignedActivePlayer(
                _nextbot.NetworkNextbotId,
                _driverIndex,
                transform.position,
                out Transform targetTransform,
                out _)
            && targetTransform != null)
        {
            targetController = targetTransform.GetComponent<PlayerController>();
            if (targetController == null)
            {
                targetController = targetTransform.GetComponentInParent<PlayerController>();
            }

            if (targetController != null
                && targetController.enabled
                && !targetController.IsInjuredOrHitReacting()
                && !targetController.IsEliminatedStateActive)
            {
                return targetController.transform.position;
            }
        }

        return offlineModeManager.GetPatrolPosition(_nextbot.NetworkNextbotId, _driverIndex, transform.position);
    }

    private void MoveDirectly(Vector3 destination)
    {
        Vector3 toDestination = destination - transform.position;
        toDestination.y = 0f;
        float distance = toDestination.magnitude;
        Vector3 desiredVelocity = distance > StoppingDistance
            ? toDestination.normalized * _moveSpeed
            : Vector3.zero;
        _velocity = Vector3.MoveTowards(_velocity, desiredVelocity, _acceleration * Time.deltaTime);

        Vector3 nextPosition = transform.position + _velocity * Time.deltaTime;
        nextPosition = ResolveGroundedPosition(nextPosition);
        transform.position = nextPosition;
    }

    private Vector3 ResolveGroundedPosition(Vector3 position)
    {
        Vector3 rayOrigin = position + Vector3.up * GroundProbeHeight;
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            GroundProbeHeight + GroundProbeDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return position;
        }

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null
                || hitCollider.transform == transform
                || hitCollider.transform.IsChildOf(transform)
                || hitCollider.GetComponentInParent<NextbotFollowPlayer>() != null
                || hitCollider.GetComponentInParent<PlayerController>() != null)
            {
                continue;
            }

            position.y = hits[i].point.y + 0.02f;
            return position;
        }

        return position;
    }

    private static float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
