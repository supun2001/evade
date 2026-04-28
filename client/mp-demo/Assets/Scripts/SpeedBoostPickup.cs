using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpeedBoostPickup : MonoBehaviour
{
    [SerializeField, Min(1f)] private float _speedMultiplier = 1.5f;
    [SerializeField, Min(0.1f)] private float _durationSeconds = 5f;
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField, Range(0f, 1f)] private float _pickupSoundVolume = 1f;
    [SerializeField, Min(0.05f)] private float _pickupSoundMaxDuration = 0.55f;
    [SerializeField, Min(0.1f)] private float _minimumTriggerRadius = 0.8f;
    [SerializeField] private bool _destroyOnPickup = true;

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
        if (!TryGetEligiblePlayerController(other, out PlayerController playerController, out bool playLocalFeedback))
        {
            return;
        }

        playerController.ApplyTemporarySpeedBoost(_speedMultiplier, _durationSeconds);
        if (playLocalFeedback)
        {
            playerController.PlayPickupFade();
            playerController.PlayLocalAbilitySound(_pickupSound, _pickupSoundVolume, _pickupSoundMaxDuration);
        }

        if (_destroyOnPickup)
        {
            Destroy(GetPickupRoot());
        }
    }

    private static bool TryGetEligiblePlayerController(Collider other, out PlayerController playerController, out bool playLocalFeedback)
    {
        playerController = null;
        playLocalFeedback = false;

        if (other == null)
        {
            return false;
        }

        playerController = other.GetComponentInParent<PlayerController>();
        if (playerController == null
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
            if (offlineIdentity == null)
            {
                playerController = null;
                return false;
            }

            playLocalFeedback = offlineIdentity.IsLocalPlayer;
            return true;
        }

        NetworkPlayer networkPlayer = other.GetComponentInParent<NetworkPlayer>();
        if (networkPlayer == null || !networkPlayer.IsLocalPlayer)
        {
            playerController = null;
            return false;
        }

        playLocalFeedback = true;
        return true;
    }

    private void EnsureTriggerCollider()
    {
        Collider[] colliders = GetComponents<Collider>();
        SphereCollider selectedSphereCollider = null;

        foreach (Collider collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            if (collider is MeshCollider meshCollider)
            {
                meshCollider.enabled = false;
                continue;
            }

            if (collider is SphereCollider sphereCollider)
            {
                collider.enabled = true;
                collider.isTrigger = true;
                selectedSphereCollider = sphereCollider;
            }
            else
            {
                collider.enabled = false;
            }
        }

        if (selectedSphereCollider == null)
        {
            selectedSphereCollider = GetComponent<SphereCollider>();
        }

        if (selectedSphereCollider == null)
        {
            selectedSphereCollider = gameObject.AddComponent<SphereCollider>();
        }

        ConfigureFallbackTrigger(selectedSphereCollider);
    }

    private void ConfigureFallbackTrigger(SphereCollider fallbackTrigger)
    {
        fallbackTrigger.isTrigger = true;

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            fallbackTrigger.center = meshBounds.center;
            fallbackTrigger.radius = Mathf.Max(_minimumTriggerRadius, Mathf.Max(meshBounds.extents.x, meshBounds.extents.y, meshBounds.extents.z));
            return;
        }

        Renderer rendererComponent = GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            Bounds worldBounds = rendererComponent.bounds;
            Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
            Vector3 lossyScale = transform.lossyScale;
            float safeScaleX = Mathf.Approximately(lossyScale.x, 0f) ? 1f : lossyScale.x;
            float safeScaleY = Mathf.Approximately(lossyScale.y, 0f) ? 1f : lossyScale.y;
            float safeScaleZ = Mathf.Approximately(lossyScale.z, 0f) ? 1f : lossyScale.z;
            Vector3 localExtents = new Vector3(
                worldBounds.extents.x / Mathf.Abs(safeScaleX),
                worldBounds.extents.y / Mathf.Abs(safeScaleY),
                worldBounds.extents.z / Mathf.Abs(safeScaleZ));

            fallbackTrigger.center = localCenter;
            fallbackTrigger.radius = Mathf.Max(_minimumTriggerRadius, Mathf.Max(localExtents.x, localExtents.y, localExtents.z));
            return;
        }

        fallbackTrigger.center = Vector3.zero;
        fallbackTrigger.radius = _minimumTriggerRadius;
    }

    private GameObject GetPickupRoot()
    {
        Transform rootTransform = transform.root;
        return rootTransform != null ? rootTransform.gameObject : gameObject;
    }
}
