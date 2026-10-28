using System;
using System.Collections.Generic;
using Bajoterra.Client.Content;
using Bajoterra.Client.Hud;
using Bajoterra.Client.Input;
using Bajoterra.Client.Interpolation;
using Bajoterra.Client.Prediction;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Session
{
    /// <summary>
    /// Everything the client knows about the match in progress, and the three clocks it must keep apart:
    /// the local player is drawn from the prediction (now), the other combatants and the slugs from the snapshot
    /// buffer (a little in the past, interpolated), and gameplay events are replayed when the render clock reaches
    /// them. The owner feeds it messages and calls <see cref="Update"/> once per rendered frame.
    /// </summary>
    public sealed class MatchSession
    {
        /// <summary>How far behind the newest server state remote entities are drawn (two snapshot intervals).</summary>
        public const double InterpolationDelay = 0.1;

        private readonly EventScheduler _scheduler;
        private readonly Action<GameEvent> _play;
        private readonly CharacterInfo _character;
        private double _lastLocalNow;

        public MatchSession(ContentCatalog content, MatchStart start, int ticksPerSecond)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            if (start == null)
            {
                throw new ArgumentNullException(nameof(start));
            }
            if (start.Map == null)
            {
                throw new ArgumentException("The server did not send the arena in MatchStart");
            }
            Map = new ArenaMap(start.Map);
            MapId = start.MapId;
            YourId = (int)start.YourCombatantId;
            Slots = new List<Slot>(start.Slots);
            if (YourId < 0 || YourId >= Slots.Count)
            {
                throw new ArgumentException("MatchStart names combatant " + YourId + " but lists " + Slots.Count);
            }
            _character = content.FindCharacter(Slots[YourId].CharacterId)
                         ?? throw new ArgumentException("Unknown character " + Slots[YourId].CharacterId);
            Prediction = new PredictionController(content, Map, _character, YourId);
            TicksPerSecond = ticksPerSecond;
            Stats = new MatchStats(Slots.Count, TeamOf);
            _scheduler = new EventScheduler(ticksPerSecond);
            _play = Play;
        }

        public ContentCatalog Content { get; }

        public ArenaMap Map { get; }

        public string MapId { get; }

        public int YourId { get; }

        public int TicksPerSecond { get; }

        /// <summary>Combatants of the match; the list index is the combatant id.</summary>
        public IReadOnlyList<Slot> Slots { get; }

        public Slot You
        {
            get { return Slots[YourId]; }
        }

        public int YourTeam
        {
            get { return You.Team; }
        }

        public PredictionController Prediction { get; }

        public ServerClock Clock { get; } = new ServerClock();

        public SnapshotBuffer Snapshots { get; } = new SnapshotBuffer();

        public WorldFrame Frame { get; } = new WorldFrame();

        public KillFeed KillFeed { get; } = new KillFeed();

        /// <summary>Kills, deaths, damage and healing counted from the events played so far.</summary>
        public MatchStats Stats { get; }

        /// <summary>The local clock at the last <see cref="Update"/>, in seconds.</summary>
        public double LocalNow
        {
            get { return _lastLocalNow; }
        }

        /// <summary>When the local player last fired, on the local clock (negative before the first shot).</summary>
        public double OwnShotAt { get; private set; } = -1e9;

        /// <summary>How long the blaster waits after the last own shot before it can fire again, in seconds.</summary>
        public float OwnShotCooldown { get; private set; }

        /// <summary>The newest snapshot, or null before the first one.</summary>
        public Snapshot Latest
        {
            get { return Snapshots.Latest; }
        }

        /// <summary>True once the match is over (the server sent <c>MatchEnd</c>).</summary>
        public bool Finished { get; private set; }

        /// <summary>Raised for each gameplay event at the moment it should be shown or heard.</summary>
        public event Action<GameEvent> EventPlayed;

        /// <summary>The latest state of the local combatant, or null.</summary>
        public CombatantState YourState
        {
            get
            {
                Snapshot latest = Latest;
                if (latest == null)
                {
                    return null;
                }
                for (int i = 0; i < latest.Combatants.Count; i++)
                {
                    if (latest.Combatants[i].Id == (uint)YourId)
                    {
                        return latest.Combatants[i];
                    }
                }
                return null;
            }
        }

        public string NameOf(int combatantId)
        {
            return combatantId >= 0 && combatantId < Slots.Count ? Slots[combatantId].Name : null;
        }

        public int TeamOf(int combatantId)
        {
            return combatantId >= 0 && combatantId < Slots.Count ? Slots[combatantId].Team : -1;
        }

        public void OnSnapshot(Snapshot snapshot, double localNow)
        {
            Clock.Observe(localNow, (int)snapshot.Tick);
            Snapshots.Add(snapshot.Tick / (double)TicksPerSecond, snapshot);
            Prediction.Reconcile(snapshot);
        }

        public void OnEvents(EventBatch batch)
        {
            for (int i = 0; i < batch.Events.Count; i++)
            {
                GameEvent gameEvent = batch.Events[i];
                if (gameEvent.Kind == EventKind.Shot && gameEvent.Actor == YourId)
                {
                    Play(gameEvent); // your own shot: no reason to wait for the interpolation delay
                }
                else
                {
                    _scheduler.Enqueue(gameEvent);
                }
            }
        }

        public void OnMatchEnd()
        {
            Finished = true;
            _scheduler.Release(double.MaxValue, _play);
        }

        /// <summary>Advances the render clock, samples the world and releases the events that are due.</summary>
        public void Update(double localNow)
        {
            double delta = _lastLocalNow > 0.0 ? Math.Max(0.0, localNow - _lastLocalNow) : 0.0;
            _lastLocalNow = localNow;
            Prediction.Smooth(delta);
            KillFeed.Update(localNow);
            if (!Clock.IsSynchronized)
            {
                return;
            }
            double renderTime = Clock.ServerTimeAt(localNow) - InterpolationDelay;
            WorldSampler.Sample(Snapshots, renderTime, Frame);
            ApplyPrediction();
            _scheduler.Release(renderTime, _play);
        }

        /// <summary>Replaces the local combatant's interpolated pose with the predicted one.</summary>
        private void ApplyPrediction()
        {
            if (!Frame.Valid || !Prediction.IsPredicting)
            {
                return;
            }
            int index = Frame.IndexOf(YourId);
            if (index < 0)
            {
                return;
            }
            ref CombatantFrame you = ref Frame.Combatants[index];
            PredictedBody body = Prediction.Current;
            you.Position = Prediction.DisplayPosition;
            you.Posture = body.Posture;
            you.HitboxHeight = (float)body.HitHeightNow;
            you.ActiveMove = body.ActiveSlot >= 0 ? (int)_character.MoveIndices[body.ActiveSlot] : -1;
            you.Yaw = (float)body.Yaw;
        }

        private void NoteOwnShot(GameEvent shot)
        {
            float multiplier = 1f;
            if (shot.A >= 0 && shot.A < Content.SlugCount)
            {
                float configured = Content.Slug(shot.A).FireDelayMultiplier;
                multiplier = configured > 0f ? configured : 1f;
            }
            // The server rounds half up and never waits less than one tick.
            int ticks = Math.Max(1, (int)Math.Floor(_character.FireIntervalTicks * (double)multiplier + 0.5));
            OwnShotAt = _lastLocalNow;
            OwnShotCooldown = ticks / (float)TicksPerSecond;
        }

        private void Play(GameEvent gameEvent)
        {
            Stats.Record(gameEvent);
            if (gameEvent.Kind == EventKind.Shot && gameEvent.Actor == YourId)
            {
                NoteOwnShot(gameEvent);
            }
            if (gameEvent.Kind == EventKind.Defeated)
            {
                int victim = gameEvent.Subject;
                int killer = gameEvent.Actor;
                KillFeed.Add(NameOf(killer), TeamOf(killer), NameOf(victim), TeamOf(victim), _lastLocalNow);
            }
            EventPlayed?.Invoke(gameEvent);
        }
    }
}
