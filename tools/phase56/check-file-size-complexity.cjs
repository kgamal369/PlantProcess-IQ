const fs = require('fs');
const path = require('path');

const root = process.cwd();
const frontendRoot = path.join(root, 'Frontend', 'PlantProcess.Web');
const srcRoot = path.join(frontendRoot, 'src');
const baselinePath = path.join(root, 'tools', 'phase56', 'phase56-file-size-baseline.json');

const warnAt = 500;
const errorAt = Number(process.env.PPIQ_FILE_SIZE_ERROR_AT || 700);

const explicitTrackedTargets = new Set([
  'src/components/dashboarding/WidgetBuilderWizardContent.implementation.tsx',
  'src/components/dashboarding/WidgetBuilderWizard.implementation.tsx',
  'src/api/productCoreApiClient.implementation.ts',
  'src/pages/Admin/AdminDbConfigurationTab.implementation.tsx',
  'src/pages/MaterialAnalytics/MaterialAnalyticsPages.implementation.tsx',
  'src/App.implementation.tsx'
]);

function walk(dir) {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return walk(full);
    return [full];
  });
}

function readBaseline() {
  if (!fs.existsSync(baselinePath)) return new Set();

  const payload = JSON.parse(fs.readFileSync(baselinePath, 'utf8'));
  return new Set((payload.files || []).map((entry) => entry.path));
}

function lineCount(file) {
  const text = fs.readFileSync(file, 'utf8');
  return text.split(/\r?\n/).length;
}

function isTracked(relativePath, baseline) {
  if (baseline.has(relativePath)) return true;
  if (explicitTrackedTargets.has(relativePath)) return true;
  if (relativePath.includes('/phase56-migrated-legacy.css')) return true;
  return false;
}

const baseline = readBaseline();
const offenders = [];
const warnings = [];

for (const file of walk(srcRoot)) {
  if (!/\.(ts|tsx|css)$/.test(file)) continue;

  const relativePath = path.relative(frontendRoot, file).split(path.sep).join('/');
  const lines = lineCount(file);
  const tracked = isTracked(relativePath, baseline);

  if (lines > errorAt && !tracked) {
    offenders.push(`${relativePath}: ${lines} lines`);
  } else if (lines > warnAt) {
    warnings.push(`${relativePath}: ${lines} lines${tracked ? ' (tracked existing/P05 split target)' : ''}`);
  }
}

for (const warning of warnings) {
  console.warn('[WARN] ' + warning);
}

if (offenders.length > 0) {
  console.error('Unknown frontend god-file offenders:');
  for (const offender of offenders) {
    console.error(' - ' + offender);
  }
  console.error('');
  console.error('To accept a currently-existing file as a tracked P05 split target, regenerate the baseline intentionally:');
  console.error('node tools/phase56/create-file-size-baseline.cjs');
  process.exit(1);
}

console.log('Phase 5 file-size gate passed. Existing large files are tracked; unknown new god-files are blocked.');