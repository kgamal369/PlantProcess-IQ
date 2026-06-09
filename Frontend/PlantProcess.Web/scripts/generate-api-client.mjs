#!/usr/bin/env node
// T-019 - Generate typed API definitions from openapi/openapi.json using
// openapi-typescript. Output: src/api/generated/schema.d.ts
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const spec = path.resolve('openapi/openapi.json');
const outDir = path.resolve('src/api/generated');
const out = path.join(outDir, 'schema.d.ts');

if (!fs.existsSync(spec)) {
  console.error('openapi/openapi.json not found. Run `npm run openapi:export` against a running API first.');
  process.exit(1);
}
fs.mkdirSync(outDir, { recursive: true });
execFileSync('npx', ['openapi-typescript', spec, '-o', out], { stdio: 'inherit' });
console.log('Generated ' + out);