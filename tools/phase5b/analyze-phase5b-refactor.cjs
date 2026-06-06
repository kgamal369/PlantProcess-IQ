const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const cp = require('child_process');

const root = process.cwd();

const targets = [
  {
    taskId: 'T-036A',
    label: 'Widget builder content wizard',
    rel: 'Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizardContent.implementation.tsx',
    targetMaxLines: 400,
    preferredFolder: 'Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/content',
    strategy: 'Split by wizard sections/components, keep wrapper/orchestrator thin, extract PreviewTable/WizardSection/helper components.'
  },
  {
    taskId: 'T-036B',
    label: 'Widget builder wizard shell',
    rel: 'Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizard.implementation.tsx',
    targetMaxLines: 400,
    preferredFolder: 'Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/wizard',
    strategy: 'Split wizard shell into state hook, step navigation, preview orchestration, save/publish actions.'
  },
  {
    taskId: 'T-037A',
    label: 'Product core API client',
    rel: 'Frontend/PlantProcess.Web/src/api/productCoreApiClient.implementation.ts',
    targetMaxLines: 450,
    preferredFolder: 'Frontend/PlantProcess.Web/src/api/product-core',
    strategy: 'Split by endpoint domain: dashboard, materials, analytics, admin, mapping, workflow, license/demo/read-models.'
  },
  {
    taskId: 'T-037B',
    label: 'Admin DB configuration tab',
    rel: 'Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.implementation.tsx',
    targetMaxLines: 450,
    preferredFolder: 'Frontend/PlantProcess.Web/src/pages/Admin/db-configuration',
    strategy: 'Split into connection profile list, form/editor, validation/test panel, schema preview, helper hooks.'
  },
  {
    taskId: 'T-037C',
    label: 'Material analytics pages',
    rel: 'Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.implementation.tsx',
    targetMaxLines: 450,
    preferredFolder: 'Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/sections',
    strategy: 'Split by page section: overview, filters, KPI cards, charts, detail panels, helper formatters.'
  }
];

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function normalize(p) {
  return p.split(path.sep).join('/');
}

function readText(file) {
  return fs.readFileSync(file, 'utf8');
}

function writeText(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\n/g, '\r\n'), 'utf8');
  console.log('Wrote: ' + normalize(path.relative(root, file)));
}

function sha256(text) {
  return crypto.createHash('sha256').update(text, 'utf8').digest('hex');
}

function lineCount(text) {
  return text.replace(/\r\n/g, '\n').split('\n').length;
}

function topLevelDeclaration(line) {
  if (!/^\S/.test(line)) return null;

  const trimmed = line.trim();

  const patterns = [
    { kind: 'function', re: /^(export\s+)?(default\s+)?(async\s+)?function\s+([A-Za-z_$][\w$]*)/ },
    { kind: 'component-or-const', re: /^(export\s+)?const\s+([A-Za-z_$][\w$]*)\s*[:=]/ },
    { kind: 'let', re: /^(export\s+)?let\s+([A-Za-z_$][\w$]*)\s*[:=]/ },
    { kind: 'var', re: /^(export\s+)?var\s+([A-Za-z_$][\w$]*)\s*[:=]/ },
    { kind: 'type', re: /^(export\s+)?type\s+([A-Za-z_$][\w$]*)\b/ },
    { kind: 'interface', re: /^(export\s+)?interface\s+([A-Za-z_$][\w$]*)\b/ },
    { kind: 'class', re: /^(export\s+)?class\s+([A-Za-z_$][\w$]*)\b/ },
    { kind: 'enum', re: /^(export\s+)?enum\s+([A-Za-z_$][\w$]*)\b/ }
  ];

  for (const pattern of patterns) {
    const match = trimmed.match(pattern.re);

    if (!match) continue;

    const name = match[4] || match[2];
    return {
      kind: pattern.kind,
      name,
      exported: /^export\s+/.test(trimmed),
      raw: trimmed.slice(0, 180)
    };
  }

  return null;
}

function collectImports(lines) {
  const imports = [];
  let active = null;

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];

    if (active) {
      active.lines.push(line);
      if (line.includes(';')) {
        imports.push(active);
        active = null;
      }
      continue;
    }

    if (/^import\s/.test(line.trim())) {
      active = {
        startLine: index + 1,
        lines: [line]
      };

      if (line.includes(';')) {
        imports.push(active);
        active = null;
      }
    }
  }

  return imports.map((entry) => ({
    startLine: entry.startLine,
    endLine: entry.startLine + entry.lines.length - 1,
    text: entry.lines.join('\n')
  }));
}

function classifyDeclaration(target, declaration) {
  const name = declaration.name.toLowerCase();
  const rel = target.rel.toLowerCase();

  if (rel.includes('productcoreapiclient')) {
    if (/dashboard|reference|widget/.test(name)) return 'dashboard-api';
    if (/material|genealogy|unit/.test(name)) return 'material-api';
    if (/correlation|analytics|risk|quality|defect|inspection/.test(name)) return 'analytics-api';
    if (/admin|schema|mapping|configuration|import|job/.test(name)) return 'admin-config-api';
    if (/license|commercial|feature|gate/.test(name)) return 'license-api';
    if (/workflow|process|operation/.test(name)) return 'workflow-api';
    return 'shared-api-types';
  }

  if (rel.includes('widgetbuilderwizardcontent')) {
    if (/preview|table|chart|insight/.test(name)) return 'preview-components';
    if (/section|field|filter|measure|dimension|query/.test(name)) return 'step-sections';
    if (/empty|badge|card|pill|summary/.test(name)) return 'shared-ui';
    return 'content-orchestration';
  }

  if (rel.includes('widgetbuilderwizard.implementation')) {
    if (/state|reducer|effect|load|save|publish|optimistic/.test(name)) return 'wizard-hooks-actions';
    if (/step|navigation|footer|header/.test(name)) return 'wizard-shell-components';
    if (/preview|chart|script/.test(name)) return 'wizard-preview';
    return 'wizard-orchestrator';
  }

  if (rel.includes('admindbconfigurationtab')) {
    if (/connection|profile|credential|database|provider/.test(name)) return 'connection-profile-section';
    if (/form|field|editor|modal/.test(name)) return 'db-config-editor';
    if (/test|validation|health|status/.test(name)) return 'db-validation-panel';
    if (/schema|table|preview/.test(name)) return 'schema-preview-section';
    return 'admin-db-shared';
  }

  if (rel.includes('materialanalyticspages')) {
    if (/filter|query|selector/.test(name)) return 'filter-section';
    if (/overview|summary|kpi|metric|card/.test(name)) return 'overview-section';
    if (/chart|trend|distribution/.test(name)) return 'chart-section';
    if (/detail|table|grid|list/.test(name)) return 'details-section';
    return 'material-analytics-shared';
  }

  return 'shared';
}

function scanTarget(target) {
  const absolute = path.join(root, target.rel);

  if (!fs.existsSync(absolute)) {
    return {
      ...target,
      exists: false,
      error: 'File not found'
    };
  }

  const text = readText(absolute);
  const lines = text.replace(/\r\n/g, '\n').split('\n');
  const imports = collectImports(lines);
  const declarations = [];

  for (let index = 0; index < lines.length; index += 1) {
    const declaration = topLevelDeclaration(lines[index]);

    if (declaration) {
      declarations.push({
        ...declaration,
        startLine: index + 1
      });
    }
  }

  for (let index = 0; index < declarations.length; index += 1) {
    declarations[index].endLine =
      index + 1 < declarations.length
        ? declarations[index + 1].startLine - 1
        : lines.length;

    declarations[index].approxLines =
      declarations[index].endLine - declarations[index].startLine + 1;

    declarations[index].suggestedModuleGroup =
      classifyDeclaration(target, declarations[index]);
  }

  const modules = new Map();

  for (const declaration of declarations) {
    const key = declaration.suggestedModuleGroup;

    if (!modules.has(key)) {
      modules.set(key, {
        group: key,
        suggestedFile: `${target.preferredFolder}/${key}.tsx`,
        declarations: [],
        approxLines: 0
      });
    }

    const module = modules.get(key);
    module.declarations.push({
      name: declaration.name,
      kind: declaration.kind,
      startLine: declaration.startLine,
      endLine: declaration.endLine,
      approxLines: declaration.approxLines,
      exported: declaration.exported
    });
    module.approxLines += declaration.approxLines;
  }

  return {
    ...target,
    exists: true,
    absolutePath: absolute,
    lineCount: lineCount(text),
    sha256: sha256(text),
    importBlockCount: imports.length,
    exportLineCount: lines.filter((line) => line.trim().startsWith('export ')).length,
    topLevelDeclarationCount: declarations.length,
    declarations,
    proposedModules: Array.from(modules.values()).sort((a, b) => a.group.localeCompare(b.group)),
    risk: lineCount(text) > target.targetMaxLines ? 'requires-split' : 'already-under-target'
  };
}

function markdownReport(report) {
  const lines = [];

  lines.push('# PlantProcess IQ Phase 5B Refactor Preflight');
  lines.push('');
  lines.push(`Generated: ${report.generatedAtUtc}`);
  lines.push('');
  lines.push('## Purpose');
  lines.push('');
  lines.push('This report prepares the actual Phase 5B god-file decomposition. It does not modify source files.');
  lines.push('It records current hashes, line counts, imports, top-level declarations, and proposed module groups.');
  lines.push('');
  lines.push('## Summary');
  lines.push('');
  lines.push('| Task | File | Lines | Declarations | Imports | Risk |');
  lines.push('|---|---:|---:|---:|---:|---|');

  for (const target of report.targets) {
    lines.push(`| ${target.taskId} | \`${target.rel}\` | ${target.lineCount ?? 'missing'} | ${target.topLevelDeclarationCount ?? 0} | ${target.importBlockCount ?? 0} | ${target.risk ?? target.error} |`);
  }

  lines.push('');
  lines.push('## Target details');

  for (const target of report.targets) {
    lines.push('');
    lines.push(`### ${target.taskId} — ${target.label}`);
    lines.push('');
    lines.push(`- File: \`${target.rel}\``);
    lines.push(`- Current lines: ${target.lineCount ?? 'missing'}`);
    lines.push(`- Target max lines per resulting file: ${target.targetMaxLines}`);
    lines.push(`- Current SHA-256: \`${target.sha256 ?? 'missing'}\``);
    lines.push(`- Strategy: ${target.strategy}`);
    lines.push('');

    if (!target.exists) {
      lines.push(`Missing file: ${target.error}`);
      continue;
    }

    lines.push('#### Proposed module groups');
    lines.push('');
    lines.push('| Group | Suggested file | Approx lines | Declarations |');
    lines.push('|---|---|---:|---:|');

    for (const module of target.proposedModules) {
      lines.push(`| ${module.group} | \`${module.suggestedFile}\` | ${module.approxLines} | ${module.declarations.length} |`);
    }

    lines.push('');
    lines.push('#### Top-level declaration inventory');
    lines.push('');
    lines.push('| Declaration | Kind | Lines | Group | Exported |');
    lines.push('|---|---|---:|---|---|');

    for (const declaration of target.declarations) {
      lines.push(`| \`${declaration.name}\` | ${declaration.kind} | ${declaration.startLine}-${declaration.endLine} | ${declaration.suggestedModuleGroup} | ${declaration.exported ? 'yes' : 'no'} |`);
    }
  }

  lines.push('');
  lines.push('## Safe next implementation order');
  lines.push('');
  lines.push('1. Split `productCoreApiClient.implementation.ts` first because API-domain split is usually easiest to build-verify.');
  lines.push('2. Split `MaterialAnalyticsPages.implementation.tsx` by section after API imports are stable.');
  lines.push('3. Split `AdminDbConfigurationTab.implementation.tsx` by editor/list/validation/schema preview.');
  lines.push('4. Split widget-builder files last, because wizard behavior must be preserved exactly.');
  lines.push('');
  lines.push('## Required gate after each split');
  lines.push('');
  lines.push('```powershell');
  lines.push('powershell -ExecutionPolicy Bypass -File .\\tools\\phase56\\Invoke-Phase5Phase6Validation.ps1 -ProjectRoot "C:\\Workspace\\PlantProcess-IQ" -RunFrontendBuild');
  lines.push('```');
  lines.push('');

  return lines.join('\n');
}

function writeValidator() {
  const validatorPath = path.join(root, 'tools', 'phase5b', 'validate-phase5b-preflight.cjs');

  const validator = `const fs = require('fs');
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
`;

  writeText(validatorPath, validator);
}

const report = {
  generatedAtUtc: new Date().toISOString(),
  phase: 'P05B · God-file decomposition preflight',
  mode: 'read-only source analysis',
  targets: targets.map(scanTarget)
};

const docsDir = path.join(root, 'docs', 'phase5b');
const toolsDir = path.join(root, 'tools', 'phase5b');
ensureDir(docsDir);
ensureDir(toolsDir);

writeText(path.join(docsDir, 'phase5b-refactor-report.json'), JSON.stringify(report, null, 2) + '\n');
writeText(path.join(docsDir, 'phase5b-refactor-report.md'), markdownReport(report) + '\n');
writeText(path.join(toolsDir, 'phase5b-targets.snapshot.json'), JSON.stringify({
  generatedAtUtc: report.generatedAtUtc,
  targets: report.targets.map((target) => ({
    taskId: target.taskId,
    rel: target.rel,
    lineCount: target.lineCount,
    sha256: target.sha256,
    topLevelDeclarationCount: target.topLevelDeclarationCount,
    proposedModuleCount: target.proposedModules ? target.proposedModules.length : 0
  }))
}, null, 2) + '\n');

writeValidator();

console.log('');
console.log('Phase 5B refactor preflight completed.');
console.log('Report JSON: docs/phase5b/phase5b-refactor-report.json');
console.log('Report MD  : docs/phase5b/phase5b-refactor-report.md');
console.log('Snapshot   : tools/phase5b/phase5b-targets.snapshot.json');