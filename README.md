# modern-dotnet-template

A starter template for .NET 10 services: minimal APIs in vertical slices, Aspire, Native AOT, and
build gates strict enough that a green build means something. It is also shaped for coding agents:
the layout, the gates and the instruction files are designed to be worked in by one.

|               |                                                                           |
| ------------- | ------------------------------------------------------------------------- |
| Runtime       | .NET 10 LTS, pinned in `global.json`                                      |
| API           | Minimal APIs in `Features/` slices, no dispatch framework                 |
| Orchestration | Aspire 13: OpenTelemetry, health checks, resilience, service discovery    |
| Deployment    | Native AOT, **~15 MB** container image (CI fails above 20), no Dockerfile |
| Tests         | xUnit v3: unit, integration (via Aspire), architecture                    |
| Contract      | `openapi.json` generated at build, committed, drift-gated in CI           |
| Agents        | `AGENTS.md`, a slice skill, subagents, a format hook, deny rules          |

## Quickstart

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download), the Aspire CLI
(`dotnet tool install -g aspire.cli`) and [mise](https://mise.jdx.dev).

```bash
git clone https://github.com/oscarjaergren/modern-dotnet-template.git
cd modern-dotnet-template
mise install && prek install   # pinned linters and git hooks
dotnet build && dotnet test
aspire run                     # with the Aspire dashboard
```

Try the endpoints with `src/Api/Api.http` or `curl localhost:5180/ping`. Build the container (needs
Docker or Podman) with:

```bash
dotnet publish src/Api/Api.csproj -c Release -r linux-x64 /t:PublishContainer
```

The `Ping` and `Greetings` slices show the conventions worth copying. **Delete both** once you have
real slices.

## Deliberately not included

Each is a choice a template shouldn't make for you:

- **Data layer.** Your first real decision, and the one that decides whether Native AOT stays. See
  [adding-a-database.md](docs/adding-a-database.md).
- **Dispatch library** (MediatR, Wolverine), **assertion library**, **API docs UI** (Scalar, Swagger
  UI). Why: [adding-a-dependency.md](docs/adding-a-dependency.md).
- **Messaging, auth, multi-tenancy, UI, cloud target, IaC, Dockerfile.** The SDK builds the image.

## Gates

Warnings are errors, including NuGet audit findings; analyzers run at `latest-recommended`; trim and
AOT warnings are errors; `dotnet format` and the API contract are checked in CI. The first build of
this repo failed on a vulnerable transitive package, which is the point:
[build-gates.md](docs/build-gates.md).

## Agents and docs

`AGENTS.md` holds the agent instructions, and Claude Code reads it natively. It lists the traps that
fail silently, such as an
`internal` request type turning off validation, and indexes `docs/`, where each page is named for
the task that sends you there. There are no ADRs:
[documentation-approach.md](docs/documentation-approach.md) explains why.

Prior art: Jeremy Miller, *The Codebase Is the Prompt* (June 2026), argues the same thesis, with the
caveat that conventions must be written down for any of it to work.

## Licence

MIT.
