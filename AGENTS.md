# AGENTS.md

Instructions for coding agents working in this repository. This file is canonical; `CLAUDE.md`
imports it. If you change conventions, change them here.

## What this repo is

A starter template for .NET services: .NET 10, minimal APIs, vertical slices, Aspire for
orchestration, Native AOT, and strict build gates. There is **no domain, no data layer, and no
dispatch framework** — those are the consumer's decisions, deliberately left open.

The two sample slices (`Ping`, `Greetings`) exist to demonstrate conventions and prove the pipeline.
**They are meant to be deleted** once real slices exist.

## Commands

Everything runs from the repo root.

| Task | Command |
|---|---|
| Build | `dotnet build` |
| All tests | `dotnet test` |
| One project | `dotnet test --project tests/Api.UnitTests/Api.UnitTests.csproj` |
| Format check | `dotnet format --verify-no-changes` |
| Fix formatting | `dotnet format` |
| Run locally | `aspire run` |
| AOT publish | `dotnet publish src/Api/Api.csproj -c Release -r linux-x64` |
| Container | `dotnet publish src/Api/Api.csproj -c Release -r linux-x64 /t:PublishContainer` |

Note `dotnet test --project <path>`, not `dotnet test <path>` — this repo uses the
Microsoft.Testing.Platform runner (opted into via `global.json`), where the old positional form
is not valid.

## Layout

```
src/Api/Features/<Slice>/     one folder per slice; slices never reference each other
src/Api/Program.cs            one Map* call per slice, no assembly scanning
src/ServiceDefaults/          OTel, health checks, resilience, service discovery
src/AppHost/                  Aspire orchestration; dev-time only, never deployed
tests/Api.UnitTests/          mirrors src/Api/Features/ exactly
tests/Api.IntegrationTests/   real app over HTTP via Aspire.Hosting.Testing
tests/Api.ArchitectureTests/  enforces slice isolation
artifacts/                    ALL build output (UseArtifactsOutput) — gitignored, safe to delete
```

There are no `bin/` or `obj/` folders beside the source. `artifacts/bin/<Project>/<pivot>/` and
`artifacts/publish/<Project>/<pivot>/` are the equivalents, where pivot is e.g. `release_linux-x64`.

## Adding a slice

1. Create `src/Api/Features/<Slice>/`.
2. Add an endpoint class with a single `internal static IEndpointRouteBuilder Map<Slice>(this IEndpointRouteBuilder app)`.
3. Add request/response records — **`public`**, see the trap below.
4. Register the types in `src/Api/ApiJsonSerializerContext.cs`.
5. Add one line to `Program.cs`: `app.Map<Slice>();`.
6. Add unit tests under `tests/Api.UnitTests/Features/<Slice>/`.
7. Add integration tests under `tests/Api.IntegrationTests/Features/<Slice>/`.
8. Run `dotnet build` — this regenerates `src/Api/openapi.json`. Commit it.

`Greetings` is the reference implementation. Copy its shape.

## Rules that are enforced, not suggested

**Slices never reference each other.** `Api.ArchitectureTests` discovers every namespace under
`Api.Features.*` and asserts pairwise isolation, so a new slice is covered automatically. If two
slices need shared code, move it *out* of `Features/` — do not relax the rule.

**No assembly scanning for endpoint registration.** Every route is visible in `Program.cs`.

**Data is a `record`.** Requests, responses, value objects — `public sealed record`, `init` not
`set`, `required` for mandatory members. Value equality is what makes `Assert.Equal` compare contents
in tests. Services and anything with behaviour stay classes.
See [docs/data-models.md](docs/data-models.md).

**Exceptions are for bugs, not for expected failures.** A missing record, a rejected business rule,
or invalid input is an outcome — return it as a typed result (`Results<Ok<T>, ValidationProblem,
ProblemHttpResult>`) so it appears in the signature *and* in `openapi.json`. A thrown exception
appears in neither, and becomes a 500 indistinguishable from a real bug. `CA1031` is an error, and
`src/Api/Infrastructure/ProblemDetailsExceptionHandler.cs` is the safety net for what genuinely is
exceptional. See [docs/errors-and-failures.md](docs/errors-and-failures.md).

**Warnings are errors.** So are NuGet audit findings (`NU1903`) — a dependency with a known
advisory fails `restore`, not review.

**`openapi.json` is committed and drift-gated.** CI regenerates it and fails if the result differs.
API contract changes therefore show up in the PR diff.

## Traps that will silently cost you hours

These are real failures hit while building this template, not hypotheticals.

**Validation request types must be `public`.** .NET 10's validation source generator only discovers
public types. Mark a request record `internal` and the generated resolver comes back empty:
validation never runs, invalid payloads return **200 instead of 400**, and there is no build error,
no analyzer warning, and no log line. The convention here is that types crossing the HTTP boundary
are `public`; everything else in a slice stays `internal`.

**Validation also needs an MSBuild opt-in.** `Api.csproj` sets
`InterceptorsNamespaces` to include `Microsoft.Extensions.Validation.Generated`. Most blog posts
say `Microsoft.AspNetCore.Http.Validation.Generated` — that is the preview name and does nothing.
Do not "fix" this property.

**`AddValidation()` must be called from the assembly that defines the endpoints.** It is in
`Program.cs` in the `Api` project for that reason. Moving it to a library breaks validation silently.

**Every wire type needs an entry in `ApiJsonSerializerContext`.** Under Native AOT there is no
reflection fallback — a missing entry fails at runtime, not at build.

**`ProblemHttpResult` does not document its own status code.** It has no compile-time status, so a
409 returned via `TypedResults.Problem(...)` is missing from `openapi.json` unless the endpoint also
declares `.ProducesProblem(StatusCodes.Status409Conflict)`. Typed arms like `Ok<T>` are inferred and
need no equivalent. Check the `openapi.json` diff after adding a failure path.

## Native AOT constraints

The API publishes with `PublishAot=true`, and CI fails on any trim or AOT warning. When writing code:

- No reflection over types not statically referenced. No `Activator.CreateInstance`.
- No dynamic code generation, `Reflection.Emit`, or runtime expression compilation.
- System.Text.Json **source generation only** — never the reflection-based serializer.
- Prefer `WebApplication.CreateSlimBuilder` (already used) over `CreateBuilder`.
- `InvariantGlobalization=true` — no culture-aware formatting or comparison.

If you add a library that is not AOT-safe, you have two honest options: replace it, or remove the
AOT gate deliberately following [docs/native-aot.md](docs/native-aot.md). Do not suppress the
warnings — suppressing `IL2026` does not make the code work, it moves the failure to runtime.

## Testing conventions

- xUnit v3 with built-in assertions. **Do not add an assertion library** —
  see [docs/adding-a-dependency.md](docs/adding-a-dependency.md).
- Test names read as sentences: `Post_greetings_rejects_invalid_input`.
- Common usings are global, declared in `tests/Directory.Build.props` (a directory-scoped props file
  that imports the root one). Add to that list rather than adding a using to every test file.
- Unit tests target logic that does not need HTTP (`Greeter`), and mirror the source folder layout.
- Integration tests assert against the **wire format** (`JsonDocument`), not the C# types, so they
  stay honest when internals are renamed.
- Integration tests must cover failure paths, not just happy paths. The validation trap above was
  caught only because the 400s are asserted.

## What CI enforces

Build (warnings as errors) → unit + architecture tests → integration tests → `dotnet format
--verify-no-changes` → `openapi.json` drift → AOT publish with zero trim/AOT warnings → container
build → container starts and serves both endpoints.

A green local `dotnet build && dotnet test && dotnet format --verify-no-changes` covers most of it.

## When to read more

Everything above applies to every task. The pages below do not — open one only when its trigger
matches, and expect it to answer the question on its own without needing a second page.

| If you're… | Read |
|---|---|
| defining a request, response, value object, or anything that holds data | [docs/data-models.md](docs/data-models.md) |
| an operation can fail, or you're about to `throw` | [docs/errors-and-failures.md](docs/errors-and-failures.md) |
| two slices need the same code, or you're adding something that isn't a slice | [docs/code-organisation.md](docs/code-organisation.md) |
| adding EF Core, Dapper, Postgres, or any persistence | [docs/adding-a-database.md](docs/adding-a-database.md) |
| adding a NuGet package, or wondering why some library is missing | [docs/adding-a-dependency.md](docs/adding-a-dependency.md) |
| hit by a trim/AOT warning, or removing the AOT gate | [docs/native-aot.md](docs/native-aot.md) |
| blocked by a build gate, or changing what's enforced | [docs/build-gates.md](docs/build-gates.md) |
| changing the container image, or working out how to deploy | [docs/containers-and-deployment.md](docs/containers-and-deployment.md) |
| changing any doc or instruction file, or looking for the ADRs | [docs/documentation-approach.md](docs/documentation-approach.md) |

Adding a slice is a procedure rather than a decision, so it is a skill rather than a doc:
`.claude/skills/add-slice/`. Non-Claude agents should follow the "Adding a slice" section above.

There are no architecture decision records — [docs/documentation-approach.md](docs/documentation-approach.md)
explains why. History comes from `git log`.
