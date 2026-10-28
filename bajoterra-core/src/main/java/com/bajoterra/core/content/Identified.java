package com.bajoterra.core.content;

/** A content definition addressable by a stable string id and a dense index (used by snapshots and tables). */
public interface Identified {

    /** Stable, human-readable id as written in the content files. */
    String id();

    /** Dense index in the owning {@link Registry}; assigned in id order so it is deterministic. */
    int index();
}
