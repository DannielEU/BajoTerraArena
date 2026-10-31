package com.bajoterra.server;

import com.bajoterra.core.character.CharacterDefinition;
import com.bajoterra.core.content.Content;
import com.bajoterra.core.entity.Posture;
import com.bajoterra.core.item.PickupDefinition;
import com.bajoterra.core.move.MoveDefinition;
import com.bajoterra.core.slug.Element;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.world.GameMap;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;
import java.io.InputStream;
import java.io.PrintStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * Prints a Markdown catalog of all loaded content (ids, stats and a one-line description of every slug's effects).
 * It is the reference the Unity team uses to map content ids to models and animations.
 *
 * <p>Usage: {@code ContentCatalogPrinter [output.md]} (stdout when no file is given). Run it again whenever content
 * changes; the committed {@code docs/content-catalog.md} is its output.
 */
public final class ContentCatalogPrinter {

    private static final ObjectMapper MAPPER = new ObjectMapper();

    private ContentCatalogPrinter() {
        throw new AssertionError("Utility class, not instantiable");
    }

    public static void main(String[] args) throws IOException {
        String markdown = render(Content.load());
        if (args.length > 0) {
            Files.writeString(Path.of(args[0]), markdown, StandardCharsets.UTF_8);
        } else {
            new PrintStream(System.out, true, StandardCharsets.UTF_8).print(markdown);
        }
    }

    /** Renders the whole catalog. */
    public static String render(Content content) {
        StringBuilder out = new StringBuilder();
        out.append("# Catálogo de contenido\n\n");
        out.append("> Generado por `ContentCatalogPrinter`; no editar a mano. Los números son valores iniciales de ")
                .append("balance y se ajustan en los JSON de `bajoterra-core/src/main/resources/content/`.\n\n");

        out.append("## Babosas (").append(content.slugs().size()).append(")\n\n");
        out.append("| índice | id | elemento | rol | objetivo | velocidad | gravedad | retardo | ")
                .append("efectos al impactar |\n");
        out.append("|---|---|---|---|---|---|---|---|---|\n");
        for (SlugDefinition slug : content.slugs().all()) {
            out.append(String.format(Locale.ROOT, "| %d | `%s` | %s | %s | %s | %.0f m/s | %.0f | ×%.1f | %s |%n",
                    slug.index(), slug.id(), slug.element(), slug.role(), slug.targeting(),
                    slug.projectile().speed(), slug.projectile().gravity(), slug.fireDelayMultiplier(),
                    describeSlug(slug)));
        }

        out.append("\n## Personajes (").append(content.characters().size()).append(")\n\n");
        out.append("| índice | id | vida | escudo | velocidad | afinidad | potencia de cura | movimientos | ")
                .append("blaster |\n");
        out.append("|---|---|---|---|---|---|---|---|---|\n");
        for (CharacterDefinition c : content.characters().all()) {
            List<String> moves = new ArrayList<>();
            c.moves().forEach(m -> moves.add(m.id()));
            out.append(String.format(Locale.ROOT, "| %d | `%s` | %.0f | %.0f | %.1f m/s | %s | ×%.2f | %s | %d ticks, "
                            + "bloqueo %d tiros |%n", c.index(), c.id(), c.maxHp(), c.maxShield(), c.speed(),
                    c.affinity(), c.healPotency(), String.join(", ", moves), c.blaster().fireIntervalTicks(),
                    c.blaster().reloadShots()));
        }

        out.append("\n## Movimientos (").append(content.moves().size()).append(")\n\n");
        out.append("| índice | id | duración | enfriamiento | tipo | desde | invulnerable | conducta |\n");
        out.append("|---|---|---|---|---|---|---|---|\n");
        for (MoveDefinition m : content.moves().all()) {
            out.append(String.format(Locale.ROOT, "| %d | `%s` | %s | %.2f s | %s | %s | %s | %s |%n", m.index(),
                    m.id(), m.holdable() ? "mientras se mantiene" : seconds(m.durationTicks()),
                    m.cooldownTicks() / (double) SimulationConstants.TICKS_PER_SECOND,
                    m.stance() ? "postura" : (m.holdable() ? "mantenido" : "acción"), postures(m.allowedFromMask()),
                    m.invulnerableToTick() > m.invulnerableFromTick()
                            ? seconds(m.invulnerableFromTick()) + "–" + seconds(m.invulnerableToTick()) : "no",
                    m.behavior().getClass().getSimpleName()));
        }

        out.append("\n## Objetos (").append(content.pickups().size()).append(")\n\n");
        out.append("| índice | id | requisito | reaparece | radio |\n|---|---|---|---|---|\n");
        for (PickupDefinition p : content.pickups().all()) {
            out.append(String.format(Locale.ROOT, "| %d | `%s` | %s | %s | %.1f m |%n", p.index(), p.id(),
                    p.requirement(), seconds(p.respawnTicks()), p.radius()));
        }

        out.append("\n## Mapas (").append(content.maps().size()).append(")\n\n");
        out.append("| índice | id | tamaño | obstáculos | spawns por equipo | objetos | nodos de navegación | "
                + "puntos de cobertura |\n|---|---|---|---|---|---|---|---|\n");
        for (GameMap map : content.maps().all()) {
            out.append(String.format(Locale.ROOT, "| %d | `%s` | %.0f × %.0f m | %d | %d / %d | %d | %d | %d |%n",
                    map.index(), map.id(), map.maxX() - map.minX(), map.maxZ() - map.minZ(), map.obstacleCount(),
                    map.spawns().get(0).size(), map.spawns().get(1).size(), map.pickups().size(),
                    map.nav().size(), map.coverCount()));
        }

        out.append("\n## Ventajas elementales\n\nMultiplicador cuando el elemento atacante (fila) golpea a un ")
                .append("personaje ")
                .append("con la afinidad indicada (columna). Solo se listan los valores distintos de 1.\n\n");
        out.append("| ataca | contra | multiplicador |\n|---|---|---|\n");
        for (Element attacker : Element.values()) {
            for (Element defender : Element.values()) {
                double multiplier = content.elementChart().multiplier(attacker, defender);
                if (Math.abs(multiplier - 1.0) > 1e-9) {
                    out.append(String.format(Locale.ROOT, "| %s | %s | ×%.2f |%n", attacker, defender, multiplier));
                }
            }
        }

        out.append("\n## Tipos de efecto registrados\n\n");
        content.effects().types().forEach(type -> out.append("- `").append(type).append("`\n"));
        return out.toString();
    }

    // ------------------------------------------------------------------------------------------ helpers

    private static String seconds(int ticks) {
        return String.format(Locale.ROOT, "%.2f s", ticks / (double) SimulationConstants.TICKS_PER_SECOND);
    }

    private static String postures(int mask) {
        List<String> names = new ArrayList<>();
        for (Posture posture : Posture.values()) {
            if ((mask & posture.bit()) != 0) {
                names.add(posture.name());
            }
        }
        return String.join(", ", names);
    }

    private static String describeSlug(SlugDefinition slug) {
        JsonNode json = readSlugJson(slug.id());
        if (json == null) {
            return "-";
        }
        List<String> parts = new ArrayList<>();
        if (json.has("onHit")) {
            parts.add(describeList(json.path("onHit")));
        }
        if (json.has("onWorldHit")) {
            parts.add("al chocar con un obstáculo: " + describeList(json.path("onWorldHit")));
        }
        if (slug.projectile().sticky()) {
            parts.add(String.format(Locale.ROOT, "se pega donde choca y a los %.1f s explota: %s",
                    slug.projectile().fuseTicks() / (double) SimulationConstants.TICKS_PER_SECOND,
                    describeList(json.path("onExpire"))));
        } else if (json.has("onExpire")) {
            parts.add("al caducar: " + describeList(json.path("onExpire")));
        }
        if (slug.projectile().pierce() > 0) {
            parts.add("atraviesa " + slug.projectile().pierce());
        }
        if (slug.trail().exists()) {
            parts.add(String.format(Locale.ROOT, "deja un muro de fuego de hasta %.0f m que arde %.1f s: %s",
                    slug.trail().maxLength(),
                    slug.trail().durationTicks() / (double) SimulationConstants.TICKS_PER_SECOND,
                    describeList(json.path("trail").path("effects"))));
        }
        return String.join("; ", parts);
    }

    private static JsonNode readSlugJson(String id) {
        try (InputStream in = ContentCatalogPrinter.class.getClassLoader()
                .getResourceAsStream("content/slugs/" + id + ".json")) {
            return in == null ? null : MAPPER.readTree(in);
        } catch (IOException e) {
            return null;
        }
    }

    private static String describeList(JsonNode effects) {
        List<String> parts = new ArrayList<>();
        for (JsonNode effect : effects) {
            parts.add(describe(effect));
        }
        return String.join(" + ", parts);
    }

    private static String describe(JsonNode e) {
        String type = e.path("type").asText();
        return switch (type) {
            case "damage" -> "daño " + e.path("amount").asText()
                    + (e.has("shieldPierce")
                    ? " (ignora " + (int) (e.path("shieldPierce").asDouble() * 100) + "% escudo)" : "");
            case "heal" -> "cura " + e.path("amount").asText();
            case "restore_shield" -> "escudo +" + e.path("amount").asText();
            case "overshield" -> "escudo extra " + e.path("amount").asText() + " (" + duration(e) + ")";
            case "knockback" -> "empuje " + e.path("force").asText();
            case "burn" -> "quemadura " + e.path("dps").asText() + "/s (" + duration(e) + ")";
            case "slow" -> "lentitud ×" + e.path("factor").asText() + " (" + duration(e) + ")";
            case "freeze" -> "congela (" + duration(e) + ")";
            case "stun" -> "aturde (" + duration(e) + ")";
            case "regen" -> "regeneración " + e.path("hps").asText() + "/s (" + duration(e) + ")";
            case "blind" -> "ciega (" + duration(e) + ")";
            case "cleanse" -> "limpia " + e.path("tags").toString();
            case "area" -> "área r=" + e.path("radius").asText() + " [" + describeList(e.path("effects")) + "]";
            case "flash" -> "destello visible a " + e.path("radius").asText() + " m [" + describeList(e.path("effects"))
                    + "] a quien lo mire";
            case "ice_wall" -> "levanta un muro de hielo (" + e.path("hits").asText("2") + " impactos, " + duration(e)
                    + ")";
            case "chain" -> "cadena " + e.path("jumps").asText(" 1") + "× [" + describeList(e.path("effects")) + "]";
            default -> type;
        };
    }

    private static String duration(JsonNode e) {
        if (e.has("durationSeconds")) {
            return e.path("durationSeconds").asText() + " s";
        }
        return e.path("durationTicks").asText() + " ticks";
    }
}
