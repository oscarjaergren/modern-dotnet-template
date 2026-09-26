# Documentation approach

**Read this if:** you are changing `docs/`, `AGENTS.md` or a skill, or looking for the ADRs.

## Requirements

Argue with these, not with the shape that fell out of them.

1. **Living.** Pages are corrected in place; the current state is readable directly.
2. **Bounded.** Volume does not grow monotonically. Untrue content is deleted, not archived.
3. **Cheap to retrieve.** Only what prevents mistakes is always loaded; the rest is read on demand.
4. **Self-contained.** One page finishes one task.
5. **Short enough to read whole.** Agents truncate long pages without saying so.
6. **Findable.** A deterministic index in the always-loaded file, not a convention.
7. **One source of truth.** No parallel decision log. Git carries history.
8. **Humans and agents read the same files.** A compromise, below.

## Why not ADRs

ADRs fail 1 and 2 by design. An accepted record is never edited, only superseded, so the log only
grows and "what is true now?" means replaying it. For an agent retrieving one page at a time, that
is a correctness risk: it can act on a decision reversed years ago.

The cost of the alternative: a stale ADR is still correct about its moment; a stale living page is
simply wrong, and nothing signals it. Two things mitigate that: code comments point at the page that
explains them, and nothing goes in without something that keeps it true (below).

arc42 and Simon Brown's guidebook assume linear reading (fails 3 and 5). Diátaxis splits a task
across pages by purpose (fails 4). Oxide-style RFDs are reasonable if you want provenance with
edits. This borrows Diátaxis's how-to versus what-is distinction, but as layers that load at
different times.

## Humans and agents

Agents set the structure: task-scoped, self-contained pages, deliberate repetition, an index built
for retrieval. Humans set the register: prose, a README front door. Humans lose a linear story, and
reading `docs/` end to end feels repetitive. Where the two conflict, favour the agent. A person
skims past redundancy; an agent acts on half a truncated page.

## Layers

Split by when a file is read, not by subject:

| Layer                       | Loaded                | Holds                             |
| --------------------------- | --------------------- | --------------------------------- |
| `AGENTS.md`                 | always                | commands, rules, traps, the index |
| `.claude/skills/*/SKILL.md` | when the task matches | how to *do* one thing             |
| `docs/*.md`                 | on demand via index   | what something *is*, and why      |

A procedure is a skill; an explanation is a doc. Anything that breaks the build or fails silently
goes in `AGENTS.md` as one line, because a trap nobody reads about is not mitigated. Keep
`AGENTS.md` short: it is re-sent on every turn of every session.

## Measured figures

A number needs an answer to "what keeps this true?": a CI assertion, or a dated source. A number
typed in once is not allowed, because nothing will say when it stops being true. The same goes for
counts and lists that restate something the repo already says.

## Page rules

- **Named for the trigger.** "Adding a database", not "persistence": the task is the unit.
- **Self-contained.** `adding-a-database.md` repeats the AOT removal steps from `native-aot.md` on
  purpose.
- **Under about 150 lines.** Past that it is usually two tasks.
- **Indexed in `AGENTS.md`.** A page nothing points at is a page nothing reads.

## No CLAUDE.md

`AGENTS.md` is the cross-vendor convention, and Claude Code has read it natively since v2.1.277, so
there is no `CLAUDE.md`. That has a silent failure mode, recorded in the `AGENTS.md` traps: any
`CLAUDE.md` or `CLAUDE.local.md` in the tree takes precedence, and older versions read nothing. The
fix for either is a `CLAUDE.md` containing `@AGENTS.md`, never a copy, which would drift.

## Adding something

- A silent trap: one line in `AGENTS.md`, plus a test that catches it.
- A repeated procedure: a skill. A one-off: a `docs/` page.
- A constraint or its reasoning: the page for the task that would hit it, plus an index row.

Change a page in the same commit as the behaviour it describes, and delete what stops being true.
