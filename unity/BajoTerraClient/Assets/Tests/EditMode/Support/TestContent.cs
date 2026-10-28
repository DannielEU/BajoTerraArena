using System;
using System.Collections.Generic;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Tests
{
    /// <summary>Builds small, hand-made server messages for tests that do not need the real game content.</summary>
    public static class TestContent
    {
        public const int TicksPerSecond = 60;

        /// <summary>Two slugs, one character with jump/crouch/roll, one pickup.</summary>
        public static ContentDetails Details()
        {
            var details = new ContentDetails();
            details.Slugs.Add(new SlugInfo
            {
                Id = "infurnus", Name = "Infurnus", Element = 1, Targeting = Targeting.Enemies, Role = "DAMAGE",
                ProjectileRadius = 0.25f, ProjectileSpeed = 45f
            });
            details.Slugs.Add(new SlugInfo
            {
                Id = "doc", Name = "Doc", Element = 8, Targeting = Targeting.Allies, Role = "SUPPORT",
                ProjectileRadius = 0.3f, ProjectileSpeed = 30f
            });
            MoveInfo jump = new MoveInfo
            {
                Id = "jump", DurationTicks = 1, CooldownTicks = 21, Interruptible = true, AllowedFromMask = 1 | 4,
                SpeedMultiplier = 1f, HitboxHeightMultiplier = 1f, BehaviorType = "impulse"
            };
            jump.BehaviorParams["vertical"] = 8.5f;
            MoveInfo crouch = new MoveInfo
            {
                Id = "crouch", Holdable = true, Stance = true, AllowedFromMask = 1, SpeedMultiplier = 0.5f,
                HitboxHeightMultiplier = 0.6f
            };
            MoveInfo roll = new MoveInfo
            {
                Id = "roll", DurationTicks = 30, CooldownTicks = 72, AllowedFromMask = 1 | 4, BlocksFiring = true,
                SpeedMultiplier = 1f, HitboxHeightMultiplier = 0.6f, InvulnerableFromTick = 3,
                InvulnerableToTick = 21, BehaviorType = "drive"
            };
            roll.BehaviorParams["speed"] = 8f;
            details.Moves.Add(jump);
            details.Moves.Add(crouch);
            details.Moves.Add(roll);
            var eli = new CharacterInfo
            {
                Id = "eli", Name = "Eli Shane", MaxHp = 100f, MaxShield = 50f, Speed = 6f, HitRadius = 0.4f,
                HitHeight = 1.8f, FireIntervalTicks = 42, ReloadShots = 3
            };
            eli.MoveIndices.AddRange(new uint[] { 0, 1, 2 });
            details.Characters.Add(eli);
            details.Pickups.Add(new PickupInfo { Id = "health_pack", Name = "Health pack", Radius = 1f });
            return details;
        }

        public static Content.ContentCatalog Catalog()
        {
            ContentDetails details = Details();
            return new Content.ContentCatalog(ManifestFor(details), details);
        }

        /// <summary>The ids of <paramref name="details"/> in index order, as the server's manifest lists them.</summary>
        public static ContentManifest ManifestFor(ContentDetails details)
        {
            var manifest = new ContentManifest();
            foreach (SlugInfo slug in details.Slugs)
            {
                manifest.SlugIds.Add(slug.Id);
            }
            foreach (CharacterInfo character in details.Characters)
            {
                manifest.CharacterIds.Add(character.Id);
            }
            foreach (MoveInfo move in details.Moves)
            {
                manifest.MoveIds.Add(move.Id);
            }
            foreach (PickupInfo pickup in details.Pickups)
            {
                manifest.PickupIds.Add(pickup.Id);
            }
            manifest.MapIds.Add("test");
            manifest.ElementIds.AddRange(new[]
            {
                "NEUTRAL", "FIRE", "WATER", "EARTH", "AIR", "ENERGY", "ICE", "METAL", "LIGHT"
            });
            return manifest;
        }

        public static Welcome Welcome(ContentDetails details = null, uint version = 1)
        {
            details = details ?? Details();
            return new Welcome
            {
                ProtocolVersion = version, ConnectionId = 7, TickRate = TicksPerSecond, SnapshotRate = 20,
                Content = ManifestFor(details), Details = details
            };
        }

        /// <summary>A flat 40 x 40 m arena with one wall and one low box.</summary>
        public static MapData Map()
        {
            var map = new MapData { Name = "Test", MinX = -20, MaxX = 20, MinZ = -20, MaxZ = 20, GroundY = 0 };
            map.Obstacles.Add(new ObstacleInfo
            {
                Id = "wall", MinX = 5, MinY = 0, MinZ = -3, MaxX = 6, MaxY = 4, MaxZ = 3,
                BlocksProjectiles = true, BlocksMovement = true, BlocksLineOfSight = true
            });
            map.Obstacles.Add(new ObstacleInfo
            {
                Id = "low", MinX = -3, MinY = 0, MinZ = 8, MaxX = 3, MaxY = 1, MaxZ = 9,
                BlocksProjectiles = true, BlocksMovement = true, BlocksLineOfSight = false
            });
            map.Pickups.Add(new PickupPlacement { PickupIndex = 0, X = 10, Y = 0, Z = 10 });
            map.Spawns.Add(new SpawnInfo { Team = 0, X = -10, Y = 0, Z = 0, Yaw = 1.5707964f });
            map.Spawns.Add(new SpawnInfo { Team = 1, X = 10, Y = 0, Z = 0, Yaw = -1.5707964f });
            return map;
        }

        /// <summary>Six combatants, ids 0-2 on team 0 and 3-5 on team 1; the receiver is <paramref name="you"/>.</summary>
        public static MatchStart MatchStart(int you = 0, MapData map = null)
        {
            var start = new MatchStart { MapId = "test", YourCombatantId = (uint)you, Map = map ?? Map() };
            for (int i = 0; i < 6; i++)
            {
                start.Slots.Add(new Slot
                {
                    CombatantId = (uint)i, Team = i < 3 ? 0 : 1, CharacterId = "eli", Name = i == you ? "You" : "Bot " + i,
                    Bot = i != you, Ready = true, You = i == you, MaxHp = 100f, MaxShield = 50f
                });
            }
            return start;
        }

        public static CombatantState Combatant(int id, float x, float z, float yaw = 0f, bool alive = true)
        {
            var state = new CombatantState
            {
                Id = (uint)id, X = x, Y = 0f, Z = z, Yaw = yaw, Hp = 100f, Shield = 50f, Alive = alive,
                Posture = Posture.Grounded, ActiveMove = -1, HitboxHeight = 1.8f
            };
            for (int slot = 0; slot < 10; slot++)
            {
                state.Slots.Add(new SlotState { SlugIndex = (uint)(slot % 2), ShotsUntilReady = 0 });
            }
            return state;
        }

        public static Snapshot Snapshot(uint tick, IEnumerable<CombatantState> combatants,
            MatchPhase phase = MatchPhase.RoundActive, uint ack = 0, LocalState local = null,
            IEnumerable<ProjectileState> projectiles = null)
        {
            var snapshot = new Snapshot
            {
                Tick = tick, AckSequence = ack,
                Match = new MatchInfo { Phase = phase, TicksLeft = 3000, Round = 1, LastRoundWinner = -2, Winner = -1 }
            };
            snapshot.Combatants.AddRange(combatants);
            if (projectiles != null)
            {
                snapshot.Projectiles.AddRange(projectiles);
            }
            if (local != null)
            {
                snapshot.Local = local;
            }
            return snapshot;
        }

        public static LocalState GroundedLocal(int moves = 3)
        {
            var local = new LocalState { Grounded = true, StatusSpeedMultiplier = 1f, MoveDirZ = 1f };
            for (int i = 0; i < moves; i++)
            {
                local.MoveCooldownTicks.Add(0);
            }
            return local;
        }

        public static ServerMessage Wrap(Welcome welcome)
        {
            return new ServerMessage { Welcome = welcome };
        }

        public static ServerMessage Wrap(MatchStart start)
        {
            return new ServerMessage { MatchStart = start };
        }

        public static ServerMessage Wrap(Snapshot snapshot)
        {
            return new ServerMessage { Snapshot = snapshot };
        }

        public static ServerMessage Wrap(EventBatch events)
        {
            return new ServerMessage { Events = events };
        }

        public static ServerMessage Wrap(RoomState room)
        {
            return new ServerMessage { RoomState = room };
        }

        public static GameEvent Event(EventKind kind, uint tick, int actor = -1, int subject = -1, int a = 0, int b = 0,
            float value = 0f)
        {
            return new GameEvent { Kind = kind, Tick = tick, Actor = actor, Subject = subject, A = a, B = b, Value = value };
        }

        /// <summary>
        /// What the server would send for a combatant whose movement state is <paramref name="body"/> (the inverse
        /// of <c>PredictedBody.LoadFrom</c>).
        /// </summary>
        public static void DescribeBody(Prediction.PredictedBody body, CharacterInfo character, int id,
            out CombatantState state, out LocalState local)
        {
            state = Combatant(id, (float)body.X, (float)body.Z, (float)body.Yaw, body.Alive);
            state.Y = (float)body.Y;
            state.Posture = body.Posture;
            state.HitboxHeight = (float)body.HitHeightNow;
            state.ActiveMove = body.ActiveSlot >= 0 ? (int)character.MoveIndices[body.ActiveSlot] : -1;
            local = new LocalState
            {
                VelY = (float)body.VelY, KnockX = (float)body.KnockX, KnockZ = (float)body.KnockZ,
                Grounded = body.Grounded, StatusSpeedMultiplier = (float)body.StatusSpeed, Stunned = body.Stunned,
                MoveElapsedTicks = (uint)body.Elapsed, MoveDirX = (float)body.DirX, MoveDirZ = (float)body.DirZ
            };
            for (int slot = 0; slot < character.MoveIndices.Count; slot++)
            {
                local.MoveCooldownTicks.Add((uint)System.Math.Max(0, body.CooldownUntil[slot] - body.Tick));
            }
        }

        public static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
