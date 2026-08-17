---
name: add-slice
description: Add a new vertical slice (feature) to the API — folder, endpoint, request/response types, validation, JSON registration, unit test, and integration test. Use whenever adding a new endpoint or feature to this template.
---

# Add a vertical slice

Follow this end to end. Steps 3 and 4 are where silent failures come from — do not skip them.

`src/Api/Features/Greetings/` is the reference implementation; when in doubt, copy its shape.

## 1. Create the folder

`src/Api/Features/<Slice>/`, where `<Slice>` is a plural noun (`Orders`, `Invoices`).

## 2. Request and response types

One file each, in the slice folder.

```csharp
namespace Api.Features.<Slice>;

public sealed record <Thing>Request
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string Name { get; init; }
}
```

**These must be `public`.** .NET 10's validation source generator only discovers public types. If
you mark a request record `internal`, the generated resolver is empty, validation never runs, and
invalid payloads return **200 instead of 400** — with no build error, no analyzer warning, and no
log line. Everything else in the slice stays `internal`.

Validation is plain DataAnnotations. Do not add FluentValidation.

## 3. Register the types for JSON — required

Add every type that crosses the wire to `src/Api/ApiJsonSerializerContext.cs`:

```csharp
[JsonSerializable(typeof(<Thing>Request))]
[JsonSerializable(typeof(<Thing>Response))]
```

Under Native AOT there is no reflection fallback. A missing entry fails at **runtime**, not build.

## 4. The endpoint

```csharp
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Features.<Slice>;

internal static class <Slice>Endpoint
{
    internal static IEndpointRouteBuilder Map<Slice>(this IEndpointRouteBuilder app)
    {
        app.MapPost("/<slice>", Handle)
            .WithName("<Name>")
            .WithSummary("<One line.>");

        return app;
    }

    private static Results<Ok<<Thing>Response>, ValidationProblem> Handle(
        <Thing>Request request,
        TimeProvider timeProvider) =>
        TypedResults.Ok(new <Thing>Response(/* ... */));
}
```

Declare the `ValidationProblem` arm even though the handler never returns it — validation
short-circuits before the handler, and declaring it is what documents the 400 in `openapi.json`.

## 5. Slice-local logic

Anything worth unit testing goes in its own file, free of HTTP — see `Greeter.cs`. Prefer a static
function. Reach for DI only when you have something to substitute.

**The slice may not reference any other slice.** `Api.ArchitectureTests` enforces this and will fail
the build. If you need shared code, move it out of `Features/`.

## 6. Register in Program.cs

One line, next to the others:

```csharp
app.Map<Slice>();
```

No assembly scanning — every route stays visible in one file.

## 7. Unit tests

`tests/Api.UnitTests/Features/<Slice>/`, mirroring the source layout exactly.

xUnit v3, built-in assertions. **No assertion library.** Name tests as sentences:
`Greet_without_age_omits_it`.

## 8. Integration tests

`tests/Api.IntegrationTests/Features/<Slice>/`, using `[Collection(nameof(ApiCollection))]` and the
shared `ApiFixture`.

Assert against the **wire format** with `JsonDocument`, not the C# types — that keeps the test
honest if internals are renamed.

**Cover the failure paths.** At minimum assert one 400 per validated field. This is not box-ticking:
it is the only thing that catches the `public`-type trap from step 2.

## 9. Verify

```bash
dotnet build && dotnet test && dotnet format --verify-no-changes
```

The build regenerates `src/Api/openapi.json`. **Review that diff** — it is the API contract, and CI
fails if it is not committed.

## If you get stuck

This skill is the procedure. Reference material lives in `docs/`, and each page is self-contained:

- a trim or AOT warning → `docs/native-aot.md`
- the slice needs a database → `docs/adding-a-database.md`
- the slice needs a package → `docs/adding-a-dependency.md`
- a build gate is blocking you → `docs/build-gates.md`
- question about record vs class, struct, or `required` → `docs/data-models.md`

## Checklist

- [ ] Request/response types are `public`
- [ ] Types registered in `ApiJsonSerializerContext`
- [ ] `ValidationProblem` declared in the handler's return type
- [ ] No reference to another slice
- [ ] One `Map<Slice>()` line in `Program.cs`
- [ ] Unit tests mirror the source folder
- [ ] Integration tests assert at least one 400
- [ ] `openapi.json` diff reviewed and committed
