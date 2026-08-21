# Linting and git hooks

**Read this if:** a hook is blocking your commit or push, you want to add a check, or you are
setting the repo up for the first time.

## Bootstrap

```bash
mise install        # installs every linter at the version pinned in mise.toml
prek install        # installs the pre-commit, commit-msg and pre-push hooks
```

Without the second command a fresh clone has no hooks at all, and nothing will tell you.

## What runs when

Stage is a performance decision. The commit hook is kept under half a second because a slow commit
hook gets bypassed, and a bypassed hook is worse than none — it creates confidence without cover.

| Stage      | Checks                                                                                                                                                                                    | Typical |
| ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------- |
| pre-commit | gitleaks, typos, editorconfig-checker, actionlint, shellcheck, dprint, plus hygiene (large files, merge markers, case collisions, private keys, mixed line endings, JSON/YAML/TOML parse) | ~0.5s   |
| commit-msg | conventional commit format                                                                                                                                                                | instant |
| pre-push   | `dotnet build`, `dotnet test`, `dotnet format`, openapi.json drift, lychee link check                                                                                                     | seconds |

`dotnet format` is on pre-push rather than pre-commit deliberately: it loads the whole workspace even
when given `--include`, which took the commit hook from 0.5s to 6s. Formatting is already fixed at
edit time (below), so the check is a safety net rather than the mechanism.

## Fix at edit time, verify at commit time

The hooks **verify**; they do not rewrite your files.

`scripts/format.sh` is the only thing that formats, and `.claude/hooks/format-cs.sh` runs it on every
agent edit. So by the time anything is committed there is usually nothing left to fix, and the commit
hook passes without a rejection cycle.

This is deliberate. A hook that reformats during the commit produces a commit containing content the
author never saw — harmless for whitespace, genuinely risky for anything semantic, and it silently
invalidates an agent's model of the file it just wrote.

To format manually:

```bash
scripts/format.sh              # whole repo
scripts/format.sh path/to/file # specific files
```

## Running the checks yourself

```bash
prek run --all-files           # everything — exactly what CI runs
prek run <hook-id> --all-files # one check, when iterating on a single failure
prek run --hook-stage pre-push # the slow ones, without pushing
```

## Nothing fails silently

The requirement that shaped the configuration:

- **A missing tool fails.** Linters are invoked through `mise exec`, so the pinned version is the one
  that runs. If mise or the tool is absent the hook errors — it never falls back to whatever happens
  to be on PATH, and never skips.
- **No `|| true`, no `continue-on-error`.** Anywhere.
- **`fail_fast` is off**, so one run reports every failure rather than stopping at the first. Fixing
  is one round trip instead of one per problem.
- **Every failure names the command that fixes it.**
- **`--no-verify` cannot ship.** It is not blocked — it is a legitimate escape — but CI runs the
  identical definitions via `prek run --all-files`, so a local bypass only defers the failure.

## The rule that makes pre-push safe

`git push --no-verify` skips pre-push hooks, so a pre-push check is only real if **CI runs the same
thing**. Note that `prek run --all-files` runs the *pre-commit* stage only — it does not cover
pre-push hooks.

So every pre-push hook needs a CI counterpart:

| pre-push hook         | Covered in CI by                        |
| --------------------- | --------------------------------------- |
| `build`               | `dotnet build`                          |
| `test`                | `dotnet test`                           |
| `openapi-drift`       | the `git diff` contract check           |
| `dotnet-format-check` | `dotnet format --verify-no-changes`     |
| `lychee`              | `prek run lychee --hook-stage pre-push` |

`lychee` was missed when the hooks were first written — it had no CI equivalent, so a
`--no-verify` push would have shipped broken links with nothing downstream to catch them. **If you
add a pre-push hook, add its CI counterpart in the same commit**, or it is advisory rather than
enforced.

## Adding a check

Add a block to `.pre-commit-config.yaml`, and pin the tool in `mise.toml` if it is a new binary.
CI picks it up automatically because CI runs the same file. Then **verify it actually fails**:
introduce a real violation, watch it fail, revert. An unverified gate is decoration.

## Deliberate omissions

**markdownlint.** Only available via npm, and its remaining value after dprint's formatting is
stylistic rules that fight hand-written prose. `lychee` covers the markdown failure that actually
matters — broken links.

**dprint's TOML plugin.** It collapses the aligned comments in `mise.toml` and buys nothing;
editorconfig-checker already covers that file's whitespace.

**`IndentSize` in editorconfig-checker.** Disabled in `.editorconfig-checker.json`. The check counts
leading spaces and demands a multiple of `indent_size`, which is wrong for aligned continuation lines
— a C# expression continued under an operator, an XML comment aligned under its first line, a
markdown list item indented 3 spaces to align under `1.`. `dotnet format` already owns C# indentation
and accepts all of them.

## Related

Build-time gates — warnings as errors, analyzers, NuGet audit — are a separate layer, in
[build-gates.md](build-gates.md).
