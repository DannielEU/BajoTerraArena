using System;
using Bajoterra.Client.Content;
using Bajoterra.Client.Session;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Hud
{
    /// <summary>One of the ten slug slots as the HUD shows it.</summary>
    public struct SlugSlotView
    {
        public int SlugIndex;
        public string Name;
        public string Element;
        public string Role;
        public Targeting Targeting;

        /// <summary>0 when the slug can be fired; 1-3 are the shots that must still pass.</summary>
        public int ShotsUntilReady;

        public bool Selected;

        public bool Ready
        {
            get { return ShotsUntilReady == 0; }
        }
    }

    /// <summary>
    /// What the HUD displays, computed from the match state with no engine involved (view-model). The Unity UI
    /// only copies these values onto widgets, so everything about the numbers is testable without Unity.
    /// </summary>
    public sealed class HudModel
    {
        public const int SlotCount = 10;

        public readonly SlugSlotView[] Slots = new SlugSlotView[SlotCount];

        public bool Alive;
        public float Hp;
        public float MaxHp;
        public float Shield;
        public float MaxShield;
        public float Overshield;

        public MatchPhase Phase;
        public int Round;
        public int WinsOwnTeam;
        public int WinsOtherTeam;
        public int SecondsLeft;

        /// <summary>Round-trip time to the server, in milliseconds (0 until measured).</summary>
        public int PingMs;

        /// <summary>How far the blaster is through the wait before the next shot: 0 just fired, 1 ready.</summary>
        public float BlasterProgress = 1f;

        /// <summary>True when the blaster can fire again (the wait is over; the slot may still be locked).</summary>
        public bool BlasterReady = true;

        /// <summary>Slots whose slug can be fired right now.</summary>
        public int ReadySlots;

        /// <summary>The selected slot's index, or -1.</summary>
        public int SelectedSlot = -1;

        /// <summary>Your courage level: how many teammates your team is down (0 when it is not outnumbered).</summary>
        public int Courage;

        /// <summary>True while an enemy grenade is stuck to you.</summary>
        public bool GrenadeOnYou;

        /// <summary>Seconds left before the grenade stuck to you explodes (the soonest one, if several).</summary>
        public float GrenadeSeconds;

        public bool HasState { get; private set; }

        public string CharacterName { get; private set; }

        /// <summary>Copies the current values out of the match; call once per frame before drawing.</summary>
        public void Refresh(MatchSession match, int pingMs)
        {
            PingMs = pingMs;
            Snapshot latest = match.Latest;
            CombatantState you = match.YourState;
            HasState = latest != null && you != null;
            if (!HasState)
            {
                return;
            }
            Slot slot = match.You;
            CharacterName = match.Content.FindCharacter(slot.CharacterId)?.Name ?? slot.CharacterId;
            Alive = you.Alive;
            Hp = you.Hp;
            MaxHp = slot.MaxHp;
            Shield = you.Shield;
            MaxShield = slot.MaxShield;
            Overshield = you.Overshield;
            Courage = you.Alive ? (int)you.Courage : 0;
            RefreshGrenade(latest, match);

            ReadySlots = 0;
            SelectedSlot = -1;
            for (int i = 0; i < SlotCount; i++)
            {
                if (i >= you.Slots.Count)
                {
                    Slots[i] = default;
                    continue;
                }
                SlotState state = you.Slots[i];
                SlugInfo info = match.Content.Slug((int)state.SlugIndex);
                Slots[i].SlugIndex = (int)state.SlugIndex;
                Slots[i].Name = info.Name;
                Slots[i].Element = match.Content.ElementName((int)info.Element);
                Slots[i].Role = info.Role;
                Slots[i].Targeting = info.Targeting;
                Slots[i].ShotsUntilReady = (int)state.ShotsUntilReady;
                Slots[i].Selected = i == (int)you.SelectedSlot;
                if (state.ShotsUntilReady == 0)
                {
                    ReadySlots++;
                }
                if (Slots[i].Selected)
                {
                    SelectedSlot = i;
                }
            }

            double since = match.LocalNow - match.OwnShotAt;
            float wait = match.OwnShotCooldown;
            BlasterProgress = wait <= 0f ? 1f : (float)Math.Max(0.0, Math.Min(1.0, since / wait));
            BlasterReady = BlasterProgress >= 1f;

            MatchInfo info2 = latest.Match;
            Phase = info2.Phase;
            Round = (int)info2.Round;
            bool team0 = slot.Team == 0;
            WinsOwnTeam = (int)(team0 ? info2.WinsTeam0 : info2.WinsTeam1);
            WinsOtherTeam = (int)(team0 ? info2.WinsTeam1 : info2.WinsTeam0);
            SecondsLeft = (int)Math.Ceiling(info2.TicksLeft / (double)match.TicksPerSecond);
        }

        private void RefreshGrenade(Snapshot latest, MatchSession match)
        {
            GrenadeOnYou = false;
            GrenadeSeconds = 0f;
            int soonest = int.MaxValue;
            for (int i = 0; i < latest.Projectiles.Count; i++)
            {
                ProjectileState p = latest.Projectiles[i];
                if (p.Stuck && p.StuckTo == match.YourId && match.TeamOf((int)p.Owner) != match.YourTeam
                    && (int)p.FuseTicks < soonest)
                {
                    soonest = (int)p.FuseTicks;
                }
            }
            if (soonest != int.MaxValue)
            {
                GrenadeOnYou = true;
                GrenadeSeconds = soonest / (float)match.TicksPerSecond;
            }
        }

        /// <summary>Formats seconds as m:ss.</summary>
        public static string Clock(int seconds)
        {
            seconds = Math.Max(0, seconds);
            return (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }

        /// <summary>Fraction in [0, 1] of a bar, safe for a zero maximum.</summary>
        public static float Fraction(float value, float max)
        {
            return max <= 0f ? 0f : Math.Max(0f, Math.Min(1f, value / max));
        }
    }
}
