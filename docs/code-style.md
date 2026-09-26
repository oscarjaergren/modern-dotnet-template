# Code style

**Read this if:** you are writing C# here, or wondering why the code looks the way it does.

## Prefer the platform

Reach for a language feature first, a built-in API second, a package last. Modern .NET already
covers validation, OpenAPI, HTTP result types, testable clocks and fast logging, and every package
avoided is one less thing to keep AOT-clean, licence-checked and updated. In a template it matters
more: whatever the first slice uses, every later slice copies.

## Language conventions

| Convention                            | Why                                                                    |
| ------------------------------------- | ---------------------------------------------------------------------- |
| File-scoped namespaces                | One less indent level. Enforced.                                       |
| Implicit and shared usings            | Declared once, not per file (`tests/Directory.Build.props` for tests). |
| `record` for data                     | Value equality; see [data-models.md](data-models.md).                  |
| `required` members, `init` over `set` | Missing values are compile errors; objects are immutable.              |
| Collection expressions                | Shorter, and the compiler picks the construction.                      |
| Primary constructors                  | Dependencies declared once.                                            |
| `sealed` by default                   | Nothing here is designed for inheritance.                              |

`var` where the type is obvious from the right-hand side, spelled out where it isn't. Not enforced.

## Built-ins over packages

- `TimeProvider`, not `DateTime.UtcNow`: fakeable with no wrapper interface.
- `IExceptionHandler` + `AddProblemDetails`, not custom middleware; see
  [errors-and-failures.md](errors-and-failures.md).
- `[LoggerMessage]`, not `logger.LogError(...)`: source-generated and checked at compile time.
- System.Text.Json source generation, mandatory under AOT.
- Built-in DataAnnotations validation, not FluentValidation: source-generated and AOT-safe.
- Built-in OpenAPI, not Swashbuckle.
- `FrozenSet` / `FrozenDictionary` for lookup tables built once and read often.

## Build and SDK conventions

- Central Package Management: versions only in `Directory.Packages.props`, with transitive pinning.
- `Directory.Build.props` per scope; `tests/` has its own, which imports the root one.
- `UseArtifactsOutput`: all build output under `artifacts/`.
- `.slnx`, the XML solution format, which diffs cleanly.
- `global.json` sets the SDK floor and the Microsoft.Testing.Platform runner.
- Warnings are errors; see [build-gates.md](build-gates.md).

## Architecture

Vertical slices under `src/Api/Features/`, each owning its endpoint, types and logic, and never
referencing each other. Routes are registered explicitly, one line per slice in `Program.cs`. Logic
that doesn't need HTTP lives outside the endpoint, as a static function unless there is something to
substitute. Failures are returned, not thrown. There is no Domain/Application/Infrastructure onion;
`Infrastructure/` holds plumbing, not a layer. Details in [code-organisation.md](code-organisation.md).

## Extending it, in order

1. **A data layer**, once you know what you store. Read [adding-a-database.md](adding-a-database.md)
   first: it decides whether AOT stays.
2. **Auth**, once there is something to protect. It constrains every endpoint.
3. **A domain result type**, once there are multi-step operations. C# 15 unions are worth waiting
   for.
4. **A dispatch library**, only if you keep hand-writing the same pipeline.

Messaging, caching and multi-tenancy come easier after these, and are hard to remove if added early.
