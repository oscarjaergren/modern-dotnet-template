# Native AOT

**Read this if:** a trim or AOT warning is failing your build, something works locally but crashes in
the published binary, or you want to remove the AOT gate.

## What is on and why

`src/Api` publishes with `PublishAot=true` and `InvariantGlobalization=true`. Trim and AOT
diagnostics (`IL2026`, `IL3050`, `IL2091`) are errors, and CI **runs the published binary** and
exercises the endpoints rather than just publishing it — AOT failures surface at runtime, so a
successful publish proves very little on its own.

It is on by default because it is nearly free while there is no data layer, and because retrofitting
it onto a mature codebase is painful while removing it is a four-line change.

## The constraints

- No reflection over types that are not statically referenced. No `Activator.CreateInstance`.
- No `Reflection.Emit`, runtime expression compilation, or dynamic proxy generation.
- System.Text.Json **source generation only**. Every type crossing the wire must be listed in
  `src/Api/ApiJsonSerializerContext.cs`; a missing entry fails at runtime, not at build.
- `WebApplication.CreateSlimBuilder`, not `CreateBuilder` — the slim host omits the reflection-heavy
  defaults.
- `InvariantGlobalization=true` means no culture-aware formatting or comparison.

## When a warning fires

Read it literally. `IL2026` means a called method is annotated `RequiresUnreferencedCode`; `IL3050`
means `RequiresDynamicCode`. Both mean the library does something that cannot survive trimming.

In order of preference:

1. **Use a different API in the same library** — often there is a source-generated or explicitly
   typed overload that is AOT-safe.
2. **Use a different library.** See `docs/adding-a-dependency.md`.
3. **Remove the AOT gate deliberately**, below.

**Do not suppress the warning.** A suppressed `IL2026` does not make the code work; it makes the
failure move to runtime, where it appears as a confusing `MissingMetadataException` in production
rather than a clear error at build time. That trade is never worth it.

## Removing the gate

Legitimate, documented, and most likely if you are adding EF Core — see `docs/adding-a-database.md`,
which covers the persistence side.

1. `src/Api/Api.csproj` — delete `<PublishAot>true</PublishAot>`.
2. `src/Api/Api.csproj` — optionally delete `<InvariantGlobalization>true</InvariantGlobalization>`.
3. `.github/workflows/ci.yml` — delete the `aot-container` job, or keep the container publish and
   drop the AOT expectations.
4. `.editorconfig` — optionally relax `IL2026` / `IL3050` / `IL2091` from `error`.

Keep `<CopyOutputSymbolsToPublishDirectory>false</CopyOutputSymbolsToPublishDirectory>`; it is still
correct and keeps debug symbols out of the image.

Nothing else depends on AOT. Slices, gates, tests, and container publishing work unchanged.

## Debug symbols

AOT strips symbols into a separate `Api.dbg`, which the SDK copies into the publish folder by
default — so it ends up inside the container, several times larger than the binary it describes.
`CopyOutputSymbolsToPublishDirectory=false` prevents that.

Symbolication is not lost: the identical `Api.dbg` is still written to
`artifacts/bin/Api/release_linux-x64/native/`, and CI uploads it as an artifact.

Note that filtering `ResolvedFileToPublish` does **not** work for this — the native `.dbg` never
passes through that item group. Use the property.

## Size tuning

Not enabled by default. If you need a smaller binary:

| Setting | Effect |
|---|---|
| `OptimizationPreference=Size` | modest |
| `IlcFoldIdenticalMethodBodies=true` | small, safe |
| `UseSystemResourceKeys=true` | strips exception message text |
| `StackTraceSupport=false` | largest single win, **degrades production diagnostics** |

`StackTraceSupport=false` is deliberately not a default. Trading away crash diagnostics for a
single-digit percentage of binary size is a bad bargain for most services, and an especially bad one
to impose on everyone generating a project from a template.

## Prerequisites

Native AOT shells out to `clang` and the system linker on Linux. Without them, publishing fails at
the "Generating native code" step with a confusing linker error:

```bash
sudo apt-get install -y clang zlib1g-dev
```

The devcontainer and the CI workflow both install these.
