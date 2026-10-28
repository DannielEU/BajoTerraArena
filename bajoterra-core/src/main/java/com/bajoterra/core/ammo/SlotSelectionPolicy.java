package com.bajoterra.core.ammo;

/** Decides which slot is selected right after a shot (Strategy). */
public interface SlotSelectionPolicy {

    /** Called after {@code loadout} fired {@code firedSlot}; may change {@link Loadout#selected()}. */
    void afterFire(Loadout loadout, int firedSlot);

    /** Keeps the fired slot selected if it is still usable, otherwise moves on to the next available slot. */
    SlotSelectionPolicy MANUAL = (loadout, firedSlot) -> {
        if (!loadout.isAvailable(loadout.selected())) {
            loadout.cycle(1);
        }
    };

    /** Always advances to the next available slot, like the rotating chamber of a blaster. */
    SlotSelectionPolicy ROUND_ROBIN = (loadout, firedSlot) -> loadout.cycle(1);
}
