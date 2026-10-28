package com.bajoterra.core.ai;

/** Composite node: ticks its children in order and fails with the first one that fails. */
public final class Sequence implements BtNode {

    private final BtNode[] children;

    public Sequence(BtNode... children) {
        this.children = children.clone();
    }

    @Override
    public Status tick(BotContext context) {
        for (BtNode child : children) {
            if (child.tick(context) == Status.FAILURE) {
                return Status.FAILURE;
            }
        }
        return Status.SUCCESS;
    }
}
