# Token-saving tools, evaluated

**Read this if:** you are considering a plugin, skill or proxy that promises a smaller token bill.

## The layer decides the ceiling

In agentic coding, re-sent context is most of the bill and output is a sliver of it. What a tool
can save is bounded by the layer it works at:

| Layer                          | Ceiling      | Why                                          |
| ------------------------------ | ------------ | -------------------------------------------- |
| Agent prose output             | ~5% of spend | Removing all of it saves 5%.                 |
| One command's output           | small        | Seen once, mostly not re-sent.               |
| **Re-sent context**            | **large**    | Billed again on every turn.                  |
| **Not sending it** (retrieval) | **largest**  | A file never read costs nothing to compress. |

## Measured results

A production replay over **614 million tokens and $926 of real spend**:

| Tool         | Layer                      | Share of spend saved |
| ------------ | -------------------------- | -------------------- |
| **Headroom** | API proxy, re-sent context | **2.8%**             |
| RTK          | shell command output       | 0.5%                 |
| CAVEMAN      | agent prose                | 0.4%                 |

An independent paired A/B on SkillsBench (JetBrains; 87 tasks, 24 configurations, 3 trials) found
CAVEMAN saves 8.5% of output tokens, against an advertised 65%, and RTK costs **7.6% more** at low
reasoning effort (p=0.004). Quality was unaffected. Together the studies give the 5% ceiling:
8.5% of output tokens is 0.4% of spend only if output is about 5% of the bill.

- **Headroom** compresses the uncached part of the context and preserves the cache prefix, so it
  avoids compaction's full-price re-read. Median compression is 54% on ideal payloads; real payloads
  are mostly plain text, hence 2.8%.
- **CAVEMAN** has no quality cost (8 better, 10 worse, 64 tied; p=0.82). A reported +11.6% was one
  outlier task crossing a pricing tier.
- **RTK** really does compress shell output 60–90%, and still costs more: the agent takes **13.8%
  more turns** (p=0.03), each re-sending the whole prefix, from re-reading raw output and a broken
  rewrite of compound `find` predicates. The penalty vanishes at high reasoning effort.

## Verdicts

Three are measured above. The rest are judged on layer; their own savings claims are quoted only to
be distrusted. Each was checked against its repository.

| Tool                          | Layer               | Verdict               | Why                                                                             |
| ----------------------------- | ------------------- | --------------------- | ------------------------------------------------------------------------------- |
| claude-hud                    | measurement         | **Use**               | MIT, free, shows the window filling live.                                       |
| Token Savior                  | **retrieval**       | **Try first**         | Symbol summaries instead of files. MIT. Claims range 43–97%; measure it.        |
| Headroom                      | context compression | **Try**               | Best measured result, 2.8%. Apache-2.0. Use library or MCP mode, not the proxy. |
| Context Mode                  | tool output         | **Try**               | Keeps big outputs in a local index. Elastic v2: fine at work, no resale.        |
| claude-context                | **retrieval**       | **Monorepos only**    | Needs a paid embedding provider; nothing to index at this size.                 |
| code-review-graph             | code graph          | **Not at this size**  | Break-even is around 100 files.                                                 |
| CAVEMAN                       | agent output        | **Optional**          | 0.4% of spend. Harmless, and pleasant to read.                                  |
| token-optimizer (alexgreensh) | diagnostics         | **Personal use only** | PolyForm Noncommercial blocks company use. `/context` does the same job.        |
| claude-code-router            | model routing       | **Skip**              | `model:` and `/model` do it natively, without a proxy.                          |
| claude-mem / memsearch        | memory              | **Skip**              | Built-in memory covers it; a second store can disagree.                         |
| token-optimizer-mcp           | caching             | **Skip**              | Adds its own tool definitions to every turn.                                    |
| CLAUDE.md terseness packs     | agent output        | **Skip**              | Capped at ~5%. A short `AGENTS.md` does it for free.                            |
| RTK                           | shell output        | **Avoid**             | The one measured net loss: more turns cost more than compression saves.         |

## The proxy trap

Claude Code loads MCP tool definitions on demand, and stops when `ANTHROPIC_BASE_URL` points at a
non-first-party host, because most proxies drop `tool_reference` blocks. Any proxy, including
Headroom's proxy mode and claude-code-router, therefore re-sends every tool definition every turn;
forcing `ENABLE_TOOL_SEARCH=true` makes the requests fail instead. With several MCP servers that can
cost more than the proxy saves. Prefer a tool's library or MCP mode.

## Nothing here is installed

A proxy or plugin is a personal choice, not a property of a .NET codebase. The template does the
structural part instead: layered docs, subagents, model routing ([ai-workflow.md](ai-workflow.md)).

## Evaluating the next one

- Ask which layer it works at before installing anything.
- Compare invoices, not token estimates, over a week with and without it.
- Watch the cache-read ratio and per-turn input over a long session.
- Run a fixed task set more than once; one outlier swung a published figure by 11%.
- Distrust self-reported savings. One tool claimed 96 million tokens saved while the invoice rose,
  by scoring against a counterfactual that billing never applies.
