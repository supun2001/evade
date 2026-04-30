using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DangerZoneElimination : MonoBehaviour
{
    [SerializeField] private bool _requireAirborneEntry = true;
    [SerializeField] private BoxCollider _triggerCollider;

    private void Awake()
    {
        EnsureSupportedTriggerCollider();
    }

    private void Reset()
    {
        EnsureSupportedTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureSupportedTriggerCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryGetEligibleLocalPlayer(other, out PlayerController playerController, out string sessionId, out bool isOffline))
        {
            return;
        }

        if (_requireAirborneEntry && playerController.IsGrounded())
        {
            return;
        }

        if (isOffline)
        {
            if (!OfflineModeManager.TryGetExisting(out OfflineModeManager offlineModeManager)
                || !offlineModeManager.TryEliminatePlayerInstantly(sessionId))
            {
                return;
            }
        }
        else
        {
            NetworkManager.Instance?.SendHazardElimination();
        }

        playerController.ApplyNetworkEliminated();
        if (!playerController.IsSpectating())
        {
            playerController.EnterSpectateMode();
        }
    }

    private static bool TryGetEligibleLocalPlayer(Collider other, out PlayerController playerController, out string sessionId, out bool isOffline)
    {
        playerController = null;
        sessionId = string.Empty;
        isOffline = false;

        if (other == null)
        {
            return false;
        }

        playerController = other.GetComponentInParent<PlayerController>();
        if (playerController == null
            || playerController.IsSpectating()
            || playerController.IsInjuredOrHitReacting()
            || playerController.IsBeingCarried())
        {
            playerController = null;
            return false;
        }

        if (OfflineModeManager.TryGetExisting(out OfflineModeManager offlineModeManager)
            && offlineModeManager.IsOfflineModeActive)
        {
            OfflinePlayerIdentity offlineIdentity = other.GetComponentInParent<OfflinePlayerIdentity>();
            if (offlineIdentity == null || !offlineIdentity.IsLocalPlayer)
            {
                playerController = null;
                return false;
            }

            sessionId = offlineIdentity.SessionId;
            isOffline = true;
            return !string.IsNullOrWhiteSpace(sessionId);
        }

        NetworkPlayer networkPlayer = other.GetComponentInParent<NetworkPlayer>();
        if (networkPlayer == null || !networkPlayer.IsLocalPlayer || !networkPlayer.TryGetSessionId(out sessionId))
        {
            playerController = null;
            sessionId = string.Empty;
            return false;
        }

        return true;
    }

    private void EnsureSupportedTriggerCollider()
    {
        if (_triggerCollider == null)
        {
            _triggerCollider = GetComponent<BoxCollider>();
        }

        if (_triggerCollider == null)
        {
            _triggerCollider = gameObject.AddComponent<BoxCollider>();
        }

        _triggerCollider.isTrigger = true;

        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.enabled = false;
        }

        if (_triggerCollider.size == Vector3.zero || _triggerCollider.size == Vector3.one)
        {
            Renderer targetRenderer = GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                Vector3 lossyScale = transform.lossyScale;
                _triggerCollider.center = transform.InverseTransformPoint(targetRenderer.bounds.center);
                _triggerCollider.size = new Vector3(
                    SafeDivide(targetRenderer.bounds.size.x, lossyScale.x),
                    Mathf.Max(0.5f, SafeDivide(targetRenderer.bounds.size.y, lossyScale.y)),
                    SafeDivide(targetRenderer.bounds.size.z, lossyScale.z));
            }
        }
    }

    private static float SafeDivide(float value, float divisor)
    {
        if (Mathf.Approximately(divisor, 0f))
        {
            return value;
        }

        return value / divisor;
    }
}
