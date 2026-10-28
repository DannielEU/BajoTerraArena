package com.bajoterra.server;

import static com.bajoterra.server.ServerFixtures.CONTENT;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.event.EventType;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.MatchConfig;
import com.bajoterra.protocol.generated.CombatantState;
import com.bajoterra.protocol.generated.EventKind;
import com.bajoterra.protocol.generated.InputFrame;
import com.bajoterra.protocol.generated.Posture;
import com.bajoterra.protocol.generated.Snapshot;
import java.util.concurrent.atomic.AtomicInteger;
import org.junit.jupiter.api.Test;

/** Unit tests of the small server building blocks. */
class SmallServerTests {

    @Test
    void everyCoreEventTypeHasAMatchingWireKind() {
        for (EventType type : EventType.values()) {
            EventKind kind = EventKind.forNumber(type.ordinal());
            assertThat(kind).as("kind for %s", type).isNotNull();
            assertThat(kind.name()).isEqualTo(type.name());
        }
        assertThat(EventKind.values().length - 1).as("no extra kinds (minus UNRECOGNIZED)")
                .isEqualTo(EventType.values().length);
    }

    @Test
    void everyCorePostureAndPhaseHasAMatchingWireValue() {
        for (com.bajoterra.core.entity.Posture posture : com.bajoterra.core.entity.Posture.values()) {
            assertThat(Posture.forNumber(posture.ordinal()).name()).isEqualTo(posture.name());
        }
        for (com.bajoterra.core.rules.MatchPhase phase : com.bajoterra.core.rules.MatchPhase.values()) {
            assertThat(com.bajoterra.protocol.generated.MatchPhase.forNumber(phase.ordinal()).name())
                    .isEqualTo(phase.name());
        }
    }

    @Test
    void theManifestListsContentInIndexOrder() {
        var manifest = SnapshotBuilder.manifest(CONTENT);

        for (int i = 0; i < CONTENT.slugs().size(); i++) {
            assertThat(manifest.getSlugIds(i)).isEqualTo(CONTENT.slugs().get(i).id());
        }
        assertThat(manifest.getCharacterIdsList()).contains("eli", "pronto");
        assertThat(manifest.getMapIdsList()).contains("caverna");
        assertThat(manifest.getPickupIdsList()).contains("health_pack");
    }

    @Test
    void aSnapshotMirrorsTheSimulationState() {
        GameSimulation sim = new GameSimulation(CONTENT, MatchConfig.builder("caverna").seed(3).build());
        sim.start();
        for (int i = 0; i < 300; i++) {
            sim.tick();
        }

        Snapshot snapshot = SnapshotBuilder.snapshot(sim);

        assertThat(snapshot.getTick()).isEqualTo(300);
        assertThat(snapshot.getCombatantsCount()).isEqualTo(6);
        for (CombatantState state : snapshot.getCombatantsList()) {
            var c = sim.combatants()[state.getId()];
            assertThat(state.getX()).isEqualTo((float) c.pos.x);
            assertThat(state.getHp()).isEqualTo((float) c.vitals.hp());
            assertThat(state.getAlive()).isEqualTo(c.alive);
            assertThat(state.getSlotsCount()).isEqualTo(10);
            assertThat(state.getSelectedSlot()).isEqualTo(c.loadout.selected());
        }
        assertThat(snapshot.getProjectilesCount()).isEqualTo(sim.projectiles().activeCount());
        assertThat(snapshot.getMatch().getRound()).isEqualTo(1);
    }

    @Test
    void eventBatchesResumeWhereThePreviousOneStoppedAndAreCapped() {
        GameSimulation sim = new GameSimulation(CONTENT, MatchConfig.builder("caverna").seed(3).build());
        sim.start();
        for (int i = 0; i < 1500; i++) {
            sim.tick();
        }

        SnapshotBuilder.Batch first = SnapshotBuilder.events(sim, 0);
        SnapshotBuilder.Batch second = SnapshotBuilder.events(sim, first.nextSequence());

        assertThat(first.batch().getEventsCount()).isLessThanOrEqualTo(SnapshotBuilder.MAX_EVENTS_PER_BATCH);
        assertThat(first.nextSequence()).isGreaterThan(0);
        assertThat(second.nextSequence()).isGreaterThanOrEqualTo(first.nextSequence());
        SnapshotBuilder.Batch fresh = SnapshotBuilder.events(sim, sim.events().written());
        assertThat(fresh.batch().getEventsCount()).isZero();
    }

    @Test
    void theInputBufferKeepsOneShotSlotChangesUntilTheyAreConsumed() {
        InputBuffer buffer = new InputBuffer();
        assertThat(buffer.drain(0)).isNull();

        buffer.accept(InputFrame.newBuilder().setSequence(1).setSelectSlot(4).setCycleSlot(1).setMoveZ(1).build());
        buffer.accept(InputFrame.newBuilder().setSequence(2).setSelectSlot(-1).setMoveZ(0.5f).build());

        var frame = buffer.drain(2);
        assertThat(frame.combatantId()).isEqualTo(2);
        assertThat(frame.sequence()).isEqualTo(2);
        assertThat(frame.moveZ()).isEqualTo(0.5f);
        assertThat(frame.selectSlot()).as("the earlier selection survives").isEqualTo(4);
        assertThat(frame.cycleSlot()).isEqualTo(1);

        var next = buffer.drain(2);
        assertThat(next.selectSlot()).isEqualTo(-1);
        assertThat(next.cycleSlot()).isZero();
    }

    @Test
    void theSchedulerRunsTheTaskAtTheTickRate() throws InterruptedException {
        AtomicInteger runs = new AtomicInteger();
        try (TickScheduler scheduler = new TickScheduler(runs::incrementAndGet, true, "test-scheduler")) {
            scheduler.start();
            Thread.sleep(500);
        }

        assertThat(runs.get()).as("about 30 ticks in half a second").isBetween(24, 36);
    }

    @Test
    void theFastSchedulerDoesNotSleep() throws InterruptedException {
        AtomicInteger runs = new AtomicInteger();
        try (TickScheduler scheduler = new TickScheduler(runs::incrementAndGet, false, "fast-scheduler")) {
            scheduler.start();
            Thread.sleep(100);
            assertThat(scheduler.meanTickNanos()).isGreaterThanOrEqualTo(0);
        }

        assertThat(runs.get()).isGreaterThan(1_000);
    }

    @Test
    void aFailingTaskDoesNotKillTheScheduler() throws InterruptedException {
        AtomicInteger runs = new AtomicInteger();
        try (TickScheduler scheduler = new TickScheduler(() -> {
            if (runs.incrementAndGet() % 2 == 0) {
                throw new IllegalStateException("boom");
            }
        }, false, "failing-scheduler")) {
            scheduler.start();
            Thread.sleep(100);
        }

        assertThat(runs.get()).isGreaterThan(100);
    }
}
