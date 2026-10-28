// Signatures of the UnityEditor API used by the editor scripts, written from the public documentation.
#pragma warning disable CS0649, CS0067, CS8981
using System;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItemAttribute : Attribute
    {
        public MenuItemAttribute(string path)
        {
        }

        public MenuItemAttribute(string path, bool isValidateFunction)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InitializeOnLoadAttribute : Attribute
    {
    }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok) => true;
    }

    public static class AssetDatabase
    {
        public static void Refresh()
        {
        }

        public static void SaveAssets()
        {
        }

        public static bool IsValidFolder(string path) => true;

        public static string CreateFolder(string parent, string name) => string.Empty;

        public static string[] FindAssets(string filter) => new string[0];
    }

    public static class EditorApplication
    {
        public static bool isPlaying { get; set; }
        public static bool isPlayingOrWillChangePlaymode { get; }
        public static bool isCompiling { get; }
        public static Action delayCall { get; set; }
        public static event Action quitting;
    }

    public struct EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled)
        {
        }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; }
    }

    public static class PlayerSettings
    {
        public static bool runInBackground { get; set; }
        public static string productName { get; set; }
        public static string companyName { get; set; }
        public static bool resizableWindow { get; set; }
    }

    public class AssetImporter
    {
        public string assetPath { get; }
    }

    public abstract class AssetPostprocessor
    {
        public string assetPath { get; }

        public AssetImporter assetImporter { get; }
    }

    public enum ModelImporterAnimationType { None, Legacy, Generic, Human }

    public enum ModelImporterAvatarSetup { NoAvatar, CreateFromThisModel, CopyFromOther }

    public enum ModelImporterAnimationCompression { Off, KeyframeReduction, KeyframeReductionAndCompression, Optimal }

    public enum ModelImporterTangents { Import, CalculateLegacy, CalculateLegacyWithSplitTangents, CalculateMikk, None }

    public class ModelImporter : AssetImporter
    {
        public ModelImporterAnimationType animationType { get; set; }

        public ModelImporterAvatarSetup avatarSetup { get; set; }

        public bool importAnimation { get; set; }

        public ModelImporterAnimationCompression animationCompression { get; set; }

        public bool optimizeGameObjects { get; set; }

        public bool importBlendShapes { get; set; }

        public bool importCameras { get; set; }

        public bool importLights { get; set; }

        public ModelImporterTangents importTangents { get; set; }

        public bool isReadable { get; set; }
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }

    public enum NewSceneMode { Single, Additive }

    public static class EditorSceneManager
    {
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;

        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string path) => true;
    }
}
