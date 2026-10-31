package com.bajoterra.core.ai;

import com.bajoterra.core.ammo.Loadout;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.SlugRole;

/**
 * Utility-based slug choice: every available slot gets a score for the current situation and the best one wins.
 * Damage slugs are weighted by elemental matchup and the target's remaining health; control slugs lose value
 * against targets that are already controlled; support slugs gain value with the ally's missing health.
 */
final class SlugUtilityScorer {

    /** Closer than this a bot avoids throwing a blinding slug at its target. */
    private static final double MIN_FLASH_DISTANCE = 9.0;

    /** Best available non-support slot against {@code target}, or -1. */
    int bestAttackSlot(BotContext context, Combatant target) {
        Combatant self = context.self();
        Loadout loadout = self.loadout;
        var chart = context.sim().content().elementChart();
        boolean controlled = target.statuses.has("stun") || target.statuses.has("freeze")
                || target.statuses.has("slow") || target.statuses.has("blind");
        double distance = Math.sqrt(target.pos.horizontalDistanceSq(self.pos));
        int best = -1;
        double bestScore = 0;
        for (int slot = 0; slot < Loadout.SIZE; slot++) {
            if (!loadout.isAvailable(slot)) {
                continue;
            }
            SlugDefinition slug = loadout.slug(slot);
            if (slug.role() == SlugRole.SUPPORT || raisesWall(slug)) {
                continue;
            }
            double score = slug.botWeight() * chart.multiplier(slug.element(), target.character.affinity());
            if (slug.role() == SlugRole.CONTROL) {
                score *= controlled ? 0.25 : 1.1;
            } else if (target.vitals.totalFraction() < 0.3) {
                score *= 1.3;
            }
            if (slug.projectile().gravity() > 8 && distance < 6) {
                score *= 0.5;
            }
            if (slug.onExpire().flashRadius() > 0 && distance < MIN_FLASH_DISTANCE) {
                // Too close: the flash would go off in the bot's own face.
                score *= 0.2;
            }
            score *= 1.0 + context.sim().arena().random().nextDouble() * 0.2;
            if (score > bestScore) {
                bestScore = score;
                best = slot;
            }
        }
        return best;
    }

    /** True when the slug raises a wall (it is cover, not an attack). */
    static boolean raisesWall(SlugDefinition slug) {
        return slug.onHit().raisesWall() || slug.onWorldHit().raisesWall() || slug.onExpire().raisesWall();
    }

    /** An available slot whose slug raises a wall, or -1. */
    int wallSlot(BotContext context) {
        Loadout loadout = context.self().loadout;
        for (int slot = 0; slot < Loadout.SIZE; slot++) {
            if (loadout.isAvailable(slot) && raisesWall(loadout.slug(slot))) {
                return slot;
            }
        }
        return -1;
    }

    /** Best available support slot for {@code ally}, or -1. */
    int bestSupportSlot(BotContext context, Combatant ally) {
        Loadout loadout = context.self().loadout;
        double need = 2.0 - ally.vitals.totalFraction() - ally.vitals.hpFraction();
        int best = -1;
        double bestScore = 0;
        for (int slot = 0; slot < Loadout.SIZE; slot++) {
            if (!loadout.isAvailable(slot)) {
                continue;
            }
            SlugDefinition slug = loadout.slug(slot);
            if (slug.role() != SlugRole.SUPPORT) {
                continue;
            }
            double score = slug.botWeight() * need * (1.0 + context.sim().arena().random().nextDouble() * 0.2);
            if (score > bestScore) {
                bestScore = score;
                best = slot;
            }
        }
        return best;
    }
}
