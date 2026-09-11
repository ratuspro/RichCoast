#!/usr/bin/env bash
# One-shot headless project setup: resolves packages, wires URP 2D as the default pipeline,
# sets portrait-only + Android player settings (see Assets/Editor/ProjectSetup.cs).
# Safe to re-run. Unity must not have the project open elsewhere.
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=Tools/unity-path.sh
source "$PROJECT_DIR/Tools/unity-path.sh"
resolve_unity_path "$PROJECT_DIR"

LOG="$PROJECT_DIR/Logs/setup.log"
mkdir -p "$PROJECT_DIR/Logs"

# TMP's essential resources (default font/settings/shaders) can't be imported in a -quit run;
# unpack them directly. Needs a resolved Library/PackageCache (or the editor's built-in copy).
python "$PROJECT_DIR/Tools/import-tmp-essentials.py" || true

echo "Applying project setup with $UNITY_PATH (log: $LOG)"
"$UNITY_PATH" \
  -batchmode -nographics -quit \
  -projectPath "$(native_path "$PROJECT_DIR")" \
  -executeMethod RichCoast.EditorTools.ProjectSetup.ApplyAndBuild \
  -logFile "$(native_path "$LOG")"
echo "done — exit $?"
grep -E "\[ProjectSetup\]|\[SceneBuilder\]" "$LOG" || true
if grep -qE "error CS[0-9]+" "$LOG"; then
  echo "COMPILE ERRORS:" >&2
  grep -E "error CS[0-9]+" "$LOG" | sed 's/^.*Assets/Assets/' | sort -u >&2
  exit 1
fi
