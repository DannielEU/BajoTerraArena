package com.bajoterra.core.ai;

/** Composite node: ticks its children in order and succeeds with the first one that succeeds. */
public final class Selector implements BtNode {

    private final BtNode[] children;

    public Selector(BtNode... children) {
        this.children = children.clone();
    }

    @Override
    public Status tick(BotContext context) {
        for (BtNode child : children) {
            if (child.tick(context) == Status.SUCCESS) {
                return Status.SUCCESS;
            }
        }
        return Status.FAILURE;
    }
}
