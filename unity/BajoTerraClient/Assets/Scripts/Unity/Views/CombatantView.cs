using Bajoterra.Client;
using Bajoterra.Client.Content;
using Bajoterra.Client.Interpolation;
using Bajoterra.Protocol;
using UnityEngine;
using ProtoCharacter = Bajoterra.Protocol.CharacterInfo;

namespace Bajoterra.Unity
{
    /// <summary>
    /// One player or bot on screen: the <see cref="ICharacterModel"/> animated from the server's state (the velocity it
    /// is really moving with, crouch from the hitbox, the roll, the defeat fall), a team ring on the floor, a shield
    /// bubble that follows the shield value and flashes when it is hit, a golden aura on the floor while the combatant
    /// has courage (their team is outnumbered), and a name tag with health and shield bars and a dot per status. The data that drives it (<see cref="CombatantFrame"/>) does not depend on which model it is.
    /// </summary>
    public sealed class CombatantView
    {
        private const float BarWidth = 1.2f;
        private const int MaxStatusDots = 6;
        private const float TagMaxDistance = 48f;

        private readonly Transform _root;
        private readonly Transform _spin;
        private readonly ICharacterModel _model;
        private readonly int _rollMove;
        private readonly Transform _shieldBubble;
        private readonly Material _shieldMaterial;
        private readonly Transform _ring;
        private readonly Transform _courage;
        private readonly Renderer _courageRenderer;
        private readonly MaterialPropertyBlock _courageBlock = new MaterialPropertyBlock();
        private readonly Transform _tag;
        private readonly TextMesh _nameText;
        private readonly TextMesh _nameShadow;
        private readonly Transform _hpFill;
        private readonly Transform _shieldFill;
        private readonly Transform _overFill;
        private readonly Transform[] _statusDots = new Transform[MaxStatusDots];
        private readonly Renderer[] _statusRenderers = new Renderer[MaxStatusDots];
        private readonly Renderer[] _tagRenderers;
        private readonly ArenaMap _map;
        private readonly ContentCatalog _content;
        private readonly float _radius;
        private readonly float _standingHeight;
        private readonly bool _isYou;
        private readonly bool _ally;
        private readonly Color _teamColor;
        private float _height;
        private float _spinAngle;
        private float _fall;
        private Vector2 _velocity;
        private float _lastShield = -1f;
        private float _shieldFlash;
        private Vector3 _lastPosition;
        private bool _hasLast;
        private bool _wasAlive = true;

        public CombatantView(Transform parent, Slot slot, ProtoCharacter character, Color teamColor, bool isYou,
            bool ally, ArenaMap map, ContentCatalog content)
        {
            _isYou = isYou;
            _ally = ally;
            _teamColor = teamColor;
            _map = map;
            _content = content;
            _radius = character.HitRadius;
            _standingHeight = character.HitHeight;
            _height = _standingHeight;

            _root = new GameObject(slot.Name).transform;
            _root.SetParent(parent, false);
            _spin = new GameObject("Spin").transform;
            _spin.SetParent(_root, false);
            _model = CharacterModels.Create(_spin, character.Id, _radius, _standingHeight, teamColor);
            _rollMove = content.MoveIndexOf("roll");

            // Shield bubble.
            GameObject bubble = Gfx.Primitive(PrimitiveType.Sphere, "Shield", _root, null);
            _shieldBubble = bubble.transform;
            _shieldMaterial = MaterialFactory.Fresnel(Palette.WithAlpha(Palette.Shield, 0.8f));
            Renderer bubbleRenderer = bubble.GetComponent<Renderer>();
            bubbleRenderer.sharedMaterial = _shieldMaterial;
            bubbleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bubbleRenderer.receiveShadows = false;

            // Team ring on the floor.
            GameObject ring = Gfx.Make("Team ring", Billboard.QuadMesh(), _root,
                MaterialFactory.GlowInstance(Textures.Ring, 1.5f));
            _ring = ring.transform;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", isYou ? new Color(1f, 1f, 1f, 0.85f) : Palette.WithAlpha(teamColor, 0.9f));
            ring.GetComponent<Renderer>().SetPropertyBlock(block);
            _ring.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _ring.localPosition = Vector3.up * 0.05f;
            _ring.localScale = Vector3.one * (_radius * 4.4f);
            Gfx.NoShadows(ring);

            // Courage aura: a second, golden ring that spins and breathes around the team ring.
            GameObject aura = Gfx.Make("Courage aura", Billboard.QuadMesh(), _root,
                MaterialFactory.GlowInstance(Textures.Ring, 2f));
            _courage = aura.transform;
            _courageRenderer = aura.GetComponent<Renderer>();
            _courage.localPosition = Vector3.up * 0.07f;
            Gfx.NoShadows(aura);
            aura.SetActive(false);

            _tag = new GameObject("Tag").transform;
            _tag.SetParent(_root, false);
            BuildTag(slot.Name, teamColor, out _hpFill, out _shieldFill, out _overFill, out _nameText, out _nameShadow);
            _tag.gameObject.SetActive(!isYou);
            _tagRenderers = _tag.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < MaxStatusDots; i++)
            {
                GameObject dot = Gfx.Primitive(PrimitiveType.Sphere, "Status " + i, _tag, MaterialFactory.Unlit(Color.white));
                dot.transform.localScale = Vector3.one * 0.15f;
                dot.transform.localPosition = new Vector3((i - (MaxStatusDots - 1) * 0.5f) * 0.2f, 0.3f, 0f);
                _statusDots[i] = dot.transform;
                _statusRenderers[i] = dot.GetComponent<Renderer>();
                dot.SetActive(false);
            }
            ApplyHeight(_height);
        }

        public void Destroy()
        {
            _model.Destroy();
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }
        }

        /// <summary>World position of the centre of the body, for effects.</summary>
        public Vector3 Center
        {
            get { return _root.position + Vector3.up * (_height * 0.5f); }
        }

        public Vector3 Head
        {
            get { return _root.position + Vector3.up * (_height + 0.35f); }
        }

        public void Apply(in CombatantFrame frame, float deltaTime, Camera camera, ContentLookup lookup)
        {
            Vector3 position = frame.Position.ToUnity();
            _root.position = position;
            _root.rotation = Quaternion.Euler(0f, frame.Yaw * Mathf.Rad2Deg, 0f);

            float targetHeight = frame.HitboxHeight > 0.1f ? frame.HitboxHeight : _standingHeight;
            _height = Mathf.MoveTowards(_height, targetHeight, deltaTime * 8f);
            ApplyHeight(_height);

            CombatantState state = frame.State;
            bool alive = frame.Alive;

            // The velocity the body is really moving with, in its own frame (x right, y forward): drives the run cycle.
            Vector2 local = Vector2.zero;
            if (_hasLast && deltaTime > 1e-4f)
            {
                Vector3 moved = position - _lastPosition;
                moved.y = 0f;
                Vector3 world = Vector3.ClampMagnitude(moved / deltaTime, 20f);
                Vector3 inBody = Quaternion.Inverse(_root.rotation) * world;
                local = new Vector2(inBody.x, inBody.z);
            }
            _lastPosition = position;
            _hasLast = true;
            _velocity = Vector2.Lerp(_velocity, alive ? local : Vector2.zero, 1f - Mathf.Exp(-deltaTime * 14f));

            // Rolling and dashing are server actions. A model with its own roll clip plays it; the primitive stand-in
            // is spun instead. The fall pose lies the body down when defeated.
            bool acting = frame.Posture == Posture.Action && alive;
            bool rolling = acting && _rollMove >= 0 && frame.ActiveMove == _rollMove;
            bool spinning = _model.HandlesRoll ? false : acting;
            _spinAngle = spinning ? (_spinAngle + deltaTime * 720f) % 360f
                : Mathf.MoveTowardsAngle(_spinAngle, 0f, deltaTime * 900f);
            _fall = Mathf.MoveTowards(_fall, alive ? 0f : 1f, deltaTime * 3f);
            float centre = _height * 0.5f;
            _spin.localPosition = new Vector3(0f, Mathf.Lerp(centre, _radius * 0.9f, _fall), 0f);
            _spin.localRotation = Quaternion.Euler(_spinAngle, 0f, _fall * 90f);
            _model.Body.localPosition = new Vector3(0f, -centre, 0f);

            if (alive != _wasAlive)
            {
                _wasAlive = alive;
                _model.SetDead(!alive);
            }

            var pose = new CharacterPose
            {
                DeltaTime = deltaTime,
                Height = _height,
                Velocity = _velocity,
                Airborne = frame.Posture == Posture.Airborne,
                Rolling = rolling,
                Fall = _fall,
                Pitch = frame.Pitch,
                SlugColor = SelectedSlugColor(state)
            };
            _model.Pose(in pose);

            UpdateShield(state, alive, deltaTime, lookup);
            UpdateTag(state, alive, camera, lookup);
            UpdateCourage(state, alive);
            _ring.gameObject.SetActive(alive);
        }

        // ------------------------------------------------------------------------------------------ parts

        private Color SelectedSlugColor(CombatantState state)
        {
            if (state != null && state.Slots.Count > 0)
            {
                int selected = (int)state.SelectedSlot;
                if (selected >= 0 && selected < state.Slots.Count)
                {
                    int slug = (int)state.Slots[selected].SlugIndex;
                    if (slug >= 0 && slug < _content.SlugCount)
                    {
                        return Palette.Element(_content.ElementName((int)_content.Slug(slug).Element));
                    }
                }
            }
            return Color.white;
        }

        private void ApplyHeight(float height)
        {
            float bubble = Mathf.Max(_radius * 2f, height) * 1.12f;
            _shieldBubble.localScale = new Vector3(Mathf.Max(_radius * 2.6f, bubble * 0.62f), bubble, Mathf.Max(_radius * 2.6f, bubble * 0.62f));
            _shieldBubble.localPosition = new Vector3(0f, height * 0.5f, 0f);
            _tag.localPosition = new Vector3(0f, height + 0.5f, 0f);
        }

        private void UpdateShield(CombatantState state, bool alive, float deltaTime, ContentLookup lookup)
        {
            _shieldBubble.gameObject.SetActive(alive);
            if (!alive)
            {
                return;
            }
            if (_lastShield >= 0f && state.Shield < _lastShield - 0.1f)
            {
                _shieldFlash = 1f;
            }
            _lastShield = state.Shield;
            _shieldFlash = Mathf.MoveTowards(_shieldFlash, 0f, deltaTime * 3.5f);

            float fraction = Mathf.Clamp01(state.Shield / lookup.MaxShield);
            bool over = state.Overshield > 0.5f;
            Color color = over ? Palette.Overshield : Palette.Shield;
            // Faint when the shield is low, a bright hit flash when it takes damage.
            color.a = (Mathf.Lerp(0.18f, 0.7f, fraction) * (over ? 1.3f : 1f)) * (_isYou ? 0.45f : 1f) + _shieldFlash * 0.6f;
            if (fraction <= 0.01f && !over)
            {
                color.a = 0f;
            }
            _shieldMaterial.color = color;
            _shieldMaterial.SetFloat("_Rim", 1.2f + _shieldFlash * 3.5f);
        }

        private void UpdateCourage(CombatantState state, bool alive)
        {
            int level = alive && state != null ? (int)state.Courage : 0;
            bool show = level > 0;
            if (_courage.gameObject.activeSelf != show)
            {
                _courage.gameObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }
            float time = Time.time;
            float breathe = 1f + 0.08f * Mathf.Sin(time * (4f + 2f * level));
            _courage.localRotation = Quaternion.Euler(90f, time * 60f, 0f);
            _courage.localScale = Vector3.one * (_radius * (5.6f + 0.8f * level) * breathe);
            _courageBlock.SetColor("_Color", Palette.WithAlpha(Palette.Courage, 0.55f + 0.25f * level * breathe));
            _courageRenderer.SetPropertyBlock(_courageBlock);
        }

        private void UpdateTag(CombatantState state, bool alive, Camera camera, ContentLookup lookup)
        {
            if (_isYou)
            {
                return;
            }
            bool visible = alive;
            if (visible && camera != null)
            {
                Vector3 toTag = _tag.position - camera.transform.position;
                float distance = toTag.magnitude;
                visible = distance < TagMaxDistance;
                if (visible && distance > 0.5f)
                {
                    // Enemies behind a wall are not marked; allies always are.
                    Vector3 direction = toTag / distance;
                    Vector3 eye = camera.transform.position;
                    float clear = _map.SightDistance(new Vec3(eye.x, eye.y, eye.z),
                        new Vec3(direction.x, direction.y, direction.z), distance, 0f);
                    visible = _ally || clear >= distance - 0.01f;
                }
            }
            _tag.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }
            if (camera != null)
            {
                _tag.rotation = camera.transform.rotation;
                // Keep the tag a readable size at any range.
                float distance = Vector3.Distance(camera.transform.position, _tag.position);
                _tag.localScale = Vector3.one * Mathf.Clamp(distance / 14f, 0.8f, 1.9f);
            }
            SetFill(_hpFill, state.Hp / lookup.MaxHp);
            SetFill(_shieldFill, state.Shield / lookup.MaxShield);
            SetFill(_overFill, state.Overshield / lookup.MaxShield);

            int count = Mathf.Min(state.Statuses.Count, MaxStatusDots);
            for (int i = 0; i < MaxStatusDots; i++)
            {
                bool active = i < count;
                _statusDots[i].gameObject.SetActive(active);
                if (active)
                {
                    _statusRenderers[i].sharedMaterial = MaterialFactory.Unlit(Palette.Status(state.Statuses[i].Id));
                }
            }
        }

        private static void SetFill(Transform fill, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            fill.localScale = new Vector3(Mathf.Max(0.0001f, BarWidth * fraction), fill.localScale.y, 1f);
            fill.localPosition = new Vector3(-BarWidth * 0.5f + BarWidth * fraction * 0.5f, fill.localPosition.y, -0.001f);
        }

        private void BuildTag(string playerName, Color teamColor, out Transform hp, out Transform shield,
            out Transform over, out TextMesh name, out TextMesh shadow)
        {
            shadow = TagLabel("NameShadow", playerName, new Vector3(0.012f, 0.585f, 0.002f),
                new Color(0f, 0f, 0f, 0.85f));
            name = TagLabel("Name", playerName, new Vector3(0f, 0.6f, 0f), Color.Lerp(teamColor, Color.white, 0.55f));

            Quad("Back", new Vector3(0f, 0.04f, 0.004f), new Vector3(BarWidth + 0.1f, 0.34f, 1f),
                new Color(0.02f, 0.03f, 0.05f, 0.75f));
            Quad("Edge", new Vector3(0f, 0.04f, 0.006f), new Vector3(BarWidth + 0.02f, 0.28f, 1f),
                new Color(0f, 0f, 0f, 0.55f));
            hp = Quad("Health", new Vector3(0f, 0.12f, 0f), new Vector3(BarWidth, 0.12f, 1f), Palette.Health);
            shield = Quad("Shield", new Vector3(0f, -0.01f, 0f), new Vector3(BarWidth, 0.08f, 1f), Palette.Shield);
            over = Quad("Overshield", new Vector3(0f, -0.09f, 0f), new Vector3(BarWidth, 0.04f, 1f),
                Palette.Overshield);
        }

        private TextMesh TagLabel(string objectName, string text, Vector3 position, Color color)
        {
            GameObject label = new GameObject(objectName, typeof(TextMesh));
            label.transform.SetParent(_tag, false);
            label.transform.localPosition = position;
            TextMesh mesh = label.GetComponent<TextMesh>();
            mesh.font = UiKit.Font;
            label.GetComponent<MeshRenderer>().sharedMaterial = UiKit.Font.material;
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = 0.065f;
            mesh.fontSize = 64;
            mesh.color = color;
            return mesh;
        }

        private Transform Quad(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject quad = Gfx.Primitive(PrimitiveType.Quad, name, _tag, MaterialFactory.Unlit(color));
            quad.transform.localPosition = position;
            quad.transform.localScale = scale;
            return quad.transform;
        }
    }

    /// <summary>Per-character numbers a view needs to turn raw vitals into bar fractions.</summary>
    public readonly struct ContentLookup
    {
        public readonly float MaxHp;
        public readonly float MaxShield;

        public ContentLookup(float maxHp, float maxShield)
        {
            MaxHp = Mathf.Max(1f, maxHp);
            MaxShield = Mathf.Max(1f, maxShield);
        }
    }
}
