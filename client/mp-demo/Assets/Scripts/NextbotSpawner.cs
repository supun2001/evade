using UnityEngine;

public class NextbotSpawner : MonoBehaviour
{
    private const bool NEXTBOTS_ENABLED = false;
    [SerializeField] private string spawnPointName = "SpawnPoint";
    [SerializeField] private Vector3 fallbackSpawnPosition = new Vector3(6.45f, 0f, -2.38f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<NextbotSpawner>() != null)
        {
            return;
        }

        GameObject spawnerObject = new GameObject("NextbotSpawner");
        spawnerObject.AddComponent<NextbotSpawner>();
    }

    private void Start()
    {
        EnsureExistingSceneNextbotsAreActive();
        RemoveExistingNextbots();

        if (!NEXTBOTS_ENABLED)
        {
            return;
        }

        SpawnNextbots();
    }

    private void SpawnNextbots()
    {
        NextbotAgent[] existingNextbots = FindObjectsByType<NextbotAgent>(FindObjectsSortMode.None);
        if (existingNextbots.Length > 0)
        {
            return;
        }

        Vector3 spawnOrigin = ResolveSpawnOrigin();
        GameObject nextbotObject = new GameObject("Nextbot_1");
        nextbotObject.transform.position = spawnOrigin;
        nextbotObject.AddComponent<NextbotAgent>();
    }

    private void RemoveExistingNextbots()
    {
        NextbotAgent[] existingNextbots = FindObjectsByType<NextbotAgent>(FindObjectsSortMode.None);
        for (int i = 0; i < existingNextbots.Length; i++)
        {
            if (existingNextbots[i] != null)
            {
                Destroy(existingNextbots[i].gameObject);
            }
        }
    }

    private void EnsureExistingSceneNextbotsAreActive()
    {
        NextbotFollowPlayer[] sceneNextbots = Resources.FindObjectsOfTypeAll<NextbotFollowPlayer>();
        for (int i = 0; i < sceneNextbots.Length; i++)
        {
            NextbotFollowPlayer nextbot = sceneNextbots[i];
            if (nextbot == null)
            {
                continue;
            }

            GameObject nextbotObject = nextbot.gameObject;
            if (!nextbotObject.scene.IsValid())
            {
                continue;
            }

            if (!nextbotObject.activeSelf)
            {
                nextbotObject.SetActive(true);
            }
        }
    }

    private Vector3 ResolveSpawnOrigin()
    {
        GameObject spawnPoint = GameObject.Find(spawnPointName);
        if (spawnPoint != null)
        {
            return spawnPoint.transform.position;
        }

        return fallbackSpawnPosition;
    }
}
