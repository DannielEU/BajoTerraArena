using System;
using Bajoterra.Client.Content;
using Bajoterra.Client.Input;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Prediction
{
    /// <summary>
    /// The client's copy of the server's movement rules for one combatant: the locomotion state machine
    /// (jump, crouch, roll, dash...), the integration of velocity, gravity and knockback, and the collision with the
    /// arena boxes. It mirrors <c>LocomotionStateMachine</c>, <c>MovementSystem</c> and <c>World.resolveCombatant</c>
    /// of the Java core line by line, and a golden test replays server-recorded movement to keep both in step.
    ///
    /// The server stays authoritative: any difference only shows up as a small correction when the next snapshot
    /// arrives. All numbers come from the content details the server sends, never from constants in this file,
    /// except the three physical constants below that the server does not send.
    /// </summary>
    public static class MovementModel
    {
        /// <summary>Downward acceleration in m/s^2 (MovementSystem.GRAVITY).</summary>
        public const double Gravity = 25.0;

        /// <summary>Per-tick factor applied to the knockback velocity (MovementSystem.KNOCK_DECAY_PER_TICK).</summary>
        public const double KnockDecay = 0.92;

        /// <summary>Highest ledge a body walks onto without jumping (World.STEP_HEIGHT).</summary>
        public const double StepHeight = 0.35;

        /// <summary>How far down a body that was standing is pulled back onto the ground (World.SNAP_DOWN).</summary>
        public const double SnapDown = 0.35;

        public const double TickSeconds = 1.0 / 60.0;

        /// <summary>Advances the body by one simulation tick under <paramref name="input"/>.</summary>
        public static void Step(PredictedBody body, in PlayerInput input, ArenaMap map)
        {
            body.Tick++;
            body.Yaw = input.AimYaw;
            double moveX = Axis(input.MoveX);
            double moveZ = Axis(input.MoveZ);

            double speedMultiplier = body.StatusSpeed;
            double hitboxMultiplier = 1.0;
            bool driven = false;
            double drivenX = 0.0;
            double drivenZ = 0.0;

            // ----- LocomotionStateMachine.update
            int mask = (int)input.MoveMask;
            if (body.ActiveSlot >= 0 && ShouldEnd(body, mask))
            {
                EndMove(body);
            }
            if (body.Alive && !body.Stunned)
            {
                for (int slot = 0; slot < body.Moves.Length; slot++)
                {
                    if ((mask & (1 << slot)) != 0 && slot != body.ActiveSlot && CanStart(body, slot))
                    {
                        StartMove(body, slot, moveX, moveZ);
                        break;
                    }
                }
            }
            if (body.ActiveSlot >= 0)
            {
                MoveRule rule = body.Moves[body.ActiveSlot];
                speedMultiplier *= rule.Info.SpeedMultiplier;
                hitboxMultiplier *= rule.Info.HitboxHeightMultiplier;
                if (rule.Kind == BehaviorKind.Drive)
                {
                    driven = true;
                    drivenX = body.DirX * rule.DriveSpeed;
                    drivenZ = body.DirZ * rule.DriveSpeed;
                }
                body.Elapsed++;
            }
            body.Posture = ComputePosture(body);
            body.HitHeightNow = body.HitHeight * hitboxMultiplier;

            // ----- MovementSystem.integrate
            double vx = 0.0;
            double vz = 0.0;
            if (body.Alive && !body.Stunned)
            {
                if (driven)
                {
                    vx = drivenX;
                    vz = drivenZ;
                }
                else
                {
                    double length = Math.Sqrt(moveX * moveX + moveZ * moveZ);
                    double mx = moveX;
                    double mz = moveZ;
                    if (length > 1.0)
                    {
                        mx /= length;
                        mz /= length;
                    }
                    double speed = body.Speed * speedMultiplier;
                    double sin = Math.Sin(body.Yaw);
                    double cos = Math.Cos(body.Yaw);
                    // forward = (sin, cos), right = (cos, -sin)
                    vx = (sin * mz + cos * mx) * speed;
                    vz = (cos * mz - sin * mx) * speed;
                }
            }
            if (body.Grounded && body.VelY <= 0.0)
            {
                body.VelY = 0.0;
            }
            else
            {
                body.VelY -= Gravity * TickSeconds;
            }
            body.X += (vx + body.KnockX) * TickSeconds;
            body.Y += body.VelY * TickSeconds;
            body.Z += (vz + body.KnockZ) * TickSeconds;
            body.KnockX *= KnockDecay;
            body.KnockZ *= KnockDecay;
            Resolve(body, map);
        }

        private static double Axis(float value)
        {
            return float.IsNaN(value) ? 0.0 : Math.Max(-1.0, Math.Min(1.0, value));
        }

        // ------------------------------------------------------------------------------------------ locomotion

        private static bool ShouldEnd(PredictedBody body, int mask)
        {
            if (body.Stunned || !body.Alive)
            {
                return true;
            }
            MoveInfo move = body.Moves[body.ActiveSlot].Info;
            if (move.Holdable)
            {
                bool released = (mask & (1 << body.ActiveSlot)) == 0;
                return released || (move.Stance && !body.Grounded);
            }
            return body.Elapsed >= move.DurationTicks;
        }

        private static bool CanStart(PredictedBody body, int slot)
        {
            if (body.Tick < body.CooldownUntil[slot])
            {
                return false;
            }
            if (body.ActiveSlot >= 0 && !body.Moves[body.ActiveSlot].Info.Interruptible)
            {
                return false;
            }
            return (body.Moves[slot].Info.AllowedFromMask & (1u << (int)body.Posture)) != 0;
        }

        private static void StartMove(PredictedBody body, int slot, double moveX, double moveZ)
        {
            if (body.ActiveSlot >= 0)
            {
                EndMove(body);
            }
            double sin = Math.Sin(body.Yaw);
            double cos = Math.Cos(body.Yaw);
            double dirX = sin;
            double dirZ = cos;
            double inputLength = Math.Sqrt(moveX * moveX + moveZ * moveZ);
            if (inputLength > 0.1)
            {
                double right = moveX / inputLength;
                double forward = moveZ / inputLength;
                dirX = sin * forward + cos * right;
                dirZ = cos * forward - sin * right;
            }
            MoveRule rule = body.Moves[slot];
            body.ActiveSlot = slot;
            body.Elapsed = 0;
            body.DirX = dirX;
            body.DirZ = dirZ;
            body.CooldownUntil[slot] = body.Tick + Math.Max(1, (int)rule.Info.CooldownTicks);
            if (rule.Kind == BehaviorKind.Impulse)
            {
                if (rule.Vertical > 0.0)
                {
                    body.VelY = rule.Vertical;
                    body.Grounded = false;
                }
                body.KnockX += dirX * rule.Horizontal;
                body.KnockZ += dirZ * rule.Horizontal;
            }
        }

        private static void EndMove(PredictedBody body)
        {
            body.ActiveSlot = -1;
            body.Elapsed = 0;
        }

        private static Posture ComputePosture(PredictedBody body)
        {
            if (body.ActiveSlot >= 0)
            {
                return body.Moves[body.ActiveSlot].Info.Stance ? Posture.Crouching : Posture.Action;
            }
            return body.Grounded ? Posture.Grounded : Posture.Airborne;
        }

        // ------------------------------------------------------------------------------------------ collision

        private static void Resolve(PredictedBody body, ArenaMap map)
        {
            double radius = body.HitRadius;
            for (int pass = 0; pass < 2; pass++)
            {
                PushOutOfObstacles(body, map, radius);
            }
            body.X = Math.Min(Math.Max(body.X, map.MinX + radius), map.MaxX - radius);
            body.Z = Math.Min(Math.Max(body.Z, map.MinZ + radius), map.MaxZ - radius);
            double ground = GroundHeightAt(map, body.X, body.Z, body.Y, radius);
            if (body.Y <= ground)
            {
                body.Y = ground;
                if (body.VelY < 0.0)
                {
                    body.VelY = 0.0;
                }
            }
            else if (body.Grounded && body.VelY <= 0.001 && body.Y - ground <= SnapDown)
            {
                // Was standing and the surface dropped a little (a ramp going down): follow it.
                body.Y = ground;
                body.VelY = 0.0;
            }
            body.Grounded = body.Y <= ground + 0.02 && body.VelY <= 0.001;
        }

        private static double GroundHeightAt(ArenaMap map, double x, double z, double feetY, double radius)
        {
            double ground = map.GroundY;
            ObstacleBox[] obstacles = map.Obstacles;
            for (int i = 0; i < obstacles.Length; i++)
            {
                ObstacleBox o = obstacles[i];
                if (o.BlocksMovement && o.ContainsXz(x, z, radius * 0.5))
                {
                    double top = o.TopAt(x, z);
                    if (top <= feetY + StepHeight && top > ground)
                    {
                        ground = top;
                    }
                }
            }
            return ground;
        }

        private static void PushOutOfObstacles(PredictedBody body, ArenaMap map, double radius)
        {
            double feet = body.Y;
            double top = feet + body.HitHeightNow;
            // Walls raised during the round first, as the server does.
            ObstacleBox[] barriers = map.Barriers;
            for (int i = 0; i < map.BarrierCount; i++)
            {
                ObstacleBox b = barriers[i];
                if (b.MaxY - feet > StepHeight && b.MinY < top)
                {
                    PushOut(body, b, radius);
                }
            }
            ObstacleBox[] obstacles = map.Obstacles;
            for (int i = 0; i < obstacles.Length; i++)
            {
                ObstacleBox o = obstacles[i];
                if (!o.BlocksMovement || o.TopAt(body.X, body.Z) - feet <= StepHeight || o.MinY >= top)
                {
                    continue;
                }
                PushOut(body, o, radius);
            }
        }

        private static void PushOut(PredictedBody body, ObstacleBox o, double radius)
        {
            double nearestX = Math.Min(Math.Max(body.X, o.MinX), o.MaxX);
            double nearestZ = Math.Min(Math.Max(body.Z, o.MinZ), o.MaxZ);
            double dx = body.X - nearestX;
            double dz = body.Z - nearestZ;
            double distanceSq = dx * dx + dz * dz;
            if (distanceSq >= radius * radius)
            {
                return;
            }
            if (distanceSq > 1e-12)
            {
                double distance = Math.Sqrt(distanceSq);
                double push = radius - distance;
                body.X += dx / distance * push;
                body.Z += dz / distance * push;
                return;
            }
            // The centre is inside the box: leave through the closest face.
            double toMinX = body.X - o.MinX;
            double toMaxX = o.MaxX - body.X;
            double toMinZ = body.Z - o.MinZ;
            double toMaxZ = o.MaxZ - body.Z;
            double smallest = toMinX;
            int face = 0;
            if (toMaxX < smallest)
            {
                smallest = toMaxX;
                face = 1;
            }
            if (toMinZ < smallest)
            {
                smallest = toMinZ;
                face = 2;
            }
            if (toMaxZ < smallest)
            {
                face = 3;
            }
            switch (face)
            {
                case 0:
                    body.X = o.MinX - radius;
                    break;
                case 1:
                    body.X = o.MaxX + radius;
                    break;
                case 2:
                    body.Z = o.MinZ - radius;
                    break;
                default:
                    body.Z = o.MaxZ + radius;
                    break;
            }
        }
    }
}
