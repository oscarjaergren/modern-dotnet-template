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

## Evaluated and rejected: output-compression tools

Two popular tools promise large savings. Both were evaluated and neither is used here.

| Tool                                     | Advertised              | Measured                                                                         |
| ---------------------------------------- | ----------------------- | -------------------------------------------------------------------------------- |
| CAVEMAN (terse agent output)             | 65% output-token saving | 8.5% output tokens; the arm cost **11.6% more** overall                          |
| RTK (compresses shell output via a hook) | 60–90% cost reduction   | **+7.6% more expensive** at low reasoning effort (p=0.004); ±0% at high (p=0.99) |

Two independent analyses, different methods, same conclusion:

- **A paired A/B on SkillsBench** — an independent open benchmark (87 tasks, 24 model-harness
  configurations, 3 trials each), run by JetBrains. Quality was unaffected in both cases.
- **A production replay over 614 million tokens and $926 of real spend**, measuring three such tools
  at once: RTK saved **0.5%** of actual spend, CAVEMAN **0.4%**, all three combined **3.7%**.

The replay's conclusion is the line worth keeping:

> "The advertised numbers are not exaggerated. Each measures a different thing on a different
> workload."

**The vendors are not lying.** RTK really does compress shell output by 60–90%. That compression is
simply half a percent of the bill.

Why, structurally:

- They compress the cheapest category (see the cost model above).
- RTK only intercepts shell commands. An agent's built-in file-read and search tools bypass it
  entirely, about half of what agents run is uncovered commands like `python3`, and what remains
  carries under 20% of tool-result characters.
- Cached re-reads dominate, and on *new* input RTK moved +3.2% (p=0.23 — noise).
- RTK estimates tokens as characters ÷ 4 and scores itself against a counterfactual the billing
  system never applies: its scoreboard reported 96 million tokens saved while the invoice went up.

**Disclosure:** JetBrains sells competing agent tooling and launched a competing context product the
same month. That is a real interest. What defuses it is not trust — it is that the benchmark is
third-party and open, the methodology and p-values are published, the findings were quality-neutral
rather than a hatchet job, and an unaffiliated replay using a completely different method reached
the same place.

**The generalisable lesson, which outlives both tools:** a tool that reports its own savings against
a counterfactual it invented is not evidence. Only a paired A/B against the invoice is. Apply that
to the next tool advertising 90%.

## Memory and context plugins: none added

Agents increasingly ship built-in memory, and this repo's structure already does the job that
context plugins target. A third-party memory store on top would mean two or three systems that can
disagree about what is true — the same failure this repo avoids everywhere else.

If you adopt one anyway, prefer the one your agent vendor ships, and check it against whatever
memory the agent already has before assuming it adds anything.

## How to verify any of this yourself

Do not trust a tool's own scoreboard, including any claim on this page.

- Watch the **cache-read ratio** and per-turn input tokens across a long session.
- Compare **invoices**, not token estimates, across a week with and without a change.
- If you A/B a tool, keep the task set fixed and run it more than once — the JetBrains numbers moved
  by reasoning effort, and a single outlier task was enough to swing the CAVEMAN result by 11%.
