using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Bajoterra.Client;
using Bajoterra.Client.Content;
using Bajoterra.Client.Interpolation;
using Bajoterra.Client.Session;
using Bajoterra.Protocol;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Bajoterra.Unity.Tests
{
    /// <summary>
    /// Records footage for the promotional video (<c>video/</c> at the repository root). Only runs when
    /// <c>BAJOTERRA_TRAILER</c> names an output directory: it plays a real local match against bots, drives Eli with
    /// scripted controls (run, jump, wall kick, air roll while firing, slide while firing, then real combat), points a
    /// cinematic camera at every slug effect the match produces, and streams the frames in real time to ffmpeg
    /// (<c>trailer.mp4</c>), the game's own sound to <c>trailer.wav</c> and a timed log of shots and effects to
    /// <c>markers.jsonl</c>, so the edit can pick its cuts.
    /// </summary>
    public sealed class TrailerCapture
    {
        private const string OutputVariable = "BAJOTERRA_TRAILER";
        private const float RecordSeconds = 230f;

        private static readonly string[] Loadout = { "tormato", "frostcrawler", "infurnus", "bubbaleone", "lariat" };

        [UnitySetUp]
        public IEnumerator StartApp()
        {
            yield return null;
            if (GameApp.Instance == null)
            {
                new GameObject("Bajoterra").AddComponent<GameApp>();
            }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator StopApp()
        {
            if (GameApp.Instance != null)
            {
                Object.Destroy(GameApp.Instance.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator Record_the_trailer_footage()
        {
            string dir = Environment.GetEnvironmentVariable(OutputVariable);
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Ignore("Set " + OutputVariable + " to an output directory to record the trailer footage.");
            }
            if (LocalServerLauncher.FindJar(string.Empty) == null)
            {
                Assert.Ignore("Server jar not built: run `mvn -DskipTests package` at the repository root.");
            }
            Directory.CreateDirectory(dir);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            var host = new GameObject("Trailer");
            var recorder = host.AddComponent<FrameRecorder>();
            var cinema = host.AddComponent<CinematicCamera>();
            var log = new MarkerLog(Path.Combine(dir, "markers.jsonl"), recorder);
            try
            {
                yield return Frames(20);
                // The main menu, with its heroes behind it.
                recorder.Begin(dir, true);
                log.Mark("menu");
                yield return Seconds(5f);

                Click(FindButton("Door JUGAR"));
                yield return Frames(5);
                Click(FindButton("Local"));
                yield return WaitForButton("JoinButton", 40f);
                Click(FindButton("JoinButton"));
                yield return WaitForButton("Ready", 20f);
                yield return Seconds(1.5f);
                log.Mark("room");
                yield return Seconds(5f);

                Invoke(GameApp.Instance, "OnReady", "eli", Loadout);
                yield return WaitUntil(() => IsActive("Hud"), 40f);
                log.Mark("match");

                object presenter = Private(GameApp.Instance, "_presenter");
                var controls = new ScriptedControls();
                SetPrivate(presenter, "_controls", controls);
                object rig = Private(presenter, "_camera");
                GameClient client = (GameClient)Private(GameApp.Instance, "_client");
                MatchSession match = client.Match;
                cinema.Map = match.Map;
                var director = new Director(match, cinema, recorder, log, controls, (CameraRig)rig);
                match.EventPlayed += director.OnEvent;

                float end = Time.realtimeSinceStartup + RecordSeconds;
                IEnumerator script = director.Run();
                while (Time.realtimeSinceStartup < end && script.MoveNext())
                {
                    yield return script.Current;
                }
                match.EventPlayed -= director.OnEvent;
            }
            finally
            {
                recorder.Finish();
                log.Close();
            }
            Debug.Log("[trailer] footage written to " + dir);
        }

        // ------------------------------------------------------------------------------------------ the script

        /// <summary>Plays the match: scripted moves first, then combat, with cinematic cuts to every effect.</summary>
        private sealed class Director
        {
            private readonly MatchSession _match;
            private readonly CinematicCamera _cinema;
            private readonly FrameRecorder _recorder;
            private readonly MarkerLog _log;
            private readonly ScriptedControls _controls;
            private readonly CameraRig _rig;
            private float _cinematicUntil;
            private float _lastEffectShot = -100f;
            private int _effectShots;
            private int _lastRound = -1;
            private bool _roundEndFilmed;

            public Director(MatchSession match, CinematicCamera cinema, FrameRecorder recorder, MarkerLog log,
                ScriptedControls controls, CameraRig rig)
            {
                _match = match;
                _cinema = cinema;
                _recorder = recorder;
                _log = log;
                _controls = controls;
                _rig = rig;
            }

            private float Now
            {
                get { return Time.realtimeSinceStartup; }
            }

            private bool Alive(out CombatantFrame me)
            {
                me = default;
                WorldFrame frame = _match.Frame;
                if (!frame.Valid)
                {
                    return false;
                }
                int index = frame.IndexOf(_match.YourId);
                if (index < 0)
                {
                    return false;
                }
                me = frame.Combatants[index];
                return me.Alive;
            }

            private bool RoundActive
            {
                get { return _match.Frame.Valid && _match.Frame.Match != null && _match.Frame.Match.Phase == MatchPhase.RoundActive; }
            }

            public IEnumerator Run()
            {
                // Wait for the first round to begin.
                while (!RoundActive)
                {
                    yield return null;
                }
                _lastRound = (int)_match.Frame.Match.Round;
                IEnumerator moves = Moves();
                while (moves.MoveNext())
                {
                    yield return moves.Current;
                }
                _cinema.Release();
                _recorder.Hud = true;
                // Combat until the recording time runs out, with a showcase of each slug whenever a rival is in range.
                float slotTimer = 0f;
                int slot = 0;
                float strafeTimer = 0f;
                float strafe = 1f;
                int showcase = 0;
                float nextShowcase = Now + 2f;
                while (true)
                {
                    float dt = Time.unscaledDeltaTime;
                    WatchRounds();
                    if (Now >= _cinematicUntil && _cinema.Active && !_showing)
                    {
                        _cinema.Release();
                        _recorder.Hud = true;
                    }
                    _controls.Reset();
                    if (Alive(out CombatantFrame me) && RoundActive)
                    {
                        bool found = FindEnemy(me, out Vector3 target, out Vector3 enemyFeet);
                        float range = found ? (enemyFeet - me.Position.ToUnity()).magnitude : 999f;
                        if (found && Now >= nextShowcase && range > 7f && range < 24f && !_cinema.Active
                            && SlotReady(showcase % Loadout.Length))
                        {
                            IEnumerator show = Showcase(showcase % Loadout.Length);
                            while (show.MoveNext())
                            {
                                yield return show.Current;
                            }
                            showcase++;
                            nextShowcase = Now + 2.5f;
                            continue;
                        }
                        if (found)
                        {
                            Aim(me, target);
                            Vector3 d = target - (me.Position.ToUnity() + Vector3.up * 1.5f);
                            _controls.Fire = d.magnitude < 40f;
                            _controls.MoveZ = d.magnitude > 14f ? 1f : (d.magnitude < 7f ? -0.6f : 0f);
                        }
                        strafeTimer += dt;
                        if (strafeTimer > 1.3f)
                        {
                            strafeTimer = 0f;
                            strafe = -strafe;
                        }
                        _controls.MoveX = strafe * 0.8f;
                        slotTimer += dt;
                        if (slotTimer > 0.75f)
                        {
                            slotTimer = 0f;
                            slot = (slot + 1) % Loadout.Length;
                            _controls.SlotKey = slot;
                        }
                        _controls.Fusion = true;
                        if (Mathf.Repeat(Now, 3.1f) < dt)
                        {
                            _controls.Hold("jump");
                        }
                    }
                    yield return null;
                }
            }

            private bool _showing;
            private uint _lastProjectile;

            private bool SlotReady(int slot)
            {
                CombatantState you = _match.YourState;
                return you != null && slot < you.Slots.Count && you.Slots[slot].ShotsUntilReady == 0;
            }

            private void Aim(CombatantFrame me, Vector3 target)
            {
                Vector3 d = target - (me.Position.ToUnity() + Vector3.up * 1.5f);
                _rig.SetAim(Mathf.Atan2(d.x, d.z), Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) + 0.03f);
            }

            /// <summary>
            /// Fires one slug at the nearest rival and films it: a camera chasing the slug in flight, then circling
            /// the spot where it lands. Frostcrawler is thrown at the open floor between the two, so its ice wall rises
            /// as cover in the open instead of against a wall.
            /// </summary>
            private IEnumerator Showcase(int slot)
            {
                string slug = Loadout[slot];
                CombatantState you = _match.YourState;
                int slugIndex = (int)you.Slots[slot].SlugIndex;
                _showing = true;
                _recorder.Hud = false;
                // Select the slot and let the server confirm it.
                float deadline = Now + 1.2f;
                while (Now < deadline && (_match.YourState == null || _match.YourState.SelectedSlot != (uint)slot))
                {
                    _controls.Reset();
                    _controls.SlotKey = slot;
                    yield return null;
                }
                // Over the shoulder while it aims and fires.
                _cinema.Track(() => Me(), () => _rig.Yaw, new Vector3(1.3f, 2.0f, -3.2f), 1.6f);
                _log.Mark("show_" + slug);
                uint before = NewestOwnProjectile(slugIndex);
                deadline = Now + 1.5f;
                Vector3 lastVelocity = Vector3.forward;
                bool fired = false;
                while (Now < deadline)
                {
                    _controls.Reset();
                    if (!Alive(out CombatantFrame me) || !FindEnemy(me, out Vector3 target, out Vector3 feet))
                    {
                        break;
                    }
                    Vector3 from = me.Position.ToUnity();
                    if (slug == "frostcrawler")
                    {
                        // The open floor 40 % of the way to the rival.
                        target = from + (feet - from) * 0.4f;
                    }
                    else if (slug == "tormato" || slug == "infurnus")
                    {
                        target = feet + Vector3.up * 0.3f;
                    }
                    else
                    {
                        // Lead a moving rival: where it will be when the slug arrives.
                        Vector3 velocity = EnemyVelocity();
                        target += velocity * ((target - from).magnitude / 44f);
                    }
                    Aim(me, target);
                    _controls.Fire = true;
                    yield return null;
                    uint now = NewestOwnProjectile(slugIndex);
                    if (now != 0 && now != before)
                    {
                        fired = true;
                        _lastProjectile = now;
                        break;
                    }
                }
                _controls.Reset();
                if (fired)
                {
                    _log.Mark("shot_" + slug);
                    // Chase it while it flies.
                    Vector3 last = Vector3.zero;
                    _cinema.Chase(() =>
                    {
                        if (FindProjectile(_lastProjectile, out ProjectileFrame p))
                        {
                            last = p.Position.ToUnity();
                            Vector3 v = p.Velocity.ToUnity();
                            if (v.sqrMagnitude > 0.01f)
                            {
                                lastVelocity = v;
                            }
                        }
                        return last;
                    }, () => lastVelocity);
                    float flight = Now + 1.6f;
                    while (Now < flight && FindProjectile(_lastProjectile, out ProjectileFrame flying) && !flying.Stuck)
                    {
                        yield return null;
                    }
                    _log.Mark("impact_" + slug);
                    Vector3 at = last;
                    _cinema.Orbit(() => at + Vector3.up * 0.9f, slug == "tormato" ? 6.5f : 5.0f, 2.2f, 28f);
                    float hold = Now + (slug == "tormato" ? 2.8f : 2.4f);
                    while (Now < hold)
                    {
                        yield return null;
                    }
                }
                _showing = false;
                _cinema.Release();
                _recorder.Hud = true;
                _lastEffectShot = Now;
            }

            private uint NewestOwnProjectile(int slugIndex)
            {
                WorldFrame frame = _match.Frame;
                uint best = 0;
                for (int i = 0; i < frame.ProjectileCount; i++)
                {
                    ProjectileFrame p = frame.Projectiles[i];
                    if (p.Owner == _match.YourId && p.SlugIndex == slugIndex && p.Id > best)
                    {
                        best = p.Id;
                    }
                }
                return best;
            }

            private bool FindProjectile(uint id, out ProjectileFrame found)
            {
                WorldFrame frame = _match.Frame;
                for (int i = 0; i < frame.ProjectileCount; i++)
                {
                    if (frame.Projectiles[i].Id == id)
                    {
                        found = frame.Projectiles[i];
                        return true;
                    }
                }
                found = default;
                return false;
            }

            private int _trackedEnemy = -1;
            private Vector3 _trackedLast;
            private float _trackedTime;
            private Vector3 _trackedVelocity;

            private Vector3 EnemyVelocity()
            {
                return _trackedVelocity;
            }

            /// <summary>Run, jump, wall kick, roll in the air while firing, slide while firing: all facing the open arena.</summary>
            private IEnumerator Moves()
            {
                CombatantFrame me;
                if (!Alive(out me))
                {
                    yield break;
                }
                ArenaMap map = _match.Map;
                Vector3 centre = new Vector3((map.MinX + map.MaxX) * 0.5f, 0f, (map.MinZ + map.MaxZ) * 0.5f);
                Vector3 p = me.Position.ToUnity();
                Vector3 toCentre = centre - p;
                float yaw = Mathf.Atan2(toCentre.x, toCentre.z) + 0.35f;
                _rig.SetAim(yaw, 0.02f);

                // 1. Running into the arena, filmed from low in front.
                _recorder.Hud = false;
                _log.Mark("moves_run");
                _cinema.Track(() => Me(), () => yaw, new Vector3(0.5f, 0.9f, 3.4f), 1.1f);
                yield return Hold(1.5f, () => { _controls.MoveZ = 1f; _rig.SetAim(yaw, 0.02f); });

                // 2. A jump, from the side, the open arena behind.
                _log.Mark("moves_jump");
                _cinema.Track(() => Me(), () => yaw, new Vector3(3.4f, 1.3f, 0.8f), 1.2f);
                _controls.Hold("jump");
                yield return Hold(1.2f, () => { _controls.MoveZ = 1f; _rig.SetAim(yaw, 0.02f); });

                // 3. The wall kick: turn to the nearest arena edge, run at it, jump, kick off it back into the arena
                //    and roll in the air firing. The camera waits in the open, facing the wall, so Eli flies at it.
                p = Me();
                float west = p.x - map.MinX, east = map.MaxX - p.x, south = p.z - map.MinZ, north = map.MaxZ - p.z;
                float wallYaw = -Mathf.PI / 2f;
                float best = west;
                if (east < best) { best = east; wallYaw = Mathf.PI / 2f; }
                if (south < best) { best = south; wallYaw = Mathf.PI; }
                if (north < best) { wallYaw = 0f; }
                var wallDir = new Vector3(Mathf.Sin(wallYaw), 0f, Mathf.Cos(wallYaw));
                var side = new Vector3(wallDir.z, 0f, -wallDir.x);
                float toWall = DistanceToWall(p, map, wallYaw);
                Vector3 contact = p + wallDir * toWall;
                Vector3 cameraAt = contact - wallDir * 9.5f + side * 4.5f + Vector3.up * 2.6f;
                yaw = wallYaw;
                _log.Mark("moves_wall_approach");
                _cinema.Watch(cameraAt, () => Me() + Vector3.up * 1.2f);
                float deadline = Now + 5f;
                while (Now < deadline && Alive(out me) && DistanceToWall(me.Position.ToUnity(), map, wallYaw) > 2.2f)
                {
                    _controls.Reset();
                    _controls.MoveZ = 1f;
                    _rig.SetAim(yaw, 0.02f);
                    yield return null;
                }
                _controls.Reset();
                _controls.Hold("jump");
                _log.Mark("moves_wall_jump");
                yield return Hold(0.35f, () => { _controls.MoveZ = 1f; });
                _controls.Reset();
                yield return null;
                _controls.Hold("wall_kick");
                _controls.Hold("jump");
                _log.Mark("moves_wall_kick");
                yield return Hold(0.12f, () => { });
                // Turn round in the air towards the open arena (and the camera), roll and fire.
                yaw = wallYaw + Mathf.PI + 0.25f;
                _rig.SetAim(yaw, 0.05f);
                _controls.Reset();
                yield return null;
                _controls.Hold("roll");
                _controls.Fire = true;
                _log.Mark("moves_air_roll_fire");
                yield return Hold(0.9f, () => { _controls.Fire = true; _controls.MoveZ = 1f; _rig.SetAim(yaw, 0.05f); });

                // 4. Land, run towards the rivals and slide while firing at them.
                Alive(out me);
                if (FindEnemy(me, out Vector3 enemy, out Vector3 enemyFeet))
                {
                    Vector3 d = enemyFeet - me.Position.ToUnity();
                    yaw = Mathf.Atan2(d.x, d.z);
                }
                _log.Mark("moves_slide_run");
                _cinema.Track(() => Me(), () => yaw, new Vector3(2.2f, 0.6f, 3.6f), 1.0f);
                yield return Hold(0.9f, () => { _controls.MoveZ = 1f; _rig.SetAim(yaw, 0.02f); });
                _log.Mark("moves_slide");
                yield return Hold(1.1f, () =>
                {
                    _controls.MoveZ = 1f;
                    _controls.Hold("slide");
                    _controls.Hold("crouch");
                    _controls.Fire = true;
                    _rig.SetAim(yaw, 0.02f);
                });
                _controls.Reset();
                _log.Mark("moves_done");
            }

            private Vector3 Me()
            {
                Alive(out CombatantFrame me);
                return me.Position.ToUnity();
            }

            private IEnumerator Hold(float seconds, Action each)
            {
                float until = Now + seconds;
                while (Now < until)
                {
                    // Held moves are cleared by Reset only when the caller asks; one-shot presses last one frame.
                    each();
                    yield return null;
                    _controls.ReleaseTaps();
                }
            }

            private static float DistanceToWall(Vector3 p, ArenaMap map, float yaw)
            {
                if (Mathf.Abs(yaw + Mathf.PI / 2f) < 0.1f) return p.x - map.MinX;
                if (Mathf.Abs(yaw - Mathf.PI / 2f) < 0.1f) return map.MaxX - p.x;
                if (Mathf.Abs(yaw - Mathf.PI) < 0.1f) return p.z - map.MinZ;
                return map.MaxZ - p.z;
            }

            private bool FindEnemy(CombatantFrame me, out Vector3 target)
            {
                return FindEnemy(me, out target, out Vector3 feet);
            }

            /// <summary>The nearest living rival: its chest and its feet; also keeps an estimate of its velocity.</summary>
            private bool FindEnemy(CombatantFrame me, out Vector3 target, out Vector3 feet)
            {
                target = default;
                feet = default;
                WorldFrame frame = _match.Frame;
                float best = float.MaxValue;
                int id = -1;
                for (int i = 0; i < frame.CombatantCount; i++)
                {
                    CombatantFrame c = frame.Combatants[i];
                    if (!c.Alive || _match.TeamOf(c.Id) == _match.YourTeam)
                    {
                        continue;
                    }
                    float d = (c.Position.ToUnity() - me.Position.ToUnity()).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        feet = c.Position.ToUnity();
                        target = feet + Vector3.up * (c.HitboxHeight * 0.6f);
                        id = c.Id;
                    }
                }
                if (id >= 0)
                {
                    float t = Time.realtimeSinceStartup;
                    if (id == _trackedEnemy && t - _trackedTime > 0.05f)
                    {
                        Vector3 v = (feet - _trackedLast) / (t - _trackedTime);
                        _trackedVelocity = Vector3.Lerp(_trackedVelocity, new Vector3(v.x, 0f, v.z), 0.5f);
                    }
                    else if (id != _trackedEnemy)
                    {
                        _trackedVelocity = Vector3.zero;
                    }
                    if (id != _trackedEnemy || t - _trackedTime > 0.05f)
                    {
                        _trackedEnemy = id;
                        _trackedLast = feet;
                        _trackedTime = t;
                    }
                }
                return id >= 0;
            }

            private void WatchRounds()
            {
                MatchInfo info = _match.Frame.Valid ? _match.Frame.Match : null;
                if (info == null)
                {
                    return;
                }
                if (info.Phase == MatchPhase.RoundEnd && !_roundEndFilmed)
                {
                    _roundEndFilmed = true;
                    // The winners dance: orbit the first survivor.
                    WorldFrame frame = _match.Frame;
                    for (int i = 0; i < frame.CombatantCount; i++)
                    {
                        if (frame.Combatants[i].Alive)
                        {
                            int id = frame.Combatants[i].Id;
                            _log.Mark("round_end_dance", id);
                            _cinema.Orbit(() => Position(id) + Vector3.up * 1.0f, 4.2f, 1.4f, 30f);
                            _recorder.Hud = false;
                            _cinematicUntil = Now + 4.5f;
                            break;
                        }
                    }
                }
                if (info.Phase == MatchPhase.RoundActive && (int)info.Round != _lastRound)
                {
                    _lastRound = (int)info.Round;
                    _roundEndFilmed = false;
                    _log.Mark("round_start", _lastRound);
                }
            }

            private Vector3 Position(int id)
            {
                WorldFrame frame = _match.Frame;
                int index = frame.IndexOf(id);
                return index >= 0 ? frame.Combatants[index].Position.ToUnity() : Vector3.zero;
            }

            public void OnEvent(GameEvent e)
            {
                string kind = null;
                switch (e.Kind)
                {
                    case EventKind.Tornado: kind = "tornado"; break;
                    case EventKind.BarrierRaised: kind = "ice_wall"; break;
                    case EventKind.Bubble: kind = "bubble"; break;
                    case EventKind.Lasso: kind = "lasso"; break;
                    case EventKind.WebSpun: kind = "web"; break;
                    case EventKind.Explosion: kind = "explosion"; break;
                    case EventKind.Flash: kind = "flash"; break;
                    case EventKind.Fusion: kind = "fusion"; break;
                    case EventKind.Defeated: kind = "defeated"; break;
                    case EventKind.Shot:
                        _log.Event("shot", e, SlugName(e.A));
                        return;
                    case EventKind.StatusApplied:
                        _log.Event("status", e, null);
                        return;
                    case EventKind.PickupTaken:
                        _log.Event("pickup", e, null);
                        return;
                    case EventKind.MoveStarted:
                        _log.Event("move", e, null);
                        return;
                    default:
                        return;
                }
                _log.Event(kind, e, null);
                if (_showing)
                {
                    return;
                }
                // Cut to the effect, but not more than one cut every 3 s, and leave every third to the player's view.
                if (Time.realtimeSinceStartup - _lastEffectShot < 3f || kind == "defeated" || kind == "fusion")
                {
                    return;
                }
                _effectShots++;
                if (_effectShots % 3 == 0)
                {
                    return;
                }
                _lastEffectShot = Time.realtimeSinceStartup;
                Vector3 at = new Vector3(e.X, e.Y, e.Z);
                if (at.sqrMagnitude < 0.01f && e.Subject >= 0)
                {
                    at = Position(e.Subject);
                }
                _log.Mark("effect_" + kind);
                float radius = kind == "tornado" ? 8.5f : 6.5f;
                float height = kind == "tornado" ? 3.2f : 2.4f;
                _cinema.Orbit(() => at + Vector3.up * 1.0f, radius, height, 24f);
                _recorder.Hud = false;
                _cinematicUntil = Time.realtimeSinceStartup + (kind == "tornado" ? 3.2f : 2.4f);
            }

            private string SlugName(int index)
            {
                try
                {
                    return _match.Content.Manifest.SlugIds[index];
                }
                catch (ArgumentOutOfRangeException)
                {
                    return index.ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        // ------------------------------------------------------------------------------------------ controls

        /// <summary>Controls the script sets frame by frame instead of a keyboard and mouse.</summary>
        private sealed class ScriptedControls : IControls
        {
            private readonly HashSet<string> _held = new HashSet<string>();
            private readonly HashSet<string> _taps = new HashSet<string>();

            public float MoveX { get; set; }
            public float MoveZ { get; set; }
            public bool Fire { get; set; }
            public bool Fusion { get; set; }
            public int SlotKey { get; set; } = -1;

            /// <summary>Presses a move for this frame (call again every frame to keep it held).</summary>
            public void Hold(string moveId)
            {
                _held.Add(moveId);
                _taps.Add(moveId);
            }

            /// <summary>Drops the presses of the last frame that nobody renewed.</summary>
            public void ReleaseTaps()
            {
                _held.Clear();
                SlotKey = -1;
            }

            public void Reset()
            {
                MoveX = 0f;
                MoveZ = 0f;
                Fire = false;
                Fusion = false;
                SlotKey = -1;
                _held.Clear();
            }

            public void Poll()
            {
            }

            public Vector2 Look
            {
                get { return Vector2.zero; }
            }

            public bool IsMoveHeld(string moveId)
            {
                return _held.Contains(moveId);
            }

            public int Scroll
            {
                get { return 0; }
            }

            public bool DancePressed
            {
                get { return false; }
            }

            public bool ClickPressed
            {
                get { return false; }
            }

            public bool PausePressed
            {
                get { return false; }
            }

            public bool DebugPressed
            {
                get { return false; }
            }

            public bool ScoreboardHeld
            {
                get { return false; }
            }
        }

        // ------------------------------------------------------------------------------------------ camera

        /// <summary>Overrides the game camera after the game placed it, while a shot is active.</summary>
        [DefaultExecutionOrder(31000)]
        private sealed class CinematicCamera : MonoBehaviour
        {
            private Func<Vector3> _target;
            private Func<float> _facing;
            private Vector3 _offset;
            private float _lookHeight;
            private bool _orbit;
            private float _radius;
            private float _height;
            private float _speed;
            private float _angle;
            private Vector3 _position;
            private bool _fresh;
            private int _mode;
            private Vector3 _fixed;
            private Func<Vector3> _velocity;

            /// <summary>The arena, to keep walls, pillars and crates from coming between the camera and its subject.</summary>
            public ArenaMap Map { get; set; }

            private float _dodge;

            /// <summary>
            /// The wanted camera spot if nothing stands between it and the subject; otherwise the nearest clear spot
            /// turning around the subject (and a little higher), remembered so the camera does not flicker.
            /// </summary>
            private Vector3 Unblocked(Vector3 look, Vector3 wanted)
            {
                Vector3 arm = wanted - look;
                Vector3 first = look + Quaternion.Euler(0f, _dodge, 0f) * arm;
                if (Clear(look, first))
                {
                    return first;
                }
                float[] turns = { 25f, -25f, 50f, -50f, 75f, -75f, 105f, -105f, 140f, -140f, 180f };
                for (int lift = 0; lift < 3; lift++)
                {
                    foreach (float turn in turns)
                    {
                        Vector3 candidate = look + Quaternion.Euler(0f, _dodge + turn, 0f) * arm + Vector3.up * (1.2f * lift);
                        if (Clear(look, candidate))
                        {
                            _dodge += turn;
                            _fresh = true;
                            return candidate;
                        }
                    }
                }
                // Nothing clear: come in close.
                return look + arm.normalized * 1.6f + Vector3.up * 0.8f;
            }

            private bool Clear(Vector3 from, Vector3 to)
            {
                ArenaMap map = Map;
                if (to.x < map.MinX + 0.4f || to.x > map.MaxX - 0.4f || to.z < map.MinZ + 0.4f || to.z > map.MaxZ - 0.4f)
                {
                    return false;
                }
                foreach (ObstacleBox o in map.Obstacles)
                {
                    if (Hits(from, to, o, 0.35f))
                    {
                        return false;
                    }
                }
                return true;
            }

            /// <summary>Whether the segment passes through the box grown by a margin (slab test).</summary>
            private static bool Hits(Vector3 a, Vector3 b, ObstacleBox o, float margin)
            {
                Vector3 min = new Vector3(o.MinX - margin, o.MinY - margin, o.MinZ - margin);
                Vector3 max = new Vector3(o.MaxX + margin, o.MaxY + margin, o.MaxZ + margin);
                Vector3 d = b - a;
                float t0 = 0f, t1 = 1f;
                for (int axis = 0; axis < 3; axis++)
                {
                    float start = a[axis], dir = d[axis];
                    if (Mathf.Abs(dir) < 1e-6f)
                    {
                        if (start < min[axis] || start > max[axis])
                        {
                            return false;
                        }
                        continue;
                    }
                    float near = (min[axis] - start) / dir, far = (max[axis] - start) / dir;
                    if (near > far)
                    {
                        float swap = near;
                        near = far;
                        far = swap;
                    }
                    t0 = Mathf.Max(t0, near);
                    t1 = Mathf.Min(t1, far);
                    if (t0 > t1)
                    {
                        return false;
                    }
                }
                return true;
            }

            public bool Active { get; private set; }

            /// <summary>Follows a body with an offset in its facing frame (x right, y up, z forward).</summary>
            public void Track(Func<Vector3> target, Func<float> facing, Vector3 offset, float lookHeight)
            {
                _target = target;
                _facing = facing;
                _offset = offset;
                _lookHeight = lookHeight;
                _orbit = false;
                _mode = 0;
                _dodge = 0f;
                Active = true;
                _fresh = true;
            }

            /// <summary>Circles a point at a radius and height, turning <paramref name="degreesPerSecond"/>.</summary>
            public void Orbit(Func<Vector3> target, float radius, float height, float degreesPerSecond)
            {
                _target = target;
                _radius = radius;
                _height = height;
                _speed = degreesPerSecond;
                Camera camera = Camera.main;
                Vector3 centre = target();
                Vector3 from = camera != null ? camera.transform.position - centre : Vector3.back;
                _angle = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
                _orbit = true;
                _mode = 1;
                _dodge = 0f;
                Active = true;
                _fresh = true;
            }

            /// <summary>Stays at a fixed spot and turns to keep a moving point in view.</summary>
            public void Watch(Vector3 position, Func<Vector3> target)
            {
                _target = target;
                _mode = 2;
                _dodge = 0f;
                _fixed = position;
                Active = true;
                _fresh = true;
            }

            /// <summary>Flies behind a moving point (a slug in flight), looking where it goes.</summary>
            public void Chase(Func<Vector3> target, Func<Vector3> velocity)
            {
                _target = target;
                _velocity = velocity;
                _mode = 3;
                Active = true;
                _fresh = true;
            }

            public void Release()
            {
                Active = false;
            }

            private void LateUpdate()
            {
                Camera camera = Camera.main;
                if (!Active || camera == null || _target == null)
                {
                    return;
                }
                Vector3 centre = _target();
                Vector3 wanted;
                Vector3 look;
                if (_mode == 2)
                {
                    wanted = _fixed;
                    look = centre;
                }
                else if (_mode == 3)
                {
                    Vector3 dir = _velocity().normalized;
                    var flat = new Vector3(dir.x, 0f, dir.z).normalized;
                    wanted = centre - dir * 2.6f + Vector3.up * 0.7f + new Vector3(flat.z, 0f, -flat.x) * 0.6f;
                    look = centre + dir * 6f;
                    _fresh = true;
                }
                else if (_orbit)
                {
                    _angle += _speed * Time.unscaledDeltaTime;
                    float a = _angle * Mathf.Deg2Rad;
                    wanted = centre + new Vector3(Mathf.Sin(a) * _radius, _height, Mathf.Cos(a) * _radius);
                    look = centre;
                }
                else
                {
                    float yaw = _facing();
                    var forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                    var right = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
                    wanted = centre + right * _offset.x + Vector3.up * _offset.y + forward * _offset.z;
                    look = centre + Vector3.up * _lookHeight;
                }
                if (_mode != 3 && Map != null)
                {
                    wanted = Unblocked(look, wanted);
                }
                _position = _fresh ? wanted : Vector3.Lerp(_position, wanted, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 6f));
                _fresh = false;
                camera.transform.SetPositionAndRotation(_position, Quaternion.LookRotation(look - _position, Vector3.up));
            }
        }

        // ------------------------------------------------------------------------------------------ recording

        /// <summary>
        /// Streams the game camera at 30 fps of real time to ffmpeg (frames are read back asynchronously and repeated
        /// when the game runs slower than that, so the footage keeps the server's pace), and the game's sound to a WAV.
        /// </summary>
        [DefaultExecutionOrder(32000)]
        private sealed class FrameRecorder : MonoBehaviour
        {
            private const int Width = 1920;
            private const int Height = 1080;
            private const int Fps = 30;

            private Process _ffmpeg;
            private Stream _stdin;
            private Thread _writer;
            private readonly Queue<KeyValuePair<byte[], int>> _queue = new Queue<KeyValuePair<byte[], int>>();
            private readonly object _lock = new object();
            private bool _closing;
            private double _start;
            private int _framesRequested;
            private readonly Stack<RenderTexture> _free = new Stack<RenderTexture>();
            private AudioTap _tap;

            /// <summary>Draw the HUD and other overlay canvases into the footage.</summary>
            public bool Hud { get; set; }

            public bool Recording { get; private set; }

            public double Elapsed
            {
                get { return Recording ? Time.realtimeSinceStartupAsDouble - _start : 0.0; }
            }

            public void Begin(string dir, bool hud)
            {
                Hud = hud;
                string ffmpeg = File.Exists("/opt/homebrew/bin/ffmpeg") ? "/opt/homebrew/bin/ffmpeg" : "ffmpeg";
                var info = new ProcessStartInfo(ffmpeg,
                    "-y -loglevel error -f rawvideo -pix_fmt rgba -s " + Width + "x" + Height + " -r " + Fps
                    + " -i - -vf vflip -c:v h264_videotoolbox -b:v 40M -pix_fmt yuv420p \""
                    + Path.Combine(dir, "trailer.mp4") + "\"")
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                };
                _ffmpeg = Process.Start(info);
                _stdin = _ffmpeg.StandardInput.BaseStream;
                _writer = new Thread(WriteLoop) { IsBackground = true };
                _writer.Start();
                AudioListener listener = Object.FindAnyObjectByType<AudioListener>();
                if (listener != null)
                {
                    _tap = listener.gameObject.AddComponent<AudioTap>();
                    _tap.Begin(Path.Combine(dir, "trailer.wav"));
                }
                _start = Time.realtimeSinceStartupAsDouble;
                _framesRequested = 0;
                Recording = true;
            }

            public void Finish()
            {
                if (!Recording)
                {
                    return;
                }
                Recording = false;
                AsyncGPUReadback.WaitAllRequests();
                lock (_lock)
                {
                    _closing = true;
                    Monitor.PulseAll(_lock);
                }
                _writer.Join();
                _stdin.Close();
                _ffmpeg.WaitForExit(60000);
                if (_tap != null)
                {
                    _tap.Finish();
                    Object.Destroy(_tap);
                }
            }

            private void LateUpdate()
            {
                if (!Recording)
                {
                    return;
                }
                int due = (int)(Elapsed * Fps) + 1;
                int count = due - _framesRequested;
                if (count <= 0)
                {
                    return;
                }
                _framesRequested = due;
                Camera camera = Camera.main;
                if (camera == null)
                {
                    return;
                }
                RenderTexture target = _free.Count > 0 ? _free.Pop() : new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                var converted = new List<Canvas>();
                if (Hud)
                {
                    foreach (Canvas canvas in Object.FindObjectsByType<Canvas>())
                    {
                        if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                        {
                            canvas.renderMode = RenderMode.ScreenSpaceCamera;
                            canvas.worldCamera = camera;
                            canvas.planeDistance = 1f;
                            converted.Add(canvas);
                        }
                    }
                    Canvas.ForceUpdateCanvases();
                }
                RenderTexture previous = camera.targetTexture;
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = previous;
                foreach (Canvas canvas in converted)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                int repeat = count;
                AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, request =>
                {
                    if (!request.hasError)
                    {
                        byte[] bytes = request.GetData<byte>().ToArray();
                        lock (_lock)
                        {
                            _queue.Enqueue(new KeyValuePair<byte[], int>(bytes, repeat));
                            Monitor.PulseAll(_lock);
                        }
                    }
                    _free.Push(target);
                });
            }

            private void WriteLoop()
            {
                while (true)
                {
                    KeyValuePair<byte[], int> item;
                    lock (_lock)
                    {
                        while (_queue.Count == 0 && !_closing)
                        {
                            Monitor.Wait(_lock);
                        }
                        if (_queue.Count == 0)
                        {
                            return;
                        }
                        item = _queue.Dequeue();
                    }
                    for (int i = 0; i < item.Value; i++)
                    {
                        _stdin.Write(item.Key, 0, item.Key.Length);
                    }
                }
            }
        }

        /// <summary>Copies what the listener hears into a 16-bit WAV file.</summary>
        private sealed class AudioTap : MonoBehaviour
        {
            private FileStream _file;
            private BinaryWriter _out;
            private int _channels = 2;
            private int _rate;
            private long _samples;
            private volatile bool _on;
            private readonly object _lock = new object();

            public void Begin(string path)
            {
                _rate = AudioSettings.outputSampleRate;
                _file = new FileStream(path, FileMode.Create);
                _out = new BinaryWriter(_file);
                _out.Write(new byte[44]);
                _on = true;
            }

            private void OnAudioFilterRead(float[] data, int channels)
            {
                if (!_on)
                {
                    return;
                }
                lock (_lock)
                {
                    if (_out == null)
                    {
                        return;
                    }
                    _channels = channels;
                    for (int i = 0; i < data.Length; i++)
                    {
                        _out.Write((short)(Mathf.Clamp(data[i], -1f, 1f) * 32767f));
                    }
                    _samples += data.Length;
                }
            }

            public void Finish()
            {
                _on = false;
                lock (_lock)
                {
                    long bytes = _samples * 2;
                    _out.Seek(0, SeekOrigin.Begin);
                    _out.Write(Encoding.ASCII.GetBytes("RIFF"));
                    _out.Write((int)(36 + bytes));
                    _out.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                    _out.Write(16);
                    _out.Write((short)1);
                    _out.Write((short)_channels);
                    _out.Write(_rate);
                    _out.Write(_rate * _channels * 2);
                    _out.Write((short)(_channels * 2));
                    _out.Write((short)16);
                    _out.Write(Encoding.ASCII.GetBytes("data"));
                    _out.Write((int)bytes);
                    _out.Close();
                    _out = null;
                }
            }
        }

        /// <summary>One JSON line per marker or event, timed in seconds of footage.</summary>
        private sealed class MarkerLog
        {
            private readonly StreamWriter _out;
            private readonly FrameRecorder _recorder;

            public MarkerLog(string path, FrameRecorder recorder)
            {
                _out = new StreamWriter(path, false);
                _recorder = recorder;
            }

            public void Mark(string name, int value = 0)
            {
                Write("{\"t\":" + T() + ",\"mark\":\"" + name + "\",\"v\":" + value + "}");
            }

            public void Event(string kind, GameEvent e, string slug)
            {
                Write("{\"t\":" + T() + ",\"event\":\"" + kind + "\",\"actor\":" + e.Actor + ",\"subject\":" + e.Subject
                      + ",\"a\":" + e.A + ",\"b\":" + e.B + (slug != null ? ",\"slug\":\"" + slug + "\"" : string.Empty)
                      + ",\"x\":" + F(e.X) + ",\"y\":" + F(e.Y) + ",\"z\":" + F(e.Z) + "}");
            }

            private string T()
            {
                return F((float)_recorder.Elapsed);
            }

            private static string F(float v)
            {
                return v.ToString("0.###", CultureInfo.InvariantCulture);
            }

            private void Write(string line)
            {
                _out.WriteLine(line);
                _out.Flush();
            }

            public void Close()
            {
                _out.Close();
            }
        }

        // ------------------------------------------------------------------------------------------ helpers

        private static bool IsActive(string objectName)
        {
            foreach (RectTransform rect in Object.FindObjectsByType<RectTransform>())
            {
                if (rect.name == objectName && rect.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }
            return false;
        }

        private static object Private(object target, string field)
        {
            var info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(info, "private field " + field);
            return info.GetValue(target);
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(info, "private field " + field);
            info.SetValue(target, value);
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(info, "method " + method);
            info.Invoke(target, args);
        }

        private static Button FindButton(string name)
        {
            foreach (Button button in Object.FindObjectsByType<Button>())
            {
                if (button.name == name && button.gameObject.activeInHierarchy)
                {
                    return button;
                }
            }
            return null;
        }

        private static void Click(Button button)
        {
            Assert.IsNotNull(button, "button to click");
            button.onClick.Invoke();
        }

        private static IEnumerator WaitForButton(string name, float timeoutSeconds)
        {
            return WaitUntil(() => FindButton(name) != null, timeoutSeconds);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "timed out");
                yield return null;
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Seconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }
    }
}
