#!/usr/bin/env bash
# Runs the Unity-side tests inside the real editor, using the editor version pinned in ProjectVersion.txt.
#
#   unity/scripts/unity-test.sh [editmode|playmode|all] [--screenshots DIR]
#
# EditMode runs headless. PlayMode boots the game, plays a local match against bots and needs a graphics device
# (so no -nographics); --screenshots also writes one PNG per stage into DIR. Close the project in the editor first:
# Unity refuses to open a project that is already open. UNITY_TEST_TIMEOUT (seconds, default 600) bounds each run.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
project="$root/unity/BajoTerraClient"
version="$(sed -n 's/^m_EditorVersion: //p' "$project/ProjectSettings/ProjectVersion.txt")"
editor="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity}"
export JAVA_HOME="${JAVA_HOME:-/opt/homebrew/opt/openjdk/libexec/openjdk.jdk/Contents/Home}"
timeout="${UNITY_TEST_TIMEOUT:-600}"

mode="all"
if [[ $# -gt 0 && "$1" != --* ]]; then
  mode="$1"
  shift
fi
while [[ $# -gt 0 ]]; do
  case "$1" in
    --screenshots) export BAJOTERRA_SCREENSHOTS="$2"; shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 64 ;;
  esac
done

if [[ ! -x "$editor" ]]; then
  echo "Unity $version not found at: $editor" >&2
  echo "Install it from Unity Hub, or point UNITY_EDITOR at the editor binary." >&2
  exit 1
fi

mkdir -p "$project/Logs"
status=0

run() {
  local platform="$1"
  shift
  local results="$project/Logs/$platform-results.xml"
  local log="$project/Logs/$platform.log"
  echo "== Unity $version: $platform tests (log: $log)"
  rm -f "$results"
  "$editor" -batchmode "$@" -projectPath "$project" -runTests -testPlatform "$platform" \
    -testResults "$results" -logFile "$log" &
  local pid=$!
  # A hung test must not hang the script: stop the editor after UNITY_TEST_TIMEOUT seconds.
  ( sleep "$timeout"; echo "watchdog: stopping Unity after ${timeout}s" >&2; kill "$pid" 2>/dev/null ) &
  local watchdog=$!
  disown "$watchdog"
  wait "$pid" || status=$?
  kill "$watchdog" 2>/dev/null || true
  if [[ -f "$results" ]]; then
    sed -n '2p' "$results" | grep -o 'result="[^"]*" total="[^"]*" passed="[^"]*" failed="[^"]*" inconclusive="[^"]*" skipped="[^"]*"' || true
  else
    echo "no results file: see $log" >&2
    status=1
  fi
}

case "$mode" in
  editmode) run EditMode -nographics ;;
  playmode) run PlayMode ;;
  all) run EditMode -nographics; run PlayMode ;;
  *) echo "mode must be editmode, playmode or all" >&2; exit 64 ;;
esac
exit "$status"
