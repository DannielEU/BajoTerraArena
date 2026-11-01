using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The stand-in body of one combatant, built from primitives: legs that swing when it walks, a torso with a team
    /// stripe, a big friendly head with the character's hair or gear, two arms holding a blaster whose chamber glows
    /// in the colour of the selected slug, and a backpack. It is only used for a character that has no Blender model
    /// in <c>Resources/Models</c> (see <see cref="CharacterModels"/>), e.g. content the server adds later.
    /// </summary>
    public sealed class PrimitiveCharacterModel : ICharacterModel
    {
        private readonly float _radius;
        private readonly float _standing;
        private readonly CharacterStyle _style;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Material> _originals = new List<Material>();

        private readonly Transform _body;
        private readonly Transform _legL;
        private readonly Transform _legR;
        private readonly Transform _legMeshL;
        private readonly Transform _legMeshR;
        private readonly Transform _bootL;
        private readonly Transform _bootR;
        private readonly Transform _torso;
        private readonly Transform _stripe;
        private readonly Transform _belt;
        private readonly Transform _pack;
        private readonly Transform _head;
        private readonly Transform _gun;
        private readonly Transform _gripRight;
        private readonly Transform _gripLeft;
        private readonly Transform _armRight;
        private readonly Transform _armLeft;
        private readonly Transform[] _plates = new Transform[2];
        private readonly Material _chamberMaterial;
        private readonly Renderer _chamberRenderer;
        private readonly Transform _chamberHalo;
        private readonly MaterialPropertyBlock _haloBlock = new MaterialPropertyBlock();
        private Color _lastSlugColor = Color.clear;
        private bool _dead;
        private float _walkPhase;
        private float _danceTime;
        private float _walkAmount;

        public PrimitiveCharacterModel(Transform parent, CharacterStyle style, float radius, float standingHeight, Color team)
        {
            _style = style;
            _radius = radius;
            _standing = standingHeight;
            float bulk = style.Bulk;

            _body = new GameObject("Model").transform;
            _body.SetParent(parent, false);

            Material skin = Mat(style.Skin, 0.3f);
            Material outfit = Mat(style.Outfit, 0.3f);
            Material trim = Mat(style.Trim, 0.4f);
            Material pants = Mat(style.Pants, 0.25f);
            Material hair = Mat(style.Hair, 0.2f);
            Material metal = MaterialFactory.Surface(null, new Color(0.17f, 0.18f, 0.22f), 0.55f, 0.55f);
            Material teamMaterial = MaterialFactory.Emissive(team, 1.2f, 0.4f);
            _chamberMaterial = MaterialFactory.LitInstance(Color.white);
            MaterialFactory.SetEmission(_chamberMaterial, Color.white * 2.5f);

            // Legs hang from hip pivots so they can swing.
            float legR = 0.26f * radius * Mathf.Sqrt(bulk);
            _legL = Pivot("Hip L");
            _legR = Pivot("Hip R");
            _legMeshL = Part(PrimitiveType.Capsule, "Leg L", _legL, pants);
            _legMeshR = Part(PrimitiveType.Capsule, "Leg R", _legR, pants);
            _bootL = Part(PrimitiveType.Cube, "Boot L", _legL, metal);
            _bootR = Part(PrimitiveType.Cube, "Boot R", _legR, metal);
            _legMeshL.localScale = new Vector3(legR * 2f, 0.4f, legR * 2f);
            _legMeshR.localScale = new Vector3(legR * 2f, 0.4f, legR * 2f);

            _torso = Part(PrimitiveType.Cube, "Torso", _body, outfit);
            _stripe = Part(PrimitiveType.Cube, "Stripe", _body, teamMaterial);
            _belt = Part(PrimitiveType.Cube, "Belt", _body, trim);
            _pack = Part(PrimitiveType.Cube, "Pack", _body, metal);
            var packLight = Part(PrimitiveType.Sphere, "Pack light", _pack, teamMaterial);
            packLight.localPosition = new Vector3(0f, 0.25f, -0.55f);
            packLight.localScale = new Vector3(0.28f, 0.2f, 0.2f);

            _head = new GameObject("Head").transform;
            _head.SetParent(_body, false);
            BuildHead(_head, style, skin, hair, trim, metal);

            if (style.ShoulderPlates)
            {
                for (int i = 0; i < 2; i++)
                {
                    _plates[i] = Part(PrimitiveType.Sphere, "Plate", _body, metal);
                }
            }

            _armRight = Part(PrimitiveType.Capsule, "Arm R", _body, outfit);
            _armLeft = Part(PrimitiveType.Capsule, "Arm L", _body, outfit);

            _gun = new GameObject("Blaster").transform;
            _gun.SetParent(_body, false);
            Transform gunBody = Part(PrimitiveType.Cube, "Gun body", _gun, metal);
            gunBody.localPosition = new Vector3(0f, 0f, 0.25f);
            gunBody.localScale = new Vector3(0.1f, 0.15f, 0.44f);
            Transform barrel = Part(PrimitiveType.Cylinder, "Barrel", _gun, metal);
            barrel.localPosition = new Vector3(0f, 0.01f, 0.56f);
            barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.localScale = new Vector3(0.075f, 0.16f, 0.075f);
            Transform muzzle = Part(PrimitiveType.Cylinder, "Muzzle ring", _gun, teamMaterial);
            muzzle.localPosition = new Vector3(0f, 0.01f, 0.7f);
            muzzle.localRotation = Quaternion.Euler(90f, 0f, 0f);
            muzzle.localScale = new Vector3(0.1f, 0.02f, 0.1f);
            Transform chamber = Part(PrimitiveType.Sphere, "Chamber", _gun, _chamberMaterial);
            chamber.localPosition = new Vector3(0f, 0.11f, 0.2f);
            chamber.localScale = Vector3.one * 0.13f;
            _chamberRenderer = chamber.GetComponent<Renderer>();
            Transform handle = Part(PrimitiveType.Cube, "Handle", _gun, trim);
            handle.localPosition = new Vector3(0f, -0.13f, 0.1f);
            handle.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            handle.localScale = new Vector3(0.07f, 0.2f, 0.08f);
            _gripRight = new GameObject("Grip R").transform;
            _gripRight.SetParent(_gun, false);
            _gripRight.localPosition = new Vector3(0f, -0.11f, 0.1f);
            _gripLeft = new GameObject("Grip L").transform;
            _gripLeft.SetParent(_gun, false);
            _gripLeft.localPosition = new Vector3(0f, -0.04f, 0.4f);

            // A soft coloured glow around the chamber (camera-facing sprite).
            _chamberHalo = Gfx.Halo(_gun, "Chamber glow", Vector3.zero, Color.white, 0.5f);
            _chamberHalo.localPosition = new Vector3(0f, 0.11f, 0.2f);
            _renderers.Remove(_chamberHalo.GetComponent<Renderer>());

            // Everything starts hanging at the standing pose.
            Layout(standingHeight, 0f, 0f, false, 0f, Color.white);
        }

        public bool HandlesRoll
        {
            get { return false; }
        }

        public void Destroy()
        {
        }

        public void Pose(in CharacterPose pose)
        {
            float speed = pose.Velocity.magnitude;
            _walkAmount = Mathf.MoveTowards(_walkAmount, Mathf.Clamp(speed / 6f, 0f, 1.5f), pose.DeltaTime * 6f);
            _walkPhase += pose.DeltaTime * (4f + speed * 1.4f);
            Layout(pose.Height, _walkPhase, _walkAmount, pose.Airborne, pose.Pitch, pose.SlugColor);

            // The stand-in's dance: hops on the beat and twists from side to side (a full spin in the third one).
            if (pose.Dance > 0)
            {
                _danceTime += pose.DeltaTime;
                float beat = _danceTime * Mathf.PI * 4f;
                float hop = Mathf.Abs(Mathf.Sin(beat)) * 0.12f * _standing;
                float twist = pose.Dance == 3 ? _danceTime * 360f : Mathf.Sin(beat * 0.5f) * (pose.Dance == 1 ? 25f : 45f);
                _body.localPosition += new Vector3(0f, hop, 0f);
                _body.localRotation = Quaternion.Euler(0f, twist, Mathf.Sin(beat * 0.5f) * 6f);
            }
            else
            {
                _danceTime = 0f;
                _body.localRotation = Quaternion.identity;
            }
        }

        /// <summary>Lays the parts out for the current hitbox height and animation state.</summary>
        /// <param name="height">The hitbox height now (smaller while crouching).</param>
        /// <param name="walkPhase">Radians of the walk cycle.</param>
        /// <param name="walkAmount">0 standing still, 1 at a full walk, more when sprinting.</param>
        /// <param name="airborne">True in the air (legs tuck instead of swinging).</param>
        /// <param name="pitch">Aim pitch in radians (positive looks up).</param>
        /// <param name="slugColor">Colour of the selected slug, for the blaster's chamber.</param>
        private void Layout(float height, float walkPhase, float walkAmount, bool airborne, float pitch, Color slugColor)
        {
            float bulk = _style.Bulk;
            float scale = height / _standing;
            float hip = 0.46f * height;
            float torsoHeight = 0.34f * height;
            float bodyWidth = 1.5f * _radius * bulk;
            float bodyDepth = 0.95f * _radius * Mathf.Sqrt(bulk);
            float headRadius = 0.13f * _standing * _style.HeadSize;
            float bob = airborne ? 0f : Mathf.Abs(Mathf.Sin(walkPhase)) * 0.045f * Mathf.Clamp01(walkAmount);
            float legR = 0.26f * _radius * Mathf.Sqrt(bulk);
            float swing = airborne ? 0f : Mathf.Sin(walkPhase) * 38f * Mathf.Clamp(walkAmount, 0f, 1.4f);

            // Legs.
            float legX = 0.34f * _radius * bulk;
            _legL.localPosition = new Vector3(-legX, hip + bob, 0f);
            _legR.localPosition = new Vector3(legX, hip + bob, 0f);
            float tuck = airborne ? 22f : 0f;
            _legL.localRotation = Quaternion.Euler(swing + tuck, 0f, 0f);
            _legR.localRotation = Quaternion.Euler(-swing - tuck * 0.4f, 0f, 0f);
            float legLength = Mathf.Max(0.1f, hip);
            _legMeshL.localScale = new Vector3(legR * 2f, legLength * 0.5f, legR * 2f);
            _legMeshR.localScale = new Vector3(legR * 2f, legLength * 0.5f, legR * 2f);
            _legMeshL.localPosition = new Vector3(0f, -legLength * 0.5f, 0f);
            _legMeshR.localPosition = new Vector3(0f, -legLength * 0.5f, 0f);
            Vector3 bootSize = new Vector3(legR * 2.3f, 0.09f * scale + 0.03f, legR * 3.3f);
            _bootL.localScale = bootSize;
            _bootR.localScale = bootSize;
            _bootL.localPosition = new Vector3(0f, -legLength + bootSize.y * 0.5f, legR * 0.7f);
            _bootR.localPosition = new Vector3(0f, -legLength + bootSize.y * 0.5f, legR * 0.7f);

            // Torso block.
            float torsoY = hip + torsoHeight * 0.5f + bob;
            _torso.localPosition = new Vector3(0f, torsoY, 0f);
            _torso.localScale = new Vector3(bodyWidth, torsoHeight, bodyDepth);
            _stripe.localPosition = new Vector3(0f, hip + torsoHeight * 0.72f + bob, 0f);
            _stripe.localScale = new Vector3(bodyWidth + 0.02f, torsoHeight * 0.16f, bodyDepth + 0.02f);
            _belt.localPosition = new Vector3(0f, hip + torsoHeight * 0.06f + bob, 0f);
            _belt.localScale = new Vector3(bodyWidth + 0.03f, torsoHeight * 0.12f, bodyDepth + 0.03f);
            _pack.localPosition = new Vector3(0f, torsoY + torsoHeight * 0.05f, -bodyDepth * 0.5f - 0.08f);
            _pack.localScale = new Vector3(bodyWidth * 0.62f, torsoHeight * 0.7f, 0.16f);

            // Head.
            float headY = hip + torsoHeight + headRadius * 0.82f + bob;
            _head.localPosition = new Vector3(0f, headY, 0.02f);
            _head.localRotation = Quaternion.Euler(-pitch * Mathf.Rad2Deg * 0.35f, 0f, 0f);
            _head.localScale = Vector3.one * headRadius * 2f;

            // Shoulders, arms and the blaster.
            Vector3 shoulderR = new Vector3(bodyWidth * 0.5f + 0.02f, hip + torsoHeight * 0.88f + bob, 0f);
            Vector3 shoulderL = new Vector3(-bodyWidth * 0.5f - 0.02f, hip + torsoHeight * 0.88f + bob, 0f);
            if (_style.ShoulderPlates)
            {
                _plates[0].localPosition = shoulderR + new Vector3(0.06f, 0.04f, 0f);
                _plates[1].localPosition = shoulderL + new Vector3(-0.06f, 0.04f, 0f);
                _plates[0].localScale = new Vector3(0.28f, 0.2f, 0.3f);
                _plates[1].localScale = new Vector3(0.28f, 0.2f, 0.3f);
            }
            _gun.localPosition = new Vector3(bodyWidth * 0.5f + 0.02f, hip + torsoHeight * 0.55f + bob, 0.1f);
            _gun.localRotation = Quaternion.Euler(-pitch * Mathf.Rad2Deg, 0f, 0f);
            float armRadius = 0.07f * Mathf.Sqrt(bulk);
            Limb(_armRight, shoulderR, _body.InverseTransformPoint(_gripRight.position), armRadius);
            Limb(_armLeft, shoulderL, _body.InverseTransformPoint(_gripLeft.position), armRadius);

            if (!_dead && slugColor != _lastSlugColor)
            {
                _lastSlugColor = slugColor;
                _chamberMaterial.color = slugColor;
                MaterialFactory.SetEmission(_chamberMaterial, slugColor * 2.5f);
                _haloBlock.SetColor("_Color", new Color(slugColor.r, slugColor.g, slugColor.b, 0.7f));
                _chamberHalo.GetComponent<Renderer>().SetPropertyBlock(_haloBlock);
            }
        }

        /// <summary>The model's root, so the owner can spin or tip it.</summary>
        public Transform Body
        {
            get { return _body; }
        }

        /// <summary>Greys the whole body out when defeated, and restores it when the next round brings it back.</summary>
        public void SetDead(bool dead)
        {
            if (dead == _dead)
            {
                return;
            }
            _dead = dead;
            Material gray = MaterialFactory.Surface(null, new Color(0.2f, 0.2f, 0.23f), 0.2f, 0f);
            for (int i = 0; i < _renderers.Count; i++)
            {
                _renderers[i].sharedMaterial = dead ? gray : _originals[i];
            }
            _chamberHalo.gameObject.SetActive(!dead);
            _chamberRenderer.sharedMaterial = dead ? gray : _chamberMaterial;
            if (!dead)
            {
                _lastSlugColor = Color.clear;
            }
        }

        // ------------------------------------------------------------------------------------------ building

        private static Material Mat(Color color, float smoothness)
        {
            return MaterialFactory.Surface(null, color, smoothness, 0f);
        }

        private Transform Pivot(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_body, false);
            return t;
        }

        private Transform Part(PrimitiveType type, string name, Transform parent, Material material)
        {
            GameObject go = Gfx.Primitive(type, name, parent, material);
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            _renderers.Add(renderer);
            _originals.Add(material);
            return go.transform;
        }

        private void BuildHead(Transform head, CharacterStyle style, Material skin, Material hair, Material trim,
            Material metal)
        {
            // The head transform is scaled to the head's diameter; children are in units of the head's radius * 2.
            Transform skull = Part(PrimitiveType.Sphere, "Skull", head, style.Gear == HeadGear.Visor ? metal : skin);
            skull.localScale = Vector3.one;

            Material white = Mat(Color.white, 0.6f);
            Material dark = Mat(new Color(0.05f, 0.05f, 0.08f), 0.8f);
            if (style.Gear != HeadGear.Visor)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform eye = Part(PrimitiveType.Sphere, "Eye", head, white);
                    eye.localPosition = new Vector3(side * 0.17f, 0.04f, 0.4f);
                    eye.localScale = new Vector3(0.2f, 0.24f, 0.12f);
                    Transform pupil = Part(PrimitiveType.Sphere, "Pupil", head, dark);
                    pupil.localPosition = new Vector3(side * 0.17f, 0.03f, 0.465f);
                    pupil.localScale = new Vector3(0.1f, 0.13f, 0.06f);
                }
            }

            switch (style.Gear)
            {
                case HeadGear.SpikyHair:
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            float angle = i / 6f * Mathf.PI * 2f;
                            Transform spike = Part(PrimitiveType.Cube, "Spike", head, hair);
                            spike.localPosition = new Vector3(Mathf.Cos(angle) * 0.26f, 0.42f, Mathf.Sin(angle) * 0.2f - 0.06f);
                            spike.localRotation = Quaternion.Euler(Mathf.Sin(angle) * 28f, 0f, -Mathf.Cos(angle) * 28f)
                                                  * Quaternion.Euler(0f, 45f, 0f);
                            spike.localScale = new Vector3(0.2f, 0.34f, 0.2f);
                        }
                        Transform cap = Part(PrimitiveType.Sphere, "Hair", head, hair);
                        cap.localPosition = new Vector3(0f, 0.12f, -0.05f);
                        cap.localScale = new Vector3(1.06f, 0.95f, 1.0f);
                        // Goggles pushed up on the forehead.
                        Material lens = MaterialFactory.Emissive(new Color(0.3f, 0.9f, 1f), 1.6f, 0.8f);
                        Transform band = Part(PrimitiveType.Cube, "Goggle band", head, trim);
                        band.localPosition = new Vector3(0f, 0.28f, 0.2f);
                        band.localScale = new Vector3(1.04f, 0.1f, 0.9f);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Transform glass = Part(PrimitiveType.Cylinder, "Goggle", head, lens);
                            glass.localPosition = new Vector3(side * 0.22f, 0.34f, 0.44f);
                            glass.localRotation = Quaternion.Euler(90f, 0f, 0f);
                            glass.localScale = new Vector3(0.24f, 0.05f, 0.24f);
                        }
                        break;
                    }
                case HeadGear.Ponytail:
                    {
                        Transform cap = Part(PrimitiveType.Sphere, "Hair", head, hair);
                        cap.localPosition = new Vector3(0f, 0.1f, -0.1f);
                        cap.localScale = new Vector3(1.1f, 1.02f, 1.02f);
                        Transform tail = Part(PrimitiveType.Capsule, "Ponytail", head, hair);
                        tail.localPosition = new Vector3(0.16f, -0.05f, -0.68f);
                        tail.localRotation = Quaternion.Euler(-24f, 0f, 14f);
                        tail.localScale = new Vector3(0.26f, 0.62f, 0.26f);
                        Transform bow = Part(PrimitiveType.Cube, "Bow", head, trim);
                        bow.localPosition = new Vector3(0.1f, 0.3f, -0.5f);
                        bow.localScale = new Vector3(0.3f, 0.16f, 0.1f);
                        break;
                    }
                case HeadGear.Visor:
                    {
                        Material glow = MaterialFactory.Emissive(new Color(1f, 0.55f, 0.15f), 2.4f, 0.6f);
                        Transform visor = Part(PrimitiveType.Cube, "Visor", head, glow);
                        visor.localPosition = new Vector3(0f, 0.05f, 0.42f);
                        visor.localScale = new Vector3(0.78f, 0.15f, 0.2f);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Transform ear = Part(PrimitiveType.Cylinder, "Ear plate", head, trim);
                            ear.localPosition = new Vector3(side * 0.52f, 0f, 0f);
                            ear.localRotation = Quaternion.Euler(0f, 0f, 90f);
                            ear.localScale = new Vector3(0.34f, 0.04f, 0.34f);
                        }
                        Transform crest = Part(PrimitiveType.Cube, "Crest", head, hair);
                        crest.localPosition = new Vector3(0f, 0.5f, -0.05f);
                        crest.localScale = new Vector3(0.1f, 0.2f, 0.6f);
                        break;
                    }
                case HeadGear.Mole:
                    {
                        Material nose = Mat(new Color(1f, 0.55f, 0.6f), 0.5f);
                        Transform snout = Part(PrimitiveType.Sphere, "Nose", head, nose);
                        snout.localPosition = new Vector3(0f, -0.06f, 0.52f);
                        snout.localScale = Vector3.one * 0.3f;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Transform ear = Part(PrimitiveType.Sphere, "Ear", head, skin);
                            ear.localPosition = new Vector3(side * 0.5f, 0.12f, -0.02f);
                            ear.localScale = new Vector3(0.18f, 0.34f, 0.3f);
                        }
                        Transform fur = Part(PrimitiveType.Sphere, "Fur", head, hair);
                        fur.localPosition = new Vector3(0f, 0.26f, -0.1f);
                        fur.localScale = new Vector3(1.0f, 0.55f, 0.95f);
                        // A miner's lamp on a headband.
                        Material lamp = MaterialFactory.Emissive(new Color(1f, 0.9f, 0.5f), 2.5f, 0.6f);
                        Transform lampMesh = Part(PrimitiveType.Sphere, "Lamp", head, lamp);
                        lampMesh.localPosition = new Vector3(0f, 0.42f, 0.34f);
                        lampMesh.localScale = new Vector3(0.22f, 0.2f, 0.22f);
                        break;
                    }
            }
        }

        /// <summary>Stretches a capsule between two points (in the body's space).</summary>
        private static void Limb(Transform limb, Vector3 from, Vector3 to, float radius)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 1e-4f)
            {
                return;
            }
            limb.localPosition = (from + to) * 0.5f;
            limb.localRotation = Quaternion.FromToRotation(Vector3.up, delta / length);
            limb.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        }
    }
}
