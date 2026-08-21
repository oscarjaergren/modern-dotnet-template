@AGENTS.md

# Claude-specific notes

The import above is the canonical instruction set. Everything below applies only to Claude Code.

## Why this file is an import rather than a symlink

Claude Code does not read `AGENTS.md` natively; roughly thirty other agents do. A symlink breaks on
Windows clones without `core.symlinks` and renders confusingly on GitHub, so the documented `@`
import is used instead. See `docs/documentation-approach.md`.

## Available tooling

- **`/add-slice`** (`.claude/skills/add-slice/`) — end-to-end recipe for a new vertical slice,
  including the traps that cause silent failures. Prefer it over improvising.
- **`slice-reviewer`** (`.claude/agents/`) — reviews a slice for isolation, nullability, AOT safety,
  and test coverage. Worth running before opening a PR.
- **`codebase-locator`** (`.claude/agents/`) — answers "where is X?" on the cheapest model tier, in
  its own context window, so the main thread never pays for the files it opened. Prefer it over
  searching from the main thread. See [docs/ai-workflow.md](docs/ai-workflow.md).
- A `PostToolUse` hook runs `dotnet format` on any `.cs` file you edit, so formatting stays
  CI-clean without being asked. Do not hand-format to satisfy it.

## Working style in this repo

Run `dotnet build` after editing — it is fast (~2s incremental) and the strict gates mean the
compiler catches most mistakes immediately. It also regenerates `src/Api/openapi.json`; if that
file changes, commit it, because CI fails on drift.

When a change touches an endpoint's request or response shape, check the `openapi.json` diff before
declaring the work done. That diff is the API contract, and it is the thing reviewers read first.

Do not weaken a gate to make a build pass. The gates are the product — if one is genuinely wrong,
change it deliberately, and update the page in `docs/` that describes it in the same commit.
