#!/usr/bin/env bash
# Store screenshots (M7) from the Windows build, at the sizes the stores ask for:
#   Builds/Store/steam/   1920×1080 landscape (Steam, itch.io)
#   Builds/Store/phone/   1080×1920 portrait (Google Play phone screenshots)
#   Builds/Store/icon-512.png   (Google Play hi-res icon)
#
#   Tools/store-shots.sh              # from Builds/Windows/HumanError.exe (needs a build: Tools/pipeline.sh)
#   DEMO=1 Tools/store-shots.sh       # play as the demo (title shows DEMO)
# Each scene uses the game's debug flags (see GameFlow); saves go to a scratch folder, never the player's.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/Builds/Store"
SHOT="$ROOT/Tools/shot.sh"
mkdir -p "$OUT/steam" "$OUT/phone"
extra=(--he-store)
[ -n "${DEMO:-}" ] && extra+=(--he-demo)
# A fresh save folder: the title shouldn't offer to continue someone's debug run.
export SAVES="$ROOT/Logs/store-saves"
rm -rf "$SAVES"

# name | flags. Seeds are pinned so reruns give the same visitors.
scenes=(
  "01-title|--he-screen title"
  "02-desk|--he-screen desk --he-shift 4 --he-seed 7 --he-ask kesh,palm"
  "03-tools|--he-screen desk --he-shift 12 --he-seed 3 --he-tools uv,gillscope --he-uv --he-scope --he-find SuitFlicker"
  "04-home|--he-home-after 5 --he-seed 11"
  "05-report|--he-screen report --he-shift 6 --he-seed 5"
  "06-rulebook|--he-screen rulebook --he-shift 9 --he-seed 2"
  "07-act|--he-screen act --he-act 2"
)

for entry in "${scenes[@]}"; do
  name="${entry%%|*}"; flags="${entry#*|}"
  # shellcheck disable=SC2086
  FRAMES=90 "$SHOT" "$OUT/steam/$name.png" $flags "${extra[@]}" > /dev/null
  # shellcheck disable=SC2086
  PHONE=1080x1920 FRAMES=90 "$SHOT" "$OUT/phone/$name.png" $flags "${extra[@]}" > /dev/null
  echo "  $name"
done

# Exact store sizes (the phone capture is supersampled from a window limited by the monitor's height).
python - "$OUT" "$ROOT/Assets/_Game/Art/Generated/Icon/icon.png" <<'EOF'
import sys, os
from PIL import Image
out, icon = sys.argv[1], sys.argv[2]
for folder, size in (("steam", (1920, 1080)), ("phone", (1080, 1920))):
    d = os.path.join(out, folder)
    for f in sorted(os.listdir(d)):
        p = os.path.join(d, f)
        im = Image.open(p).convert("RGB")
        if im.size != size:
            im = im.resize(size, Image.LANCZOS)
        im.save(p)
Image.open(icon).convert("RGBA").resize((512, 512), Image.LANCZOS).save(os.path.join(out, "icon-512.png"))
print("sizes ok")
EOF
echo "Store screenshots → $OUT"
