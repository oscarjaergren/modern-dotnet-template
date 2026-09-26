#!/usr/bin/env bash
#
# Formats the repo, or the files given. Fixing only; verification belongs to the git hooks.
#
# Usage: scripts/format.sh [files...]
#
# Two dotnet format traps, both silent (exit 0, nothing formatted):
#   1. --include matches paths relative to the working directory, not absolute ones.
#   2. --no-restore half-loads the workspace, so only some fixes apply.
#
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

SOLUTION="ModernDotnetTemplate.slnx"

# The pinned version via `mise exec`, since editor hook environments often lack mise shims.
# Falls back to PATH, then fails loudly: a formatter that silently does nothing is worse.
tool() {
  local name="$1"; shift
  if command -v mise >/dev/null 2>&1 && mise which "$name" >/dev/null 2>&1; then
    mise exec -- "$name" "$@"
  elif command -v "$name" >/dev/null 2>&1; then
    "$name" "$@"
  else
    echo "error: '$name' not found." >&2
    echo "       Run 'mise install' to provision the pinned toolchain." >&2
    return 127
  fi
}

# dotnet is pinned by global.json, not mise, so it is expected on PATH.
require() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "error: '$1' not found on PATH." >&2
    exit 127
  fi
}

# Split incoming paths (absolute or relative) into C# and everything else, as repo-relative paths.
cs_files=()
other_files=()
for f in "$@"; do
  # Strip the repo root from absolute paths. Shell only, so no Python is needed.
  rel="${f#"$ROOT"/}"
  case "$rel" in
    *.cs) cs_files+=("$rel") ;;
    *)    other_files+=("$rel") ;;
  esac
done

# --- C#: dotnet format ------------------------------------------------------------------------
if [ "$#" -eq 0 ] || [ "${#cs_files[@]}" -gt 0 ]; then
  require dotnet
  args=("$SOLUTION")
  for f in ${cs_files[@]+"${cs_files[@]}"}; do args+=(--include "$f"); done
  dotnet format "${args[@]}"
fi

# --- markdown / json: dprint ------------------------------------------------------------------
if [ "$#" -eq 0 ] || [ "${#other_files[@]}" -gt 0 ]; then
  tool dprint fmt ${other_files[@]+"${other_files[@]}"}
fi
