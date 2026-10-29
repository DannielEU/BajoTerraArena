using Bajoterra.Protocol;

namespace Bajoterra.Client.Interpolation
{
    /// <summary>A combatant as it should be drawn at the render time.</summary>
    public struct CombatantFrame
    {
        public int Id;
        public Vec3 Position;
        public float Yaw;
        public float Pitch;
        public bool Alive;
        public Posture Posture;
        public int ActiveMove;
        public float HitboxHeight;

        /// <summary>Latest authoritative state: vitals, slots and statuses are not interpolated.</summary>
        public CombatantState State;
    }

    /// <summary>A slug in flight (or a grenade stuck somewhere) as it should be drawn at the render time.</summary>
    public struct ProjectileFrame
    {
        public uint Id;
        public int SlugIndex;
        public int Owner;
        public Vec3 Position;
        public Vec3 Velocity;

        /// <summary>A sticky slug that landed: it no longer flies and its fuse is burning.</summary>
        public bool Stuck;

        /// <summary>Stuck slugs: simulation ticks left before the explosion.</summary>
        public int FuseTicks;

        /// <summary>Stuck slugs: the combatant it clings to, or -1 when it is stuck to the world.</summary>
        public int StuckTo;
    }

    public struct PickupFrame
    {
        public int Index;
        public bool Available;
    }

    /// <summary>
    /// What the renderer needs for one frame. The arrays are allocated once and refilled by
    /// <see cref="WorldSampler"/>, so sampling at 60+ Hz produces no garbage.
    /// </summary>
    public sealed class WorldFrame
    {
        public const int MaxCombatants = 16;
        public const int MaxProjectiles = 256;
        public const int MaxPickups = 64;

        public readonly CombatantFrame[] Combatants = new CombatantFrame[MaxCombatants];
        public readonly ProjectileFrame[] Projectiles = new ProjectileFrame[MaxProjectiles];
        public readonly PickupFrame[] Pickups = new PickupFrame[MaxPickups];

        public int CombatantCount;
        public int ProjectileCount;
        public int PickupCount;

        /// <summary>The newest match info (phase, round, score).</summary>
        public MatchInfo Match;

        /// <summary>Server time the frame was sampled at.</summary>
        public double RenderTime;

        /// <summary>False until the first snapshot arrived.</summary>
        public bool Valid;

        /// <summary>Index of the combatant with the given id in <see cref="Combatants"/>, or -1.</summary>
        public int IndexOf(int id)
        {
            for (int i = 0; i < CombatantCount; i++)
            {
                if (Combatants[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
