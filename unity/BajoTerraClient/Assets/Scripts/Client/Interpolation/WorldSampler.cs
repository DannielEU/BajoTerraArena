using System;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Interpolation
{
    /// <summary>
    /// Fills a <see cref="WorldFrame"/> from the snapshot buffer at a render time that lags the newest snapshot by
    /// a fixed interpolation delay. Combatants are interpolated linearly (angles along the shortest arc). Slugs are
    /// interpolated when both surrounding snapshots contain them and extrapolated along their velocity otherwise,
    /// so a shot appears and disappears smoothly.
    /// </summary>
    public static class WorldSampler
    {
        /// <summary>How far past the newest snapshot moving things may be extrapolated, in seconds.</summary>
        public const double MaxExtrapolation = 0.25;

        public static void Sample(SnapshotBuffer buffer, double renderTime, WorldFrame frame)
        {
            frame.RenderTime = renderTime;
            if (!buffer.Find(renderTime, out TimedSnapshot from, out TimedSnapshot to, out double alpha,
                    out double overshoot))
            {
                frame.Valid = false;
                frame.CombatantCount = 0;
                frame.ProjectileCount = 0;
                frame.PickupCount = 0;
                return;
            }
            frame.Valid = true;
            Snapshot a = from.Snapshot;
            Snapshot b = to.Snapshot;
            float t = (float)alpha;
            frame.Match = b.Match;

            SampleCombatants(a, b, t, frame);
            SampleProjectiles(a, b, from.ServerTime, to.ServerTime, t, Math.Min(overshoot, MaxExtrapolation), frame);

            int pickups = Math.Min(b.Pickups.Count, WorldFrame.MaxPickups);
            for (int i = 0; i < pickups; i++)
            {
                frame.Pickups[i].Index = (int)b.Pickups[i].Index;
                frame.Pickups[i].Available = b.Pickups[i].Available;
            }
            frame.PickupCount = pickups;
        }

        private static void SampleCombatants(Snapshot a, Snapshot b, float t, WorldFrame frame)
        {
            int count = Math.Min(b.Combatants.Count, WorldFrame.MaxCombatants);
            for (int i = 0; i < count; i++)
            {
                CombatantState to = b.Combatants[i];
                CombatantState from = FindCombatant(a, to.Id, i) ?? to;
                ref CombatantFrame c = ref frame.Combatants[i];
                c.Id = (int)to.Id;
                // A big jump is a teleport (a new round placed everybody): do not drag the body across the map.
                bool teleported = Math.Abs(to.X - from.X) + Math.Abs(to.Z - from.Z) > 6f;
                float k = teleported ? 1f : t;
                c.Position = new Vec3(Lerp(from.X, to.X, k), Lerp(from.Y, to.Y, k), Lerp(from.Z, to.Z, k));
                c.Yaw = Angles.LerpAngle(from.Yaw, to.Yaw, k);
                c.Pitch = Lerp(from.Pitch, to.Pitch, k);
                bool useNewer = t >= 0.5f;
                CombatantState discrete = useNewer ? to : from;
                c.Alive = to.Alive && discrete.Alive;
                c.Posture = discrete.Posture;
                c.ActiveMove = discrete.ActiveMove;
                c.HitboxHeight = Lerp(from.HitboxHeight, to.HitboxHeight, k);
                c.State = to;
            }
            frame.CombatantCount = count;
        }

        private static void SampleProjectiles(Snapshot a, Snapshot b, double timeA, double timeB, float t,
            double overshoot, WorldFrame frame)
        {
            int count = 0;
            double span = Math.Max(1e-6, timeB - timeA);
            for (int i = 0; i < b.Projectiles.Count && count < WorldFrame.MaxProjectiles; i++)
            {
                ProjectileState to = b.Projectiles[i];
                ProjectileState from = FindProjectile(a, to.Id, i);
                Vec3 velocity = new Vec3(to.Vx, to.Vy, to.Vz);
                Vec3 position;
                if (from != null && !ReferenceEquals(a, b))
                {
                    position = new Vec3(Lerp(from.X, to.X, t), Lerp(from.Y, to.Y, t), Lerp(from.Z, to.Z, t));
                }
                else if (from != null)
                {
                    // Render time is past the newest snapshot (or before the oldest): extrapolate along the velocity.
                    position = new Vec3(to.X, to.Y, to.Z) + velocity * (float)overshoot;
                }
                else
                {
                    // New since the older snapshot: it started somewhere in the last interval, so trace it back.
                    float back = (float)((1.0 - t) * span);
                    position = new Vec3(to.X, to.Y, to.Z) - velocity * back + velocity * (float)overshoot;
                }
                frame.Projectiles[count].Id = to.Id;
                frame.Projectiles[count].SlugIndex = (int)to.SlugIndex;
                frame.Projectiles[count].Owner = (int)to.Owner;
                frame.Projectiles[count].Position = position;
                frame.Projectiles[count].Velocity = velocity;
                count++;
            }
            frame.ProjectileCount = count;
        }

        private static CombatantState FindCombatant(Snapshot snapshot, uint id, int hint)
        {
            if (hint < snapshot.Combatants.Count && snapshot.Combatants[hint].Id == id)
            {
                return snapshot.Combatants[hint];
            }
            for (int i = 0; i < snapshot.Combatants.Count; i++)
            {
                if (snapshot.Combatants[i].Id == id)
                {
                    return snapshot.Combatants[i];
                }
            }
            return null;
        }

        private static ProjectileState FindProjectile(Snapshot snapshot, uint id, int hint)
        {
            if (hint < snapshot.Projectiles.Count && snapshot.Projectiles[hint].Id == id)
            {
                return snapshot.Projectiles[hint];
            }
            for (int i = 0; i < snapshot.Projectiles.Count; i++)
            {
                if (snapshot.Projectiles[i].Id == id)
                {
                    return snapshot.Projectiles[i];
                }
            }
            return null;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
