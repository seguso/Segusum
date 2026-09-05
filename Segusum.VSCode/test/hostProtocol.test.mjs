import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const project = process.env.SEGUSUM_LITGIR_PROJECT && path.resolve(process.env.SEGUSUM_LITGIR_PROJECT);
const sourcePath = process.env.SEGUSUM_DIRTY_SOURCE && path.resolve(process.env.SEGUSUM_DIRTY_SOURCE);

if (!project || !sourcePath || !fs.existsSync(project) || !fs.existsSync(sourcePath)) {
  console.log('host protocol dirty-buffer test skipped (set SEGUSUM_LITGIR_PROJECT and SEGUSUM_DIRTY_SOURCE)');
} else {
  const host = path.join(repo, 'Segusum.Tooling.Host', 'bin', 'Debug', 'net8.0', 'Segusum.Tooling.Host.dll');
  assert.ok(fs.existsSync(host), `missing host: ${host}`);
  const diskText = fs.readFileSync(sourcePath, 'utf8');
  const insertion = diskText.indexOf('\n') + 1;
  const dirtyText = diskText.slice(0, insertion) + '\n\n\n' + diskText.slice(insertion);
  const symbol = 'creaCicloMikeNonRipete';
  const offset = dirtyText.lastIndexOf(symbol);
  assert.ok(offset >= 0);
  const before = dirtyText.slice(0, offset);
  const line = before.split('\n').length;
  const column = offset - before.lastIndexOf('\n');

  const child = spawn('dotnet', [host], { cwd: path.dirname(project), stdio: ['pipe', 'pipe', 'pipe'] });
  let stdout = '';
  let stderr = '';
  const responses = new Map();
  child.stdout.setEncoding('utf8');
  child.stderr.setEncoding('utf8');
  child.stdout.on('data', chunk => {
    stdout += chunk;
    for (const line of stdout.split('\n').slice(0, -1)) {
      try { const response = JSON.parse(line); responses.set(response.id, response); } catch { /* diagnostics stay on stderr */ }
    }
    stdout = stdout.slice(stdout.lastIndexOf('\n') + 1);
  });
  child.stderr.on('data', chunk => { stderr += chunk; });
  const request = (id, method, params) => new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`timeout waiting for ${method}\nstderr=${stderr}`)), 120000);
    const poll = () => {
      if (responses.has(id)) { clearTimeout(timer); resolve(responses.get(id)); return; }
      setTimeout(poll, 20);
    };
    child.stdin.write(`${JSON.stringify({ id, method, params })}\n`);
    poll();
  });

  try {
    const initialized = await request(1, 'initialize', { projectPath: project });
    assert.equal(initialized.error, null, JSON.stringify(initialized));
    const result = await request(2, 'definition', { path: sourcePath, line, column, text: dirtyText });
    assert.equal(result.error, null, JSON.stringify(result));
    assert.equal(result.result?.displayName, symbol, `stdout=${JSON.stringify(result)}\nstderr=${stderr}`);
    const references = await request(3, 'references', { path: sourcePath, line, column, text: dirtyText });
    assert.equal(references.error, null, JSON.stringify(references));
    assert.ok((references.result ?? []).every(reference => reference.displayName === symbol));
    console.log('host protocol dirty-buffer test: definition-first and references resolve the current dirty token');
  } finally {
    child.kill();
  }
}
