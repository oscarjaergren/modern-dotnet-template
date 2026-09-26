# Adding a database

**Read this if:** you are adding EF Core, Dapper, Marten, Postgres, or any other persistence. This
page is self-contained.

## Why there isn't one

Persistence is the most opinionated and most entangled choice a service makes: migrations,
transactions, test strategy and AOT all follow from it. So there is no ORM, database or repository
abstraction to rip out. Having no data layer is also what keeps the Native AOT gate cheap.

## Decide about AOT first

**EF Core and Native AOT are either/or.** Microsoft describes EF Core's AOT support as experimental
and unsuited to production, and it rules out dynamic query composition.

- **Keep AOT:** use persistence without runtime code generation, usually Dapper with
  source-generated mapping.
- **Drop AOT:** a supported exit, and the right call for most teams adding EF Core. Do it in its own
  commit:
  1. `src/Api/Api.csproj`: delete `<PublishAot>true</PublishAot>`.
  2. `src/Api/Api.csproj`: optionally delete `<InvariantGlobalization>true</InvariantGlobalization>`.
  3. `.github/workflows/ci.yml`: delete the `aot-container` job, or keep the container and drop the
     AOT expectations.
  4. `.editorconfig`: optionally relax `IL2026`, `IL3050`, `IL2091`.

  Keep `CopyOutputSymbolsToPublishDirectory=false`. Nothing else depends on AOT.

For EF Core migrations, run `dotnet-ef` with `dotnet tool exec` rather than adding it to
`.config/dotnet-tools.json`: a second tool there breaks `dotnet tool restore` on every fresh machine
(see the trap in `AGENTS.md`).

## Wiring it through Aspire

One line in `src/AppHost/AppHost.cs` gives you a containerised database:

```csharp
var db = builder.AddPostgres("postgres").AddDatabase("appdb");

builder.AddProject<Projects.Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health");
```

Add the client integration to `src/Api` (for example `Aspire.Npgsql`, with its version in
`Directory.Packages.props`) and call `builder.AddNpgsqlDataSource("appdb")`. Aspire injects the
connection string; don't put one in `appsettings.json`.

## What changes for tests and CI

Today `aspire run` and the integration tests need no container runtime. A database resource changes
that: Docker or Podman must be running locally, and CI gets slower (GitHub's `ubuntu-latest` has
Docker, so it keeps working). Add the database to the health wait in `ApiFixture`, and prefer
Aspire's throwaway container per run over a shared instance.

## Where the code goes

Persistence belongs to the slice that uses it; don't create a `Repositories/` folder. Anything
several slices share, such as a `DbContext`, goes outside `Api.Features`, because slices may not
reference each other. `src/Api/Persistence/` is a reasonable home.
