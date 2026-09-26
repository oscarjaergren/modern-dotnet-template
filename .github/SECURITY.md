# Security policy

## Reporting a vulnerability

Report it privately through this repository's
[private vulnerability reporting](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability),
not a public issue. The template has no deployment of its own, so the response is a fix to it.

## What the template does

NuGet audit findings fail the build. CodeQL and dependency review gate every pull request. Secrets
are scanned on commit and across full history in CI. The container is chiseled, non-root and has
no shell. Health probes return a status and nothing else. Details are in
[build-gates.md](../docs/build-gates.md) and
[containers-and-deployment.md](../docs/containers-and-deployment.md).

## What you must do

- **Patch by rebuilding with a newer SDK.** Native AOT compiles the .NET runtime into the binary, so
  a fresh base image does not pick up a runtime security release. Raise the `global.json` floor;
  [here is why](../docs/containers-and-deployment.md#patching-aot-moves-the-runtime-into-your-binary).
- **Add authentication, authorisation, rate limiting and CORS** before exposing anything generated
  from this template. None are included.
