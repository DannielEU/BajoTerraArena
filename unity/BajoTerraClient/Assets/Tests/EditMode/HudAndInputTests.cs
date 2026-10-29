using Bajoterra.Client.Camera;
using Bajoterra.Client.Content;
using Bajoterra.Client.Hud;
using Bajoterra.Client.Input;
using Bajoterra.Client.Session;
using Bajoterra.Protocol;
using NUnit.Framework;

namespace Bajoterra.Client.Tests
{
    public class HudAndInputTests
    {
        private static ContentCatalog Catalog()
        {
            ContentDetails details = TestContent.Details();
            return new ContentCatalog(TestContent.ManifestFor(details), details);
        }

        private static MatchSession Session(int you = 0)
        {
            return new MatchSession(Catalog(), TestContent.MatchStart(you), 60);
        }

        [Test]
        public void TheKillFeedKeepsTheLatestEntriesAndExpiresOldOnes()
        {
            var feed = new KillFeed();
            for (int i = 0; i < 7; i++)
            {
                feed.Add("A" + i, 0, "B" + i, 1, i);
            }

            Assert.AreEqual(KillFeed.MaxEntries, feed.Count);
            Assert.AreEqual("A2", feed[0].Killer);

            feed.Update(10.0);
            Assert.AreEqual(2, feed.Count, "entries added at t=5 and t=6 live until t=11 and t=12");

            feed.Update(100.0);
            Assert.AreEqual(0, feed.Count);
        }

        [Test]
        public void AnUnattributedDefeatHasNoKiller()
        {
            var feed = new KillFeed();
            feed.Add(null, -1, "Bot 3", 1, 0.0);

            Assert.IsTrue(feed[0].Unattributed);
        }

        [Test]
        public void DefeatEventsFeedTheKillFeedWithNamesAndTeams()
        {
            MatchSession match = Session();
            var batch = new EventBatch { Tick = 600 };
            batch.Events.Add(TestContent.Event(EventKind.Defeated, 600, actor: 0, subject: 4));
            batch.Events.Add(TestContent.Event(EventKind.Defeated, 601, actor: -1, subject: 2));

            match.OnEvents(batch);
            match.OnMatchEnd(); // releases everything that was waiting

            Assert.AreEqual(2, match.KillFeed.Count);
            Assert.AreEqual("You", match.KillFeed[0].Killer);
            Assert.AreEqual("Bot 4", match.KillFeed[0].Victim);
            Assert.AreEqual(1, match.KillFeed[0].VictimTeam);
            Assert.IsTrue(match.KillFeed[1].Unattributed);
        }

        [Test]
        public void TheHudShowsVitalsSlotsAndTheScoreFromTheOwnTeamsPoint()
        {
            MatchSession match = Session(you: 4); // team 1
            CombatantState you = TestContent.Combatant(4, 0f, 0f);
            you.Hp = 64f;
            you.Shield = 12.5f;
            you.Overshield = 7f;
            you.SelectedSlot = 2;
            you.Slots[1].ShotsUntilReady = 3;
            Snapshot snapshot = TestContent.Snapshot(300, new[] { you });
            snapshot.Match.Round = 3;
            snapshot.Match.WinsTeam0 = 2;
            snapshot.Match.WinsTeam1 = 1;
            snapshot.Match.TicksLeft = 3601;
            match.OnSnapshot(snapshot, 1.0);
            var hud = new HudModel();

            hud.Refresh(match, 42);

            Assert.IsTrue(hud.HasState);
            Assert.AreEqual(64f, hud.Hp);
            Assert.AreEqual(100f, hud.MaxHp);
            Assert.AreEqual(12.5f, hud.Shield);
            Assert.AreEqual(7f, hud.Overshield);
            Assert.AreEqual(42, hud.PingMs);
            Assert.AreEqual(3, hud.Round);
            Assert.AreEqual(1, hud.WinsOwnTeam);
            Assert.AreEqual(2, hud.WinsOtherTeam);
            Assert.AreEqual(61, hud.SecondsLeft);
            Assert.AreEqual("Eli Shane", hud.CharacterName);

            Assert.IsTrue(hud.Slots[2].Selected);
            Assert.IsFalse(hud.Slots[0].Selected);
            Assert.IsTrue(hud.Slots[0].Ready);
            Assert.IsFalse(hud.Slots[1].Ready);
            Assert.AreEqual(3, hud.Slots[1].ShotsUntilReady);
            Assert.AreEqual("Doc", hud.Slots[1].Name);
            Assert.AreEqual("LIGHT", hud.Slots[1].Element);
            Assert.AreEqual(Targeting.Allies, hud.Slots[1].Targeting);
            Assert.AreEqual("Infurnus", hud.Slots[0].Name);
            Assert.AreEqual("FIRE", hud.Slots[0].Element);
        }

        [Test]
        public void TheHudShowsCourageAndWarnsAboutAnEnemyGrenadeStuckToYou()
        {
            MatchSession match = Session(you: 4); // team 1
            CombatantState you = TestContent.Combatant(4, 0f, 0f);
            you.Courage = 2;
            var enemyGrenade = new ProjectileState { Id = 9, Owner = 0, Stuck = true, StuckTo = 4, FuseTicks = 30 };
            var laterGrenade = new ProjectileState { Id = 10, Owner = 1, Stuck = true, StuckTo = 4, FuseTicks = 80 };
            var allyGrenade = new ProjectileState { Id = 11, Owner = 3, Stuck = true, StuckTo = 4, FuseTicks = 6 };
            match.OnSnapshot(TestContent.Snapshot(300, new[] { you },
                projectiles: new[] { laterGrenade, enemyGrenade, allyGrenade }), 1.0);
            var hud = new HudModel();

            hud.Refresh(match, 0);

            Assert.AreEqual(2, hud.Courage);
            Assert.IsTrue(hud.GrenadeOnYou);
            Assert.AreEqual(0.5f, hud.GrenadeSeconds, 1e-4f, "the soonest enemy grenade; a teammate's does not hurt you");
        }

        [Test]
        public void NoGrenadeWarningForAGrenadeStuckSomewhereElse()
        {
            MatchSession match = Session(you: 4);
            var onTheWall = new ProjectileState { Id = 9, Owner = 0, Stuck = true, StuckTo = -1, FuseTicks = 30 };
            var onSomeoneElse = new ProjectileState { Id = 10, Owner = 0, Stuck = true, StuckTo = 5, FuseTicks = 30 };
            match.OnSnapshot(TestContent.Snapshot(300, new[] { TestContent.Combatant(4, 0f, 0f) },
                projectiles: new[] { onTheWall, onSomeoneElse }), 1.0);
            var hud = new HudModel();

            hud.Refresh(match, 0);

            Assert.IsFalse(hud.GrenadeOnYou);
            Assert.AreEqual(0, hud.Courage);
        }

        [Test]
        public void TheHudHasNoStateBeforeTheFirstSnapshot()
        {
            var hud = new HudModel();

            hud.Refresh(Session(), 0);

            Assert.IsFalse(hud.HasState);
        }

        [Test]
        public void ClockAndFractionHelpersFormatSafely()
        {
            Assert.AreEqual("1:05", HudModel.Clock(65));
            Assert.AreEqual("0:00", HudModel.Clock(-3));
            Assert.AreEqual(0.5f, HudModel.Fraction(25f, 50f));
            Assert.AreEqual(0f, HudModel.Fraction(5f, 0f));
            Assert.AreEqual(1f, HudModel.Fraction(80f, 50f));
            Assert.AreEqual(0f, HudModel.Fraction(-4f, 50f));
        }

        [Test]
        public void MoveBitsFollowTheCharactersMoveSetOrder()
        {
            ContentCatalog catalog = Catalog();
            var mask = new MoveMask(catalog, catalog.FindCharacter("eli"));

            Assert.AreEqual(3, mask.Count);
            Assert.AreEqual(1u, mask.BitFor("jump"));
            Assert.AreEqual(2u, mask.BitFor("crouch"));
            Assert.AreEqual(4u, mask.BitFor("roll"));
            Assert.AreEqual(0u, mask.BitFor("dash"), "Eli cannot dash");
            Assert.AreEqual("roll", mask.MoveId(2));
        }

        [Test]
        public void TheInputFrameCarriesEveryFieldOfThePlayerInput()
        {
            var input = new PlayerInput
            {
                MoveX = -1f, MoveZ = 0.5f, AimYaw = 2f, AimPitch = -0.3f, HasCamera = true,
                CameraPosition = new Vec3(1f, 2f, 3f), Fire = true, MoveMask = 5, SelectSlot = 7, CycleSlot = -1
            };

            InputFrame frame = input.ToFrame(99);

            Assert.AreEqual(99u, frame.Sequence);
            Assert.AreEqual(-1f, frame.MoveX);
            Assert.AreEqual(0.5f, frame.MoveZ);
            Assert.AreEqual(2f, frame.AimYaw);
            Assert.AreEqual(-0.3f, frame.AimPitch);
            Assert.IsTrue(frame.HasCamera);
            Assert.AreEqual(3f, frame.CamZ);
            Assert.IsTrue(frame.Fire);
            Assert.AreEqual(5u, frame.MoveMask);
            Assert.AreEqual(7, frame.SelectSlot);
            Assert.AreEqual(-1, frame.CycleSlot);
        }

        [Test]
        public void OneShotPressesSurviveUntilTheNextTickAndAreSentOnce()
        {
            var shots = new OneShotInputs();
            var input = PlayerInput.Idle;

            shots.Select(4);
            shots.Select(6);
            shots.Cycle(1);
            shots.Cycle(1);
            shots.Cycle(-1);
            shots.Cycle(-1);
            shots.Cycle(-1);
            shots.Drain(ref input);

            Assert.AreEqual(6, input.SelectSlot, "the last request wins");
            Assert.AreEqual(-1, input.CycleSlot, "net notches decide the direction");

            input = PlayerInput.Idle;
            shots.Drain(ref input);
            Assert.AreEqual(-1, input.SelectSlot);
            Assert.AreEqual(0, input.CycleSlot);
        }

        [Test]
        public void OutOfRangeSlotRequestsAreIgnored()
        {
            var shots = new OneShotInputs();
            var input = PlayerInput.Idle;

            shots.Select(10);
            shots.Select(-3);
            shots.Drain(ref input);

            Assert.AreEqual(-1, input.SelectSlot);
        }

        [Test]
        public void TheIdleInputRequestsNoSlotChange()
        {
            Assert.AreEqual(-1, PlayerInput.Idle.SelectSlot);
            Assert.AreEqual(0, PlayerInput.Idle.CycleSlot);
        }

        [Test]
        public void TheCameraSitsBehindAndAboveTheCharacterLookingAlongTheAim()
        {
            var camera = new ThirdPersonCamera();
            camera.SetAim(0f, 0f);

            CameraPose pose = camera.Solve(new Vec3(0f, 0f, 0f), 1.8f, null);

            Assert.AreEqual(-camera.Distance, pose.Position.Z, 1e-4f, "behind a character facing +Z");
            Assert.AreEqual(camera.ShoulderOffset, pose.Position.X, 1e-4f, "a little to the right");
            Assert.AreEqual(1.8f * camera.PivotHeightFraction, pose.Position.Y, 1e-4f);
            Assert.AreEqual(0f, pose.Yaw);
            Assert.AreEqual(1f, pose.Forward.Z, 1e-5f);
        }

        [Test]
        public void LookingUpTurnsTheCameraDownBehindTheCharacter()
        {
            var camera = new ThirdPersonCamera();
            camera.SetAim(0f, 0.8f);

            CameraPose pose = camera.Solve(Vec3.Zero, 1.8f, null);

            Assert.Less(pose.Position.Y, 1.8f * camera.PivotHeightFraction, "orbits below the pivot when looking up");
        }

        [Test]
        public void PitchIsClampedAndYawWraps()
        {
            var camera = new ThirdPersonCamera();

            camera.AddLook(7f, 5f);

            Assert.LessOrEqual(camera.Pitch, ThirdPersonCamera.MaxPitch);
            Assert.That(camera.Yaw, Is.InRange(-System.Math.PI, System.Math.PI));
            camera.AddLook(0f, -10f);
            Assert.GreaterOrEqual(camera.Pitch, ThirdPersonCamera.MinPitch);
        }

        [Test]
        public void AWallBehindTheCharacterPullsTheCameraIn()
        {
            var map = new ArenaMap(TestContent.Map());
            var camera = new ThirdPersonCamera();
            camera.SetAim((float)System.Math.PI / 2f, 0f); // facing +X: the camera sits on the -X side
            Vec3 feet = new Vec3(7f, 0f, 0f);              // right behind the wall at x = 5..6 (it is on the -X side)

            CameraPose pose = camera.Solve(feet, 1.8f, map);

            Assert.Greater(pose.Position.X, 6f, "the camera stays on the character's side of the wall");
            Assert.Less(Vec3.Distance(pose.Position, new Vec3(7f, 1.58f, 0f)), camera.Distance);
        }

        [Test]
        public void ARayHitsABoxAtTheRightDistance()
        {
            var box = new ObstacleBox(new ObstacleInfo { MinX = 5, MinY = 0, MinZ = -3, MaxX = 6, MaxY = 4, MaxZ = 3 });

            Assert.AreEqual(5f, box.RayDistance(new Vec3(0f, 1f, 0f), new Vec3(1f, 0f, 0f), 100f, 0f), 1e-5f);
            Assert.Less(box.RayDistance(new Vec3(0f, 1f, 0f), new Vec3(-1f, 0f, 0f), 100f, 0f), 0f, "behind the ray");
            Assert.Less(box.RayDistance(new Vec3(0f, 6f, 0f), new Vec3(1f, 0f, 0f), 100f, 0f), 0f, "over the box");
            Assert.Less(box.RayDistance(new Vec3(0f, 1f, 0f), new Vec3(1f, 0f, 0f), 3f, 0f), 0f, "too short");
            Assert.AreEqual(4.8f, box.RayDistance(new Vec3(0f, 1f, 0f), new Vec3(1f, 0f, 0f), 100f, 0.2f), 1e-5f);
        }
    }
}
