using System;
using System.Collections;
using System.IO;
using System.Linq;
using Bajoterra.Client;
using Bajoterra.Client.Content;
using Bajoterra.Client.Interpolation;
using Bajoterra.Protocol;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Bajoterra.Unity.Tests
{
    /// <summary>
    /// Tormato's tornadoes, Arachnet's web traps and fused or megamorphed slugs are drawn as the server lists them, and
    /// go away when it stops listing them. With <c>BAJOTERRA_SCREENSHOTS</c> set it also writes a picture of them.
    /// </summary>
    public sealed class TornadoAndWebViewTests
    {
        private GameObject _stage;
        private AudioPlayer _audio;

        [SetUp]
        public void Setup()
        {
            if (GameApp.Instance != null)
            {
                Object.Destroy(GameApp.Instance.gameObject);
            }
            _stage = new GameObject("Tornado stage");
            _audio = new AudioPlayer(_stage.transform);
        }

        [TearDown]
        public void Teardown()
        {
            Object.Destroy(_stage);
        }

        private static Snapshot Tornado(bool flaming, uint ticksLeft, float x = 0f, bool freezing = false)
        {
            var snapshot = new Snapshot();
            snapshot.Tornadoes.Add(new TornadoState
            {
                Id = flaming ? 2u : freezing ? 3u : 1u, SlugIndex = 0, Owner = 0, X = x, Y = 0f, Z = 0f, Radius = 3.5f,
                EyeRadius = 1.2f, Height = 4.5f, TicksLeft = ticksLeft, LifeTicks = 180, Flaming = flaming, Freezing = freezing
            });
            return snapshot;
        }

        private static Snapshot Webs(uint ticksLeft)
        {
            var snapshot = new Snapshot();
            snapshot.Webs.Add(new WebState { Id = 1, Owner = 1, X = 0f, Y = 0f, Z = 0f, Radius = 1.6f, TicksLeft = ticksLeft, LifeTicks = 900 });
            snapshot.Webs.Add(new WebState { Id = 2, Owner = 0, X = 3.5f, Y = 0f, Z = 1f, Radius = 1.6f, TicksLeft = ticksLeft, LifeTicks = 900 });
            return snapshot;
        }

        private static ContentCatalog Catalog()
        {
            var manifest = new ContentManifest();
            manifest.SlugIds.Add("tormato");
            manifest.SlugIds.Add("arachnet");
            foreach (string element in new[] { "NEUTRAL", "FIRE", "WATER", "EARTH", "AIR", "ENERGY", "ICE", "METAL", "LIGHT" })
            {
                manifest.ElementIds.Add(element);
            }
            var details = new ContentDetails();
            details.Slugs.Add(new SlugInfo
            {
                Id = "tormato", Name = "Tormato", Element = 4, Targeting = Targeting.Enemies, Role = "CONTROL",
                ProjectileRadius = 0.28f, ProjectileSpeed = 38f, TornadoRadius = 3.5f
            });
            details.Slugs.Add(new SlugInfo
            {
                Id = "arachnet", Name = "Arachnet", Element = 3, Targeting = Targeting.Enemies, Role = "CONTROL",
                ProjectileRadius = 0.3f, ProjectileSpeed = 40f, WebRadius = 1.6f
            });
            return new ContentCatalog(manifest, details);
        }

        private static WorldFrame Flying(int partner, float radius, float x)
        {
            var frame = new WorldFrame { Valid = true, ProjectileCount = 1 };
            frame.Projectiles[0] = new ProjectileFrame
            {
                Id = (uint)(10 + partner + 2), SlugIndex = 0, Owner = 0, Position = new Vec3(x, 1.4f, -2f),
                Velocity = new Vec3(20f, 0f, 0f), StuckTo = -1, PartnerSlug = partner, Radius = radius
            };
            return frame;
        }

        [Test]
        public void ATornadoSpinsWhileListedAndCatchesFire()
        {
            var tornadoes = new TornadoViews(_stage.transform, _audio);
            tornadoes.Apply(Tornado(false, 150), 0.05f);
            Assert.AreEqual(1, tornadoes.Count);
            ParticleSystem streaks = _stage.GetComponentsInChildren<ParticleSystem>().First(p => p.name == "Streaks");
            streaks.Simulate(0.5f, true, false);
            Assert.Greater(streaks.particleCount, 20, "dust spirals up the tornado");
            Assert.IsFalse(_stage.GetComponentsInChildren<Light>().Any(l => l.name == "Fire light" && l.enabled));

            tornadoes.Apply(Tornado(true, 150), 0.05f);
            Assert.IsTrue(_stage.GetComponentsInChildren<Light>().Any(l => l.name == "Fire light" && l.enabled),
                "a fire tornado glows");

            tornadoes.Apply(new Snapshot(), 0.05f);
            Assert.AreEqual(0, tornadoes.Count);
            tornadoes.Destroy();
        }

        [Test]
        public void ATornadoThatSwallowedAnIceSlugTurnsIcyBlue()
        {
            var tornadoes = new TornadoViews(_stage.transform, _audio);
            tornadoes.Apply(Tornado(false, 150), 0.05f);
            Snapshot icy = Tornado(false, 149, 0f, false);
            icy.Tornadoes[0].Freezing = true;
            tornadoes.Apply(icy, 0.05f);

            Light light = _stage.GetComponentsInChildren<Light>().First(l => l.name == "Fire light");
            Assert.IsTrue(light.enabled, "a freezing tornado glows");
            Assert.Greater(light.color.b, light.color.r, "with a cold light");
            tornadoes.Destroy();
        }

        [Test]
        public void WebsLieOnTheFloorUntilTheyAreGone()
        {
            var webs = new WebViews(_stage.transform, 0, id => id == 0 ? 0 : 1);
            webs.Apply(Webs(600), 0.05f);
            Assert.AreEqual(2, webs.Count);
            Assert.IsNotNull(WebViews.Texture);

            webs.Apply(new Snapshot(), 0.05f);
            Assert.AreEqual(0, webs.Count);
            webs.Destroy();
        }

        [UnityTest]
        public IEnumerator Pictures_of_a_tornado_webs_and_fused_slugs()
        {
            string dir = Environment.GetEnvironmentVariable("BAJOTERRA_SCREENSHOTS");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Ignore("set BAJOTERRA_SCREENSHOTS to write the pictures");
            }
            Directory.CreateDirectory(dir);
            RenderSettings.ambientLight = new Color(0.4f, 0.47f, 0.5f);
            RenderSettings.fog = false;
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.SetParent(_stage.transform, false);
            floor.transform.localScale = Vector3.one * 3f;
            floor.GetComponent<Renderer>().sharedMaterial =
                MaterialFactory.Surface(Textures.Floor, new Color(0.45f, 0.55f, 0.55f), 0.2f, 0f);
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(_stage.transform, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.12f, 0.14f);
            camera.fieldOfView = 50f;

            // 1. A tornado, a fire tornado and a freezing tornado side by side.
            var tornadoes = new TornadoViews(_stage.transform, _audio);
            for (int i = 0; i < 45; i++)
            {
                var both = Tornado(false, (uint)(170 - i), -7f);
                both.Tornadoes.Add(Tornado(true, (uint)(170 - i), 0f).Tornadoes[0]);
                both.Tornadoes.Add(Tornado(false, (uint)(170 - i), 7f, true).Tornadoes[0]);
                tornadoes.Apply(both, 0.033f);
                yield return null;
            }
            // Batchmode frames last a few milliseconds: give the dust a second of real flight before the picture.
            foreach (ParticleSystem streaks in _stage.GetComponentsInChildren<ParticleSystem>())
            {
                streaks.Simulate(1.2f, true, false);
            }
            camera.transform.position = new Vector3(0f, 3.6f, -17f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 2.2f, 0f) - camera.transform.position);
            yield return null;
            Shoot(camera, Path.Combine(dir, "tornadoes.png"));
            tornadoes.Destroy();

            // 2. An enemy web and an allied one on the floor, a fused slug and a giant one flying over them.
            var webs = new WebViews(_stage.transform, 0, id => id == 0 ? 0 : 1);
            var slugs = new ProjectileViews(_stage.transform, Catalog(), new ArenaMap(new MapData { MinX = -20, MaxX = 20, MinZ = -20, MaxZ = 20 }), _audio);
            for (int i = 0; i < 20; i++)
            {
                webs.Apply(Webs(600), 0.033f);
                yield return null;
            }
            WorldFrame frame = Flying(1, 0.28f, -2f);
            frame.ProjectileCount = 2;
            frame.Projectiles[1] = Flying(-1, 0.48f, 2f).Projectiles[0];
            for (int i = 0; i < 8; i++)
            {
                slugs.Apply(frame, i * 0.033f);
                yield return null;
            }
            camera.transform.position = new Vector3(0.5f, 4.5f, -7.5f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0.8f, 0.4f, 0f) - camera.transform.position);
            yield return null;
            Shoot(camera, Path.Combine(dir, "webs-and-fused-slugs.png"));
            webs.Destroy();
            slugs.Destroy();
        }

        private static void Shoot(Camera camera, string path)
        {
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = null;
            RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.Destroy(texture);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
