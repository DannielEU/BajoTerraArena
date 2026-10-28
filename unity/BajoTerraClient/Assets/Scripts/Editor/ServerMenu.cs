using System.Diagnostics;
using System.IO;
using Bajoterra.Unity;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Bajoterra.Editor
{
    /// <summary>
    /// Editor menu to build and run the Java server without leaving Unity: <b>Bajoterra &gt; Server</b>. It needs
    /// Maven and a JDK 27 on the machine (see docs/unity-handoff.md).
    /// </summary>
    public static class ServerMenu
    {
        private static readonly LocalServerLauncher Launcher = new LocalServerLauncher();

        static ServerMenu()
        {
            EditorApplication.quitting += Launcher.Stop;
        }

        [MenuItem("Bajoterra/Server/Build server jar")]
        public static void BuildServer()
        {
            string root = RepositoryRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog("Bajoterra", "No se encontró la raíz del repositorio (pom.xml).", "OK");
                return;
            }
            var info = new ProcessStartInfo(MavenExecutable(), "-q -DskipTests package")
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            string home = System.Environment.GetEnvironmentVariable("JAVA_HOME");
            info.EnvironmentVariables["JAVA_HOME"] = string.IsNullOrEmpty(home)
                ? "/opt/homebrew/opt/openjdk/libexec/openjdk.jdk/Contents/Home"
                : home;
            var process = new Process { StartInfo = info, EnableRaisingEvents = true };
            process.OutputDataReceived += (sender, e) => { if (e.Data != null) { Debug.Log("[mvn] " + e.Data); } };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) { Debug.LogWarning("[mvn] " + e.Data); } };
            process.Exited += (sender, e) => Debug.Log("Bajoterra: mvn package finished with code " + process.ExitCode);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Debug.Log("Bajoterra: building the server jar (mvn -DskipTests package)...");
        }

        [MenuItem("Bajoterra/Server/Start local server")]
        public static void StartServer()
        {
            if (Launcher.Start(string.Empty, 7777, out string error))
            {
                Debug.Log("Bajoterra: local server starting on port 7777.");
            }
            else
            {
                Debug.LogWarning("Bajoterra: " + error);
            }
        }

        [MenuItem("Bajoterra/Server/Stop local server")]
        public static void StopServer()
        {
            Launcher.Stop();
            Debug.Log("Bajoterra: local server stopped.");
        }

        private static string RepositoryRoot()
        {
            // Assets -> BajoTerraClient -> unity -> <repo>
            string dir = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            for (int i = 0; i < 4 && !string.IsNullOrEmpty(dir); i++)
            {
                if (File.Exists(Path.Combine(dir, "pom.xml")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private static string MavenExecutable()
        {
            foreach (string candidate in new[] { "/opt/homebrew/bin/mvn", "/usr/local/bin/mvn", "/usr/bin/mvn" })
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return "mvn";
        }
    }
}
