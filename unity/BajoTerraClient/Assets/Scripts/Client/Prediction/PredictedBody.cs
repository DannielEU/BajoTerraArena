using System;
using Bajoterra.Client.Content;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Prediction
{
    /// <summary>How the client reproduces the numeric effect of a move.</summary>
    public enum BehaviorKind
    {
        /// <summary>Only the data modifiers apply (crouch) or the behavior is unknown to this client.</summary>
        None,

        /// <summary>One-off velocity change when the move starts (jump).</summary>
        Impulse,

        /// <summary>Fixed horizontal velocity while the move lasts (roll, dash).</summary>
        Drive,

        /// <summary>A kick off a wall in mid-air: up and away from the wall (wall_kick).</summary>
        WallKick
    }

    /// <summary>A move of the player's character, with the behavior parameters already parsed.</summary>
    public sealed class MoveRule
    {
        public MoveRule(MoveInfo info)
        {
            Info = info;
            float value;
            switch (info.BehaviorType)
            {
                case "impulse":
                    Kind = BehaviorKind.Impulse;
                    Vertical = info.BehaviorParams.TryGetValue("vertical", out value) ? value : 0.0;
                    Horizontal = info.BehaviorParams.TryGetValue("horizontal", out value) ? value : 0.0;
                    break;
                case "drive":
                    Kind = BehaviorKind.Drive;
                    DriveSpeed = info.BehaviorParams.TryGetValue("speed", out value) ? value : 0.0;
                    break;
                case "wallkick":
                    Kind = BehaviorKind.WallKick;
                    Vertical = info.BehaviorParams.TryGetValue("vertical", out value) ? value : 0.0;
                    Horizontal = info.BehaviorParams.TryGetValue("push", out value) ? value : 0.0;
                    MaxKicks = info.BehaviorParams.TryGetValue("maxKicks", out value) ? (int)Math.Round(value) : 1;
                    break;
                default:
                    Kind = BehaviorKind.None;
                    break;
            }
        }

        public MoveInfo Info { get; }

        public BehaviorKind Kind { get; }

        public double Vertical { get; }

        public double Horizontal { get; }

        public double DriveSpeed { get; }

        /// <summary>Wall kicks allowed before landing (wall kick only).</summary>
        public int MaxKicks { get; }
    }

    /// <summary>
    /// The movement state of the local player's combatant: everything the server's movement code reads and writes
    /// for one combatant. Mutable and reused, so stepping it never allocates.
    /// </summary>
    public sealed class PredictedBody
    {
        public const int MaxMoves = 32;

        public readonly int[] CooldownUntil = new int[MaxMoves];

        public PredictedBody(CharacterInfo character, MoveRule[] moves)
        {
            Speed = character.Speed;
            HitRadius = character.HitRadius;
            HitHeight = character.HitHeight;
            Moves = moves;
            Reset();
        }

        // Constants of the character.
        public double Speed { get; }

        public double HitRadius { get; }

        public double HitHeight { get; }

        public MoveRule[] Moves { get; }

        // Kinematic state (feet position).
        public double X;
        public double Y;
        public double Z;
        public double VelY;
        public double KnockX;
        public double KnockZ;
        public double Yaw;
        public bool Grounded = true;
        public bool Alive = true;
        public Posture Posture = Posture.Grounded;

        /// <summary>Server tick this state corresponds to; every step advances it by one.</summary>
        public int Tick;

        // Move runner.
        public int ActiveSlot = -1;
        public int Elapsed;
        public double DirX;
        public double DirZ = 1.0;

        /// <summary>Move buttons held on the previous tick (a wall kick needs a fresh press).</summary>
        public int LastMask;

        // Wall kicks: the wall within reach after the last step, kicks since landing and the last wall kicked off.
        public double WallNX;
        public double WallNZ;
        public int WallKicks;
        public double LastKickX;
        public double LastKickZ;

        // Effects of statuses on movement (slows, freezes, stun), constant over a replay window.
        public double StatusSpeed = 1.0;
        public bool Stunned;

        /// <summary>Current hitbox height after the active move's modifier (set by every step).</summary>
        public double HitHeightNow;

        public void Reset()
        {
            X = Y = Z = VelY = KnockX = KnockZ = Yaw = 0.0;
            Grounded = true;
            Alive = true;
            Posture = Posture.Grounded;
            Tick = 0;
            ActiveSlot = -1;
            Elapsed = 0;
            DirX = 0.0;
            DirZ = 1.0;
            LastMask = 0;
            WallNX = WallNZ = LastKickX = LastKickZ = 0.0;
            WallKicks = 0;
            StatusSpeed = 1.0;
            Stunned = false;
            HitHeightNow = HitHeight;
            Array.Clear(CooldownUntil, 0, CooldownUntil.Length);
        }

        public void CopyFrom(PredictedBody other)
        {
            X = other.X;
            Y = other.Y;
            Z = other.Z;
            VelY = other.VelY;
            KnockX = other.KnockX;
            KnockZ = other.KnockZ;
            Yaw = other.Yaw;
            Grounded = other.Grounded;
            Alive = other.Alive;
            Posture = other.Posture;
            Tick = other.Tick;
            ActiveSlot = other.ActiveSlot;
            Elapsed = other.Elapsed;
            DirX = other.DirX;
            DirZ = other.DirZ;
            LastMask = other.LastMask;
            WallNX = other.WallNX;
            WallNZ = other.WallNZ;
            WallKicks = other.WallKicks;
            LastKickX = other.LastKickX;
            LastKickZ = other.LastKickZ;
            StatusSpeed = other.StatusSpeed;
            Stunned = other.Stunned;
            HitHeightNow = other.HitHeightNow;
            Array.Copy(other.CooldownUntil, CooldownUntil, CooldownUntil.Length);
        }

        /// <summary>
        /// Loads the authoritative state the server sent for the local player: the public combatant state plus the
        /// extra movement state of <c>LocalState</c>.
        /// </summary>
        public void LoadFrom(CombatantState state, LocalState local, int serverTick, CharacterInfo character)
        {
            Alive = state.Alive;
            X = state.X;
            Y = state.Y;
            Z = state.Z;
            Yaw = state.Yaw;
            Posture = state.Posture;
            HitHeightNow = state.HitboxHeight;
            Tick = serverTick;
            VelY = local.VelY;
            KnockX = local.KnockX;
            KnockZ = local.KnockZ;
            Grounded = local.Grounded;
            StatusSpeed = local.StatusSpeedMultiplier;
            Stunned = local.Stunned;
            Elapsed = (int)local.MoveElapsedTicks;
            DirX = local.MoveDirX;
            DirZ = local.MoveDirZ;
            LastMask = (int)local.LastMoveMask;
            WallNX = local.WallNormalX;
            WallNZ = local.WallNormalZ;
            WallKicks = (int)local.WallKicks;
            LastKickX = local.LastKickX;
            LastKickZ = local.LastKickZ;
            ActiveSlot = -1;
            if (state.ActiveMove >= 0)
            {
                for (int slot = 0; slot < character.MoveIndices.Count; slot++)
                {
                    if (character.MoveIndices[slot] == (uint)state.ActiveMove)
                    {
                        ActiveSlot = slot;
                        break;
                    }
                }
            }
            Array.Clear(CooldownUntil, 0, CooldownUntil.Length);
            for (int slot = 0; slot < local.MoveCooldownTicks.Count && slot < MaxMoves; slot++)
            {
                CooldownUntil[slot] = serverTick + (int)local.MoveCooldownTicks[slot];
            }
        }
    }
}
