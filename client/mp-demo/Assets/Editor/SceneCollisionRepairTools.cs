using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneCollisionRepairTools
{
    [MenuItem("Evade/Collision/Repair Broken Colliders In Active Scene")]
    private static void RepairBrokenCollidersInActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            Debug.LogWarning("SceneCollisionRepairTools: no active loaded scene to repair.");
            return;
        }

        int repairedCount = 0;
        int boxProxyCount = 0;
        List<MeshCollider> brokenColliders = new List<MeshCollider>();

        GameObject[] roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            brokenColliders.AddRange(roots[i].GetComponentsInChildren<MeshCollider>(true));
        }

        for (int i = 0; i < brokenColliders.Count; i++)
        {
            MeshCollider meshCollider = brokenColliders[i];
            if (meshCollider == null || meshCollider.sharedMesh != null)
            {
                continue;
            }

            if (TryAssignLocalMesh(meshCollider))
            {
                repairedCount++;
                continue;
            }

            if (ReplaceWithBoundsProxy(meshCollider))
            {
                repairedCount++;
                boxProxyCount++;
            }
        }

        if (repairedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }

        Debug.Log(
            $"SceneCollisionRepairTools: repaired {repairedCount} broken colliders in {activeScene.name}. " +
            $"Created {boxProxyCount} box proxies.");
    }

    private static bool TryAssignLocalMesh(MeshCollider meshCollider)
    {
        MeshFilter meshFilter = meshCollider.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return false;
        }

        Undo.RecordObject(meshCollider, "Assign MeshCollider Mesh");
        meshCollider.sharedMesh = meshFilter.sharedMesh;
        EditorUtility.SetDirty(meshCollider);
        return true;
    }

    private static bool ReplaceWithBoundsProxy(MeshCollider brokenMeshCollider)
    {
        Renderer[] renderers = brokenMeshCollider.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        if (!TryGetCombinedWorldBounds(renderers, out Bounds worldBounds))
        {
            return false;
        }

        GameObject targetObject = brokenMeshCollider.gameObject;
        Transform targetTransform = targetObject.transform;

        BoxCollider boxCollider = targetObject.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = Undo.AddComponent<BoxCollider>(targetObject);
        }
        else
        {
            Undo.RecordObject(boxCollider, "Update BoxCollider Proxy");
        }

        Vector3 localCenter = targetTransform.InverseTransformPoint(worldBounds.center);
        Vector3 localSize = targetTransform.InverseTransformVector(worldBounds.size);
        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

        boxCollider.center = localCenter;
        boxCollider.size = localSize;
        EditorUtility.SetDirty(boxCollider);

        Undo.DestroyObjectImmediate(brokenMeshCollider);
        return true;
    }

    private static bool TryGetCombinedWorldBounds(Renderer[] renderers, out Bounds combinedBounds)
    {
        combinedBounds = default;
        bool foundAny = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!foundAny)
            {
                combinedBounds = renderer.bounds;
                foundAny = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        return foundAny;
    }
}
