#!/usr/bin/env bash
#
# PostToolUse hook: formats each file the agent edits (via scripts/format.sh), so its output
# is CI-clean and the git hooks only have to verify. See docs/linting-and-hooks.md.
#
# A PostToolUse hook cannot block; the edit has already happened. Exit 2 shows stderr to the
# agent, so a real failure uses it rather than leaving the agent to assume its output is
# formatted. Exit 0 means there was nothing to do.
set -uo pipefail

payload=$(cat)

# jq via mise, so the pinned version runs, falling back to PATH.
jq_bin() {
  if command -v mise >/dev/null 2>&1 && mise which jq >/dev/null 2>&1; then
    mise exec -- jq "$@"
  elif command -v jq >/dev/null 2>&1; then
    jq "$@"
  else
    return 127
  fi
}

file=$(printf '%s' "$payload" | jq_bin -r '.tool_input.file_path // empty' 2>/dev/null)
case $? in
  0) ;;
  127) echo "format-cs.sh: jq not found, so this edit was NOT formatted. Run 'mise install'." >&2
       exit 2 ;;
  *) echo "format-cs.sh: could not read the hook payload, so this edit was NOT formatted." >&2
     exit 2 ;;
esac

# Only the file types scripts/format.sh knows how to format.
case "$file" in
  *.cs|*.md|*.json) ;;
  *) exit 0 ;;
esac

[ -f "$file" ] || exit 0

root="${CLAUDE_PROJECT_DIR:-$(git -C "$(dirname "$file")" rev-parse --show-toplevel 2>/dev/null || pwd)}"
[ -x "$root/scripts/format.sh" ] || exit 0

if ! output=$("$root/scripts/format.sh" "$file" 2>&1); then
  echo "format-cs.sh: formatting $file failed, so it is NOT CI-clean:" >&2
  printf '%s\n' "$output" >&2
  exit 2
fi
