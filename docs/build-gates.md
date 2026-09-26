# Build gates

**Read this if:** a gate is blocking you, you want to relax or add one, or you wonder why the build
is this strict.

## What is enforced

`Directory.Build.props`, for every project:

- `TreatWarningsAsErrors` and `MSBuildTreatWarningsAsErrors`, so NuGet audit findings fail restore.
- Nullable enabled; `CS8600`, `CS8602`, `CS8603`, `CS8618` are errors.
- .NET analyzers at `latest-recommended`, plus Meziantou; code style enforced from `.editorconfig`.
- Trim and AOT diagnostics (`IL2026`, `IL2091`, `IL3050`) are errors.
- Central Package Management with transitive pinning; deterministic CI builds.

CI adds: `dotnet format --verify-no-changes`, `openapi.json` drift, an AOT publish with zero
warnings, a container that must serve its endpoints and probes under a size ceiling, and a
full-history secret scan.

The point is a signal an agent can act on: a build that passes with forty warnings does not say
whether the work is done. `latest-recommended` rather than `latest-all`, because `latest-all` turns
performance suggestions into failures.

It has already paid for itself. The first build failed because `Microsoft.AspNetCore.OpenApi`
pulled in `Microsoft.OpenApi` 2.0.0, which has a high-severity advisory (`GHSA-v5pm-xwqc-g5wc`).
The fix was a transitive pin in `Directory.Packages.props`, with no new direct dependency.

## Why there are no NuGet lock files

They worked, and were removed because they break Renovate. Renovate does not regenerate
`packages.lock.json` when a version changes in `Directory.Packages.props` or `global.json`, and the
workaround needs `postUpgradeTasks`, which hosted Renovate does not allow. Every dependency PR would
fail CI until someone regenerated the lock by hand. Transitive pinning already fixes every version
this repo resolves. If you self-host Renovate or don't use it, turn lock files back on with
`RestorePackagesWithLockFile` and locked-mode restore in CI.

## Relaxing a gate

Assume the code is wrong before the rule is. `CA1848` was once suppressed; the right fix was
switching the one log site to `[LoggerMessage]`, which is also correct under AOT.

A suppression is legitimate only when the rule cannot be satisfied here, or the repo has diverged
on purpose for a reason that is written down. Every suppression lives in `.editorconfig`, next to
that reason.

Suppress in `.editorconfig`, as narrowly as possible, with a comment saying why. Never
`#pragma warning disable` in source. Never relax a gate to get one build through; change it on
purpose, in its own commit.

## Adding a gate

Put it in `Directory.Build.props` if it applies everywhere, `.editorconfig` if it is style or
severity. Then introduce a violation, watch it fail, and revert. An unverified gate is decoration.

Git hooks and formatting are a separate layer: [linting-and-hooks.md](linting-and-hooks.md).
