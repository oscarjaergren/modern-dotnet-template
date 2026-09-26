# Docs

Read on demand. The always-loaded conventions are in [`AGENTS.md`](../AGENTS.md), which carries the
same index.

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

Each page is named for the task that sends you to it, finishes that task on its own, and is
corrected in place rather than superseded. Repetition across pages is accepted: a missed
cross-reference costs more than a repeated paragraph. A page that stops being true is rewritten or
deleted. There are no ADRs; [documentation-approach.md](documentation-approach.md) says why.

A measured figure needs an answer to "what keeps this true?": a CI assertion, or a dated source. A
number typed in once is not allowed, because nothing will say when it stops being true.
