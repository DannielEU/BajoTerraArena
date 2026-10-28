package com.bajoterra.core.content;

import java.util.List;

/** Thrown when content (JSON files, specs, maps) is malformed or semantically invalid. */
public class ContentException extends RuntimeException {

    private static final long serialVersionUID = 1L;

    public ContentException(String message) {
        super(message);
    }

    public ContentException(String message, Throwable cause) {
        super(message, cause);
    }

    /** Builds an exception whose message lists every problem on its own line. */
    public static ContentException of(List<String> problems) {
        return new ContentException(problems.size() + " content problem(s):\n - " + String.join("\n - ", problems));
    }
}
