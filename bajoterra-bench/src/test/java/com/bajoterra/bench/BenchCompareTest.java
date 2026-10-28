package com.bajoterra.bench;

import static org.assertj.core.api.Assertions.assertThat;

import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;
import org.junit.jupiter.api.Test;

class BenchCompareTest {

    private static final ObjectMapper MAPPER = new ObjectMapper();

    private static String result(String name, String param, double score, double allocBytes) {
        return "{\"benchmark\":\"" + name + "\",\"params\":{\"n\":\"" + param + "\"},"
                + "\"primaryMetric\":{\"score\":" + score + ",\"scoreUnit\":\"us/op\"},"
                + "\"secondaryMetrics\":{\"gc.alloc.rate.norm\":{\"score\":" + allocBytes + "}}}";
    }

    private static com.fasterxml.jackson.databind.JsonNode json(String... results) throws IOException {
        return MAPPER.readTree("[" + String.join(",", results) + "]");
    }

    @Test
    void theCommittedBaselineMeetsThePerformanceBudget() throws IOException {
        var baseline = MAPPER.readTree(new java.io.File("baseline/baseline.json"));

        assertThat(baseline.size()).isGreaterThan(20);
        assertThat(BenchCompare.compare(baseline, baseline, 0.25)).isEmpty();
        int ticks = 0;
        for (var result : baseline) {
            if (result.path("benchmark").asText().endsWith("TickBenchmark.tick")) {
                ticks++;
                // Budget (plan): a whole tick well under 250 us and no garbage.
                assertThat(result.path("primaryMetric").path("score").asDouble()).as("tick in us").isLessThan(250);
                assertThat(result.path("secondaryMetrics").path("gc.alloc.rate.norm").path("score").asDouble())
                        .as("bytes allocated per tick").isLessThan(1.0);
            }
            if (result.path("benchmark").asText().endsWith("NetworkBenchmark.encodeFrame")) {
                assertThat(result.path("primaryMetric").path("score").asDouble()).as("encode in us").isLessThan(50);
            }
            if (result.path("benchmark").asText().endsWith("BotBenchmark.decide")) {
                assertThat(result.path("primaryMetric").path("score").asDouble()).as("decision in us").isLessThan(100);
            }
        }
        assertThat(ticks).isEqualTo(3);
    }

    @Test
    void identicalRunsHaveNoRegressions() throws IOException {
        var run = json(result("tick", "0", 5.0, 0));

        assertThat(BenchCompare.compare(run, run, 0.25)).isEmpty();
    }

    @Test
    void improvementsAndSmallChangesPass() throws IOException {
        var baseline = json(result("tick", "0", 5.0, 0), result("tick", "64", 9.0, 0));
        var current = json(result("tick", "0", 3.0, 0), result("tick", "64", 10.0, 0));

        assertThat(BenchCompare.compare(baseline, current, 0.25)).isEmpty();
    }

    @Test
    void aSlowdownBeyondTheThresholdIsReportedWithItsParameters() throws IOException {
        var baseline = json(result("tick", "64", 9.0, 0));
        var current = json(result("tick", "64", 13.0, 0));

        var regressions = BenchCompare.compare(baseline, current, 0.25);

        assertThat(regressions).hasSize(1);
        assertThat(regressions.get(0)).contains("tick n=64").contains("9.000 -> 13.000").contains("+44 %");
    }

    @Test
    void startingToAllocateIsARegressionEvenIfTheTimeIsTheSame() throws IOException {
        var baseline = json(result("tick", "0", 5.0, 0));
        var current = json(result("tick", "0", 5.0, 48));

        assertThat(BenchCompare.compare(baseline, current, 0.25)).singleElement().asString()
                .contains("started allocating");
    }

    @Test
    void benchmarksMissingFromTheBaselineAreIgnored() throws IOException {
        var baseline = json(result("old", "0", 5.0, 0));
        var current = json(result("brand-new", "0", 500.0, 0));

        assertThat(BenchCompare.compare(baseline, current, 0.25)).isEmpty();
    }
}
