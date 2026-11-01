package com.bajoterra.server;

import static com.bajoterra.server.ServerFixtures.CONTENT;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.rules.MatchPhase;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.InputFrame;
import com.bajoterra.core.sim.MatchConfig;
import com.bajoterra.core.sim.SlotConfig;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Base64;
import java.util.List;
import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.Test;

/**
 * Records how the real server moves a combatant under scripted inputs (walking, strafing, jumping, crouching,
 * rolling, dashing, sliding along walls, climbing onto low cover) and pins the recording in a text fixture inside the
 * Unity project. The C# movement model of the client replays the very same inputs and must reach the same positions,
 * so a change to the server's movement rules fails here until the fixture is regenerated, and then fails in the
 * client tests until the C# model is updated too.
 *
 * <p>Regenerate with {@code mvn -pl bajoterra-server test -Dtest=MovementParityFixtureTest
 * -Dbajoterra.updateFixtures=true}.
 */
class MovementParityFixtureTest {

    private static final Path FIXTURE = Path.of("..", "unity", "BajoTerraClient", "Assets", "Tests", "EditMode",
            "Fixtures", "movement-parity.txt");

    /** Hold an input for a number of ticks. */
    private record Hold(int ticks, float moveX, float moveZ, float yaw, int mask) {
    }

    private record Scenario(String name, String character, double x, double z, double yaw, List<Hold> holds) {
    }

    private static final float QUARTER = (float) (Math.PI / 2);

    private static List<Scenario> scenarios() {
        // Eli's move mask bits: 1 jump, 2 crouch, 4 roll, 8 wall kick. Pronto: 1 jump, 2 crouch, 4 roll, 8 dash,
        // 16 wall kick.
        return List.of(
                new Scenario("walk_and_strafe", "eli", -25.03, 0.02, QUARTER, List.of(
                        new Hold(45, 0f, 1f, QUARTER, 0),
                        new Hold(45, 1f, 0f, QUARTER, 0),
                        new Hold(45, 1f, 1f, 0.6f, 0),
                        new Hold(30, -0.5f, 0.2f, -1.2f, 0))),
                new Scenario("jump_chain", "eli", -10.03, 0.02, 0f, List.of(
                        new Hold(20, 0f, 0f, 0f, 0),
                        new Hold(1, 0f, 1f, 0f, 1),
                        new Hold(50, 0f, 1f, 0f, 0),
                        new Hold(60, 0.5f, 1f, 0.4f, 1),
                        new Hold(40, 0f, 0f, 0.4f, 0))),
                new Scenario("crouch_then_jump_from_crouch", "eli", -8.03, 4.02, 0.5f, List.of(
                        new Hold(60, 0f, 1f, 0.5f, 2),
                        new Hold(30, 0f, 1f, 0.5f, 0),
                        new Hold(1, 0f, 1f, 0.5f, 3),
                        new Hold(50, 0f, 1f, 0.5f, 2),
                        new Hold(20, 0f, 0f, 0.5f, 0))),
                new Scenario("roll_cooldown_and_repeat", "eli", -12.03, -6.02, QUARTER, List.of(
                        new Hold(10, 0f, 1f, QUARTER, 0),
                        new Hold(45, 0f, 1f, QUARTER, 4),
                        new Hold(60, 0f, 1f, QUARTER, 0),
                        new Hold(60, 1f, 1f, QUARTER, 4),
                        new Hold(10, 0f, 0f, QUARTER, 0))),
                new Scenario("pronto_dash_ground_and_air", "pronto", -6.03, -10.02, 0f, List.of(
                        new Hold(20, 0f, 0f, 0f, 0),
                        new Hold(8, 1f, 0f, 0f, 8),
                        new Hold(60, 0f, 1f, 0f, 0),
                        new Hold(1, 0f, 1f, 0f, 1),
                        new Hold(6, 0f, 1f, 0f, 0),
                        new Hold(14, -1f, 0f, 0f, 8),
                        new Hold(50, 0f, 0f, 0f, 0))),
                new Scenario("slide_along_wall", "eli", -6.03, 8.02, QUARTER, List.of(
                        new Hold(60, 0f, 1f, QUARTER, 0),
                        new Hold(45, 0f, 1f, 1.1f, 0),
                        new Hold(45, 0f, 1f, 2.0f, 0),
                        new Hold(30, -1f, 0f, QUARTER, 0))),
                new Scenario("jump_onto_low_cover", "eli", -6.03, 0.02, QUARTER, List.of(
                        new Hold(50, 0f, 1f, QUARTER, 0),
                        new Hold(1, 0f, 0.97f, QUARTER, 1),
                        new Hold(60, 0f, 0.97f, QUARTER, 0),
                        new Hold(60, 0f, 0.97f, QUARTER, 0))),
                new Scenario("map_boundary", "eli", -28.03, 0.02, -QUARTER, List.of(
                        new Hold(60, 0f, 1f, -QUARTER, 0),
                        new Hold(30, 1f, 0f, -QUARTER, 0))),
                // Up the south ramp of the west hill (rises towards +z), across its top and down the north ramp.
                new Scenario("ramp_over_the_hill", "eli", -15.53, -9.02, 0f, List.of(
                        new Hold(20, 0f, 0f, 0f, 0),
                        new Hold(185, 0f, 1f, 0f, 0),
                        new Hold(30, 0f, 0f, 0f, 0))),
                // Climb, jump from the slope, drift sideways in the air and walk back down backwards.
                new Scenario("ramp_jump_and_back_down", "eli", -15.53, -7.02, 0f, List.of(
                        new Hold(25, 0f, 1f, 0f, 0),
                        new Hold(1, 0f, 1f, 0f, 1),
                        new Hold(55, 0.4f, 1f, 0.3f, 0),
                        new Hold(80, 0f, -1f, 0.3f, 0),
                        new Hold(20, 0f, 0f, 0.3f, 0))),
                // Along the north balcony: up its east ramp (rises towards -x), over the top, and a roll on it.
                new Scenario("balcony_east_ramp", "eli", 13.03, 18.52, -QUARTER, List.of(
                        new Hold(90, 0f, 1f, -QUARTER, 0),
                        new Hold(45, 0f, 1f, -QUARTER, 4),
                        new Hold(60, 0f, 1f, -QUARTER, 0),
                        new Hold(60, 0f, -1f, -QUARTER, 0))),
                // Sprint down a ramp with a dash (Pronto): the speed must not launch the body off the slope.
                new Scenario("dash_down_the_ramp", "pronto", -15.53, 3.52, 0f, List.of(
                        new Hold(30, 0f, 1f, 0f, 0),
                        new Hold(12, 0f, 1f, 0f, 8),
                        new Hold(60, 0f, 1f, 0f, 0))),
                // Run at the west edge of the arena, jump, and kick off it with a fresh press; a second kick off the
                // same wall is refused.
                new Scenario("wall_kick_off_the_edge", "eli", -27.03, 0.02, -QUARTER, List.of(
                        new Hold(40, 0f, 1f, -QUARTER, 0),
                        new Hold(1, 0f, 1f, -QUARTER, 9),
                        new Hold(14, 0f, 1f, -QUARTER, 0),
                        new Hold(1, 0f, 1f, -QUARTER, 8),
                        new Hold(10, 0f, -1f, -QUARTER, 0),
                        new Hold(1, 0f, -1f, -QUARTER, 8),
                        new Hold(60, 0f, 0f, -QUARTER, 0))),
                // Holding the button from the jump never kicks: the kick needs a fresh press.
                new Scenario("held_jump_does_not_kick", "pronto", -27.03, 4.02, -QUARTER, List.of(
                        new Hold(40, 0f, 1f, -QUARTER, 0),
                        new Hold(50, 0f, 1f, -QUARTER, 17),
                        new Hold(20, 0f, 0f, -QUARTER, 0))),
                // Along the wall the slide scenario uses: jump next to it and kick off it.
                new Scenario("wall_kick_off_cover", "eli", -6.03, 8.02, QUARTER, List.of(
                        new Hold(60, 0f, 1f, QUARTER, 0),
                        new Hold(1, 0f, 1f, QUARTER, 1),
                        new Hold(12, 0f, 1f, QUARTER, 0),
                        new Hold(1, 0f, 1f, QUARTER, 8),
                        new Hold(60, 0f, 0f, QUARTER, 0))));
    }

    @Test
    void theRecordedMovementMatchesTheCommittedFixture() throws IOException {
        Assumptions.assumeTrue(Files.isDirectory(FIXTURE.getParent()),
                "The Unity project is not next to this module; nothing to compare");
        String generated = generate();

        if (Boolean.getBoolean("bajoterra.updateFixtures")) {
            Files.writeString(FIXTURE, generated, StandardCharsets.UTF_8);
            return;
        }

        assertThat(Files.exists(FIXTURE)).as("fixture exists; create it with -Dbajoterra.updateFixtures=true")
                .isTrue();
        assertThat(Files.readString(FIXTURE, StandardCharsets.UTF_8))
                .as("The server's movement changed. Regenerate the fixture (see the class comment) and update the"
                        + " C# MovementModel until its parity tests pass again.")
                .isEqualTo(generated);
    }

    @Test
    void everyScenarioEndsWhereTheScriptSuggests() {
        // Sanity checks of the scenarios themselves, so the fixture is not accidentally meaningless.
        GameSimulation cover = prepared("eli", -6.03, 0.02, QUARTER);
        run(cover, scenarios().get(6).holds());
        Combatant c = cover.combatants()[0];
        // Walking into the 1 m box, jumping onto it and walking on ends past the box on the ground.
        assertThat(c.pos.x).isGreaterThan(1.0);

        GameSimulation boundary = prepared("eli", -28.03, 0.02, -QUARTER);
        run(boundary, scenarios().get(7).holds());
        assertThat(boundary.combatants()[0].pos.x).isEqualTo(-30 + boundary.combatants()[0].hitRadius(),
                within(1e-9));
    }

    @Test
    void theRampScenariosReallyClimbAndComeBackDown() {
        GameSimulation hill = prepared("eli", -15.53, -9.02, 0f);
        double highest = 0;
        int sequence = 0;
        Combatant c = hill.combatants()[0];
        for (Hold hold : scenarios().get(8).holds()) {
            for (int i = 0; i < hold.ticks(); i++) {
                hill.submit(new InputFrame(0, ++sequence, hold.moveX(), hold.moveZ(), hold.yaw(), 0f, false, 0f, 0f,
                        0f, false, hold.mask(), -1, 0));
                hill.tick();
                highest = Math.max(highest, c.pos.y);
            }
        }
        assertThat(highest).as("reached the 2 m plateau").isEqualTo(2.0, within(0.01));
        assertThat(c.pos.z).isGreaterThan(8.0);
        assertThat(c.pos.y).as("back on the ground after the north ramp").isZero();

        GameSimulation balcony = prepared("eli", 13.03, 18.52, -QUARTER);
        double balconyTop = 0;
        Combatant b = balcony.combatants()[0];
        sequence = 0;
        for (Hold hold : scenarios().get(10).holds()) {
            for (int i = 0; i < hold.ticks(); i++) {
                balcony.submit(new InputFrame(0, ++sequence, hold.moveX(), hold.moveZ(), hold.yaw(), 0f, false, 0f,
                        0f, 0f, false, hold.mask(), -1, 0));
                balcony.tick();
                balconyTop = Math.max(balconyTop, b.pos.y);
            }
        }
        assertThat(balconyTop).as("reached the 2.4 m balcony").isEqualTo(2.4, within(0.01));
    }

    // ------------------------------------------------------------------------------------------ generation

    private static String generate() {
        StringBuilder out = new StringBuilder(256 * 1024);
        out.append("# Recorded by MovementParityFixtureTest from the Java server. Do not edit by hand.\n");
        out.append("details ").append(b64(SnapshotBuilder.details(CONTENT).toByteArray())).append('\n');
        out.append("map ").append(b64(SnapshotBuilder.mapData(CONTENT.maps().require("caverna"), CONTENT)
                .toByteArray())).append('\n');
        for (Scenario scenario : scenarios()) {
            GameSimulation sim = prepared(scenario.character(), scenario.x(), scenario.z(), scenario.yaw());
            out.append("scenario ").append(scenario.name()).append(' ').append(scenario.character()).append('\n');
            snap(out, sim, 0);
            int n = 0;
            int sequence = 0;
            for (Hold hold : scenario.holds()) {
                for (int i = 0; i < hold.ticks(); i++) {
                    sim.submit(new InputFrame(0, ++sequence, hold.moveX(), hold.moveZ(), hold.yaw(), 0f, false, 0f,
                            0f, 0f, false, hold.mask(), -1, 0));
                    sim.tick();
                    n++;
                    Combatant c = sim.combatants()[0];
                    out.append("tick ").append(n).append(' ').append(hold.moveX()).append(' ').append(hold.moveZ())
                            .append(' ').append(hold.yaw()).append(' ').append(hold.mask()).append(" | ")
                            .append((float) c.pos.x).append(' ').append((float) c.pos.y).append(' ')
                            .append((float) c.pos.z).append(' ').append(c.posture.ordinal()).append(' ')
                            .append(c.moves.active() == null ? -1 : c.moves.active().index()).append(' ')
                            .append(c.grounded ? 1 : 0).append('\n');
                    if (n % 10 == 0) {
                        snap(out, sim, n);
                    }
                }
            }
            out.append("end\n");
        }
        return out.toString();
    }

    private static void snap(StringBuilder out, GameSimulation sim, int n) {
        Combatant c = sim.combatants()[0];
        out.append("snap ").append(n).append(' ').append(sim.tickCount()).append(' ')
                .append(b64(SnapshotBuilder.combatant(c).toByteArray())).append(' ')
                .append(b64(SnapshotBuilder.localState(c, sim.tickCount()).toByteArray())).append('\n');
    }

    /** A match advanced to the first active round, with only the human moving, placed at the given spot. */
    private static GameSimulation prepared(String character, double x, double z, double yaw) {
        GameSimulation sim = new GameSimulation(CONTENT, MatchConfig.builder("caverna").seed(7)
                .slot(SlotConfig.human(0, character, List.of())).build());
        sim.start();
        while (sim.match().phase() != MatchPhase.ROUND_ACTIVE) {
            sim.tick();
        }
        // Everybody else stands still and silent: no brain, no held fire button, nothing already in flight.
        for (Combatant combatant : sim.combatants()) {
            combatant.brain = null;
            combatant.input.reset();
        }
        sim.projectiles().releaseAll();
        Combatant human = sim.combatants()[0];
        human.pos.set(x, 0, z);
        human.vel.setZero();
        human.knock.setZero();
        human.yaw = yaw;
        human.grounded = true;
        return sim;
    }

    private static void run(GameSimulation sim, List<Hold> holds) {
        int sequence = 0;
        for (Hold hold : holds) {
            for (int i = 0; i < hold.ticks(); i++) {
                sim.submit(new InputFrame(0, ++sequence, hold.moveX(), hold.moveZ(), hold.yaw(), 0f, false, 0f, 0f,
                        0f, false, hold.mask(), -1, 0));
                sim.tick();
            }
        }
    }

    private static String b64(byte[] bytes) {
        return Base64.getEncoder().encodeToString(bytes);
    }
}
