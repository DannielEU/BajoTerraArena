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
import com.bajoterra.core.hazard.Hazard;
import com.bajoterra.core.math.Trig;
import com.bajoterra.core.projectile.Projectile;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.MatchConfig;
import com.bajoterra.core.sim.SlotConfig;
import com.bajoterra.core.slug.Element;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.world.Barrier;
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
    void aBotTakesAMomentToReactToAnEnemyThatJustCameIntoView() {
        GameSimulation sim = sim("test_open", 1,
                bot(0, "eli", 0.7, "flaringo"), human(1, "kord", "doc"));
        startRound(sim);
        Combatant bot = sim.combatants()[0];
        place(bot, -10, 0, FACING_PLUS_X);
        place(sim.combatants()[1], 10, 0, -FACING_PLUS_X);
        int start = sim.tickCount();

        int firstShot = -1;
        for (int i = 0; i < 120 && firstShot < 0; i++) {
            sim.tick();
            if (bot.shotsFired > 0) {
                firstShot = sim.tickCount();
            }
        }

        assertThat(firstShot).as("it does shoot").isPositive();
        assertThat(firstShot - start).isGreaterThanOrEqualTo(BotProfile.fromSkill(0.7).reactionTicks());
    }

    @Test
    void aBotRunsOutOfTheBlastOfAnEnemyGrenadeStuckNextToIt() {
        GameSimulation sim = sim("test_open", 5,
                bot(0, "eli", 1.0, "flaringo"), human(1, "kord", "doc"));
        startRound(sim);
        Combatant bot = sim.combatants()[0];
        Combatant thrower = sim.combatants()[1];
        place(bot, -5, 0, FACING_PLUS_X);
        place(thrower, 15, 8, -FACING_PLUS_X);
        SlugDefinition grenuke = CONTENT.slugs().require("grenuke");
        double reach = grenuke.onExpire().areaRadius();

        boolean escaped = false;
        for (int attempt = 0; attempt < 6 && !escaped; attempt++) {
            Projectile grenade = sim.projectiles().acquire();
            grenade.owner = thrower;
            grenade.slug = grenuke;
            grenade.pos.set(bot.pos.x + 0.8, 0.3, bot.pos.z);
            grenade.vel.setZero();
            grenade.stuck = true;
            grenade.ticksLeft = 80;
            ticks(sim, 70);
            escaped = Trig.hypot(bot.pos.x - grenade.pos.x, bot.pos.z - grenade.pos.z) > reach + bot.hitRadius();
            ticks(sim, 15); // let it go off before the next attempt
        }

        assertThat(escaped).as("the bot left the blast radius").isTrue();
    }

    @Test
    void aBlindedBotSeesNobody() {
        GameSimulation sim = sim("test_open", 3,
                bot(0, "eli", 1.0, "flaringo"), human(1, "kord", "doc"));
        startRound(sim);
        Combatant bot = sim.combatants()[0];
        place(bot, -6, 0, FACING_PLUS_X);
        place(sim.combatants()[1], 6, 0, -FACING_PLUS_X);
        BotBrain brain = (BotBrain) bot.brain;
        bot.stats.blindness = 1.0;
        brain.planNow(sim, bot);
        assertThat(brain.context().target).as("blinded").isNull();

        bot.stats.blindness = 0;
        brain.planNow(sim, bot);
        assertThat(brain.context().target).as("sees again").isSameAs(sim.combatants()[1]);
    }

    @Test
    void aPhosphoroFiredAtABotBlindsItAndItStaggersWithoutShootingUntilItSeesAgain() {
        GameSimulation sim = sim("test_open", 13,
                bot(0, "eli", 0.0, "flaringo"), human(1, "kord", "phosphoro"));
        startRound(sim);
        Combatant bot = sim.combatants()[0];
        Combatant human = sim.combatants()[1];
        place(bot, 4, 0, -FACING_PLUS_X);
        place(human, -8, 0, FACING_PLUS_X);
        human.input.aimPitch = 0.1;
        human.input.fire = true;

        int blindTicks = 0;
        int shotsWhenBlinded = -1;
        int shotsWhileBlind = 0;
        double before = 0;
        double staggered = 0;
        for (int i = 0; i < 360; i++) {
            before = bot.pos.x;
            double beforeZ = bot.pos.z;
            sim.tick();
            if (human.shotsFired > 0) {
                human.input.fire = false;
            }
            if (bot.statuses.has("blind") && bot.stats.blindness > BotBrain.BLIND_SIGHT) {
                blindTicks++;
                if (shotsWhenBlinded < 0) {
                    shotsWhenBlinded = bot.shotsFired;
                }
                shotsWhileBlind = bot.shotsFired - shotsWhenBlinded;
                staggered += Trig.hypot(bot.pos.x - before, bot.pos.z - beforeZ);
            }
        }

        assertThat(blindTicks).as("the bot was blinded").isGreaterThan(60);
        assertThat(shotsWhileBlind).as("a blinded bot does not shoot").isZero();
        assertThat(staggered).as("it staggers about").isGreaterThan(0.5);
    }

    @Test
    void aHurtBotInTheOpenRaisesAnIceWallBetweenItselfAndItsTarget() {
        GameSimulation sim = sim("test_open", 17,
                bot(0, "eli", 0.8, "frostcrawler", "flaringo"), human(1, "kord", "doc"));
        startRound(sim);
        Combatant bot = sim.combatants()[0];
        Combatant enemy = sim.combatants()[1];
        place(bot, -8, 0, FACING_PLUS_X);
        place(enemy, 8, 0, -FACING_PLUS_X);
        bot.vitals.applyDamage(bot.vitals.shield() + bot.vitals.hp() * 0.6, 0);

        Barrier wall = null;
        for (int i = 0; i < 240 && wall == null; i++) {
            sim.tick();
            for (Barrier b : sim.world().barriers()) {
                if (b.active) {
                    wall = b;
                }
            }
        }

        assertThat(wall).as("a wall went up").isNotNull();
        double middle = (wall.minX + wall.maxX) / 2;
        assertThat(middle).as("between them").isGreaterThan(bot.pos.x).isLessThan(enemy.pos.x);
    }

    @Test
    void aBotStandingInAnEnemysFireStepsOutOfIt() {
        GameSimulation sim = sim("test_open", 19,
                bot(0, "eli", 0.7, "doc"), human(1, "kord", "infurnus"));
        startRound(sim);
        Combatant bot = sim.combatants()[0];
        Combatant slinger = sim.combatants()[1];
        place(bot, 0, 0, FACING_PLUS_X);
        place(slinger, 15, 9, 0);
        Hazard patch = sim.hazards().lay(slinger, CONTENT.slugs().require("infurnus"),
                0, 0, 0);

        ticks(sim, 60);

        assertThat(sim.hazards().touching(bot)).as("out of the fire").isNull();
        assertThat(Trig.hypot(bot.pos.x - patch.x, bot.pos.z - patch.z)).isGreaterThan(0.8);
    }

    @Test
    void aSkilledBotTurnsItsEyesAwayFromAFlashAboutToGoOff() {
        GameSimulation sim = sim("test_open", 11,
                bot(0, "eli", 1.0, "doc"), human(1, "kord", "flaringo"));
        startRound(sim);
        Combatant bot = sim.combatants()[0];
        Combatant thrower = sim.combatants()[1];
        place(bot, -6, 0, FACING_PLUS_X);
        bot.pitch = 0;
        place(thrower, 15, 9, 0);
        SlugDefinition phosphoro = CONTENT.slugs().require("phosphoro");

        boolean lookedAway = false;
        for (int attempt = 0; attempt < 8 && !lookedAway; attempt++) {
            Projectile flash = sim.projectiles().acquire();
            flash.owner = thrower;
            flash.slug = phosphoro;
            flash.pos.set(bot.pos.x + 5 * Trig.sin(bot.yaw), 0.3, bot.pos.z + 5 * Trig.cos(bot.yaw));
            flash.vel.setZero();
            flash.stuck = true;
            flash.ticksLeft = 80;
            ticks(sim, 79);
            double toFlashX = flash.pos.x - bot.pos.x;
            double toFlashZ = flash.pos.z - bot.pos.z;
            double facing = (toFlashX * Trig.sin(bot.yaw) + toFlashZ * Trig.cos(bot.yaw))
                    / Trig.hypot(toFlashX, toFlashZ);
            lookedAway = facing < 0;
            ticks(sim, 2);   // let it go off before the next attempt
            bot.statuses.clear();
        }

        assertThat(lookedAway).as("the bot faced away from the flash").isTrue();
    }

    @Test
    void aBotOnlyDodgesSlugsItCanSeeComing() {
        assertThat(dodgesASlugFrom(FACING_PLUS_X)).as("slug coming at its face").isTrue();
        assertThat(dodgesASlugFrom(-FACING_PLUS_X)).as("slug coming at its back").isFalse();
    }

    /** Puts an enemy slug 6 m in front of (+x) a bot that faces {@code yaw}, flying straight at it. */
    private static boolean dodgesASlugFrom(double yaw) {
        GameSimulation sim = sim("test_open", 7,
                bot(0, "eli", 1.0, "doc"), human(1, "kord", "flaringo"));
        startRound(sim);
        Combatant bot = sim.combatants()[0];
        Combatant shooter = sim.combatants()[1];
        place(bot, 0, 0, yaw);
        bot.yaw = yaw;
        place(shooter, 15, 9, 0);
        BotBrain brain = (BotBrain) bot.brain;
        for (int attempt = 0; attempt < 12; attempt++) {
            Projectile slug = sim.projectiles().acquire();
            slug.owner = shooter;
            slug.slug = CONTENT.slugs().require("flaringo");
            slug.pos.set(6, bot.centerY(), 0);
            slug.vel.set(-30, 0, 0);
            slug.ticksLeft = 100;
            brain.planNow(sim, bot);
            sim.projectiles().release(slug);
            if (brain.context().dodgeTicksLeft > 0) {
                return true;
            }
        }
        return false;
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
        assertThat(expert.reactionTicks()).isLessThan(novice.reactionTicks());
    }
}
