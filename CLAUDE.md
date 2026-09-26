@AGENTS.md

# Claude Code

Everything above is canonical. This file is an `@` import rather than a symlink because symlinks
break on Windows clones; see [docs/documentation-approach.md](docs/documentation-approach.md).

- **`/add-slice`**: the full recipe for a new slice, including the silent-failure traps.
- **`slice-reviewer`** agent: reviews a slice for isolation, nullability, AOT safety and tests.
  Run it before a PR.
- **`codebase-locator`** agent: answers "where is X?" on the cheapest model in its own context.
  Prefer it over searching from the main thread.
- A `PostToolUse` hook formats every `.cs`, `.md` and `.json` file you edit. Don't hand-format. If
  it reports a failure, that file is **not** formatted; fix the cause before moving on.

After editing, run `dotnet build` (about 2s incremental). If `src/Api/openapi.json` changes, read the
diff before calling the work done: it is the API contract, and CI fails on uncommitted drift.
