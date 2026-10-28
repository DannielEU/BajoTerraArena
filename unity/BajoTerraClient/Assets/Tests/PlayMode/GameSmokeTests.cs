using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Bajoterra.Protocol;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Bajoterra.Unity.Tests
{
    /// <summary>
    /// Boots the real game in Play mode and drives its code-built UI the way a player does: menu, local server with
    /// bots, lobby, match. The tests fail on any error or exception the game logs. When
    /// <c>BAJOTERRA_SCREENSHOTS</c> names a directory, each stage is also written there as a PNG (needs a graphics
    /// device, so not under <c>-nographics</c>).
    /// </summary>
    public sealed class GameSmokeTests
    {
        private const string ScreenshotVariable = "BAJOTERRA_SCREENSHOTS";

        [UnitySetUp]
        public IEnumerator StartApp()
        {
            yield return null;
            if (GameApp.Instance == null)
            {
                // The runtime-initialize hook already started the game when Play mode began; this covers the case
                // where the test runner reset the scene afterwards.
                new GameObject("Bajoterra").AddComponent<GameApp>();
            }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator StopApp()
        {
            if (GameApp.Instance != null)
            {
                Object.Destroy(GameApp.Instance.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator Menu_is_built_and_the_game_stays_quiet()
        {
            yield return Frames(30);

            Assert.IsNotNull(FindButton("Door JUGAR"), "the play door of the main menu");
            Assert.IsNotNull(FindButton("Door CONTROLES"), "the controls door");
            Assert.IsNotNull(FindButton("Door AJUSTES"), "the settings door");
            Assert.IsNotNull(FindButton("Door SALIR"), "the quit door");
            Assert.IsNotNull(Object.FindAnyObjectByType<Camera>(), "the game camera");
            Assert.IsNotNull(Object.FindAnyObjectByType<Canvas>(), "the UI canvas");
            AssertCameraRendering();
            yield return Seconds(0.6f);
            Capture("1-menu");

            Click(FindButton("Door CONTROLES"));
            yield return Seconds(0.4f);
            Capture("1b-controls");
            Click(FindButton("Door AJUSTES"));
            yield return Seconds(0.4f);
            Capture("1c-settings");
            Click(FindButton("Door JUGAR"));
            yield return Seconds(0.4f);
            Assert.IsNotNull(FindButton("Local"), "the 'play against bots' button");
            Assert.IsNotNull(FindButton("Connect"), "the connect button");
            Capture("1d-play");
            AssertCameraRendering();
        }

        [UnityTest]
        [Timeout(150000)]
        public IEnumerator A_local_match_against_bots_runs_without_errors()
        {
            string jar = LocalServerLauncher.FindJar(string.Empty);
            if (jar == null)
            {
                Assert.Ignore("Server jar not built: run `mvn -DskipTests package` at the repository root.");
            }
            yield return Frames(10);

            Debug.Log("[smoke] starting the local server and connecting");
            Click(FindButton("Door JUGAR"));
            yield return Frames(5);
            Click(FindButton("Local"));
            yield return WaitForButton("JoinButton", 40f, "the lobby after the local server started");
            Capture("2-lobby-join");

            Debug.Log("[smoke] joining the room");
            Click(FindButton("JoinButton"));
            yield return WaitForButton("Ready", 20f, "the room step with the Ready button");
            yield return Frames(30);
            Capture("3-lobby-room");

            Debug.Log("[smoke] ready, waiting for the match");
            Click(FindButton("Ready"));
            yield return Seconds(4f);
            Capture("4-match-start");
            yield return Seconds(8f);
            Capture("5-match-running");
            AssertCameraRendering();

            int renderers = Object.FindObjectsByType<Renderer>().Length;
            Debug.Log("Smoke test: " + renderers + " renderers alive during the match.");
            Assert.Greater(renderers, 10, "the arena and the combatants should be on screen");

            // Free-camera photos of the arena's new places (the presenter puts the camera back next frame).
            Photo("5a-hill", new Vector3(-9f, 5f, -11f), new Vector3(-15.5f, 1.2f, 0f));
            Photo("5b-balcony", new Vector3(2f, 5f, 4f), new Vector3(0f, 2f, 18f));
            Photo("5c-middle", new Vector3(-6f, 3.5f, -3f), new Vector3(4f, 1f, 3f));
            Photo("5d-ramp-up-close", new Vector3(-20f, 2.2f, 1f), new Vector3(-15.5f, 1.4f, -4f));

            // The Tab table, the pause menu and the end-of-match screen, driven through the game's own code.
            object presenter = Private(GameApp.Instance, "_presenter");
            var realControls = (Bajoterra.Unity.IControls)Private(presenter, "_controls");
            SetPrivate(presenter, "_controls", new HeldScoreboard(realControls));
            yield return Seconds(0.8f);
            Assert.IsTrue(IsActive("ScoreboardLayer"), "holding Tab shows the match table");
            Capture("6-scoreboard");
            SetPrivate(presenter, "_controls", realControls);
            yield return Frames(5);

            Invoke(GameApp.Instance, "TogglePause");
            yield return Seconds(0.8f);
            Capture("7-pause");
            AssertCameraRendering();
            Invoke(GameApp.Instance, "TogglePause");
            yield return Frames(5);

            // Wait (not for ever) for the bots to defeat the idle player, to see the defeat panel for real.
            float deadline = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < deadline && !IsActive("Death"))
            {
                AssertCameraRendering();
                yield return null;
            }
            if (IsActive("Death"))
            {
                yield return Seconds(1.6f);
                Capture("8-defeated");
            }
            AssertCameraRendering();

            Invoke(GameApp.Instance, "OnMatchEnded", new MatchEnd { Winner = 0, WinsTeam0 = 3, WinsTeam1 = 1 });
            yield return Seconds(1.0f);
            Capture("9-result");
            AssertCameraRendering();
        }

        /// <summary>
        /// Plays two whole matches on the server's "quick" rules (one 15 s round): lobby, match, the real end of the
        /// match, the result screen, back to the room, ready again. At every frame of the way a camera must be drawing
        /// to Display 1, and the HUD and the result screen must come and go.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Two_whole_quick_matches_in_a_row_always_have_a_camera_rendering()
        {
            string jar = LocalServerLauncher.FindJar(string.Empty);
            if (jar == null)
            {
                Assert.Ignore("Server jar not built: run `mvn -DskipTests package` at the repository root.");
            }
            Environment.SetEnvironmentVariable("BAJOTERRA_SERVER_ARGS", "--rules quick");
            try
            {
                yield return Frames(10);
                Click(FindButton("Door JUGAR"));
                yield return Frames(5);
                Click(FindButton("Local"));
                yield return WaitForButton("JoinButton", 40f, "the lobby after the local server started");
                Click(FindButton("JoinButton"));
                yield return WaitForButton("Ready", 20f, "the room step");
                for (int match = 1; match <= 2; match++)
                {
                    Click(FindButton("Ready"));
                    yield return WaitUntilActive("Hud", 30f, "match " + match + " to start");
                    yield return Seconds(2f);
                    Capture("10-quick-match-" + match);
                    yield return WaitUntilActive("Result", 90f, "match " + match + " to end");
                    yield return Seconds(1f);
                    Capture("11-quick-result-" + match);
                    Assert.IsFalse(IsActive("Hud"), "the HUD is gone when the result screen shows");
                    Click(FindButton("Primary"));
                    yield return WaitForButton("Ready", 10f, "the room after match " + match);
                    AssertCameraRendering();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("BAJOTERRA_SERVER_ARGS", null);
            }
        }

        // ------------------------------------------------------------------------------------------ helpers

        private static IEnumerator WaitUntilActive(string objectName, float timeoutSeconds, string what)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!IsActive(objectName))
            {
                AssertCameraRendering();
                Assert.Less(Time.realtimeSinceStartup, deadline, "timed out waiting for " + what);
                yield return null;
            }
        }

        private static bool IsActive(string objectName)
        {
            foreach (RectTransform rect in Object.FindObjectsByType<RectTransform>())
            {
                if (rect.name == objectName && rect.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }
            return false;
        }

        private static object Private(object target, string field)
        {
            var info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(info, "private field " + field);
            return info.GetValue(target);
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(info, "private field " + field);
            info.SetValue(target, value);
        }

        /// <summary>The real controls with the Tab key held down.</summary>
        private sealed class HeldScoreboard : Bajoterra.Unity.IControls
        {
            private readonly Bajoterra.Unity.IControls _inner;

            public HeldScoreboard(Bajoterra.Unity.IControls inner)
            {
                _inner = inner;
            }

            public void Poll() { _inner.Poll(); }
            public float MoveX { get { return _inner.MoveX; } }
            public float MoveZ { get { return _inner.MoveZ; } }
            public Vector2 Look { get { return _inner.Look; } }
            public bool Fire { get { return _inner.Fire; } }
            public bool IsMoveHeld(string moveId) { return _inner.IsMoveHeld(moveId); }
            public int SlotKey { get { return _inner.SlotKey; } }
            public int Scroll { get { return _inner.Scroll; } }
            public bool ClickPressed { get { return _inner.ClickPressed; } }
            public bool PausePressed { get { return _inner.PausePressed; } }
            public bool DebugPressed { get { return _inner.DebugPressed; } }
            public bool ScoreboardHeld { get { return true; } }
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(info, "method " + method);
            info.Invoke(target, args);
        }

        /// <summary>
        /// The Game view says "Display 1 No cameras rendering" when no enabled camera draws to the screen. The game
        /// must have one at every moment of its life.
        /// </summary>
        private static void AssertCameraRendering()
        {
            bool found = false;
            foreach (Camera camera in Camera.allCameras)
            {
                if (camera.isActiveAndEnabled && camera.targetTexture == null && camera.targetDisplay == 0
                    && camera.cullingMask != 0)
                {
                    found = true;
                }
            }
            Assert.IsTrue(found, "an enabled camera is drawing to Display 1");
        }

        private static void Photo(string name, Vector3 position, Vector3 target)
        {
            Camera camera = Camera.main;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            Capture(name);
        }

        private static Button FindButton(string name)
        {
            foreach (Button button in Object.FindObjectsByType<Button>())
            {
                if (button.name == name && button.gameObject.activeInHierarchy)
                {
                    return button;
                }
            }
            return null;
        }

        private static void Click(Button button)
        {
            Assert.IsNotNull(button, "button to click");
            button.onClick.Invoke();
        }

        private static IEnumerator WaitForButton(string name, float timeoutSeconds, string what)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (FindButton(name) == null)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    var visible = new System.Collections.Generic.List<string>();
                    foreach (Button button in Object.FindObjectsByType<Button>())
                    {
                        if (button.gameObject.activeInHierarchy)
                        {
                            visible.Add(button.name);
                        }
                    }
                    Assert.Fail("Timed out waiting for " + what + " (button '" + name + "'). Visible buttons: "
                        + string.Join(", ", visible));
                }
                yield return null;
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Seconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Writes the current frame as a PNG. It renders the game camera into a texture by hand, with the overlay
        /// canvases temporarily drawn by that camera, because the usual end-of-frame screenshot never fires in
        /// batch mode.
        /// </summary>
        private static void Capture(string name)
        {
            string dir = Environment.GetEnvironmentVariable(ScreenshotVariable);
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "the game camera");
            Canvas[] canvases = Object.FindObjectsByType<Canvas>();
            var overlays = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                    overlays.Add(canvas);
                }
            }
            Canvas.ForceUpdateCanvases();

            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), texture.EncodeToPNG());
            Debug.Log("[smoke] screenshot " + name);
            Object.Destroy(texture);
            target.Release();
            Object.Destroy(target);
            foreach (Canvas canvas in overlays)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }
    }
}
