#!/usr/bin/env node
// T-019 - Export the API's OpenAPI document to openapi/openapi.json.
// Requires a running API. Default URL: http://localhost:5063/swagger/v1/swagger.json
// Override with PPIQ_OPENAPI_URL.
import fs from 'node:fs';
import path from 'node:path';

const url = process.env.PPIQ_OPENAPI_URL || 'http://localhost:5063/swagger/v1/swagger.json';
const outDir = path.resolve('openapi');
const outFile = path.join(outDir, 'openapi.json');

const res = await fetch(url);
if (!res.ok) { console.error('Failed to fetch ' + url + ': HTTP ' + res.status); process.exit(1); }
const spec = await res.json();
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(outFile, JSON.stringify(spec, null, 2) + '\n');
console.log('Wrote ' + outFile + ' from ' + url);
console.log('Next: npm run openapi:generate   (then commit openapi/ and src/api/generated/)');