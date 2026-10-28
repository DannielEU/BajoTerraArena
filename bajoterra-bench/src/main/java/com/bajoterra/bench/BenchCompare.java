package com.bajoterra.bench;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

/**
 * Performance regression gate: compares two JMH JSON result files and fails when a benchmark got slower by more than
 * a threshold, or when something that used to be allocation-free started to allocate.
 *
 * <p>Usage: {@code BenchCompare <baseline.json> <current.json> [thresholdPercent]} (default 25 %).
 */
public final class BenchCompare {

    /** Differences smaller than this many nanoseconds-or-microseconds-equivalent units are measurement noise. */
    private static final double ABSOLUTE_NOISE = 0.002;
    /** Allocation below this many bytes per operation counts as "none". */
    private static final double ALLOCATION_NOISE = 1.0;
    /** Name of the allocation metric in JMH's JSON output (the console shows it with a leading middle dot). */
    private static final String ALLOCATION_METRIC = "gc.alloc.rate.norm";

    private BenchCompare() {
        throw new AssertionError("Utility class, not instantiable");
    }

    public static void main(String[] args) throws IOException {
        if (args.length < 2) {
            System.err.println("Usage: BenchCompare <baseline.json> <current.json> [thresholdPercent]");
            System.exit(2);
        }
        double threshold = (args.length > 2 ? Double.parseDouble(args[2]) : 25.0) / 100.0;
        ObjectMapper mapper = new ObjectMapper();
        List<String> regressions = compare(mapper.readTree(new File(args[0])), mapper.readTree(new File(args[1])),
                threshold);
        if (regressions.isEmpty()) {
            System.out.println("No performance regressions (threshold " + (int) (threshold * 100) + " %).");
        } else {
            regressions.forEach(line -> System.out.println("REGRESSION: " + line));
            System.exit(1);
        }
    }

    /** Returns one line per regression; empty when the current run is as good as the baseline. */
    public static List<String> compare(JsonNode baseline, JsonNode current, double threshold) {
        Map<String, JsonNode> before = index(baseline);
        List<String> regressions = new ArrayList<>();
        for (Map.Entry<String, JsonNode> entry : index(current).entrySet()) {
            JsonNode old = before.get(entry.getKey());
            if (old == null) {
                continue;
            }
            double oldScore = old.path("primaryMetric").path("score").asDouble();
            double newScore = entry.getValue().path("primaryMetric").path("score").asDouble();
            String unit = entry.getValue().path("primaryMetric").path("scoreUnit").asText();
            if (newScore > oldScore * (1 + threshold) && newScore - oldScore > ABSOLUTE_NOISE) {
                regressions.add(String.format(Locale.ROOT, "%s: %.3f -> %.3f %s (+%.0f %%)", entry.getKey(), oldScore,
                        newScore, unit, 100 * (newScore / oldScore - 1)));
            }
            double oldAlloc = old.path("secondaryMetrics").path(ALLOCATION_METRIC).path("score").asDouble(0);
            double newAlloc = entry.getValue().path("secondaryMetrics").path(ALLOCATION_METRIC).path("score")
                    .asDouble(0);
            if (oldAlloc < ALLOCATION_NOISE && newAlloc >= ALLOCATION_NOISE) {
                regressions.add(String.format(Locale.ROOT, "%s: started allocating (%.1f B/op, was none)",
                        entry.getKey(), newAlloc));
            }
        }
        return regressions;
    }

    private static Map<String, JsonNode> index(JsonNode results) {
        Map<String, JsonNode> map = new LinkedHashMap<>();
        for (JsonNode result : results) {
            StringBuilder key = new StringBuilder(result.path("benchmark").asText());
            JsonNode params = result.path("params");
            params.fieldNames().forEachRemaining(name -> key.append(' ').append(name).append('=')
                    .append(params.path(name).asText()));
            map.put(key.toString(), result);
        }
        return map;
    }
}
