// Helpers shared by the flows: a throwaway checkout of the template, and a `claude -p` session.
import { execFileSync, spawn } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
export const REPO = execFileSync('git', ['-C', HERE, 'rev-parse', '--show-toplevel'], { encoding: 'utf8' }).trim();

export function git(cwd, ...args) {
  return execFileSync('git', ['-c', 'user.name=dev', '-c', 'user.email=dev@example.invalid', ...args],
    { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
}

// The template at a pinned commit, without this directory, so answer keys are out of reach.
export function checkout(pinned) {
  const ws = mkdtempSync(join(tmpdir(), 'eval-'));
  execFileSync('bash', ['-c', `git -C "$1" archive ${pinned} | tar -x -C "$2"`, '_', REPO, ws]);
  rmSync(join(ws, '.claude', 'hillclimb'), { recursive: true, force: true });
  git(ws, 'init', '-q', '-b', 'main');
  return ws;
}

// A command's exit code and output; never throws on a non-zero exit.
export function sh(cmd, args, cwd) {
  return new Promise((resolvePromise, reject) => {
    const child = spawn(cmd, args, { cwd, stdio: ['ignore', 'pipe', 'pipe'] });
    let out = '';
    child.stdout.on('data', d => { out += d; });
    child.stderr.on('data', d => { out += d; });
    child.on('error', reject);
    child.on('close', code => resolvePromise({ code, out }));
  });
}

// Runs claude and collects its stream-json events. The child is killed at the ceiling, so a
// hung session can't outlive its case (the scaffold's ceiling alone frees the slot, not the process).
export function runClaude(args, cwd, timeoutS, env = {}) {
  return new Promise((resolvePromise, reject) => {
    const child = spawn('claude', args, { cwd, env: { ...process.env, ...env }, stdio: ['ignore', 'pipe', 'pipe'] });
    let out = '', err = '';
    child.stdout.on('data', d => { out += d; });
    child.stderr.on('data', d => { err += d; });
    const timer = timeoutS > 0 ? setTimeout(() => child.kill('SIGKILL'), Math.max(1, timeoutS - 5) * 1000) : null;
    child.on('error', reject);
    child.on('close', code => {
      if (timer) clearTimeout(timer);
      const events = out.split('\n').filter(l => l.trim().startsWith('{')).map(l => JSON.parse(l));
      const result = events.findLast(e => e.type === 'result');
      if (!result) {
        const e = new Error(`claude exited ${code} with no result: ${err.slice(-500)}`);
        e.failure_class = code === null ? 'timeout' : 'harness_error';
        if (/rate.?limit|overloaded|429|529/i.test(err + out)) e.status = 429;
        return reject(e);
      }
      resolvePromise({ events, result });
    });
  });
}

const textOf = c => typeof c === 'string' ? c
  : (c ?? []).map(b => b.type === 'text' ? b.text : JSON.stringify(b)).join('\n');

export function transcriptFrom(system, prompt, events) {
  const init = events.find(e => e.type === 'system' && e.subtype === 'init');
  const turns = [{ role: 'system', content: `${system}\nmodel: ${init?.model}\ntools: ${(init?.tools ?? []).join(', ')}` },
                 { role: 'user', content: prompt }];
  for (const e of events) {
    if (e.type === 'assistant') {
      let thinking;
      for (const b of e.message.content) {
        if (b.type === 'thinking') thinking = (thinking ? thinking + '\n' : '') + (b.thinking || '');
        else if (b.type === 'text') { turns.push({ role: 'assistant', content: b.text, ...(thinking ? { thinking } : {}) }); thinking = undefined; }
        else if (b.type === 'tool_use') { turns.push({ role: 'tool_call', name: b.name, content: JSON.stringify(b.input, null, 2), ...(thinking ? { thinking } : {}) }); thinking = undefined; }
      }
    } else if (e.type === 'user') {
      for (const b of e.message.content ?? [])
        if (b.type === 'tool_result') turns.push({ role: 'tool_result', content: textOf(b.content) });
    }
  }
  return turns;
}

// What a row records about the session, from its events.
export function sessionRun(system, prompt, { events, result }) {
  if (result.is_error) {
    const e = new Error(`claude result ${result.subtype}: ${String(result.result ?? '').slice(0, 300)}`);
    e.failure_class = 'harness_error';
    throw e;
  }
  const assistant = events.filter(e => e.type === 'assistant');
  const last = assistant.at(-1)?.message;
  return {
    output: result.result,
    transcript: transcriptFrom(system, prompt, events),
    // The agent's own model, from the responses; Claude Code may use another model for
    // housekeeping, which modelUsage would also list.
    model: last?.model,
    usage: result.usage,
    stop_reason: result.stop_reason ?? last?.stop_reason,
    turns: result.num_turns,
    tool_calls: assistant.flatMap(e => e.message.content).filter(b => b.type === 'tool_use').length,
    reported_cost_usd: result.total_cost_usd,
    model_usage: result.modelUsage,
  };
}

export const perfFrom = run => ({ tool_calls: run.tool_calls, turns: run.turns, reported_cost_usd: run.reported_cost_usd });
