#!/usr/bin/env bash
# Screenshot the Windows build with debug flags (GameFlow lists them). Saves never touch the player's folder.
#
#   Tools/shot.sh <out.png> [game flags...]        e.g. Tools/shot.sh Logs/shots/desk.png --he-screen desk --he-shift 4
#   SIZE=1080x1920 Tools/shot.sh ...               portrait (phone layout)
#   PHONE=1080x2340 Tools/shot.sh ...             portrait phone in a 1080-tall window (608x1080 by default), captured at 2×
#   FRAMES=90 Tools/shot.sh ...                    wait longer before capturing (animations)
#   EXE=Builds/WindowsDemo/HumanErrorDemo.exe ...  another build (the demo)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
out="$1"; shift
size="${SIZE:-1920x1080}"
extra=()
if [ -n "${PHONE:-}" ]; then
  pw="${PHONE%x*}"; ph="${PHONE#*x}"
  size="${SIZE:-$(( 1080 * pw / ph ))x1080}"
  extra=(--he-phone "$PHONE" --screenshot-scale "${SCALE:-2}")
fi
w="${size%x*}"; h="${size#*x}"
exe="${EXE:-$ROOT/Builds/Windows/HumanError.exe}"
saves="${SAVES:-$ROOT/Logs/shot-saves}"
mkdir -p "$(dirname "$out")" "$saves"
"$exe" -screen-fullscreen 0 -screen-width "$w" -screen-height "$h" -logFile "$ROOT/Logs/shot.log" \
  --screenshot "$(cygpath -w "$(realpath -m "$out")")" --screenshot-frames "${FRAMES:-60}" \
  --he-save-dir "$(cygpath -w "$saves")" "${extra[@]}" "$@" > /dev/null
echo "→ $out"
