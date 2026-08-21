# Docs

Reference material, read on demand. Day-to-day conventions live in
[`AGENTS.md`](../AGENTS.md) — that is the always-loaded file, and it carries the same index as below.

| Read this if you're…                                                         | Page                                                         |
| ---------------------------------------------------------------------------- | ------------------------------------------------------------ |
| considering a plugin or proxy that promises token savings                    | [token-saving-tools.md](token-saving-tools.md)               |
| working with an agent here, or your token bill is too high                   | [ai-workflow.md](ai-workflow.md)                             |
| writing C# here, or wondering why the code looks like it does                | [code-style.md](code-style.md)                               |
| a hook blocked your commit, or you're adding a check                         | [linting-and-hooks.md](linting-and-hooks.md)                 |
| defining a request, response, value object, or anything that holds data      | [data-models.md](data-models.md)                             |
| an operation can fail, or you're about to `throw`                            | [errors-and-failures.md](errors-and-failures.md)             |
| two slices need the same code, or you're adding something that isn't a slice | [code-organisation.md](code-organisation.md)                 |
| adding EF Core, Dapper, Postgres, or any persistence                         | [adding-a-database.md](adding-a-database.md)                 |
| adding a NuGet package, or wondering why some library is missing             | [adding-a-dependency.md](adding-a-dependency.md)             |
| hit by a trim/AOT warning, or removing the AOT gate                          | [native-aot.md](native-aot.md)                               |
| blocked by a build gate, or changing what's enforced                         | [build-gates.md](build-gates.md)                             |
| changing the container image, or working out how to deploy                   | [containers-and-deployment.md](containers-and-deployment.md) |
| changing any doc or instruction file, or looking for the ADRs                | [documentation-approach.md](documentation-approach.md)       |

## How these are written

Pages are **living** — corrected in place, never superseded and archived. They are named by the
**task that makes you open them**, each **self-contained** enough to finish that task without
opening a second one, and short enough to be read whole. Some content is therefore repeated across
pages on purpose: a missed cross-reference costs more than a duplicated paragraph.

Deleting is part of maintaining them. A page that stops being true gets rewritten or removed, not
left beside its replacement.

There are no architecture decision records. ADRs are immutable by design, so a decision log only
grows and answering "what is true now?" means replaying it — bad for a reader, worse for an agent
retrieving one page at a time.
[documentation-approach.md](documentation-approach.md) lists the full requirements this was chosen
against, and the cost: a stale ADR is still correct, a stale living document is just wrong.

Measured figures are deliberately absent — CI reports current numbers on every run, and numbers
written into documentation go stale silently. History comes from `git log`.
