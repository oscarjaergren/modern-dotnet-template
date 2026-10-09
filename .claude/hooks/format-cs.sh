#!/usr/bin/env bash
#
# PostToolUse hook: formats each file the agent edits (via scripts/format.sh), so its output
# is CI-clean and the git hooks only have to verify. See docs/linting-and-hooks.md.
#
# A PostToolUse hook cannot block; the edit has already happened. Every failure exits 2, which
# shows stderr to the agent, rather than leaving it to assume its output is formatted.
set -uo pipefail

fail() {
  echo "format-cs.sh: $1, so this edit was NOT formatted." >&2
  exit 2
}

file=$(mise exec -- jq -r '.tool_input.file_path // empty') \
  || fail "could not read the hook payload with the pinned jq (run 'mise install')"

# Only the file types scripts/format.sh knows how to format.
case "$file" in
  *.cs|*.md|*.json) ;;
  *) exit 0 ;;
esac

# Deleted or moved by the edit: nothing to format.
[ -f "$file" ] || exit 0

output=$("$CLAUDE_PROJECT_DIR/scripts/format.sh" "$file" 2>&1) || {
  printf '%s\n' "$output" >&2
  fail "formatting $file failed"
}
