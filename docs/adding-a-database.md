# Adding a database

**Read this if:** you are adding EF Core, Dapper, Marten, Postgres, or any other persistence.

This page is self-contained. You should not need to open another one to finish the job.

## Why there isn't one already

Persistence is the most opinionated choice in a service and the one most likely to differ between
projects. It is also the most entangled — migrations, transactions, testing strategy, and Native AOT
compatibility all follow from it. A template that picks for you is only useful to people who agree
with the pick.

So there is no ORM, no database, no repository abstraction, and no `IUnitOfWork`. There is nothing
to rip out and no abstraction to fight.

It also has a load-bearing side effect: with no data layer, Native AOT is trivially satisfiable,
which is what makes the AOT gate viable at all.

## The decision you have to make first

**EF Core and Native AOT are an either/or.** Microsoft's own documentation describes EF Core's AOT
support as highly experimental and unsuited to production, and forbids dynamic query composition.

So before writing any code, pick one:

- **Keep AOT** → use a persistence approach that works without runtime code generation, or accept
  significant constraints. Dapper with source-generated mapping is the usual answer.
- **Drop AOT** → follow the removal steps below. This is a supported exit, not a failure. It is
  documented precisely because it is the most likely reason someone reverses a default here.

Most teams adding EF Core should drop AOT. Do it deliberately and in its own commit.

## Removing the AOT gate

Four changes, all small:

1. `src/Api/Api.csproj` — delete `<PublishAot>true</PublishAot>`.
2. `src/Api/Api.csproj` — optionally delete `<InvariantGlobalization>true</InvariantGlobalization>`
   if you need culture-aware formatting.
3. `.github/workflows/ci.yml` — delete the `aot-container` job, or keep the container publish and
   drop only the AOT expectations.
4. `.editorconfig` — optionally relax `IL2026` / `IL3050` / `IL2091` from `error`.

Keep `<CopyOutputSymbolsToPublishDirectory>false</CopyOutputSymbolsToPublishDirectory>`. It is still
correct without AOT and keeps debug symbols out of the runtime image.

Nothing else depends on AOT. Slices, gates, tests, and container publishing all work unchanged.
`docs/native-aot.md` covers the constraints in more detail if you would rather keep the gate.

## Wiring the database through Aspire

Aspire is already orchestrating the app, so a containerised database is one line in
`src/AppHost/AppHost.cs`:

```csharp
var db = builder.AddPostgres("postgres").AddDatabase("appdb");

builder.AddProject<Projects.Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health");
```

Add the matching client integration package to `src/Api` (for example `Aspire.Npgsql`) and register
it with `builder.AddNpgsqlDataSource("appdb")`. Aspire injects the connection string; do not add one
to `appsettings.json`.

Remember to add the package version to `Directory.Packages.props` — this repo uses Central Package
Management, so `PackageReference` entries carry no `Version` attribute.

## What this changes about your test and CI setup

This is the part people miss.

Right now the AppHost has only a project resource, so `aspire run` and the integration tests need
**no container runtime**. The moment you add a database resource, both need Docker or Podman.

- Local: a container runtime must be running before `aspire run` or `dotnet test`.
- CI: GitHub's `ubuntu-latest` runners have Docker preinstalled, so `.github/workflows/ci.yml` keeps
  working — but the `build-and-test` job gets slower and can now fail for infrastructure reasons.
- Integration tests will need to wait for the database to be healthy. `ApiFixture` already calls
  `WaitForResourceHealthyAsync("api")`; add the database resource to that wait.

Consider whether the integration tests should get a fresh database per run. Aspire gives you a
throwaway container, which is usually better than a shared instance and per-test cleanup.

## Where the code goes

Persistence belongs to the slice that uses it. Do not create a `Repositories/` folder — that is the
layered layout this template deliberately avoids, and `Api.ArchitectureTests` will not stop you but
the structure will fight you.

If several slices genuinely share persistence concerns (a `DbContext`, say), put it in a namespace
**outside** `Api.Features`, because slices may not reference each other. A `src/Api/Persistence/`
folder is a reasonable home.
