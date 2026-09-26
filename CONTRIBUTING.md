# Contributing

Anything added here is inherited by every project generated from this template, so the bar for
additions is high. Setup is in the [README](README.md).

## Pull requests

- `main` accepts squash-merged pull requests with every check green.
- The PR title becomes the commit on `main`, so write it as a conventional commit: `feat:`, `fix:`,
  `docs:`, `chore:`.
- Change behaviour and the `docs/` page that describes it in the same PR.
- A changed `src/Api/openapi.json` means a changed API contract; say so in the description.

## What gets accepted

Fixes to the gates, CI or agent instructions; corrections where advice has gone stale; new
silent-failure traps for `AGENTS.md`.

Not new libraries: the template's value is what it leaves out
([adding-a-dependency.md](docs/adding-a-dependency.md)). If you think an exclusion is wrong, open an
issue arguing against the relevant page rather than a PR adding the package.
