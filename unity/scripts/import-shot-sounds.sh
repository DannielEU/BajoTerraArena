#!/usr/bin/env bash
# Turns the recorded blaster shots in sound/ into the clips the game plays (Assets/Resources/Sounds/Shots).
# Each one is trimmed of leading and trailing silence (so it starts on the trigger), mixed to mono (shots are
# positioned in 3D) and levelled to the same loudness, so no shot is louder than the others. Needs ffmpeg.
#
#   import-shot-sounds.sh   convert every sound/*.mp3; run it again after adding or replacing a shot
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
target="$root/unity/BajoTerraClient/Assets/Resources/Sounds/Shots"
loudness=-16 # LUFS

trim="aformat=channel_layouts=mono,silenceremove=start_periods=1:start_threshold=-60dB,areverse"
trim="$trim,silenceremove=start_periods=1:start_threshold=-60dB,areverse"

mkdir -p "$target"
for input in "$root"/sound/*.mp3; do
    name="$(basename "${input%.*}")"
    measured="$(ffmpeg -hide_banner -nostats -i "$input" -af "$trim,ebur128=framelog=quiet" -f null - 2>&1 \
        | awk '/Integrated loudness/ { getline; print $2 }')"
    gain="$(awk -v target="$loudness" -v measured="$measured" 'BEGIN { printf "%.1f", target - measured }')"
    ffmpeg -hide_banner -loglevel error -y -i "$input" -af "$trim,volume=${gain}dB" -ar 44100 -c:a pcm_s16le \
        "$target/$name.wav"
    echo "$name.wav  (${measured} LUFS, ${gain} dB)"
done
