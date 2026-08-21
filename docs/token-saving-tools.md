# Token-saving tools, evaluated

**Read this if:** you are considering a plugin, skill or proxy that promises to cut your token bill.

## The short version

Two of the most widely recommended tools were evaluated here and neither is used. The reason
generalises, so it is worth understanding rather than just taking the conclusion.

**The cost model is why.** In agentic coding, output is a small fraction of the bill, input
dominates, and most input is re-sent context billed at roughly a tenth the rate. A tool that
compresses what the agent *says*, or that compresses individual shell outputs, is working on the
cheapest categories. Full reasoning in [ai-workflow.md](ai-workflow.md).

## The measurements

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

## Memory and context plugins

Same answer, different reason. Agents increasingly ship built-in memory — Claude Code has
`autoMemoryEnabled` on by default — and this repo's layered docs already do what context plugins
target. Adding a third-party store would mean two or three systems that can disagree about what is
true, which is the failure this repo avoids everywhere else.

If you adopt one anyway, prefer the one your agent vendor ships, and check what memory the agent
already has before assuming a plugin adds anything.

## How to evaluate the next one

Do not trust a tool's own scoreboard — including any number on this page.

- Compare **invoices**, not token estimates, across a week with and without it.
- Watch the **cache-read ratio** and per-turn input across a long session.
- If you A/B it, keep the task set fixed and run it more than once. The published numbers moved with
  reasoning effort, and a single outlier task was enough to swing one result by 11%.
