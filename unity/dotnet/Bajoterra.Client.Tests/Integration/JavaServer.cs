using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;

namespace Bajoterra.Client.Integration
{
    /// <summary>
    /// Starts the real Java server (the shaded jar built by Maven) on a free port and stops it afterwards. When the jar
    /// is not built the integration tests are skipped, unless <c>BAJOTERRA_REQUIRE_SERVER=1</c> turns that into a
    /// failure (the verify script sets it).
    /// </summary>
    public sealed class JavaServer : IDisposable
    {
        private static readonly Regex PortLine = new Regex(@"listening on TCP port (\d+)");

        private readonly Process _process;

        private JavaServer(Process process, int port)
        {
            _process = process;
            Port = port;
        }

        public int Port { get; }

        public static JavaServer Start()
        {
            string jar = FindJar();
            if (jar == null)
            {
                string message = "The server jar is not built (run: mvn -DskipTests package)";
                if (Environment.GetEnvironmentVariable("BAJOTERRA_REQUIRE_SERVER") == "1")
                {
                    Assert.Fail(message);
                }
                Assert.Ignore(message);
            }
            // The ramp tests walk Caverna's hills, so these tests always play the hand-made map.
            var start = new ProcessStartInfo(JavaExecutable(), "-jar \"" + jar + "\" --port 0 --map caverna")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            Process process = Process.Start(start);
            string line = null;
            var reader = new Thread(() => { line = process.StandardOutput.ReadLine(); }) { IsBackground = true };
            reader.Start();
            if (!reader.Join(TimeSpan.FromSeconds(20)) || line == null)
            {
                process.Kill();
                throw new InvalidOperationException("The server did not report its port");
            }
            Match match = PortLine.Match(line);
            if (!match.Success)
            {
                process.Kill();
                throw new InvalidOperationException("Unexpected server output: " + line);
            }
            // Keep draining so the server never blocks on a full pipe.
            new Thread(() => { while (process.StandardOutput.ReadLine() != null) { } }) { IsBackground = true }
                .Start();
            new Thread(() => { while (process.StandardError.ReadLine() != null) { } }) { IsBackground = true }
                .Start();
            return new JavaServer(process, int.Parse(match.Groups[1].Value));
        }

        public void Dispose()
        {
            if (!_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(5000);
            }
            _process.Dispose();
        }

        private static string FindJar()
        {
            for (string dir = AppContext.BaseDirectory; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
            {
                string target = Path.Combine(dir, "bajoterra-server", "target");
                if (Directory.Exists(target))
                {
                    return Directory.GetFiles(target, "bajoterra-server-*-all.jar").OrderBy(f => f).FirstOrDefault();
                }
            }
            return null;
        }

        private static string JavaExecutable()
        {
            string home = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (string.IsNullOrEmpty(home))
            {
                home = "/opt/homebrew/opt/openjdk/libexec/openjdk.jdk/Contents/Home";
            }
            string candidate = Path.Combine(home, "bin", "java");
            return File.Exists(candidate) ? candidate : "java";
        }
    }
}
