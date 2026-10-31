using System;
using Bajoterra.Client.Content;
using Bajoterra.Protocol;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Turns the server's gameplay events into what the player sees and hears: muzzle flashes, impacts with sparks
    /// and shock rings, explosions (a grenade's is bigger and louder), floating damage and healing numbers (gold with
    /// a "!" for headshots), defeat bursts, courage auras, hit markers and screen shake, plus the sound. It owns pools of particle systems, flashes, rings, bubbles and text meshes, so
    /// firefights allocate nothing per event. The events say where and how much; this class only decides how it
    /// looks.
    /// </summary>
    public sealed class VfxPlayer
    {
        private const int BurstPoolSize = 24;
        private const int FlashPoolSize = 10;
        private const int RingPoolSize = 12;
        private const int ExplosionPoolSize = 8;
        private const int NumberPoolSize = 32;

        private sealed class Flash
        {
            public Transform Halo;
            public Renderer HaloRenderer;
            public Light Light;
            public float Age = 99f;
            public float Duration;
            public float Size;
            public float Intensity;
            public Color Color;
        }

        private sealed class Ring
        {
            public Transform Root;
            public Renderer Renderer;
            public float Age = 99f;
            public float Duration;
            public float Radius;
            public Color Color;
        }

        private sealed class Explosion
        {
            public Transform Root;
            public Material Material;
            public float Age = 99f;
            public float Radius;
            public Color Color;
        }

        private sealed class FloatingNumber
        {
            public TextMesh Text;
            public TextMesh Shadow;
            public float Age = 99f;
            public float Life = 1f;
            public Vector3 Start;
            public Color Color;
        }

        private readonly Transform _root;
        private readonly ContentCatalog _content;
        private readonly ArenaMap _map;
        private readonly AudioPlayer _audio;
        private readonly CombatantViews _views;
        private readonly int _yourId;
        private readonly int _yourTeam;
        private readonly Func<int, int> _teamOf;
        private readonly ParticleSystem[] _bursts = new ParticleSystem[BurstPoolSize];
        private readonly Flash[] _flashes = new Flash[FlashPoolSize];
        private readonly Ring[] _rings = new Ring[RingPoolSize];
        private readonly Explosion[] _explosions = new Explosion[ExplosionPoolSize];
        private readonly FloatingNumber[] _numbers = new FloatingNumber[NumberPoolSize];
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private int _nextBurst;
        private int _nextFlash;
        private int _nextRing;
        private int _nextExplosion;
        private int _nextNumber;
        private Vector3 _cameraPosition;

        /// <summary>Raised when the local player takes damage (the HUD flashes the screen).</summary>
        public event Action<float> YouWereHit;

        /// <summary>Raised when a slug of the local player hits an enemy; the flag says it was the last hit.</summary>
        public event Action<bool> YouHitAnEnemy;

        /// <summary>Raised when a slug of the local player hits an enemy in the head.</summary>
        public event Action YouLandedACritical;

        /// <summary>Raised with (new level, previous level) when the local player's courage changes (0 = it ended).</summary>
        public event Action<int, int> YourCourageChanged;

        /// <summary>Raised with a strength in metres when the camera should shake.</summary>
        public event Action<float> Shake;

        public VfxPlayer(Transform parent, ContentCatalog content, ArenaMap map, AudioPlayer audio,
            CombatantViews views, int yourId, int yourTeam, Func<int, int> teamOf)
        {
            _root = new GameObject("Effects").transform;
            _root.SetParent(parent, false);
            _content = content;
            _map = map;
            _audio = audio;
            _views = views;
            _yourId = yourId;
            _yourTeam = yourTeam;
            _teamOf = teamOf;
            for (int i = 0; i < BurstPoolSize; i++)
            {
                _bursts[i] = CreateBurst(i);
            }
            for (int i = 0; i < FlashPoolSize; i++)
            {
                _flashes[i] = CreateFlash(i);
            }
            for (int i = 0; i < RingPoolSize; i++)
            {
                _rings[i] = CreateRing(i);
            }
            for (int i = 0; i < ExplosionPoolSize; i++)
            {
                _explosions[i] = CreateExplosion(i);
            }
            for (int i = 0; i < NumberPoolSize; i++)
            {
                _numbers[i] = CreateNumber(i);
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
        }

        // ------------------------------------------------------------------------------------------ events

        public void Handle(GameEvent e)
        {
            var position = new Vector3(e.X, e.Y, e.Z);
            switch (e.Kind)
            {
                case EventKind.Shot:
                    OnShot(e, position);
                    break;
                case EventKind.ProjectileHit:
                    OnProjectileHit(e, position);
                    break;
                case EventKind.ProjectileWorldHit:
                    {
                        Color color = Color.Lerp(SlugColor(e.A), new Color(0.8f, 0.8f, 0.85f), 0.5f);
                        if (IsSticky(e.A))
                        {
                            // A grenade does not burst on landing: it splats and sticks.
                            Burst(position, color, 10, 2f, 0.1f);
                            _audio.PlayAt(_audio.Stick, position, 0.8f, Random(0.9f, 1.1f));
                            break;
                        }
                        Burst(position, color, 18, 3.8f, 0.12f);
                        ShockRing(position, color, 0.9f, 0.22f);
                        FlashAt(position, SlugColor(e.A), 1.6f, 0.1f, 1.4f);
                        _audio.PlayAt(_audio.WorldHit, position, 0.6f, Random(0.85f, 1.15f));
                        break;
                    }
                case EventKind.ProjectileExpired:
                    Burst(position, SlugColor(e.A), 10, 1.6f, 0.1f);
                    break;
                case EventKind.Explosion:
                    if (IsSticky(e.A))
                    {
                        GrenadeBlast(position, e.Value);
                    }
                    else
                    {
                        Explode(position, e.Value);
                    }
                    break;
                case EventKind.Flash:
                    OnFlash(position, e.Value);
                    break;
                case EventKind.BarrierRaised:
                    // Frost bursting out of the floor as the ice shoots up.
                    Burst(position + Vector3.up * 0.3f, new Color(0.75f, 0.97f, 1f), 50, 4.5f, 0.2f);
                    ShockRing(position + Vector3.up * 0.05f, new Color(0.6f, 0.95f, 1f), 2.4f, 0.4f);
                    _audio.PlayAt(_audio.IceForm, position, 0.9f, Random(0.95f, 1.05f));
                    break;
                case EventKind.BarrierHit:
                    Burst(position, new Color(0.85f, 1f, 1f), 26, 5f, 0.12f);
                    _audio.PlayAt(_audio.IceCrack, position, 0.85f, Random(0.9f, 1.15f));
                    break;
                case EventKind.BarrierGone:
                    if (e.A == 1)
                    {
                        // Shattered: a spray of shards and a crash.
                        Burst(position + Vector3.up * 1.1f, new Color(0.85f, 1f, 1f), 90, 7f, 0.22f);
                        Burst(position + Vector3.up * 0.6f, new Color(0.6f, 0.9f, 1f), 40, 3f, 0.4f);
                        FlashAt(position + Vector3.up * 1f, new Color(0.7f, 0.95f, 1f), 3f, 0.2f, 3f);
                        _audio.PlayAt(_audio.IceShatter, position, 1f, Random(0.95f, 1.05f));
                    }
                    else
                    {
                        // Melted away: a last puff of cold mist.
                        Burst(position + Vector3.up * 0.3f, new Color(0.8f, 0.95f, 1f), 18, 1.2f, 0.45f);
                        _audio.PlayAt(_audio.Splish, position, 0.4f, 0.8f);
                    }
                    break;
                case EventKind.Damage:
                    OnDamage(e);
                    break;
                case EventKind.Heal:
                    Amount(e.Subject, e.Value, Palette.Health, _audio.Heal);
                    HealBurst(e.Subject, Palette.Health);
                    break;
                case EventKind.ShieldRestored:
                    Amount(e.Subject, e.Value, Palette.Shield, _audio.Shield);
                    HealBurst(e.Subject, Palette.Shield);
                    break;
                case EventKind.Overshield:
                    Amount(e.Subject, e.Value, Palette.Overshield, _audio.Shield);
                    HealBurst(e.Subject, Palette.Overshield);
                    break;
                case EventKind.Defeated:
                    OnDefeated(e, position);
                    break;
                case EventKind.PickupTaken:
                    OnPickup(e);
                    break;
                case EventKind.MoveStarted:
                    OnMove(e);
                    break;
                case EventKind.Courage:
                    OnCourage(e);
                    break;
                case EventKind.RoundStarted:
                    _audio.PlayUi(_audio.GoBeep, 0.8f);
                    break;
                case EventKind.RoundEnded:
                case EventKind.MatchEnded:
                    _audio.PlayUi(_audio.Whistle, 0.8f);
                    break;
            }
        }

        /// <summary>Advances every animated effect; call once per frame.</summary>
        public void Update(float deltaTime, Camera camera)
        {
            if (camera != null)
            {
                _cameraPosition = camera.transform.position;
            }
            UpdateNumbers(deltaTime, camera);
            UpdateFlashes(deltaTime);
            UpdateRings(deltaTime);
            UpdateExplosions(deltaTime);
        }

        // ------------------------------------------------------------------------------------------ handlers

        private void OnShot(GameEvent e, Vector3 muzzle)
        {
            Color color = SlugColor(e.A);
            Burst(muzzle, color, 12, 2.8f, 0.09f);
            FlashAt(muzzle, color, e.Actor == _yourId ? 1.4f : 1.9f, 0.07f, 2.4f);
            AudioClip clip = _audio.Shots[UnityEngine.Random.Range(0, _audio.Shots.Length)];
            _audio.PlayAt(clip, muzzle, e.Actor == _yourId ? 0.7f : 0.9f, Random(0.95f, 1.05f));
        }

        private void OnProjectileHit(GameEvent e, Vector3 position)
        {
            Color color = SlugColor(e.A);
            bool critical = e.B == 1;
            if (IsSticky(e.A))
            {
                Burst(position, color, 12, 2.4f, 0.1f);
                _audio.PlayAt(_audio.Stick, position, 0.9f, Random(0.9f, 1.1f));
            }
            else
            {
                Burst(position, critical ? Palette.Critical : color, critical ? 44 : 30, 5.5f, 0.15f);
                ShockRing(position, color, 1.3f, 0.28f);
                FlashAt(position, color, 2.4f, 0.12f, 2.2f);
                _audio.PlayAt(_audio.Hit, position, 0.8f, Random(0.9f, 1.1f));
            }
            if (critical)
            {
                ShockRing(position, Palette.Critical, 0.8f, 0.2f);
                _audio.PlayAt(_audio.Critical, position, 0.75f, Random(0.97f, 1.03f));
            }
            if (e.Actor == _yourId && e.Subject != _yourId && _teamOf(e.Subject) != _yourTeam)
            {
                YouHitAnEnemy?.Invoke(false);
                _audio.PlayUi(_audio.HitMarker, 0.7f);
                if (critical)
                {
                    YouLandedACritical?.Invoke();
                    _audio.PlayUi(_audio.Critical, 0.6f);
                }
            }
        }

        private void OnCourage(GameEvent e)
        {
            bool gained = e.A > e.B;
            if (gained && _views.TryGetCenter(e.Subject, out Vector3 center))
            {
                Burst(center, Palette.Courage, 40, 4f, 0.16f);
                ShockRing(center - Vector3.up * (center.y - 0.1f), Palette.Courage, 2.6f, 0.5f);
                FlashAt(center, Palette.Courage, 3f, 0.25f, 2.5f);
            }
            if (e.Subject == _yourId)
            {
                if (gained)
                {
                    _audio.PlayUi(_audio.Courage, 0.75f);
                }
                YourCourageChanged?.Invoke(e.A, e.B);
            }
        }

        private void OnDamage(GameEvent e)
        {
            if (e.B == 1)
            {
                Amount(e.Subject, e.Value, Palette.Critical, null, true);
            }
            else
            {
                Amount(e.Subject, e.Value, Palette.Damage, null);
            }
            if (e.Subject == _yourId)
            {
                YouWereHit?.Invoke(e.Value);
                Shake?.Invoke(Mathf.Clamp(0.05f + e.Value / 250f, 0.05f, 0.25f));
            }
        }

        private void OnDefeated(GameEvent e, Vector3 position)
        {
            Color color = new Color(1f, 0.8f, 0.6f);
            Burst(position + Vector3.up * 0.9f, color, 70, 8f, 0.22f);
            ShockRing(position + Vector3.up * 0.1f, color, 3.2f, 0.5f);
            FlashAt(position + Vector3.up * 0.9f, color, 4.5f, 0.2f, 2.8f);
            _audio.PlayAt(_audio.Defeat, position, 1f);
            if (e.Actor == _yourId && e.Subject != _yourId && _teamOf(e.Subject) != _yourTeam)
            {
                YouHitAnEnemy?.Invoke(true);
            }
        }

        private void OnPickup(GameEvent e)
        {
            if (e.A < 0 || e.A >= _map.Pickups.Length)
            {
                return;
            }
            PickupSlot slot = _map.Pickups[e.A];
            Vector3 at = slot.Position.ToUnity() + Vector3.up * 0.9f;
            Color color = Palette.Pickup(_content.Pickup(slot.PickupIndex).Id);
            Burst(at, color, 34, 3.2f, 0.14f);
            ShockRing(slot.Position.ToUnity() + Vector3.up * 0.1f, color, 2.2f, 0.45f);
            FlashAt(at, color, 3f, 0.2f, 2f);
            _audio.PlayAt(_audio.Pickup, at, 0.9f);
        }

        private void OnMove(GameEvent e)
        {
            if (e.A < 0 || e.A >= _content.Details.Moves.Count || !_views.TryGetCenter(e.Actor, out Vector3 center))
            {
                return;
            }
            Vector3 feet = center - Vector3.up * (center.y - 0.05f);
            string id = _content.Move(e.A).Id;
            Color dust = new Color(0.75f, 0.78f, 0.85f);
            if (id == "jump")
            {
                Burst(feet, dust, 10, 2.2f, 0.13f);
                ShockRing(feet, dust, 1.1f, 0.25f);
                _audio.PlayAt(_audio.Jump, feet, 0.45f);
            }
            else if (id == "roll" || id == "dash")
            {
                Burst(feet, dust, 14, 2.4f, 0.13f);
                _audio.PlayAt(_audio.Roll, feet, 0.55f);
            }
        }

        private void HealBurst(int subject, Color color)
        {
            if (_views.TryGetCenter(subject, out Vector3 center))
            {
                Burst(center, color, 16, 1.8f, 0.1f);
                ShockRing(center - Vector3.up * (center.y - 0.1f), color, 1.5f, 0.4f);
            }
        }

        private void Amount(int subject, float value, Color color, AudioClip sound, bool critical = false)
        {
            if (value < 0.5f || !_views.TryGetHead(subject, out Vector3 head))
            {
                return;
            }
            FloatingNumber number = _numbers[_nextNumber];
            _nextNumber = (_nextNumber + 1) % NumberPoolSize;
            number.Age = 0f;
            number.Life = 0.95f + Mathf.Clamp01(value / 80f) * 0.35f;
            number.Start = head + new Vector3(Random(-0.35f, 0.35f), 0f, Random(-0.35f, 0.35f));
            number.Color = color;
            string text = Mathf.RoundToInt(value) + (critical ? "!" : string.Empty);
            float size = (0.075f + Mathf.Clamp01(value / 90f) * 0.07f) * (critical ? 1.45f : 1f);
            number.Text.text = text;
            number.Shadow.text = text;
            number.Text.characterSize = size;
            number.Shadow.characterSize = size;
            number.Text.color = color;
            number.Text.gameObject.SetActive(true);
            number.Shadow.gameObject.SetActive(true);
            if (sound != null)
            {
                _audio.PlayAt(sound, head, 0.6f);
            }
        }

        private void Explode(Vector3 center, float radius)
        {
            Explosion explosion = _explosions[_nextExplosion];
            _nextExplosion = (_nextExplosion + 1) % ExplosionPoolSize;
            explosion.Age = 0f;
            explosion.Radius = Mathf.Max(0.5f, radius);
            explosion.Color = new Color(1f, 0.62f, 0.25f);
            explosion.Root.position = center;
            explosion.Root.gameObject.SetActive(true);
            Burst(center, new Color(1f, 0.6f, 0.2f), 60, Mathf.Max(3.5f, radius * 2.4f), 0.3f);
            Burst(center, new Color(1f, 0.9f, 0.6f), 24, Mathf.Max(2f, radius), 0.4f);
            ShockRing(center, explosion.Color, explosion.Radius * 2.2f, 0.45f);
            FlashAt(center, explosion.Color, explosion.Radius * 3.2f, 0.28f, 3.5f);
            _audio.PlayAt(_audio.Explosion, center, 1f, Random(0.9f, 1.05f));
            float distance = Vector3.Distance(center, _cameraPosition);
            Shake?.Invoke(0.4f * Mathf.Clamp01(1f - distance / 28f));
        }

        /// <summary>
        /// A grenade going off: a much bigger fireball than a slug's area hit, two waves of sparks, a grey puff of
        /// smoke, a wide shock ring, the heavy blast sound and a strong shake when it is close to the camera.
        /// </summary>
        private void GrenadeBlast(Vector3 center, float radius)
        {
            Explosion explosion = _explosions[_nextExplosion];
            _nextExplosion = (_nextExplosion + 1) % ExplosionPoolSize;
            explosion.Age = 0f;
            explosion.Radius = Mathf.Max(0.8f, radius * 0.85f);
            explosion.Color = new Color(1f, 0.5f, 0.15f);
            explosion.Root.position = center;
            explosion.Root.gameObject.SetActive(true);
            Burst(center, new Color(1f, 0.55f, 0.15f), 110, radius * 3.2f, 0.34f);
            Burst(center, new Color(1f, 0.92f, 0.55f), 40, radius * 1.6f, 0.5f);
            Burst(center, new Color(0.35f, 0.33f, 0.32f), 36, radius * 0.9f, 0.7f);
            ShockRing(center, explosion.Color, radius * 1.15f, 0.5f);
            ShockRing(center, Palette.Danger, radius * 0.7f, 0.3f);
            FlashAt(center, explosion.Color, radius * 4f, 0.35f, 5f);
            _audio.PlayAt(_audio.GrenadeBlast, center, 1f, Random(0.94f, 1.04f));
            float distance = Vector3.Distance(center, _cameraPosition);
            Shake?.Invoke(0.6f * Mathf.Clamp01(1f - distance / 32f));
        }

        /// <summary>
        /// A Phosphoro going off: a burst of white light that floods the whole cavern for a moment (the walls light
        /// up even for those who looked away), a ring of sparkles and a sharp electric bang.
        /// </summary>
        private void OnFlash(Vector3 center, float radius)
        {
            Color light = new Color(0.85f, 1f, 1f);
            FlashAt(center, light, Mathf.Max(6f, radius * 0.55f), 0.6f, 9f);
            FlashAt(center, Color.white, 4.5f, 0.22f, 14f);
            Burst(center, new Color(0.8f, 1f, 1f), 70, 9f, 0.22f);
            Burst(center, new Color(1f, 0.97f, 0.7f), 30, 4f, 0.4f);
            ShockRing(center, light, 3.2f, 0.35f);
            _audio.PlayAt(_audio.FlashBang, center, 1f, Random(0.96f, 1.04f));
            float distance = Vector3.Distance(center, _cameraPosition);
            Shake?.Invoke(0.15f * Mathf.Clamp01(1f - distance / 20f));
        }

        private bool IsSticky(int slugIndex)
        {
            return slugIndex >= 0 && slugIndex < _content.SlugCount && _content.Slug(slugIndex).Sticky;
        }

        private Color SlugColor(int slugIndex)
        {
            if (slugIndex < 0 || slugIndex >= _content.SlugCount)
            {
                return Color.white;
            }
            return Palette.Element(_content.ElementName((int)_content.Slug(slugIndex).Element));
        }

        // ------------------------------------------------------------------------------------------ primitives

        private void Burst(Vector3 position, Color color, int count, float speed, float size)
        {
            ParticleSystem system = _bursts[_nextBurst];
            _nextBurst = (_nextBurst + 1) % BurstPoolSize;
            ParticleSystem.MainModule main = system.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                startColor = color
            };
            system.Emit(emit, count);
        }

        private void FlashAt(Vector3 position, Color color, float size, float duration, float intensity)
        {
            Flash flash = _flashes[_nextFlash];
            _nextFlash = (_nextFlash + 1) % FlashPoolSize;
            flash.Age = 0f;
            flash.Duration = duration;
            flash.Size = size;
            flash.Intensity = intensity;
            flash.Color = color;
            flash.Halo.position = position;
            flash.Light.transform.position = position;
            flash.Halo.gameObject.SetActive(true);
            flash.Light.enabled = true;
        }

        private void ShockRing(Vector3 position, Color color, float radius, float duration)
        {
            Ring ring = _rings[_nextRing];
            _nextRing = (_nextRing + 1) % RingPoolSize;
            ring.Age = 0f;
            ring.Duration = duration;
            ring.Radius = radius;
            ring.Color = color;
            ring.Root.position = position;
            ring.Root.gameObject.SetActive(true);
        }

        private void UpdateFlashes(float deltaTime)
        {
            for (int i = 0; i < FlashPoolSize; i++)
            {
                Flash flash = _flashes[i];
                if (flash.Age > flash.Duration)
                {
                    continue;
                }
                flash.Age += deltaTime;
                float t = Mathf.Clamp01(flash.Age / flash.Duration);
                if (t >= 1f)
                {
                    flash.Halo.gameObject.SetActive(false);
                    flash.Light.enabled = false;
                    flash.Age = 99f;
                    continue;
                }
                float fade = 1f - t;
                flash.Halo.localScale = Vector3.one * flash.Size * (0.6f + 0.6f * t);
                _block.SetColor("_Color", new Color(flash.Color.r, flash.Color.g, flash.Color.b, fade));
                flash.HaloRenderer.SetPropertyBlock(_block);
                flash.Light.color = flash.Color;
                flash.Light.intensity = flash.Intensity * fade;
                flash.Light.range = 4f + flash.Size * 2.2f;
            }
        }

        private void UpdateRings(float deltaTime)
        {
            for (int i = 0; i < RingPoolSize; i++)
            {
                Ring ring = _rings[i];
                if (ring.Age > ring.Duration)
                {
                    continue;
                }
                ring.Age += deltaTime;
                float t = Mathf.Clamp01(ring.Age / ring.Duration);
                if (t >= 1f)
                {
                    ring.Root.gameObject.SetActive(false);
                    ring.Age = 99f;
                    continue;
                }
                float eased = 1f - (1f - t) * (1f - t);
                ring.Root.localScale = Vector3.one * (ring.Radius * 2f * eased + 0.1f);
                _block.SetColor("_Color", new Color(ring.Color.r, ring.Color.g, ring.Color.b, (1f - t) * 0.9f));
                ring.Renderer.SetPropertyBlock(_block);
            }
        }

        private void UpdateExplosions(float deltaTime)
        {
            for (int i = 0; i < ExplosionPoolSize; i++)
            {
                Explosion explosion = _explosions[i];
                if (explosion.Age > 0.45f)
                {
                    continue;
                }
                explosion.Age += deltaTime;
                bool alive = explosion.Age <= 0.45f;
                explosion.Root.gameObject.SetActive(alive);
                if (!alive)
                {
                    continue;
                }
                float t = explosion.Age / 0.45f;
                explosion.Root.localScale = Vector3.one * (explosion.Radius * 2f * Mathf.Sqrt(t));
                Color color = explosion.Color;
                color.a = 1.1f * (1f - t);
                explosion.Material.color = color;
            }
        }

        private void UpdateNumbers(float deltaTime, Camera camera)
        {
            for (int i = 0; i < NumberPoolSize; i++)
            {
                FloatingNumber number = _numbers[i];
                if (number.Age > number.Life)
                {
                    continue;
                }
                number.Age += deltaTime;
                bool alive = number.Age <= number.Life;
                number.Text.gameObject.SetActive(alive);
                number.Shadow.gameObject.SetActive(alive);
                if (!alive)
                {
                    number.Age = 99f;
                    continue;
                }
                float t = number.Age / number.Life;
                // Pop up fast, then drift.
                float rise = (1f - (1f - t) * (1f - t)) * 1.6f;
                Vector3 at = number.Start + Vector3.up * rise;
                number.Text.transform.position = at;
                if (camera != null)
                {
                    number.Text.transform.rotation = camera.transform.rotation;
                    // Constant on-screen size: grow with distance.
                    float distance = Vector3.Distance(_cameraPosition, at);
                    number.Text.transform.localScale = Vector3.one * Mathf.Clamp(distance / 12f, 0.8f, 2.4f)
                                                       * (1f + 0.4f * Mathf.Clamp01(1f - t * 6f));
                }
                Color color = number.Color;
                color.a = 1f - t * t * t;
                number.Text.color = color;
                number.Shadow.color = new Color(0f, 0f, 0f, color.a * 0.85f);
            }
        }

        // ------------------------------------------------------------------------------------------ pools

        private ParticleSystem CreateBurst(int index)
        {
            var go = new GameObject("Burst " + index, typeof(ParticleSystem));
            go.transform.SetParent(_root, false);
            ParticleSystem system = go.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            main.gravityModifier = 0.6f;
            main.maxParticles = 300;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystem.SizeOverLifetimeModule shrink = system.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialFactory.Glow();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return system;
        }

        private Flash CreateFlash(int index)
        {
            Transform halo = Gfx.Halo(_root, "Flash " + index, Vector3.zero, Color.white, 1f, Textures.Flare);
            Light light = Gfx.AddPointLight(_root, "Flash light " + index, Vector3.zero, Color.white, 6f, 0f);
            light.enabled = false;
            halo.gameObject.SetActive(false);
            return new Flash { Halo = halo, HaloRenderer = halo.GetComponent<Renderer>(), Light = light };
        }

        private Ring CreateRing(int index)
        {
            GameObject quad = Gfx.Make("Ring " + index, Billboard.QuadMesh(), _root,
                MaterialFactory.GlowInstance(Textures.Ring, 1.5f));
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Gfx.NoShadows(quad);
            quad.SetActive(false);
            return new Ring { Root = quad.transform, Renderer = quad.GetComponent<Renderer>() };
        }

        private Explosion CreateExplosion(int index)
        {
            GameObject sphere = Gfx.Primitive(PrimitiveType.Sphere, "Explosion " + index, _root, null);
            Material material = MaterialFactory.Fresnel(new Color(1f, 0.7f, 0.3f, 1f), 1.8f, 1.2f, 0.18f);
            sphere.GetComponent<Renderer>().sharedMaterial = material;
            Gfx.NoShadows(sphere);
            sphere.SetActive(false);
            return new Explosion { Root = sphere.transform, Material = material };
        }

        private FloatingNumber CreateNumber(int index)
        {
            var go = new GameObject("Number " + index, typeof(TextMesh));
            go.transform.SetParent(_root, false);
            TextMesh text = StyleNumber(go);
            var shadowObject = new GameObject("Shadow", typeof(TextMesh));
            shadowObject.transform.SetParent(go.transform, false);
            shadowObject.transform.localPosition = new Vector3(0.015f, -0.015f, 0.01f);
            TextMesh shadow = StyleNumber(shadowObject);
            go.SetActive(false);
            return new FloatingNumber { Text = text, Shadow = shadow };
        }

        private static TextMesh StyleNumber(GameObject go)
        {
            TextMesh text = go.GetComponent<TextMesh>();
            text.font = UiKit.DisplayFont;
            go.GetComponent<MeshRenderer>().sharedMaterial = UiKit.DisplayFont.material;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.09f;
            text.fontSize = 64;
            return text;
        }

        private static float Random(float min, float max)
        {
            return UnityEngine.Random.Range(min, max);
        }
    }
}
