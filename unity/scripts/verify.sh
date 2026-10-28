#!/usr/bin/env bash
# Verifies the Unity client without the Unity editor:
#   1. the committed Game.cs is the one generated from game.proto,
#   2. the shaded Java server jar is built,
#   3. unit tests + integration tests (the C# client against the real Java server) pass,
#   4. the Unity-facing code compiles against the UnityEngine reference assemblies, with both input backends.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
export JAVA_HOME="${JAVA_HOME:-/opt/homebrew/opt/openjdk/libexec/openjdk.jdk/Contents/Home}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export BAJOTERRA_REQUIRE_SERVER=1

echo "== 1/4 protocol: committed Game.cs is current"
"$root/unity/scripts/sync-protocol.sh" --check

echo "== 2/4 server jar"
(cd "$root" && mvn -q -B -DskipTests -Djacoco.skip=true -Dcheckstyle.skip=true -Dspotbugs.skip=true package)

echo "== 3/4 client tests (unit + integration against the Java server)"
dotnet test "$root/unity/dotnet/Bajoterra.Client.Tests" -v q

echo "== 4/4 compile check of the Unity layer (Input System, then legacy Input Manager)"
dotnet build "$root/unity/dotnet/Bajoterra.Unity.Check" -v q --nologo --no-incremental
dotnet build "$root/unity/dotnet/Bajoterra.Unity.Check" -v q --nologo --no-incremental -p:InputBackend=legacy

echo "Unity client verification passed."
