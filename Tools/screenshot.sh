#!/usr/bin/env bash
# Render the running game to Logs/game-scene.png.
#
# Runs the explicit ScreenshotCapture PlayMode test with graphics enabled — a -nographics run
# has no framebuffer to capture. Unity must not have the project open elsewhere.
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=Tools/unity-path.sh
source "$PROJECT_DIR/Tools/unity-path.sh"
resolve_unity_path "$PROJECT_DIR"

OUT="$PROJECT_DIR/Logs/game-scene.png"
LOG="$PROJECT_DIR/Logs/screenshot.log"
mkdir -p "$PROJECT_DIR/Logs"
rm -f "$OUT"

set +e
"$UNITY_PATH" \
  -batchmode \
  -projectPath "$(native_path "$PROJECT_DIR")" \
  -runTests -testPlatform PlayMode \
  -testFilter "RichCoast.Tests.PlayMode.ScreenshotCapture.CaptureGameScene" \
  -testResults "$(native_path "$PROJECT_DIR/Logs/screenshot-results.xml")" \
  -logFile "$(native_path "$LOG")"
set -e

if [[ -f "$OUT" ]]; then
  echo "wrote $OUT"
else
  echo "no screenshot produced — last 40 log lines:" >&2
  tail -n 40 "$LOG" >&2
  exit 1
fi
