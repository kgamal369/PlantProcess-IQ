# ============================================================
# FILE: tools/phase2/Apply-PPIQ-Phase2-UI-Standards.ps1
#
# PlantProcess IQ Phase 2 UI Standards Foundation - Clean V2
#
# Covers:
# PPIQ-T009 Audit all button instances
# PPIQ-T010 Audit all table instances
# PPIQ-T011 Audit tab / segmented / navigation instances
# PPIQ-T012 Audit input / form-field / select instances
# PPIQ-T013 StandardButton
# PPIQ-T014 StandardTable
# PPIQ-T015 StandardTabs
# PPIQ-T016 StandardInput / StandardSelect / StandardTextArea
# PPIQ-T017 StandardCard / StandardModal / StandardToast
# PPIQ-T018 Storybook UI Standards
#
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\phase2\Apply-PPIQ-Phase2-UI-Standards.ps1
# ============================================================

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Get-Location
$frontendRoot = Join-Path $repoRoot "Frontend\PlantProcess.Web"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

if (-not (Test-Path $frontendRoot)) {
    throw "Frontend project not found: $frontendRoot"
}

function Write-FileUtf8 {
    param(
        [string]$RelativePath,
        [string]$Content
    )

    $path = Join-Path $repoRoot $RelativePath
    $folder = Split-Path $path -Parent

    if (-not (Test-Path $folder)) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($path, $Content, $utf8NoBom)
    Write-Host "Wrote $RelativePath"
}

Write-Host ""
Write-Host "=== Applying PPIQ Phase 2 UI Standards Foundation V2 ==="
Write-Host ""

# ============================================================
# T009-T012: Audit script
# ============================================================

Write-FileUtf8 "Frontend\PlantProcess.Web\tools\ui\audit-ui-instances.mjs" @'
import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const src = path.join(root, "src");
const outDir = path.join(root, "reports", "ui-audit");

const groups = [
  {
    task: "PPIQ-T009",
    group: "button",
    canonical: "StandardButton",
    patterns: [
      ["native-button", /<button\b/g],
      ["role-button", /role=["']button["']/g],
      ["button-class", /className=\{?["'`][^"'`]*(btn|button|cta|action)[^"'`]*["'`]\}?/gi],
      ["standard-button", /<StandardButton\b/g],
    ],
  },
  {
    task: "PPIQ-T010",
    group: "table",
    canonical: "StandardTable",
    patterns: [
      ["native-table", /<table\b/g],
      ["table-tags", /<(thead|tbody|tfoot|tr|th|td)\b/g],
      ["grid-role", /role=["'](table|grid|row|columnheader|cell)["']/g],
      ["standard-table", /<StandardTable\b/g],
    ],
  },
  {
    task: "PPIQ-T011",
    group: "navigation",
    canonical: "StandardTabs",
    patterns: [
      ["nav", /<nav\b/g],
      ["router-link", /<(NavLink|Link)\b/g],
      ["tab-role", /role=["'](tab|tablist|tabpanel)["']/g],
      ["tab-class", /className=\{?["'`][^"'`]*(tab|tabs|segment|segmented|nav)[^"'`]*["'`]\}?/gi],
      ["standard-tabs", /<StandardTabs\b/g],
    ],
  },
  {
    task: "PPIQ-T012",
    group: "form",
    canonical: "StandardInput / StandardSelect / StandardTextArea",
    patterns: [
      ["form", /<form\b/g],
      ["input", /<input\b/g],
      ["select", /<select\b/g],
      ["textarea", /<textarea\b/g],
      ["label", /<label\b/g],
      ["standard-field", /<(StandardInput|StandardSelect|StandardTextArea)\b/g],
    ],
  },
];

function walk(dir) {
  const result = [];
  if (!fs.existsSync(dir)) return result;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (full.includes(`${path.sep}node_modules${path.sep}`)) continue;
    if (full.includes(`${path.sep}dist${path.sep}`)) continue;
    if (full.includes(`${path.sep}reports${path.sep}`)) continue;

    if (entry.isDirectory()) {
      result.push(...walk(full));
      continue;
    }

    if (!entry.isFile()) continue;
    if (!/\.(tsx|ts|jsx|js)$/.test(entry.name)) continue;
    if (entry.name.endsWith(".d.ts")) continue;

    result.push(full);
  }

  return result;
}

function rel(file) {
  return path.relative(root, file).replaceAll(path.sep, "/");
}

function lineOf(text, index) {
  return text.slice(0, index).split(/\r?\n/).length;
}

const findings = [];

for (const file of walk(src)) {
  const text = fs.readFileSync(file, "utf8");

  for (const group of groups) {
    for (const [patternName, regex] of group.patterns) {
      regex.lastIndex = 0;
      let match;

      while ((match = regex.exec(text))) {
        findings.push({
          task: group.task,
          group: group.group,
          canonical: group.canonical,
          file: rel(file),
          line: lineOf(text, match.index),
          pattern: patternName,
          evidence: match[0].replace(/\s+/g, " ").slice(0, 160),
        });

        if (match.index === regex.lastIndex) regex.lastIndex++;
      }
    }
  }
}

fs.mkdirSync(outDir, { recursive: true });

const byTask = findings.reduce((acc, item) => {
  acc[item.task] = (acc[item.task] ?? 0) + 1;
  return acc;
}, {});

const byFile = findings.reduce((acc, item) => {
  acc[item.file] ??= { file: item.file, total: 0, button: 0, table: 0, navigation: 0, form: 0 };
  acc[item.file].total++;
  acc[item.file][item.group]++;
  return acc;
}, {});

const summary = {
  generatedAtUtc: new Date().toISOString(),
  totalFindings: findings.length,
  byTask,
  byFile: Object.values(byFile).sort((a, b) => b.total - a.total || a.file.localeCompare(b.file)),
};

const md = [
  "# PlantProcess IQ Phase 2 UI Audit",
  "",
  `Generated UTC: \`${summary.generatedAtUtc}\``,
  "",
  "## Summary",
  "",
  `Total findings: **${summary.totalFindings}**`,
  "",
  "| Task | Area | Count | Canonical target |",
  "|---|---|---:|---|",
  `| PPIQ-T009 | Buttons | ${byTask["PPIQ-T009"] ?? 0} | StandardButton |`,
  `| PPIQ-T010 | Tables | ${byTask["PPIQ-T010"] ?? 0} | StandardTable |`,
  `| PPIQ-T011 | Tabs / navigation | ${byTask["PPIQ-T011"] ?? 0} | StandardTabs |`,
  `| PPIQ-T012 | Forms / fields | ${byTask["PPIQ-T012"] ?? 0} | StandardInput / Select / TextArea |`,
  "",
  "## Highest impact files",
  "",
  "| File | Total | Buttons | Tables | Navigation | Forms |",
  "|---|---:|---:|---:|---:|---:|",
  ...summary.byFile.slice(0, 50).map((f) => `| \`${f.file}\` | ${f.total} | ${f.button} | ${f.table} | ${f.navigation} | ${f.form} |`),
  "",
  "## Detailed findings",
  "",
  "| Task | Group | File | Line | Pattern | Evidence |",
  "|---|---|---|---:|---|---|",
  ...findings.map((f) => `| ${f.task} | ${f.group} | \`${f.file}\` | ${f.line} | ${f.pattern} | \`${f.evidence.replaceAll("|", "\\|")}\` |`),
  "",
  "## Rollout recommendation",
  "",
  "Start replacing high-use shared components first, then dashboard/admin buttons, then tables, then tabs/navigation, then forms.",
  "",
  "## Product guard",
  "",
  "These standards are generic manufacturing UI primitives. Do not hard-code steel-only vocabulary into base UI components.",
].join("\n");

fs.writeFileSync(path.join(outDir, "phase2-ui-audit.json"), JSON.stringify({ summary, findings }, null, 2), "utf8");
fs.writeFileSync(path.join(outDir, "phase2-ui-audit.md"), md, "utf8");

console.log("PPIQ Phase 2 UI audit completed.");
console.log(`Findings: ${findings.length}`);
console.log("Report: reports/ui-audit/phase2-ui-audit.md");
'@

# ============================================================
# Validation script
# ============================================================

Write-FileUtf8 "Frontend\PlantProcess.Web\tools\ui\validate-ui-standards.mjs" @'
import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

const required = [
  "tools/ui/audit-ui-instances.mjs",
  "src/ui/design-tokens.css",
  "src/ui/standard-components.css",
  "src/ui/standard-components.tsx",
  "src/ui/index.ts",
  "src/ui/standards/ui-component-specs.md",
  "src/ui/standard-components.stories.tsx",
  ".storybook/main.ts",
  ".storybook/preview.ts",
  ".storybook/preview.css",
];

const missing = required.filter((file) => !fs.existsSync(path.join(root, file)));

if (missing.length) {
  console.error("Missing Phase 2 UI standards files:");
  for (const item of missing) console.error(`- ${item}`);
  process.exit(1);
}

const pkg = JSON.parse(fs.readFileSync(path.join(root, "package.json"), "utf8"));
const scripts = ["ui:audit", "ui:validate", "storybook", "build:storybook", "validate:phase2:ui-standards"];
const missingScripts = scripts.filter((name) => !pkg.scripts?.[name]);

if (missingScripts.length) {
  console.error("Missing package.json scripts:");
  for (const item of missingScripts) console.error(`- ${item}`);
  process.exit(1);
}

console.log("PPIQ Phase 2 UI standards validation passed.");
'@

# ============================================================
# Design tokens
# ============================================================

Write-FileUtf8 "Frontend\PlantProcess.Web\src\ui\design-tokens.css" @'
:root {
  color-scheme: dark;

  --ppiq-font-sans: Inter, ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif;

  --ppiq-bg-0: #050b18;
  --ppiq-bg-1: #081426;
  --ppiq-bg-2: #0b1730;

  --ppiq-surface-0: rgba(8, 20, 38, 0.86);
  --ppiq-surface-1: rgba(11, 23, 48, 0.84);
  --ppiq-surface-2: rgba(16, 33, 61, 0.78);

  --ppiq-border-subtle: rgba(0, 212, 255, 0.1);
  --ppiq-border: rgba(0, 212, 255, 0.18);
  --ppiq-border-strong: rgba(0, 212, 255, 0.34);

  --ppiq-text: #eaf6ff;
  --ppiq-text-muted: #a0bdd8;
  --ppiq-text-soft: #6d8eae;
  --ppiq-text-disabled: #48647f;

  --ppiq-brand: #00d4ff;
  --ppiq-brand-strong: #36e5ff;
  --ppiq-brand-soft: rgba(0, 212, 255, 0.12);

  --ppiq-success: #30d158;
  --ppiq-warning: #ffd166;
  --ppiq-danger: #ff4d6d;
  --ppiq-info: #5ac8fa;

  --ppiq-radius-sm: 8px;
  --ppiq-radius-md: 12px;
  --ppiq-radius-lg: 16px;
  --ppiq-radius-xl: 22px;

  --ppiq-shadow-sm: 0 8px 20px rgba(0, 0, 0, 0.22);
  --ppiq-shadow-md: 0 18px 46px rgba(0, 0, 0, 0.34);
  --ppiq-shadow-glow: 0 0 24px rgba(0, 212, 255, 0.18);

  --ppiq-focus: 0 0 0 3px rgba(0, 212, 255, 0.24);
  --ppiq-transition-fast: 120ms ease;
  --ppiq-transition: 180ms ease;
}
'@

# ============================================================
# Component CSS
# ============================================================

Write-FileUtf8 "Frontend\PlantProcess.Web\src\ui\standard-components.css" @'
.ppiq-btn {
  appearance: none;
  border: 1px solid transparent;
  border-radius: var(--ppiq-radius-sm);
  font-family: var(--ppiq-font-sans);
  font-weight: 800;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.45rem;
  cursor: pointer;
  user-select: none;
  white-space: nowrap;
  transition: transform var(--ppiq-transition-fast), border-color var(--ppiq-transition-fast), background var(--ppiq-transition-fast), box-shadow var(--ppiq-transition-fast);
}

.ppiq-btn:hover:not(:disabled) {
  transform: translateY(-1px);
}

.ppiq-btn:focus-visible {
  outline: none;
  box-shadow: var(--ppiq-focus);
}

.ppiq-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

.ppiq-btn--xs { min-height: 28px; padding: 0 0.55rem; font-size: 11px; }
.ppiq-btn--sm { min-height: 34px; padding: 0 0.75rem; font-size: 12px; }
.ppiq-btn--md { min-height: 40px; padding: 0 1rem; font-size: 13px; }
.ppiq-btn--lg { min-height: 46px; padding: 0 1.25rem; font-size: 14px; }
.ppiq-btn--full { width: 100%; }

.ppiq-btn--primary {
  background: linear-gradient(135deg, rgba(0, 212, 255, 0.96), rgba(10, 132, 255, 0.92));
  color: #03111f;
  border-color: rgba(0, 212, 255, 0.42);
  box-shadow: 0 0 22px rgba(0, 212, 255, 0.2);
}

.ppiq-btn--secondary {
  background: rgba(0, 212, 255, 0.08);
  color: var(--ppiq-text);
  border-color: var(--ppiq-border);
}

.ppiq-btn--ghost {
  background: transparent;
  color: var(--ppiq-text-muted);
  border-color: transparent;
}

.ppiq-btn--danger {
  background: rgba(255, 77, 109, 0.14);
  color: #ffd8df;
  border-color: rgba(255, 77, 109, 0.32);
}

.ppiq-btn--success {
  background: rgba(48, 209, 88, 0.14);
  color: #caffd5;
  border-color: rgba(48, 209, 88, 0.28);
}

.ppiq-spinner {
  width: 0.95em;
  height: 0.95em;
  border-radius: 999px;
  border: 2px solid currentColor;
  border-top-color: transparent;
  animation: ppiq-spin 720ms linear infinite;
}

@keyframes ppiq-spin {
  to { transform: rotate(360deg); }
}

.ppiq-card {
  border: 1px solid var(--ppiq-border-subtle);
  border-radius: var(--ppiq-radius-lg);
  background: radial-gradient(circle at top left, rgba(0, 212, 255, 0.08), transparent 44%), linear-gradient(180deg, var(--ppiq-surface-1), rgba(5, 11, 24, 0.78));
  box-shadow: var(--ppiq-shadow-sm);
  overflow: hidden;
}

.ppiq-card__header {
  padding: 1.25rem 1.25rem 0.75rem;
  border-bottom: 1px solid rgba(0, 212, 255, 0.07);
}

.ppiq-card__eyebrow {
  margin: 0 0 0.25rem;
  font-size: 10px;
  font-weight: 900;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: var(--ppiq-brand);
}

.ppiq-card__title {
  margin: 0;
  font-size: 16px;
  color: var(--ppiq-text);
}

.ppiq-card__subtitle {
  margin: 0.4rem 0 0;
  color: var(--ppiq-text-soft);
  font-size: 12px;
}

.ppiq-card__body {
  padding: 1.25rem;
}

.ppiq-card__footer {
  padding: 1rem 1.25rem;
  border-top: 1px solid rgba(0, 212, 255, 0.07);
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}

.ppiq-table-wrap {
  border: 1px solid var(--ppiq-border-subtle);
  border-radius: var(--ppiq-radius-lg);
  background: rgba(8, 20, 38, 0.66);
  overflow: hidden;
}

.ppiq-table-scroll { overflow: auto; }

.ppiq-table {
  width: 100%;
  border-collapse: collapse;
  min-width: 720px;
  color: var(--ppiq-text);
}

.ppiq-table th {
  text-align: left;
  font-size: 10px;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: var(--ppiq-text-soft);
  background: rgba(0, 212, 255, 0.06);
  border-bottom: 1px solid var(--ppiq-border-subtle);
  padding: 0.72rem 0.85rem;
}

.ppiq-table td {
  padding: 0.78rem 0.85rem;
  border-bottom: 1px solid rgba(0, 212, 255, 0.06);
  font-size: 13px;
  color: var(--ppiq-text-muted);
}

.ppiq-table tbody tr:hover { background: rgba(0, 212, 255, 0.045); }
.ppiq-align-right { text-align: right !important; }
.ppiq-align-center { text-align: center !important; }
.ppiq-row-danger td { background: rgba(255, 77, 109, 0.06); }
.ppiq-row-warning td { background: rgba(255, 209, 102, 0.055); }
.ppiq-row-success td { background: rgba(48, 209, 88, 0.045); }

.ppiq-state {
  padding: 2rem;
  text-align: center;
  color: var(--ppiq-text-soft);
  font-size: 13px;
}

.ppiq-state strong {
  display: block;
  color: var(--ppiq-text);
  font-size: 14px;
  margin-bottom: 0.35rem;
}

.ppiq-tabs {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.ppiq-tabs__list {
  display: inline-flex;
  gap: 0.35rem;
  padding: 0.28rem;
  border: 1px solid var(--ppiq-border-subtle);
  border-radius: var(--ppiq-radius-md);
  background: rgba(0, 212, 255, 0.045);
  width: fit-content;
  max-width: 100%;
  overflow-x: auto;
}

.ppiq-tabs__button {
  border: 1px solid transparent;
  border-radius: var(--ppiq-radius-sm);
  background: transparent;
  color: var(--ppiq-text-soft);
  min-height: 34px;
  padding: 0 0.8rem;
  font-size: 12px;
  font-weight: 800;
  cursor: pointer;
  white-space: nowrap;
}

.ppiq-tabs__button[aria-selected="true"] {
  color: var(--ppiq-text);
  background: rgba(0, 212, 255, 0.13);
  border-color: rgba(0, 212, 255, 0.24);
}

.ppiq-field {
  display: flex;
  flex-direction: column;
  gap: 0.42rem;
}

.ppiq-field__label {
  color: var(--ppiq-text-muted);
  font-size: 12px;
  font-weight: 800;
}

.ppiq-field__required {
  color: var(--ppiq-warning);
  margin-left: 0.15rem;
}

.ppiq-field__control {
  width: 100%;
  min-height: 40px;
  box-sizing: border-box;
  border: 1px solid var(--ppiq-border-subtle);
  border-radius: var(--ppiq-radius-sm);
  background: rgba(5, 11, 24, 0.76);
  color: var(--ppiq-text);
  padding: 0 0.8rem;
  font: 500 13px/1.4 var(--ppiq-font-sans);
}

.ppiq-field__control:focus {
  outline: none;
  border-color: var(--ppiq-border-strong);
  box-shadow: var(--ppiq-focus);
}

.ppiq-field__textarea {
  min-height: 96px;
  resize: vertical;
  padding-top: 0.7rem;
}

.ppiq-field__hint {
  color: var(--ppiq-text-soft);
  font-size: 11px;
}

.ppiq-field__error {
  color: #ffb3c0;
  font-size: 11px;
}

.ppiq-field--error .ppiq-field__control {
  border-color: rgba(255, 77, 109, 0.52);
}

.ppiq-modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 999;
  background: rgba(3, 8, 18, 0.74);
  backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1.25rem;
}

.ppiq-modal {
  width: min(720px, 100%);
  border: 1px solid var(--ppiq-border);
  border-radius: var(--ppiq-radius-xl);
  background: linear-gradient(180deg, #0b1730, #050b18);
  box-shadow: var(--ppiq-shadow-md), var(--ppiq-shadow-glow);
  color: var(--ppiq-text);
}

.ppiq-modal__header,
.ppiq-modal__body,
.ppiq-modal__footer {
  padding: 1.25rem;
}

.ppiq-modal__header,
.ppiq-modal__footer {
  border-color: var(--ppiq-border-subtle);
}

.ppiq-modal__header {
  border-bottom: 1px solid var(--ppiq-border-subtle);
  display: flex;
  justify-content: space-between;
  gap: 1rem;
}

.ppiq-modal__footer {
  border-top: 1px solid var(--ppiq-border-subtle);
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}

.ppiq-toast-viewport {
  position: fixed;
  right: 1.25rem;
  bottom: 1.25rem;
  z-index: 1200;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  width: min(420px, calc(100vw - 2rem));
}

.ppiq-toast {
  border: 1px solid var(--ppiq-border-subtle);
  border-radius: var(--ppiq-radius-md);
  background: rgba(8, 20, 38, 0.96);
  box-shadow: var(--ppiq-shadow-md);
  padding: 1rem;
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 0.75rem;
}

.ppiq-toast__title {
  margin: 0;
  color: var(--ppiq-text);
  font-size: 13px;
  font-weight: 900;
}

.ppiq-toast__description {
  margin: 0.35rem 0 0;
  color: var(--ppiq-text-soft);
  font-size: 12px;
}

.ppiq-standards-page {
  min-height: 100vh;
  background: radial-gradient(circle at 14% 0%, rgba(0, 212, 255, 0.1), transparent 34%), linear-gradient(180deg, var(--ppiq-bg-0), var(--ppiq-bg-1));
  padding: 2rem;
  color: var(--ppiq-text);
  font-family: var(--ppiq-font-sans);
}

.ppiq-standards-grid {
  display: grid;
  gap: 1.25rem;
}

.ppiq-standards-grid--two {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.ppiq-token-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(148px, 1fr));
  gap: 0.75rem;
}

.ppiq-token {
  border: 1px solid var(--ppiq-border-subtle);
  border-radius: var(--ppiq-radius-md);
  overflow: hidden;
  background: rgba(8, 20, 38, 0.6);
}

.ppiq-token__swatch { height: 58px; }
.ppiq-token__meta { padding: 0.65rem; }
.ppiq-token__name { font-size: 11px; font-weight: 900; }
.ppiq-token__value { margin-top: 0.2rem; font-size: 10px; color: var(--ppiq-text-soft); }

@media (max-width: 860px) {
  .ppiq-standards-page { padding: 1rem; }
  .ppiq-standards-grid--two { grid-template-columns: 1fr; }
}
'@

# ============================================================
# Component TSX
# ============================================================

Write-FileUtf8 "Frontend\PlantProcess.Web\src\ui\standard-components.tsx" @'
import {
  createContext,
  forwardRef,
  useCallback,
  useContext,
  useEffect,
  useId,
  useMemo,
  useState,
  type ButtonHTMLAttributes,
  type HTMLAttributes,
  type InputHTMLAttributes,
  type ReactNode,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from "react";
import { createPortal } from "react-dom";
import "./design-tokens.css";
import "./standard-components.css";

export type StandardSize = "xs" | "sm" | "md" | "lg";
export type StandardButtonVariant = "primary" | "secondary" | "ghost" | "danger" | "success";

export function cx(...classes: Array<string | false | null | undefined>) {
  return classes.filter(Boolean).join(" ");
}

export type StandardButtonProps = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "type"> & {
  variant?: StandardButtonVariant;
  size?: StandardSize;
  fullWidth?: boolean;
  loading?: boolean;
  leftIcon?: ReactNode;
  rightIcon?: ReactNode;
  type?: "button" | "submit" | "reset";
};

export const StandardButton = forwardRef<HTMLButtonElement, StandardButtonProps>(
  (
    {
      variant = "primary",
      size = "md",
      fullWidth = false,
      loading = false,
      leftIcon,
      rightIcon,
      children,
      className,
      disabled,
      type = "button",
      ...rest
    },
    ref,
  ) => {
    const isDisabled = disabled || loading;

    return (
      <button
        ref={ref}
        type={type}
        className={cx(
          "ppiq-btn",
          `ppiq-btn--${variant}`,
          `ppiq-btn--${size}`,
          fullWidth && "ppiq-btn--full",
          className,
        )}
        disabled={isDisabled}
        aria-busy={loading || undefined}
        aria-disabled={isDisabled || undefined}
        {...rest}
      >
        {loading ? <span className="ppiq-spinner" aria-hidden="true" /> : null}
        {!loading && leftIcon ? <span>{leftIcon}</span> : null}
        <span>{children}</span>
        {!loading && rightIcon ? <span>{rightIcon}</span> : null}
      </button>
    );
  },
);

StandardButton.displayName = "StandardButton";

export type StandardCardProps = HTMLAttributes<HTMLElement> & {
  eyebrow?: ReactNode;
  title?: ReactNode;
  subtitle?: ReactNode;
  footer?: ReactNode;
  as?: "section" | "article" | "div";
};

export function StandardCard({
  eyebrow,
  title,
  subtitle,
  footer,
  children,
  className,
  as = "section",
  ...rest
}: StandardCardProps) {
  const Component = as;

  return (
    <Component className={cx("ppiq-card", className)} {...rest}>
      {eyebrow || title || subtitle ? (
        <header className="ppiq-card__header">
          {eyebrow ? <p className="ppiq-card__eyebrow">{eyebrow}</p> : null}
          {title ? <h3 className="ppiq-card__title">{title}</h3> : null}
          {subtitle ? <p className="ppiq-card__subtitle">{subtitle}</p> : null}
        </header>
      ) : null}
      <div className="ppiq-card__body">{children}</div>
      {footer ? <footer className="ppiq-card__footer">{footer}</footer> : null}
    </Component>
  );
}

export type StandardTableAlignment = "left" | "center" | "right";
export type StandardTableTone = "neutral" | "success" | "warning" | "danger";

export type StandardTableColumn<T> = {
  key: string;
  header: ReactNode;
  cell: (row: T, rowIndex: number) => ReactNode;
  align?: StandardTableAlignment;
  width?: string;
};

export type StandardTableProps<T> = {
  caption?: string;
  columns: ReadonlyArray<StandardTableColumn<T>>;
  data: ReadonlyArray<T>;
  getRowKey: (row: T, rowIndex: number) => string | number;
  loading?: boolean;
  error?: ReactNode;
  emptyTitle?: ReactNode;
  emptyDescription?: ReactNode;
  onRowClick?: (row: T, rowIndex: number) => void;
  getRowTone?: (row: T, rowIndex: number) => StandardTableTone;
  className?: string;
};

export function StandardTable<T>({
  caption,
  columns,
  data,
  getRowKey,
  loading = false,
  error,
  emptyTitle = "No records available",
  emptyDescription = "Adjust filters or refresh the data source.",
  onRowClick,
  getRowTone,
  className,
}: StandardTableProps<T>) {
  if (error) {
    return (
      <div className={cx("ppiq-table-wrap", className)}>
        <div className="ppiq-state">
          <strong>Table refresh failed</strong>
          <span>{error}</span>
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div className={cx("ppiq-table-wrap", className)}>
        <div className="ppiq-state">
          <strong>Refreshing table</strong>
          <span>Loading the latest manufacturing intelligence data.</span>
        </div>
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <div className={cx("ppiq-table-wrap", className)}>
        <div className="ppiq-state">
          <strong>{emptyTitle}</strong>
          <span>{emptyDescription}</span>
        </div>
      </div>
    );
  }

  return (
    <div className={cx("ppiq-table-wrap", className)}>
      <div className="ppiq-table-scroll">
        <table className="ppiq-table">
          {caption ? <caption>{caption}</caption> : null}
          <thead>
            <tr>
              {columns.map((column) => (
                <th
                  key={column.key}
                  className={cx(
                    column.align === "right" && "ppiq-align-right",
                    column.align === "center" && "ppiq-align-center",
                  )}
                  style={column.width ? { width: column.width } : undefined}
                >
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.map((row, rowIndex) => {
              const tone = getRowTone?.(row, rowIndex) ?? "neutral";

              return (
                <tr
                  key={getRowKey(row, rowIndex)}
                  className={cx(
                    onRowClick && "ppiq-clickable-row",
                    tone !== "neutral" && `ppiq-row-${tone}`,
                  )}
                  onClick={onRowClick ? () => onRowClick(row, rowIndex) : undefined}
                >
                  {columns.map((column) => (
                    <td
                      key={column.key}
                      className={cx(
                        column.align === "right" && "ppiq-align-right",
                        column.align === "center" && "ppiq-align-center",
                      )}
                    >
                      {column.cell(row, rowIndex)}
                    </td>
                  ))}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export type StandardTabItem<TId extends string = string> = {
  id: TId;
  label: ReactNode;
  badge?: ReactNode;
  disabled?: boolean;
  panel?: ReactNode;
};

export type StandardTabsProps<TId extends string = string> = {
  items: ReadonlyArray<StandardTabItem<TId>>;
  activeId: TId;
  onChange: (id: TId) => void;
  ariaLabel: string;
  children?: ReactNode;
  className?: string;
};

export function StandardTabs<TId extends string = string>({
  items,
  activeId,
  onChange,
  ariaLabel,
  children,
  className,
}: StandardTabsProps<TId>) {
  const active = items.find((item) => item.id === activeId);

  return (
    <div className={cx("ppiq-tabs", className)}>
      <div className="ppiq-tabs__list" role="tablist" aria-label={ariaLabel}>
        {items.map((item) => (
          <button
            key={item.id}
            type="button"
            role="tab"
            className="ppiq-tabs__button"
            aria-selected={item.id === activeId}
            disabled={item.disabled}
            onClick={() => onChange(item.id)}
          >
            <span>{item.label}</span>
            {item.badge ? <span>{item.badge}</span> : null}
          </button>
        ))}
      </div>
      <div role="tabpanel">{children ?? active?.panel}</div>
    </div>
  );
}

type FieldShellProps = {
  id?: string;
  label?: ReactNode;
  required?: boolean;
  helperText?: ReactNode;
  error?: ReactNode;
  className?: string;
  children: (id: string, describedBy?: string) => ReactNode;
};

function FieldShell({ id, label, required, helperText, error, className, children }: FieldShellProps) {
  const generatedId = useId();
  const fieldId = id ?? generatedId;
  const hintId = helperText ? `${fieldId}-hint` : undefined;
  const errorId = error ? `${fieldId}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(" ") || undefined;

  return (
    <div className={cx("ppiq-field", error && "ppiq-field--error", className)}>
      {label ? (
        <label className="ppiq-field__label" htmlFor={fieldId}>
          {label}
          {required ? <span className="ppiq-field__required">*</span> : null}
        </label>
      ) : null}
      {children(fieldId, describedBy)}
      {helperText ? (
        <div className="ppiq-field__hint" id={hintId}>
          {helperText}
        </div>
      ) : null}
      {error ? (
        <div className="ppiq-field__error" id={errorId} role="alert">
          {error}
        </div>
      ) : null}
    </div>
  );
}

export type StandardInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, "size"> & {
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
};

export const StandardInput = forwardRef<HTMLInputElement, StandardInputProps>(
  ({ label, helperText, error, className, required, id, ...rest }, ref) => (
    <FieldShell id={id} label={label} required={required} helperText={helperText} error={error} className={className}>
      {(fieldId, describedBy) => (
        <input
          ref={ref}
          id={fieldId}
          className="ppiq-field__control"
          required={required}
          aria-invalid={Boolean(error) || undefined}
          aria-describedby={describedBy}
          {...rest}
        />
      )}
    </FieldShell>
  ),
);

StandardInput.displayName = "StandardInput";

export type StandardSelectOption = {
  value: string;
  label: ReactNode;
  disabled?: boolean;
};

export type StandardSelectProps = Omit<SelectHTMLAttributes<HTMLSelectElement>, "size"> & {
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
  placeholder?: string;
  options: ReadonlyArray<StandardSelectOption>;
};

export const StandardSelect = forwardRef<HTMLSelectElement, StandardSelectProps>(
  ({ label, helperText, error, placeholder, options, className, required, id, ...rest }, ref) => (
    <FieldShell id={id} label={label} required={required} helperText={helperText} error={error} className={className}>
      {(fieldId, describedBy) => (
        <select
          ref={ref}
          id={fieldId}
          className="ppiq-field__control"
          required={required}
          aria-invalid={Boolean(error) || undefined}
          aria-describedby={describedBy}
          {...rest}
        >
          {placeholder ? <option value="">{placeholder}</option> : null}
          {options.map((option) => (
            <option key={option.value} value={option.value} disabled={option.disabled}>
              {option.label}
            </option>
          ))}
        </select>
      )}
    </FieldShell>
  ),
);

StandardSelect.displayName = "StandardSelect";

export type StandardTextAreaProps = TextareaHTMLAttributes<HTMLTextAreaElement> & {
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
};

export const StandardTextArea = forwardRef<HTMLTextAreaElement, StandardTextAreaProps>(
  ({ label, helperText, error, className, required, id, ...rest }, ref) => (
    <FieldShell id={id} label={label} required={required} helperText={helperText} error={error} className={className}>
      {(fieldId, describedBy) => (
        <textarea
          ref={ref}
          id={fieldId}
          className="ppiq-field__control ppiq-field__textarea"
          required={required}
          aria-invalid={Boolean(error) || undefined}
          aria-describedby={describedBy}
          {...rest}
        />
      )}
    </FieldShell>
  ),
);

StandardTextArea.displayName = "StandardTextArea";

export type StandardModalProps = {
  open: boolean;
  title: ReactNode;
  description?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  onClose: () => void;
};

export function StandardModal({ open, title, description, children, footer, onClose }: StandardModalProps) {
  useEffect(() => {
    if (!open) return;

    const handler = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };

    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [open, onClose]);

  if (!open || typeof document === "undefined") return null;

  return createPortal(
    <div className="ppiq-modal-backdrop" role="presentation" onMouseDown={onClose}>
      <section className="ppiq-modal" role="dialog" aria-modal="true" onMouseDown={(event) => event.stopPropagation()}>
        <header className="ppiq-modal__header">
          <div>
            <h2>{title}</h2>
            {description ? <p>{description}</p> : null}
          </div>
          <StandardButton variant="ghost" size="sm" onClick={onClose} aria-label="Close modal">
            ×
          </StandardButton>
        </header>
        <div className="ppiq-modal__body">{children}</div>
        {footer ? <footer className="ppiq-modal__footer">{footer}</footer> : null}
      </section>
    </div>,
    document.body,
  );
}

export type StandardToastMessage = {
  id: string;
  intent: "success" | "warning" | "danger" | "info";
  title: ReactNode;
  description?: ReactNode;
};

type ToastContextValue = {
  notify: (message: Omit<StandardToastMessage, "id"> & { id?: string }) => string;
  dismiss: (id: string) => void;
};

const ToastContext = createContext<ToastContextValue | null>(null);

export function StandardToastProvider({ children }: { children: ReactNode }) {
  const [messages, setMessages] = useState<StandardToastMessage[]>([]);

  const dismiss = useCallback((id: string) => {
    setMessages((current) => current.filter((message) => message.id !== id));
  }, []);

  const notify = useCallback(
    (message: Omit<StandardToastMessage, "id"> & { id?: string }) => {
      const id = message.id ?? `toast-${Date.now()}-${Math.random().toString(36).slice(2)}`;
      setMessages((current) => [{ ...message, id }, ...current].slice(0, 5));
      window.setTimeout(() => dismiss(id), 6000);
      return id;
    },
    [dismiss],
  );

  const value = useMemo(() => ({ notify, dismiss }), [notify, dismiss]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="ppiq-toast-viewport" role="region" aria-label="Notifications">
        {messages.map((message) => (
          <article key={message.id} className="ppiq-toast" role="status">
            <div>
              <p className="ppiq-toast__title">{message.title}</p>
              {message.description ? <p className="ppiq-toast__description">{message.description}</p> : null}
            </div>
            <StandardButton variant="ghost" size="xs" onClick={() => dismiss(message.id)} aria-label="Dismiss notification">
              ×
            </StandardButton>
          </article>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useStandardToast() {
  const context = useContext(ToastContext);
  if (!context) throw new Error("useStandardToast must be used inside StandardToastProvider.");
  return context;
}
'@

Write-FileUtf8 "Frontend\PlantProcess.Web\src\ui\index.ts" @'
export * from "./standard-components";
'@

# ============================================================
# Specs
# ============================================================

Write-FileUtf8 "Frontend\PlantProcess.Web\src\ui\standards\ui-component-specs.md" @'
# PlantProcess IQ UI Component Standards

## Scope

This standard defines canonical UI primitives for PlantProcess IQ.

It supports:
- Generic manufacturing quality intelligence.
- Investigation-first workflows.
- Plant data quality and risk analysis.
- Future BI, Qlik, Power BI, Python ML, correlation, and explainability integration.
- Demo and commercial packaging without hard-coding steel-only concepts.

Steel terminology must remain page content, configuration, metadata, or demo data. It must not be baked into base UI components.

## PPIQ-T013 - StandardButton

Use StandardButton for all direct user actions.

Required behavior:
- Uses type button by default.
- Supports loading, disabled, aria-busy, and aria-disabled.
- Supports variants: primary, secondary, ghost, danger, success.
- Supports sizes: xs, sm, md, lg.
- Supports left and right icons.
- Must not replace semantic links for route navigation.

## PPIQ-T014 - StandardTable

Use StandardTable for structured analytical data.

Required behavior:
- Centralized empty, loading, and error states.
- Explicit column definitions.
- Explicit getRowKey.
- Optional row tone: neutral, success, warning, danger.
- Optional row click behavior.
- Does not own filtering, paging, or API calls.

## PPIQ-T015 - StandardTabs

Use StandardTabs for tab-like sections and segmented views.

Required behavior:
- Uses tablist, tab, and tabpanel roles.
- Requires ariaLabel.
- Supports disabled tabs.
- Supports badges and icons.
- Does not own route navigation unless wrapped by a page-level route strategy.

## PPIQ-T016 - StandardInput, StandardSelect, StandardTextArea

Use standard fields for every form field.

Required behavior:
- Label support.
- Required marker support.
- Helper text support.
- Error text support.
- aria-invalid and aria-describedby support.
- No hidden validation messages.

## PPIQ-T017 - StandardCard, StandardModal, StandardToast

StandardCard is used for grouped information, metrics, settings, and analytical panels.

StandardModal is used for blocking confirmation or focused configuration.

StandardToast is used for lightweight status notifications.

## PPIQ-T018 - Storybook UI Standards

Storybook must include:
- Brand token examples.
- Button examples.
- Table examples.
- Tabs examples.
- Form field examples.
- Card, modal, and toast examples.
- Do and do-not examples.

Storybook is a standards reference, not a production route.
'@

# ============================================================
# Storybook
# ============================================================

Write-FileUtf8 "Frontend\PlantProcess.Web\.storybook\main.ts" @'
import type { StorybookConfig } from "@storybook/react-vite";

const config: StorybookConfig = {
  stories: ["../src/**/*.stories.@(ts|tsx|js|jsx|mjs)", "../src/**/*.mdx"],
  addons: ["@storybook/addon-docs", "@storybook/addon-a11y"],
  framework: {
    name: "@storybook/react-vite",
    options: {},
  },
  docs: {
    autodocs: "tag",
  },
};

export default config;
'@

Write-FileUtf8 "Frontend\PlantProcess.Web\.storybook\preview.ts" @'
import type { Preview } from "@storybook/react-vite";
import "../src/index.css";
import "../src/ui/design-tokens.css";
import "../src/ui/standard-components.css";
import "./preview.css";

const preview: Preview = {
  parameters: {
    layout: "fullscreen",
    backgrounds: {
      default: "PlantProcess IQ Dark",
      values: [
        {
          name: "PlantProcess IQ Dark",
          value: "#050b18",
        },
      ],
    },
  },
};

export default preview;
'@

Write-FileUtf8 "Frontend\PlantProcess.Web\.storybook\preview.css" @'
html,
body,
#storybook-root {
  min-height: 100%;
  background: #050b18;
}

body {
  margin: 0;
}
'@

Write-FileUtf8 "Frontend\PlantProcess.Web\src\ui\standard-components.stories.tsx" @'
import { useState } from "react";
import type { Meta, StoryObj } from "@storybook/react-vite";
import {
  StandardButton,
  StandardCard,
  StandardInput,
  StandardModal,
  StandardSelect,
  StandardTable,
  StandardTabs,
  StandardTextArea,
  StandardToastProvider,
  useStandardToast,
  type StandardTableColumn,
} from "./standard-components";

type DemoSignal = {
  id: string;
  asset: string;
  area: string;
  riskScore: number;
  status: "Stable" | "Watch" | "Critical";
};

const rows: DemoSignal[] = [
  { id: "SIG-1001", asset: "Production line A", area: "Thermal process", riskScore: 18, status: "Stable" },
  { id: "SIG-1002", asset: "Production line B", area: "Mechanical process", riskScore: 56, status: "Watch" },
  { id: "SIG-1003", asset: "Production line C", area: "Quality inspection", riskScore: 84, status: "Critical" },
];

const columns: StandardTableColumn<DemoSignal>[] = [
  { key: "asset", header: "Asset", cell: (row) => row.asset },
  { key: "area", header: "Process area", cell: (row) => row.area },
  { key: "risk", header: "Risk score", align: "right", cell: (row) => row.riskScore },
  { key: "status", header: "Status", cell: (row) => row.status },
];

const meta: Meta = {
  title: "PlantProcess IQ/UI Standards",
  parameters: {
    layout: "fullscreen",
  },
};

export default meta;

type Story = StoryObj;

function ToastButton() {
  const toast = useStandardToast();

  return (
    <StandardButton
      onClick={() =>
        toast.notify({
          intent: "success",
          title: "Configuration saved",
          description: "The manufacturing intelligence settings were updated.",
        })
      }
    >
      Show standard toast
    </StandardButton>
  );
}

function Token({ name, value }: { name: string; value: string }) {
  return (
    <div className="ppiq-token">
      <div className="ppiq-token__swatch" style={{ background: value }} />
      <div className="ppiq-token__meta">
        <div className="ppiq-token__name">{name}</div>
        <div className="ppiq-token__value">{value}</div>
      </div>
    </div>
  );
}

function StandardsPage() {
  const [tab, setTab] = useState("components");
  const [open, setOpen] = useState(false);

  return (
    <StandardToastProvider>
      <main className="ppiq-standards-page">
        <div className="ppiq-standards-grid">
          <StandardCard
            eyebrow="PPIQ-T018"
            title="PlantProcess IQ UI Standards"
            subtitle="Canonical dark industrial command-center UI primitives for generic manufacturing intelligence."
          >
            <StandardTabs
              ariaLabel="UI standards"
              activeId={tab}
              onChange={setTab}
              items={[
                { id: "components", label: "Components" },
                { id: "tokens", label: "Brand tokens" },
              ]}
            />
          </StandardCard>

          {tab === "tokens" ? (
            <StandardCard title="Brand tokens" subtitle="Use tokens instead of one-off colors.">
              <div className="ppiq-token-grid">
                <Token name="Brand" value="#00d4ff" />
                <Token name="Background" value="#050b18" />
                <Token name="Surface" value="#0b1730" />
                <Token name="Success" value="#30d158" />
                <Token name="Warning" value="#ffd166" />
                <Token name="Danger" value="#ff4d6d" />
              </div>
            </StandardCard>
          ) : null}

          {tab === "components" ? (
            <>
              <div className="ppiq-standards-grid ppiq-standards-grid--two">
                <StandardCard title="StandardButton" subtitle="One component for direct actions.">
                  <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
                    <StandardButton>Primary</StandardButton>
                    <StandardButton variant="secondary">Secondary</StandardButton>
                    <StandardButton variant="ghost">Ghost</StandardButton>
                    <StandardButton variant="danger">Danger</StandardButton>
                    <StandardButton loading>Refreshing</StandardButton>
                  </div>
                </StandardCard>

                <StandardCard title="Standard fields" subtitle="Labels, helper text, errors, and accessibility.">
                  <div style={{ display: "grid", gap: 14 }}>
                    <StandardInput label="Connector name" placeholder="Production data source" required />
                    <StandardSelect
                      label="Process domain"
                      placeholder="Select domain"
                      options={[
                        { value: "thermal", label: "Thermal process" },
                        { value: "mechanical", label: "Mechanical process" },
                        { value: "inspection", label: "Inspection / quality" },
                      ]}
                    />
                    <StandardTextArea label="Investigation note" helperText="Keep notes factual and investigation-first." />
                  </div>
                </StandardCard>
              </div>

              <StandardCard title="StandardTable" subtitle="Centralized analytical table behavior.">
                <StandardTable
                  caption="Manufacturing intelligence risk signals"
                  columns={columns}
                  data={rows}
                  getRowKey={(row) => row.id}
                  getRowTone={(row) =>
                    row.status === "Critical" ? "danger" : row.status === "Watch" ? "warning" : "success"
                  }
                />
              </StandardCard>

              <StandardCard title="StandardModal and StandardToast">
                <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
                  <StandardButton onClick={() => setOpen(true)}>Open modal</StandardButton>
                  <ToastButton />
                </div>

                <StandardModal
                  open={open}
                  title="Confirm investigation action"
                  description="Use modals only for focused decisions."
                  onClose={() => setOpen(false)}
                  footer={
                    <>
                      <StandardButton variant="ghost" onClick={() => setOpen(false)}>
                        Cancel
                      </StandardButton>
                      <StandardButton onClick={() => setOpen(false)}>Confirm</StandardButton>
                    </>
                  }
                >
                  This modal follows the canonical PlantProcess IQ visual and accessibility pattern.
                </StandardModal>
              </StandardCard>
            </>
          ) : null}
        </div>
      </main>
    </StandardToastProvider>
  );
}

export const UIStandards: Story = {
  render: () => <StandardsPage />,
};
'@

# ============================================================
# package.json scripts + tsconfig excludes
# ============================================================

$packagePath = Join-Path $frontendRoot "package.json"
$packageUpdateJs = @'
const fs = require("fs");
const path = require("path");

const packagePath = path.join(process.cwd(), "package.json");
const pkg = JSON.parse(fs.readFileSync(packagePath, "utf8"));

pkg.scripts ??= {};
pkg.scripts["ui:audit"] = "node tools/ui/audit-ui-instances.mjs";
pkg.scripts["ui:validate"] = "node tools/ui/validate-ui-standards.mjs";
pkg.scripts["storybook"] = "storybook dev -p 6006";
pkg.scripts["build:storybook"] = "storybook build";
pkg.scripts["validate:phase2:ui-standards"] = "npm run ui:audit && npm run ui:validate && npm run build";

fs.writeFileSync(packagePath, JSON.stringify(pkg, null, 2) + "\n");
'@

Push-Location $frontendRoot
try {
    node -e $packageUpdateJs

    $tsconfigPath = Join-Path $frontendRoot "tsconfig.app.json"
    if (Test-Path $tsconfigPath) {
        $tsconfigUpdateJs = @'
const fs = require("fs");
const path = require("path");

const file = path.join(process.cwd(), "tsconfig.app.json");
const json = JSON.parse(fs.readFileSync(file, "utf8"));

json.exclude ??= [];
for (const item of ["src/**/*.stories.ts", "src/**/*.stories.tsx", ".storybook", "storybook-static"]) {
  if (!json.exclude.includes(item)) json.exclude.push(item);
}

fs.writeFileSync(file, JSON.stringify(json, null, 2) + "\n");
'@
        node -e $tsconfigUpdateJs
    }
}
finally {
    Pop-Location
}

Write-Host "Updated Frontend\PlantProcess.Web\package.json"
Write-Host "Updated Frontend\PlantProcess.Web\tsconfig.app.json"

Write-Host ""
Write-Host "=== PPIQ Phase 2 UI Standards Foundation V2 applied ==="
Write-Host ""
Write-Host "Next commands:"
Write-Host "  cd Frontend\PlantProcess.Web"
Write-Host "  npm run ui:audit"
Write-Host "  npm run ui:validate"
Write-Host "  npm run build"
Write-Host "  npm install -D storybook @storybook/react-vite @storybook/addon-docs @storybook/addon-a11y"
Write-Host "  npm run storybook"