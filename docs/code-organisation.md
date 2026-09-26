# Code organisation

**Read this if:** two slices need the same code, you are adding something that isn't a slice, or you
wonder why the layout is like this.

## Slices

A slice is one folder under `src/Api/Features/` that owns a feature: endpoint, request and response
types, and logic. `Greetings` is the reference. Organising by layer (`Controllers/`, `Services/`,
`Repositories/`) spreads one change across four folders; a slice keeps it in one, so deleting a
feature is deleting a directory, and an agent extending the API reads one folder, not the repo.

## Slices never reference each other

`Api.ArchitectureTests` enforces it, and discovers slices by namespace, so a new slice is covered
automatically. A companion test fails when there are fewer than two slices, because with one the
rule passes vacuously. If you delete both samples before you have two real slices, expect that
failure until you do.

If you change the rule, prove it still fails: add a deliberate cross-slice reference, watch the test
name it, then revert.

## Where shared code goes

Out of `Features/` entirely, never into another slice. `src/Api/Infrastructure/` is the example:
`ProblemDetailsExceptionHandler` belongs to no feature. Before moving anything, wait for the second
caller: code only one slice uses is that slice's code. When shared code grows its own dependencies
and lifecycle, promote it to a project under `src/`.

## Explicit registration

Each slice exposes one extension method, and `Program.cs` calls it:

```csharp
app.MapPing();
app.MapGreetings();
```

No assembly scanning or attribute discovery, so every route is visible in one file. The cost is one
hand-written line per slice.

## Not here on purpose

No dispatch library (MediatR, Wolverine): slices give co-location, which is what makes the code
navigable, and a request pipeline is a separate, larger decision; see
[adding-a-dependency.md](adding-a-dependency.md). No onion layering; `Infrastructure/` is plumbing,
not a layer.

## Tests mirror the source

`tests/Api.UnitTests/Features/Greetings/` tests `src/Api/Features/Greetings/`, so a new test has
exactly one place to go. Integration tests follow the same layout and assert the wire format, so they
survive renames.
