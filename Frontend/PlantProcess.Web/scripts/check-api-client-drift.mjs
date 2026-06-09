#!/usr/bin/env node
// T-019 - CI drift guard: fail if the committed API types are stale vs the
// committed openapi/openapi.json. Dormant (exit 0) until the spec is committed,
// so it does not break the pipeline before T-019 is set up.
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';

const spec = path.resolve('openapi/openapi.json');
const committed = path.resolve('src/api/generated/schema.d.ts');

if (!fs.existsSync(spec)) {
  console.warn('API client drift check NOT ARMED: openapi/openapi.json not committed yet.');
  console.warn('Run `npm run openapi:export` then `npm run openapi:generate` and commit both.');
  process.exit(0);
}
if (!fs.existsSync(committed)) {
  console.error('FAIL: openapi/openapi.json is committed but src/api/generated/schema.d.ts is missing.');
  console.error('Run `npm run openapi:generate` and commit it.');
  process.exit(1);
}

const tmp = path.join(os.tmpdir(), 'ppiq-openapi-' + Date.now() + '.d.ts');
execFileSync('npx', ['openapi-typescript', spec, '-o', tmp], { stdio: 'inherit' });
const fresh = fs.readFileSync(tmp, 'utf8');
const have = fs.readFileSync(committed, 'utf8');
fs.rmSync(tmp, { force: true });

if (fresh !== have) {
  console.error('FAIL: committed API types are stale. Run `npm run openapi:generate` and commit.');
  process.exit(1);
}
console.log('OK: committed API types match openapi/openapi.json.');
process.exit(0);