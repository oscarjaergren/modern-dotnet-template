# Token-saving tools, evaluated

**Read this if:** you are considering a plugin, skill or proxy that promises to cut your token bill.

## The one thing that predicts whether a tool helps

**Which layer it works at.** That single question predicts the return better than any benchmark,
because it determines the size of the pool the tool is allowed to drain.

In agentic coding, output is a small fraction of spend, and input dominated by **re-sent context** is
almost all of it. So:

| Layer                                 | Ceiling      | Why                                                                            |
| ------------------------------------- | ------------ | ------------------------------------------------------------------------------ |
| Agent's prose output                  | ~5% of spend | Output is ~5% of the bill (derived below). Even removing *all* of it saves 5%. |
| One command's output                  | small        | Only some commands, seen once, mostly not re-sent.                             |
| **Re-sent context / history**         | **large**    | Every token here is billed again on every turn.                                |
| **Not sending it at all** (retrieval) | **largest**  | A file never read costs nothing to compress.                                   |

A tool at the bottom of that table can beat a perfect tool at the top.

## Measured results

From a production replay over **614 million tokens and $926 of real spend**, with three tools
measured simultaneously:

| Tool         | Layer                      | Share of actual spend saved |
| ------------ | -------------------------- | --------------------------- |
| **Headroom** | API proxy, re-sent context | **2.8%**                    |
| RTK          | shell command output       | 0.5%                        |
| CAVEMAN      | agent prose output         | 0.4%                        |
| Combined     |                            | 3.7%                        |

Headroom alone beats the other two together by roughly 3×, and it is not close. That is the layer
argument in one line.

Separately, a paired A/B on **SkillsBench** (an independent open benchmark — 87 tasks, 24
model-harness configurations, 3 trials each, run by JetBrains) measured CAVEMAN at 8.5% of output
tokens against an advertised 65%, and found RTK **+7.6% more expensive** at low reasoning effort
(p=0.004) while neutral at high effort. Quality was unaffected in both cases.

### Where the 5% figure comes from

The two studies cross-check each other. CAVEMAN saves **8.5% of output tokens** and **0.4% of
actual spend**. For both to hold, output must be about **5% of what you pay**.

That number bounds every output-compression tool that will ever be pitched to you.

## Verdicts

**Headroom — worth trying.** Apache-2.0, and it works across Claude Code, Codex, Cursor, OpenCode
and MCP clients rather than one vendor. It compresses the *live zone* — the uncached part of the
context — while **preserving the cache prefix**, so it does not trigger the full-price re-read that
compaction does. Its compressors are content-typed: deduplicating search-result rows by score,
format-aware reduction for git diffs and JSON arrays. Median compression reaches 54% on ideal
payloads; the 2.8% overall reflects that most real payloads are plain text with little duplication.

**CAVEMAN — fine, use it if you like terse output.** It saves 0.4%, which will not show on an
invoice, but it costs nothing, has no quality impact (8 better / 10 worse / 64 tied, p=0.82), and
terser agent output is genuinely nicer to read. Its reported +11.6% in one benchmark was a single
outlier task crossing a pricing tier — variance, not a finding. There is no mechanism by which it
hurts.

**RTK — the one to avoid.** Not because the compression is fake; it genuinely compresses shell
output 60–90%. Because it makes the agent take **+13.8% more turns** (p=0.03), and a turn re-sends
the entire prefix. Compression saves bytes once; a retry costs the whole context. The causes were
compression-induced re-reads (paying for the filtered output *and* then the raw one) and a broken
rewrite of compound `find` predicates. The penalty vanishes at high reasoning effort, which is the
tell: a stronger model infers what compression removed, a weaker one asks again. RTK is a local
binary consuming no tokens itself — every extra penny came from the agent behaving differently.

## The wider ecosystem, with verdicts

Compression is one layer of several. Every tool below was checked to exist and its licence read from
the repository, not from a blog post.

**How to read the verdict column.** Three of these were measured against real spend — the replay
above — and those verdicts are evidence. The rest are judged on **layer**, which bounds the return
before you install anything. Where a tool's own claim is quoted, it is quoted to be distrusted.

Licence is a footnote, not a verdict — two here are not OSI-approved, and only one of those actually
stops a company using it. It is called out in the row where it bites.

| Tool                          | Layer               | Verdict               | Why                                                                                                                                                                        |
| ----------------------------- | ------------------- | --------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| claude-hud                    | measurement         | **Use**               | MIT, free, and the only thing here that shows the window filling while it happens. Measurement before optimisation.                                                        |
| Token Savior                  | **retrieval**       | **Try first**         | The right layer: symbol summaries instead of whole files. MIT. Its claimed saving is quoted as 43%, 80% and 97% by three different sources — ignore all three and measure. |
| Headroom                      | context compression | **Try**               | Best measured return of anything here, and that return is 2.8%. Apache-2.0, works across agents. Prefer its library or MCP mode — see the proxy trap below.                |
| Context Mode                  | tool output         | **Try**               | Right layer — sandboxes test logs and MCP payloads into a local index instead of the window. Elastic v2: internal use at work is fine, reselling it as a service is not.   |
| claude-context                | **retrieval**       | **Monorepos only**    | BM25 plus embeddings over the repo. MIT, but needs a paid embedding provider, so it moves cost rather than removing it. Nothing to index at this repo's size.              |
| code-review-graph             | code graph          | **Not at this size**  | Tree-sitter blast-radius map, MIT. Break-even is around 100 files; the reported 8–49× is on monorepos, and the overhead exceeds the benefit below that.                    |
| CAVEMAN                       | agent output        | **Optional**          | 0.4% of spend. Harmless, pleasant to read, invisible on an invoice. Its "65%" is a share of output tokens, and output is ~5% of the bill.                                  |
| token-optimizer (alexgreensh) | diagnostics         | **Personal use only** | PolyForm Noncommercial genuinely blocks company use, not just resale. `/context` and a statusline gauge answer the same question for free.                                 |
| claude-code-router            | model routing       | **Skip**              | The saving is real, but `model:` frontmatter and `/model` already do it natively — and doing it through a proxy costs you tool-definition deferral.                        |
| claude-mem / memsearch        | memory              | **Skip**              | `autoMemoryEnabled` is on by default and covers this. A second memory store is a second thing that can disagree with the first.                                            |
| token-optimizer-mcp           | caching             | **Skip**              | An MCP server whose purpose is saving tokens still injects its own tool definitions to do it. Verify it nets out before believing the headline.                            |
| CLAUDE.md "terseness" packs   | agent output        | **Skip**              | Capped at ~5% by arithmetic, whatever the README says. Keeping `AGENTS.md` short is the same idea for free.                                                                |
| RTK                           | shell output        | **Avoid**             | The only measured *negative*. It compresses 60–90% and still costs more, because it drives +13.8% more turns (p=0.03) and a turn re-sends everything.                      |

The three measured verdicts are argued in full under [Verdicts](#verdicts) above.

### The proxy trap

Claude Code defers MCP tool definitions and loads them on demand — tool search, on by default. That
is the largest single lever on MCP overhead, and **it switches itself off when `ANTHROPIC_BASE_URL`
points at a non-first-party host**, because most proxies do not forward `tool_reference` blocks.

So routing the agent through a local proxy re-inflates every deferred tool definition onto every
turn. Forcing it back with `ENABLE_TOOL_SEARCH=true` does not help: the requests then fail against a
proxy that cannot carry those blocks. Anything in the table that works as a proxy — Headroom in
proxy mode, claude-code-router — is making that trade, whether or not its README mentions it.

With no MCP servers connected this costs nothing. With several connected it can exceed the
compression that was the reason for installing the proxy. Use the library or MCP integration instead
of the proxy where a tool offers one, and measure with the servers you actually run.

## Nothing here is installed in this template

Not a verdict on the tools — a scope decision. A template ships to people with different agents,
budgets and workflows, and a proxy or plugin is a personal choice rather than a property of a .NET
codebase. What the template *does* do is the structural work: layered docs, isolated subagents,
explicit model routing. See [ai-workflow.md](ai-workflow.md).

Memory plugins are the one case where a specific caution applies: agents increasingly ship built-in
memory, so check what yours already has before adding a second store that can disagree with it.

## How to evaluate the next one

Do not trust a tool's own scoreboard — including the numbers on this page.

- Ask **which layer it works at** first. That bounds the answer before you install anything.
- Compare **invoices**, not token estimates, across a week with and without it.
- Watch the **cache-read ratio** and per-turn input across a long session.
- Keep the task set fixed and run it more than once. Published results moved with reasoning effort,
  and a single outlier task was enough to swing one figure by 11%.
- Be suspicious of any tool that reports its own savings. One scoreboard claimed 96 million tokens
  saved while the invoice went up, because it scored against a counterfactual the billing system
  never applies.
