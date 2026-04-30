using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DangerZoneElimination : MonoBehaviour
{
    [SerializeField] private bool _requireAirborneEntry = true;

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
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

    private void EnsureTriggerCollider()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
}
