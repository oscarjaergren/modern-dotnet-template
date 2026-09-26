# Native AOT

**Read this if:** a trim or AOT warning is failing your build, something works locally but crashes
in the published binary, or you want to remove the AOT gate.

## What is on

`src/Api` publishes with `PublishAot=true` and `InvariantGlobalization=true`. `IL2026`, `IL3050`
and `IL2091` are errors, and CI runs the published binary against every endpoint, because AOT
failures show up at runtime rather than at publish. It is on by default because it costs almost
nothing without a data layer, and removing it later is four lines while retrofitting it is painful.

## Constraints

- No reflection over types that aren't statically referenced; no `Activator.CreateInstance`.
- No `Reflection.Emit`, runtime expression compilation or dynamic proxies.
- System.Text.Json source generation only. Every wire type goes in `ApiJsonSerializerContext.cs`; a
  missing entry fails at runtime.
- `WebApplication.CreateSlimBuilder`, not `CreateBuilder`.
- No culture-aware formatting or comparison.

## When a warning fires

`IL2026` means a called method needs unreferenced code; `IL3050` means it needs dynamic code. In
order of preference: use an AOT-safe overload in the same library, use a different library
([adding-a-dependency.md](adding-a-dependency.md)), or remove the gate deliberately.

**Never suppress the warning.** That doesn't make the code work; it moves the failure to runtime.

## Removing the gate

Most likely when adding EF Core; see [adding-a-database.md](adding-a-database.md).

1. `src/Api/Api.csproj`: delete `<PublishAot>true</PublishAot>`.
2. `src/Api/Api.csproj`: optionally delete `<InvariantGlobalization>true</InvariantGlobalization>`.
3. `.github/workflows/ci.yml`: delete the `aot-container` job, or keep the container and drop the
   AOT expectations.
4. `.editorconfig`: optionally relax `IL2026`, `IL3050`, `IL2091`.

Keep `CopyOutputSymbolsToPublishDirectory=false`. Nothing else depends on AOT.

## Debug symbols

AOT writes symbols to a separate `Api.dbg`, several times the size of the binary, and the SDK copies
it into the publish folder, and so into the image. `CopyOutputSymbolsToPublishDirectory=false` stops
that; filtering `ResolvedFileToPublish` does not, because the `.dbg` never passes through it. The
symbols stay in `artifacts/bin/Api/release_linux-x64/native/`, and CI uploads them.

## VerifyReferenceAotCompatibility is off on purpose

It checks that each referenced assembly is *annotated* as AOT-compatible, and here it produced 104
`IL3058` errors: most of the ASP.NET Core shared framework and Polly, none of which carry the
annotation. The same build publishes with zero AOT warnings, because the publish-time analysis
checks the code you actually reach. The property suits libraries with a few curated references, not
an app that references the whole framework.

## Size tuning

Off by default. `OptimizationPreference=Size` (modest), `IlcFoldIdenticalMethodBodies=true` (small,
safe), `UseSystemResourceKeys=true` (drops exception message text). `StackTraceSupport=false` is the
biggest win and is deliberately not a default: it costs production crash diagnostics.

## Prerequisites

AOT needs `clang` and the system linker. Without them, publish fails at "Generating native code"
with a confusing linker error. The devcontainer and CI install both:

```bash
sudo apt-get install -y clang zlib1g-dev
```
