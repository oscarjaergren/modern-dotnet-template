# Code style

**Read this if:** you are writing C# here and want the house conventions, or you are wondering why
something is written the way it is.

The other pages in `docs/` cover single decisions in depth. This one is the shape of the code
overall, and the principle underneath it.

## The principle: prefer the platform

Reach for a language feature first, a built-in .NET API second, and a NuGet package last. Modern
.NET has absorbed most of what used to need a library — validation, OpenAPI generation, result
types at the HTTP boundary, testable clocks, high-performance logging — and every package avoided
is one fewer thing to keep AOT-clean, licence-check and update.

This is not minimalism for its own sake. It is that in a template, **every dependency propagates**:
whatever the first slice uses becomes what every later slice copies. See
[adding-a-dependency.md](adding-a-dependency.md).

## Language conventions

| Convention                                      | Why                                                                                                                   |
| ----------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| File-scoped namespaces                          | One less indent level in every file. Enforced by `.editorconfig`.                                                     |
| Global usings in `Directory.Build.props`        | Common usings declared once, not repeated in every file. Test projects get theirs from `tests/Directory.Build.props`. |
| `record` for anything that is data              | Value equality, so `Assert.Equal` compares contents. See [data-models.md](data-models.md).                            |
| `required` members                              | A missing property is a compile error, not a runtime null.                                                            |
| `init` over `set`                               | Immutable after construction; object initialisers and JSON still work.                                                |
| Collection expressions (`[...]`, `[.. spread]`) | Shorter, and the compiler picks an efficient construction.                                                            |
| Primary constructors                            | Dependencies declared once, in the signature.                                                                         |
| `sealed` by default                             | Nothing here is designed for inheritance; sealing says so and helps the JIT.                                          |

`var` is used where the type is obvious from the right-hand side and spelled out where it is not.
The analyzers do not enforce a rule here because both readings are defensible.

## Prefer these built-ins

Each of these replaces something people still reach for a package to do:

- **`TimeProvider`** rather than `DateTime.UtcNow` — injectable and fake-able with no wrapper
  interface of your own. Registered in `Program.cs`, injected into endpoints.
- **`IExceptionHandler` + `AddProblemDetails`** rather than custom middleware. Without them an
  unhandled exception returns a 500 with an empty body. See
  [errors-and-failures.md](errors-and-failures.md).
- **`[LoggerMessage]`** rather than `logger.LogError(...)` — source-generated, no boxing, and the
  message template is validated at compile time. It is also what `CA1848` asks for, which is why
  that rule is enforced rather than suppressed.
- **`System.Text.Json` source generation** rather than reflection-based serialization. Mandatory
  under Native AOT — see [native-aot.md](native-aot.md).
- **Built-in DataAnnotations validation** rather than FluentValidation. Source-generated in .NET 10,
  so it is AOT-safe.
- **Built-in OpenAPI** rather than Swashbuckle, which is no longer in the .NET templates.
- **`FrozenSet` / `FrozenDictionary`** for lookup tables built once and read many times.

## Build and SDK conventions

The half nobody writes about, and where a lot of the value is:

- **Central Package Management** — every version in `Directory.Packages.props`; `PackageReference`
  entries carry no `Version`. Transitive pinning is on, so a vulnerable transitive package can be
  overridden in one place.
- **`Directory.Build.props`**, including a nested one under `tests/` that imports the root. Shared
  settings live in one file per scope rather than being copy-pasted into every `.csproj`.
- **`UseArtifactsOutput`** — all build output under `artifacts/`, no `bin/` or `obj/` beside source.
- **`.slnx`** — the XML solution format, which actually diffs.
- **`global.json`** pins the SDK, and opts into the Microsoft.Testing.Platform runner. VSTest support
  is gone in the .NET 10 SDK.
- **Warnings are errors**, including MSBuild warnings and NuGet audit findings. See
  [build-gates.md](build-gates.md).

## Architecture

**Vertical slices, not layers.** One folder per feature under `src/Api/Features/`, owning its
endpoint, its request and response types, and its logic. A feature is one directory; deleting it is
`rm -rf`. Slices may not reference each other, and an architecture test enforces it. See
[code-organisation.md](code-organisation.md).

**Explicit registration over discovery.** One `app.Map<Slice>()` line per slice in `Program.cs`. No
assembly scanning, no attribute-driven routing — every route the app serves is visible in one file,
and finding the code behind a route is one grep. The cost is one line per slice, written by hand.

**Logic that does not need HTTP lives outside the endpoint.** `Greeter` is a static function, not a
service with an interface. Reach for DI when you have something to substitute, not by reflex.

**Failures are returned, not thrown.** The endpoint's return type names every way the request can
end, and that is what appears in the committed `openapi.json`. A thrown exception appears in neither.

**No layering ceremony.** There is no `Domain/`, `Application/`, `Infrastructure/` onion. There is an
`Infrastructure/` folder, but it holds cross-cutting plumbing, not a layer.

## Suggested additions, in order

If you are extending this beyond the template, the order that tends to hurt least:

1. **A data layer**, once you know what you are storing — and read
   [adding-a-database.md](adding-a-database.md) first, because it is also the decision that
   determines whether Native AOT stays.
2. **Auth**, once you have something worth protecting. Not before — it constrains every endpoint.
3. **A result type for the domain**, once there is a domain layer with multi-step operations.
   C# 15 union types land with .NET 11, so this one is worth waiting for.
4. **A dispatch library**, only if you find yourself hand-writing the same pipeline repeatedly.
   The slice layout gives you the structure without it.

Messaging, caching and multi-tenancy are all easier to add once the above are settled, and much
harder to remove if added speculatively.
