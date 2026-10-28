package com.bajoterra.core.event;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.Test;

class EventBufferTest {

    @Test
    void eventsAreReadBackBySequenceNumber() {
        EventBuffer buffer = new EventBuffer(8);
        GameEvent event = buffer.begin(EventType.DAMAGE, 12);
        event.actor = 3;
        event.value = 9.5;

        GameEvent read = buffer.at(0);

        assertThat(read.type).isEqualTo(EventType.DAMAGE);
        assertThat(read.tick).isEqualTo(12);
        assertThat(read.actor).isEqualTo(3);
        assertThat(read.value).isEqualTo(9.5);
        assertThat(buffer.written()).isEqualTo(1);
    }

    @Test
    void theOldestEventsAreOverwrittenOnceTheRingIsFull() {
        EventBuffer buffer = new EventBuffer(4);
        for (int i = 0; i < 10; i++) {
            buffer.begin(EventType.SHOT, i).a = i;
        }

        assertThat(buffer.oldestAvailable()).isEqualTo(6);
        assertThat(buffer.at(5)).isNull();
        assertThat(buffer.at(6).a).isEqualTo(6);
        assertThat(buffer.at(9).a).isEqualTo(9);
        assertThat(buffer.at(10)).isNull();
    }

    @Test
    void reusedSlotsStartClean() {
        EventBuffer buffer = new EventBuffer(2);
        GameEvent first = buffer.begin(EventType.HEAL, 1);
        first.actor = 5;
        first.x = 9;
        buffer.begin(EventType.HEAL, 2);

        GameEvent reused = buffer.begin(EventType.SHOT, 3);

        assertThat(reused.actor).isEqualTo(-1);
        assertThat(reused.x).isZero();
        assertThat(reused.sequence).isEqualTo(2);
    }

    @Test
    void capacityMustBeAPowerOfTwo() {
        assertThatThrownBy(() -> new EventBuffer(100)).isInstanceOf(IllegalArgumentException.class);
        assertThatThrownBy(() -> new EventBuffer(0)).isInstanceOf(IllegalArgumentException.class);
    }
}
