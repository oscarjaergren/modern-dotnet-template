#!/usr/bin/env bash
#
# The one place that knows how to format this repo.
#
# Called by two things that must agree:
#   - .claude/hooks/format-cs.sh   (agent edit-time, fixes files)
#   - .pre-commit-config.yaml      (git pre-commit, checks files)
#
# Usage:
#   scripts/format.sh [--check] [files...]
#
# With no files, operates on the whole repo. --check verifies without writing and exits non-zero
# if anything is unformatted.
#
# Two traps live here so they live nowhere else. Both were hit for real during development, and
# both fail *silently* — the command exits 0 and formats nothing:
#
#   1. `dotnet format --include` matches paths RELATIVE to the working directory. Given an
#      absolute path it matches nothing and exits 0.
#   2. `--no-restore` half-loads the workspace, so only some fixes apply. The rest then fail CI.
#
set -euo pipefail

CHECK=0
if [ "${1:-}" = "--check" ]; then CHECK=1; shift; fi

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

SOLUTION="ModernDotnetTemplate.slnx"

# Resolve a linter to the version mise pins, without requiring mise shims to be on PATH.
#
# This matters: the agent edit-time hook runs in whatever environment the editor gives it, which
# often has no shims activated. Resolving through `mise exec` makes the script work everywhere and
# always run the pinned version. Falling back to PATH keeps it usable without mise; failing loudly
# is the last resort, because a formatter that silently does nothing is worse than no formatter.
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
  rel="$(python3 -c 'import os,sys; print(os.path.relpath(sys.argv[1], sys.argv[2]))' "$f" "$ROOT")"
  case "$rel" in
    *.cs) cs_files+=("$rel") ;;
    *)    other_files+=("$rel") ;;
  esac
done

status=0

# --- C#: dotnet format ------------------------------------------------------------------------
if [ "$#" -eq 0 ] || [ "${#cs_files[@]}" -gt 0 ]; then
  require dotnet
  args=("$SOLUTION")
  if [ "${#cs_files[@]}" -gt 0 ]; then
    for f in "${cs_files[@]}"; do args+=(--include "$f"); done
  fi
  if [ "$CHECK" -eq 1 ]; then
    if ! dotnet format "${args[@]}" --verify-no-changes; then
      echo "error: C# formatting issues. Fix with: scripts/format.sh" >&2
      status=1
    fi
  else
    dotnet format "${args[@]}"
  fi
fi

# --- markdown / json: dprint ------------------------------------------------------------------
if [ "$#" -eq 0 ] || [ "${#other_files[@]}" -gt 0 ]; then
  if [ "$CHECK" -eq 1 ]; then
    if [ "${#other_files[@]}" -gt 0 ]; then
      tool dprint check "${other_files[@]}" || { echo "error: formatting issues. Fix with: scripts/format.sh" >&2; status=1; }
    else
      tool dprint check || { echo "error: formatting issues. Fix with: scripts/format.sh" >&2; status=1; }
    fi
  else
    if [ "${#other_files[@]}" -gt 0 ]; then
      tool dprint fmt "${other_files[@]}"
    else
      tool dprint fmt
    fi
  fi
fi

exit "$status"
