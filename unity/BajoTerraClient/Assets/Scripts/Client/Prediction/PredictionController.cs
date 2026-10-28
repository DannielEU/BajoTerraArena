using System;
using Bajoterra.Client.Content;
using Bajoterra.Client.Input;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Prediction
{
    /// <summary>
    /// Client-side prediction with server reconciliation for the local player's movement.
    ///
    /// Every client tick the input is applied immediately to a local copy of the body (<see cref="Predict"/>), so
    /// movement feels instant. When a snapshot arrives it carries the authoritative state at some server tick plus
    /// the sequence number of the last input the server applied (<c>ack_sequence</c>); the controller restarts from
    /// that state and replays the inputs the server has not seen yet (<see cref="Reconcile"/>). If the replay ends
    /// somewhere else than the prediction did, the difference is blended away over a few frames instead of
    /// teleporting the character, unless it is large (then it snaps).
    /// </summary>
    public sealed class PredictionController
    {
        /// <summary>Corrections larger than this many metres snap instead of blending.</summary>
        public const double SnapDistance = 2.0;

        /// <summary>Time constant of the visual error decay, in seconds.</summary>
        public const double BlendSeconds = 0.1;

        private const int HistoryCapacity = 512;

        private readonly ArenaMap _map;
        private readonly CharacterInfo _character;
        private readonly int _yourId;
        private readonly PredictedBody _current;
        private readonly PredictedBody _scratch;
        private readonly uint[] _historySequence = new uint[HistoryCapacity];
        private readonly PlayerInput[] _historyInput = new PlayerInput[HistoryCapacity];
        private int _historyHead;
        private int _historyCount;
        private bool _active;
        private double _offsetX;
        private double _offsetY;
        private double _offsetZ;

        public PredictionController(ContentCatalog content, ArenaMap map, CharacterInfo character, int yourId)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _character = character ?? throw new ArgumentNullException(nameof(character));
            _yourId = yourId;
            MoveInfo[] infos = content.MoveSetOf(character);
            var rules = new MoveRule[infos.Length];
            for (int i = 0; i < rules.Length; i++)
            {
                rules[i] = new MoveRule(infos[i]);
            }
            _current = new PredictedBody(character, rules);
            _scratch = new PredictedBody(character, rules);
            Enabled = true;
        }

        /// <summary>When false the body simply follows the server (useful to compare feel or to debug).</summary>
        public bool Enabled { get; set; }

        /// <summary>The predicted state at the newest client tick.</summary>
        public PredictedBody Current
        {
            get { return _current; }
        }

        /// <summary>True while the local player is being predicted (the round is running and the player is alive).</summary>
        public bool IsPredicting
        {
            get { return _active; }
        }

        /// <summary>Metres between where the client thought it was and where the server's state replays to.</summary>
        public double LastError { get; private set; }

        public int ReconcileCount { get; private set; }

        public int SnapCount { get; private set; }

        /// <summary>Number of inputs sent that the server has not acknowledged yet.</summary>
        public int PendingInputs
        {
            get { return _historyCount; }
        }

        /// <summary>Position to draw the player at: the predicted body plus the blended correction.</summary>
        public Vec3 DisplayPosition
        {
            get
            {
                return new Vec3((float)(_current.X + _offsetX), (float)(_current.Y + _offsetY),
                    (float)(_current.Z + _offsetZ));
            }
        }

        /// <summary>
        /// Applies one input to the predicted body. Call it once per client tick with exactly the input that is
        /// sent to the server under <paramref name="sequence"/>.
        /// </summary>
        public void Predict(uint sequence, in PlayerInput input)
        {
            // The input is recorded even while not predicting (countdown, between snapshots), so the first
            // reconciliation of a round can replay everything the server has not acknowledged.
            if (_historyCount == HistoryCapacity)
            {
                // The server stopped acknowledging; forget the oldest input rather than grow without bound.
                _historyHead = (_historyHead + 1) % HistoryCapacity;
                _historyCount--;
            }
            int slot = (_historyHead + _historyCount) % HistoryCapacity;
            _historySequence[slot] = sequence;
            _historyInput[slot] = input;
            _historyCount++;
            if (_active)
            {
                MovementModel.Step(_current, input, _map);
            }
        }

        /// <summary>Takes the authoritative state from a snapshot and replays the unacknowledged inputs.</summary>
        public void Reconcile(Snapshot snapshot)
        {
            CombatantState own = null;
            for (int i = 0; i < snapshot.Combatants.Count; i++)
            {
                if (snapshot.Combatants[i].Id == (uint)_yourId)
                {
                    own = snapshot.Combatants[i];
                    break;
                }
            }
            if (own == null)
            {
                return;
            }
            bool shouldPredict = Enabled && snapshot.Local != null && own.Alive
                                 && snapshot.Match != null && snapshot.Match.Phase == MatchPhase.RoundActive;
            if (!shouldPredict)
            {
                if (snapshot.Local != null)
                {
                    _current.LoadFrom(own, snapshot.Local, (int)snapshot.Tick, _character);
                }
                else
                {
                    _current.X = own.X;
                    _current.Y = own.Y;
                    _current.Z = own.Z;
                    _current.Alive = own.Alive;
                }
                _active = false;
                _historyCount = 0;
                _offsetX = _offsetY = _offsetZ = 0.0;
                LastError = 0.0;
                return;
            }

            _scratch.LoadFrom(own, snapshot.Local, (int)snapshot.Tick, _character);
            DropAcknowledged(snapshot.AckSequence);
            for (int i = 0; i < _historyCount; i++)
            {
                MovementModel.Step(_scratch, _historyInput[(_historyHead + i) % HistoryCapacity], _map);
            }

            if (_active)
            {
                double errorX = _current.X - _scratch.X;
                double errorY = _current.Y - _scratch.Y;
                double errorZ = _current.Z - _scratch.Z;
                double error = Math.Sqrt(errorX * errorX + errorY * errorY + errorZ * errorZ);
                LastError = error;
                if (error > SnapDistance)
                {
                    _offsetX = _offsetY = _offsetZ = 0.0;
                    SnapCount++;
                }
                else
                {
                    // What was drawn stays continuous: new display = replayed state + (old display - replayed state).
                    _offsetX += errorX;
                    _offsetY += errorY;
                    _offsetZ += errorZ;
                }
            }
            else
            {
                LastError = 0.0;
                _offsetX = _offsetY = _offsetZ = 0.0;
            }
            ReconcileCount++;
            _current.CopyFrom(_scratch);
            _active = true;
        }

        /// <summary>Blends the visual correction away; call once per rendered frame.</summary>
        public void Smooth(double deltaSeconds)
        {
            double keep = Math.Exp(-Math.Max(0.0, deltaSeconds) / BlendSeconds);
            _offsetX *= keep;
            _offsetY *= keep;
            _offsetZ *= keep;
        }

        private void DropAcknowledged(uint ack)
        {
            while (_historyCount > 0 && !IsAfter(_historySequence[_historyHead], ack))
            {
                _historyHead = (_historyHead + 1) % HistoryCapacity;
                _historyCount--;
            }
        }

        /// <summary>True when sequence <paramref name="a"/> is newer than <paramref name="b"/> (wrap-safe).</summary>
        private static bool IsAfter(uint a, uint b)
        {
            return a != b && unchecked(a - b) < 0x80000000u;
        }
    }
}
