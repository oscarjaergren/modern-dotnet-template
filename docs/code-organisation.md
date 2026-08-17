# Code organisation

**Read this if:** two slices need the same code, you are adding something that is not a slice, or you
are wondering why the layout is like this.

## What a slice is

One folder under `src/Api/Features/` owning everything for one feature: the endpoint, its request
and response types, and its logic. `Greetings` is the reference implementation.

The alternative is organising by technical layer — `Controllers/`, `Services/`, `Repositories/` —
where a single change is spread across four folders and you must hold the whole structure in your
head to touch any of it. Slices trade that for locality: a feature is one directory, and deleting it
is `rm -rf` on that directory.

This is also the property that makes the codebase workable for an agent. Extending the API means
reading one folder, not the repository.

## The rule that makes it real

**A slice may not reference another slice.** `Api.ArchitectureTests` discovers every namespace under
`Api.Features.*` and asserts pairwise isolation, so a newly added slice is covered automatically and
nobody has to remember to update the test.

Without enforcement this is a convention that decays — one "temporary" cross-reference at a time
until the folders are decorative. With enforcement it is a constraint that forces a better question
when two slices want the same code: *where does this actually belong?*

If you change the guard, verify it still fails. Introduce a deliberate cross-slice reference, watch
the test fail naming the exact pair, then revert. An unverified guard is decoration — see
[build-gates.md](build-gates.md).

## Where shared code goes

Not in another slice. Out of `Features/` entirely.

`src/Api/Infrastructure/` is the worked example — `ProblemDetailsExceptionHandler` lives there
because it is genuinely cross-cutting and belongs to no feature. Anything that several slices need,
or that is about the application rather than a feature, goes in a sibling namespace outside
`Features/`.

A useful test before you move something: **if only one slice needs it, it is not shared** — it is
that slice's code, and moving it out makes both harder to read. Wait for the second caller.

If shared code grows past a folder into something with its own dependencies and lifecycle, promote it
to a project in `src/`. That is a real threshold, not a formality; most templates do not reach it.

## Registration is explicit

Each slice exposes one extension method and `Program.cs` calls it:

```csharp
app.MapPing();
app.MapGreetings();
```

No assembly scanning, no attribute discovery, no source-generated registration. Every route the
application serves is visible in one file, and finding the code behind a route is one grep.

The cost is a line per slice, written by hand. That is the whole cost, and it buys a codebase where
"what endpoints exist?" is answerable by reading rather than by running.

## What is deliberately not here

There is no dispatch library — no MediatR, no Wolverine. The `Features/` layout gives you slice
co-location, which is the part that makes the code navigable; handler discovery and a request
pipeline are a separate and much larger decision. See
[adding-a-dependency.md](adding-a-dependency.md).

There is no `Domain/`, `Application/`, or `Infrastructure/` layering. There is an `Infrastructure/`
folder, but it holds cross-cutting application plumbing, not a layer in the onion sense — nothing
depends on it in one direction by policy.

## Tests mirror the source

`tests/Api.UnitTests/Features/Greetings/` tests `src/Api/Features/Greetings/`. The shapes are
identical on purpose: asked to "add tests for slice X", there is exactly one place the file goes and
no judgement required.

Integration tests follow the same layout and assert against the wire format rather than the C# types,
so they stay honest when internals are renamed.
