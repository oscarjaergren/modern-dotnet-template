# AI workflow and token cost

**Read this if:** you are working on this repo with a coding agent, or your token bill is higher
than you expected.

Written for any agent — Claude Code, Codex, Cursor, Copilot, Gemini CLI. The cost model is a
property of how frontier models are priced, not of one tool. The few Claude-specific mechanics are
marked.

## The cost model, which is the whole point

Most token-saving advice targets the wrong thing. In agentic coding:

- **Output is a small fraction of the bill.** Agents emit code, diffs and tool calls, not prose.
- **Input dominates, and most of it is re-sent context.** The whole prefix goes up again every turn.
- **Cached input bills at roughly a tenth of fresh input.** That discount is the largest one a
  session gets, and it is automatic.

So compressing what the agent *says* attacks the cheapest category, and compressing individual
command outputs attacks a slice of the second-cheapest. Neither is where the money is.

## What actually moves the bill, in order

**1. Model routing — the single biggest lever.** Use a capable mid-tier model by default, the
cheapest tier for high-volume low-judgement work, and the top tier only for genuinely hard calls.
Subagents in this repo set `model:` explicitly for that reason.

**2. Protect the cache.** Anything that changes the stable prefix invalidates it and you pay full
freight for the lot. Keep always-loaded instructions stable; put volatile detail in files that are
read on demand. The metric to watch is the **cache-read ratio rising while per-turn input stays
flat** as the conversation grows.

**3. Subagents for fan-out.** A subagent has its own context window and returns only a summary, so
the main thread never pays for the files it read. Ideal for "search the codebase and tell me where
X is". **Caveat:** multi-agent *teams* have been measured at roughly 7× a normal session. A scalpel,
not a default.

**4. Progressive disclosure.** Already the structure here: always-loaded conventions, procedures
loaded on task match, reference docs pulled in on demand via an index. See
[documentation-approach.md](documentation-approach.md).

**5. Batch related work.** Ten turns that each re-send 4k of shared context spend 40k before any new
work happens. Grouping related changes into one prompt is a minute of planning for thousands of
tokens.

**6. Session hygiene.** Start a fresh session for unrelated work. Note that **compaction is not
lossless** — path-scoped rules, nested instructions and exact tool output can be dropped. Anything
load-bearing belongs in a file, not in the conversation.

## Run code-writing agents in worktrees

The failure this prevents is silent and destructive. Two agents open the same file, each edits its
own in-memory copy, both write back — **the second write wins and erases the first agent's work**.
No error, no conflict marker, nothing in the diff to suggest anything was lost.

Git worktrees fix it structurally: each agent gets its own directory and branch, sharing one git
history. Nothing to remember, nothing to coordinate.

*Claude-specific:* built-in since v2.1.49. Put it in the agent's frontmatter:

```yaml
---
name: my-agent
isolation: worktree
---
```

**Use it on every subagent that writes code.** It costs nothing and removes a whole class of
data loss. Read-only agents — like `codebase-locator` — do not need it.

To ask for it in a prompt rather than a config: *"work on this in a separate worktree"*. If your
agent has no worktree support, `git worktree add ../feature-x -b feature-x` and point it there.

Practical ceiling is 4–8 concurrent worktrees per person. Past that you are bottlenecked on
reviewing the output, not on the agent.

## A settings.json starting point

*Claude-specific.* Every key below is real; the defaults are noted where they matter.

```jsonc
{
  "$schema": "https://json.schemastore.org/claude-code-settings.json",

  // Default to a mid-tier model. The top tier is a per-task decision, not a standing one.
  "model": "sonnet",

  // Compaction. Default is on, with a model-tuned window; valid range is 100000-1000000 TOKENS.
  // Lower = compact sooner = smaller context per turn. See the trade-off below before lowering it.
  "autoCompactEnabled": true,
  "autoCompactWindow": 300000,

  // On by default. The agent keeps its own notes across sessions, which is why this repo adds
  // no third-party memory plugin.
  "autoMemoryEnabled": true,

  // Session files are kept 30 days by default. Shorten it if the repo is sensitive.
  "cleanupPeriodDays": 30,

  "includeCoAuthoredBy": true
}
```

### The compaction trade-off, which is not free

A lower `autoCompactWindow` is usually described as a straight win. It is not, and the reason is the
same mechanism as everything else on this page: **compaction rewrites the prefix, so it invalidates
the prompt cache.** The next turn pays full input price for the new context, plus the cost of the
summarisation call itself, plus whatever detail the summary dropped.

So:

- **Long sessions** — worth it. You stop carrying a huge context on every turn, and the one-off
  cache reset pays back over the remaining turns.
- **Short sessions** — a lower window can cost more than it saves. You paid to reset a cache you
  were about to stop using.

`300000` is a reasonable aggressive setting for long working sessions. Do not treat it as a default
to copy: it is a lever to tune against how you actually work, and the honest way to tune it is the
same as everything else here — compare invoices across a week, not a tool's estimate.

Because compaction is lossy, anything load-bearing belongs in a file rather than in the
conversation. That is the same argument that produced this repo's docs layout.

## How to verify any of this yourself

Do not trust a tool's own scoreboard, including any claim on this page.

- Watch the **cache-read ratio** and per-turn input tokens across a long session.
- Compare **invoices**, not token estimates, across a week with and without a change.
- If you A/B a tool, keep the task set fixed and run it more than once — the JetBrains numbers moved
  by reasoning effort, and a single outlier task was enough to swing the CAVEMAN result by 11%.

## Before you install a token-saving tool

Ask **which layer it works at** — that bounds the return before you install anything. A tool
compressing the agent's prose is capped at ~5% of your bill; one compressing re-sent context is not.
Measured results for the main options, including one worth trying and one to avoid, are in
[token-saving-tools.md](token-saving-tools.md).
