package com.bajoterra.core.combat;

import static com.bajoterra.core.Fixtures.arena;
import static com.bajoterra.core.Fixtures.combatant;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.slug.Element;
import com.bajoterra.core.slug.ElementChart;
import org.junit.jupiter.api.Test;

class DamagePipelineTest {

    @Test
    void neutralDamageIsAppliedAsIs() {
        Combatant attacker = combatant(0, 0, "eli", "infurnus");
        Combatant victim = combatant(1, 1, "eli", "infurnus");
        CombatArena arena = arena(attacker, victim);

        double dealt = arena.damage(attacker, victim, 20, Element.NEUTRAL, 0);

        assertThat(dealt).isEqualTo(20);
        assertThat(victim.vitals.shield()).isEqualTo(30);
        assertThat(attacker.damageDealt).isEqualTo(20);
        assertThat(victim.damageTaken).isEqualTo(20);
    }

    @Test
    void elementalAdvantageMultipliesDamage() {
        Combatant attacker = combatant(0, 0, "eli", "infurnus");
        Combatant waterUser = combatant(1, 1, "trixie", "infurnus"); // affinity WATER
        CombatArena arena = arena(attacker, waterUser);

        double dealt = arena.damage(attacker, waterUser, 20, Element.FIRE, 0);

        assertThat(dealt).isCloseTo(20 * 0.8, within(1e-9)); // fire is weak against water
    }

    @Test
    void statusVulnerabilityStacksWithTheElementChart() {
        Combatant attacker = combatant(0, 0, "eli", "infurnus");
        Combatant victim = combatant(1, 1, "eli", "infurnus");
        CombatArena arena = arena(attacker, victim);
        victim.stats.multiplyElementTaken(Element.FIRE, 1.25);

        double dealt = arena.damage(attacker, victim, 20, Element.FIRE, 0);

        assertThat(dealt).isCloseTo(25, within(1e-9));
    }

    @Test
    void invulnerabilityBlocksAllDamage() {
        Combatant attacker = combatant(0, 0, "eli", "infurnus");
        Combatant victim = combatant(1, 1, "eli", "infurnus");
        CombatArena arena = arena(attacker, victim);
        victim.stats.invulnerable = true;

        double dealt = arena.damage(attacker, victim, 500, Element.NEUTRAL, 0);

        assertThat(dealt).isZero();
        assertThat(victim.alive).isTrue();
        assertThat(victim.vitals.hp()).isEqualTo(victim.vitals.maxHp());
    }

    @Test
    void friendlyFireIsDisabledByDefaultButSelfDamageIsNot() {
        Combatant shooter = combatant(0, 0, "eli", "infurnus");
        Combatant teammate = combatant(1, 0, "eli", "infurnus");
        CombatArena arena = arena(shooter, teammate);

        assertThat(arena.damage(shooter, teammate, 30, Element.NEUTRAL, 0)).isZero();
        assertThat(arena.damage(shooter, shooter, 30, Element.NEUTRAL, 0)).isEqualTo(30);
    }

    @Test
    void friendlyFireMultiplierCanBeEnabled() {
        Combatant shooter = combatant(0, 0, "eli", "infurnus");
        Combatant teammate = combatant(1, 0, "eli", "infurnus");
        Combatant[] all = {shooter, teammate};
        CombatArena arena = new CombatArena(all,
                DamagePipeline.standard(ElementChart.neutral(), 0.5),
                new com.bajoterra.core.event.EventBuffer(64), 1);

        assertThat(arena.damage(shooter, teammate, 30, Element.NEUTRAL, 0)).isEqualTo(15);
    }

    @Test
    void killingBlowMarksTheVictimDefeatedAndCreditsTheKiller() {
        Combatant killer = combatant(0, 0, "eli", "infurnus");
        Combatant victim = combatant(1, 1, "pronto", "infurnus"); // 80 hp + 40 shield
        CombatArena arena = arena(killer, victim);

        arena.damage(killer, victim, 119, Element.NEUTRAL, 0);
        assertThat(victim.alive).isTrue();

        arena.damage(killer, victim, 5, Element.NEUTRAL, 0);

        assertThat(victim.alive).isFalse();
        assertThat(victim.deaths).isEqualTo(1);
        assertThat(killer.kills).isEqualTo(1);
        assertThat(arena.aliveCount(1)).isZero();
    }

    @Test
    void deadCombatantsTakeNoFurtherDamage() {
        Combatant killer = combatant(0, 0, "eli", "infurnus");
        Combatant victim = combatant(1, 1, "pronto", "infurnus");
        CombatArena arena = arena(killer, victim);
        arena.damage(killer, victim, 1000, Element.NEUTRAL, 0);

        assertThat(arena.damage(killer, victim, 50, Element.NEUTRAL, 0)).isZero();
        assertThat(killer.kills).isEqualTo(1);
    }

    @Test
    void healingHonoursTheHealReceivedMultiplier() {
        Combatant healer = combatant(0, 0, "trixie", "doc");
        Combatant ally = combatant(1, 0, "eli", "doc");
        CombatArena arena = arena(healer, ally);
        arena.damage(null, ally, 80, Element.NEUTRAL, 1.0); // piercing: straight to health
        ally.stats.healReceivedMultiplier = 0.5;

        double healed = arena.heal(healer, ally, 40);

        assertThat(healed).isEqualTo(20);
        assertThat(healer.healingDone).isEqualTo(20);
    }
}
