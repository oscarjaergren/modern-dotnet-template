# AGENTS.md

Instructions for coding agents, read natively by most of them, including Claude Code (v2.1.277+).

A .NET 10 service template: minimal APIs in vertical slices, Aspire, Native AOT, strict build gates.
No domain, data layer or dispatch framework, on purpose. The `Ping` and `Greetings` slices
demonstrate conventions and are **meant to be deleted**.

## Commands

`mise tasks` lists every command with what it does; `mise run <task>` runs one, from the repo root.
The ones you need most:

- `mise run build`, and `mise run test`, or `mise run test <text>` for test methods matching it.
- `mise run check` before pushing: what CI's build job checks.
- The app in the background: `mise run start`, then `url`, `rebuild` after a code change, `logs`
  and `traces` (JSON), and `stop`. `mise run start` doesn't hold the terminal; `aspire run` does.
- The container: `mise run image`, then `mise run image-check`.

## Layout

```
src/Api/Features/<Slice>/     one folder per slice; slices never reference each other
src/Api/Program.cs            one Map* call per slice, no assembly scanning
src/ServiceDefaults/          OTel, health checks, resilience, service discovery
src/AppHost/                  Aspire orchestration, dev-time only
tests/Api.UnitTests/          mirrors src/Api/Features/
tests/Api.IntegrationTests/   real app over HTTP via Aspire
tests/Api.ArchitectureTests/  enforces slice isolation
artifacts/                    all build output; no bin/ or obj/ beside source
```

## Adding a slice

Copy `Greetings`. Folder under `Features/`; one `Map<Slice>` extension method; `public sealed
record` request/response types; register them in `ApiJsonSerializerContext.cs`; one
`app.Map<Slice>()` line in `Program.cs`; unit and integration tests in the mirrored folders; build
and commit the `openapi.json` diff. Claude Code has this as the `/add-slice` skill.

## Rules

- **Slices never reference each other.** Enforced by `Api.ArchitectureTests`. Shared code moves out
  of `Features/`. [code-organisation.md](docs/code-organisation.md)
- **Data is a `record`**: `public sealed`, `init`, `required`. Behaviour stays in classes.
  [data-models.md](docs/data-models.md)
- **Expected failures are returned, not thrown**, as `Results<...>` so they appear in `openapi.json`.
  `CA1031` is an error. [errors-and-failures.md](docs/errors-and-failures.md)
- **Warnings are errors**, including NuGet audit. Never weaken a gate to pass a build.
  [build-gates.md](docs/build-gates.md)
- **Native AOT**: no unreferenced reflection, no `Reflection.Emit`, source-generated JSON only,
  `InvariantGlobalization`. Never suppress `IL2026`/`IL3050`. [native-aot.md](docs/native-aot.md)
- **Tests**: xUnit v3 built-in assertions only; names are sentences; integration tests assert the
  wire format (`JsonDocument`) and cover failure paths.

## Traps

Each of these fails silently, cryptically, or only on someone else's machine.

- **Validation fails open.** Bad input returns 200, not 400, if a request type isn't `public` or
  `AddValidation()` moves out of the assembly that defines the endpoints. The SDK registers the
  generator's interceptors; older posts that add an `InterceptorsNamespaces` entry predate that.
  CI's contract fuzzing catches it.
- **Every wire type needs an `ApiJsonSerializerContext` entry.** A missing one fails the build's
  OpenAPI step with `JsonTypeInfo metadata for type '...' was not provided`.
- **`TypedResults.Problem(...)` needs a matching `.ProducesProblem(status)`**, or the status is
  missing from `openapi.json`.
- **A `CLAUDE.md` or `CLAUDE.local.md` in the repo or above it replaces this file** for Claude Code,
  silently, unless it imports it with `@AGENTS.md`. Claude Code before v2.1.277 (v2.1.281 on
  Bedrock or with telemetry off) needs exactly that import. A hook rejects a committed one without
  it.
- **Pin CLI tools in `.config/mise.toml`, not a `dotnet-tools.json` manifest.** A manifest with two
  tools fails `dotnet tool restore` on any fresh machine
  ([dotnet/sdk#53783](https://github.com/dotnet/sdk/issues/53783)), so a hook rejects one. For a
  one-off .NET tool, use `dotnet tool exec`.

## Claude Code

- **`/add-slice`**: the full recipe for a new slice.
- **`slice-reviewer`** agent: reviews a slice before a PR.
- **`codebase-locator`** agent: answers "where is X?" on the cheapest model, in its own context.
  Prefer it over searching from the main thread.
- A `PostToolUse` hook formats every `.cs`, `.md` and `.json` file you edit, so don't hand-format.
  If it reports a failure, that file is **not** formatted; fix the cause before moving on.

After a change, read the `src/Api/openapi.json` diff before calling the work done: it is the API
contract.

## CI

Build, all tests, format, `openapi.json` drift, AOT publish with zero trim warnings, and a container
that must serve its endpoints and probes under a size ceiling, then survive fuzzing against
`openapi.json`. A separate job scans full git history for secrets, and PR titles must be
conventional commits, since squash merging makes the title the commit on `main`. Coverage is
reported, not gated; instead, pull requests are mutation tested, so unit tests must catch 80% of the
mutants in changed code, endpoints and `Program.cs` excepted. The devcontainer is built and run
through the gates when its inputs change, and weekly.

## Docs

Open a page only when its trigger matches.

| If you're…                                                   | Read                                                                   |
| ------------------------------------------------------------ | ---------------------------------------------------------------------- |
| working here with an agent, or weighing a token-saving tool  | [docs/ai-workflow.md](docs/ai-workflow.md)                             |
| writing C#, or wondering why the code looks like this        | [docs/code-style.md](docs/code-style.md)                               |
| blocked by a hook, or adding a check                         | [docs/linting-and-hooks.md](docs/linting-and-hooks.md)                 |
| defining a request, response or value object                 | [docs/data-models.md](docs/data-models.md)                             |
| writing something that can fail, or about to `throw`         | [docs/errors-and-failures.md](docs/errors-and-failures.md)             |
| sharing code between slices, or adding a non-slice           | [docs/code-organisation.md](docs/code-organisation.md)                 |
| adding persistence                                           | [docs/adding-a-database.md](docs/adding-a-database.md)                 |
| adding a NuGet package, or wondering why a library is absent | [docs/adding-a-dependency.md](docs/adding-a-dependency.md)             |
| hit by a trim/AOT warning, or removing AOT                   | [docs/native-aot.md](docs/native-aot.md)                               |
| blocked by a build gate                                      | [docs/build-gates.md](docs/build-gates.md)                             |
| changing the image, or deploying                             | [docs/containers-and-deployment.md](docs/containers-and-deployment.md) |
| changing any doc or instruction file                         | [docs/documentation-approach.md](docs/documentation-approach.md)       |

There are no ADRs; history comes from `git log`.
