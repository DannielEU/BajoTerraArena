package com.bajoterra.core.combat;

import static com.bajoterra.core.Fixtures.arena;
import static com.bajoterra.core.Fixtures.combatant;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventBuffer;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.slug.Element;
import org.junit.jupiter.api.Test;

/** The actions effects perform on the arena: exact amounts, events and bookkeeping. */
class CombatArenaTest {

    private final Combatant a = combatant(0, 0, "eli", "doc");
    private final Combatant b = combatant(1, 0, "kord", "doc");
    private final Combatant c = combatant(2, 1, "pronto", "doc");
    private final CombatArena arena = arena(a, b, c);

    private int events(EventType type) {
        EventBuffer buffer = arena.events();
        int count = 0;
        for (long seq = buffer.oldestAvailable(); seq < buffer.written(); seq++) {
            if (buffer.at(seq).type == type) {
                count++;
            }
        }
        return count;
    }

    private GameEvent last(EventType type) {
        EventBuffer buffer = arena.events();
        GameEvent found = null;
        for (long seq = buffer.oldestAvailable(); seq < buffer.written(); seq++) {
            if (buffer.at(seq).type == type) {
                found = buffer.at(seq);
            }
        }
        return found;
    }

    @Test
    void byIdReturnsCombatantsOrNullOutsideTheRange() {
        assertThat(arena.byId(-1)).isNull();
        assertThat(arena.byId(0)).isSameAs(a);
        assertThat(arena.byId(2)).isSameAs(c);
        assertThat(arena.byId(3)).isNull();
    }

    @Test
    void aliveCountOnlyCountsTheTeamsLivingMembers() {
        assertThat(arena.aliveCount(0)).isEqualTo(2);
        assertThat(arena.aliveCount(1)).isEqualTo(1);

        arena.damage(c, a, 1000, Element.NEUTRAL, 0);

        assertThat(arena.aliveCount(0)).isEqualTo(1);
        assertThat(arena.aliveCount(1)).isEqualTo(1);
    }

    @Test
    void tinyDamageStillCountsAndZeroDamageDoesNot() {
        assertThat(arena.damage(c, a, 0, Element.NEUTRAL, 0)).isZero();
        assertThat(events(EventType.DAMAGE)).isZero();

        assertThat(arena.damage(c, a, 0.25, Element.NEUTRAL, 0)).isEqualTo(0.25);
        assertThat(events(EventType.DAMAGE)).isEqualTo(1);
        assertThat(last(EventType.DAMAGE).value).isEqualTo(0.25);
        assertThat(last(EventType.DAMAGE).actor).isEqualTo(2);
        assertThat(last(EventType.DAMAGE).subject).isEqualTo(0);
    }

    @Test
    void environmentalDamageHasNoAttacker() {
        arena.damage(null, a, 10, Element.FIRE, 0);

        assertThat(last(EventType.DAMAGE).actor).isEqualTo(-1);
        assertThat(last(EventType.DAMAGE).a).isEqualTo(Element.FIRE.ordinal());
    }

    @Test
    void aKillIsRecordedOnceWithTheKillersIdAndTheVictimsPosition() {
        a.pos.set(3, 0, 4);

        arena.damage(c, a, 1000, Element.NEUTRAL, 0);
        arena.damage(c, a, 1000, Element.NEUTRAL, 0);

        assertThat(events(EventType.DEFEATED)).isEqualTo(1);
        GameEvent defeat = last(EventType.DEFEATED);
        assertThat(defeat.actor).isEqualTo(2);
        assertThat(defeat.subject).isEqualTo(0);
        assertThat(defeat.x).isEqualTo(3);
        assertThat(defeat.z).isEqualTo(4);
        assertThat(c.kills).isEqualTo(1);
        assertThat(a.deaths).isEqualTo(1);
    }

    @Test
    void killingATeammateOrYourselfEarnsNoKill() {
        arena.damage(b, a, 1000, Element.NEUTRAL, 1.0);   // same team (friendly fire off: nothing happens)
        assertThat(b.kills).isZero();

        arena.damage(c, c, 1000, Element.NEUTRAL, 0);
        assertThat(c.kills).isZero();
        assertThat(c.deaths).isEqualTo(1);
    }

    @Test
    void healingHonoursTheMultiplierAndIgnoresTheDead() {
        arena.damage(null, b, 50, Element.NEUTRAL, 1.0);
        b.stats.healReceivedMultiplier = 0.5;

        assertThat(arena.heal(a, b, 20)).isEqualTo(10);
        assertThat(last(EventType.HEAL).value).isEqualTo(10);
        assertThat(last(EventType.HEAL).actor).isEqualTo(0);
        assertThat(a.healingDone).isEqualTo(10);

        arena.damage(c, b, 10_000, Element.NEUTRAL, 0);   // a team-0 kill by team 1
        assertThat(arena.heal(a, b, 20)).isZero();
    }

    @Test
    void healingWithoutASourceIsNotAttributed() {
        arena.damage(null, b, 50, Element.NEUTRAL, 1.0);

        assertThat(arena.heal(null, b, 5)).isEqualTo(5);
        assertThat(last(EventType.HEAL).actor).isEqualTo(-1);
    }

    @Test
    void shieldRestorationUsesTheMultiplierAndReportsTheAmountRestored() {
        arena.damage(null, b, 60, Element.NEUTRAL, 0);   // kord: shield 70 -> 10
        b.stats.healReceivedMultiplier = 2.0;

        assertThat(arena.restoreShield(a, b, 10)).isEqualTo(20);
        assertThat(b.vitals.shield()).isEqualTo(30);
        assertThat(last(EventType.SHIELD_RESTORED).value).isEqualTo(20);
        assertThat(a.healingDone).isEqualTo(20);
        assertThat(arena.restoreShield(null, b, 0)).isZero();
    }

    @Test
    void shieldRestorationIsLimitedByTheMaximumAndSkipsTheDead() {
        arena.damage(null, b, 5, Element.NEUTRAL, 0);

        assertThat(arena.restoreShield(a, b, 100)).isEqualTo(5);

        arena.damage(c, b, 10_000, Element.NEUTRAL, 0);
        assertThat(arena.restoreShield(a, b, 5)).isZero();
    }

    @Test
    void overshieldGrantsAreRecordedAndSkipTheDead() {
        arena.grantOvershield(a, b, 30, 60);

        assertThat(b.vitals.overshield()).isEqualTo(30);
        assertThat(last(EventType.OVERSHIELD).value).isEqualTo(30);

        arena.damage(c, a, 10_000, Element.NEUTRAL, 0);
        arena.grantOvershield(b, a, 30, 60);
        assertThat(a.vitals.overshield()).isZero();
    }

    @Test
    void knockbackPushesAlongTheNormalisedDirection() {
        arena.knockback(c, 3, 4, 10, 0);

        assertThat(c.knock.x).isCloseTo(6, within(1e-9));
        assertThat(c.knock.z).isCloseTo(8, within(1e-9));
        assertThat(c.knock.y).isZero();
        assertThat(last(EventType.KNOCKBACK).value).isEqualTo(10);
        assertThat(last(EventType.KNOCKBACK).subject).isEqualTo(2);
    }

    @Test
    void knockbackStacksAndIgnoresAZeroDirection() {
        arena.knockback(c, 1, 0, 5, 0);
        arena.knockback(c, 1, 0, 5, 0);
        arena.knockback(c, 0, 0, 100, 0);

        assertThat(c.knock.x).isEqualTo(10);
        assertThat(c.knock.z).isZero();
    }

    @Test
    void liftOnlyRaisesTheVerticalSpeedAndLeavesTheGround() {
        c.vel.y = 2;
        arena.knockback(c, 1, 0, 1, 5);
        assertThat(c.vel.y).isEqualTo(5);
        assertThat(c.grounded).isFalse();

        arena.knockback(c, 1, 0, 1, 3);
        assertThat(c.vel.y).as("a weaker lift never slows an upward push").isEqualTo(5);
    }

    @Test
    void noLiftKeepsTheCombatantGrounded() {
        arena.knockback(c, 1, 0, 1, 0);

        assertThat(c.grounded).isTrue();
        assertThat(c.vel.y).isZero();
    }

    @Test
    void deadCombatantsAreNotPushed() {
        arena.damage(a, c, 10_000, Element.NEUTRAL, 0);

        arena.knockback(c, 1, 0, 50, 4);

        assertThat(c.knock.x).isZero();
        assertThat(events(EventType.KNOCKBACK)).isZero();
    }

    @Test
    void theClockAdvancesAndEventsCarryTheCurrentTick() {
        arena.advanceTick();
        arena.advanceTick();
        arena.damage(c, a, 5, Element.NEUTRAL, 0);

        assertThat(arena.tick()).isEqualTo(2);
        assertThat(last(EventType.DAMAGE).tick).isEqualTo(2);
    }

    @Test
    void anEmptyPipelineStepNeverDropsBelowZero() {
        DamageEvent event = new DamageEvent();
        event.amount = -5;
        DamagePipeline pipeline = new DamagePipeline(e -> e.amount -= 1);

        pipeline.run(event);

        assertThat(event.amount).isZero();
    }
}
