#!/usr/bin/env bash
# Copies the C# classes generated from game.proto into the Unity project.
#
#   sync-protocol.sh           regenerate (Maven) and copy Game.cs
#   sync-protocol.sh --check   fail if the committed copy differs from the freshly generated one
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
generated="$root/bajoterra-protocol/target/generated-sources/csharp/Game.cs"
target="$root/unity/BajoTerraClient/Assets/Scripts/Client/Generated/Game.cs"

export JAVA_HOME="${JAVA_HOME:-/opt/homebrew/opt/openjdk/libexec/openjdk.jdk/Contents/Home}"
(cd "$root" && mvn -q -B -pl bajoterra-protocol -am -DskipTests -Djacoco.skip=true package)

if [[ "${1:-}" == "--check" ]]; then
    if ! diff -q "$generated" "$target" > /dev/null; then
        echo "Game.cs is out of date. Run unity/scripts/sync-protocol.sh and commit the result." >&2
        exit 1
    fi
    echo "Game.cs is up to date."
else
    mkdir -p "$(dirname "$target")"
    cp "$generated" "$target"
    echo "Copied $generated"
fi
