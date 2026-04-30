using UnityEditor;
using UnityEditor.SceneManagement;

public static class DeveloperMapLoader
{
    private const string ClassicScenePath = "Assets/Scenes/Classic.unity";
    private const string BackroomScenePath = "Assets/Scenes/backroom.unity";
    private const string BrutalistVoidScenePath = "Assets/Scenes/BrutalistVoid.unity";
    private const string ParkourScenePath = "Assets/Scenes/parkour.unity";

    [MenuItem("Evade/Maps/Load Classic")]
    private static void LoadClassic()
    {
        LoadScene(ClassicScenePath);
    }

    [MenuItem("Evade/Maps/Load Backroom")]
    private static void LoadBackroom()
    {
        LoadScene(BackroomScenePath);
    }

    [MenuItem("Evade/Maps/Load Brutalist Void")]
    private static void LoadBrutalistVoid()
    {
        LoadScene(BrutalistVoidScenePath);
    }

    [MenuItem("Evade/Maps/Load Parkour")]
    private static void LoadParkour()
    {
        LoadScene(ParkourScenePath);
    }

    private static void LoadScene(string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            return;
        }

        if (EditorSceneManager.GetActiveScene().isDirty
            && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(scenePath);
    }
}
