package com.bajoterra.server;

import java.util.List;

/** Server-side state of one connected client. Only touched on the simulation thread. */
final class Session {

    final int connectionId;
    String name = "player";
    boolean greeted;
    Room room;
    int team = -1;
    String characterId = "eli";
    List<String> slugIds = List.of();
    boolean ready;
    int combatantId = -1;
    final InputBuffer input = new InputBuffer();

    Session(int connectionId) {
        this.connectionId = connectionId;
    }
}
