#!/usr/bin/env bash
# Runs the JMH suite with the allocation profiler and compares it with the committed baseline.
#
#   bajoterra-bench/scripts/run-benchmarks.sh            # quick run + regression check
#   bajoterra-bench/scripts/run-benchmarks.sh --update   # also overwrite the baseline with this run
#
# Needs JDK 27 and Maven on the PATH. Takes a few minutes.
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$root/bajoterra-bench/target/jmh-result.json"
baseline="$root/bajoterra-bench/baseline/baseline.json"

(cd "$root" && mvn -B -q -DskipTests -Dcheckstyle.skip -Dspotbugs.skip package -pl bajoterra-bench -am)

java -jar "$root/bajoterra-bench/target/benchmarks.jar" \
  -prof gc -wi 2 -w 1 -i 3 -r 1 -f 1 -rf json -rff "$out" \
  'com.bajoterra.bench\..*Benchmark\..*'

if [[ "${1:-}" == "--update" ]]; then
  mkdir -p "$(dirname "$baseline")"
  cp "$out" "$baseline"
  echo "Baseline updated: $baseline"
  exit 0
fi

java -cp "$root/bajoterra-bench/target/benchmarks.jar" com.bajoterra.bench.BenchCompare "$baseline" "$out" 25
