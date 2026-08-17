#!/usr/bin/env bash
# Render the running game to Logs/game-scene.png.
#
# Runs the explicit ScreenshotCapture PlayMode test with graphics enabled — a -nographics run has
# no framebuffer to capture. Unity must not have the project open elsewhere.
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ -z "${UNITY_PATH:-}" ]]; then
  VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
  UNITY_PATH="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
fi

OUT="$PROJECT_DIR/Logs/game-scene.png"
rm -f "$OUT"

set +e
"$UNITY_PATH" \
  -batchmode \
  -projectPath "$PROJECT_DIR" \
  -runTests -testPlatform PlayMode \
  -testFilter "RichCoast.Tests.PlayMode.ScreenshotCapture.CaptureGameScene" \
  -testResults "$PROJECT_DIR/Logs/screenshot-results.xml" \
  -logFile - > "$PROJECT_DIR/Logs/screenshot.log" 2>&1
set -e

if [[ -f "$OUT" ]]; then
  echo "wrote $OUT"
else
  echo "no screenshot produced — last 40 log lines:" >&2
  tail -n 40 "$PROJECT_DIR/Logs/screenshot.log" >&2
  exit 1
fi
