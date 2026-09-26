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
- **Health endpoints report status only.** `/health` and `/alive` are mapped in every environment so
  a deployment can probe them, and their response body is the aggregate status with no check names
  or exception text. Adding a detailed response writer republishes your dependency list — do it
  behind authentication or on a separate port.
- **The OpenAPI endpoint is Development-only** for the same reason; the document is committed to the
  repository instead.

## One thing it cannot do for you

Native AOT compiles the .NET runtime into the binary, so a runtime security release is picked up
only by rebuilding with a newer SDK — a fresh base image does not do it. Bump `global.json` when a
.NET security release lands. See
[docs/containers-and-deployment.md](docs/containers-and-deployment.md#patching-aot-moves-the-runtime-into-your-binary).

## What it does not do

There is no authentication, authorisation, rate limiting, or CORS policy — see the README's
exclusions. Add them before exposing anything generated from this template to the internet.
