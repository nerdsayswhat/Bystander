// Stopgap for metavr's MCP incompatibilities with Claude Code 2.1.292:
//  - tools/list lacks ttlMs/cacheScope (client rejects it)
//  - the server/discover probe gets an unreliable answer (flaky connect), so answer it here -> client falls back to legacy initialize
// Remove once metavr/Claude Code are fixed: `claude mcp remove metavr -s local`, then `metavr mcp install claude-code --execute -y`.
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import os from 'node:os';
import path from 'node:path';

const exe = path.join(os.homedir(), '.metavr', 'bin', 'metavr.exe');
const child = spawn(exe, ['mcp', 'server', '--update'], { stdio: ['pipe', 'pipe', 'inherit'] });

createInterface({ input: process.stdin }).on('line', (line) => {
  try {
    const m = JSON.parse(line);
    if (m.method === 'server/discover') {
      process.stdout.write(JSON.stringify({ jsonrpc: '2.0', id: m.id, error: { code: -32601, message: 'Method not found' } }) + '\n');
      return;
    }
  } catch {} // non-JSON passes through
  child.stdin.write(line + '\n');
}).on('close', () => child.stdin.end());

const LISTS = ['tools', 'resources', 'resourceTemplates', 'prompts'];
createInterface({ input: child.stdout }).on('line', (line) => {
  try {
    const r = JSON.parse(line);
    if (r.result && LISTS.some((k) => Array.isArray(r.result[k]))) {
      if (typeof r.result.ttlMs !== 'number') r.result.ttlMs = 0;
      if (!['public', 'private'].includes(r.result.cacheScope)) r.result.cacheScope = 'private';
      line = JSON.stringify(r);
    }
  } catch {}
  process.stdout.write(line + '\n');
});

child.on('exit', (code) => process.exit(code ?? 0));
