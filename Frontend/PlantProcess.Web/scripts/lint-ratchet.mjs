#!/usr/bin/env node
// ============================================================================
// T-018 - Lint warning budget + ratchet.
// Counts ESLint warnings across the project and compares to a committed budget
// (lint-budget.json). Rules:
//   - Any ESLint ERROR fails (errors are never allowed).
//   - Warnings ABOVE budget fail (a regression).
//   - Warnings BELOW budget print how to ratchet the budget down.
//   - Missing budget file => NOT ARMED: warns loudly and exits 0 so adding the
//     CI gate does not break the pipeline before you initialise it.
// Arm it once:  npm run lint:ratchet:init   (then commit lint-budget.json)
// ============================================================================
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const BUDGET_FILE = path.resolve('lint-budget.json');
const args = process.argv.slice(2);
const wantInit = args.includes('--init');
const setIdx = args.indexOf('--set');
const forced = setIdx >= 0 ? Number(args[setIdx + 1]) : null;

function runEslintJson() {
  let out = '';
  try {
    out = execFileSync('npx', ['eslint', '.', '-f', 'json'], { encoding: 'utf8', maxBuffer: 128 * 1024 * 1024 });
  } catch (e) {
    out = (e.stdout || '').toString();
    if (!out) { console.error('Failed to run eslint:', e.message); process.exit(2); }
  }
  let report;
  try { report = JSON.parse(out); } catch { console.error('Could not parse eslint JSON.'); process.exit(2); }
  let errors = 0, warnings = 0;
  for (const f of report) { errors += f.errorCount || 0; warnings += f.warningCount || 0; }
  return { errors, warnings };
}

const { errors, warnings } = runEslintJson();
console.log('ESLint: ' + errors + ' error(s), ' + warnings + ' warning(s).');

if (wantInit || forced !== null) {
  const value = (forced !== null && !Number.isNaN(forced)) ? forced : warnings;
  fs.writeFileSync(BUDGET_FILE, JSON.stringify({ maxWarnings: value }, null, 2) + '\n');
  console.log('Wrote ' + path.basename(BUDGET_FILE) + ' with maxWarnings=' + value + '. Commit it.');
  process.exit(errors > 0 ? 1 : 0);
}

if (!fs.existsSync(BUDGET_FILE)) {
  console.warn('--------------------------------------------------------------');
  console.warn('RATCHET NOT ARMED: no lint-budget.json.');
  console.warn('Run `npm run lint:ratchet:init` and commit lint-budget.json to');
  console.warn('start enforcing the warning budget. (Errors already fail below.)');
  console.warn('--------------------------------------------------------------');
  process.exit(errors > 0 ? 1 : 0);
}

const budget = JSON.parse(fs.readFileSync(BUDGET_FILE, 'utf8'));
const max = Number(budget.maxWarnings);
if (Number.isNaN(max)) { console.error('lint-budget.json has no numeric maxWarnings.'); process.exit(1); }

if (errors > 0) { console.error('FAIL: ' + errors + ' lint error(s) - errors are never allowed.'); process.exit(1); }
if (warnings > max) {
  console.error('FAIL: ' + warnings + ' warnings exceed budget ' + max + '. Fix the new warnings.');
  process.exit(1);
}
if (warnings < max) {
  console.log('Ratchet: warnings (' + warnings + ') are below budget (' + max + '). Lower it:');
  console.log('  npm run lint:ratchet:init   (or: node scripts/lint-ratchet.mjs --set ' + warnings + ')');
}
console.log('OK: ' + warnings + ' warnings within budget ' + max + '.');
process.exit(0);