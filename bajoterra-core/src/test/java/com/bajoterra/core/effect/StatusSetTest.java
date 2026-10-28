package com.bajoterra.core.effect;

import static com.bajoterra.core.Fixtures.apply;
import static com.bajoterra.core.Fixtures.arena;
import static com.bajoterra.core.Fixtures.combatant;
import static com.bajoterra.core.Fixtures.effect;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventBuffer;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.slug.Element;
import java.util.List;
import org.junit.jupiter.api.Test;

/** Lifecycle of status effects: capacity, periodic hooks, expiry events and removal. */
class StatusSetTest {

    private final Combatant source = combatant(0, 1, "eli", "doc");
    private final Combatant target = combatant(1, 0, "kord", "doc");
    private final CombatArena arena = arena(source, target);   // array index must equal the combatant id

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

    private void tick(int times) {
        for (int i = 0; i < times; i++) {
            target.statuses.tick(target, arena);
            arena.advanceTick();
        }
    }

    private int activeCount() {
        int active = 0;
        for (int i = 0; i < StatusSet.CAPACITY; i++) {
            if (target.statuses.at(i).active()) {
                active++;
            }
        }
        return active;
    }

    private static AbstractStatusEffect status(String id, int durationTicks) {
        return new AbstractStatusEffect(new AbstractStatusEffect.Params(id, durationTicks, StackingPolicy.REFRESH, 1,
                0, List.of())) {
        };
    }

    private static String spec(String type, int durationTicks) {
        return switch (type) {
            case "burn" -> "{\"type\":\"burn\",\"dps\":2,\"durationTicks\":" + durationTicks + "}";
            case "slow" -> "{\"type\":\"slow\",\"factor\":0.5,\"durationTicks\":" + durationTicks + "}";
            case "regen" -> "{\"type\":\"regen\",\"hps\":2,\"durationTicks\":" + durationTicks + "}";
            case "blind" -> "{\"type\":\"blind\",\"durationTicks\":" + durationTicks + "}";
            default -> "{\"type\":\"" + type + "\",\"durationTicks\":" + durationTicks + "}";
        };
    }

    @Test
    void applyingAndExpiringAreBothAnnounced() {
        apply(arena, effect("{\"type\":\"stun\",\"durationTicks\":3}"), source, target);
        assertThat(events(EventType.STATUS_APPLIED)).isEqualTo(1);

        tick(2);
        assertThat(target.statuses.has("stun")).isTrue();
        assertThat(events(EventType.STATUS_EXPIRED)).isZero();

        tick(1);
        assertThat(target.statuses.has("stun")).isFalse();
        assertThat(events(EventType.STATUS_EXPIRED)).isEqualTo(1);
        assertThat(activeCount()).isZero();
    }

    @Test
    void periodicEffectsFireExactlyEveryPeriod() {
        apply(arena, effect("{\"type\":\"burn\",\"dps\":6,\"durationTicks\":60}"), source, target);

        tick(9);
        assertThat(events(EventType.DAMAGE)).isZero();
        tick(1);
        assertThat(events(EventType.DAMAGE)).isEqualTo(1);   // burn ticks every 10 ticks
        tick(10);
        assertThat(events(EventType.DAMAGE)).isEqualTo(2);
        assertThat(70 - target.vitals.shield()).isCloseTo(2.0, within(1e-9));   // 6 dps * 10/60 s, twice
    }

    @Test
    void burnStacksMultiplyTheDamage() {
        Effect burn = effect("{\"type\":\"burn\",\"dps\":6,\"durationTicks\":60,"
                + "\"stacking\":\"STACK\",\"maxStacks\":3}");
        apply(arena, burn, source, target);
        apply(arena, burn, source, target);

        tick(10);

        assertThat(70 - target.vitals.shield()).isCloseTo(2.0, within(1e-9));   // 2 stacks * 6 dps * 10/60
    }

    @Test
    void burnDamageIsAttributedToItsSource() {
        apply(arena, effect("{\"type\":\"burn\",\"dps\":600,\"durationTicks\":60}"), source, target);

        tick(10);

        assertThat(source.damageDealt).isGreaterThan(0);
    }

    @Test
    void whenFullTheStatusClosestToExpiringIsEvicted() {
        for (int i = 0; i < StatusSet.CAPACITY; i++) {
            apply(arena, status("s" + i, 100 - 10 * i), source, target);   // s7 has the least time left
        }
        assertThat(activeCount()).isEqualTo(StatusSet.CAPACITY);

        apply(arena, status("newcomer", 80), source, target);

        assertThat(activeCount()).isEqualTo(StatusSet.CAPACITY);
        assertThat(target.statuses.has("newcomer")).isTrue();
        assertThat(target.statuses.has("s7")).as("the shortest was evicted").isFalse();
        for (int i = 0; i < StatusSet.CAPACITY - 1; i++) {
            assertThat(target.statuses.has("s" + i)).isTrue();
        }
    }

    @Test
    void aFreeSlotIsUsedBeforeEvictingAnything() {
        for (int i = 0; i < StatusSet.CAPACITY - 1; i++) {
            apply(arena, status("s" + i, 10), source, target);
        }

        apply(arena, status("last", 500), source, target);

        for (int i = 0; i < StatusSet.CAPACITY - 1; i++) {
            assertThat(target.statuses.has("s" + i)).isTrue();
        }
        assertThat(target.statuses.has("last")).isTrue();
    }

    @Test
    void removingByTagReturnsHowManyWereRemovedAndKeepsTheOthers() {
        apply(arena, effect(spec("burn", 100)), source, target);
        apply(arena, effect(spec("slow", 100)), source, target);
        apply(arena, effect(spec("freeze", 100)), source, target);

        int removed = target.statuses.removeByTags(List.of("cc"));

        assertThat(removed).isEqualTo(2);   // slow and freeze carry "cc"
        assertThat(target.statuses.has("burn")).isTrue();
        assertThat(target.statuses.removeByTags(List.of("nothing-like-this"))).isZero();
        assertThat(target.statuses.removeByTags(List.of("burn", "dot"))).isEqualTo(1);   // counted once
    }

    @Test
    void clearRemovesEverythingWithoutExpiryEvents() {
        apply(arena, effect(spec("burn", 100)), source, target);
        apply(arena, effect(spec("stun", 100)), source, target);

        target.statuses.clear();

        assertThat(activeCount()).isZero();
        assertThat(events(EventType.STATUS_EXPIRED)).isZero();
    }

    @Test
    void modifiersOfAllActiveStatusesCombine() {
        apply(arena, effect(spec("slow", 100)), source, target);
        apply(arena, effect(spec("freeze", 100)), source, target);
        apply(arena, effect(spec("blind", 100)), source, target);
        apply(arena, effect(spec("stun", 100)), source, target);
        target.stats.reset();

        target.statuses.modify(target.stats);

        assertThat(target.stats.speedMultiplier).isCloseTo(0.5 * 0.15, within(1e-9));
        assertThat(target.stats.elementTakenMultiplier(Element.FIRE)).isCloseTo(1.25, within(1e-9));
        assertThat(target.stats.aimSpread).isCloseTo(0.25, within(1e-9));
        assertThat(target.stats.stunned).isTrue();
    }

    @Test
    void refreshingKeepsASingleInstanceWithTheLatestSource() {
        Combatant other = combatant(2, 1, "eli", "doc");
        CombatArena wide = arena(source, target, other);
        apply(wide, effect(spec("burn", 100)), source, target);
        apply(wide, effect(spec("burn", 100)), other, target);

        int found = 0;
        for (int i = 0; i < StatusSet.CAPACITY; i++) {
            if (target.statuses.at(i).active()) {
                found++;
                assertThat(target.statuses.at(i).sourceId()).isEqualTo(2);
            }
        }
        assertThat(found).isEqualTo(1);
    }

    @Test
    void statusStrengthsRankStrongerEffectsHigher() {
        AbstractStatusEffect weakBurn = (AbstractStatusEffect) effect(spec("burn", 60));
        AbstractStatusEffect strongBurn = (AbstractStatusEffect) effect(
                "{\"type\":\"burn\",\"dps\":9,\"durationTicks\":60}");
        AbstractStatusEffect slow = (AbstractStatusEffect) effect(spec("slow", 60));
        AbstractStatusEffect freeze = (AbstractStatusEffect) effect(spec("freeze", 60));
        AbstractStatusEffect regen = (AbstractStatusEffect) effect(spec("regen", 60));
        AbstractStatusEffect blind = (AbstractStatusEffect) effect(spec("blind", 60));

        assertThat(strongBurn.strength()).isGreaterThan(weakBurn.strength());
        assertThat(weakBurn.strength()).isEqualTo(2.0);
        assertThat(slow.strength()).isEqualTo(0.5);
        assertThat(freeze.strength()).isCloseTo(0.85, within(1e-9));
        assertThat(regen.strength()).isEqualTo(2.0);
        assertThat(blind.strength()).isCloseTo(0.25, within(1e-9));
    }

    @Test
    void regenerationIsScaledByTheCastersPotencyAndTheContextScale() {
        Combatant trixie = combatant(3, 0, "trixie", "doc");   // potency 1.25
        CombatArena local = arena(trixie, target);
        local.damage(null, target, 100, Element.NEUTRAL, 1.0);
        double before = target.vitals.hp();
        apply(local, effect("{\"type\":\"regen\",\"hps\":6,\"durationTicks\":60}"), trixie, target);

        for (int i = 0; i < 10; i++) {
            target.statuses.tick(target, local);
        }

        assertThat(target.vitals.hp() - before).isCloseTo(6 * 1.25 * 10 / 60.0, within(1e-9));
    }
}
