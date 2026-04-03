using System.Collections.Generic;
using UnityEngine;

public class NextbotSpawner : MonoBehaviour
{
    private const int DesiredNextbotCount = 5;
    private NextbotRegistry _nextbotRegistry;

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
        EnsureNetworkedNextbotInstances();
    }

    private void EnsureNetworkedNextbotInstances()
    {
        List<NextbotFollowPlayer> sceneNextbots = GetSceneNextbots();
        if (sceneNextbots.Count == 0)
        {
            return;
        }

        NextbotFollowPlayer template = sceneNextbots[0];
        for (int index = sceneNextbots.Count; index < DesiredNextbotCount; index++)
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

            bool shouldKeep = index < DesiredNextbotCount;
            if (!shouldKeep)
            {
                Destroy(nextbot.gameObject);
                continue;
            }

            nextbot.gameObject.name = $"NextBots_{index + 1}";
            string nextbotId = $"nextbot_{index}";
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
