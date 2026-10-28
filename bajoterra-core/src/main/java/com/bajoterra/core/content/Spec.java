package com.bajoterra.core.content;

import com.bajoterra.core.sim.Ticks;
import com.fasterxml.jackson.databind.JsonNode;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * Typed, validating view over a JSON object. Every accessor reports problems as a {@link ContentException} whose
 * message names the file/field path, so content authors get actionable errors.
 */
public final class Spec {

    private final JsonNode node;
    private final String path;

    public Spec(JsonNode node, String path) {
        this.node = node;
        this.path = path;
    }

    public String path() {
        return path;
    }

    /** Names of the fields of this object, in file order. */
    public List<String> fieldNames() {
        List<String> names = new ArrayList<>();
        node.fieldNames().forEachRemaining(names::add);
        return names;
    }

    public boolean has(String field) {
        JsonNode value = node.get(field);
        return value != null && !value.isNull();
    }

    /** Creates an exception that points at a field of this spec. */
    public ContentException fail(String field, String message) {
        return new ContentException(path + (field.isEmpty() ? "" : "." + field) + ": " + message);
    }

    public String requireString(String field) {
        JsonNode value = node.get(field);
        if (value == null || !value.isTextual() || value.asText().isBlank()) {
            throw fail(field, "required non-empty string");
        }
        return value.asText();
    }

    public String optString(String field, String fallback) {
        return has(field) ? requireString(field) : fallback;
    }

    public double requireDouble(String field, double min, double max) {
        JsonNode value = node.get(field);
        if (value == null || !value.isNumber()) {
            throw fail(field, "required number in [" + min + ", " + max + "]");
        }
        return checkRange(field, value.asDouble(), min, max);
    }

    public double optDouble(String field, double fallback, double min, double max) {
        return has(field) ? requireDouble(field, min, max) : fallback;
    }

    public int requireInt(String field, int min, int max) {
        JsonNode value = node.get(field);
        if (value == null || !value.isIntegralNumber()) {
            throw fail(field, "required integer in [" + min + ", " + max + "]");
        }
        return (int) checkRange(field, value.asLong(), min, max);
    }

    public int optInt(String field, int fallback, int min, int max) {
        return has(field) ? requireInt(field, min, max) : fallback;
    }

    /**
     * Reads a duration expressed either as {@code durationTicks} or as {@code durationSeconds}.
     *
     * @return the duration in ticks, at least 1
     */
    public int requireDurationTicks() {
        if (has("durationTicks")) {
            return requireInt("durationTicks", 1, 100_000);
        }
        return Math.max(1, Ticks.fromSeconds(requireDouble("durationSeconds", 0.0167, 1000)));
    }

    public boolean optBoolean(String field, boolean fallback) {
        if (!has(field)) {
            return fallback;
        }
        JsonNode value = node.get(field);
        if (!value.isBoolean()) {
            throw fail(field, "expected true or false");
        }
        return value.asBoolean();
    }

    public <E extends Enum<E>> E requireEnum(String field, Class<E> type) {
        String text = requireString(field);
        try {
            return Enum.valueOf(type, text.trim().toUpperCase(Locale.ROOT));
        } catch (IllegalArgumentException e) {
            throw fail(field, "'" + text + "' is not one of " + List.of(type.getEnumConstants()));
        }
    }

    public <E extends Enum<E>> E optEnum(String field, Class<E> type, E fallback) {
        return has(field) ? requireEnum(field, type) : fallback;
    }

    /** Returns the nested object as a spec, or throws if absent or not an object. */
    public Spec requireObject(String field) {
        JsonNode value = node.get(field);
        if (value == null || !value.isObject()) {
            throw fail(field, "required object");
        }
        return new Spec(value, path + "." + field);
    }

    /** Returns the nested object as a spec, or null if the field is absent. */
    public Spec optObject(String field) {
        return has(field) ? requireObject(field) : null;
    }

    /** Returns the elements of an array of objects; an absent field yields an empty list. */
    public List<Spec> list(String field) {
        List<Spec> result = new ArrayList<>();
        if (!has(field)) {
            return result;
        }
        JsonNode array = node.get(field);
        if (!array.isArray()) {
            throw fail(field, "expected an array");
        }
        for (int i = 0; i < array.size(); i++) {
            JsonNode element = array.get(i);
            if (!element.isObject()) {
                throw fail(field + "[" + i + "]", "expected an object");
            }
            result.add(new Spec(element, path + "." + field + "[" + i + "]"));
        }
        return result;
    }

    /** Returns an array of strings; an absent field yields an empty list. */
    public List<String> stringList(String field) {
        List<String> result = new ArrayList<>();
        if (!has(field)) {
            return result;
        }
        JsonNode array = node.get(field);
        if (!array.isArray()) {
            throw fail(field, "expected an array of strings");
        }
        for (int i = 0; i < array.size(); i++) {
            if (!array.get(i).isTextual()) {
                throw fail(field + "[" + i + "]", "expected a string");
            }
            result.add(array.get(i).asText());
        }
        return result;
    }

    /** Returns a fixed-length array of numbers. */
    public double[] requireVector(String field, int length) {
        JsonNode array = node.get(field);
        if (array == null || !array.isArray() || array.size() != length) {
            throw fail(field, "required array of " + length + " numbers");
        }
        double[] values = new double[length];
        for (int i = 0; i < length; i++) {
            if (!array.get(i).isNumber()) {
                throw fail(field + "[" + i + "]", "expected a number");
            }
            values[i] = array.get(i).asDouble();
        }
        return values;
    }

    private double checkRange(String field, double value, double min, double max) {
        if (value < min || value > max) {
            throw fail(field, value + " is outside [" + min + ", " + max + "]");
        }
        return value;
    }
}
