// The add-slice skill (.claude/skills/add-slice/SKILL.md), run as a developer runs it: a ticket
// typed into `claude -p` in a checkout of the template at PINNED, edits accepted, the repo's own
// commands allowed. Graded by building and testing what it wrote; no judge model.
import { copyFileSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { REPO, checkout, git, runClaude, sessionRun, sh } from '../lib.mjs';

const PINNED = 'a342b70';
const HERE = dirname(fileURLToPath(import.meta.url));
const SKILL = join('.claude', 'skills', 'add-slice', 'SKILL.md');
const ALLOWED = 'Bash(mise:*),Bash(dotnet:*),Bash(git:*),Bash(ls:*),Bash(cat:*),Bash(mkdir:*),Bash(cp:*)';

// EVAL_CASES=id,id runs a subset; unset runs every case.
export async function loadCases() {
  const only = process.env.EVAL_CASES ? new Set(process.env.EVAL_CASES.split(',')) : null;
  return readFileSync(join(HERE, 'cases.jsonl'), 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l))
    .filter(c => !only || only.has(c.id));
}

// Each case's reference is a patch holding a slice that passes. Its integration tests are the
// acceptance tests: they speak HTTP only, so they hold for any implementation of the ticket.
function acceptanceTests(reference) {
  const file = readFileSync(join(HERE, reference), 'utf8').split(/^diff --git /m)
    .find(f => f.startsWith('a/tests/Api.IntegrationTests/Features/'));
  return file.slice(file.indexOf('\n@@')).split('\n').filter(l => l.startsWith('+')).map(l => l.slice(1)).join('\n')
    .replace(/^namespace .*;$/m, 'namespace Api.IntegrationTests.Acceptance;')
    .replace(/^public class \w+/m, 'public class AcceptanceTests'); // the analyzers want file and type to match
}

// Builds and tests whatever is in the workspace, with the acceptance tests added.
async function inspect(ws, input) {
  git(ws, 'add', '-A');
  git(ws, 'commit', '-q', '--allow-empty', '-m', 'The slice');
  const added = git(ws, 'diff', '--name-only', '--diff-filter=A', 'base').split('\n');
  const diff = git(ws, 'diff', 'base');
  mkdirSync(join(ws, 'tests', 'Api.IntegrationTests', 'Acceptance'));
  writeFileSync(join(ws, 'tests', 'Api.IntegrationTests', 'Acceptance', 'AcceptanceTests.cs'), acceptanceTests(input.reference));

  const build = await sh('dotnet', ['build', '--nologo', '-v', 'q'], ws);
  const checks = { build: build.code === 0, tests: false, behaves: false, contract: false, log: build.out.slice(-3000),
    tested: ['Api.UnitTests', 'Api.IntegrationTests'].every(p => added.some(f => f.startsWith(`tests/${p}/Features/`))) };
  if (checks.build) {
    // The build regenerates openapi.json, so a difference means a stale one was left behind.
    checks.contract = git(ws, 'status', '--porcelain', '--', 'src/Api/openapi.json') === '';
    const test = await sh('dotnet', ['test'], ws);
    const failed = [...test.out.matchAll(/^\s*failed ([\w.]+)/gm)].map(m => m[1]);
    const ran = /Test run summary/.test(test.out) && (test.code === 0 || failed.length > 0);
    checks.behaves = ran && !failed.some(n => n.includes('.Acceptance.'));
    checks.tests = ran && !failed.some(n => !n.includes('.Acceptance.'));
    checks.log = failed.length ? `failed: ${failed.join(', ')}` : test.out.slice(-1500);
  }
  return { checks, diff };
}

export async function runCase(input, ctx) {
  if (!ctx.model) throw new Error('pass --model, e.g. claude-haiku-5-5');
  const ws = checkout(PINNED);
  try {
    // The skill under test is the working tree's, so an edited skill is what runs.
    copyFileSync(join(REPO, SKILL), join(ws, SKILL));
    git(ws, 'add', '-A');
    git(ws, 'commit', '-q', '-m', 'Initial commit');
    git(ws, 'tag', 'base');
    let run;
    if (process.env.EVAL_REFERENCE) {
      // Free self-check: the reference instead of a session. Every metric but skill_used must be 1.
      git(ws, 'apply', '--binary', join(HERE, input.reference));
      run = { output: 'reference', transcript: [], model: ctx.model, events: [] };
    } else {
      const session = await runClaude(
        ['-p', input.prompt, '--model', ctx.model, '--output-format', 'stream-json', '--verbose',
         '--no-session-persistence', '--setting-sources', 'project', '--strict-mcp-config',
         '--permission-mode', 'acceptEdits', '--allowedTools', ALLOWED],
        ws, ctx.timeoutS, { MISE_TRUSTED_CONFIG_PATHS: ws });
      run = { ...sessionRun('add-slice skill (.claude/skills/add-slice/SKILL.md)', input.prompt, session),
              events: session.events, session_s: session.result.duration_ms / 1000 };
    }
    const { checks, diff } = await inspect(ws, input);
    run.transcript.push({ role: 'tool_result', content: `What the session left behind (git diff):\n${diff}` });
    run.checks = { ...checks, skill_used: run.events.some(e => e.type === 'assistant'
      && e.message.content.some(b => b.type === 'tool_use' && b.name === 'Skill' && b.input?.skill === 'add-slice')) };
    delete run.events;
    return run;
  } finally {
    rmSync(ws, { recursive: true, force: true });
  }
}

export async function gradeCase(input, run) {
  const { log, ...checks } = run.checks;
  const { skill_used, ...gates } = checks;
  const grade = { mergeable: Object.values(gates).every(Boolean) ? 1 : 0,
                  ...Object.fromEntries(Object.entries(checks).map(([k, v]) => [k, v ? 1 : 0])) };
  return { grade, explanation: Object.fromEntries(Object.keys(grade).map(k => [k, log])) };
}

// The session's own duration: the runner's clock would include the grading build and test run.
export const perfFrom = run => ({ tool_calls: run.tool_calls, turns: run.turns,
                                  reported_cost_usd: run.reported_cost_usd, latency_s: run.session_s });
