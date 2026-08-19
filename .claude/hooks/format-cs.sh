#!/usr/bin/env bash
#
# PostToolUse hook: format files as the agent edits them, so its output lands CI-clean.
#
# This is the "fix at edit time" half of the design. Because formatting is already correct by the
# time anything is committed, the git pre-commit hook only has to *verify* — it never rewrites
# files mid-commit, so a commit never contains content the author did not see.
# See docs/linting-and-hooks.md.
#
# The formatting logic itself lives in scripts/format.sh, which the git hook also calls. One
# implementation, so the two can never disagree.
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

# Only the file types scripts/format.sh knows how to format.
case "$file" in
  *.cs|*.md|*.json) ;;
  *) exit 0 ;;
esac

[ -f "$file" ] || exit 0

root="${CLAUDE_PROJECT_DIR:-$(git -C "$(dirname "$file")" rev-parse --show-toplevel 2>/dev/null || pwd)}"
[ -x "$root/scripts/format.sh" ] || exit 0

"$root/scripts/format.sh" "$file" >/dev/null || true
exit 0
