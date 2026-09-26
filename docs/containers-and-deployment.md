# Containers and deployment

**Read this if:** you are changing how the image is built, looking for the Dockerfile, or working out
how to deploy this.

## There is no Dockerfile, on purpose

The .NET SDK produces an OCI image directly. Configuration lives in `src/Api/Api.csproj`:

```bash
dotnet publish src/Api/Api.csproj -c Release -r linux-x64 /t:PublishContainer
```

A hand-written multi-stage Dockerfile is another artifact to keep correct as the project evolves —
base image tags, build stages, and layer ordering all drift, and nothing fails when they do. The SDK
derives all of it from the project.

Aspire uses the same mechanism internally: its container image builder runs
`dotnet publish /t:PublishContainer` for .NET project resources. So this is one mechanism, not two,
and anything configured here is inherited by Aspire's publish path automatically.

CI gates it: the image is built, run, and curled, including a check that invalid input returns 400.

## Base image

`ContainerFamily=noble-chiseled`, which resolves to
`mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled` — runtime-deps because an AOT app is
self-contained and needs no .NET runtime. Chiseled means no shell and no package manager in the
image, and it runs as a non-root user by default.

**Do not switch to a `chiseled-aot` base.** Widely-copied advice recommends one, and Microsoft's own
`publish-configuration` documentation still carries stale `8.0.200`-era text mentioning
`jammy-chiseled-aot`. There is no `-aot` variant for .NET 10 in any family — querying MCR's tag list
directly, `-aot` images exist only for .NET 8 and 9 and only on Alpine/musl. Setting one gets you a
tag that does not exist.

Set the family explicitly rather than relying on inference, so a future SDK change cannot silently
move you to a different base.

## Patching: AOT moves the runtime into your binary

For a framework-dependent app, a .NET security release reaches production when you rebuild on a
fresh base image, because the runtime lives in the image. **That does not work here.** `runtime-deps`
contains no .NET at all. The runtime is compiled into the `Api` binary by the ILCompiler that ships
with your SDK, so there are two patch streams and they move differently:

| What                         | Lives in                  | Picked up by                                        |
| ---------------------------- | ------------------------- | --------------------------------------------------- |
| OS libraries — OpenSSL, libc | the `noble-chiseled` base | any rebuild; the floating tag pulls the current one |
| The .NET runtime             | your binary               | a rebuild **with a newer SDK** — bump `global.json` |

The second row is the one that catches people. Rebuilding with the same SDK recompiles the same
runtime, however fresh the base layer.

`global.json` sets a **floor**, not an exact version: with `rollForward: latestFeature`, a build uses
the newest installed 10.0 SDK at or above it. So the runtime you ship depends on the machine that
builds it. The .NET 10.0.12 security release (8 September 2026, six CVEs) showed both sides: CI's
runner already had SDK 10.0.401 and compiled in the patched runtime, while a developer machine
with only 10.0.400 compiled in the vulnerable 10.0.11, from the same commit.

Raising the floor is what makes the patch unconditional — no machine can build with an older SDK
once `global.json` says 10.0.401. Treat that bump as a security fix, not housekeeping: Renovate
raises it on the same schedule as everything else, and for this app it is worth merging promptly.

## Image size

Two things dominate, and one of them is a trap.

`CopyOutputSymbolsToPublishDirectory=false` keeps the AOT debug symbols out of the image. By default
the SDK copies `Api.dbg` into the publish folder, and it is substantially larger than the binary
itself — the image ends up several times the size of the thing it runs. Symbols are still written to
`artifacts/bin/Api/release_linux-x64/native/` and uploaded by CI as an artifact.

Filtering `ResolvedFileToPublish` does **not** work for this; the native `.dbg` never passes through
that item group. Use the property.

CI prints the current image size on every run, which is the number to trust. Figures written into
documentation go stale the first time someone adds a package.

## Deployment is not chosen for you

There is no Kubernetes manifest, no IaC, and no cloud target. Aspire describes the application; where
it runs is yours to decide.

Aspire's Docker Compose publisher is deliberately **not** wired in, because
`AddDockerComposeEnvironment()` adds a compute environment — a deployment-target opinion this
template does not hold.

To opt in, add `Aspire.Hosting.Docker` to `src/AppHost` (and its version to
`Directory.Packages.props`), then one line in `src/AppHost/AppHost.cs`:

```csharp
builder.AddDockerComposeEnvironment("compose");
```

Then:

```bash
aspire publish -p docker-compose
```

which emits `docker-compose.yaml` and `.env`. Because Aspire builds project images with
`dotnet publish /t:PublishContainer`, everything above — AOT, chiseled base, symbol exclusion — is
inherited with nothing to duplicate.

Generated deployment artifacts are build output and are gitignored, the same way a rendered Helm
chart would be. `src/Api/openapi.json` is different: that is a contract, and it is committed.

## Health endpoints

`ServiceDefaults` maps `/health` (readiness) and `/alive` (liveness) **in every environment**, and
the container gate in CI asserts both answer from the published image.

This departs from the Aspire template, which maps them in Development only. That default is aimed
at a real leak — a detailed health response names every registered check, so it publishes your
dependency list to anyone who asks — but the leak is in the *response body*, not the route.
`Extensions.WriteStatusOnly` writes the aggregate status and nothing else: `Healthy` or
`Unhealthy`, no check names, no exception text. Two integration tests assert that.

What you are accepting: an unauthenticated caller can tell the service is up. That is what every
load balancer in front of it already knows. What you get: a container Kubernetes and Container Apps
can probe without editing the template first, which was the single most likely thing to surprise
you on a first deployment.

If you swap in a detailed writer — worth doing behind a boundary, since per-check detail is genuinely
useful — put it on a separate port or behind authentication at the same time.

The runtime `/openapi/v1.json` endpoint is Development-only for the same reason. The document is
committed to the repository, so nothing needs to serve it in production.
