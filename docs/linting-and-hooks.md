# Linting and git hooks

**Read this if:** a hook blocked your commit or push, you want to add a check, or you are setting up
a fresh clone.

## Setup

```bash
mise install    # every linter, at the version pinned in .config/mise.toml
prek install    # pre-commit, commit-msg and pre-push hooks
```

Without `prek install` a clone has no hooks, and nothing tells you.

## What runs when

| Stage      | Checks                                                                                                       |
| ---------- | ------------------------------------------------------------------------------------------------------------ |
| pre-commit | gitleaks, typos, editorconfig-checker, actionlint, shellcheck, dprint, file hygiene, and two AGENTS.md traps |
| commit-msg | conventional commit format                                                                                   |
| pre-push   | `dotnet build`, `dotnet test`, `dotnet format`, `openapi.json` drift, lychee links                           |

The commit hook stays fast because a slow one gets bypassed with `--no-verify`. Anything that loads
the .NET workspace is too slow for it: `dotnet format` took the hook from 0.5s to 6s, so it runs on
pre-push.

Secrets are scanned twice. The commit hook scans the staged diff, which stops a secret before it
enters history but sees nothing in CI or in a `--no-verify` commit. CI scans full history, because a
secret committed and later deleted still needs rotating.

## Fix at edit time, verify at commit time

Hooks only verify; they never rewrite files, so a commit never contains content its author didn't
see. `scripts/format.sh` is the one thing that formats, and the agent's `PostToolUse` hook runs it
on every edit, so there is rarely anything left for the commit hook to reject.

```bash
scripts/format.sh                # whole repo
scripts/format.sh path/to/file   # specific files
```

Two `dotnet format` traps are handled in that script, and both fail silently if it is changed
carelessly: `--include` matches paths relative to the working directory, so an absolute path
formats nothing; and `--no-restore` half-loads the workspace, so only some fixes apply.

## Running checks yourself

```bash
prek run --all-files             # the commit stage, exactly as CI runs it
prek run <hook-id> --all-files   # one check
prek run --hook-stage pre-push   # the slow ones
```

## Nothing fails silently

- Tools run through `mise exec`. A missing tool fails the hook; it never falls back to `PATH`.
- No `|| true` or `continue-on-error` anywhere.
- The agent hook exits 2 on a formatting failure, which shows the error to the agent.
- `fail_fast` is off, so one run reports every failure.
- `--no-verify` is allowed, but CI runs the same definitions, so it only defers the failure. A
  secret is the exception: once pushed, it needs rotating, whatever CI says.

## Every pre-push hook needs a CI counterpart

`git push --no-verify` skips pre-push, and `prek run --all-files` only runs the commit stage. So:

| pre-push hook         | CI counterpart                            |
| --------------------- | ----------------------------------------- |
| `build`, `test`       | `dotnet build`, `dotnet test`             |
| `openapi-drift`       | the same `scripts/check-openapi-drift.sh` |
| `dotnet-format-check` | `dotnet format --verify-no-changes`       |
| `lychee`              | `prek run lychee --hook-stage pre-push`   |

Add a pre-push hook and its CI step in the same commit, or the check is advisory. The commit-msg
hook's counterpart is the PR-title check: squash merging makes the title the commit on `main`, so
CI runs it through the same `committed.toml`.

## Adding a check

Add it to `.pre-commit-config.yaml` and pin any new binary in `.config/mise.toml`; CI picks it up.
Then introduce a violation and watch it fail.

## Left out on purpose

- **markdownlint**: npm-only, and after dprint its remaining rules fight prose. lychee covers broken
  links, the failure that matters.
- **dprint's TOML plugin**: it collapses the aligned comments in `.config/mise.toml`.
- **editorconfig-checker's `IndentSize`**: it rejects aligned continuation lines that `dotnet format`
  accepts.

Build-time gates are a separate layer: [build-gates.md](build-gates.md).
