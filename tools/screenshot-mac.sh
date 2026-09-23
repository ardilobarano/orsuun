#!/usr/bin/env bash
# Mac counterpart of screenshot.ps1: runs the Mac player in a phone-shaped window, lets it save its own screen, quits.
#   tools/screenshot-mac.sh artifacts/lane.png            (local session, lane)
#   tools/screenshot-mac.sh artifacts/forge.png -forge     (Forge screen)
# Build the player first: Unity -batchmode -quit -projectPath client -executeMethod Orsuun.Client.EditorTools.ProjectSetup.BuildMac
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$1"; shift
case "$OUT" in /*) ;; *) OUT="$ROOT/$OUT" ;; esac
rm -f "$OUT"
"$ROOT/client/Builds/Mac/Orsuun.app/Contents/MacOS/"* -screen-fullscreen 0 -screen-width 540 -screen-height 960 -local -shot "$OUT" -shotAfter "${SHOT_AFTER:-8}" "$@" >/dev/null 2>&1 &
PID=$!
for _ in $(seq 1 60); do [ -f "$OUT" ] && break; sleep 1; done
sleep 1; kill "$PID" 2>/dev/null || true
[ -f "$OUT" ] && echo "saved $OUT" || { echo "no screenshot"; exit 1; }
