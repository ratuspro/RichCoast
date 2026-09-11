#!/usr/bin/env bash
# Resolve the Unity editor executable for this project. Sourced by the other Tools/ scripts.
#
# Order: an explicit UNITY_PATH override, else the Hub install matching
# ProjectSettings/ProjectVersion.txt on whichever OS we're on (Windows Git Bash, macOS, Linux).
# Sets UNITY_PATH and UNITY_VERSION.

resolve_unity_path() {
  local project_dir="$1"
  UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$project_dir/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
  if [[ -n "${UNITY_PATH:-}" ]]; then
    return
  fi
  case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*)
      UNITY_PATH="/c/Program Files/Unity/Hub/Editor/$UNITY_VERSION/Editor/Unity.exe" ;;
    Darwin)
      UNITY_PATH="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity" ;;
    *)
      UNITY_PATH="$HOME/Unity/Hub/Editor/$UNITY_VERSION/Editor/Unity" ;;
  esac
  if [[ ! -x "$UNITY_PATH" ]]; then
    echo "Unity editor $UNITY_VERSION not found at: $UNITY_PATH" >&2
    echo "Install it via Unity Hub or set UNITY_PATH." >&2
    exit 1
  fi
}

# Unity on Windows wants a native path for -projectPath / -testResults / -logFile.
native_path() {
  if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi
}
