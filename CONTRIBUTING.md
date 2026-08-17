# Contributing

Thanks for taking a look. This is a template repository, so the bar for additions is deliberately
high: anything added here is inherited by every project generated from it.

## Before you open a PR

```bash
dotnet build                      # must be clean — warnings are errors
dotnet test                       # all three test projects
dotnet format --verify-no-changes # must report no changes
```

If you touched an endpoint's request or response shape, `src/Api/openapi.json` will have changed.
Commit it — CI fails on drift.

## What is likely to be accepted

- Fixes to the gates, CI, or agent instructions.
- Corrections where the template's advice has gone stale against a newer SDK or package.
- Improvements to `AGENTS.md`, especially newly discovered silent-failure traps.

## What is unlikely to be accepted

Adding a library. The template's value is what it leaves out — see the "deliberately does not
include" section of the README, and `docs/adding-a-dependency.md` for the reasoning. A data layer,
dispatch library, assertion library, and API documentation UI have each been considered and rejected
on purpose.

If you think one of those decisions is wrong, open an issue arguing against the relevant page in
`docs/` rather than a PR adding the package. That is a more interesting conversation and a faster one.

## Conventions

- Conventional commits (`feat:`, `fix:`, `docs:`, `chore:`).
- There are no ADRs. `docs/` holds living pages, named by the task that makes you open them, and
  they are edited in place when things change. `docs/documentation-approach.md` explains the reasoning
  and the trade-off; `git log -p docs/<page>.md` is the history.
- Changing behaviour means updating the relevant page in the same PR. A page that describes the old
  behaviour is worse than no page.
- Keep pages self-contained and short enough to read whole. Some duplication between pages is
  intended — see `docs/documentation-approach.md`.
- Suppress an analyzer in `.editorconfig` with a comment explaining why. Never with a scattered
  `#pragma`.
