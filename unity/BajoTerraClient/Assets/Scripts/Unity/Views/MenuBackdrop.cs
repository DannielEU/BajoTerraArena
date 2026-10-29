using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The scene behind the menus: a lit corner of the cavern with the four heroes lined up on a wooden stage, glowing
    /// crystals and giant glowing mushrooms, drifting spores and a camera that sways slowly around them. It is built from the same procedural
    /// pieces as the arena, needs no server, and is thrown away when a match starts.
    /// </summary>
    public sealed class MenuBackdrop
    {
        private sealed class Hero
        {
            public ICharacterModel Model;
            public Transform Root;
            public float Height;
            public float Phase;
            public float Yaw;
        }

        private static readonly string[] Ids = { "trixie", "eli", "kord", "pronto" };
        private static readonly float[] Heights = { 1.7f, 1.8f, 2.35f, 1.15f };
        private static readonly float[] Radii = { 0.4f, 0.4f, 0.65f, 0.3f };
        private static readonly Color[] Crystals =
        {
            new Color(0.2f, 0.95f, 0.85f), new Color(0.3f, 0.7f, 1f), new Color(0.45f, 1f, 0.4f), new Color(0.25f, 1f, 0.7f)
        };

        private readonly Transform _root;
        private readonly Camera _camera;
        private readonly Hero[] _heroes = new Hero[Ids.Length];
        private readonly Light[] _lights = new Light[4];
        private float _time;

        public MenuBackdrop(Transform parent, Camera camera)
        {
            _camera = camera;
            _root = new GameObject("Menu backdrop").transform;
            _root.SetParent(parent, false);
            // Far from the arena's origin so the two never overlap even for a frame.
            _root.position = new Vector3(0f, 0f, -400f);

            BuildStage();
            BuildCave();
            BuildHeroes();
            BuildDust();
        }

        /// <summary>Sways the camera and idles the heroes; call once per frame while the backdrop is shown.</summary>
        public void Tick(float deltaTime)
        {
            _time += deltaTime;
            Vector3 origin = _root.position;
            // A slow arc in front of the stage, looking at the middle of the line-up.
            float swing = Mathf.Sin(_time * 0.18f) * 0.38f;
            Vector3 target = origin + new Vector3(-0.2f, 1.25f, 0f);
            Vector3 position = origin + new Vector3(Mathf.Sin(swing) * 9f + 0.1f, 2.2f + Mathf.Sin(_time * 0.23f) * 0.25f,
                -Mathf.Cos(swing) * 9f);
            Transform t = _camera.transform;
            t.position = position;
            t.rotation = Quaternion.LookRotation(target - position, Vector3.up);

            for (int i = 0; i < _heroes.Length; i++)
            {
                Hero hero = _heroes[i];
                float breathe = Mathf.Sin(_time * 1.6f + hero.Phase) * 0.012f;
                hero.Root.localPosition = new Vector3(hero.Root.localPosition.x, 0.3f + breathe, hero.Root.localPosition.z);
                // Each hero glances slowly towards the camera and away again.
                float glance = Mathf.Sin(_time * 0.5f + hero.Phase) * 12f;
                hero.Root.localRotation = Quaternion.Euler(0f, hero.Yaw + glance, 0f);
                var pose = new CharacterPose
                {
                    DeltaTime = deltaTime,
                    Height = hero.Height,
                    Pitch = 0.12f + Mathf.Sin(_time * 0.7f + hero.Phase) * 0.04f,
                    SlugColor = Crystals[i % Crystals.Length]
                };
                hero.Model.Pose(in pose);
            }
            for (int i = 0; i < _lights.Length; i++)
            {
                _lights[i].intensity = 1.5f * (0.85f + 0.15f * Mathf.Sin(_time * 0.9f + i * 1.9f));
            }
        }

        public void Destroy()
        {
            foreach (Hero hero in _heroes)
            {
                if (hero != null)
                {
                    hero.Model.Destroy();
                }
            }
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }
        }

        // ------------------------------------------------------------------------------------------ pieces

        private void BuildStage()
        {
            Material floor = MaterialFactory.Surface(Textures.Floor, Color.white, 0.35f, 0f);
            Mesh ground = MeshKit.Box(new Vector3(60f, 1f, 40f), Textures.FloorTile, Textures.FloorTile, 3);
            GameObject go = Gfx.Make("Ground", ground, _root, floor, floor);
            go.transform.localPosition = new Vector3(0f, -0.5f, 8f);

            Material stone = MaterialFactory.Surface(Textures.Bricks, Color.white, 0.2f, 0f);
            Material planks = MaterialFactory.Surface(Textures.Planks, Color.white, 0.25f, 0f);
            Mesh stage = MeshKit.Box(new Vector3(12.5f, 0.3f, 4.2f), Textures.BrickTile, Textures.PlankTile, 5);
            GameObject deck = Gfx.Make("Stage", stage, _root, stone, planks);
            deck.transform.localPosition = new Vector3(3.2f, 0.15f, 0f);
            Material glow = MaterialFactory.Emissive(new Color(0.2f, 0.95f, 0.82f), 1.8f);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject strip = Gfx.Primitive(PrimitiveType.Cube, "Stage trim", _root, glow);
                strip.transform.localPosition = new Vector3(3.2f, 0.31f, side * 2.05f);
                strip.transform.localScale = new Vector3(12.5f, 0.05f, 0.08f);
                Gfx.NoShadows(strip);
            }
        }

        private void BuildCave()
        {
            Material rock = MaterialFactory.Surface(Textures.Rock, Color.white, 0.2f, 0f);
            Mesh back = MeshKit.Box(new Vector3(70f, 18f, 4f), Textures.RockTile, Textures.RockTile, 7);
            GameObject wall = Gfx.Make("Back wall", back, _root, rock, rock);
            wall.transform.localPosition = new Vector3(0f, 9f, 11f);
            Mesh left = MeshKit.Box(new Vector3(4f, 18f, 40f), Textures.RockTile, Textures.RockTile, 8);
            GameObject sideWall = Gfx.Make("Left wall", left, _root, rock, rock);
            sideWall.transform.localPosition = new Vector3(-16f, 9f, 0f);
            GameObject sideWall2 = Gfx.Make("Right wall", left, _root, rock, rock);
            sideWall2.transform.localPosition = new Vector3(24f, 9f, 0f);
            Mesh ceiling = MeshKit.Box(new Vector3(70f, 2f, 50f), Textures.RockTile, Textures.RockTile, 9);
            GameObject top = Gfx.Make("Ceiling", ceiling, _root, rock, rock);
            top.transform.localPosition = new Vector3(0f, 14f, 4f);
            Gfx.NoShadows(top);

            // Crystal clusters on the back wall, each with its own light.
            var random = new System.Random(11);
            float[] xs = { -4f, 3f, 9f, 14f };
            for (int i = 0; i < xs.Length; i++)
            {
                Color color = Crystals[i % Crystals.Length];
                Material glow = MaterialFactory.Emissive(color, 2.4f, 0.8f);
                for (int c = 0; c < 4; c++)
                {
                    float radius = 0.18f + (float)random.NextDouble() * 0.2f;
                    float height = 1.2f + (float)random.NextDouble() * 2.4f;
                    Mesh mesh = MeshKit.Crystal(radius, height, 6, new Vector2((float)(random.NextDouble() - 0.5), 0f));
                    GameObject crystal = Gfx.Make("Crystal", mesh, _root, glow);
                    crystal.transform.localPosition = new Vector3(xs[i] + (float)(random.NextDouble() - 0.5) * 2f, 0f, 8.7f - (float)random.NextDouble());
                    crystal.transform.localRotation = Quaternion.Euler((float)(random.NextDouble() - 0.5) * 20f,
                        (float)random.NextDouble() * 360f, (float)(random.NextDouble() - 0.5) * 20f);
                    Gfx.NoShadows(crystal);
                }
                Vector3 at = _root.position + new Vector3(xs[i], 1.2f, 7.4f);
                _lights[i] = Gfx.AddPointLight(_root, "Crystal light", at, color, 15f, 1.5f);
                Gfx.Halo(_root, "Crystal glow", at + Vector3.up * 0.3f, new Color(color.r, color.g, color.b, 0.55f), 5f);
            }
            // Glowing mushrooms: clumps along the back wall and two giants framing the stage.
            float[] clumps = { -1f, 6f, 11.5f };
            for (int i = 0; i < clumps.Length; i++)
            {
                Color color = Mushrooms.Colors[i % Mushrooms.Colors.Length];
                Mushrooms.Clump(_root, _root.position + new Vector3(clumps[i], 0f, 8.2f), Vector3.back,
                    2f + (float)random.NextDouble() * 1.4f, color, random);
                Gfx.Halo(_root, "Mushroom glow", _root.position + new Vector3(clumps[i], 2.2f, 7.6f),
                    new Color(color.r, color.g, color.b, 0.45f), 5f);
            }
            Mushrooms.One(_root, _root.position + new Vector3(10.5f, 0f, 4.5f), 5.6f, Mushrooms.Colors[0], Vector3.left,
                9f, random);
            Mushrooms.One(_root, _root.position + new Vector3(-3.8f, 0f, 5.5f), 4.4f, Mushrooms.Colors[1], Vector3.right,
                7f, random);
            Gfx.Halo(_root, "Giant glow", _root.position + new Vector3(10.2f, 5.2f, 4.5f), new Color(0.2f, 0.95f, 0.85f, 0.5f), 8f);
            Gfx.Halo(_root, "Giant glow", _root.position + new Vector3(-3.6f, 4.1f, 5.5f), new Color(0.3f, 0.65f, 1f, 0.5f), 7f);

            // Stalactites.
            for (int i = 0; i < 18; i++)
            {
                float length = 1.5f + (float)random.NextDouble() * 4f;
                Mesh cone = MeshKit.Cone(0.4f + (float)random.NextDouble() * 0.8f, length, 7, Textures.RockTile);
                GameObject spike = Gfx.Make("Stalactite", cone, _root, rock);
                spike.transform.localPosition = new Vector3(Mathf.Lerp(-12f, 20f, (float)random.NextDouble()), 13f,
                    Mathf.Lerp(-8f, 9f, (float)random.NextDouble()));
                spike.transform.localRotation = Quaternion.Euler(180f, (float)random.NextDouble() * 360f, 0f);
                Gfx.NoShadows(spike);
            }
            // Boulders on the floor.
            for (int i = 0; i < 16; i++)
            {
                float radius = 0.6f + (float)random.NextDouble() * 1.3f;
                GameObject boulder = Gfx.Make("Boulder", MeshKit.Rock(radius, i + 3, Textures.RockTile), _root, rock);
                boulder.transform.localPosition = new Vector3(Mathf.Lerp(-12f, 20f, (float)random.NextDouble()), 0f,
                    7f + (float)random.NextDouble() * 1.4f);
                boulder.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            }
        }

        private void BuildHeroes()
        {
            for (int i = 0; i < Ids.Length; i++)
            {
                var hero = new Hero { Height = Heights[i], Phase = i * 1.7f, Yaw = 180f + (i - 1.5f) * 11f };
                hero.Root = new GameObject(Ids[i]).transform;
                hero.Root.SetParent(_root, false);
                hero.Root.localPosition = new Vector3(i * 2.05f, 0.3f, 0f);
                hero.Root.localRotation = Quaternion.Euler(0f, hero.Yaw, 0f);
                var spin = new GameObject("Spin").transform;
                spin.SetParent(hero.Root, false);
                spin.localPosition = new Vector3(0f, Heights[i] * 0.5f, 0f);
                Color team = i % 2 == 0 ? Palette.TeamOwn : new Color(1f, 0.75f, 0.3f);
                hero.Model = CharacterModels.Create(spin, Ids[i], Radii[i], Heights[i], team);
                hero.Model.Body.localPosition = new Vector3(0f, -Heights[i] * 0.5f, 0f);
                _heroes[i] = hero;

                // A soft pool of light under each hero.
                GameObject ring = Gfx.Make("Ring", Billboard.QuadMesh(), hero.Root, MaterialFactory.GlowInstance(Textures.Ring, 1.3f));
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                ring.transform.localScale = Vector3.one * (Radii[i] * 4.4f);
                var block = new MaterialPropertyBlock();
                block.SetColor("_Color", new Color(team.r, team.g, team.b, 0.9f));
                ring.GetComponent<Renderer>().SetPropertyBlock(block);
                Gfx.NoShadows(ring);
            }
        }

        private void BuildDust()
        {
            var go = new GameObject("Dust", typeof(ParticleSystem));
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(3f, 4f, 2f);
            ParticleSystem system = go.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.startLifetime = 12f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new Color(0.55f, 1f, 0.85f, 0.45f);
            main.maxParticles = 160;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 14f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 8f, 16f);
            ParticleSystem.VelocityOverLifetimeModule drift = system.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            drift.y = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            drift.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialFactory.Glow();
            system.Play();
        }
    }
}
