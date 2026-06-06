const fs = require('fs');
const path = require('path');

const root = process.cwd();
const frontendRoot = path.join(root, 'Frontend', 'PlantProcess.Web');
const srcRoot = path.join(frontendRoot, 'src');
const baselinePath = path.join(root, 'tools', 'phase56', 'phase56-file-size-baseline.json');

function walk(dir) {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return walk(full);
    return [full];
  });
}

function lineCount(file) {
  const text = fs.readFileSync(file, 'utf8');
  return text.split(/\r?\n/).length;
}

const files = walk(srcRoot)
  .filter((file) => /\.(ts|tsx|css)$/.test(file))
  .map((file) => {
    const relativePath = path.relative(frontendRoot, file).split(path.sep).join('/');
    return {
      path: relativePath,
      lines: lineCount(file),
      capturedAsExistingPhase56Baseline: true
    };
  })
  .filter((entry) => entry.lines > 500)
  .sort((a, b) => a.path.localeCompare(b.path));

const payload = {
  generatedAtUtc: new Date().toISOString(),
  purpose: 'Phase 5/6 baseline of existing large frontend files. Future unknown large files should fail the gate.',
  warningThreshold: 500,
  errorThreshold: 700,
  files
};

fs.mkdirSync(path.dirname(baselinePath), { recursive: true });
fs.writeFileSync(baselinePath, JSON.stringify(payload, null, 2) + '\n', 'utf8');

console.log('Created Phase 5/6 file-size baseline: ' + baselinePath);
console.log('Tracked existing large files: ' + files.length);