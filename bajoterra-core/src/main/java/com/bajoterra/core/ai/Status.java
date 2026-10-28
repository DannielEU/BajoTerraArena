package com.bajoterra.core.ai;

/** Result of ticking a behavior-tree node. Decisions are instantaneous, so there is no "running" state. */
public enum Status {
    SUCCESS,
    FAILURE
}
