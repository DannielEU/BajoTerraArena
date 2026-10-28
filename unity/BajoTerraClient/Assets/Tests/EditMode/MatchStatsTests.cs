using Bajoterra.Client.Hud;
using Bajoterra.Client.Session;
using Bajoterra.Protocol;
using NUnit.Framework;

namespace Bajoterra.Client.Tests
{
    public class MatchStatsTests
    {
        // Combatants 0-2 are team 0, 3-5 team 1.
        private static MatchStats NewStats()
        {
            return new MatchStats(6, id => id < 3 ? 0 : 1);
        }

        private static GameEvent Damage(int actor, int subject, float value, int element = 1)
        {
            return TestContent.Event(EventKind.Damage, 10, actor: actor, subject: subject, a: element, value: value);
        }

        [Test]
        public void DamageToAnEnemyCountsForTheAttackerAndTheVictim()
        {
            MatchStats stats = NewStats();

            stats.Record(Damage(0, 3, 20f));
            stats.Record(Damage(0, 4, 5f));
            stats.Record(Damage(4, 0, 12f));

            Assert.AreEqual(25f, stats.Line(0).DamageDealt, 1e-4f);
            Assert.AreEqual(12f, stats.Line(0).DamageTaken, 1e-4f);
            Assert.AreEqual(20f, stats.Line(3).DamageTaken, 1e-4f);
            Assert.AreEqual(12f, stats.Line(4).DamageDealt, 1e-4f);
        }

        [Test]
        public void DamageToAnAllyOrToYourselfIsNotCountedAsDealt()
        {
            MatchStats stats = NewStats();

            stats.Record(Damage(0, 1, 30f));
            stats.Record(Damage(2, 2, 10f));

            Assert.AreEqual(0f, stats.Line(0).DamageDealt);
            Assert.AreEqual(0f, stats.Line(2).DamageDealt);
            Assert.AreEqual(30f, stats.Line(1).DamageTaken, 1e-4f);
        }

        [Test]
        public void TheLastAttackerAndTheElementOfTheLastHitAreRemembered()
        {
            MatchStats stats = NewStats();

            stats.Record(Damage(3, 0, 8f, element: 1));
            stats.Record(Damage(4, 0, 14f, element: 2));

            Assert.AreEqual(4, stats.LastAttacker(0));
            Assert.AreEqual(2, stats.LastElement(0));
            Assert.AreEqual(14f, stats.LastDamage(0), 1e-4f);
            Assert.AreEqual(-1, stats.LastAttacker(5), "nobody hurt combatant 5");
        }

        [Test]
        public void ANewRoundForgetsWhoHurtWhomButKeepsTheTotals()
        {
            MatchStats stats = NewStats();
            stats.Record(Damage(3, 0, 8f));

            stats.Record(TestContent.Event(EventKind.RoundStarted, 100, a: 2));

            Assert.AreEqual(-1, stats.LastAttacker(0));
            Assert.AreEqual(8f, stats.Line(0).DamageTaken, 1e-4f);
        }

        [Test]
        public void DefeatsCountKillsAndDeathsButNotSelfOrFriendlyOnes()
        {
            MatchStats stats = NewStats();

            stats.Record(TestContent.Event(EventKind.Defeated, 20, actor: 0, subject: 3));
            stats.Record(TestContent.Event(EventKind.Defeated, 21, actor: 1, subject: 2)); // friendly fire
            stats.Record(TestContent.Event(EventKind.Defeated, 22, actor: 4, subject: 4)); // fell by itself
            stats.Record(TestContent.Event(EventKind.Defeated, 23, actor: -1, subject: 5)); // no killer

            Assert.AreEqual(1, stats.Line(0).Kills);
            Assert.AreEqual(1, stats.Line(3).Deaths);
            Assert.AreEqual(0, stats.Line(1).Kills);
            Assert.AreEqual(1, stats.Line(2).Deaths);
            Assert.AreEqual(0, stats.Line(4).Kills);
            Assert.AreEqual(1, stats.Line(4).Deaths);
            Assert.AreEqual(1, stats.Line(5).Deaths);
        }

        [Test]
        public void HealingIsCountedForTheHealerOnly()
        {
            MatchStats stats = NewStats();

            stats.Record(TestContent.Event(EventKind.Heal, 30, actor: 1, subject: 0, value: 25f));
            stats.Record(TestContent.Event(EventKind.ShieldRestored, 31, actor: 1, subject: 0, value: 10f));
            stats.Record(TestContent.Event(EventKind.Heal, 32, actor: 1, subject: 1, value: 40f)); // self heal

            Assert.AreEqual(35f, stats.Line(1).Healing, 1e-4f);
            Assert.AreEqual(0f, stats.Line(0).Healing);
        }

        [Test]
        public void IdsOutsideTheMatchAreIgnored()
        {
            MatchStats stats = NewStats();

            stats.Record(Damage(99, 3, 5f));
            stats.Record(Damage(0, -4, 5f));
            stats.Record(TestContent.Event(EventKind.Defeated, 5, actor: 0, subject: 77));

            Assert.AreEqual(0f, stats.Line(0).DamageDealt);
            Assert.AreEqual(0, stats.Line(77).Deaths);
        }

        [Test]
        public void TheBlasterWaitsAfterTheOwnShotAndTheHudModelShowsTheProgress()
        {
            var match = new MatchSession(TestContent.Catalog(), TestContent.MatchStart(0), 60);
            match.Update(10.0);
            var batch = new EventBatch { Tick = 100 };
            batch.Events.Add(TestContent.Event(EventKind.Shot, 100, actor: 0, a: 0));
            match.OnEvents(batch);

            Assert.AreEqual(42f / 60f, match.OwnShotCooldown, 1e-4f, "0.7 s for a 42-tick blaster and a neutral slug");
            Assert.AreEqual(10.0, match.OwnShotAt, 1e-9);

            var model = new HudModel();
            CombatantState you = TestContent.Combatant(0, 0f, 0f);
            match.OnSnapshot(TestContent.Snapshot(100, new[] { you }), 10.0);
            match.Update(10.35);
            model.Refresh(match, 30);
            Assert.AreEqual(0.5f, model.BlasterProgress, 0.02f);
            Assert.IsFalse(model.BlasterReady);

            match.Update(10.8);
            model.Refresh(match, 30);
            Assert.IsTrue(model.BlasterReady);
            Assert.AreEqual(1f, model.BlasterProgress);
        }
    }
}
