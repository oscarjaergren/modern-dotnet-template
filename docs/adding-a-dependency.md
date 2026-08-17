# Adding a dependency

**Read this if:** you are about to add a NuGet package — or you are wondering why some obvious
library is missing.

## The bar is deliberately high

This is a template. Everything in it is inherited by every project generated from it, and every
dependency is something a consumer must either accept or remove. The value of the template is
substantially in what it leaves out.

That is not a reason never to add one. It is a reason to be able to answer: *what does every
consumer inherit, and would they have chosen it themselves?*

## Four checks before you add anything

**1. Native AOT.** The API publishes with `PublishAot=true` and CI fails on any trim or AOT warning.
A package that uses reflection over non-statically-referenced types, `Reflection.Emit`, or runtime
expression compilation will fail the build. Suppressing the warning is not a fix — see
`docs/native-aot.md`.

**2. Licence.** Check it is genuinely permissive and stays that way. Two widely-used .NET libraries
moved to commercial licences recently, which turned an aesthetic preference into a procurement
question for everyone who had adopted them by default. `.github/workflows/dependency-review.yml`
denies GPL and AGPL, but licence *changes* on existing packages are not caught automatically.

**3. Advisories.** NuGet audit runs on restore and `TreatWarningsAsErrors` makes findings fatal, so a
package with a known advisory will fail the build rather than warn. This is not hypothetical — see
`docs/build-gates.md`.

**4. Central Package Management.** Versions live in `Directory.Packages.props`. `PackageReference`
entries in project files carry **no** `Version` attribute. Transitive pinning is enabled, so you can
override a vulnerable transitive package by adding a `PackageVersion` for it without taking a direct
dependency.

## What was considered and left out

Each of these is a real opinion, and in most cases the reason is the same: **the style propagates.**
Agents and developers copy whatever the existing code does, so the first usage becomes the
codebase's convention permanently. That makes the default unusually expensive.

**Dispatch libraries (MediatR, Wolverine).** The `Features/` layout gives you slice co-location,
which is the part that makes the codebase navigable. Handler discovery and a request pipeline are a
separate and much larger decision. Endpoints register through one explicit `app.Map<Slice>()` call
per slice, so every route is greppable from `Program.cs` and there is no assembly scanning.

**Assertion libraries (FluentAssertions, Shouldly).** xUnit v3's built-in assertions only. Failure
messages are less descriptive, which is why test names read as sentences —
`Post_greetings_rejects_invalid_input` carries the weight instead. Adding Shouldly or
AwesomeAssertions later is one package reference; removing a library whose style has spread through
fifty tests is not.

**Validation libraries (FluentValidation).** .NET 10's built-in validation is source-generated and
therefore AOT-safe, which the alternatives are not. It uses plain DataAnnotations. There are two
sharp edges in how it is wired — both documented in `AGENTS.md`, and both fail *silently*.

**Result libraries (ErrorOr, FluentResults, OneOf, LanguageExt).** The policy that expected failures
are returned rather than thrown is real and enforced — see
[errors-and-failures.md](errors-and-failures.md). The *library* is not, for two reasons: at the HTTP
boundary `Results<T1, T2, T3>` is already built in, and C# 15 union types ship with .NET 11 in
November 2026 and make this whole category largely redundant. Adopting one now buys a migration
later. That page has the full comparison if you need something before then.

**API documentation UIs (Scalar, Swagger UI).** The OpenAPI document is generated at build, committed
to the repo, and drift-gated in CI — that is the contract, and it is what feeds client generators and
contract linting. A browser UI is a development convenience with substitutes; `src/Api/Api.http`
covers manual endpoint exercising with no dependency. Aspire's dashboard does *not* provide one, so
dropping it means genuinely having none. Adding Scalar back is one package and one line.

**Messaging, auth, multi-tenancy, cloud SDKs, IaC.** Out of scope for a baseline template.

## If you disagree with one of these

Open an issue arguing the case rather than a PR adding the package. The reasoning above is not
sacred, but it should be argued with rather than routed around — and the conversation is faster than
a review.
