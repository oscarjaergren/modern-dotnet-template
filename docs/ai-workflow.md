# AI workflow and token cost

**Read this if:** you are working on this repo with a coding agent, or your token bill is higher
than you expected.

Config-first. The reasoning behind the tool choices is in
[token-saving-tools.md](token-saving-tools.md); this page is what to actually set.

## Where the money goes

Measured breakdown of a typical agentic session:

| Category                                                                | Share of spend |
| ----------------------------------------------------------------------- | -------------- |
| Cached system overhead (instructions, tool definitions, re-sent prefix) | 30–50%         |
| Tool I/O (file reads, command output)                                   | 30–45%         |
| Reasoning tokens                                                        | 10–30%         |
| Visible output                                                          | **1–10%**      |

Two consequences that drive everything below: compressing what the agent *says* is capped at ~5%,
and **MCP tool definitions are re-sent on every turn** — each connected server can add up to
**18,000 tokens per turn** before you type anything.

## settings.json

*Claude-specific.* Every key verified against the settings reference.

```jsonc
{
  "$schema": "https://json.schemastore.org/claude-code-settings.json",

  // Mid-tier default. The top tier becomes a per-task decision, not a standing cost.
  "model": "sonnet",

  // Range is 100000-1000000 TOKENS. Default is model-tuned and fires around 93% capacity,
  // and each compaction pass itself costs 100-200k tokens. Compacting earlier and more often
  // trades those passes against carrying a large prefix — see the trade-off below.
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

### The compaction trade-off

Lower `autoCompactWindow` is usually sold as a straight win. It is not: **compaction rewrites the
prefix, so it invalidates the prompt cache**, and the pass itself costs 100–200k tokens. Long
sessions win — you stop carrying a huge prefix every turn. Short sessions lose — you paid to reset a
cache you were about to stop using.

Compacting *deliberately* at 60–70% beats letting auto-compaction fire at 93%, because you choose
the moment and you know what was in context.

## `.claudeignore`

Keeps generated and vendored files out of reads and searches entirely — the cheapest possible fix,
since a file never read costs nothing.

```
artifacts/
bin/
obj/
*.user
*.log
```

This repo needs little else: `UseArtifactsOutput` already puts every build output under
`artifacts/`, so one line covers what would otherwise be dozens of `bin/` and `obj/` folders.

## Model routing: the actual agent file

The largest single lever, and it is one line of frontmatter. `.claude/agents/codebase-locator.md`
in this repo is the worked example — a "where is X?" agent on the cheapest tier:

```yaml
---
name: codebase-locator
description: Finds where things live in this repo. Use for "where is X?" questions.
tools: Read, Grep, Glob
model: haiku
isolation: worktree   # only needed for agents that WRITE
---
```

Two savings at once: **`model: haiku`** because locating needs no judgement, and **its own context
window** so the main thread never pays for the files it opened — it receives only the summary.

The rule: match the tier to the judgement required. Locating, inventorying and mechanical edits are
Haiku work. Reviewing is Sonnet work. Architecture is worth the top tier.

## Worktrees, for anything that writes

Two agents open the same file, both write back, **the second write silently erases the first**. No
error, nothing in the diff.

```yaml
isolation: worktree
```

Put it on every code-writing subagent — it costs nothing. Read-only agents do not need it. Without
built-in support: `git worktree add ../feature-x -b feature-x`. Practical ceiling is 4–8 concurrent;
past that you are bottlenecked on reviewing output.

## Trim MCP servers

Each connected MCP server injects its tool definitions into **every turn** — up to 18,000 tokens
each. Three idle servers can cost more per turn than the file you are editing.

Disconnect the ones you are not using this session. This is the highest-value thing on the page that
costs nothing and takes ten seconds.

## Commands worth the muscle memory

| Command    | When                                                          |
| ---------- | ------------------------------------------------------------- |
| `/clear`   | Switching to unrelated work. Cheapest possible reset.         |
| `/compact` | Deliberately, at 60–70% context, rather than waiting for 93%. |
| `/cost`    | End of a session — token count and estimated spend.           |
| `/model`   | Drop to a cheaper tier for mechanical stretches.              |
| `/context` | See what is actually occupying the window.                    |

Plan mode before a complex task is a real saving, not ceremony: planning first avoids the expensive
failure mode of an agent exploring, guessing wrong, and re-reading everything.

## Measure before optimising

Do not tune against estimates.

- `/cost` per session; `console.anthropic.com` → Usage for history.
- Run with `--verbose` for one full working day before changing anything.
- Watch the **cache-read ratio** rise while per-turn input stays flat as the conversation grows.
  That is what a healthy long session looks like.
- A statusline context gauge (`claude-hud` and similar) shows live token count and cost.

## What this repo does structurally

Not settings — architecture, and it is why the agent workflow here is cheap by default:

- **Layered docs.** `AGENTS.md` always loaded; skills on task match; `docs/` on demand via an index.
  See [documentation-approach.md](documentation-approach.md).
- **Self-contained pages**, so one read finishes a task instead of three.
- **Fix at edit time, verify at commit time**, so hooks rarely reject and cost a round trip. See
  [linting-and-hooks.md](linting-and-hooks.md).
- **Everything build-generated under `artifacts/`**, so it is trivially ignorable.

## Before installing a token-saving tool

Ask **which layer it works at** — that bounds the return before you install anything. Measured
results, including one worth trying and one to avoid, are in
[token-saving-tools.md](token-saving-tools.md).
