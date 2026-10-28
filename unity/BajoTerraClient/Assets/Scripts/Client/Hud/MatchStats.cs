using System;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Hud
{
    /// <summary>One combatant's tally for the scoreboard.</summary>
    public struct StatLine
    {
        public int Kills;
        public int Deaths;
        public float DamageDealt;
        public float DamageTaken;
        public float Healing;
    }

    /// <summary>
    /// What happened in the match so far, counted from the gameplay events as they are played: kills, deaths, damage
    /// dealt and taken and healing done for every combatant, and who hurt each combatant last (for the "defeated by"
    /// panel). It needs no engine, so the numbers are tested without Unity.
    /// </summary>
    public sealed class MatchStats
    {
        private readonly StatLine[] _lines;
        private readonly int[] _lastAttacker;
        private readonly int[] _lastElement;
        private readonly float[] _lastDamage;
        private readonly Func<int, int> _teamOf;

        public MatchStats(int combatants, Func<int, int> teamOf)
        {
            _lines = new StatLine[combatants];
            _lastAttacker = new int[combatants];
            _lastElement = new int[combatants];
            _lastDamage = new float[combatants];
            _teamOf = teamOf ?? throw new ArgumentNullException(nameof(teamOf));
            ForgetAttackers();
        }

        public int Count
        {
            get { return _lines.Length; }
        }

        public StatLine Line(int combatant)
        {
            return combatant >= 0 && combatant < _lines.Length ? _lines[combatant] : default;
        }

        /// <summary>The combatant that hurt <paramref name="victim"/> last since the round began, or -1.</summary>
        public int LastAttacker(int victim)
        {
            return Valid(victim) ? _lastAttacker[victim] : -1;
        }

        /// <summary>The element index of that last hit (an index in the manifest's element list), or -1.</summary>
        public int LastElement(int victim)
        {
            return Valid(victim) ? _lastElement[victim] : -1;
        }

        /// <summary>How much that last hit took.</summary>
        public float LastDamage(int victim)
        {
            return Valid(victim) ? _lastDamage[victim] : 0f;
        }

        /// <summary>Forgets who hurt whom (a new round starts clean).</summary>
        public void ForgetAttackers()
        {
            for (int i = 0; i < _lastAttacker.Length; i++)
            {
                _lastAttacker[i] = -1;
                _lastElement[i] = -1;
                _lastDamage[i] = 0f;
            }
        }

        /// <summary>Counts one gameplay event; events that do not matter are ignored.</summary>
        public void Record(GameEvent e)
        {
            switch (e.Kind)
            {
                case EventKind.Damage:
                    OnDamage(e);
                    break;
                case EventKind.Defeated:
                    if (Valid(e.Subject))
                    {
                        _lines[e.Subject].Deaths++;
                    }
                    if (Valid(e.Actor) && e.Actor != e.Subject && _teamOf(e.Actor) != _teamOf(e.Subject))
                    {
                        _lines[e.Actor].Kills++;
                    }
                    break;
                case EventKind.Heal:
                case EventKind.ShieldRestored:
                    if (Valid(e.Actor) && e.Actor != e.Subject && e.Value > 0f)
                    {
                        _lines[e.Actor].Healing += e.Value;
                    }
                    break;
                case EventKind.RoundStarted:
                    ForgetAttackers();
                    break;
            }
        }

        private void OnDamage(GameEvent e)
        {
            if (!Valid(e.Subject) || e.Value <= 0f)
            {
                return;
            }
            _lines[e.Subject].DamageTaken += e.Value;
            if (Valid(e.Actor) && e.Actor != e.Subject)
            {
                _lastAttacker[e.Subject] = e.Actor;
                _lastElement[e.Subject] = e.A;
                _lastDamage[e.Subject] = e.Value;
                if (_teamOf(e.Actor) != _teamOf(e.Subject))
                {
                    _lines[e.Actor].DamageDealt += e.Value;
                }
            }
        }

        private bool Valid(int combatant)
        {
            return combatant >= 0 && combatant < _lines.Length;
        }
    }
}
