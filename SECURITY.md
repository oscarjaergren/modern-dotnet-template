# Security policy

## Reporting a vulnerability

Please report security issues privately through
[GitHub Security Advisories](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
on this repository, rather than opening a public issue.

This is a template with no production deployment, so there is no incident response commitment
beyond fixing the template and noting the change in the release notes.

## What this template does for you

- **NuGet audit is a build failure.** `TreatWarningsAsErrors` turns `NU1903`/`NU1904` into failed
  restores, so a dependency with a known advisory stops the build. This already caught a real
  high-severity advisory during development — see [docs/build-gates.md](docs/build-gates.md).
- **Central Package Management with transitive pinning**, so a vulnerable transitive dependency can
  be overridden in one place.
- **CodeQL** and **dependency review** run on pull requests.
- **Renovate** keeps dependencies current.
- **Chiseled container image**, running as a non-root user with no shell in the image.
- **Health endpoints are Development-only** by default, because exposing dependency health publicly
  leaks information. If you need them in production, widen and secure them deliberately.
- **The OpenAPI endpoint is Development-only** for the same reason; the document is committed to the
  repository instead.

## What it does not do

There is no authentication, authorisation, rate limiting, or CORS policy — see the README's
exclusions. Add them before exposing anything generated from this template to the internet.
