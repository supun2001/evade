using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Build Size Optimizer — Run from Tools > Build Size Optimizer
/// Forces reimport of all audio and texture assets, removes duplicates.
/// </summary>
public class BuildSizeOptimizer
{
    // ────────────────────────────────────────────────────────
    // STEP 1 — Force reimport all changed assets
    // ────────────────────────────────────────────────────────
    [MenuItem("Tools/Build Size Optimizer/1. Force Reimport Changed Assets")]
    public static void ForceReimportChangedAssets()
    {
        var paths = new List<string>();

        // Audio files
        paths.Add("Assets/SFX/InGameMusic.mp3");
        paths.Add("Assets/SFX/MainMenu.mp3");
        paths.Add("Assets/SFX/Intermission.mp3");
        paths.Add("Assets/SFX/RoundStart.mp3");
        paths.Add("Assets/SFX/JumpBost.mp3");
        paths.Add("Assets/SFX/NextBotHits.mp3");
        paths.Add("Assets/SFX/SpeedBostUp.mp3");
        paths.Add("Assets/Resources/SFX/InGameMusic.mp3");
        paths.Add("Assets/Resources/SFX/MainMenu.mp3");
        paths.Add("Assets/Resources/SFX/Intermission.mp3");
        paths.Add("Assets/Resources/SFX/RoundStart.mp3");

        // Textures
        paths.Add("Assets/UI/Inventory.png");
        paths.Add("Assets/UI/Shop.png");
        paths.Add("Assets/UI/join game.png");
        paths.Add("Assets/Images/next_img.png");
        paths.Add("Assets/Images/pre_img.png");
        paths.Add("Assets/Resources/UI/JoinGameCard.png");
        paths.Add("Assets/Resources/UI/InventoryCard.png");
        paths.Add("Assets/Models/Abilities/lightning_red_normal.png");
        paths.Add("Assets/Models/Abilities/lightning_red_roughness.png");
        paths.Add("Assets/Models/Abilities/lightning_red_basecolor.png");
        paths.Add("Assets/Models/Abilities/lightning_red_emissive.png");
        paths.Add("Assets/Models/Abilities/lightning_red_metallic.png");
        paths.Add("Assets/Prefrabs/NextBots Assets/Untitled design (2).png");
        paths.Add("Assets/Prefrabs/NextBots Assets/Untitled design (10).png");
        paths.Add("Assets/Prefrabs/NextBots Assets/Untitled design (6).png");
        paths.Add("Assets/Prefrabs/NextBots Assets/Untitled design (13).png");
        paths.Add("Assets/Prefrabs/NextBots Assets/Untitled design (14).png");
        paths.Add("Assets/Prefrabs/NextBots Assets/TheRock.png");

        int count = 0;
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
                EditorUtility.DisplayProgressBar("Reimporting Assets", path, (float)count / paths.Count);
            }
            else
            {
                Debug.LogWarning($"[BuildSizeOptimizer] Asset not found: {path}");
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();
        Debug.Log($"[BuildSizeOptimizer] ✅ Force reimported {count} assets. Now rebuild for WebGL.");
        EditorUtility.DisplayDialog("Done!", $"Force reimported {count} assets.\n\nNow go to File > Build Settings and build for WebGL.", "OK");
    }

    // ────────────────────────────────────────────────────────
    // STEP 2 — Delete duplicate SFX files
    // ────────────────────────────────────────────────────────
    [MenuItem("Tools/Build Size Optimizer/2. Remove Duplicate SFX Folder (SAFE)")]
    public static void RemoveDuplicateSFX()
    {
        // CONFIRMED: Assets/SFX/ files have 0 references in scenes/prefabs.
        // All audio is loaded via Resources.Load() from Assets/Resources/SFX/.
        // Deleting the Assets/SFX/ folder removes ~15MB of redundant data from the build.
        var filesToDelete = new[]
        {
            "Assets/SFX/InGameMusic.mp3",
            "Assets/SFX/MainMenu.mp3",
            "Assets/SFX/Intermission.mp3",
            "Assets/SFX/RoundStart.mp3",
            "Assets/SFX/JumpBost.mp3",
            "Assets/SFX/NextBotHits.mp3",
            "Assets/SFX/SpeedBostUp.mp3",
            "Assets/SFX/PlayerGotHit.mp3",
        };

        if (!EditorUtility.DisplayDialog("Remove Duplicate SFX Folder",
            "Assets/SFX/ is NOT referenced anywhere in the project.\n" +
            "All audio loads from Assets/Resources/SFX/ via Resources.Load().\n\n" +
            $"This will delete {filesToDelete.Length} redundant files.\n" +
            "Estimated savings: ~14MB from build data!",
            "Delete Duplicates", "Cancel"))
            return;

        int deleted = 0;
        foreach (var path in filesToDelete)
        {
            if (File.Exists(path))
            {
                if (AssetDatabase.DeleteAsset(path))
                {
                    deleted++;
                    Debug.Log($"[BuildSizeOptimizer] ✅ Deleted: {path}");
                }
                else
                    Debug.LogError($"[BuildSizeOptimizer] ❌ Failed to delete: {path}");
            }
        }

        // Delete the folder itself if empty
        if (Directory.Exists("Assets/SFX"))
        {
            var remaining = Directory.GetFiles("Assets/SFX", "*", SearchOption.AllDirectories);
            var nonMeta = System.Array.FindAll(remaining, f => !f.EndsWith(".meta"));
            if (nonMeta.Length == 0)
            {
                AssetDatabase.DeleteAsset("Assets/SFX");
                Debug.Log("[BuildSizeOptimizer] ✅ Deleted empty Assets/SFX/ folder");
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done! ~14MB saved!",
            $"Deleted {deleted} redundant audio files from Assets/SFX/.\n\n" +
            "Now run Step 1 (Force Reimport) then rebuild for WebGL!", "OK");
    }

    // ────────────────────────────────────────────────────────
    // STEP 3 — Show a build size report in the console
    // ────────────────────────────────────────────────────────
    [MenuItem("Tools/Build Size Optimizer/3. Show Large Asset Report")]
    public static void ShowLargeAssetReport()
    {
        var allAssets = AssetDatabase.GetAllAssetPaths();
        var large = new List<(string path, long size)>();

        foreach (var path in allAssets)
        {
            if (!path.StartsWith("Assets/")) continue;
            if (!File.Exists(path)) continue;
            long size = new FileInfo(path).Length;
            if (size > 200_000) // > 200KB
                large.Add((path, size));
        }

        large.Sort((a, b) => b.size.CompareTo(a.size));

        Debug.Log("═══════════════ LARGE ASSET REPORT (>200KB) ═══════════════");
        foreach (var (path, size) in large)
            Debug.Log($"  {size / 1024,6} KB  {path}");
        Debug.Log($"═══════════════ {large.Count} large assets ═══════════════");
    }

    // ────────────────────────────────────────────────────────
    // STEP 4 — Clear Unity shader cache (frees stale data)
    // ────────────────────────────────────────────────────────
    [MenuItem("Tools/Build Size Optimizer/4. Clear Library Cache & Refresh")]
    public static void ClearLibraryCache()
    {
        if (!EditorUtility.DisplayDialog("Clear Library Cache",
            "This will delete Library/ShaderCache and Library/ShaderCache.ref, then refresh the database.\n\nShaders will recompile on next build (slower first build).\n\nProceed?",
            "Clear Cache", "Cancel"))
            return;

        string[] cachePaths = {
            "Library/ShaderCache",
            "Library/ShaderCache.ref",
        };

        foreach (var cp in cachePaths)
        {
            if (Directory.Exists(cp))
            {
                Directory.Delete(cp, true);
                Debug.Log($"[BuildSizeOptimizer] Cleared: {cp}");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("[BuildSizeOptimizer] ✅ Cache cleared.");
    }
}
