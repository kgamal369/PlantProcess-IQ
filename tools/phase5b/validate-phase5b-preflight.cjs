const fs = require('fs');
const path = require('path');
const cp = require('child_process');

const root = process.cwd();
const reportJsonPath = path.join(root, 'docs', 'phase5b', 'phase5b-refactor-report.json');
const reportMdPath = path.join(root, 'docs', 'phase5b', 'phase5b-refactor-report.md');
const snapshotPath = path.join(root, 'tools', 'phase5b', 'phase5b-targets.snapshot.json');

function assertFile(file) {
  if (!fs.existsSync(file)) throw new Error('Missing required file: ' + path.relative(root, file));
}

assertFile(reportJsonPath);
assertFile(reportMdPath);
assertFile(snapshotPath);

const report = JSON.parse(fs.readFileSync(reportJsonPath, 'utf8'));
if (!Array.isArray(report.targets) || report.targets.length < 5) {
  throw new Error('Phase 5B report does not contain all required targets.');
}

for (const target of report.targets) {
  if (!target.exists) throw new Error('Missing refactor target: ' + target.rel);
  if (!target.sha256 || target.sha256.length !== 64) throw new Error('Missing SHA-256 for ' + target.rel);
  if (!Array.isArray(target.proposedModules) || target.proposedModules.length === 0) {
    throw new Error('No proposed modules for ' + target.rel);
  }
}

cp.execFileSync('node', ['tools/phase56/validate-phase56.cjs'], {
  cwd: root,
  stdio: 'inherit'
});

console.log('Phase 5B preflight validation passed.');
