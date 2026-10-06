# Containers and deployment

**Read this if:** you are changing how the image is built, looking for the Dockerfile, or working out
how to deploy.

## No Dockerfile

The SDK builds the OCI image from `src/Api/Api.csproj`:

```bash
dotnet publish src/Api/Api.csproj -c Release -r linux-x64 /t:PublishContainer
```

A hand-written Dockerfile drifts (base tags, stages, layer order) and nothing fails when it does.
Aspire's publisher uses this same mechanism, so anything configured here applies there too. CI
runs the image and checks the endpoints, a 409 with a `traceId`, both health probes and a
compressed size ceiling. Then [Schemathesis](https://schemathesis.readthedocs.io) fuzzes it against
the committed `openapi.json`: every response must match the contract, and invalid input must be
rejected. A failure prints a `curl` command that reproduces it.

## Base image

`ContainerFamily=noble-chiseled` resolves to `mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled`:
runtime-deps because an AOT binary needs no .NET runtime, chiseled so there is no shell or package
manager, and non-root by default. The family is set explicitly so an SDK change can't move it.

**Do not use a `chiseled-aot` base.** Much copied advice, and some stale Microsoft docs, suggest one,
but no `-aot` tag exists for .NET 10; they were .NET 8 and 9 on Alpine only.

## Patching: AOT moves the runtime into your binary

`runtime-deps` contains no .NET. The runtime is compiled into the `Api` binary by your SDK's
ILCompiler, so there are two patch streams:

| What                        | Lives in                  | Picked up by                                        |
| --------------------------- | ------------------------- | --------------------------------------------------- |
| OS libraries: OpenSSL, libc | the `noble-chiseled` base | any rebuild; the floating tag pulls the current one |
| The .NET runtime            | your binary               | a rebuild **with a newer SDK**                      |

Rebuilding with the same SDK recompiles the same runtime, however fresh the base. And
`global.json` is a floor, not a pin: with `rollForward: latestFeature` a build uses the newest
installed SDK above it. When .NET 10.0.12 fixed six CVEs (8 September 2026), CI's runner had SDK
10.0.401 and shipped the fix, while a machine with only 10.0.400 compiled the vulnerable runtime
from the same commit. Raising the floor in `global.json` makes a patch unconditional, so treat that
Renovate PR as a security fix.

## Image size

The trap is AOT debug symbols: by default `Api.dbg`, several times the binary's size, is copied into
the image. `CopyOutputSymbolsToPublishDirectory=false` prevents it; see
[native-aot.md](native-aot.md). CI fails the build if the compressed image grows past the ceiling set in `ci.yml`.

## Deployment is yours

The image serves plain HTTP on 8080; TLS ends at your ingress or load balancer. The slim host has
no HTTPS configuration, which is why there is no `https` launch profile.

No Kubernetes manifests, IaC or cloud target. Aspire's Docker Compose publisher is not wired in,
because `AddDockerComposeEnvironment()` adds a deployment target. To opt in, add
`Aspire.Hosting.Docker` to `src/AppHost` (version in `Directory.Packages.props`), then:

```csharp
builder.AddDockerComposeEnvironment("compose");
```

```bash
aspire publish -p docker-compose   # writes docker-compose.yaml and .env
```

The image it references is built the same way as above. Generated deployment files are build output
and gitignored; `src/Api/openapi.json` is a contract and is committed.

## Health endpoints

`/health` (readiness) and `/alive` (liveness) are mapped in every environment, unlike the Aspire
template's Development-only default. That default guards against a real leak, but the leak is in the
response body, where a detailed writer names every check. `WriteStatusOnly` returns `Healthy` or
`Unhealthy` and nothing else, and integration tests assert it.

The trade-off: anyone can tell the service is up, as any load balancer already can. In return,
Kubernetes and Container Apps can probe the image without edits. If you add a detailed writer, put
it behind authentication or on a separate port.

The runtime `/openapi/v1.json` endpoint stays Development-only; the document is committed instead.
