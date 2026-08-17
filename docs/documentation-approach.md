# Documentation approach

**Read this if:** you are adding or changing anything in `docs/`, `AGENTS.md`, `CLAUDE.md`, or a
skill — or you are looking for the architecture decision records and cannot find them.

## What the approach has to satisfy

These are the requirements the format was chosen against. If you are proposing a change, argue
against these rather than against the shape that fell out of them.

**1. Living, not immutable.** Pages are corrected in place. The current state of the repository must
be readable directly, not reconstructed by replaying a chain of superseding records.

**2. Bounded, not accumulating.** Total volume must not grow monotonically. Content that stops being
true is deleted or rewritten, never archived alongside its replacement.

**3. Cheap to retrieve.** Only what prevents mistakes is always in context. Everything else is read
on demand, when a task actually touches it.

**4. Self-contained.** One page finishes one task. A reader must not need a second page to act.

**5. Short enough to read whole.** Agents silently truncate long pages, so a page that cannot be read
in one go is a page that will be half-read without anyone noticing.

**6. Findable without luck.** Discovery goes through a deterministic index in the always-loaded
file, not through a convention a tool may or may not know about.

**7. One source of truth.** No parallel decision log to keep in sync. Git carries history.

**8. Serves humans and agents from the same files.** This one is a compromise rather than a clean
win — see below.

## Why not ADRs

They fail requirements 1 and 2, structurally rather than through misuse.

An ADR is **immutable by design** — you do not edit an accepted one, you write a new record that
supersedes it. That is the mechanism, not a convention people are sloppy about. So a decision log
only ever grows, and answering "what is true now?" means reading a record and then checking whether
anything later overruled it.

That accumulation is bad for a human and worse for an agent. An agent retrieving from `docs/`
should hit the page that is true; in an ADR directory it hits *N* records of which most are history,
spends context on superseded reasoning, and can act on a decision that was reversed two years ago.
Bloat is not a tidiness complaint here — it is a correctness risk.

The clearest tell was measured figures. An early draft recorded image and binary sizes inside records
that, by their own rules, could not be edited. Those go stale the first time anyone adds a package,
and the only sanctioned fix is a new record announcing the new number.

**The cost, stated plainly:** an out-of-date ADR is still *correct*, because it only ever claimed to
describe a moment in time. An out-of-date living document is simply *wrong*, and nothing signals it.
That is the price of requirement 1, and it is paid deliberately.

Two things mitigate it. Code comments point at the page that explains them, so editing
`src/Api/Api.csproj` surfaces the pointer to `native-aot.md`. And measured figures stay out of the
docs entirely — CI prints current numbers on every run.

Several established alternatives were considered. arc42 and Simon Brown's software guidebook assume
linear reading of one large document, which fails requirements 3 and 5. Diátaxis organises by
document purpose, which splits a task across pages and fails requirement 4. Oxide-style RFDs keep
provenance while allowing edits, and are a reasonable choice if you want that. What is here borrows
Diátaxis's insight that "how to do X" and "what X is" differ, but applies it as *layers that load at
different times* rather than as sibling folders.

## Requirement 8: humans and agents want different things

Worth being honest that this is a real tension, not a solved problem. Tokens are expensive and an
agent's context window is a shared resource, so what is good for one reader is not automatically
good for the other.

Agents get: task-scoped pages, self-containment, deliberate duplication, no narrative, an index
optimised for retrieval rather than browsing.

Humans lose: there is no story of the project, no linear read-through, and reading `docs/` end to
end feels repetitive because the same constraint is restated wherever it applies.

Humans keep: prose rather than structured fragments, the README as a front door, and the same
trigger index — which turns out to work for a person who arrives with a question too.

The compromise is that agents set the *structure* and humans set the *register*. If the two ever
genuinely conflict on a specific page, favour the agent — a human can skim past redundancy, whereas
an agent that reads half a truncated page acts on it.

## The layers

Requirement 3, made concrete. The split is by **when a file is read**, not by subject:

| Layer | Loaded | Holds |
|---|---|---|
| `AGENTS.md` | always | conventions, commands, traps, and the index pointing here |
| `.claude/skills/*/SKILL.md` | when the task matches | how to *do* a specific thing |
| `docs/*.md` | on demand, via the index | what something *is* and why |

The rule of thumb: **if it describes how to do something it is a skill; if it describes what
something is, it is a document.** If getting it wrong would break the build or fail silently, it
belongs in `AGENTS.md` regardless of length — a trap nobody reads about is not mitigated.

## How that shapes a page

**Named for the trigger, not the subject** (requirement 4). The useful unit is "the smallest context
that finishes this task". Organising by subject splits `adding-a-database` across three pages and
loses the one fact that matters — that the AOT gate has to go.

**Self-contained, including duplication** (requirement 4). `adding-a-database.md` repeats the AOT
removal steps from `native-aot.md` on purpose. A missed cross-reference costs more than a repeated
paragraph.

**Under about 150 lines** (requirement 5). Past that it is usually two tasks wearing a trenchcoat.

**Indexed in `AGENTS.md`** (requirement 6). A page nothing points at is a page nothing reads.

## Why AGENTS.md is canonical and CLAUDE.md imports it

`AGENTS.md` is the cross-vendor convention, read natively by most agent tooling. Claude Code reads
`CLAUDE.md` and does not read `AGENTS.md` natively, so `CLAUDE.md` is a single `@AGENTS.md` import
plus Claude-specific notes.

A symlink was rejected: it breaks on Windows clones without `core.symlinks` and renders as a
confusing one-line file on GitHub. Duplicating the content was rejected because duplicated
instruction files always drift — requirement 7.

The same relationship holds one level down: `docs/` is canonical and skills point at it rather than
restate it. If Claude Code gains native `AGENTS.md` support, delete `CLAUDE.md` and nothing else
changes.

## Adding to a layer

- **A trap that fails silently** → `AGENTS.md`, and add a test that catches it.
- **A procedure** → a skill if it will be performed repeatedly; otherwise a `docs/` page.
- **A constraint or piece of reasoning** → the `docs/` page for the task that would make someone hit
  it, and a row in the `AGENTS.md` index.

Changing behaviour means updating the page that describes it in the same commit. A page describing
the old behaviour is worse than no page.

Deleting is part of the job. If a page stops being true, rewrite or remove it — do not leave it
beside its replacement. That is requirement 2, and it is the one most easily forgotten.
