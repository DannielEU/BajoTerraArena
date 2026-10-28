package com.bajoterra.core.ai;

/** A node of a bot's behavior tree. Leaves are lambdas over the {@link BotContext}; composites own children. */
@FunctionalInterface
public interface BtNode {

    Status tick(BotContext context);
}
