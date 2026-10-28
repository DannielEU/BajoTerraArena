package com.bajoterra.core.move;

import com.bajoterra.core.content.Spec;

/**
 * Creates a {@link MoveBehavior} from the {@code behavior} object of a move file. Implementations register
 * themselves with {@code @AutoService(MoveBehaviorFactory.class)}; no existing class needs to change.
 */
public interface MoveBehaviorFactory {

    /** Value of the {@code type} field this factory handles. */
    String type();

    MoveBehavior create(Spec spec);
}
