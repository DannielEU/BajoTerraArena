using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Bajoterra.Unity.Tests
{
    /// <summary>
    /// The Blender heroes: every one loads (not the primitive stand-in), is sized like its hitbox, crouches and rolls
    /// with real clips. With <c>BAJOTERRA_SCREENSHOTS</c> set, the gallery test also writes pictures of all four in
    /// each pose, which is how the art is judged inside the engine.
    /// </summary>
    public sealed class CharacterModelTests
    {
        private static readonly string[] Ids = { "eli", "trixie", "kord", "pronto" };
        private static readonly float[] Heights = { 1.8f, 1.7f, 2.35f, 1.15f };
        private static readonly float[] Radii = { 0.4f, 0.4f, 0.65f, 0.3f };

        private GameObject _stage;

        [SetUp]
        public void Setup()
        {
            _stage = new GameObject("Model stage");
        }

        [TearDown]
        public void Teardown()
        {
            Object.Destroy(_stage);
        }

        private ICharacterModel Make(int hero, Vector3 at, float yaw = 0f)
        {
            var root = new GameObject(Ids[hero]).transform;
            root.SetParent(_stage.transform, false);
            root.position = at;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            ICharacterModel model = CharacterModels.Create(root, Ids[hero], Radii[hero], Heights[hero], new Color(0.2f, 0.6f, 1f));
            return model;
        }

        private static Transform Bone(ICharacterModel model, string name)
        {
            foreach (Transform t in model.Body.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }
            Assert.Fail("no bone " + name);
            return null;
        }

        private static CharacterPose Standing(int hero, float dt = 1f / 60f)
        {
            return new CharacterPose { DeltaTime = dt, Height = Heights[hero], SlugColor = Color.cyan };
        }

        private static void Run(ICharacterModel model, CharacterPose pose, float seconds)
        {
            int steps = Mathf.CeilToInt(seconds * 60f);
            pose.DeltaTime = 1f / 60f;
            for (int i = 0; i < steps; i++)
            {
                model.Pose(in pose);
            }
        }

        [Test]
        public void Every_hero_has_a_blender_model_and_not_the_stand_in()
        {
            for (int i = 0; i < Ids.Length; i++)
            {
                ICharacterModel model = Make(i, Vector3.zero);
                Assert.IsInstanceOf<RiggedCharacterModel>(model, Ids[i]);
                model.Destroy();
            }
        }

        [Test]
        public void The_models_are_scaled_like_their_hitboxes_and_Kord_towers_over_Pronto()
        {
            var headHeights = new float[Ids.Length];
            for (int i = 0; i < Ids.Length; i++)
            {
                ICharacterModel model = Make(i, Vector3.zero);
                Run(model, Standing(i), 1f);
                headHeights[i] = Bone(model, "Head").position.y;
                Assert.That(headHeights[i], Is.InRange(Heights[i] * 0.7f, Heights[i] * 0.97f), Ids[i] + " head pivot height");
                model.Destroy();
            }
            Assert.Greater(headHeights[2], headHeights[0], "Kord is taller than Eli");
            Assert.Greater(headHeights[0], headHeights[1], "Eli is taller than Trixie");
            Assert.Greater(headHeights[1], headHeights[3] * 1.4f, "Pronto is much smaller");
        }

        [Test]
        public void Crouching_lowers_every_hero_to_about_the_crouch_hitbox()
        {
            for (int i = 0; i < Ids.Length; i++)
            {
                ICharacterModel model = Make(i, Vector3.zero);
                Run(model, Standing(i), 0.5f);
                float standing = Bone(model, "Head").position.y;
                CharacterPose crouch = Standing(i);
                crouch.Height = Heights[i] * 0.6f;
                Run(model, crouch, 1f);
                float crouched = Bone(model, "Head").position.y;
                Assert.Less(crouched, standing * 0.72f, Ids[i] + " should drop clearly");
                Assert.Less(crouched, Heights[i] * 0.6f, Ids[i] + " head pivot is below the crouch hitbox top");
                Run(model, Standing(i), 1f);
                Assert.AreEqual(standing, Bone(model, "Head").position.y, standing * 0.03f, Ids[i] + " stands up again");
                model.Destroy();
            }
        }

        [Test]
        public void Rolling_turns_the_body_over_and_ends_upright()
        {
            for (int i = 0; i < Ids.Length; i++)
            {
                ICharacterModel model = Make(i, Vector3.zero);
                Assert.IsTrue(model.HandlesRoll);
                Run(model, Standing(i), 0.3f);
                CharacterPose roll = Standing(i);
                roll.Height = Heights[i] * 0.6f;
                roll.Rolling = true;
                roll.Velocity = new Vector2(0f, 8f);
                float lowestUp = 1f;
                float highest = 0f;
                for (int step = 0; step < 30; step++)
                {
                    Run(model, roll, 1f / 60f);
                    lowestUp = Mathf.Min(lowestUp, Vector3.Dot(Bone(model, "Head").up, Vector3.up));
                    highest = Mathf.Max(highest, Bone(model, "Head").position.y);
                }
                Assert.Less(lowestUp, -0.3f, Ids[i] + " goes (nearly) upside down during the roll");
                Assert.Less(highest, Heights[i] * 1.05f, Ids[i] + " never rises above its standing height");
                roll.Rolling = false;
                roll.Height = Heights[i];
                roll.Velocity = Vector2.zero;
                Run(model, roll, 1.5f);
                Assert.Greater(Vector3.Dot(Bone(model, "Head").up, Vector3.up), 0.8f, Ids[i] + " ends upright");
                model.Destroy();
            }
        }

        [Test]
        public void Aiming_up_raises_the_muzzle_and_aiming_down_lowers_it()
        {
            ICharacterModel model = Make(0, Vector3.zero);
            CharacterPose pose = Standing(0);
            Run(model, pose, 0.5f);
            float level = Bone(model, "Muzzle").position.y;
            pose.Pitch = 0.6f;
            Run(model, pose, 0.1f);
            float up = Bone(model, "Muzzle").position.y;
            pose.Pitch = -0.6f;
            Run(model, pose, 0.1f);
            float down = Bone(model, "Muzzle").position.y;
            Assert.Greater(up, level + 0.15f);
            Assert.Less(down, level - 0.15f);
            model.Destroy();
        }

        [Test]
        public void Running_in_each_direction_moves_the_legs_and_defeat_lies_the_arms_down()
        {
            ICharacterModel model = Make(0, Vector3.zero);
            Transform foot = Bone(model, "Foot.L");
            CharacterPose pose = Standing(0);
            pose.Velocity = new Vector2(0f, 6f);
            Run(model, pose, 0.3f);
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 40; i++)
            {
                Run(model, pose, 1f / 60f);
                min = Mathf.Min(min, foot.position.z);
                max = Mathf.Max(max, foot.position.z);
            }
            Assert.Greater(max - min, 0.3f, "the stride swings the foot forwards and back");
            float handBefore = Bone(model, "Hand.R").position.y;
            pose.Velocity = Vector2.zero;
            pose.Fall = 1f;
            Run(model, pose, 1f);
            Assert.Less(Bone(model, "Hand.R").position.y, handBefore - 0.2f, "defeated arms drop");
            model.Destroy();
        }

        [Test]
        public void Every_hero_dances_the_three_celebration_dances_and_stops_when_told()
        {
            for (int hero = 0; hero < Ids.Length; hero++)
            {
                ICharacterModel model = Make(hero, new Vector3(hero * 3f, 0f, 0f));
                Transform hand = Bone(model, "Hand.R");
                Transform neck = Bone(model, "Neck");
                Transform muzzle = Bone(model, "Muzzle");
                Run(model, Standing(hero), 0.5f);
                float handStill = hand.position.y;
                for (int dance = 1; dance <= 3; dance++)
                {
                    CharacterPose pose = Standing(hero);
                    pose.Dance = dance;
                    Run(model, pose, 0.6f);
                    float highest = float.MinValue;
                    var reach = new Bounds(hand.position, Vector3.zero);
                    for (int i = 0; i < 150; i++)
                    {
                        Run(model, pose, 1f / 60f);
                        highest = Mathf.Max(highest, muzzle.position.y);
                        reach.Encapsulate(hand.position);
                    }
                    Assert.Greater(reach.size.magnitude, 0.07f * Heights[hero], Ids[hero] + " moves the blaster in dance " + dance);
                    if (dance != 2)
                    {
                        Assert.Greater(highest, neck.position.y, Ids[hero] + " raises the blaster in dance " + dance);
                    }
                }
                Run(model, Standing(hero), 1f);
                Assert.AreEqual(handStill, hand.position.y, 0.05f, Ids[hero] + " goes back to holding the blaster");
                model.Destroy();
            }
        }

        // ------------------------------------------------------------------------------------------ gallery

        [UnityTest]
        public IEnumerator Gallery_pictures_of_every_hero_in_every_pose()
        {
            string dir = Environment.GetEnvironmentVariable("BAJOTERRA_SCREENSHOTS");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Ignore("set BAJOTERRA_SCREENSHOTS to write the pictures");
            }
            yield return null;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.47f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.25f, 0.28f);
            RenderSettings.fog = false;
            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(_stage.transform, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.SetParent(_stage.transform, false);
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            floor.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Surface(null, new Color(0.34f, 0.36f, 0.4f), 0.1f, 0f);
            var camera = new GameObject("Gallery camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(_stage.transform, false);
            camera.fieldOfView = 34f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.52f, 0.58f, 0.68f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;

            float[] xs = { -3.3f, -1.1f, 1.6f, 3.9f };
            var models = new ICharacterModel[Ids.Length];
            for (int i = 0; i < Ids.Length; i++)
            {
                models[i] = Make(i, new Vector3(xs[i], 0f, 0f), 0f);
                Run(models[i], Standing(i), 1f);
            }

            // Behind them, the way the game camera sees them.
            yield return Shoot(camera, dir, "gallery-1-stand-back", new Vector3(0.3f, 1.7f, -9.5f), new Vector3(0.3f, 1.05f, 0f));
            // Three-quarter front, to read the faces and the blasters (the models face +Z).
            for (int i = 0; i < Ids.Length; i++)
            {
                models[i].Body.parent.rotation = Quaternion.Euler(0f, -25f, 0f);
            }
            yield return Shoot(camera, dir, "gallery-2-stand-front", new Vector3(0.3f, 1.5f, 9.5f), new Vector3(0.3f, 1.05f, 0f));
            // A portrait of each one.
            float[] heads = { 1.6f, 1.5f, 2.02f, 0.96f };
            for (int i = 0; i < Ids.Length; i++)
            {
                float reach = Heights[i] * 0.42f;
                yield return Shoot(camera, dir, "gallery-0-portrait-" + Ids[i], new Vector3(xs[i] - 0.15f * reach, heads[i] - 0.05f, reach * 1.9f),
                    new Vector3(xs[i], heads[i] - 0.08f * reach, 0f));
            }
            for (int i = 0; i < Ids.Length; i++)
            {
                models[i].Body.parent.rotation = Quaternion.Euler(0f, 90f, 0f);
            }
            yield return Shoot(camera, dir, "gallery-3-stand-side", new Vector3(0.3f, 1.3f, -10f), new Vector3(0.3f, 1.0f, 0f));

            // Crouch (hitbox at 60 %).
            for (int i = 0; i < Ids.Length; i++)
            {
                CharacterPose crouch = Standing(i);
                crouch.Height = Heights[i] * 0.6f;
                Run(models[i], crouch, 1f);
            }
            yield return Shoot(camera, dir, "gallery-4-crouch-side", new Vector3(0.3f, 1.3f, -10f), new Vector3(0.3f, 0.9f, 0f));
            for (int i = 0; i < Ids.Length; i++)
            {
                models[i].Body.parent.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
            yield return Shoot(camera, dir, "gallery-5-crouch-back", new Vector3(0.3f, 1.6f, -9.5f), new Vector3(0.3f, 0.9f, 0f));

            // Crouch-walk, then run forward.
            for (int i = 0; i < Ids.Length; i++)
            {
                models[i].Body.parent.rotation = Quaternion.Euler(0f, 90f, 0f);
                CharacterPose walk = Standing(i);
                walk.Height = Heights[i] * 0.6f;
                walk.Velocity = new Vector2(0f, 3f);
                Run(models[i], walk, 0.9f);
            }
            yield return Shoot(camera, dir, "gallery-6-crouch-walk", new Vector3(0.3f, 1.3f, -10f), new Vector3(0.3f, 0.9f, 0f));
            for (int i = 0; i < Ids.Length; i++)
            {
                CharacterPose run = Standing(i);
                run.Velocity = new Vector2(0f, 6f);
                Run(models[i], run, 1f);
            }
            yield return Shoot(camera, dir, "gallery-7-run", new Vector3(0.3f, 1.3f, -10f), new Vector3(0.3f, 1.0f, 0f));

            // The roll, one picture every 0.08 s.
            for (int i = 0; i < Ids.Length; i++)
            {
                Run(models[i], Standing(i), 1f);
            }
            var rolling = new CharacterPose[Ids.Length];
            for (int i = 0; i < Ids.Length; i++)
            {
                rolling[i] = Standing(i);
                rolling[i].Height = Heights[i] * 0.6f;
                rolling[i].Rolling = true;
                rolling[i].Velocity = new Vector2(0f, 8f);
            }
            for (int shot = 0; shot < 6; shot++)
            {
                for (int i = 0; i < Ids.Length; i++)
                {
                    Run(models[i], rolling[i], shot == 0 ? 0.04f : 0.08f);
                }
                yield return Shoot(camera, dir, "gallery-8-roll-" + shot, new Vector3(0.3f, 1.3f, -10f), new Vector3(0.3f, 0.9f, 0f));
            }

            // The three celebration dances, from the front.
            for (int i = 0; i < Ids.Length; i++)
            {
                models[i].Body.parent.rotation = Quaternion.Euler(0f, -20f, 0f);
            }
            for (int dance = 1; dance <= 3; dance++)
            {
                for (int i = 0; i < Ids.Length; i++)
                {
                    CharacterPose party = Standing(i);
                    party.Dance = dance;
                    Run(models[i], party, dance == 3 ? 1.6f : 0.62f);
                }
                yield return Shoot(camera, dir, "gallery-9-dance-" + dance, new Vector3(0.3f, 1.5f, 9.5f), new Vector3(0.3f, 1.1f, 0f));
            }
            for (int i = 0; i < Ids.Length; i++)
            {
                models[i].Body.parent.rotation = Quaternion.Euler(0f, 90f, 0f);
                Run(models[i], Standing(i), 1f);
            }

            // Down.
            for (int i = 0; i < Ids.Length; i++)
            {
                models[i].SetDead(true);
                CharacterPose down = Standing(i);
                down.Fall = 1f;
                Run(models[i], down, 1f);
            }
            yield return Shoot(camera, dir, "gallery-9-defeated", new Vector3(0.3f, 1.3f, -10f), new Vector3(0.3f, 0.9f, 0f));
            foreach (ICharacterModel model in models)
            {
                model.Destroy();
            }
        }

        /// <summary>Photographs the scene. Skinned meshes are only skinned once per frame by the engine, so real frames
        /// must pass between posing the models and taking the picture.</summary>
        private static IEnumerator Shoot(Camera camera, string dir, string name, Vector3 position, Vector3 target)
        {
            yield return null;
            yield return null;
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(target - position, Vector3.up);
            var rt = new RenderTexture(1600, 800, 24, RenderTextureFormat.ARGB32);
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
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
