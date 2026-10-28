using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Starts the game from any scene when Play is pressed (or when a build launches), so the project needs no
    /// hand-built scene: everything is created from code by <see cref="GameApp"/>.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if (GameApp.Instance != null)
            {
                return;
            }
            var go = new GameObject("Bajoterra");
            go.AddComponent<GameApp>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
