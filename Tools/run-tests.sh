#!/usr/bin/env bash
# Run the EditMode test suite headlessly via the Unity CLI.
#
#   Tools/run-tests.sh [--platform EditMode|PlayMode] [--filter <test name filter>]
#
# Unity must not have the project open in another Editor instance (it takes a project lock).
# Override the editor with UNITY_PATH=/path/to/Unity.
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PLATFORM="EditMode"
FILTER=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --platform) PLATFORM="$2"; shift 2 ;;
    --filter)   FILTER="$2";   shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

# Resolve the editor: explicit override, else the version this project is pinned to.
if [[ -z "${UNITY_PATH:-}" ]]; then
  VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
  UNITY_PATH="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
fi
if [[ ! -x "$UNITY_PATH" ]]; then
  echo "Unity editor not found at: $UNITY_PATH" >&2
  echo "Install it via Unity Hub or set UNITY_PATH." >&2
  exit 1
fi

RESULTS="$PROJECT_DIR/Logs/test-results-$PLATFORM.xml"
mkdir -p "$PROJECT_DIR/Logs"
rm -f "$RESULTS"

ARGS=(
  -batchmode -nographics
  -projectPath "$PROJECT_DIR"
  -runTests -testPlatform "$PLATFORM"
  -testResults "$RESULTS"
  -logFile -
)
[[ -n "$FILTER" ]] && ARGS+=(-testFilter "$FILTER")

echo "Running $PLATFORM tests with $UNITY_PATH"
set +e
"$UNITY_PATH" "${ARGS[@]}" > "$PROJECT_DIR/Logs/test-run-$PLATFORM.log" 2>&1
UNITY_EXIT=$?
set -e

if [[ ! -f "$RESULTS" ]]; then
  echo "FAILED: no results file produced (compile error or editor crash)." >&2
  echo "--- last 60 log lines ---" >&2
  tail -n 60 "$PROJECT_DIR/Logs/test-run-$PLATFORM.log" >&2
  exit 1
fi

# The root <test-run> element carries the counts; print them and fail on any non-pass.
python3 - "$RESULTS" <<'PY'
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
