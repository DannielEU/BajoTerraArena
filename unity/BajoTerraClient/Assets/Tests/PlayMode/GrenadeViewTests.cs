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
    /// Grenuke on screen and in the speakers: a grenade in flight carries its fuse, a stuck one blinks, marks its blast
    /// on the floor and hisses until it is gone; its sounds are real, audible clips. With <c>BAJOTERRA_SCREENSHOTS</c>
    /// set, a picture of a stuck grenade is written too.
    /// </summary>
    public sealed class GrenadeViewTests
    {
        private const int FuseTicks = 96;

        private GameObject _stage;
        private AudioPlayer _audio;
        private ProjectileViews _views;

        [SetUp]
        public void Setup()
        {
            if (GameApp.Instance != null)
            {
                Object.Destroy(GameApp.Instance.gameObject);
            }
            _stage = new GameObject("Grenade stage");
            _audio = new AudioPlayer(_stage.transform);
            _views = new ProjectileViews(_stage.transform, Catalog(), Arena(), _audio);
        }

        [TearDown]
        public void Teardown()
        {
            _views.Destroy();
            Object.Destroy(_stage);
        }

        private static ContentCatalog Catalog()
        {
            var manifest = new ContentManifest();
            manifest.SlugIds.Add("grenuke");
            manifest.ElementIds.Add("NEUTRAL");
            manifest.ElementIds.Add("FIRE");
            var details = new ContentDetails();
            details.Slugs.Add(new SlugInfo
            {
                Id = "grenuke", Name = "Grenuke", Element = 1, Targeting = Targeting.Enemies, Role = "DAMAGE",
                ProjectileRadius = 0.3f, ProjectileSpeed = 28f, FireDelayMultiplier = 1.3f,
                Sticky = true, FuseTicks = FuseTicks, BlastRadius = 3.5f
            });
            return new ContentCatalog(manifest, details);
        }

        private static ArenaMap Arena()
        {
            return new ArenaMap(new MapData { Name = "test", MinX = -20, MaxX = 20, MinZ = -20, MaxZ = 20, GroundY = 0 });
        }

        private static WorldFrame Frame(bool stuck, int fuseTicks)
        {
            var frame = new WorldFrame { Valid = true, ProjectileCount = 1 };
            frame.Projectiles[0] = new ProjectileFrame
            {
                Id = 3, SlugIndex = 0, Owner = 1, Position = new Vec3(0f, stuck ? 0.3f : 1.5f, 0f),
                Velocity = stuck ? new Vec3(0f, 0f, 0f) : new Vec3(20f, 4f, 0f),
                Stuck = stuck, FuseTicks = fuseTicks, StuckTo = -1
            };
            return frame;
        }

        private Transform Find(string name)
        {
            return _stage.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        private AudioSource Fuse()
        {
            return _stage.GetComponentsInChildren<AudioSource>(true).First(s => s.gameObject.name == "Loop");
        }

        [Test]
        public void AGrenadeInFlightIsTheGrenukeModelWithItsFuseHissingButNoBlastCircle()
        {
            _views.Apply(Frame(false, 0), 1f);

            Assert.IsTrue(Find("Slug model grenuke").gameObject.activeInHierarchy, "the 3D Grenuke flies");
            Assert.IsFalse(Find("Grenade").gameObject.activeInHierarchy, "no stand-in band and fuse on the model");
            Assert.IsFalse(Find("Blast ring").gameObject.activeSelf);
            Assert.IsTrue(Fuse().isPlaying, "the fuse already hisses, quietly");
        }

        [Test]
        public void AStuckGrenadeMarksItsBlastOnTheFloorAndHissesLouderAndHigherAsItBurnsDown()
        {
            _views.Apply(Frame(false, 0), 1f);
            _views.Apply(Frame(true, FuseTicks), 1.1f);
            float early = Fuse().pitch;
            Transform ring = Find("Blast ring");

            Assert.IsTrue(ring.gameObject.activeSelf);
            Assert.AreEqual(0.06f, ring.position.y, 0.02f, "the circle lies on the floor, not at the grenade");
            Assert.AreEqual(7f, ring.localScale.x, 0.5f, "as wide as the blast");
            Assert.Greater(Fuse().volume, 0.5f);

            _views.Apply(Frame(true, 6), 2.6f);
            Assert.Greater(Fuse().pitch, early + 0.3f, "the hiss rises as the fuse runs out");
        }

        [Test]
        public void TheFuseFallsSilentWhenTheGrenadeIsGone()
        {
            _views.Apply(Frame(true, 30), 1f);
            var empty = new WorldFrame { Valid = true, ProjectileCount = 0 };

            _views.Apply(empty, 1.1f);

            Assert.IsFalse(Fuse().isPlaying);
        }

        [Test]
        public void TheNewSoundsAreRealAudibleClips()
        {
            AssertAudible(_audio.GrenadeBlast, 1.5f, "grenade blast");
            AssertAudible(_audio.Fuse, 0.5f, "fuse");
            AssertAudible(_audio.FuseBeep, 0.05f, "fuse beep");
            AssertAudible(_audio.Stick, 0.1f, "stick");
            AssertAudible(_audio.Critical, 0.2f, "critical");
            AssertAudible(_audio.Courage, 0.5f, "courage");
            Assert.Greater(Loudness(_audio.GrenadeBlast), Loudness(_audio.Explosion), "the grenade is the bigger bang");
        }

        private static void AssertAudible(AudioClip clip, float minSeconds, string what)
        {
            Assert.GreaterOrEqual(clip.length, minSeconds, what + " is long enough");
            var data = new float[clip.samples];
            Assert.IsTrue(clip.GetData(data, 0), what + " samples can be read");
            Assert.IsFalse(data.Any(float.IsNaN), what + " has no NaN samples");
            Assert.Greater(data.Max(Mathf.Abs), 0.1f, what + " is audible");
        }

        /// <summary>Sum of squares over the clip: how much sound energy it carries.</summary>
        private static float Loudness(AudioClip clip)
        {
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            return data.Sum(x => x * x);
        }

        [UnityTest]
        public IEnumerator Picture_of_a_stuck_grenade()
        {
            string dir = Environment.GetEnvironmentVariable("BAJOTERRA_SCREENSHOTS");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Ignore("set BAJOTERRA_SCREENSHOTS to write the picture");
            }
            RenderSettings.ambientLight = new Color(0.35f, 0.45f, 0.5f);
            RenderSettings.fog = false;
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.SetParent(_stage.transform, false);
            floor.GetComponent<Renderer>().sharedMaterial =
                MaterialFactory.Surface(null, new Color(0.18f, 0.3f, 0.32f), 0.1f, 0f);
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(_stage.transform, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.12f, 0.14f);
            camera.fieldOfView = 40f;
            camera.transform.position = new Vector3(0f, 3.2f, -6.5f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.3f, 0f) - camera.transform.position);

            for (int i = 0; i < 4; i++)
            {
                _views.Apply(Frame(false, 0), i * 0.016f);
                yield return null;
            }
            // Late in the fuse, on a blink: what the player sees just before it goes off.
            _views.Apply(Frame(true, FuseTicks), 1f);
            _views.Apply(Frame(true, 20), 1.06f);
            yield return null;

            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = null;
            RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "grenade-stuck.png"), texture.EncodeToPNG());
            Object.Destroy(texture);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
