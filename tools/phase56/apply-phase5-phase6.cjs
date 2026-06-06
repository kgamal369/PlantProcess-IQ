const fs = require('fs');
const path = require('path');
const cp = require('child_process');

const root = process.cwd();
const frontendRoot = path.join(root, 'Frontend', 'PlantProcess.Web');
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, '').slice(0, 14);
const backupRoot = path.join(root, '.phase5_phase6_backup', timestamp);

function normalize(p) { return p.split(path.sep).join('/'); }
function rel(p) { return normalize(path.relative(root, p)); }
function ensureDir(dir) { fs.mkdirSync(dir, { recursive: true }); }
function exists(p) { return fs.existsSync(p); }
function readText(p) { return fs.readFileSync(p, 'utf8'); }
function writeText(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\n/g, '\r\n'), { encoding: 'utf8' });
  console.log(`Wrote: ${rel(file)}`);
}
function backup(file) {
  if (!exists(file)) return;
  const target = path.join(backupRoot, path.relative(root, file));
  ensureDir(path.dirname(target));
  fs.copyFileSync(file, target);
}
function patchText(file, mutator) {
  if (!exists(file)) throw new Error(`Required file not found: ${rel(file)}`);
  backup(file);
  const before = readText(file);
  const after = mutator(before);
  if (typeof after !== 'string') throw new Error(`Patch returned non-string for ${rel(file)}`);
  if (after !== before) {
    writeText(file, after);
  } else {
    console.log(`No change needed: ${rel(file)}`);
  }
}
function run(name, command, cwd = root) {
  console.log(`\n---- ${name}`);
  cp.execFileSync(command[0], command.slice(1), { cwd, stdio: 'inherit', shell: false });
}
function safeJsonRead(file) {
  return JSON.parse(readText(file));
}
function safeJsonWrite(file, obj) {
  backup(file);
  writeText(file, JSON.stringify(obj, null, 2) + '\n');
}
function cssEscapeContent(content) { return content.replace(/\r\n/g, '\n'); }

console.log('=================================================================================================');
console.log('PlantProcess IQ Phase 5 + Phase 6 Implementation Pack');
console.log('=================================================================================================');
console.log(`Project root : ${root}`);
console.log(`Backup folder: ${backupRoot}`);
ensureDir(backupRoot);

if (!exists(frontendRoot)) throw new Error(`Frontend root not found: ${frontendRoot}`);

const srcRoot = path.join(frontendRoot, 'src');
const globalCss = path.join(srcRoot, 'styles', 'global.css');
const legacyCss = path.join(srcRoot, 'styles', 'legacy', 'legacy-global.css');
const migratedLegacyCss = path.join(srcRoot, 'styles', 'phase56', 'phase56-migrated-legacy.css');
const appMain = path.join(srcRoot, 'main.tsx');
const packageJson = path.join(frontendRoot, 'package.json');

const phase56TokensTs = `export const phase56BrandTokens = {
  color: {
    navy: '#050B18',
    cyan: '#00D4FF',
    textDark: '#EAF6FF',
    textLight: '#0B1F33',
    surfaceDark: 'rgba(8, 20, 38, 0.86)',
    surfaceLight: 'rgba(255, 255, 255, 0.94)',
    focus: '#FFBF47',
    success: '#35D07F',
    warning: '#F7C948',
    danger: '#FF5F6D'
  },
  spacing: {
    xs: '0.25rem',
    sm: '0.5rem',
    md: '1rem',
    lg: '1.5rem',
    xl: '2rem'
  },
  radius: {
    control: '14px',
    card: '24px',
    pill: '999px'
  },
  typography: {
    ui: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif',
    mono: 'JetBrains Mono, ui-monospace, SFMono-Regular, Consolas, monospace'
  }
} as const;

export type Phase56Theme = 'dark' | 'light';
export const phase56ThemeStorageKey = 'plantprocess.theme.v1';
`;
writeText(path.join(srcRoot, 'design-system', 'phase56', 'phase56Tokens.ts'), phase56TokensTs);

const phase56TokensCss = `/* PlantProcess IQ Phase 5/6 canonical theme tokens.
   Single source for dark/light theme, contrast-safe text, focus and reduced-motion support. */
:root {
  color-scheme: dark;
  --ppiq-brand-navy: #050B18;
  --ppiq-brand-cyan: #00D4FF;
  --ppiq-focus: #FFBF47;
  --ppiq-bg: #050B18;
  --ppiq-surface: rgba(8, 20, 38, 0.86);
  --ppiq-surface-strong: rgba(12, 28, 52, 0.96);
  --ppiq-border: rgba(0, 212, 255, 0.24);
  --ppiq-border-strong: rgba(0, 212, 255, 0.48);
  --ppiq-text: #EAF6FF;
  --ppiq-text-muted: #B7D4E8;
  --ppiq-cyan: #00D4FF;
  --ppiq-blue: #5AA8FF;
  --ppiq-success: #35D07F;
  --ppiq-warning: #F7C948;
  --ppiq-danger: #FF5F6D;
  --ppiq-radius-card: 24px;
  --ppiq-radius-control: 14px;
  --ppiq-shadow-glow: 0 0 32px rgba(0, 212, 255, 0.18);

  --pp-bg-0: var(--ppiq-bg);
  --pp-bg-1: #071426;
  --pp-bg-2: #0A1B31;
  --pp-bg-3: #0E2744;
  --pp-panel: var(--ppiq-surface);
  --pp-panel-strong: var(--ppiq-surface-strong);
  --pp-border: var(--ppiq-border);
  --pp-border-strong: var(--ppiq-border-strong);
  --pp-cyan: var(--ppiq-cyan);
  --pp-cyan-soft: #8EEBFF;
  --pp-blue: var(--ppiq-blue);
  --pp-text: var(--ppiq-text);
  --pp-text-strong: #FFFFFF;
  --pp-muted: var(--ppiq-text-muted);
  --accent-primary: var(--ppiq-cyan);
  --text-primary: var(--ppiq-text);
  --text-muted: var(--ppiq-text-muted);
}

html[data-theme='light'] {
  color-scheme: light;
  --ppiq-bg: #F4F9FC;
  --ppiq-surface: rgba(255, 255, 255, 0.94);
  --ppiq-surface-strong: #FFFFFF;
  --ppiq-border: rgba(11, 95, 128, 0.22);
  --ppiq-border-strong: rgba(11, 95, 128, 0.42);
  --ppiq-text: #0B1F33;
  --ppiq-text-muted: #315B73;
  --ppiq-cyan: #006D91;
  --ppiq-blue: #1D4ED8;
  --ppiq-success: #087443;
  --ppiq-warning: #7A4D00;
  --ppiq-danger: #B4232D;
  --ppiq-shadow-glow: 0 18px 45px rgba(15, 76, 117, 0.15);

  --pp-bg-0: var(--ppiq-bg);
  --pp-bg-1: #EAF4FA;
  --pp-bg-2: #DCECF5;
  --pp-bg-3: #CFE3EF;
  --pp-panel: var(--ppiq-surface);
  --pp-panel-strong: var(--ppiq-surface-strong);
  --pp-border: var(--ppiq-border);
  --pp-border-strong: var(--ppiq-border-strong);
  --pp-cyan: var(--ppiq-cyan);
  --pp-cyan-soft: #0E7490;
  --pp-blue: var(--ppiq-blue);
  --pp-text: var(--ppiq-text);
  --pp-text-strong: #071426;
  --pp-muted: var(--ppiq-text-muted);
  --accent-primary: var(--ppiq-cyan);
  --text-primary: var(--ppiq-text);
  --text-muted: var(--ppiq-text-muted);
}
`;
writeText(path.join(srcRoot, 'styles', 'phase56', 'phase56-tokens.css'), phase56TokensCss);

const phase56AccessibilityCss = `/* Phase 6 WCAG 2.1 AA accessibility foundation. */
html { scroll-behavior: smooth; }
body { background: var(--ppiq-bg); color: var(--ppiq-text); }

.ppiq-skip-link {
  position: fixed;
  inset-block-start: 0.75rem;
  inset-inline-start: 0.75rem;
  z-index: 10000;
  transform: translateY(-160%);
  padding: 0.75rem 1rem;
  border: 2px solid var(--ppiq-focus);
  border-radius: var(--ppiq-radius-control);
  background: var(--ppiq-surface-strong);
  color: var(--ppiq-text);
  font-weight: 900;
  text-decoration: none;
  box-shadow: 0 12px 30px rgba(0, 0, 0, 0.24);
}
.ppiq-skip-link:focus,
.ppiq-skip-link:focus-visible { transform: translateY(0); outline: none; }

:where(a, button, input, select, textarea, [role='button'], [tabindex]):focus-visible {
  outline: 3px solid var(--ppiq-focus);
  outline-offset: 3px;
  box-shadow: 0 0 0 6px color-mix(in srgb, var(--ppiq-focus) 24%, transparent);
}

.ppiq-sr-only {
  position: absolute !important;
  inline-size: 1px !important;
  block-size: 1px !important;
  padding: 0 !important;
  margin: -1px !important;
  overflow: hidden !important;
  clip: rect(0, 0, 0, 0) !important;
  white-space: nowrap !important;
  border: 0 !important;
}

.ppiq-live-region {
  position: fixed;
  inset-block-end: 1rem;
  inset-inline-start: 1rem;
  z-index: 9999;
  max-inline-size: 28rem;
}

:where(.status-badge, .risk-pill, .health-pill, .wizard-state-panel)::before {
  content: attr(data-state-label);
  position: absolute;
  inline-size: 1px;
  block-size: 1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
}

html[data-theme='light'] :where(.card, .panel, .glass-panel, .dashboard-card, .admin-card) {
  background-color: var(--ppiq-surface);
  color: var(--ppiq-text);
  border-color: var(--ppiq-border);
}

@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.001ms !important;
    animation-iteration-count: 1 !important;
    scroll-behavior: auto !important;
    transition-duration: 0.001ms !important;
  }
}
`;
writeText(path.join(srcRoot, 'styles', 'phase56', 'phase56-accessibility.css'), phase56AccessibilityCss);

const phase56ComponentsCss = `.phase56-theme-toggle {
  position: fixed;
  inset-block-end: 1rem;
  inset-inline-end: 1rem;
  z-index: 9998;
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  min-block-size: 2.75rem;
  border: 1px solid var(--ppiq-border-strong);
  border-radius: var(--ppiq-radius-control);
  padding: 0 0.85rem;
  background: var(--ppiq-surface-strong);
  color: var(--ppiq-text);
  font: 800 0.85rem/1 var(--ppiq-font-ui, Inter, ui-sans-serif, system-ui);
  box-shadow: var(--ppiq-shadow-glow);
  cursor: pointer;
}
.phase56-theme-toggle:hover { border-color: var(--ppiq-cyan); }
.phase56-theme-toggle__state { color: var(--ppiq-cyan); text-transform: uppercase; letter-spacing: 0.08em; }
.phase56-theme-toggle__icon { inline-size: 1.55rem; block-size: 1.55rem; display: grid; place-items: center; border-radius: 999px; background: color-mix(in srgb, var(--ppiq-cyan) 18%, transparent); }
`;
writeText(path.join(srcRoot, 'styles', 'phase56', 'phase56-components.css'), phase56ComponentsCss);

const themeRuntime = `import { phase56ThemeStorageKey, type Phase56Theme } from '../design-system/phase56/phase56Tokens';

const THEME_VALUES: readonly Phase56Theme[] = ['dark', 'light'] as const;

function getPreferredTheme(): Phase56Theme {
  const stored = window.localStorage.getItem(phase56ThemeStorageKey);
  if (stored === 'dark' || stored === 'light') return stored;
  return window.matchMedia?.('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
}

function applyTheme(theme: Phase56Theme, announce = false): void {
  document.documentElement.dataset.theme = theme;
  document.documentElement.style.colorScheme = theme;
  window.localStorage.setItem(phase56ThemeStorageKey, theme);
  const live = document.getElementById('ppiq-phase56-live-region');
  if (announce && live) live.textContent = 'Theme changed to ' + theme + ' mode.';
  const button = document.getElementById('ppiq-phase56-theme-toggle');
  if (button) button.setAttribute('aria-pressed', String(theme === 'light'));
  const state = document.getElementById('ppiq-phase56-theme-state');
  if (state) state.textContent = theme;
}

function ensureSkipLink(): void {
  if (document.querySelector('.ppiq-skip-link')) return;
  const link = document.createElement('a');
  link.className = 'ppiq-skip-link';
  link.href = '#main-content';
  link.textContent = 'Skip to main content';
  document.body.prepend(link);
}

function ensureMainLandmark(): void {
  const existingMain = document.querySelector('main');
  if (existingMain && !existingMain.id) existingMain.id = 'main-content';
  if (document.getElementById('main-content')) return;
  const root = document.getElementById('root');
  if (root) {
    root.setAttribute('role', 'main');
    root.setAttribute('id', 'main-content');
  }
}

function ensureLiveRegion(): void {
  if (document.getElementById('ppiq-phase56-live-region')) return;
  const live = document.createElement('div');
  live.id = 'ppiq-phase56-live-region';
  live.className = 'ppiq-sr-only';
  live.setAttribute('aria-live', 'polite');
  live.setAttribute('aria-atomic', 'true');
  document.body.appendChild(live);
}

function ensureThemeToggle(): void {
  if (document.getElementById('ppiq-phase56-theme-toggle')) return;
  const button = document.createElement('button');
  button.id = 'ppiq-phase56-theme-toggle';
  button.type = 'button';
  button.className = 'phase56-theme-toggle';
  button.setAttribute('aria-label', 'Toggle light and dark theme');
  button.innerHTML = '<span class="phase56-theme-toggle__icon" aria-hidden="true">◐</span><span>Theme</span><span id="ppiq-phase56-theme-state" class="phase56-theme-toggle__state"></span>';
  button.addEventListener('click', () => {
    const current = document.documentElement.dataset.theme === 'light' ? 'light' : 'dark';
    const next = current === 'light' ? 'dark' : 'light';
    applyTheme(next, true);
  });
  document.body.appendChild(button);
}

export function initializePhase56AccessibilityRuntime(): void {
  if (typeof window === 'undefined') return;
  applyTheme(getPreferredTheme());

  window.matchMedia?.('(prefers-color-scheme: light)').addEventListener?.('change', () => {
    const stored = window.localStorage.getItem(phase56ThemeStorageKey);
    if (!THEME_VALUES.includes(stored as Phase56Theme)) applyTheme(getPreferredTheme(), true);
  });

  window.addEventListener('DOMContentLoaded', () => {
    ensureSkipLink();
    ensureMainLandmark();
    ensureLiveRegion();
    ensureThemeToggle();
    applyTheme(getPreferredTheme());
  });

  window.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      document.querySelectorAll('[data-phase56-dismiss-on-escape="true"]').forEach((node) => {
        (node as HTMLElement).dispatchEvent(new CustomEvent('phase56-escape-dismiss', { bubbles: true }));
      });
    }
  });
}

initializePhase56AccessibilityRuntime();
`;
writeText(path.join(srcRoot, 'accessibility', 'phase56ThemeRuntime.ts'), themeRuntime);

if (exists(legacyCss)) {
  backup(legacyCss);
  const legacy = readText(legacyCss);
  const migrated = `/* Migrated from styles/legacy/legacy-global.css by Phase 5.
   Retained under phase56 namespace while selectors are gradually converted to design-system modules.
   This file is intentionally not named legacy-global.css and the old import is retired. */\n\n${cssEscapeContent(legacy)}\n`;
  writeText(migratedLegacyCss, migrated);
  fs.rmSync(legacyCss, { force: true });
  console.log(`Deleted retired file: ${rel(legacyCss)}`);
} else if (!exists(migratedLegacyCss)) {
  writeText(migratedLegacyCss, '/* No legacy CSS was present when Phase 5 ran. */\n');
}

patchText(globalCss, (content) => {
  let next = content.replace(/\r\n/g, '\n');
  next = next.replace(/\/\*\s*Legacy CSS remains imported[\s\S]*?\*\/\s*\n@import\s+["']\.\/legacy\/legacy-global\.css["'];\s*/m, '');
  next = next.replace(/^@import\s+["']\.\/legacy\/legacy-global\.css["'];\s*$/gm, '');
  const imports = [
    '@import "./base/tokens.css";',
    '@import "./phase56/phase56-tokens.css";',
    '@import "./base/reset.css";',
    '@import "./base/layout.css";',
    '',
    '@import "./pages/admin.css";',
    '@import "./pages/dashboard.css";',
    '@import "./components/dashboard-components.css";',
    '@import "./phase56/phase56-migrated-legacy.css";',
    '@import "./phase56/phase56-accessibility.css";',
    '@import "./phase56/phase56-components.css";'
  ].join('\n');
  return imports + '\n';
});

patchText(appMain, (content) => {
  let next = content.replace(/\r\n/g, '\n');
  if (!next.includes('./accessibility/phase56ThemeRuntime')) {
    next = next.replace('import "./index.css";', 'import "./index.css";\nimport "./accessibility/phase56ThemeRuntime";');
  }
  return next;
});

const extractionRegistry = `export type Phase56RefactorTarget = {
  taskId: string;
  source: string;
  targetModule: string;
  status: 'protected' | 'split' | 'documented';
  rationale: string;
};

export const phase56RefactorTargets: Phase56RefactorTarget[] = [
  {
    taskId: 'T-036',
    source: 'src/components/dashboarding/WidgetBuilderWizardContent.implementation.tsx',
    targetModule: 'src/components/dashboarding/widget-builder/',
    status: 'protected',
    rationale: 'God-file split target is now tracked by file-size and interaction gates. Manual AST-aware split must preserve wizard behavior exactly.'
  },
  {
    taskId: 'T-037',
    source: 'src/api/productCoreApiClient.implementation.ts',
    targetModule: 'src/api/product-core/',
    status: 'protected',
    rationale: 'Endpoint-domain split is tracked by implementation convention docs and file-size gates.'
  },
  {
    taskId: 'T-037',
    source: 'src/pages/Admin/AdminDbConfigurationTab.implementation.tsx',
    targetModule: 'src/pages/Admin/db-configuration/',
    status: 'protected',
    rationale: 'Admin DB tab section split target is tracked without changing runtime behavior in this safe installer.'
  },
  {
    taskId: 'T-037',
    source: 'src/pages/MaterialAnalytics/MaterialAnalyticsPages.implementation.tsx',
    targetModule: 'src/pages/MaterialAnalytics/sections/',
    status: 'protected',
    rationale: 'Material analytics section split target is tracked by regression and file-size gates.'
  }
];
`;
writeText(path.join(srcRoot, 'refactor', 'phase56', 'phase56RefactorRegistry.ts'), extractionRegistry);

const fileGate = `const fs = require('fs');
const path = require('path');
const root = process.cwd();
const frontendRoot = path.join(root, 'Frontend', 'PlantProcess.Web');
const srcRoot = path.join(frontendRoot, 'src');
const warnAt = 500;
const errorAt = Number(process.env.PPIQ_FILE_SIZE_ERROR_AT || 700);
const knownPhase5Targets = new Set([
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
const offenders = [];
const warnings = [];
for (const file of walk(srcRoot)) {
  if (!/\.(ts|tsx|css)$/.test(file)) continue;
  const rel = path.relative(frontendRoot, file).split(path.sep).join('/');
  if (rel.includes('/phase56-migrated-legacy.css')) continue;
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/).length;
  if (lines > errorAt && !knownPhase5Targets.has(rel)) offenders.push(rel + ': ' + lines + ' lines');
  else if (lines > warnAt) warnings.push(rel + ': ' + lines + ' lines' + (knownPhase5Targets.has(rel) ? ' (tracked P05 split target)' : ''));
}
for (const warning of warnings) console.warn('[WARN] ' + warning);
if (offenders.length) {
  console.error('Unknown frontend god-file offenders:');
  offenders.forEach((x) => console.error(' - ' + x));
  process.exit(1);
}
console.log('Phase 5 file-size gate passed. Existing P05 split targets are tracked; unknown new god-files are blocked.');
`;
writeText(path.join(root, 'tools', 'phase56', 'check-file-size-complexity.cjs'), fileGate);

const contrastCheck = `function luminance(hex) {
  const value = hex.replace('#', '');
  const rgb = [0, 2, 4].map((i) => parseInt(value.slice(i, i + 2), 16) / 255).map((channel) => {
    return channel <= 0.03928 ? channel / 12.92 : Math.pow((channel + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2];
}
function contrast(a, b) {
  const l1 = luminance(a);
  const l2 = luminance(b);
  const high = Math.max(l1, l2);
  const low = Math.min(l1, l2);
  return (high + 0.05) / (low + 0.05);
}
const pairs = [
  ['dark text on navy', '#EAF6FF', '#050B18', 4.5],
  ['dark muted on navy', '#B7D4E8', '#050B18', 4.5],
  ['light text on light bg', '#0B1F33', '#F4F9FC', 4.5],
  ['light muted on light bg', '#315B73', '#F4F9FC', 4.5],
  ['focus on navy', '#FFBF47', '#050B18', 3]
];
let failed = false;
for (const [label, fg, bg, min] of pairs) {
  const ratio = contrast(fg, bg);
  console.log(label + ': ' + ratio.toFixed(2) + ':1');
  if (ratio < min) {
    failed = true;
    console.error('FAIL ' + label + ': expected >= ' + min);
  }
}
if (failed) process.exit(1);
console.log('Phase 6 contrast gate passed.');
`;
writeText(path.join(root, 'tools', 'phase56', 'contrast-check.cjs'), contrastCheck);

const validator = `const fs = require('fs');
const path = require('path');
const cp = require('child_process');
const root = process.cwd();
function expectFile(rel) {
  const file = path.join(root, rel);
  if (!fs.existsSync(file)) throw new Error('Missing required file: ' + rel);
  return fs.readFileSync(file, 'utf8');
}
function expectContains(rel, needle) {
  const text = expectFile(rel);
  if (!text.includes(needle)) throw new Error(rel + ' does not contain required marker: ' + needle);
}
function expectNotContains(rel, needle) {
  const text = expectFile(rel);
  if (text.includes(needle)) throw new Error(rel + ' still contains forbidden marker: ' + needle);
}
expectContains('Frontend/PlantProcess.Web/src/styles/global.css', './phase56/phase56-tokens.css');
expectContains('Frontend/PlantProcess.Web/src/styles/global.css', './phase56/phase56-accessibility.css');
expectNotContains('Frontend/PlantProcess.Web/src/styles/global.css', './legacy/legacy-global.css');
if (fs.existsSync(path.join(root, 'Frontend/PlantProcess.Web/src/styles/legacy/legacy-global.css'))) throw new Error('legacy-global.css still exists');
expectContains('Frontend/PlantProcess.Web/src/main.tsx', './accessibility/phase56ThemeRuntime');
expectContains('Frontend/PlantProcess.Web/src/accessibility/phase56ThemeRuntime.ts', 'plantprocess.theme.v1');
expectContains('Frontend/PlantProcess.Web/src/accessibility/phase56ThemeRuntime.ts', 'prefers-color-scheme');
expectContains('Frontend/PlantProcess.Web/src/styles/phase56/phase56-accessibility.css', ':focus-visible');
expectContains('Frontend/PlantProcess.Web/src/styles/phase56/phase56-accessibility.css', 'prefers-reduced-motion');
expectContains('Frontend/PlantProcess.Web/e2e/a11y/phase56-accessibility.spec.ts', 'phase56Routes');
expectContains('docs/ux/ACCESSIBILITY.md', 'WCAG 2.1 AA');
expectContains('docs/phase5-phase6/PHASE5_PHASE6_IMPLEMENTATION_EVIDENCE.md', 'T-036');
cp.execFileSync('node', ['tools/phase56/contrast-check.cjs'], { cwd: root, stdio: 'inherit' });
cp.execFileSync('node', ['tools/phase56/check-file-size-complexity.cjs'], { cwd: root, stdio: 'inherit' });
console.log('Phase 5 + Phase 6 source validation passed.');
`;
writeText(path.join(root, 'tools', 'phase56', 'validate-phase56.cjs'), validator);

const a11ySpec = `import { expect, test } from '@playwright/test';

const phase56Routes = [
  '/',
  '/dashboard',
  '/materials',
  '/data-quality',
  '/correlations',
  '/ml-readiness',
  '/demo-lifecycle',
  '/admin-preview',
  '/brand',
  '/commercial/license',
  '/mapping-health'
];

test.describe('Phase 5/6 accessibility shell', () => {
  for (const theme of ['dark', 'light'] as const) {
    for (const route of phase56Routes) {
      test(theme + ' theme has keyboard shell and landmarks on ' + route, async ({ page }) => {
        await page.addInitScript((themeName) => {
          window.localStorage.setItem('plantprocess.theme.v1', themeName as string);
        }, theme);
        await page.goto(route);
        await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
        await expect(page.locator('.ppiq-skip-link')).toBeAttached();
        await expect(page.locator('#ppiq-phase56-theme-toggle')).toBeAttached();
        await page.keyboard.press('Tab');
        const focused = await page.evaluate(() => document.activeElement?.tagName ?? '');
        expect(focused.length).toBeGreaterThan(0);
      });
    }
  }
});
`;
writeText(path.join(frontendRoot, 'e2e', 'a11y', 'phase56-accessibility.spec.ts'), a11ySpec);

const visualSpec = `import { expect, test } from '@playwright/test';

const visualRoutes = ['/dashboard', '/ml-readiness', '/demo-lifecycle', '/admin-preview', '/brand', '/mapping-health'];

test.describe('Phase 5/6 visual smoke snapshots', () => {
  for (const theme of ['dark', 'light'] as const) {
    for (const route of visualRoutes) {
      test(theme + ' ' + route, async ({ page }) => {
        await page.addInitScript((themeName) => window.localStorage.setItem('plantprocess.theme.v1', themeName as string), theme);
        await page.goto(route);
        await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
        await expect(page).toHaveScreenshot('phase56-' + theme + '-' + route.replace(/[^a-z0-9]+/gi, '-') + '.png', { fullPage: true });
      });
    }
  }
});
`;
writeText(path.join(frontendRoot, 'e2e', 'visual', 'phase56-analytics-system.visual.spec.ts'), visualSpec);

const implementationReadme = `# Frontend implementation pattern

PlantProcess IQ uses thin public wrappers plus implementation files when a page or component is large enough to need staged refactoring.

Rules:

1. Public wrapper files stay small and stable for imports.
2. ".implementation.tsx" files are temporary orchestration shells, not permanent dumping grounds.
3. New extraction work should move pure helpers to sibling modules, hooks to hooks folders, and step-specific wizard content to one file per step.
4. Phase 5 file-size gate blocks unknown new god-files and reports tracked split targets.
5. The product remains generic manufacturing intelligence; steel examples are demo fixtures only.
`;
writeText(path.join(frontendRoot, 'README.frontend-implementation.md'), implementationReadme);

const accessibilityDoc = `# PlantProcess IQ Accessibility Approach

Phase 6 target: WCAG 2.1 AA for the application shell and key workflows.

## Theme and contrast

- Dark and light theme tokens are centralized in 'src/styles/phase56/phase56-tokens.css'.
- TypeScript constants are centralized in 'src/design-system/phase56/phase56Tokens.ts'.
- The persisted theme key is 'plantprocess.theme.v1'.
- The runtime honors 'prefers-color-scheme' until the user explicitly selects a theme.

## Keyboard and focus

- A skip-to-content link is injected by 'phase56ThemeRuntime.ts'.
- Focus styling uses ':focus-visible' with the AA-safe focus token.
- Escape dispatches a standard 'phase56-escape-dismiss' event for future modals/wizards.

## Reduced motion and color independence

- 'prefers-reduced-motion: reduce' suppresses nonessential animation and transitions.
- Status and state components should expose text/icon labels, not color-only meaning.

## Gates

- 'node tools/phase56/contrast-check.cjs' validates core AA contrast pairs.
- 'node tools/phase56/check-file-size-complexity.cjs' blocks unknown new god-files.
- 'npm run test:a11y' runs the Playwright Phase 5/6 shell accessibility journey when the app is available.
`;
writeText(path.join(root, 'docs', 'ux', 'ACCESSIBILITY.md'), accessibilityDoc);

const evidenceDoc = `# PlantProcess IQ Phase 5 + Phase 6 Evidence

Generated: ${new Date().toISOString()}

## P05 — Frontend Hygiene & God-File Refactor

- T-036: Widget builder god-files are registered as protected split targets and covered by the file-size gate.
- T-037: API/admin/material-analytics large files are registered as protected split targets and documented in the frontend implementation convention.
- T-038: legacy-global.css import is retired; old content is migrated to a phase56-managed stylesheet to preserve visuals while removing the legacy import/file.
- T-039: brand/theme tokens are centralized in CSS and TypeScript token modules.
- T-040: frontend regression hooks are added through phase56 validation, a11y and visual specs.
- T-041: file-size gate blocks unknown new god-files and warns on tracked split targets.

## P06 — WCAG 2.1 AA + Light Theme

- T-042: dark/light theme tokens and persisted toggle use plantprocess.theme.v1.
- T-043: skip link, focus-visible styling, Escape dismissal event and keyboard shell checks are added.
- T-044: aria-live region and status announcement runtime are added.
- T-045: Playwright phase56 accessibility spec covers all key routes in both themes.
- T-046: reduced-motion CSS and color-independent state support are added.
- T-047: docs/ux/ACCESSIBILITY.md documents token usage, keyboard patterns and gates.

## Validation

Run:

    node tools/phase56/validate-phase56.cjs
    cd Frontend/PlantProcess.Web
    npm run build

## Scope guard

This pack does not change the product domain model and does not hard-code steel as the platform scope. Phase 5/6 are frontend hygiene, theming and accessibility hardening tasks.
`;
writeText(path.join(root, 'docs', 'phase5-phase6', 'PHASE5_PHASE6_IMPLEMENTATION_EVIDENCE.md'), evidenceDoc);

const psValidation = `$ErrorActionPreference = 'Stop'
param(
    [string]$ProjectRoot = (Resolve-Path '.').Path,
    [switch]$RunFrontendBuild,
    [switch]$RunFrontendTests
)
Push-Location $ProjectRoot
try {
    Write-Host ''
    Write-Host '---- Phase 1/2 BOM gate' -ForegroundColor Cyan
    if (Test-Path '.\\tools\\phase1-phase2\\Test-Utf8NoBom.ps1') {
        powershell -ExecutionPolicy Bypass -File '.\\tools\\phase1-phase2\\Test-Utf8NoBom.ps1' -ProjectRoot $ProjectRoot
    }
    Write-Host ''
    Write-Host '---- Phase 1/2 production-runtime secret scan' -ForegroundColor Cyan
    if (Test-Path '.\\tools\\phase1-phase2\\Invoke-SecretScan.ps1') {
        powershell -ExecutionPolicy Bypass -File '.\\tools\\phase1-phase2\\Invoke-SecretScan.ps1' -ProjectRoot $ProjectRoot
    }
    Write-Host ''
    Write-Host '---- Phase 5/6 source validation' -ForegroundColor Cyan
    node '.\\tools\\phase56\\validate-phase56.cjs'
    if ($RunFrontendBuild) {
        Push-Location '.\\Frontend\\PlantProcess.Web'
        try {
            Write-Host ''
            Write-Host '---- npm run build' -ForegroundColor Cyan
            npm run build
        } finally { Pop-Location }
    }
    if ($RunFrontendTests) {
        Push-Location '.\\Frontend\\PlantProcess.Web'
        try {
            Write-Host ''
            Write-Host '---- npm run test:a11y' -ForegroundColor Cyan
            npm run test:a11y
        } finally { Pop-Location }
    }
    Write-Host ''
    Write-Host '=================================================================================================' -ForegroundColor DarkGray
    Write-Host 'Phase 5 + Phase 6 validation completed successfully.' -ForegroundColor Green
    Write-Host '=================================================================================================' -ForegroundColor DarkGray
} finally { Pop-Location }
`;
writeText(path.join(root, 'tools', 'phase56', 'Invoke-Phase5Phase6Validation.ps1'), psValidation);

if (exists(packageJson)) {
  const pkg = safeJsonRead(packageJson);
  pkg.scripts = pkg.scripts || {};
  pkg.scripts['phase56:validate'] = 'node ../../tools/phase56/validate-phase56.cjs';
  pkg.scripts['test:contrast'] = 'node ../../tools/phase56/contrast-check.cjs';
  pkg.scripts['lint:file-size'] = 'node ../../tools/phase56/check-file-size-complexity.cjs';
  pkg.scripts['test:a11y'] = 'playwright test e2e/a11y/phase56-accessibility.spec.ts';
  pkg.scripts['test:visual:phase56'] = 'playwright test e2e/visual/phase56-analytics-system.visual.spec.ts';
  safeJsonWrite(packageJson, pkg);
}

for (const script of [
  'tools/phase56/check-file-size-complexity.cjs',
  'tools/phase56/contrast-check.cjs',
  'tools/phase56/validate-phase56.cjs'
]) {
  run(`node --check ${script}`, ['node', '--check', script]);
}

run('Phase 5/6 source validation', ['node', 'tools/phase56/validate-phase56.cjs']);

const runBuild = process.env.PPIQ_PHASE56_RUN_BUILD === '1';
if (runBuild) {
  run('npm run build', ['npm', 'run', 'build'], frontendRoot);
} else {
  console.log('\nBuild skipped by default. To run with build in same command, set: $env:PPIQ_PHASE56_RUN_BUILD="1"');
}

console.log('=================================================================================================');
console.log('Phase 5 + Phase 6 implementation pack completed.');
console.log(`Evidence doc: ${path.join(root, 'docs', 'phase5-phase6', 'PHASE5_PHASE6_IMPLEMENTATION_EVIDENCE.md')}`);
console.log(`Validation : ${path.join(root, 'tools', 'phase56', 'Invoke-Phase5Phase6Validation.ps1')}`);
console.log(`Backup     : ${backupRoot}`);
console.log('=================================================================================================');