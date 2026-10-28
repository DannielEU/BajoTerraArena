using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bajoterra.Editor
{
    /// <summary>
    /// First-open housekeeping. The game builds itself from code at start-up, so the project only needs an empty
    /// scene to open and "run in background" switched on (a network client must keep receiving while the window is
    /// unfocused). It never touches a project that already has scenes.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += Configure;
        }

        private static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }
            if (!PlayerSettings.runInBackground)
            {
                PlayerSettings.runInBackground = true;
            }
            if (AssetDatabase.FindAssets("t:Scene").Length > 0)
            {
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Bajoterra: created " + ScenePath + ". Press Play: the game builds itself from code.");
        }
    }
}
