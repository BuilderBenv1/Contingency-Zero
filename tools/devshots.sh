#!/bin/bash
# Takes the desert acceptance screenshots (before/after) for the terrain-defilade brief.
# Usage: bash tools/devshots.sh <tag>   e.g. bash tools/devshots.sh before
# Writes .shots/<tag>_<scene>_z<zoom>_r<retro>/shot_0.png
set -u
GODOT="/c/Users/theka/AppData/Local/Microsoft/WinGet/Packages/GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe/Godot_v4.3-stable_mono_win64/Godot_v4.3-stable_mono_win64_console.exe"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TAG="$1"
cd "$ROOT"
for scene in base battle; do
  for zoom in 70 110; do
    for retro in 1 0; do
      out="$ROOT/.shots/${TAG}_${scene}_z${zoom}_r${retro}"
      mkdir -p "$out"
      look=""
      if [ "$scene" = "battle" ]; then look="--dev-look=battle"; fi
      echo "=== $TAG $scene zoom=$zoom retro=$retro"
      "$GODOT" --path game -- --dev-skirmish=tablelands --dev-self-ai=1 --dev-reveal \
        --dev-fastforward=600 $look --dev-zoom=$zoom --dev-retro=$retro \
        --dev-shots="$(cygpath -w "$out")" --dev-shot-times=8 --dev-seconds=12 2>&1 | grep -E "DEV:|ERROR|SCRIPT" | tail -5
    done
  done
done
echo "ALL DONE"
