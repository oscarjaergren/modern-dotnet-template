#!/usr/bin/env bash
#
# PostToolUse hook: format files as the agent edits them, so its output lands CI-clean.
#
# This is the "fix at edit time" half of the design. Because formatting is already correct by the
# time anything is committed, the git pre-commit hook only has to *verify* — it never rewrites
# files mid-commit, so a commit never contains content the author did not see.
# See docs/linting-and-hooks.md.
#
# The formatting logic itself lives in scripts/format.sh. The git hooks do not call it — they run
# `dprint check` and `dotnet format --verify-no-changes` directly, because they verify rather than
# fix. This is the only automated caller.
#
# Exit codes follow Claude Code's PostToolUse contract, which is worth knowing because the obvious
# assumption is wrong. A PostToolUse hook cannot block anything — the edit has already happened.
# Exit 2 does not stop the agent; it shows stderr *to the agent*, so it can react. That makes
# exit 2 the right answer for a real failure: swallowing it with `|| true` would leave the agent
# believing its output was formatted when it was not, which is the one outcome this hook exists
# to prevent. Exit 0 is reserved for "nothing to do here".
set -uo pipefail

payload=$(cat)

# jq, resolved through mise so the pinned version wins, falling back to PATH. Not Python: a .NET
# repo should not need a Python interpreter to format a file, and the old fallback here was
# `|| exit 0`, which meant a machine without Python silently formatted nothing.
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
