#!/usr/bin/env bash
# Build the Android APK headlessly and (by default) install + launch it on the attached device.
#
#   Tools/build-android.sh              build, install, launch, then tail the game's logcat
#   Tools/build-android.sh --no-install build only
#
# Uses the SDK/NDK/JDK bundled with the editor's Android module. Unity must not have the
# project open elsewhere. The APK lands in Builds/Android/RichCoast.apk; the editor log in
# Logs/build-android.log.
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=Tools/unity-path.sh
source "$PROJECT_DIR/Tools/unity-path.sh"
resolve_unity_path "$PROJECT_DIR"

INSTALL=1
for arg in "$@"; do
  case "$arg" in
    --no-install) INSTALL=0 ;;
    *) echo "unknown arg: $arg" >&2; exit 2 ;;
  esac
done

APK="$PROJECT_DIR/Builds/Android/RichCoast.apk"
LOG="$PROJECT_DIR/Logs/build-android.log"
BUNDLE_ID="com.richcoast.game"
ADB="$(dirname "$UNITY_PATH")/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
[[ -x "$ADB" ]] || ADB="$(command -v adb || true)"

mkdir -p "$PROJECT_DIR/Logs"
rm -f "$APK"

set +e
"$UNITY_PATH" \
  -batchmode -quit -nographics \
  -buildTarget Android \
  -projectPath "$(native_path "$PROJECT_DIR")" \
  -executeMethod RichCoast.EditorTools.ProjectSetup.BuildAndroid \
  -logFile "$(native_path "$LOG")"
status=$?
set -e

if [[ $status -ne 0 || ! -f "$APK" ]]; then
  echo "build failed (exit $status) — errors from $LOG:" >&2
  grep -n -i "error CS\|BuildFailedException\|\[ProjectSetup\]\|Error:" "$LOG" | tail -n 40 >&2
  exit 1
fi
echo "built $APK ($(du -h "$APK" | cut -f1))"

[[ $INSTALL -eq 1 ]] || exit 0
[[ -n "$ADB" ]] || { echo "adb not found; install skipped" >&2; exit 1; }

"$ADB" install -r "$(native_path "$APK")"
"$ADB" shell am start -n "$BUNDLE_ID/com.unity3d.player.UnityPlayerActivity"
echo "launched $BUNDLE_ID — tailing Unity logcat (Ctrl+C to stop)"
"$ADB" logcat -c
"$ADB" logcat -s Unity
