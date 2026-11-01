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
    /// The 3D slugs, Phosphoro's warning glow and the pools of water: every slug of the game has its Blender model, a
    /// fired slug flies as that model, a stuck Phosphoro charges up brighter and higher-pitched until it flashes, and
    /// the water ripples when someone wades through it or a slug lands in it. With <c>BAJOTERRA_SCREENSHOTS</c> set it
    /// also writes pictures of all the slugs, of a Phosphoro charging and of a splash.
    /// </summary>
    public sealed class SlugAndWaterViewTests
    {
        private static readonly string[] SlugIds =
        {
            "aquabeek", "arachnet", "armashelt", "boon_doc", "bubbaleone", "doc", "flaringo", "frostcrawler", "frostfang",
            "grenuke", "hop_rock", "infurnus", "lariat", "phosphoro", "rammstone", "tazerling", "thresher", "tormato"
        };

        private const int FlashFuse = 90;

        private GameObject _stage;
        private AudioPlayer _audio;

        [SetUp]
        public void Setup()
        {
            if (GameApp.Instance != null)
            {
                Object.Destroy(GameApp.Instance.gameObject);
            }
            _stage = new GameObject("Slug stage");
            _audio = new AudioPlayer(_stage.transform);
        }

        [TearDown]
        public void Teardown()
        {
            Object.Destroy(_stage);
        }

        private static ContentCatalog Catalog()
        {
            var manifest = new ContentManifest();
            manifest.SlugIds.Add("infurnus");
            manifest.SlugIds.Add("phosphoro");
            foreach (string element in new[] { "NEUTRAL", "FIRE", "WATER", "EARTH", "AIR", "ENERGY", "ICE", "METAL", "LIGHT" })
            {
                manifest.ElementIds.Add(element);
            }
            var details = new ContentDetails();
            details.Slugs.Add(new SlugInfo
            {
                Id = "infurnus", Name = "Infurnus", Element = 1, Targeting = Targeting.Enemies, Role = "DAMAGE",
                ProjectileRadius = 0.25f, ProjectileSpeed = 45f
            });
            details.Slugs.Add(new SlugInfo
            {
                Id = "phosphoro", Name = "Phosphoro", Element = 8, Targeting = Targeting.Enemies, Role = "CONTROL",
                ProjectileRadius = 0.3f, ProjectileSpeed = 20f, Sticky = true, FuseTicks = FlashFuse, FlashRadius = 26f
            });
            return new ContentCatalog(manifest, details);
        }

        private static ArenaMap Arena(bool water)
        {
            var data = new MapData { Name = "test", MinX = -20, MaxX = 20, MinZ = -20, MaxZ = 20, GroundY = 0 };
            if (water)
            {
                data.Water.Add(new WaterPool { X = 0f, Z = 0f, Radius = 1.6f });
            }
            return new ArenaMap(data);
        }

        private static WorldFrame Projectile(int slug, bool stuck, int fuseTicks, float x = 0f)
        {
            var frame = new WorldFrame { Valid = true, ProjectileCount = 1 };
            frame.Projectiles[0] = new ProjectileFrame
            {
                Id = 5, SlugIndex = slug, Owner = 1, Position = new Vec3(x, stuck ? 0.3f : 1.5f, 0f),
                Velocity = stuck ? new Vec3(0f, 0f, 0f) : new Vec3(18f, 2f, 0f),
                Stuck = stuck, FuseTicks = fuseTicks, StuckTo = -1
            };
            return frame;
        }

        private static WorldFrame Walker(float x, float y = 0f)
        {
            var frame = new WorldFrame { Valid = true, CombatantCount = 1 };
            frame.Combatants[0] = new CombatantFrame
            {
                Id = 2, Position = new Vec3(x, y, 0.2f), Alive = true, HitboxHeight = 1.8f
            };
            return frame;
        }

        private Transform Find(string name)
        {
            return _stage.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        // ------------------------------------------------------------------------------------------ the models

        [Test]
        public void EverySlugOfTheGameHasA3DModelOneUnitTallWithTheGamesMaterials()
        {
            Assert.AreEqual(SlugIds.Length, Resources.LoadAll<GameObject>(SlugModels.ResourcePath).Length,
                "one model per slug, no strays");
            foreach (string id in SlugIds)
            {
                Transform model = SlugModels.Create(id, _stage.transform);
                Assert.IsNotNull(model, id + " has a model");
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                Assert.IsNotEmpty(renderers, id);
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers)
                {
                    bounds.Encapsulate(r.bounds);
                    Assert.IsTrue(r.sharedMaterials.All(m => m != null && m.color != Color.magenta), id + " materials");
                }
                Assert.AreEqual(1f, bounds.size.y, 0.02f, id + " is scaled to one unit");
                Assert.AreEqual(0f, bounds.center.y, 0.02f, id + " pivots on its middle");
                Assert.GreaterOrEqual(renderers.Sum(r => r.sharedMaterials.Length), 5, id + " has its colours");
                Object.Destroy(model.gameObject);
            }
            Assert.IsNull(SlugModels.Create("no_such_slug", _stage.transform));
        }

        [Test]
        public void EverySlugHasAPortraitForTheLobbyThatShowsTheSlugOnAClearBackground()
        {
            foreach (string id in SlugIds)
            {
                Texture2D portrait = SlugPortraits.Get(id);
                Assert.IsNotNull(portrait, id);
                Color32[] pixels = portrait.GetPixels32();
                int solid = pixels.Count(p => p.a > 200);
                Assert.Greater(solid, pixels.Length / 8, id + " fills a good part of the picture");
                Assert.Less(solid, pixels.Length * 3 / 4, id + " stands on a clear background");
                Assert.AreSame(portrait, SlugPortraits.Get(id), "made once");
            }
            Assert.IsNull(SlugPortraits.Get("no_such_slug"));
        }

        [Test]
        public void AFiredSlugFliesAsItsModelLeaningHeadFirstIntoItsPath()
        {
            var views = new ProjectileViews(_stage.transform, Catalog(), Arena(false), _audio);

            views.Apply(Projectile(0, false, 0), 1f);

            Assert.AreEqual(1, views.ActiveCount);
            Assert.IsTrue(views.AllModelled);
            Transform model = Find("Slug model infurnus");
            Assert.IsTrue(model.gameObject.activeInHierarchy);
            Assert.IsFalse(Find("Ball").gameObject.activeSelf, "no round stand-in");
            Assert.Greater(Vector3.Dot(model.up, Vector3.right), 0.6f, "its head leads the way (+x)");
            views.Destroy();
        }

        [Test]
        public void AStuckPhosphoroGlowsBrighterAndHumsHigherUntilItFlashesAndMarksNoBlast()
        {
            var views = new ProjectileViews(_stage.transform, Catalog(), Arena(false), _audio);
            views.Apply(Projectile(1, false, 0), 1f);
            Light light = _stage.GetComponentsInChildren<Light>(true).First(l => l.name == "Light");
            AudioSource hum = _stage.GetComponentsInChildren<AudioSource>(true).First(s => s.name == "Loop");
            Assert.AreSame(_audio.FlashCharge, hum.clip, "it hums, it does not hiss like a fuse");
            Assert.IsTrue(Find("Flare").gameObject.activeSelf, "a star of light glints on it");
            float flying = light.intensity;

            views.Apply(Projectile(1, true, FlashFuse), 1.1f);
            float early = hum.pitch;
            float earlyRange = light.range;
            views.Apply(Projectile(1, true, 4), 2.5f);

            Assert.Greater(hum.pitch, early + 0.8f, "the hum rises to a whine");
            Assert.Greater(light.range, earlyRange + 5f, "its light reaches further and further");
            Assert.Greater(Mathf.Max(light.intensity, flying), 2.5f);
            Assert.IsFalse(Find("Blast ring").gameObject.activeSelf, "a flash blinds, it has no blast circle");
            Assert.IsTrue(Find("Slug model phosphoro").gameObject.activeInHierarchy);
            views.Destroy();
        }

        // ------------------------------------------------------------------------------------------ ice and fire

        private static Snapshot Walls(uint hitsLeft, uint ticksLeft)
        {
            var snapshot = new Snapshot();
            snapshot.Barriers.Add(new BarrierState
            {
                Id = 4, MinX = 1.75f, MinY = 0f, MinZ = -1.6f, MaxX = 2.25f, MaxY = 2.2f, MaxZ = 1.6f, HitsLeft = hitsLeft,
                TicksLeft = ticksLeft, LifeTicks = 480
            });
            return snapshot;
        }

        [Test]
        public void AnIceWallStandsWhereTheServerSaysCracksAfterAHitAndGoesWithIt()
        {
            var walls = new BarrierViews(_stage.transform);
            walls.Apply(Walls(2, 400), 0.5f);
            Transform wall = Find("Ice wall");

            Assert.AreEqual(1, walls.Count);
            Assert.AreEqual(2f, wall.position.x, 1e-4f);
            Assert.AreEqual(90f, wall.eulerAngles.y, 0.5f, "turned to run along z");
            Assert.IsFalse(Find("Cracks").gameObject.activeSelf);

            walls.Apply(Walls(1, 380), 0.1f);
            Assert.IsTrue(Find("Cracks").gameObject.activeSelf, "cracked by the first hit");

            walls.Apply(new Snapshot(), 0.1f);
            Assert.AreEqual(0, walls.Count);
            Assert.IsFalse(wall.gameObject.activeSelf);
            walls.Destroy();
        }

        private static Snapshot Fire(int patches, uint ticksLeft)
        {
            var snapshot = new Snapshot();
            for (int i = 0; i < patches; i++)
            {
                snapshot.Hazards.Add(new HazardState
                {
                    Id = (uint)(10 + i), SlugIndex = 0, Owner = 1, X = -4f + i * 0.9f, Y = 0f, Z = 0f, TicksLeft = ticksLeft
                });
            }
            return snapshot;
        }

        private static ContentCatalog FireCatalog()
        {
            ContentCatalog catalog = Catalog();
            SlugInfo infurnus = catalog.Slug(0);
            infurnus.TrailRadius = 0.6f;
            infurnus.TrailHeight = 1.4f;
            infurnus.TrailTicks = 210;
            return catalog;
        }

        [Test]
        public void AWallOfFireBurnsPatchByPatchAndGoesOut()
        {
            var fire = new FireTrailViews(_stage.transform, FireCatalog(), _audio);
            fire.Apply(Fire(9, 200), 0.05f, new Vector3(0f, 2f, -6f));
            ParticleSystem flames = _stage.GetComponentsInChildren<ParticleSystem>().First(p => p.name == "Flames");

            Assert.AreEqual(9, fire.Count);
            Assert.Greater(flames.particleCount, 5, "flames lick up from the patches");
            Assert.IsTrue(_stage.GetComponentsInChildren<Light>().Any(l => l.name.StartsWith("Fire light") && l.enabled));

            fire.Apply(new Snapshot(), 0.05f, Vector3.zero);
            Assert.AreEqual(0, fire.Count);
            fire.Destroy();
        }

        // ------------------------------------------------------------------------------------------ the water

        [Test]
        public void AnArenaWithoutWaterHasNoPool()
        {
            var water = new WaterView(_stage.transform, Arena(false), Catalog(), _audio);
            water.Apply(Walker(0f), 0.02f);
            Assert.AreEqual(0, water.PoolCount);
            water.Destroy();
        }

        [Test]
        public void WadingThroughThePoolMakesRipplesAndWalkingPastItDoesNot()
        {
            var water = new WaterView(_stage.transform, Arena(true), Catalog(), _audio);
            Assert.AreEqual(1, water.PoolCount);
            for (int i = 0; i < 10; i++)
            {
                water.Apply(Walker(-6f + i * 0.1f), 0.02f);
            }
            Assert.AreEqual(0f, water.Motion(0), "nobody in the water yet");

            for (int i = 0; i < 12; i++)
            {
                water.Apply(Walker(-1.2f + i * 0.2f), 0.033f);
            }

            Assert.Greater(water.Motion(0), 1e-4f, "the wake and the steps ripple the water");
            water.Destroy();
        }

        [Test]
        public void ASlugLandingInThePoolSplashesIt()
        {
            var water = new WaterView(_stage.transform, Arena(true), Catalog(), _audio);
            water.Apply(new WorldFrame { Valid = true }, 0.02f);
            float before = water.Motion(0);

            water.Handle(new GameEvent { Kind = EventKind.ProjectileWorldHit, A = 0, X = 0.4f, Y = 0.25f, Z = -0.3f });
            water.Apply(new WorldFrame { Valid = true }, 0.02f);

            Assert.Greater(water.Motion(0), before + 1e-3f);
            water.Destroy();
        }

        [Test]
        public void TheFlashAndSplashSoundsAreRealAudibleClips()
        {
            foreach (AudioClip clip in new[] { _audio.FlashCharge, _audio.FlashBang, _audio.Tinnitus, _audio.Splash,
                         _audio.Splish, _audio.Plop })
            {
                var data = new float[clip.samples];
                Assert.IsTrue(clip.GetData(data, 0), clip.name);
                Assert.IsFalse(data.Any(float.IsNaN), clip.name);
                Assert.Greater(data.Max(Mathf.Abs), 0.1f, clip.name + " is audible");
            }
        }

        // ------------------------------------------------------------------------------------------ pictures

        [UnityTest]
        public IEnumerator Pictures_of_the_slugs_a_charging_phosphoro_and_a_splash()
        {
            string dir = Environment.GetEnvironmentVariable("BAJOTERRA_SCREENSHOTS");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Ignore("set BAJOTERRA_SCREENSHOTS to write the pictures");
            }
            Directory.CreateDirectory(dir);
            RenderSettings.ambientLight = new Color(0.45f, 0.52f, 0.55f);
            RenderSettings.fog = false;
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.SetParent(_stage.transform, false);
            floor.transform.localScale = Vector3.one * 3f;
            floor.GetComponent<Renderer>().sharedMaterial =
                MaterialFactory.Surface(Textures.Floor, new Color(0.55f, 0.65f, 0.65f), 0.2f, 0f);
            var sun = new GameObject("Key light", typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(_stage.transform, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(_stage.transform, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.12f, 0.14f);

            // 1. The sixteen slugs in two rows, facing the camera.
            for (int i = 0; i < SlugIds.Length; i++)
            {
                Transform model = SlugModels.Create(SlugIds[i], _stage.transform);
                int row = i < 8 ? 0 : 1;
                int column = row == 0 ? i : i - 8;
                int count = 8;
                model.position = new Vector3((column - (count - 1) / 2f) * 1.05f, 0.5f, row * 1.4f);
                model.rotation = Quaternion.Euler(0f, 180f, 0f);
                model.localScale = Vector3.one * 0.95f;
            }
            camera.fieldOfView = 32f;
            camera.transform.position = new Vector3(0f, 2.6f, -12.5f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.75f, 0.7f) - camera.transform.position);
            yield return null;
            Shoot(camera, Path.Combine(dir, "slugs-gallery.png"));
            foreach (Transform child in _stage.transform)
            {
                if (child.name.StartsWith("Slug model", StringComparison.Ordinal))
                {
                    child.gameObject.SetActive(false);
                }
            }

            // 2. A Phosphoro stuck on the floor, close to going off, next to a pool somebody just splashed.
            sun.intensity = 0.35f;
            var views = new ProjectileViews(_stage.transform, Catalog(), Arena(true), _audio);
            var water = new WaterView(_stage.transform, Arena(true), Catalog(), _audio);
            views.Apply(Projectile(1, false, 0, 2.8f), 1f);
            views.Apply(Projectile(1, true, FlashFuse, 2.8f), 1.05f);
            for (int i = 0; i < 14; i++)
            {
                water.Apply(Walker(-1.4f + i * 0.2f), 0.033f);
                views.Apply(Projectile(1, true, FlashFuse - 6 * i, 2.8f), 1.1f + i * 0.033f);
                yield return null;
            }
            water.Handle(new GameEvent { Kind = EventKind.ProjectileWorldHit, A = 0, X = -0.5f, Y = 0.25f, Z = 0.4f });
            water.Apply(new WorldFrame { Valid = true }, 0.05f);
            views.Apply(Projectile(1, true, 8, 2.8f), 1.6f);
            yield return null;
            camera.fieldOfView = 45f;
            camera.transform.position = new Vector3(0.6f, 3.4f, -5.2f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0.8f, 0.1f, 0.2f) - camera.transform.position);
            yield return null;
            Shoot(camera, Path.Combine(dir, "phosphoro-and-water.png"));
            views.Destroy();
            water.Destroy();

            // 3. An ice wall, cracked, behind a wall of fire.
            var walls = new BarrierViews(_stage.transform);
            var fire = new FireTrailViews(_stage.transform, FireCatalog(), _audio);
            walls.Apply(Walls(1, 400), 0.5f);
            for (int i = 0; i < 30; i++)
            {
                fire.Apply(Fire(9, 200), 0.033f, camera.transform.position);
                yield return null;
            }
            camera.transform.position = new Vector3(-7f, 3f, -6.5f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0.5f, 0.9f, 0f) - camera.transform.position);
            yield return null;
            Shoot(camera, Path.Combine(dir, "ice-wall-and-fire.png"));
            walls.Destroy();
            fire.Destroy();
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
