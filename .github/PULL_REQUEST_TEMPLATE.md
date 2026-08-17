## What and why

<!-- What changes, and what problem it solves. Link an issue if there is one. -->

## Checklist

- [ ] `dotnet build` is clean (warnings are errors)
- [ ] `dotnet test` passes — unit, architecture, and integration
- [ ] `dotnet format --verify-no-changes` reports no changes
- [ ] `src/Api/openapi.json` regenerated and committed if the API contract changed
- [ ] New slices do not reference other slices
- [ ] Request types bound from the body are `public` (an `internal` one silently disables validation)
- [ ] New wire types registered in `ApiJsonSerializerContext`
- [ ] No new dependency that breaks Native AOT

## Decisions

<!--
If this changes something documented in docs/, update that page in the same PR rather than
changing behaviour quietly — the pages are living documents, not a decision log. If it adds a
dependency, say why the template should carry it: see docs/adding-a-dependency.md, the bar is
deliberately high.
-->
