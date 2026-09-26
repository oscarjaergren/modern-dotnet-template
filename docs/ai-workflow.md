# AI workflow and token cost

**Read this if:** you are working here with a coding agent, or your token bill is higher than
expected. Why these choices work: [token-saving-tools.md](token-saving-tools.md).

## Where the money goes

| Category                                                        | Share of spend |
| --------------------------------------------------------------- | -------------- |
| Cached overhead: instructions, tool definitions, re-sent prefix | 30–50%         |
| Tool I/O: file reads, command output                            | 30–45%         |
| Reasoning                                                       | 10–30%         |
| Visible output                                                  | **1–10%**      |

What the agent *says* is not the bill. What is re-sent every turn is.

## Set once

*Claude Code specific; every key checked against the settings reference.*

**Yours, in `~/.claude/settings.json`.** Personal, so the repo ships none of it: a project file
setting `model` would override everyone who clones it.

```jsonc
{
  "model": "sonnet", // top tier per task, not by default
  "autoCompactEnabled": true,
  "autoCompactWindow": 300000, // tokens, not a percentage (100k–1M); lower = compact earlier
  "autoMemoryEnabled": true, // built-in memory, so no memory plugin
  "cleanupPeriodDays": 30,
  "includeCoAuthoredBy": true,
  "env": { "MAX_THINKING_TOKENS": "8000" } // reasoning is 10–30% of spend
}
```

**The repo's, in `.claude/settings.json`.** There is no `.claudeignore`; Claude Code has never read
one. Exclusion is a `Read` deny rule, in gitignore syntax:

```jsonc
{
  "permissions": {
    "deny": [
      "Read(.env)",
      "Read(*.pfx)",
      "Read(*.user)",
      "Read(./artifacts/**)"
    ]
  }
}
```

A bare filename matches at any depth; `./` anchors to the settings file. All build output is under
`artifacts/`, so one rule covers it. These keep noise and secrets out of the file tools, but **do
not stop a Bash command** reading the file. For a real boundary, use the sandbox.

**Agent frontmatter.** `.claude/agents/codebase-locator.md` is the example:

```yaml
model: haiku           # locating needs no judgement
tools: Read, Grep, Glob
```

A subagent has its own context window, so the main thread pays only for its summary. Match the tier
to the judgement: Haiku for locating and mechanical edits, Sonnet for review, the top tier for
architecture. Give every writing agent `isolation: worktree`: two agents writing the same file
otherwise lose the first write, silently. Past 4–8 concurrent agents, review becomes the
bottleneck.

## Each session

| Do this                       | When                                                            |
| ----------------------------- | --------------------------------------------------------------- |
| Disconnect unused MCP servers | At the start. Tool choice degrades past 30–50 loaded tools.     |
| Plan mode                     | Before anything complex. A wrong guess means re-reading it all. |
| `/model`                      | Dropping a tier for mechanical work.                            |
| `/clear` or a new session     | Switching task. They are the same thing.                        |
| `/compact`                    | Only mid-task, to control what survives the summary.            |

## When something looks wrong

| Check                           | Tells you                                         |
| ------------------------------- | ------------------------------------------------- |
| `/context`                      | What is in the window now.                        |
| `/cost`                         | This session's tokens and spend.                  |
| A statusline gauge (claude-hud) | The window filling, live.                         |
| `--verbose` for a day           | What normal looks like, before you tune anything. |
| `console.anthropic.com` → Usage | History across sessions.                          |

Healthy: the cache-read ratio rises while per-turn input stays flat. If per-turn input climbs with
the conversation, something is defeating the cache.

Every few weeks, run `/insights`. It reads your recorded transcripts and writes
`~/.claude/usage-data/report.html`: how you work, where sessions go wrong, unused features, and
suggested `AGENTS.md` rules. A fresh install reports zeros.

## Two traps

**Compaction is a setting, not a vigil.** Watching a gauge to `/compact` at 60–70% fails like any
habit that depends on remembering; `autoCompactWindow` does it for you. Each pass costs 100–200k
tokens and invalidates the prompt cache, so it pays off in long sessions and wastes money in short
ones. If yours are short, leave the default and end sessions instead: a finished task left in the
window is re-sent every turn, then paid for again when it is summarised.

**A proxy turns off tool deferral.** Claude Code loads MCP tool definitions on demand (tool search,
on by default), so idle servers cost almost nothing. It turns itself off when `ANTHROPIC_BASE_URL`
points at a non-first-party host, so a token-saving proxy re-sends every definition every turn, and
forcing `ENABLE_TOOL_SEARCH=true` makes those requests fail. Fifty loaded tools cost 10–20k tokens
before you type.

## What the repo does structurally

`AGENTS.md` is always loaded and kept short; skills load on task match; `docs/` loads on demand
([documentation-approach.md](documentation-approach.md)). Formatting is fixed at edit time, so hooks
rarely reject ([linting-and-hooks.md](linting-and-hooks.md)). Build output lives in `artifacts/`,
so it is trivially excluded.
