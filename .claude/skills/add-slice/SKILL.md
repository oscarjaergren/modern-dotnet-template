---
name: add-slice
description: Add a new vertical slice (feature) to the API — folder, endpoint, request/response types, validation, JSON registration, unit test, and integration test. Use whenever adding a new endpoint or feature to this template.
---

# Add a vertical slice

Copy `src/Api/Features/Greetings/`. Steps 2 and 3 are where the silent failures are (see the traps in
`AGENTS.md`).

1. **Folder:** `src/Api/Features/<Slice>/`, a plural noun (`Orders`).

2. **Request and response types**, one file each, **`public`**, validated with DataAnnotations:

   ```csharp
   public sealed record <Thing>Request
   {
       [Required]
       [StringLength(50, MinimumLength = 1)]
       public required string Name { get; init; }
   }
   ```

3. **Register them** in `src/Api/ApiJsonSerializerContext.cs`:

   ```csharp
   [JsonSerializable(typeof(<Thing>Request))]
   [JsonSerializable(typeof(<Thing>Response))]
   ```

4. **Endpoint**, `internal`, with a return type naming every outcome:

   ```csharp
   internal static class <Slice>Endpoint
   {
       internal static IEndpointRouteBuilder Map<Slice>(this IEndpointRouteBuilder app)
       {
           app.MapPost("/<slice>", Handle).WithName("<Name>").WithSummary("<One line.>");
           return app;
       }

       private static Results<Ok<<Thing>Response>, ValidationProblem> Handle(<Thing>Request request) =>
           TypedResults.Ok(new <Thing>Response(/* ... */));
   }
   ```

   Declare `ValidationProblem` even though the handler never returns it: that documents the 400. For
   a `TypedResults.Problem(...)` outcome, add the matching `.ProducesProblem(status)`.

5. **Logic** that doesn't need HTTP goes in its own file, as a static function unless something needs
   substituting. Never reference another slice; shared code moves out of `Features/`.

6. **Register the route:** one `app.Map<Slice>();` line in `Program.cs`.

7. **Unit tests** in `tests/Api.UnitTests/Features/<Slice>/`: built-in assertions, names as sentences.

8. **Integration tests** in `tests/Api.IntegrationTests/Features/<Slice>/`, with
   `[Collection(nameof(ApiCollection))]`. Assert the wire format with `JsonDocument`, and assert **at
   least one 400 per validated field**: that is the only thing that catches a non-public request type.

9. **Verify:** `dotnet build && dotnet test && dotnet format --verify-no-changes`, then read the
   `src/Api/openapi.json` diff. It is the API contract; commit it.
