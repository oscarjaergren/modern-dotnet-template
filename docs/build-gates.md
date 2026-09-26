# Build gates

**Read this if:** a gate is blocking you, you want to relax or add one, or you are wondering why the
build is this strict.

## What is enforced

From `Directory.Build.props`, applied to every project:

- `TreatWarningsAsErrors` — including NuGet audit findings.
- Nullable enabled, with `CS8600` / `CS8602` / `CS8603` / `CS8618` escalated to errors.
- `EnableNETAnalyzers` at `AnalysisLevel=latest-recommended`.
- `EnforceCodeStyleInBuild`, driven by `.editorconfig`.
- Trim and AOT diagnostics (`IL2026`, `IL2091`, `IL3050`) as errors.
- Central Package Management with transitive pinning.
- Deterministic builds; `ContinuousIntegrationBuild` when `GITHUB_ACTIONS` is set.

In CI, additionally: `dotnet format --verify-no-changes`, an `openapi.json` drift check, an AOT
publish with zero warnings, a container that must start, serve traffic, answer its health probes
and stay under a compressed size ceiling, and a secret scan over full git history.

## Why this strict

Unambiguous feedback. A build that passes with forty warnings tells you nothing about whether the
work is done — which matters for a human reviewing at the end of the week, and matters much more for
an agent deciding whether it has finished. A build that fails on the first real problem is a signal
both can act on immediately.

`latest-recommended`, not `latest-all`. `latest-all` turns performance suggestions like `CA1848`
(use `LoggerMessage` delegates) into build failures, which produces noise and teaches nothing.

## This is not theoretical

The first build of this repository failed, correctly. `Microsoft.AspNetCore.OpenApi` pulls in
`Microsoft.OpenApi` 2.0.0, which carries a high-severity advisory (`GHSA-v5pm-xwqc-g5wc`). NuGet
audit raised `NU1903`, warnings-as-errors turned it into a failed restore, and the fix was a
transitive pin in `Directory.Packages.props` — no direct dependency needed:

```xml
<PackageVersion Include="Microsoft.OpenApi" Version="2.12.0" />
```

Under default settings that would have been one line in a wall of warnings, and the template would
have shipped with a known-vulnerable dependency.

## Why there are no NuGet lock files

`RestorePackagesWithLockFile` would pin the full resolved graph, and `dotnet restore` in locked mode
would fail rather than silently resolve something new. It works — it was implemented and the gate was
verified to fire (NU1004) when a version drifted from the lock.

It was then removed, because it breaks the other thing this template ships. Renovate does **not**
regenerate `packages.lock.json` when a version is bumped inside `Directory.Packages.props`, which is
exactly the Central Package Management setup here, and it does not regenerate them on an SDK bump in
`global.json` either. The documented workaround is `postUpgradeTasks` running
`dotnet restore` with force-evaluate, which hosted Renovate does not permit. The result would be that
every dependency PR fails CI until someone regenerates the lock by hand.

Central Package Management with `CentralPackageTransitivePinningEnabled` already fixes every version
this repo resolves, so what lock files add here is full-graph determinism at the cost of breaking
automated updates. If you self-host Renovate and can run post-upgrade commands, or you do not use
Renovate at all, turning lock files back on is a reasonable change: set
`RestorePackagesWithLockFile` in `Directory.Build.props` and add locked mode to the CI restore step.

## Relaxing a gate correctly

Sometimes an analyzer is wrong, or collides with a framework's own conventions. Five suppressions
exist, all in `.editorconfig`, all with a comment explaining why:

- `CA2007` — `ConfigureAwait`, a library rule that is a no-op in ASP.NET Core app code.
- `MA0004` — Meziantou's copy of `CA2007`, suppressed for the same reason.
- `MA0048` — scoped to `src/AppHost/AppHost.cs` only. It wants the file named after its type, and
  top-level statements synthesise a `Program` class nobody wrote.
- `CA1707` — tests only. Test method names are sentences with underscores.
- `CA1711` — tests only. xUnit collection classes end in `Collection`, which `CA1711` reserves for
  `ICollection` implementations.

`CA1848` is deliberately *not* suppressed. It asks for `LoggerMessage` delegates, and the one log
site in the API uses the `[LoggerMessage]` source generator — which is also the right choice under
Native AOT, so the rule and the constraint agree.

**Never suppress a warning because it is noisy, inherited, or "legacy".** If a rule fires, the
default assumption is that the code is wrong and the code gets fixed. `CA1848` is the worked
example: it was suppressed early on, and the right answer turned out to be switching the one log
site to the `[LoggerMessage]` source generator — which was also the better code. The suppression is
gone.

A suppression is only legitimate when the rule is genuinely unfixable here, or when the repo has
deliberately diverged for an architectural or stylistic reason that is written down.

The rules:

- Suppress in `.editorconfig`, scoped as narrowly as the situation allows.
- Write a comment saying why. A suppression without a reason is indistinguishable from giving up.
- Never use scattered `#pragma warning disable` in source. It is invisible in review and it spreads.
- Never relax a gate to make a specific build pass. If the gate is genuinely wrong, change it
  deliberately in its own commit.

## Adding a gate

Add it to `Directory.Build.props` if it applies everywhere, or `.editorconfig` if it is style or
diagnostic severity. Then **verify it actually fires**: introduce a violation, watch the build fail,
revert. An unverified gate is decoration.

That advice is not rhetorical. The slice-isolation architecture test was verified this way — a
deliberate cross-slice reference was added, the test failed with the exact pair named, and the
reference was reverted. The format hook was verified the same way and turned out to be silently
doing nothing.

## The formatting hook

`.claude/hooks/format-cs.sh` formats every `.cs`, `.md` and `.json` file the agent edits, via
`scripts/format.sh`, so agent output lands CI-clean. If formatting fails it exits 2, which under
Claude Code's `PostToolUse` contract shows the error to the agent rather than blocking it — the edit
has already happened, so the useful outcome is that the agent knows.

Two traps in `dotnet format` fail silently if the script is changed carelessly, and both are
commented in `scripts/format.sh`:

- `dotnet format --include` matches paths **relative to the working directory**. Given an absolute
  path it matches nothing, exits 0, and formats nothing.
- `--no-restore` prevents it loading the workspace fully, so it applies only some fixes — enough to
  look like it worked, not enough to pass CI.
