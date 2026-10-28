using System;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Content
{
    /// <summary>The direction in which a ramp's surface gets higher (the numbering of <c>ObstacleInfo.rise</c>).</summary>
    public enum RampDirection
    {
        None = 0,
        PosX = 1,
        NegX = 2,
        PosZ = 3,
        NegZ = 4
    }

    /// <summary>
    /// An axis-aligned box of the arena with the three flags the server uses. When <see cref="Rise"/> is not
    /// <see cref="RampDirection.None"/> it is a ramp: a wedge with a flat underside at <c>MinY</c> whose top surface
    /// climbs in a straight line from <c>MinY</c> at one edge to <c>MaxY</c> at the opposite one.
    /// </summary>
    public readonly struct ObstacleBox
    {
        public readonly string Id;
        public readonly float MinX;
        public readonly float MinY;
        public readonly float MinZ;
        public readonly float MaxX;
        public readonly float MaxY;
        public readonly float MaxZ;
        public readonly bool BlocksProjectiles;
        public readonly bool BlocksMovement;
        public readonly bool BlocksLineOfSight;
        public readonly RampDirection Rise;

        public ObstacleBox(ObstacleInfo info)
        {
            Id = info.Id;
            MinX = info.MinX;
            MinY = info.MinY;
            MinZ = info.MinZ;
            MaxX = info.MaxX;
            MaxY = info.MaxY;
            MaxZ = info.MaxZ;
            BlocksProjectiles = info.BlocksProjectiles;
            BlocksMovement = info.BlocksMovement;
            BlocksLineOfSight = info.BlocksLineOfSight;
            Rise = info.Rise <= (uint)RampDirection.NegZ ? (RampDirection)info.Rise : RampDirection.None;
        }

        public bool IsRamp
        {
            get { return Rise != RampDirection.None; }
        }

        /// <summary>Metres of height gained per metre walked up the ramp (0 for a plain box).</summary>
        public double Slope
        {
            get
            {
                switch (Rise)
                {
                    case RampDirection.PosX:
                    case RampDirection.NegX:
                        return (MaxY - MinY) / (double)(MaxX - MinX);
                    case RampDirection.PosZ:
                    case RampDirection.NegZ:
                        return (MaxY - MinY) / (double)(MaxZ - MinZ);
                    default:
                        return 0.0;
                }
            }
        }

        /// <summary>Height of the ramp's sloped plane extended over the whole XZ plane (<c>MaxY</c> for a plain box).</summary>
        public double PlaneHeight(double x, double z)
        {
            switch (Rise)
            {
                case RampDirection.PosX: return MinY + Slope * (x - MinX);
                case RampDirection.NegX: return MinY + Slope * (MaxX - x);
                case RampDirection.PosZ: return MinY + Slope * (z - MinZ);
                case RampDirection.NegZ: return MinY + Slope * (MaxZ - z);
                default: return MaxY;
            }
        }

        /// <summary>
        /// Height of the surface a body at (x, z) stands on: <c>MaxY</c> for a plain box, the point of the slope for
        /// a ramp (clamped into the footprint first).
        /// </summary>
        public double TopAt(double x, double z)
        {
            if (Rise == RampDirection.None)
            {
                return MaxY;
            }
            return PlaneHeight(Math.Min(Math.Max(x, MinX), MaxX), Math.Min(Math.Max(z, MinZ), MaxZ));
        }

        public Vec3 Center
        {
            get { return new Vec3((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f, (MinZ + MaxZ) * 0.5f); }
        }

        public Vec3 Size
        {
            get { return new Vec3(MaxX - MinX, MaxY - MinY, MaxZ - MinZ); }
        }

        /// <summary>True when the point lies inside the box footprint (height ignored), grown by a margin.</summary>
        public bool ContainsXz(double x, double z, double margin)
        {
            return x >= MinX - margin && x <= MaxX + margin && z >= MinZ - margin && z <= MaxZ + margin;
        }

        /// <summary>
        /// Slab test of a ray against the box grown by <paramref name="radius"/>: the distance along the unit
        /// direction to the first contact, or a negative number when it is missed within <paramref name="maxDistance"/>.
        /// </summary>
        public float RayDistance(Vec3 origin, Vec3 direction, float maxDistance, float radius)
        {
            float tMin = 0f;
            float tMax = maxDistance;
            bool ramp = IsRamp;
            for (int axis = 0; axis < 3; axis++)
            {
                float o = axis == 0 ? origin.X : axis == 1 ? origin.Y : origin.Z;
                float d = axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z;
                float lo = (axis == 0 ? MinX : axis == 1 ? MinY : MinZ) - radius;
                float hi = ramp && axis == 1 ? float.PositiveInfinity : (axis == 0 ? MaxX : axis == 1 ? MaxY : MaxZ) + radius;
                if (Math.Abs(d) < 1e-9f)
                {
                    if (o < lo || o > hi)
                    {
                        return -1f;
                    }
                    continue;
                }
                float t1 = (lo - o) / d;
                float t2 = (hi - o) / d;
                if (t1 > t2)
                {
                    float swap = t1;
                    t1 = t2;
                    t2 = swap;
                }
                tMin = Math.Max(tMin, t1);
                tMax = Math.Min(tMax, t2);
                if (tMin > tMax)
                {
                    return -1f;
                }
            }
            if (ramp)
            {
                // The solid is everything under the sloped surface, moved outwards by the radius along its normal.
                double slope = Slope;
                double gradientX = Rise == RampDirection.PosX ? slope : Rise == RampDirection.NegX ? -slope : 0.0;
                double gradientZ = Rise == RampDirection.PosZ ? slope : Rise == RampDirection.NegZ ? -slope : 0.0;
                double f0 = origin.Y - PlaneHeight(origin.X, origin.Z) - radius * Math.Sqrt(1.0 + slope * slope);
                double fd = direction.Y - (gradientX * direction.X + gradientZ * direction.Z);
                if (Math.Abs(fd) < 1e-9)
                {
                    if (f0 > 0.0)
                    {
                        return -1f;
                    }
                }
                else
                {
                    float boundary = (float)(-f0 / fd);
                    if (fd > 0.0)
                    {
                        tMax = Math.Min(tMax, boundary);
                    }
                    else
                    {
                        tMin = Math.Max(tMin, boundary);
                    }
                    if (tMin > tMax)
                    {
                        return -1f;
                    }
                }
            }
            return tMin;
        }
    }

    /// <summary>A place where a team starts a round.</summary>
    public readonly struct SpawnPoint
    {
        public readonly int Team;
        public readonly Vec3 Position;
        public readonly float Yaw;

        public SpawnPoint(int team, Vec3 position, float yaw)
        {
            Team = team;
            Position = position;
            Yaw = yaw;
        }
    }

    /// <summary>A pickup placed in the arena; its list position is the index used by <c>PickupState</c>.</summary>
    public readonly struct PickupSlot
    {
        public readonly int PickupIndex;
        public readonly Vec3 Position;

        public PickupSlot(int pickupIndex, Vec3 position)
        {
            PickupIndex = pickupIndex;
            Position = position;
        }
    }

    /// <summary>
    /// The arena exactly as the server simulates it. The client draws these boxes, collides its own predicted body
    /// with them and keeps its camera out of them.
    /// </summary>
    public sealed class ArenaMap
    {
        public ArenaMap(MapData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            Name = data.Name;
            MinX = data.MinX;
            MaxX = data.MaxX;
            MinZ = data.MinZ;
            MaxZ = data.MaxZ;
            GroundY = data.GroundY;
            Obstacles = new ObstacleBox[data.Obstacles.Count];
            for (int i = 0; i < Obstacles.Length; i++)
            {
                Obstacles[i] = new ObstacleBox(data.Obstacles[i]);
            }
            Pickups = new PickupSlot[data.Pickups.Count];
            for (int i = 0; i < Pickups.Length; i++)
            {
                PickupPlacement p = data.Pickups[i];
                Pickups[i] = new PickupSlot((int)p.PickupIndex, new Vec3(p.X, p.Y, p.Z));
            }
            Spawns = new SpawnPoint[data.Spawns.Count];
            for (int i = 0; i < Spawns.Length; i++)
            {
                SpawnInfo s = data.Spawns[i];
                Spawns[i] = new SpawnPoint(s.Team, new Vec3(s.X, s.Y, s.Z), s.Yaw);
            }
        }

        public string Name { get; }

        public float MinX { get; }

        public float MaxX { get; }

        public float MinZ { get; }

        public float MaxZ { get; }

        public float GroundY { get; }

        public ObstacleBox[] Obstacles { get; }

        public PickupSlot[] Pickups { get; }

        public SpawnPoint[] Spawns { get; }

        /// <summary>
        /// Distance along a unit direction to the first obstacle that blocks sight, or
        /// <paramref name="maxDistance"/> when the way is clear. Used to keep the third-person camera out of walls.
        /// </summary>
        public float SightDistance(Vec3 origin, Vec3 direction, float maxDistance, float radius)
        {
            float best = Math.Min(maxDistance, BoundsDistance(origin, direction, radius));
            for (int i = 0; i < Obstacles.Length; i++)
            {
                if (!Obstacles[i].BlocksLineOfSight)
                {
                    continue;
                }
                float t = Obstacles[i].RayDistance(origin, direction, best, radius);
                if (t >= 0f && t < best)
                {
                    best = t;
                }
            }
            return best;
        }

        /// <summary>
        /// Distance along a unit direction before leaving the arena's rectangle shrunk by <paramref name="margin"/>
        /// (the walls around the arena are not boxes, but the camera must never go behind them).
        /// </summary>
        public float BoundsDistance(Vec3 origin, Vec3 direction, float margin)
        {
            float best = float.PositiveInfinity;
            if (direction.X > 1e-6f)
            {
                best = Math.Min(best, (MaxX - margin - origin.X) / direction.X);
            }
            else if (direction.X < -1e-6f)
            {
                best = Math.Min(best, (MinX + margin - origin.X) / direction.X);
            }
            if (direction.Z > 1e-6f)
            {
                best = Math.Min(best, (MaxZ - margin - origin.Z) / direction.Z);
            }
            else if (direction.Z < -1e-6f)
            {
                best = Math.Min(best, (MinZ + margin - origin.Z) / direction.Z);
            }
            return Math.Max(0f, best);
        }
    }
}
