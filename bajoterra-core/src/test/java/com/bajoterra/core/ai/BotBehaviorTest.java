package com.bajoterra.core.ai;

import static com.bajoterra.core.Fixtures.CONTENT;
import static com.bajoterra.core.Fixtures.human;
import static com.bajoterra.core.Fixtures.loadout;
import static com.bajoterra.core.Fixtures.place;
import static com.bajoterra.core.Fixtures.startRound;
import static com.bajoterra.core.Fixtures.ticks;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.MatchConfig;
import com.bajoterra.core.sim.SlotConfig;
import com.bajoterra.core.slug.Element;
import org.junit.jupiter.api.Test;

/** Individual bot behaviours in small hand-made scenarios. */
class BotBehaviorTest {

    private static final double FACING_PLUS_X = Math.PI / 2;

    private static SlotConfig bot(int team, String character, double skill, String... slugs) {
        return new SlotConfig(team, character, loadout(slugs), true, skill);
    }

    private static GameSimulation sim(String map, long seed, SlotConfig... slots) {
        MatchConfig.Builder builder = MatchConfig.builder(map).seed(seed).fillWithBots(false).maxDuplicateSlugs(10);
        for (SlotConfig slot : slots) {
            builder.slot(slot);
        }
        return new GameSimulation(CONTENT, builder.build());
    }

    private static int countMoves(GameSimulation sim, int combatantId, String moveId) {
        int index = CONTENT.moves().require(moveId).index();
        int total = 0;
        for (long seq = sim.events().oldestAvailable(); seq < sim.events().written(); seq++) {
            GameEvent event = sim.events().at(seq);
            if (event.type == EventType.MOVE_STARTED && event.actor == combatantId && event.a == index) {
                total++;
            }
        }
        return total;
    }

    @Test
    void aBotShootsAnEnemyItCanSee() {
        GameSimulation sim = sim("test_open", 1,
                bot(0, "eli", 0.9, "flaringo"), human(1, "kord", "doc"));
        startRound(sim);
        Combatant bot = sim.combatants()[0];
        Combatant enemy = sim.combatants()[1];
        place(bot, -10, 0, FACING_PLUS_X);
        place(enemy, 10, 0, -FACING_PLUS_X);

        ticks(sim, 180);

        assertThat(bot.shotsFired).isGreaterThan(2);
        assertThat(bot.damageDealt).isGreaterThan(0);
    }

    @Test
    void aBotDoesNotShootAtAnEnemyHiddenBehindAWall() {
        GameSimulation sim = sim("test_arena", 1,
                bot(0, "eli", 0.9, "flaringo"), human(1, "kord", "doc"));
        startRound(sim);
        Combatant bot = sim.combatants()[0];
        Combatant enemy = sim.combatants()[1];
        place(bot, -3, 0, FACING_PLUS_X);
        place(enemy, 3, 0, -FACING_PLUS_X);

        ticks(sim, 45);

        assertThat(bot.shotsFired).as("no line of sight, no shooting").isZero();
    }

    @Test
    void aBotWalksAroundObstaclesToReachAnEnemy() {
        GameSimulation sim = sim("test_arena", 3,
                bot(0, "eli", 0.9, "flaringo"), human(1, "kord", "doc"));
        startRound(sim);
        Combatant bot = sim.combatants()[0];
        Combatant enemy = sim.combatants()[1];
        place(bot, -10, 0, FACING_PLUS_X);
        place(enemy, 10, 0, -FACING_PLUS_X);

        ticks(sim, 60 * 25);

        assertThat(bot.shotsFired).as("found a way around the wall and attacked").isGreaterThan(0);
    }

    @Test
    void aBotHealsAWoundedAllyInsteadOfAttacking() {
        GameSimulation sim = sim("test_open", 2,
                bot(0, "eli", 0.9, "doc", "armashelt"),
                human(0, "kord", "flaringo"),
                human(1, "pronto", "flaringo"));
        startRound(sim);
        Combatant medic = sim.combatants()[0];
        Combatant ally = sim.combatants()[1];
        place(medic, -10, 0, FACING_PLUS_X);
        place(ally, 0, 3, FACING_PLUS_X);
        place(sim.combatants()[2], 15, 9, 0);
        sim.arena().damage(null, ally, 150, Element.NEUTRAL, 0); // kord: 140 hp + 70 shield -> 60 hp left
        double before = ally.vitals.hp() + ally.vitals.shield() + ally.vitals.overshield();

        ticks(sim, 240);

        double after = ally.vitals.hp() + ally.vitals.shield() + ally.vitals.overshield();
        assertThat(after).isGreaterThan(before);
        assertThat(medic.healingDone).isGreaterThan(0);
    }

    @Test
    void aSkilledBotRollsOutOfTheWayOfIncomingSlugs() {
        GameSimulation sim = sim("test_open", 4,
                bot(0, "eli", 1.0, "flaringo"), human(1, "kord", "flaringo"));
        startRound(sim);
        Combatant bot = sim.combatants()[0];
        Combatant shooter = sim.combatants()[1];
        place(bot, -10, 0, FACING_PLUS_X);
        place(shooter, 10, 0, -FACING_PLUS_X);
        shooter.input.aimYaw = -FACING_PLUS_X;
        shooter.input.fire = true;

        ticks(sim, 60 * 8);

        assertThat(countMoves(sim, 0, "roll")).as("the bot dodged at least once").isGreaterThan(0);
    }

    @Test
    void aNoviceBotNeverDodges() {
        GameSimulation sim = sim("test_open", 4,
                bot(0, "eli", 0.0, "flaringo"), human(1, "kord", "flaringo"));
        startRound(sim);
        place(sim.combatants()[0], -10, 0, FACING_PLUS_X);
        place(sim.combatants()[1], 10, 0, -FACING_PLUS_X);
        sim.combatants()[1].input.aimYaw = -FACING_PLUS_X;
        sim.combatants()[1].input.fire = true;

        ticks(sim, 60 * 8);

        assertThat(countMoves(sim, 0, "roll")).isZero();
    }

    @Test
    void aHurtBotBreaksLineOfSightByTakingCover() {
        GameSimulation sim = sim("test_arena", 6,
                bot(0, "eli", 0.9, "flaringo"), human(1, "kord", "flaringo"));
        startRound(sim);
        Combatant bot = sim.combatants()[0];
        Combatant enemy = sim.combatants()[1];
        place(bot, -2, 0, FACING_PLUS_X);
        place(enemy, 12, 0, -FACING_PLUS_X);
        sim.arena().damage(null, bot, 105, Element.NEUTRAL, 0); // 100 hp + 50 shield -> 45 left (30 %)
        enemy.input.aimYaw = -FACING_PLUS_X;

        boolean hidden = false;
        for (int tick = 0; tick < 60 * 8 && !hidden && bot.alive; tick++) {
            sim.tick();
            hidden = !sim.world().hasLineOfSight(enemy.pos.x, enemy.pos.y + 1.6, enemy.pos.z,
                    bot.pos.x, bot.pos.y + bot.hitHeight() * 0.55, bot.pos.z);
        }

        assertThat(hidden).as("the wounded bot ended up out of the enemy's sight").isTrue();
    }

    @Test
    void botsKeepMovingAndNeverEndUpOutsideTheMap() {
        GameSimulation sim = new GameSimulation(CONTENT, MatchConfig.builder("caverna").seed(8).build());
        sim.start();
        double[] startX = new double[6];
        for (int i = 0; i < 6; i++) {
            startX[i] = sim.combatants()[i].pos.x;
        }

        for (int tick = 0; tick < 60 * 40; tick++) {
            sim.tick();
            for (Combatant c : sim.combatants()) {
                assertThat(c.pos.x).isBetween(sim.map().minX(), sim.map().maxX());
                assertThat(c.pos.z).isBetween(sim.map().minZ(), sim.map().maxZ());
                assertThat(Double.isNaN(c.pos.x + c.pos.y + c.pos.z)).isFalse();
            }
        }
        int moved = 0;
        for (int i = 0; i < 6; i++) {
            if (Math.abs(sim.combatants()[i].pos.x - startX[i]) > 3) {
                moved++;
            }
        }
        assertThat(moved).isGreaterThanOrEqualTo(4);
    }

    @Test
    void skillValuesTranslateToMonotonicProfiles() {
        BotProfile novice = BotProfile.fromSkill(0);
        BotProfile expert = BotProfile.fromSkill(1);

        assertThat(expert.aimNoise()).isLessThan(novice.aimNoise());
        assertThat(expert.turnRate()).isGreaterThan(novice.turnRate());
        assertThat(expert.dodgeChance()).isGreaterThan(novice.dodgeChance());
        assertThat(expert.leadsTargets()).isTrue();
        assertThat(novice.leadsTargets()).isFalse();
        assertThat(BotProfile.fromSkill(5).dodgeChance()).isEqualTo(expert.dodgeChance());
        assertThat(BotProfile.fromSkill(-5).aimNoise()).isEqualTo(novice.aimNoise());
    }
}
