using System.IO;
using UnityEngine;
#if UNITY_EDITOR || UNITY_STANDALONE
using System.Diagnostics;
#endif

namespace Bajoterra.Unity
{
    /// <summary>
    /// Starts the Java server as a child process so the game can be tried with one click ("play against bots"). It
    /// needs a Java runtime (JDK 27 for the shipped server) and the shaded server jar built by Maven. On platforms
    /// that cannot start processes it simply reports that it is unavailable, and the player connects to a server
    /// someone else runs.
    /// </summary>
    public sealed class LocalServerLauncher
    {
        private const string ReadyMarker = "listening on TCP port";

#if UNITY_EDITOR || UNITY_STANDALONE
        private Process _process;
        private volatile bool _ready;

        /// <summary>True once the server printed that it is listening.</summary>
        public bool Ready
        {
            get { return _ready; }
        }

        /// <summary>True when the process was started and has since exited.</summary>
        public bool Exited
        {
            get { return _process != null && _process.HasExited; }
        }

        public bool Running
        {
            get { return _process != null && !_process.HasExited; }
        }

        /// <summary>Starts the server; returns false and the reason when it cannot.</summary>
        public bool Start(string configuredJar, int port, out string error)
        {
            Stop();
            string jar = FindJar(configuredJar);
            if (jar == null)
            {
                UnityEngine.Debug.LogWarning("Bajoterra: no server jar found; build it with `mvn -DskipTests package`.");
                error = UiText.CaveUnavailable;
                return false;
            }
            // BAJOTERRA_SERVER_ARGS adds server options, e.g. "--rules quick" for a match that lasts seconds.
            string extra = System.Environment.GetEnvironmentVariable("BAJOTERRA_SERVER_ARGS");
            var info = new ProcessStartInfo(JavaExecutable(), "-jar \"" + jar + "\" --port " + port
                                                             + (string.IsNullOrEmpty(extra) ? string.Empty : " " + extra))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            try
            {
                _ready = false;
                _process = new Process { StartInfo = info, EnableRaisingEvents = true };
                _process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null && e.Data.Contains(ReadyMarker))
                    {
                        _ready = true;
                    }
                };
                _process.ErrorDataReceived += (sender, e) => { };
                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch (System.Exception e)
            {
                _process = null;
                UnityEngine.Debug.LogWarning("Bajoterra: could not start Java for the local server: " + e.Message);
                error = UiText.CaveUnavailable;
                return false;
            }
            error = string.Empty;
            return true;
        }

        public void Stop()
        {
            if (_process == null)
            {
                return;
            }
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch (System.Exception)
            {
                // Already gone.
            }
            _process.Dispose();
            _process = null;
            _ready = false;
        }

        /// <summary>A TCP port nothing is listening on right now, or -1.</summary>
        public static int FreePort()
        {
            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
                listener.Start();
                int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }
            catch (System.Exception)
            {
                return -1;
            }
        }

        private static string JavaExecutable()
        {
            string home = System.Environment.GetEnvironmentVariable("JAVA_HOME");
            if (string.IsNullOrEmpty(home))
            {
                home = "/opt/homebrew/opt/openjdk/libexec/openjdk.jdk/Contents/Home";
            }
            string candidate = Path.Combine(home, "bin", "java");
            return File.Exists(candidate) ? candidate : "java";
        }
#else
        public static int FreePort() { return -1; }

        public bool Ready { get { return false; } }

        public bool Exited { get { return false; } }

        public bool Running { get { return false; } }

        public bool Start(string configuredJar, int port, out string error)
        {
            error = UiText.CaveUnavailable;
            return false;
        }

        public void Stop()
        {
        }
#endif

        /// <summary>The configured jar if it exists, otherwise the one built next to the Unity project.</summary>
        public static string FindJar(string configured)
        {
            if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
            {
                return configured;
            }
            // .../<repo>/unity/BajoTerraClient/Assets -> look for <repo>/bajoterra-server/target
            string dir = Application.dataPath;
            for (int i = 0; i < 5 && !string.IsNullOrEmpty(dir); i++)
            {
                dir = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(dir))
                {
                    break;
                }
                string target = Path.Combine(dir, "bajoterra-server", "target");
                if (Directory.Exists(target))
                {
                    string[] jars = Directory.GetFiles(target, "bajoterra-server-*-all.jar");
                    if (jars.Length > 0)
                    {
                        System.Array.Sort(jars);
                        return jars[jars.Length - 1];
                    }
                }
            }
            return null;
        }
    }
}
