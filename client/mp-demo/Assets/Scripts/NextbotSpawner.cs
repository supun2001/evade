using System.Collections.Generic;
using UnityEngine;

public class NextbotSpawner : MonoBehaviour
{
    private const int FallbackNextbotCount = 5;
    private const string NextbotPrefabResourcePath = "Prefrabs/NextBots";

    private NextbotRegistry _nextbotRegistry;
    private GameObject _nextbotPrefab;

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
        _nextbotRegistry = Resources.Load<NextbotRegistry>("NextbotRegistry");
        _nextbotPrefab = Resources.Load<GameObject>(NextbotPrefabResourcePath);
        EnsureNetworkedNextbotInstances();
    }

    private void Update()
    {
        EnsureNetworkedNextbotInstances();
    }

    private void EnsureNetworkedNextbotInstances()
    {
        List<string> desiredNextbotIds = GetDesiredNextbotIds();
        List<NextbotFollowPlayer> sceneNextbots = GetSceneNextbots();

        if (sceneNextbots.Count == 0)
        {
            NextbotFollowPlayer spawnedTemplate = SpawnTemplateNextbot();
            if (spawnedTemplate == null)
            {
                return;
            }

            sceneNextbots.Add(spawnedTemplate);
        }

        NextbotFollowPlayer template = sceneNextbots[0];
        for (int index = sceneNextbots.Count; index < desiredNextbotIds.Count; index++)
        {
            GameObject clone = Instantiate(template.gameObject, template.transform.position, template.transform.rotation);
            clone.name = $"NextBots_{index + 1}";
            NextbotFollowPlayer nextbot = clone.GetComponent<NextbotFollowPlayer>();
            if (nextbot != null)
            {
                sceneNextbots.Add(nextbot);
            }
        }

        for (int index = 0; index < sceneNextbots.Count; index++)
        {
            NextbotFollowPlayer nextbot = sceneNextbots[index];
            if (nextbot == null)
            {
                continue;
            }

            bool shouldKeep = index < desiredNextbotIds.Count;
            if (!shouldKeep)
            {
                Destroy(nextbot.gameObject);
                continue;
            }

            nextbot.gameObject.name = $"NextBots_{index + 1}";
            string nextbotId = desiredNextbotIds[index];
            nextbot.AssignNetworkNextbotId(nextbotId);
            if (_nextbotRegistry != null && _nextbotRegistry.TryGetEntry(nextbotId, out NextbotRegistryEntry entry))
            {
                nextbot.ApplyRegistryEntry(entry);
            }
            else
            {
                nextbot.ApplyRegistryEntry(null);
            }
            if (!nextbot.gameObject.activeSelf)
            {
                nextbot.gameObject.SetActive(true);
            }
        }

        RemoveExistingNextbotAgents();
    }

    private List<string> GetDesiredNextbotIds()
    {
        List<string> ids = new List<string>();

        if (NetworkManager.Instance != null
            && NetworkManager.Instance.Room != null
            && NetworkManager.Instance.Room.State != null
            && NetworkManager.Instance.Room.State.nextbots != null
            && NetworkManager.Instance.Room.State.nextbots.Count > 0)
        {
            foreach (string nextbotId in NetworkManager.Instance.Room.State.nextbots.Keys)
            {
                if (!string.IsNullOrWhiteSpace(nextbotId))
                {
                    ids.Add(nextbotId);
                }
            }
        }

        if (ids.Count == 0 && _nextbotRegistry != null && _nextbotRegistry.entries != null)
        {
            for (int i = 0; i < _nextbotRegistry.entries.Length; i++)
            {
                NextbotRegistryEntry entry = _nextbotRegistry.entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.nextbotId))
                {
                    ids.Add(entry.nextbotId);
                }
            }
        }

        if (ids.Count == 0)
        {
            for (int i = 0; i < FallbackNextbotCount; i++)
            {
                ids.Add($"nextbot_{i}");
            }
        }

        return ids;
    }

    private static List<NextbotFollowPlayer> GetSceneNextbots()
    {
        List<NextbotFollowPlayer> sceneNextbots = new List<NextbotFollowPlayer>();
        NextbotFollowPlayer[] allNextbots = Resources.FindObjectsOfTypeAll<NextbotFollowPlayer>();
        for (int i = 0; i < allNextbots.Length; i++)
        {
            NextbotFollowPlayer nextbot = allNextbots[i];
            if (nextbot == null)
            {
                continue;
            }

            GameObject nextbotObject = nextbot.gameObject;
            if (!nextbotObject.scene.IsValid())
            {
                continue;
            }

            sceneNextbots.Add(nextbot);
        }

        return sceneNextbots;
    }

    private NextbotFollowPlayer SpawnTemplateNextbot()
    {
        if (_nextbotPrefab == null)
        {
            return null;
        }

        GameObject clone = Instantiate(_nextbotPrefab);
        clone.name = "NextBots_1";
        clone.SetActive(true);
        return clone.GetComponent<NextbotFollowPlayer>();
    }

    private static void RemoveExistingNextbotAgents()
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
}
