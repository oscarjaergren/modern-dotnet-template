# modern-dotnet-template

A starter template for .NET 10 services: minimal APIs in vertical slices, Aspire for orchestration,
Native AOT, and build gates strict enough that a green build means something.

It is also built to be a repository **coding agents work well in** — which shaped the layout, the
gates, and the instruction files rather than being a section in this README.

## What you get

|               |                                                                            |
| ------------- | -------------------------------------------------------------------------- |
| Runtime       | .NET 10 (LTS), pinned via `global.json`                                    |
| API           | Minimal APIs, `Features/` slices, no dispatch framework                    |
| Orchestration | Aspire 13 — OpenTelemetry, health checks, resilience, service discovery    |
| Deployment    | Native AOT → **~15 MB** container image (CI fails above 20), no Dockerfile |
| Tests         | xUnit v3: unit, integration (Aspire), and architecture                     |
| Contract      | `openapi.json` generated at build, committed, drift-gated in CI            |
| Agents        | `AGENTS.md`, a slice skill, two subagents, a format hook, deny rules       |

## Quickstart

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download), the Aspire CLI
(`dotnet tool install -g aspire.cli`), and [mise](https://mise.jdx.dev) for the pinned linters.

```bash
git clone https://github.com/<you>/modern-dotnet-template.git
cd modern-dotnet-template
mise install && prek install   # pinned linters + git hooks
dotnet build && dotnet test
```

Run it with the Aspire dashboard:

```bash
aspire run
```

Exercise the endpoints with `src/Api/Api.http`, or:

```bash
curl localhost:5180/ping
```

Build and run the container (needs Docker or Podman):

```bash
dotnet publish src/Api/Api.csproj -c Release -r linux-x64 /t:PublishContainer
```

## Layout

```
src/Api/Features/<Slice>/     one folder per feature; slices never reference each other
src/ServiceDefaults/          OTel, health checks, resilience, service discovery
src/AppHost/                  Aspire orchestration — dev-time only, never deployed
tests/                        unit · integration · architecture
docs/                         reference, read on demand
```

Two sample slices ship with it. `Ping` proves the pipeline end to end; `Greetings` demonstrates the
conventions worth copying — validated request, typed response, logic testable without HTTP, a unit
test and an integration test. **Both are meant to be deleted.**

## What it deliberately does not include

Each of these is a real opinion a template shouldn't force:

- **No data layer.** No EF Core, no Postgres, no repository abstraction. This is your first decision,
  and the one most likely to differ. It is also what keeps Native AOT viable — see
  [docs/native-aot.md](docs/native-aot.md) and [docs/adding-a-database.md](docs/adding-a-database.md).
- **No dispatch library.** No MediatR, no Wolverine. `Features/` gives you the structure; the
  machinery is yours to pick.
- **No assertion library.** The style in the sample tests propagates, because agents copy existing
  code — [docs/adding-a-dependency.md](docs/adding-a-dependency.md).
- **No API documentation UI.** No Scalar, no Swagger UI. The OpenAPI document is the contract and
  it is committed; a UI is one package away if you want one.
- **No messaging, auth, multi-tenancy, or UI.**
- **No cloud target, IaC, or Kubernetes manifests.** Aspire describes the app; deployment is yours.
- **No Dockerfile** — the SDK builds the image. That is a feature, see
  [docs/containers-and-deployment.md](docs/containers-and-deployment.md).

## Why the gates are strict

`TreatWarningsAsErrors`, nullable enforced, analyzers at `latest-recommended`, trim/AOT diagnostics
as errors, Central Package Management, and `dotnet format --verify-no-changes` in CI.

The point is unambiguous feedback. An agent cannot tell whether it is done from a build that passes
with forty warnings; it can from one that fails on the first real problem.

This is not theoretical. The **first build of this repository failed**, correctly: a transitive
`Microsoft.OpenApi` 2.0.0 carried a high-severity advisory, NuGet audit raised it, and
warnings-as-errors turned it into a failed restore. Under default settings that would have been one
line in a wall of output. See [docs/build-gates.md](docs/build-gates.md).

## Working with agents

`AGENTS.md` is canonical and read natively by most agent tooling. `CLAUDE.md` imports it, because
Claude Code does not read `AGENTS.md`;
[docs/documentation-approach.md](docs/documentation-approach.md) explains why an import rather than a
symlink, and how the instruction layers fit together.

For Claude Code specifically, `.claude/` adds an `add-slice` skill, two subagents (`slice-reviewer`,
and `codebase-locator` on the cheapest model tier), `Read` deny rules that keep build output and
secrets out of the agent's file tools, and a `PostToolUse` hook that formats every file the agent
edits so output lands CI-clean unprompted.

`AGENTS.md` documents several traps that fail *silently* — most notably that .NET 10's validation
source generator only discovers `public` types, so an `internal` request record means invalid
payloads return 200 instead of 400 with no warning anywhere. The integration tests assert the 400s
precisely so that cannot regress.

## Documentation

`docs/` holds living pages named for the task that makes you open them — "adding a database", "a
build gate is blocking me" — each self-contained enough to finish the job without opening a second
one. `AGENTS.md` carries the index.

There are no architecture decision records. ADRs are immutable by design, and nearly everything
worth writing down here is current configuration, live constraints, and procedures, which should be
corrected in place. [docs/documentation-approach.md](docs/documentation-approach.md) makes the case,
including the cost: a stale ADR is still correct, a stale living document is just wrong.

## Prior art

Jeremy Miller, *The Codebase Is the Prompt: Wolverine, Vertical Slices, and AI-Assisted Development*
(June 2026), argues the same thesis. His caveat is load-bearing and worth repeating: compression
without documented conventions fails. That is the argument for `AGENTS.md` and the `.claude/` layer
being part of the template rather than an afterthought.

## Licence

MIT.
