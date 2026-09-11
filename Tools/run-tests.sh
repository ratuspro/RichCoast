#!/usr/bin/env bash
# Run the Unity test suite headlessly via the Unity CLI.
#
#   Tools/run-tests.sh [--platform EditMode|PlayMode] [--filter <test name filter>]
#
# Unity must not have the project open in another Editor instance (it takes a project lock) —
# close the editor first, or trigger tests in-editor instead. Override the editor with
# UNITY_PATH=/path/to/Unity.
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=Tools/unity-path.sh
source "$PROJECT_DIR/Tools/unity-path.sh"
PLATFORM="EditMode"
FILTER=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --platform) PLATFORM="$2"; shift 2 ;;
    --filter)   FILTER="$2";   shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

resolve_unity_path "$PROJECT_DIR"

RESULTS="$PROJECT_DIR/Logs/test-results-$PLATFORM.xml"
LOG="$PROJECT_DIR/Logs/test-run-$PLATFORM.log"
mkdir -p "$PROJECT_DIR/Logs"
rm -f "$RESULTS"

# PlayMode keeps graphics: the ScreenshotCapture test renders through the real camera.
ARGS=(-batchmode)
[[ "$PLATFORM" == "EditMode" ]] && ARGS+=(-nographics)
ARGS+=(
  -projectPath "$(native_path "$PROJECT_DIR")"
  -runTests -testPlatform "$PLATFORM"
  -testResults "$(native_path "$RESULTS")"
  -logFile "$(native_path "$LOG")"
)
[[ -n "$FILTER" ]] && ARGS+=(-testFilter "$FILTER")

echo "Running $PLATFORM tests with $UNITY_PATH"
set +e
"$UNITY_PATH" "${ARGS[@]}"
UNITY_EXIT=$?
set -e

if [[ ! -f "$RESULTS" ]]; then
  echo "FAILED: no results file produced (compile error or editor crash)." >&2
  echo "--- last 60 log lines ($LOG) ---" >&2
  tail -n 60 "$LOG" >&2
  exit 1
fi

# The root <test-run> element carries the counts; print them and fail on any non-pass.
python - "$RESULTS" <<'PY'
import sys, xml.etree.ElementTree as ET
run = ET.parse(sys.argv[1]).getroot()
get = lambda k: int(run.get(k, 0))
total, passed, failed = get("total"), get("passed"), get("failed")
skipped, inconclusive = get("skipped"), get("inconclusive")
print(f"tests: {total}  passed: {passed}  failed: {failed}  skipped: {skipped}  inconclusive: {inconclusive}")
if failed or inconclusive:
    for case in run.iter("test-case"):
        if case.get("result") != "Passed":
            print(f"\n  {case.get('result').upper()}: {case.get('fullname')}")
            msg = case.find("./failure/message")
            if msg is not None and msg.text:
                print("    " + msg.text.strip().replace("\n", "\n    "))
    sys.exit(1)
if total == 0:
    print("FAILED: no tests were run.")
    sys.exit(1)
print("all tests passed")
PY

exit $UNITY_EXIT
