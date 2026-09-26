# AI workflow and token cost

**Read this if:** you are working on this repo with a coding agent, or your token bill is higher
than you expected.

What to **set once**, what to do **each session**, what to check **when something looks wrong**, and
the two traps that cost real money. Why any of it works is in
[token-saving-tools.md](token-saving-tools.md).

## Where the money goes

| Category                                                                | Share of spend |
| ----------------------------------------------------------------------- | -------------- |
| Cached system overhead (instructions, tool definitions, re-sent prefix) | 30–50%         |
| Tool I/O (file reads, command output)                                   | 30–45%         |
| Reasoning tokens                                                        | 10–30%         |
| Visible output                                                          | **1–10%**      |

Everything below follows from the last row: what the agent *says* is not the bill. What gets re-sent
on every turn is.

## Set once

*Claude-specific.* Every key verified against the settings reference.

### Yours: `~/.claude/settings.json`

These are personal. A project file setting `model` or `MAX_THINKING_TOKENS` would override the
choice of everyone who clones the repo, which is why this template ships neither.

```jsonc
{
  "$schema": "https://json.schemastore.org/claude-code-settings.json",

  // Mid-tier default. The top tier becomes a per-task decision, not a standing cost.
  "model": "sonnet",

  // TOKENS, not a percentage — range 100000-1000000. The default is model-tuned and fires
  // around 93% capacity. Setting this lower IS "compact earlier", applied automatically.
  "autoCompactEnabled": true,
  "autoCompactWindow": 300000,

  // On by default. The agent keeps its own notes across sessions, which is why this repo adds
  // no third-party memory plugin.
  "autoMemoryEnabled": true,

  "cleanupPeriodDays": 30,
  "includeCoAuthoredBy": true,

  "env": {
    // Caps extended thinking. Reasoning is 10-30% of spend and is easy to overspend on
    // mechanical work.
    "MAX_THINKING_TOKENS": "8000"
  }
}
```

### The repo's: `.claude/settings.json`

**There is no `.claudeignore`.** Plenty of advice says to write one; Claude Code has never read such
a file. Exclusion is a `Read` deny rule, and this repo ships them:

```jsonc
{
  "permissions": {
    "deny": [
      "Read(.env)", // a bare filename matches at any depth
      "Read(*.pfx)",
      "Read(*.user)",
      "Read(./artifacts/**)" // ./ anchors to this settings file's directory
    ]
  }
}
```

Rules use [gitignore pattern syntax](https://git-scm.com/docs/gitignore): `*` within a path segment,
`**` across directories. `UseArtifactsOutput` puts every build output under `artifacts/`, so one
rule covers what would otherwise be dozens of `bin/` and `obj/` folders — and a file never read
costs nothing, which is the cheapest saving available.

Know what these are not: deny rules apply to the built-in file tools on a best-effort basis, and
**do not stop a Bash command that opens the file itself**. They keep noise out of the window and
secrets out of casual reads. For an actual boundary, use the sandbox.

### Agent frontmatter

The largest single lever is one line of YAML. `.claude/agents/codebase-locator.md` is the worked
example — a "where is X?" agent on the cheapest tier:

```yaml
---
name: codebase-locator
description: Finds where things live in this repo. Use for "where is X?" questions.
tools: Read, Grep, Glob
model: haiku
isolation: worktree   # only needed for agents that WRITE
---
```

`model: haiku` because locating needs no judgement, and a subagent gets **its own context window**,
so the main thread pays only for the summary, never for the files that were opened. Match the tier
to the judgement required: locating, inventorying and mechanical edits are Haiku work; reviewing is
Sonnet work; architecture is worth the top tier.

`isolation: worktree` goes on every agent that writes. Two agents open the same file, both write
back, and **the second write silently erases the first** — no error, nothing in the diff. It costs
nothing, so the only reason to omit it is a read-only agent. Without built-in support:
`git worktree add ../feature-x -b feature-x`. Practical ceiling is 4–8 concurrent, past which you
are bottlenecked on reviewing output.

## Each session

| Do this                       | When                                                                 |
| ----------------------------- | -------------------------------------------------------------------- |
| Disconnect unused MCP servers | Start of a session. Tool selection degrades past 30–50 loaded tools. |
| Plan mode                     | Before anything complex. A wrong guess means re-reading everything.  |
| `/model`                      | Dropping a tier for a mechanical stretch.                            |
| `/clear`, or a new session    | Switching to unrelated work. The two are the same thing — see below. |
| `/compact`                    | Only mid-task, when you want control over what survives the summary. |

## When something looks wrong

| Check                           | Tells you                                                  |
| ------------------------------- | ---------------------------------------------------------- |
| `/context`                      | What is occupying the window right now.                    |
| `/cost`                         | Tokens and estimated spend for this session.               |
| Statusline gauge (`claude-hud`) | The window filling, live.                                  |
| `--verbose` for a working day   | What normal looks like — measure before changing anything. |
| `console.anthropic.com` → Usage | History across sessions.                                   |

A healthy long session shows the **cache-read ratio rising while per-turn input stays flat**. If
per-turn input climbs with the conversation, something is defeating the cache.

`/insights` is the odd one out, and worth a run every few weeks. It reads back your own transcript
history and writes `~/.claude/usage-data/report.html`: how you actually use the tool, where sessions
go wrong, features you have never touched, and copyable `AGENTS.md` rules derived from the friction
it found. It only sees sessions that were recorded — on a fresh install it reports zeros.

## Two traps

**Compaction is a setting, not a vigil.** Advice to watch a gauge and `/compact` at 60–70% fails for
the same reason `dotnet format` came off the pre-commit hook: a practice that depends on remembering
does not happen. `autoCompactWindow` does it for you. The trade-off is real, though — each pass
costs 100–200k tokens and rewrites the prefix, **invalidating the prompt cache**. Long sessions win,
because you stop carrying a huge prefix on every turn; short task-scoped sessions lose, because you
paid to reset a cache you were about to abandon. If yours are short, leave the default alone and end
sessions instead.

`/clear` is worth being blunt about: it is identical to opening a new session, not a technique. What
matters is not carrying a finished task into the next one, where it is re-sent every turn and then
paid for again in a pass that summarises work you are done with.

**A proxy turns off tool deferral.** Claude Code withholds MCP tool definitions and loads them on
demand — tool search, on by default from the 4.5 generation — so idle servers cost close to nothing.
It **disables itself when `ANTHROPIC_BASE_URL` points at a non-first-party host**, because most
proxies do not forward `tool_reference` blocks. Any token-saving proxy or model router in front of
the agent therefore re-inflates every definition onto every turn, and `ENABLE_TOOL_SEARCH=true` does
not rescue it — those requests fail instead. 50 loaded tools run 10–20k tokens before you type
anything. See [token-saving-tools.md](token-saving-tools.md).

## What this repo does structurally

Not settings — architecture, and why the agent workflow here is cheap by default:

- **Layered docs.** `AGENTS.md` always loaded; skills on task match; `docs/` on demand via an index.
  See [documentation-approach.md](documentation-approach.md).
- **Self-contained pages**, so one read finishes a task instead of three.
- **Fix at edit time, verify at commit time**, so hooks rarely reject and cost a round trip. See
  [linting-and-hooks.md](linting-and-hooks.md).
- **Everything build-generated under `artifacts/`**, so it is trivially ignorable.
