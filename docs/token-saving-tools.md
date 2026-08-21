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

## The wider ecosystem

Compression is only one layer. The tools worth knowing, by what they actually do:

| Tool                   | Layer                     | What it does                                                                          |
| ---------------------- | ------------------------- | ------------------------------------------------------------------------------------- |
| Headroom               | compression (context)     | API-layer proxy, content-typed compressors, preserves cache prefix                    |
| Context Mode           | compression (tool output) | Sandboxes large outputs — test logs, DOM snapshots, MCP payloads — into local indexes |
| RTK                    | compression (shell)       | Rewrites shell commands via a hook                                                    |
| CAVEMAN                | compression (output)      | Strips filler from the agent's prose                                                  |
| Token Savior           | **retrieval**             | Symbol summaries before full files; progressive code reading via MCP                  |
| claude-context         | **retrieval**             | Repository embeddings, semantic search                                                |
| code-review-graph      | code graph                | Tree-sitter structure map; dependency and blast-radius questions                      |
| memsearch / claude-mem | memory                    | Durable decisions across sessions                                                     |

The retrieval tools are structurally the most interesting and the least measured. Not sending a file
beats compressing it, and a symbol summary instead of a 600-line file is a bigger win than any
compressor can offer on that file. If you experiment with one thing here, make it a retrieval tool.

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
