# Evaluating agents

**Read this if:** you are changing an agent, skill or prompt in `.claude/`, or choosing its model.

A prompt change needs a measurement. `.claude/hillclimb/` holds two worked examples, built with
`/claude-api build-eval` and `/claude-api hillclimb` following
[Automating eval design and hillclimbing](https://claude.dev/blog/automating-eval-design-and-hillclimbing/).
They share one runner, `run-eval.mjs`; each is a `flow.mjs` that loads cases, runs one and grades it.

## slice-reviewer: graded by a judge

- **Cases:** 40 patches on a pinned commit: 28 plant one defect, 12 are clean. A build and test run
  backs every label (`cases/cases.jsonl`); `cases/build_cases.py` generates them.
- **Run:** the agent reviews each in a throwaway checkout, as CI runs it. The answer key stays out.
- **Grade:** an Opus judge sorts each Must fix item into planted, other real, or false alarm. It
  gave the same grade on 24 of 24 re-grades. `no_hedge` is a regex.
- **Split:** 20 cases to design changes from, 20 held out to decide them (`_state.json`).

Results on the held-out cases, 2 runs each:

|                           | Sonnet | Haiku  | Haiku, `v2` prompt |
| ------------------------- | ------ | ------ | ------------------ |
| Planted defect caught     | 28/28  | 28/28  | 27/28              |
| No false alarm            | 63%    | 85%    | 85%                |
| No "couldn't build" hedge | 0%     | 0%     | 95%                |
| API price per review      | $0.064 | $0.008 | $0.006             |

Haiku catches what Sonnet does, at an eighth of the API price. The `v2` prompt stops the hedging without
losing catches. Noise is about ±15 points at 40 runs. Other real defects don't count against a
review: the Bookings fixture has a real one (`DateOnly.AddDays` overflows).

## add-slice: graded by the build

- **Cases:** four tickets (`cases.jsonl`), each with a reference slice that passes.
- **Run:** a session in a throwaway checkout, edits accepted and the repo's commands allowed.
- **Grade:** no judge. The slice must build, pass its own tests, pass the reference's HTTP tests,
  leave `openapi.json` current, and add unit and integration tests. `mergeable` is all five.

`EVAL_REFERENCE=1` grades the references instead of running sessions: a free check that every
ticket can pass. No results yet; it has not been run.

## When to run it

Run it whenever something the agent depends on changes, before you rely on the change:

- its prompt, skill or tools;
- its model, including a new release or a bigger tier; a stronger model can still be a worse fit;
- the harness: a Claude Code upgrade, CI's settings, or another agent tool;
- a miss in real use: add it as a case first, so the fix is measured.

Not on every pull request: code changes don't change the agent. Cost scales with cases × runs × the
models used, and the judge is usually the biggest part.

## Reproducing it

Needs the signed-in `claude` CLI and Node 24. From the repo root:

```bash
mise exec node@24 -- node .claude/hillclimb/run-eval.mjs --flow .claude/hillclimb/slice-reviewer --variant baseline --model claude-sonnet-5-5 --reps 2
```

The first run stops until you pass `--approve-harness`, which records a hash of the runner, cases
and prompt. `EVAL_CASES=id,id` runs a subset. It only reproduces in this repository, since cases
patch a commit a templated repo lacks.

For another agent or skill, run `/claude-api build-eval`, or copy a `flow.mjs`. Prove every label:
three of the first "clean" cases weren't.

## Not yet: CI

A labelled workflow on `.claude/agents/**` changes that runs the held-out cases and comments the
result against the committed baseline. It needs an API key and a budget.
