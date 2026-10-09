# Adding a dependency

**Read this if:** you are about to add a NuGet package, or wondering why an obvious library is
missing.

The bar is high because every project generated from this template inherits the package. Ask: would
every consumer have chosen this themselves?

## Four checks

1. **Native AOT.** CI fails on any trim or AOT warning, and suppressing one is not a fix; see
   [native-aot.md](native-aot.md).
2. **Licence.** Permissive, and likely to stay so: two popular .NET libraries recently went
   commercial. Dependency review blocks GPL and AGPL, but not a licence change on an existing
   package.
3. **Advisories.** NuGet audit runs on restore and its findings fail the build.
4. **Central Package Management.** The version goes in `Directory.Packages.props`; the
   `PackageReference` has no `Version`. A vulnerable transitive package can be overridden there
   without a direct dependency.

## Left out, and why

The common reason: **style propagates**. People and agents copy what the code already does, so the
first use of a library becomes the convention.

- **Dispatch (MediatR, Wolverine).** Slices give the navigability; a pipeline is a bigger decision.
  Routes stay explicit in `Program.cs`.
- **Assertions (FluentAssertions, Shouldly).** xUnit v3's built-ins, with sentence-style test names
  carrying the explanation. Adding one later is easy; removing one from fifty tests isn't.
- **Validation (FluentValidation).** The built-in validation is source-generated and AOT-safe. Its
  two wiring traps are in `AGENTS.md`.
- **A second result type (FluentResults, OneOf).** The template uses `ErrorOr`; see
  [errors-and-failures.md](errors-and-failures.md).
- **API docs UI (Scalar, Swagger UI).** The committed `openapi.json` is the contract; `Api.http`
  covers manual testing. Aspire's dashboard has no UI for it, so you'd have none, but Scalar is one
  package and one line away.
- **Messaging, auth, multi-tenancy, cloud SDKs, IaC.** Out of scope.

If you disagree, open an issue arguing the case rather than a PR adding the package.
