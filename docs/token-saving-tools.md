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

| Tool                                     | Advertised              | Measured                                                                           |
| ---------------------------------------- | ----------------------- | ---------------------------------------------------------------------------------- |
| CAVEMAN (terse agent output)             | 65% output-token saving | **8.5%** output tokens — the cheapest category, so ~no bill impact                 |
| RTK (compresses shell output via a hook) | 60–90% cost reduction   | **+7.6% more expensive** at low effort (p=0.004) via **+13.8% turns**; ±0% at high |

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

### How removing tokens makes the bill go up

This is the counterintuitive part, and the benchmark measured the mechanism rather than guessing at
it: **the agent took +13.8% more turns** (p=0.03).

Compression saves bytes **once**. An extra turn re-sends the **entire conversation prefix**.

Illustrative arithmetic — the mechanism is measured, these numbers are not:

- Mid-session context ~60,000 tokens.
- Compressing one `git status` from ~1,500 to ~150 tokens saves ~1,350, once.
- One extra turn re-sends all 60,000. Even at the cached rate that is ~6,000 token-equivalents.

So a single extra turn costs roughly **four times** what that compression saved. On a 20-turn task,
+13.8% is ~2.8 extra turns — you would need a dozen successful compressions just to break even, with
a tool that only sees about a third of bash calls.

Three causes were identified:

- **Compression-induced re-reads.** Filtered output lacked the detail the agent needed, so it re-ran
  the command or read the file raw — paying for both versions.
- **A genuinely broken rewrite.** Compound `find` predicates were mangled into usage errors,
  forcing recovery loops.
- **Effort dependence, which is the tell.** The penalty vanished at high reasoning effort (+0.1%
  median): the model "seems to waste fewer turns reacting to compressed output". A weaker model
  cannot infer what compression removed, so it asks again.

That last point matters because it rules out the boring explanation. The tool is a local binary and
consumes no tokens itself — every extra penny came from the **agent behaving differently**.

### The structural reasons it cannot be fixed

- It compresses the cheapest category (see the cost model above).
- It only intercepts shell commands. Built-in file-read and search tools bypass it entirely, about
  half of what agents run is uncovered commands like `python3`, and what remains carries under 20%
  of tool-result characters.
- Cached re-reads dominate; on *new* input the measured change was +3.2% (p=0.23 — noise).
- It estimates tokens as characters / 4 and scores itself against a counterfactual the billing
  system never applies: the scoreboard reported 96 million tokens saved while the invoice went up.

### Being precise about the two results

The evidence for the two tools is **not** equally strong, and it is worth not overstating:

- **RTK's +7.6% is a systematic effect** — significant on both cost (p=0.004) and turn count
  (p=0.03), with an identified mechanism.
- **CAVEMAN's +11.6% was a single outlier task** that crossed a pricing tier. That is variance, not
  a finding. The defensible claim for CAVEMAN is only that it saves ~8.5% of the cheapest category,
  which rounds to nothing — not that it costs more.

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
