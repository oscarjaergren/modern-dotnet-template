#!/usr/bin/env bash
# PostToolUse hook: format C# files as they are edited, so agent output lands CI-clean.
#
# Claude Code passes the tool payload as JSON on stdin. We pull out tool_input.file_path,
# ignore anything that is not a .cs file, and run `dotnet format` scoped to that one file.
#
# Always exits 0 — a formatting hook must never block the agent's work.
set -uo pipefail

payload=$(cat)

file=$(printf '%s' "$payload" | python3 -c \
  'import json,sys
try:
    print(json.load(sys.stdin).get("tool_input", {}).get("file_path", ""))
except Exception:
    print("")' 2>/dev/null) || exit 0

case "$file" in
  *.cs) ;;
  *) exit 0 ;;
esac

[ -f "$file" ] || exit 0

root="${CLAUDE_PROJECT_DIR:-$(git -C "$(dirname "$file")" rev-parse --show-toplevel 2>/dev/null || pwd)}"
solution="$root/ModernDotnetTemplate.slnx"
[ -f "$solution" ] || exit 0

# Two non-obvious requirements, both of which fail *silently* if you get them wrong:
#
#   1. `--include` matches paths relative to the working directory. Given an absolute path it
#      matches nothing, exits 0, and formats nothing. Claude Code always passes absolute paths,
#      so the conversion below is load-bearing.
#   2. No `--no-restore`. With it, dotnet format cannot fully load the workspace and applies only
#      some fixes — it will strip trailing blank lines but miss a missing space after a comma,
#      which CI then rejects.
#
# Both were found by deliberately mis-formatting a file and checking the hook actually fixed it.
# If you change this line, test it the same way.
rel=$(python3 -c 'import os,sys; print(os.path.relpath(sys.argv[1], sys.argv[2]))' "$file" "$root" 2>/dev/null) || exit 0

cd "$root" || exit 0
dotnet format "$solution" --include "$rel" >/dev/null 2>&1 || true
exit 0
