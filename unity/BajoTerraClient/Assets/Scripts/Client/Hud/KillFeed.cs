using System.Collections.Generic;

namespace Bajoterra.Client.Hud
{
    /// <summary>One line of the kill feed.</summary>
    public readonly struct KillFeedEntry
    {
        public readonly string Killer;
        public readonly int KillerTeam;
        public readonly string Victim;
        public readonly int VictimTeam;
        public readonly double ExpiresAt;

        public KillFeedEntry(string killer, int killerTeam, string victim, int victimTeam, double expiresAt)
        {
            Killer = killer;
            KillerTeam = killerTeam;
            Victim = victim;
            VictimTeam = victimTeam;
            ExpiresAt = expiresAt;
        }

        /// <summary>True when there was no killer (the victim fell to a status effect or an explosion with no owner).</summary>
        public bool Unattributed
        {
            get { return Killer == null; }
        }
    }

    /// <summary>The most recent defeats, newest last, each shown for a few seconds.</summary>
    public sealed class KillFeed
    {
        public const int MaxEntries = 5;
        public const double LifetimeSeconds = 6.0;

        private readonly List<KillFeedEntry> _entries = new List<KillFeedEntry>(MaxEntries + 1);

        public int Count
        {
            get { return _entries.Count; }
        }

        public KillFeedEntry this[int index]
        {
            get { return _entries[index]; }
        }

        public void Add(string killer, int killerTeam, string victim, int victimTeam, double now)
        {
            _entries.Add(new KillFeedEntry(killer, killerTeam, victim, victimTeam, now + LifetimeSeconds));
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(0);
            }
        }

        /// <summary>Drops entries whose time is up.</summary>
        public void Update(double now)
        {
            while (_entries.Count > 0 && _entries[0].ExpiresAt <= now)
            {
                _entries.RemoveAt(0);
            }
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}
