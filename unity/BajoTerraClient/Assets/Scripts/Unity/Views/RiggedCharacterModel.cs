using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Bajoterra.Unity
{
    /// <summary>
    /// A hero made in Blender (<c>unity/art/blender</c>, exported to <c>Resources/Models/&lt;id&gt;.fbx</c>): a skinned
    /// body that holds its blaster in both hands, and thirteen clips that are blended by the state the server reports.
    /// Standing and crouched idle, running in four directions (standing and crouched), a mid-air pose, the roll and a
    /// defeated pose, plus three celebration dances. The weights come from what is really happening: the speed and direction the body moves in, how
    /// much the hitbox has shrunk (crouch), whether the active move is the roll, whether it is in the air. Aiming
    /// tilts the spine afterwards, so the blaster follows the camera. The blaster's slug dial glows in the colour of
    /// the selected slug, like the primitive stand-in did.
    /// </summary>
    public sealed class RiggedCharacterModel : ICharacterModel
    {
        private const string ResourcePath = "Models/";

        private const int Idle = 0, CrouchIdle = 1, RunF = 2, RunB = 3, RunL = 4, RunR = 5, CrouchRunF = 6, CrouchRunB = 7,
            CrouchRunL = 8, CrouchRunR = 9, Air = 10, Roll = 11, Dead = 12, ClipCount = 13;

        private static readonly string[] ClipNames =
        {
            "Idle", "CrouchIdle", "RunF", "RunB", "RunL", "RunR", "CrouchRunF", "CrouchRunB", "CrouchRunL", "CrouchRunR",
            "Air", "Roll", "Dead"
        };

        /// <summary>Celebration dances (optional: a model without them simply does not dance).</summary>
        private static readonly string[] DanceNames = { "Dance1", "Dance2", "Dance3" };

        /// <summary>The height each hero was modelled at: the server's hitbox for that character. The model is scaled to the real one.</summary>
        private static readonly Dictionary<string, float> AuthoredHeights = new Dictionary<string, float>
        {
            { "eli", 1.8f }, { "trixie", 1.7f }, { "kord", 2.35f }, { "pronto", 1.15f }
        };

        private const float MaxCyclesPerSecond = 2.5f;

        private readonly Transform _body;
        private readonly Animator _animator;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private readonly AnimationClipPlayable[] _players = new AnimationClipPlayable[ClipCount];
        private readonly float[] _length = new float[ClipCount];
        private readonly float[] _weight = new float[ClipCount];
        private readonly float[] _time = new float[ClipCount];
        private readonly AnimationClipPlayable[] _dancePlayers = new AnimationClipPlayable[3];
        private readonly float[] _danceLength = new float[3];
        private readonly float[] _danceWeight = new float[3];
        private int _danceCount;
        private int _currentDance;
        private float _danceTime;
        private float _dance;

        private readonly Transform _spine;
        private readonly Transform _chest;
        private readonly Transform _neck;
        private readonly Transform _head;

        private readonly SkinnedMeshRenderer[] _renderers;
        private readonly Material[][] _original;
        private readonly Material[][] _gray;
        private readonly Material _chamberMaterial;
        private readonly Transform _chamberHalo;
        private readonly MaterialPropertyBlock _haloBlock = new MaterialPropertyBlock();
        private Color _lastSlugColor = Color.clear;

        private readonly float _standing;
        private readonly float _cycleDistance;
        private float _crouch;
        private float _move;
        private float _air;
        private float _roll;
        private float _rollTime;
        private float _rollYaw;
        private float _gait;
        private float _idle;
        private Vector2 _direction = Vector2.up;
        private bool _wasRolling;
        private bool _dead;

        private RiggedCharacterModel(Transform parent, string id, GameObject prefab, AnimationClip[] clips,
            AnimationClip[] dances, float scale, float standingHeight, Color team)
        {
            _standing = standingHeight;
            _cycleDistance = 2.2f * (standingHeight / 1.8f);

            GameObject instance = Object.Instantiate(prefab, parent, false);
            instance.name = "Model";
            instance.transform.localScale = Vector3.one * scale;
            _body = instance.transform;

            var bones = new Dictionary<string, Transform>();
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                bones[t.name] = t;
            }
            _spine = bones["Spine"];
            _chest = bones["Chest"];
            _neck = bones["Neck"];
            _head = bones["Head"];

            _animator = instance.GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = instance.AddComponent<Animator>();
            }
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _graph = PlayableGraph.Create("Hero " + id);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Pose", _animator);
            _danceCount = dances[0] != null && dances[1] != null && dances[2] != null ? 3 : 0;
            _mixer = AnimationMixerPlayable.Create(_graph, ClipCount + _danceCount);
            output.SetSourcePlayable(_mixer);
            for (int i = 0; i < ClipCount; i++)
            {
                _players[i] = AnimationClipPlayable.Create(_graph, clips[i]);
                _players[i].SetApplyFootIK(false);
                _graph.Connect(_players[i], 0, _mixer, i);
                _mixer.SetInputWeight(i, 0f);
                _length[i] = Mathf.Max(0.01f, clips[i].length);
            }
            for (int i = 0; i < _danceCount; i++)
            {
                _dancePlayers[i] = AnimationClipPlayable.Create(_graph, dances[i]);
                _dancePlayers[i].SetApplyFootIK(false);
                _graph.Connect(_dancePlayers[i], 0, _mixer, ClipCount + i);
                _mixer.SetInputWeight(ClipCount + i, 0f);
                _danceLength[i] = Mathf.Max(0.01f, dances[i].length);
            }

            // The imported materials only carry the colours: swap them for the game's own shaders.
            var converted = new Dictionary<string, Material>();
            _renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _original = new Material[_renderers.Length][];
            _gray = new Material[_renderers.Length][];
            Material grey = MaterialFactory.Surface(null, new Color(0.2f, 0.2f, 0.23f), 0.2f, 0f);
            for (int r = 0; r < _renderers.Length; r++)
            {
                SkinnedMeshRenderer renderer = _renderers[r];
                Material[] source = renderer.sharedMaterials;
                var result = new Material[source.Length];
                var greys = new Material[source.Length];
                for (int m = 0; m < source.Length; m++)
                {
                    string key = source[m].name;
                    if (!converted.TryGetValue(key, out Material material))
                    {
                        material = Convert(source[m], team);
                        converted[key] = material;
                    }
                    result[m] = material;
                    greys[m] = grey;
                }
                renderer.sharedMaterials = result;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.updateWhenOffscreen = true;
                _original[r] = result;
                _gray[r] = greys;
            }
            converted.TryGetValue("Chamber", out _chamberMaterial);

            Transform chamber = bones["Chamber"];
            _chamberHalo = Gfx.Halo(chamber, "Chamber glow", chamber.position, Color.white, 0.5f);

            _weight[Idle] = 1f;
            Evaluate();
        }

        /// <summary>The Blender model of a character, or null if it has none (the caller then uses the stand-in).</summary>
        public static RiggedCharacterModel TryCreate(Transform parent, string id, float standingHeight, Color team)
        {
            if (!AuthoredHeights.TryGetValue(id, out float authored))
            {
                return null;
            }
            var prefab = Resources.Load<GameObject>(ResourcePath + id);
            if (prefab == null)
            {
                return null;
            }
            var clips = new AnimationClip[ClipCount];
            var dances = new AnimationClip[DanceNames.Length];
            foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>(ResourcePath + id))
            {
                string name = clip.name;
                if (name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    continue;
                }
                int bar = name.LastIndexOf('|');
                string bare = bar >= 0 ? name.Substring(bar + 1) : name;
                int index = Array.IndexOf(ClipNames, bare);
                if (index >= 0)
                {
                    clips[index] = clip;
                }
                int dance = Array.IndexOf(DanceNames, bare);
                if (dance >= 0)
                {
                    dances[dance] = clip;
                }
            }
            for (int i = 0; i < ClipCount; i++)
            {
                if (clips[i] == null)
                {
                    Debug.LogWarning("Bajoterra: Models/" + id + " has no '" + ClipNames[i] + "' clip, using the stand-in body.");
                    return null;
                }
            }
            return new RiggedCharacterModel(parent, id, prefab, clips, dances, standingHeight / authored, standingHeight, team);
        }

        public Transform Body
        {
            get { return _body; }
        }

        public bool HandlesRoll
        {
            get { return true; }
        }

        public void Destroy()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }

        public void Pose(in CharacterPose pose)
        {
            float dt = Mathf.Max(0f, pose.DeltaTime);

            // Crouch comes from how far the hitbox has shrunk (it is 60 % while crouching).
            float crouchTarget = Mathf.Clamp01((_standing - pose.Height) / (_standing * 0.4f));
            _crouch = Mathf.MoveTowards(_crouch, crouchTarget, dt * 6f);

            // Locomotion: which way and how fast it really moves.
            float speed = pose.Velocity.magnitude;
            if (speed > 0.4f)
            {
                Vector2 wanted = pose.Velocity / speed;
                _direction = Vector2.Lerp(_direction, wanted, 1f - Mathf.Exp(-dt * 12f));
                if (_direction.sqrMagnitude < 0.01f)
                {
                    _direction = wanted;
                }
            }
            Vector2 d = _direction.normalized;
            float moveTarget = Mathf.Clamp01((speed - 0.35f) / 1.2f);
            _move = Mathf.MoveTowards(_move, moveTarget, dt * 7f);
            float cycle = Mathf.Lerp(_cycleDistance, _cycleDistance * 0.55f, _crouch);
            _gait += dt * Mathf.Min(speed / cycle, MaxCyclesPerSecond);
            _idle += dt;

            _air = Mathf.MoveTowards(_air, pose.Airborne && !pose.Rolling ? 1f : 0f, dt * 9f);
            if (pose.Rolling)
            {
                if (!_wasRolling)
                {
                    _rollTime = 0f;
                    _rollYaw = 0f;
                }
                _rollTime += dt;
                // The roll goes where the body is going: read the direction while it gets up to speed.
                if (_rollTime < 0.15f && speed > 1f)
                {
                    _rollYaw = Mathf.Atan2(pose.Velocity.x, pose.Velocity.y) * Mathf.Rad2Deg;
                }
            }
            _wasRolling = pose.Rolling;
            _roll = Mathf.MoveTowards(_roll, pose.Rolling ? 1f : 0f, dt * (pose.Rolling ? 18f : 9f));
            float dead = Mathf.Clamp01(pose.Fall);

            // A dance takes over the whole body while it lasts (the server stops it as soon as the hero moves).
            bool dancing = _danceCount > 0 && pose.Dance > 0 && !pose.Airborne && !pose.Rolling;
            if (dancing && pose.Dance - 1 != _currentDance)
            {
                _currentDance = Mathf.Clamp(pose.Dance - 1, 0, _danceCount - 1);
                _danceTime = 0f;
            }
            if (dancing && _dance <= 0f)
            {
                _danceTime = 0f;
            }
            _danceTime += dt;
            _dance = Mathf.MoveTowards(_dance, dancing ? 1f : 0f, dt * (dancing ? 5f : 8f));
            for (int i = 0; i < _danceCount; i++)
            {
                _danceWeight[i] = i == _currentDance ? _dance * (1f - dead) : 0f;
            }
            float still = 1f - _dance * (1f - dead);

            // Clip weights: stand/crouch x idle/run, then air, roll and defeated take over in that order. They sum to 1.
            float stand = 1f - _crouch;
            float f = Mathf.Max(0f, d.y), b = Mathf.Max(0f, -d.y), r = Mathf.Max(0f, d.x), l = Mathf.Max(0f, -d.x);
            float sum = Mathf.Max(0.0001f, f + b + r + l);
            f /= sum;
            b /= sum;
            r /= sum;
            l /= sum;
            float runStand = _move * stand;
            float runCrouch = _move * _crouch;
            float rest = (1f - _air) * (1f - _roll) * (1f - dead) * still;
            _weight[Idle] = (1f - _move) * stand * rest;
            _weight[CrouchIdle] = (1f - _move) * _crouch * rest;
            _weight[RunF] = runStand * f * rest;
            _weight[RunB] = runStand * b * rest;
            _weight[RunL] = runStand * l * rest;
            _weight[RunR] = runStand * r * rest;
            _weight[CrouchRunF] = runCrouch * f * rest;
            _weight[CrouchRunB] = runCrouch * b * rest;
            _weight[CrouchRunL] = runCrouch * l * rest;
            _weight[CrouchRunR] = runCrouch * r * rest;
            _weight[Air] = _air * (1f - _roll) * (1f - dead) * still;
            _weight[Roll] = _roll * (1f - dead) * still;
            _weight[Dead] = dead;

            float cycleTime = _gait - Mathf.Floor(_gait);
            for (int i = 0; i < ClipCount; i++)
            {
                switch (i)
                {
                    case Idle:
                    case CrouchIdle:
                    case Air:
                        _time[i] = Mathf.Repeat(_idle, _length[i]);
                        break;
                    case Roll:
                        _time[i] = Mathf.Min(_rollTime, _length[i]);
                        break;
                    case Dead:
                        _time[i] = 0f;
                        break;
                    default:
                        _time[i] = cycleTime * _length[i];
                        break;
                }
            }

            _body.localRotation = Quaternion.Euler(0f, _rollYaw * _roll, 0f);
            Evaluate();

            // Aim: the camera pitch tilts the spine (the arms carry the blaster with it) and the head half-counters it.
            float aim = (1f - _roll) * (1f - dead) * (1f - _dance);
            float degrees = -pose.Pitch * Mathf.Rad2Deg * aim;
            Vector3 axis = _body.right;
            Tilt(_spine, axis, degrees * 0.4f);
            Tilt(_chest, axis, degrees * 0.6f);
            Tilt(_neck, axis, -degrees * 0.15f);
            Tilt(_head, axis, -degrees * 0.15f);

            if (!_dead && _chamberMaterial != null && pose.SlugColor != _lastSlugColor)
            {
                Color slug = pose.SlugColor;
                _lastSlugColor = slug;
                _chamberMaterial.color = slug;
                MaterialFactory.SetEmission(_chamberMaterial, slug * 2.5f);
                _haloBlock.SetColor("_Color", new Color(slug.r, slug.g, slug.b, 0.7f));
                _chamberHalo.GetComponent<Renderer>().SetPropertyBlock(_haloBlock);
            }
        }

        public void SetDead(bool dead)
        {
            if (dead == _dead)
            {
                return;
            }
            _dead = dead;
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].sharedMaterials = dead ? _gray[i] : _original[i];
            }
            _chamberHalo.gameObject.SetActive(!dead);
            if (!dead)
            {
                _lastSlugColor = Color.clear;
            }
        }

        // ------------------------------------------------------------------------------------------ internals

        private void Evaluate()
        {
            for (int i = 0; i < ClipCount; i++)
            {
                _mixer.SetInputWeight(i, _weight[i]);
                if (_weight[i] > 1e-4f)
                {
                    _players[i].SetTime(_time[i]);
                }
            }
            for (int i = 0; i < _danceCount; i++)
            {
                _mixer.SetInputWeight(ClipCount + i, _danceWeight[i]);
                if (_danceWeight[i] > 1e-4f)
                {
                    _dancePlayers[i].SetTime(Mathf.Repeat(_danceTime, _danceLength[i]));
                }
            }
            _graph.Evaluate(0f);
        }

        private static void Tilt(Transform bone, Vector3 axis, float degrees)
        {
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

        /// <summary>The imported materials only carry colours: build the game's own material for each by its role name.</summary>
        private Material Convert(Material imported, Color team)
        {
            string name = imported.name;
            Color c = imported.color;
            switch (name)
            {
                case "Chamber":
                    Material chamber = MaterialFactory.LitInstance(c);
                    MaterialFactory.SetEmission(chamber, c * 2.5f);
                    return chamber;
                case "Team":
                    return MaterialFactory.Emissive(team, 1.3f, 0.4f);
                case "Glow":
                case "Lens":
                    return MaterialFactory.Emissive(c, 2.4f, 0.7f);
                case "GlowBlue":
                    return MaterialFactory.Emissive(c, 1.8f, 0.7f);
                case "Metal":
                    return MaterialFactory.Surface(null, c, 0.55f, 0.6f);
                case "Steel":
                    return MaterialFactory.Surface(null, c, 0.6f, 0.7f);
                case "Steel2":
                case "Eye":
                case "Tooth":
                    return MaterialFactory.Surface(null, c, 0.6f, 0.05f);
                case "Rubber":
                case "Pupil":
                    return MaterialFactory.Surface(null, c, 0.25f, 0f);
                case "Hair":
                    return MaterialFactory.Surface(null, c, 0.3f, 0f);
                default:
                    return MaterialFactory.Surface(null, c, 0.3f, 0f);
            }
        }
    }
}
