using System.Text;
using Bajoterra.Client;
using Bajoterra.Client.Hud;
using Bajoterra.Client.Input;
using Bajoterra.Client.Interpolation;
using Bajoterra.Client.Session;
using Bajoterra.Protocol;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Everything that happens on screen during one match, wired together: it reads the player's controls, steers the
    /// camera, sends one input per fixed tick, draws the world from the interpolated frame and feeds the HUD. A new
    /// presenter is created when a match starts and destroyed when it ends.
    /// </summary>
    public sealed class MatchPresenter
    {
        private readonly GameClient _client;
        private readonly MatchSession _match;
        private readonly GameSettings _settings;
        private readonly IControls _controls;
        private readonly CameraRig _camera;
        private readonly HudScreen _hud;
        private readonly AudioPlayer _audio;
        private readonly Transform _root;
        private readonly ArenaView _arena;
        private readonly WaterView _water;
        private readonly BarrierViews _barriers;
        private readonly FireTrailViews _fire;
        private readonly CombatantViews _combatants;
        private readonly ProjectileViews _projectiles;
        private readonly VfxPlayer _vfx;
        private readonly HudModel _model = new HudModel();
        private readonly MoveMask _moveMask;
        private readonly OneShotInputs _oneShots = new OneShotInputs();
        private readonly FixedStepClock _inputClock = new FixedStepClock();
        private readonly StringBuilder _debug = new StringBuilder(256);
        private string _debugText = string.Empty;
        private float _debugTimer;
        private float _smoothedFrame = 1f / 60f;
        private int _lastCountdown = -1;
        private int _spectateId = -1;
        private bool _wasAlive = true;
        private float _lastBlind;

        public MatchPresenter(GameClient client, GameSettings settings, IControls controls, Camera camera,
            HudScreen hud, AudioPlayer audio, Transform parent)
        {
            _client = client;
            _match = client.Match;
            _settings = settings;
            _controls = controls;
            _camera = new CameraRig(camera);
            _hud = hud;
            _audio = audio;
            _root = new GameObject("Match").transform;
            _root.SetParent(parent, false);

            _arena = new ArenaView(_root, _match.Map, _match.Content, _match.YourTeam);
            _water = new WaterView(_root, _match.Map, _match.Content, audio);
            _barriers = new BarrierViews(_root);
            _fire = new FireTrailViews(_root, _match.Content, audio);
            _combatants = new CombatantViews(_root, _match);
            _projectiles = new ProjectileViews(_root, _match.Content, _match.Map, audio);
            _vfx = new VfxPlayer(_root, _match.Content, _match.Map, audio, _combatants, _match.YourId, _match.YourTeam,
                _match.TeamOf);
            _moveMask = new MoveMask(_match.Content, _match.Content.FindCharacter(_match.You.CharacterId));
            _match.Prediction.Enabled = settings.Prediction;

            _match.EventPlayed += _vfx.Handle;
            _match.EventPlayed += _water.Handle;
            _match.EventPlayed += OnEvent;
            _vfx.YouWereHit += _hud.Flash;
            _vfx.YouHitAnEnemy += _hud.HitMarker;
            _vfx.YouLandedACritical += _hud.Critical;
            _vfx.YourCourageChanged += OnCourage;
            _vfx.Shake += _camera.Shake;
            _camera.SetAim(_match.Map.Spawns.Length > 0 ? SpawnYaw() : 0f, 0.15f);
            _hud.SetStatsVisible(settings.ShowStats);
            _hud.SetDefeat(false, null, Color.white, null);
        }

        /// <summary>While paused the game keeps running but ignores the controls.</summary>
        public bool Paused { get; set; }

        /// <summary>Switches client-side movement prediction on or off (the options menu).</summary>
        public void ApplyPrediction(bool enabled)
        {
            _match.Prediction.Enabled = enabled;
        }

        /// <summary>Points the presenter at a different Unity camera (the game had to make a new one).</summary>
        public void RebindCamera(Camera camera)
        {
            _camera.Rebind(camera);
        }

        /// <summary>Runs one rendered frame. Call after <see cref="GameClient.Update"/>.</summary>
        public void Frame(float deltaTime)
        {
            _controls.Poll();
            if (_controls.DebugPressed)
            {
                _hud.ToggleDebug();
            }
            _hud.SetScoreboard(_controls.ScoreboardHeld && !Paused);
            WorldFrame frame = _match.Frame;
            if (!frame.Valid)
            {
                return;
            }
            int ownIndex = frame.IndexOf(_match.YourId);
            bool alive = ownIndex >= 0 && frame.Combatants[ownIndex].Alive;
            UpdateDefeatPanel(alive);

            if (!Paused && alive)
            {
                _camera.Look(_controls.Look, _settings.LookSensitivity, _settings.InvertY);
            }
            PlaceCamera(frame, ownIndex, alive);
            if (!Paused)
            {
                ReadOneShots();
            }
            SendInputs(deltaTime, alive);

            float time = Time.time;
            _combatants.Apply(frame, deltaTime, _camera.Camera);
            _projectiles.Apply(frame, time);
            _arena.Animate(frame, time);
            _water.Apply(frame, deltaTime);
            _barriers.Apply(_match.Latest, deltaTime);
            _fire.Apply(_match.Latest, deltaTime, _camera.Camera != null ? _camera.Camera.transform.position : Vector3.zero);
            _vfx.Update(deltaTime, _camera.Camera);

            _model.Refresh(_match, _client.PingMs);
            HearTheFlash();
            AnnounceCountdown();
            UpdateDebug(deltaTime);
            _hud.Refresh(_model, _match, deltaTime, _debugText);
        }

        /// <summary>A flash that blinds you also leaves your ears ringing and the world muffled while it lasts.</summary>
        private void HearTheFlash()
        {
            float blind = _model.BlindLevel;
            if (blind > 0.25f && _lastBlind < 0.05f)
            {
                _audio.PlayUi(_audio.Tinnitus, 0.3f + 0.5f * blind);
            }
            _lastBlind = blind;
            _audio.Muffle = blind;
        }

        public void Destroy()
        {
            _audio.Muffle = 0f;
            _match.EventPlayed -= _vfx.Handle;
            _match.EventPlayed -= _water.Handle;
            _match.EventPlayed -= OnEvent;
            _vfx.YouWereHit -= _hud.Flash;
            _vfx.YouHitAnEnemy -= _hud.HitMarker;
            _vfx.YouLandedACritical -= _hud.Critical;
            _vfx.YourCourageChanged -= OnCourage;
            _vfx.Shake -= _camera.Shake;
            _hud.SetScoreboard(false);
            _hud.SetDefeat(false, null, Color.white, null);
            _vfx.Destroy();
            _projectiles.Destroy();
            _combatants.Destroy();
            _arena.Destroy();
            _water.Destroy();
            _barriers.Destroy();
            _fire.Destroy();
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }
        }

        // ------------------------------------------------------------------------------------------ frame steps

        /// <summary>Shows who got you when you go down, and clears the panel when the next round brings you back.</summary>
        private void UpdateDefeatPanel(bool alive)
        {
            if (alive == _wasAlive)
            {
                return;
            }
            _wasAlive = alive;
            if (alive)
            {
                _hud.SetDefeat(false, null, Color.white, null);
                return;
            }
            MatchStats stats = _match.Stats;
            int attacker = stats.LastAttacker(_match.YourId);
            string name = attacker >= 0 ? _match.NameOf(attacker) : null;
            Color color = Palette.Team(_match.TeamOf(attacker), _match.YourTeam);
            string detail = null;
            if (attacker >= 0)
            {
                int element = stats.LastElement(_match.YourId);
                string elementName = element >= 0 ? UiText.Element(_match.Content.ElementName(element)) : string.Empty;
                detail = "Último golpe: " + Mathf.RoundToInt(stats.LastDamage(_match.YourId)) + " de daño"
                         + (elementName.Length > 0 ? "  -  " + elementName : string.Empty);
                if (element >= 0)
                {
                    color = Palette.Element(_match.Content.ElementName(element));
                }
            }
            else
            {
                detail = "Una explosión o un efecto te derrotó.";
            }
            _hud.SetDefeat(true, name, color, detail);
            _audio.PlayUi(_audio.Defeat, 0.5f);
        }

        private void PlaceCamera(WorldFrame frame, int ownIndex, bool alive)
        {
            if (alive)
            {
                _spectateId = -1;
                _hud.SetSpectating(null);
                CombatantFrame you = frame.Combatants[ownIndex];
                _camera.Follow(you.Position, you.HitboxHeight > 0.1f ? you.HitboxHeight : 1.8f, _match.Map);
                return;
            }
            int target = PickSpectateTarget(frame);
            if (target >= 0)
            {
                CombatantFrame other = frame.Combatants[target];
                _camera.SetAim(other.Yaw, 0.2f);
                _camera.Follow(other.Position, other.HitboxHeight > 0.1f ? other.HitboxHeight : 1.8f, _match.Map);
                _hud.SetSpectating(_match.NameOf(other.Id));
            }
            else if (ownIndex >= 0)
            {
                CombatantFrame you = frame.Combatants[ownIndex];
                _camera.Follow(you.Position, 1.8f, _match.Map);
                _hud.SetSpectating(null);
            }
        }

        private int PickSpectateTarget(WorldFrame frame)
        {
            int current = _spectateId >= 0 ? frame.IndexOf(_spectateId) : -1;
            bool advance = _controls.ClickPressed && !Paused;
            if (current < 0 || !frame.Combatants[current].Alive || advance)
            {
                int start = current < 0 ? 0 : current + 1;
                int found = -1;
                // Prefer teammates, then anybody still alive.
                for (int pass = 0; pass < 2 && found < 0; pass++)
                {
                    for (int k = 0; k < frame.CombatantCount; k++)
                    {
                        int i = (start + k) % frame.CombatantCount;
                        CombatantFrame c = frame.Combatants[i];
                        bool team = _match.TeamOf(c.Id) == _match.YourTeam;
                        if (c.Alive && c.Id != _match.YourId && (pass == 1 || team))
                        {
                            found = i;
                            break;
                        }
                    }
                }
                _spectateId = found < 0 ? -1 : frame.Combatants[found].Id;
                current = found;
            }
            return current;
        }

        private void ReadOneShots()
        {
            if (_controls.SlotKey >= 0)
            {
                _oneShots.Select(_controls.SlotKey);
            }
            if (_controls.Scroll != 0)
            {
                _oneShots.Cycle(-_controls.Scroll);
            }
        }

        private void SendInputs(float deltaTime, bool alive)
        {
            int steps = _inputClock.Advance(deltaTime);
            for (int i = 0; i < steps; i++)
            {
                PlayerInput input = PlayerInput.Idle;
                if (!Paused && alive)
                {
                    input.MoveX = _controls.MoveX;
                    input.MoveZ = _controls.MoveZ;
                    input.Fire = _controls.Fire;
                    uint mask = 0;
                    for (int bit = 0; bit < _moveMask.Count; bit++)
                    {
                        if (_controls.IsMoveHeld(_moveMask.MoveId(bit)))
                        {
                            mask |= 1u << bit;
                        }
                    }
                    input.MoveMask = mask;
                }
                input.AimYaw = _camera.Pose.Yaw;
                input.AimPitch = _camera.Pose.Pitch;
                input.HasCamera = true;
                input.CameraPosition = _camera.Pose.Position;
                _oneShots.Drain(ref input);
                _client.SendInput(input);
            }
        }

        private void AnnounceCountdown()
        {
            if (_model.Phase == MatchPhase.Countdown && _model.SecondsLeft != _lastCountdown)
            {
                _lastCountdown = _model.SecondsLeft;
                _audio.PlayUi(_audio.CountdownBeep, 0.6f);
            }
            else if (_model.Phase != MatchPhase.Countdown)
            {
                _lastCountdown = -1;
            }
        }

        private void UpdateDebug(float deltaTime)
        {
            _smoothedFrame += (deltaTime - _smoothedFrame) * 0.05f;
            _debugTimer -= deltaTime;
            if (_debugTimer > 0f)
            {
                return;
            }
            _debugTimer = 0.25f;
            _debug.Length = 0;
            var p = _match.Prediction;
            _debug.Append(Mathf.RoundToInt(1f / Mathf.Max(0.0001f, _smoothedFrame))).Append(" FPS\n")
                .Append("ping ").Append(_client.PingMs).Append(" ms  predicción ").Append(p.IsPredicting ? "sí" : "no")
                .Append("  error ")
                .Append(p.LastError.ToString("0.000")).Append(" m\n")
                .Append("pendientes ").Append(p.PendingInputs).Append("  saltos ").Append(p.SnapCount).Append('\n')
                .Append("snapshots ").Append(_match.Snapshots.Count).Append("  eventos en cola");
            _debugText = _debug.ToString();
        }

        // ------------------------------------------------------------------------------------------ events

        private void OnEvent(GameEvent e)
        {
            switch (e.Kind)
            {
                case EventKind.RoundStarted:
                    // Every match has its own arena: the first round says which one.
                    _hud.Banner(UiText.Round + " " + e.A, Color.white, 2.2f,
                        e.A == 1 ? UiText.ArenaIntro(_match.Map.Name) : UiText.FightNow);
                    break;
                case EventKind.RoundEnded:
                    if (e.A < 0)
                    {
                        _hud.Banner(UiText.RoundDraw, Color.white);
                    }
                    else if (e.A == _match.YourTeam)
                    {
                        _hud.Banner(UiText.RoundWon, Palette.Health);
                        _audio.PlayUi(_audio.Victory, 0.5f);
                    }
                    else
                    {
                        _hud.Banner(UiText.RoundLost, Palette.TeamOther);
                        _audio.PlayUi(_audio.Loss, 0.5f);
                    }
                    break;
            }
        }

        /// <summary>Announces courage when it starts or grows; it ending is shown only by the badge going away.</summary>
        private void OnCourage(int level, int previous)
        {
            if (level > previous)
            {
                _hud.Banner(UiText.CourageBanner, Palette.Courage, 2.4f, UiText.CourageDetail(level));
            }
        }

        private float SpawnYaw()
        {
            foreach (Bajoterra.Client.Content.SpawnPoint spawn in _match.Map.Spawns)
            {
                if (spawn.Team == _match.YourTeam)
                {
                    return spawn.Yaw;
                }
            }
            return 0f;
        }
    }
}
