// ============================================================
// FILE: tools/phase2/apply-phase2-full-ui-standards.cjs
//
// PlantProcess IQ - Phase 2B Full UI Standards Acceptance Pack
//
// Completes acceptance-side implementation for:
//   PPIQ-T009 button-inventory.csv
//   PPIQ-T010 table-inventory.csv
//   PPIQ-T011 tabs-inventory.csv
//   PPIQ-T012 input-inventory.csv
//   PPIQ-T013 StandardButton
//   PPIQ-T014 StandardTable
//   PPIQ-T015 StandardTabs
//   PPIQ-T016 StandardInput / StandardSelect / StandardTextArea
//   PPIQ-T017 StandardCard / StandardModal / StandardToast
//   PPIQ-T018 Storybook standards pages, onboarding, Do/Don't, tokens
//
// Run from repo root:
//   cd C:\Workspace\PlantProcess-IQ
//   node tools/phase2/apply-phase2-full-ui-standards.cjs
// ============================================================

const fs = require("fs");
const path = require("path");

const repoRoot = process.cwd();
const frontendRoot = path.join(repoRoot, "Frontend", "PlantProcess.Web");

if (!fs.existsSync(frontendRoot)) {
  throw new Error("Frontend project not found: " + frontendRoot);
}

function normalize(p) {
  return p.split(path.sep).join("/");
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function writeFile(relativePath, content) {
  const fullPath = path.join(repoRoot, relativePath);
  ensureDir(path.dirname(fullPath));
  fs.writeFileSync(fullPath, content.replace(/\r?\n/g, "\n"), "utf8");
  console.log("Wrote " + relativePath);
}

function updateJson(relativePath, updater) {
  const fullPath = path.join(repoRoot, relativePath);
  const json = JSON.parse(fs.readFileSync(fullPath, "utf8"));
  updater(json);
  fs.writeFileSync(fullPath, JSON.stringify(json, null, 2) + "\n", "utf8");
  console.log("Updated " + relativePath);
}

// ============================================================
// 1. Canonical tokens
// ============================================================

writeFile("Frontend/PlantProcess.Web/src/components/standard/tokens.ts", `
export const ppiqTokens = {
  color: {
    navy900: "#050B18",
    navy800: "#081426",
    navy700: "#0B1730",
    navy600: "#10213D",
    surface1: "rgba(11, 23, 48, 0.88)",
    surface2: "rgba(16, 33, 61, 0.82)",
    borderSubtle: "rgba(0, 212, 255, 0.12)",
    borderStrong: "rgba(0, 212, 255, 0.34)",
    brandBlue: "#0A84FF",
    brandCyan: "#00D4FF",
    success: "#2CE6A2",
    warning: "#FFD166",
    danger: "#FF4D6D",
    info: "#5AC8FA",
    text: "#EAF6FF",
    textMuted: "#A0BDD8",
    textSoft: "#6D8EAE",
    textDisabled: "#48647F",
  },
  radius: {
    sm: "6px",
    md: "8px",
    lg: "12px",
    xl: "18px",
  },
  spacing: {
    xs: "4px",
    sm: "8px",
    md: "12px",
    lg: "16px",
    xl: "24px",
    xxl: "32px",
  },
  elevation: {
    flat: "none",
    raised: "0 12px 32px rgba(0, 0, 0, 0.28)",
    floating: "0 22px 60px rgba(0, 0, 0, 0.42)",
    glow: "0 0 28px rgba(0, 212, 255, 0.2)",
  },
  motion: {
    fast: "120ms ease",
    normal: "200ms ease-out",
  },
} as const;

export type PpiqTokenMap = typeof ppiqTokens;
`);

// ============================================================
// 2. Canonical CSS
// ============================================================

writeFile("Frontend/PlantProcess.Web/src/components/standard/standard-components.css", `
:root {
  --ppiq-std-bg-0: #050B18;
  --ppiq-std-bg-1: #081426;
  --ppiq-std-bg-2: #0B1730;
  --ppiq-std-surface-1: rgba(11, 23, 48, 0.88);
  --ppiq-std-surface-2: rgba(16, 33, 61, 0.82);
  --ppiq-std-border-subtle: rgba(0, 212, 255, 0.12);
  --ppiq-std-border-strong: rgba(0, 212, 255, 0.34);
  --ppiq-std-brand-blue: #0A84FF;
  --ppiq-std-brand-cyan: #00D4FF;
  --ppiq-std-success: #2CE6A2;
  --ppiq-std-warning: #FFD166;
  --ppiq-std-danger: #FF4D6D;
  --ppiq-std-info: #5AC8FA;
  --ppiq-std-text: #EAF6FF;
  --ppiq-std-text-muted: #A0BDD8;
  --ppiq-std-text-soft: #6D8EAE;
  --ppiq-std-disabled: #48647F;
  --ppiq-std-radius-sm: 6px;
  --ppiq-std-radius-md: 8px;
  --ppiq-std-radius-lg: 12px;
  --ppiq-std-radius-xl: 18px;
  --ppiq-std-focus: 0 0 0 4px rgba(0, 212, 255, 0.42);
}

.ppiq-std-button {
  appearance: none;
  border: 1px solid transparent;
  border-radius: var(--ppiq-std-radius-md);
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif;
  font-weight: 800;
  letter-spacing: 0.01em;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  white-space: nowrap;
  text-decoration: none;
  cursor: pointer;
  user-select: none;
  transition: transform 120ms ease, background 120ms ease, border-color 120ms ease, box-shadow 120ms ease, opacity 120ms ease;
}

.ppiq-std-button:hover:not([aria-disabled="true"]):not(:disabled) {
  transform: translateY(-1px);
}

.ppiq-std-button:active:not([aria-disabled="true"]):not(:disabled) {
  transform: translateY(1px);
}

.ppiq-std-button:focus-visible {
  outline: 2px solid transparent;
  outline-offset: 2px;
  box-shadow: var(--ppiq-std-focus);
}

.ppiq-std-button[aria-disabled="true"],
.ppiq-std-button:disabled {
  opacity: 0.4;
  cursor: not-allowed;
  pointer-events: none;
}

.ppiq-std-button--sm {
  min-height: 28px;
  padding: 0 12px;
  font-size: 12px;
}

.ppiq-std-button--md {
  min-height: 36px;
  padding: 0 16px;
  font-size: 14px;
}

.ppiq-std-button--lg {
  min-height: 44px;
  padding: 0 24px;
  font-size: 16px;
}

.ppiq-std-button--icon-only.ppiq-std-button--sm {
  width: 28px;
  padding: 0;
}

.ppiq-std-button--icon-only.ppiq-std-button--md {
  width: 36px;
  padding: 0;
}

.ppiq-std-button--icon-only.ppiq-std-button--lg {
  width: 44px;
  padding: 0;
}

.ppiq-std-button--full {
  width: 100%;
}

.ppiq-std-button--primary {
  background: var(--ppiq-std-brand-blue);
  color: #FFFFFF;
  border-color: rgba(10, 132, 255, 0.72);
  box-shadow: 0 0 20px rgba(10, 132, 255, 0.24);
}

.ppiq-std-button--secondary {
  background: transparent;
  color: var(--ppiq-std-brand-blue);
  border-color: var(--ppiq-std-brand-blue);
}

.ppiq-std-button--ghost {
  background: transparent;
  color: var(--ppiq-std-brand-blue);
  border-color: transparent;
}

.ppiq-std-button--ghost:hover:not(:disabled) {
  background: var(--ppiq-std-surface-2);
}

.ppiq-std-button--danger {
  background: var(--ppiq-std-danger);
  color: #FFFFFF;
  border-color: rgba(255, 77, 109, 0.72);
}

.ppiq-std-button--success {
  background: var(--ppiq-std-success);
  color: var(--ppiq-std-bg-0);
  border-color: rgba(44, 230, 162, 0.72);
}

.ppiq-std-button__spinner {
  width: 1em;
  height: 1em;
  border-radius: 999px;
  border: 2px solid currentColor;
  border-top-color: transparent;
  animation: ppiqStdSpin 720ms linear infinite;
}

.ppiq-std-button__label--loading {
  opacity: 0.68;
}

@keyframes ppiqStdSpin {
  to {
    transform: rotate(360deg);
  }
}

.ppiq-std-card {
  border: 1px solid var(--ppiq-std-border-subtle);
  border-radius: var(--ppiq-std-radius-lg);
  background: radial-gradient(circle at top left, rgba(0, 212, 255, 0.08), transparent 38%), linear-gradient(180deg, var(--ppiq-std-surface-1), rgba(5, 11, 24, 0.84));
  color: var(--ppiq-std-text);
  overflow: hidden;
}

.ppiq-std-card--flat {
  box-shadow: none;
}

.ppiq-std-card--raised {
  box-shadow: 0 12px 32px rgba(0, 0, 0, 0.28);
}

.ppiq-std-card--floating {
  box-shadow: 0 22px 60px rgba(0, 0, 0, 0.42), 0 0 28px rgba(0, 212, 255, 0.16);
}

.ppiq-std-card__header,
.ppiq-std-card__body,
.ppiq-std-card__footer {
  padding: 16px;
}

.ppiq-std-card__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  border-bottom: 1px solid rgba(0, 212, 255, 0.08);
}

.ppiq-std-card__eyebrow {
  margin: 0 0 4px;
  color: var(--ppiq-std-brand-cyan);
  font-size: 11px;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.12em;
}

.ppiq-std-card__title {
  margin: 0;
  color: var(--ppiq-std-text);
  font-size: 16px;
}

.ppiq-std-card__subtitle {
  margin: 6px 0 0;
  color: var(--ppiq-std-text-soft);
  font-size: 12px;
}

.ppiq-std-card__footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  border-top: 1px solid rgba(0, 212, 255, 0.08);
}

.ppiq-std-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}

.ppiq-std-field__label {
  color: var(--ppiq-std-text-muted);
  font-size: 12px;
  font-weight: 800;
}

.ppiq-std-field__required {
  color: var(--ppiq-std-warning);
  margin-left: 3px;
}

.ppiq-std-field__shell {
  display: flex;
  align-items: center;
  gap: 8px;
  border: 1px solid var(--ppiq-std-border-subtle);
  border-radius: var(--ppiq-std-radius-sm);
  background: rgba(5, 11, 24, 0.72);
  color: var(--ppiq-std-text);
  transition: border-color 120ms ease, box-shadow 120ms ease;
}

.ppiq-std-field__shell:focus-within {
  border-color: var(--ppiq-std-brand-blue);
  box-shadow: 0 0 0 2px rgba(10, 132, 255, 0.38);
}

.ppiq-std-field--error .ppiq-std-field__shell {
  border-color: var(--ppiq-std-danger);
  box-shadow: 0 0 0 2px rgba(255, 77, 109, 0.24);
}

.ppiq-std-field__shell--sm {
  min-height: 28px;
  padding: 0 8px;
}

.ppiq-std-field__shell--md {
  min-height: 36px;
  padding: 0 10px;
}

.ppiq-std-field__shell--lg {
  min-height: 44px;
  padding: 0 12px;
}

.ppiq-std-field__control {
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--ppiq-std-text);
  font: 500 13px/1.4 Inter, ui-sans-serif, system-ui, sans-serif;
  width: 100%;
  min-width: 0;
}

.ppiq-std-field__control::placeholder {
  color: rgba(160, 189, 216, 0.52);
}

.ppiq-std-field__control:disabled {
  color: var(--ppiq-std-disabled);
  cursor: not-allowed;
}

.ppiq-std-field__textarea-shell {
  align-items: flex-start;
  padding-top: 8px;
  padding-bottom: 8px;
}

.ppiq-std-field__textarea {
  min-height: 92px;
  resize: vertical;
}

.ppiq-std-field__helper {
  color: var(--ppiq-std-text-soft);
  font-size: 11px;
  line-height: 1.4;
  min-height: 16px;
}

.ppiq-std-field__error {
  color: #FFC4CF;
  font-size: 11px;
  line-height: 1.4;
  min-height: 16px;
}

.ppiq-std-select-menu {
  margin-top: 4px;
  border: 1px solid var(--ppiq-std-border-subtle);
  border-radius: var(--ppiq-std-radius-md);
  background: #081426;
  box-shadow: 0 18px 46px rgba(0, 0, 0, 0.34);
  max-height: 240px;
  overflow: auto;
  padding: 6px;
  z-index: 20;
}

.ppiq-std-select-option {
  border-radius: 6px;
  padding: 8px 10px;
  color: var(--ppiq-std-text-muted);
  cursor: pointer;
}

.ppiq-std-select-option[aria-selected="true"],
.ppiq-std-select-option:hover {
  background: rgba(0, 212, 255, 0.12);
  color: var(--ppiq-std-text);
}

.ppiq-std-chip-list {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.ppiq-std-chip {
  border-radius: 999px;
  border: 1px solid var(--ppiq-std-border-subtle);
  padding: 2px 8px;
  color: var(--ppiq-std-brand-cyan);
  background: rgba(0, 212, 255, 0.08);
  font-size: 11px;
}

.ppiq-std-tabs {
  display: flex;
  gap: 16px;
}

.ppiq-std-tabs--horizontal {
  flex-direction: column;
}

.ppiq-std-tabs--vertical {
  flex-direction: row;
}

.ppiq-std-tabs__list {
  position: relative;
  display: flex;
  gap: 6px;
  padding: 4px;
  border: 1px solid var(--ppiq-std-border-subtle);
  border-radius: var(--ppiq-std-radius-md);
  background: rgba(0, 212, 255, 0.045);
  overflow-x: auto;
}

.ppiq-std-tabs--vertical .ppiq-std-tabs__list {
  flex-direction: column;
  min-width: 220px;
  overflow-x: visible;
}

.ppiq-std-tabs__button {
  position: relative;
  border: 0;
  border-radius: var(--ppiq-std-radius-sm);
  background: transparent;
  color: var(--ppiq-std-text-soft);
  min-height: 36px;
  padding: 0 12px;
  font-weight: 800;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  white-space: nowrap;
}

.ppiq-std-tabs__button:focus-visible {
  outline: 2px solid transparent;
  outline-offset: 2px;
  box-shadow: var(--ppiq-std-focus);
}

.ppiq-std-tabs__button[aria-selected="true"] {
  color: var(--ppiq-std-text);
  background: rgba(10, 132, 255, 0.13);
}

.ppiq-std-tabs__button[aria-selected="true"]::after {
  content: "";
  position: absolute;
  background: var(--ppiq-std-brand-blue);
  transition: all 200ms ease-out;
}

.ppiq-std-tabs--horizontal .ppiq-std-tabs__button[aria-selected="true"]::after {
  left: 10px;
  right: 10px;
  bottom: 0;
  height: 4px;
  border-radius: 999px 999px 0 0;
}

.ppiq-std-tabs--vertical .ppiq-std-tabs__button[aria-selected="true"]::after {
  left: 0;
  top: 7px;
  bottom: 7px;
  width: 4px;
  border-radius: 999px;
}

.ppiq-std-tabs__badge {
  min-width: 18px;
  height: 18px;
  border-radius: 999px;
  padding: 0 6px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 212, 255, 0.14);
  color: var(--ppiq-std-brand-cyan);
  font-size: 10px;
}

.ppiq-std-tabs__panel {
  min-width: 0;
  flex: 1;
}

.ppiq-std-table-shell {
  border: 1px solid var(--ppiq-std-border-subtle);
  border-radius: var(--ppiq-std-radius-lg);
  background: rgba(8, 20, 38, 0.68);
  overflow: hidden;
  color: var(--ppiq-std-text);
}

.ppiq-std-table-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  padding: 12px;
  border-bottom: 1px solid rgba(0, 212, 255, 0.08);
}

.ppiq-std-table-toolbar__left,
.ppiq-std-table-toolbar__right {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.ppiq-std-table-scroll {
  overflow: auto;
  max-height: var(--ppiq-table-max-height, none);
}

.ppiq-std-table {
  width: 100%;
  border-collapse: collapse;
  min-width: 760px;
}

.ppiq-std-table th {
  position: sticky;
  top: 0;
  z-index: 1;
  background: #0B1730;
  color: var(--ppiq-std-text-soft);
  font-size: 11px;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  text-align: left;
  padding: 10px 12px;
  border-bottom: 1px solid var(--ppiq-std-border-subtle);
  user-select: none;
}

.ppiq-std-table td {
  color: var(--ppiq-std-text-muted);
  padding: 10px 12px;
  border-bottom: 1px solid rgba(0, 212, 255, 0.06);
  font-size: 13px;
}

.ppiq-std-table--compact td {
  height: 36px;
  padding-top: 6px;
  padding-bottom: 6px;
}

.ppiq-std-table--comfortable td {
  height: 44px;
}

.ppiq-std-table--spacious td {
  height: 56px;
}

.ppiq-std-table tr:hover td {
  background: rgba(0, 212, 255, 0.045);
}

.ppiq-std-table tr[aria-selected="true"] td {
  background: rgba(0, 212, 255, 0.08);
}

.ppiq-std-table tr[aria-selected="true"] td:first-child {
  box-shadow: inset 2px 0 0 var(--ppiq-std-brand-cyan);
}

.ppiq-std-table__header-button {
  border: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  text-transform: inherit;
  letter-spacing: inherit;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
}

.ppiq-std-table__resize-handle {
  float: right;
  width: 4px;
  height: 20px;
  cursor: col-resize;
  border-radius: 99px;
  background: rgba(0, 212, 255, 0.18);
}

.ppiq-std-table-state {
  padding: 36px 18px;
  text-align: center;
  color: var(--ppiq-std-text-soft);
}

.ppiq-std-table-state strong {
  display: block;
  color: var(--ppiq-std-text);
  margin-bottom: 6px;
}

.ppiq-std-table-skeleton {
  height: 14px;
  border-radius: 999px;
  background: linear-gradient(90deg, rgba(0, 212, 255, 0.08), rgba(0, 212, 255, 0.18), rgba(0, 212, 255, 0.08));
  background-size: 200% 100%;
  animation: ppiqStdSkeleton 1.2s ease-in-out infinite;
}

@keyframes ppiqStdSkeleton {
  0% { background-position: 0% 50%; }
  100% { background-position: -200% 50%; }
}

.ppiq-std-table-pagination {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  padding: 12px;
  border-top: 1px solid rgba(0, 212, 255, 0.08);
  color: var(--ppiq-std-text-soft);
  font-size: 12px;
}

.ppiq-std-modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 1000;
  padding: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(3, 8, 18, 0.74);
  backdrop-filter: blur(8px);
}

.ppiq-std-modal {
  width: min(100%, var(--ppiq-modal-width, 720px));
  max-height: calc(100vh - 48px);
  display: flex;
  flex-direction: column;
  border-radius: var(--ppiq-std-radius-xl);
  border: 1px solid var(--ppiq-std-border-strong);
  background: linear-gradient(180deg, #0B1730, #050B18);
  box-shadow: 0 22px 60px rgba(0, 0, 0, 0.42), 0 0 28px rgba(0, 212, 255, 0.18);
  color: var(--ppiq-std-text);
}

.ppiq-std-modal__header,
.ppiq-std-modal__body,
.ppiq-std-modal__footer {
  padding: 16px;
}

.ppiq-std-modal__header {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  border-bottom: 1px solid var(--ppiq-std-border-subtle);
}

.ppiq-std-modal__body {
  overflow: auto;
}

.ppiq-std-modal__footer {
  display: flex;
  justify-content: space-between;
  gap: 10px;
  border-top: 1px solid var(--ppiq-std-border-subtle);
}

.ppiq-std-modal__footer-actions {
  margin-left: auto;
  display: flex;
  gap: 8px;
}

.ppiq-std-standards-page {
  min-height: 100vh;
  padding: 28px;
  background: radial-gradient(circle at 12% 0%, rgba(0, 212, 255, 0.12), transparent 34%), linear-gradient(180deg, #050B18, #081426);
  color: var(--ppiq-std-text);
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif;
}

.ppiq-std-standards-grid {
  display: grid;
  gap: 18px;
}

.ppiq-std-standards-grid--two {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.ppiq-std-token-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(148px, 1fr));
  gap: 12px;
}

.ppiq-std-token {
  border: 1px solid var(--ppiq-std-border-subtle);
  border-radius: var(--ppiq-std-radius-md);
  overflow: hidden;
  background: rgba(8, 20, 38, 0.72);
}

.ppiq-std-token__swatch {
  height: 60px;
}

.ppiq-std-token__body {
  padding: 10px;
}

.ppiq-std-token__name {
  color: var(--ppiq-std-text);
  font-size: 12px;
  font-weight: 900;
}

.ppiq-std-token__value {
  margin-top: 4px;
  color: var(--ppiq-std-text-soft);
  font-size: 11px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
}

.ppiq-std-do-dont {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 18px;
}

.ppiq-std-do,
.ppiq-std-dont {
  border-radius: var(--ppiq-std-radius-lg);
  padding: 16px;
  border: 1px solid var(--ppiq-std-border-subtle);
}

.ppiq-std-do {
  background: rgba(44, 230, 162, 0.07);
  border-color: rgba(44, 230, 162, 0.24);
}

.ppiq-std-dont {
  background: rgba(255, 77, 109, 0.07);
  border-color: rgba(255, 77, 109, 0.24);
}

@media (max-width: 880px) {
  .ppiq-std-standards-page {
    padding: 16px;
  }

  .ppiq-std-standards-grid--two,
  .ppiq-std-do-dont {
    grid-template-columns: 1fr;
  }

  .ppiq-std-tabs--vertical {
    flex-direction: column;
  }

  .ppiq-std-tabs--vertical .ppiq-std-tabs__list {
    flex-direction: row;
    min-width: 0;
    overflow-x: auto;
  }
}
`);

// ============================================================
// 3. StandardButton
// ============================================================

writeFile("Frontend/PlantProcess.Web/src/components/standard/StandardButton.tsx", `
import { forwardRef, type AnchorHTMLAttributes, type ButtonHTMLAttributes, type MouseEvent, type ReactNode, type Ref } from "react";
import { Loader2 } from "lucide-react";
import "./standard-components.css";

type StandardButtonVariant = "primary" | "secondary" | "ghost" | "danger" | "success";
type StandardButtonSize = "sm" | "md" | "lg";
type StandardButtonAs = "button" | "a";

type SharedProps = {
  as?: StandardButtonAs;
  variant?: StandardButtonVariant;
  size?: StandardButtonSize;
  leadingIcon?: ReactNode;
  trailingIcon?: ReactNode;
  isLoading?: boolean;
  isDisabled?: boolean;
  fullWidth?: boolean;
  iconOnly?: boolean;
  ariaLabel?: string;
  children?: ReactNode;
};

type ButtonProps = SharedProps &
  Omit<ButtonHTMLAttributes<HTMLButtonElement>, "disabled" | "aria-label"> & {
    as?: "button";
    href?: never;
  };

type AnchorProps = SharedProps &
  Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "aria-label"> & {
    as: "a";
    href: string;
  };

export type StandardButtonProps = ButtonProps | AnchorProps;

function classes(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

export const StandardButton = forwardRef<HTMLButtonElement | HTMLAnchorElement, StandardButtonProps>(
  (props, ref) => {
    const {
      as = "button",
      variant = "primary",
      size = "md",
      leadingIcon,
      trailingIcon,
      isLoading = false,
      isDisabled = false,
      fullWidth = false,
      iconOnly = false,
      ariaLabel,
      children,
      className,
      onClick,
      ...rest
    } = props;

    const disabled = isDisabled || isLoading;

    const classNames = classes(
      "ppiq-std-button",
      "ppiq-std-button--" + variant,
      "ppiq-std-button--" + size,
      fullWidth && "ppiq-std-button--full",
      iconOnly && "ppiq-std-button--icon-only",
      className,
    );

    const content = (
      <>
        {isLoading ? <Loader2 className="ppiq-std-button__spinner" size={16} aria-hidden="true" /> : leadingIcon}
        {!iconOnly ? (
          <span className={isLoading ? "ppiq-std-button__label--loading" : undefined}>
            {isLoading ? "Loading" : children}
          </span>
        ) : (
          <span className="sr-only">{ariaLabel ?? String(children ?? "Button")}</span>
        )}
        {!isLoading ? trailingIcon : null}
      </>
    );

    if (as === "a") {
      const anchorProps = rest as Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "aria-label">;
      return (
        <a
          ref={ref as Ref<HTMLAnchorElement>}
          className={classNames}
          aria-label={ariaLabel}
          aria-disabled={disabled || undefined}
          aria-busy={isLoading || undefined}
          tabIndex={disabled ? -1 : anchorProps.tabIndex}
          onClick={(event) => {
            if (disabled) {
              event.preventDefault();
              return;
            }
            onClick?.(event as unknown as MouseEvent<HTMLButtonElement>);
          }}
          {...anchorProps}
        >
          {content}
        </a>
      );
    }

    const buttonProps = rest as Omit<ButtonHTMLAttributes<HTMLButtonElement>, "disabled" | "aria-label">;

    return (
      <button
        ref={ref as Ref<HTMLButtonElement>}
        type={buttonProps.type ?? "button"}
        className={classNames}
        disabled={disabled}
        aria-label={ariaLabel}
        aria-busy={isLoading || undefined}
        aria-disabled={disabled || undefined}
        onClick={onClick as ButtonHTMLAttributes<HTMLButtonElement>["onClick"]}
        {...buttonProps}
      >
        {content}
      </button>
    );
  },
);

StandardButton.displayName = "StandardButton";
`);

// ============================================================
// 4. StandardFields
// ============================================================

writeFile("Frontend/PlantProcess.Web/src/components/standard/StandardFields.tsx", `
import {
  forwardRef,
  useMemo,
  useState,
  type ChangeEvent,
  type InputHTMLAttributes,
  type ReactNode,
  type TextareaHTMLAttributes,
} from "react";
import { ChevronDown, Search, X } from "lucide-react";
import { StandardButton } from "./StandardButton";
import "./standard-components.css";

export type StandardFieldSize = "sm" | "md" | "lg";

function cx(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

type FieldChromeProps = {
  id?: string;
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
  required?: boolean;
  size?: StandardFieldSize;
  leadingIcon?: ReactNode;
  trailingIcon?: ReactNode;
  children: (id: string, describedBy?: string) => ReactNode;
  textarea?: boolean;
  className?: string;
};

function FieldChrome({
  id,
  label,
  helperText,
  error,
  required,
  size = "md",
  leadingIcon,
  trailingIcon,
  children,
  textarea,
  className,
}: FieldChromeProps) {
  const safeId = id ?? "ppiq-field-" + Math.random().toString(36).slice(2);
  const hintId = helperText ? safeId + "-hint" : undefined;
  const errorId = error ? safeId + "-error" : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(" ") || undefined;

  return (
    <div className={cx("ppiq-std-field", Boolean(error) && "ppiq-std-field--error", className)}>
      {label ? (
        <label className="ppiq-std-field__label" htmlFor={safeId}>
          {label}
          {required ? <span className="ppiq-std-field__required">*</span> : null}
        </label>
      ) : null}

      <div className={cx("ppiq-std-field__shell", "ppiq-std-field__shell--" + size, textarea && "ppiq-std-field__textarea-shell")}>
        {leadingIcon ? <span aria-hidden="true">{leadingIcon}</span> : null}
        {children(safeId, describedBy)}
        {trailingIcon ? <span aria-hidden="true">{trailingIcon}</span> : null}
      </div>

      {error ? (
        <div id={errorId} role="alert" className="ppiq-std-field__error">
          {error}
        </div>
      ) : (
        <div id={hintId} className="ppiq-std-field__helper">
          {helperText ?? " "}
        </div>
      )}
    </div>
  );
}

export type StandardInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, "size" | "onChange"> & {
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
  leadingIcon?: ReactNode;
  trailingIcon?: ReactNode;
  size?: StandardFieldSize;
  isLoading?: boolean;
  value?: string;
  onChange?: (value: string, event: ChangeEvent<HTMLInputElement>) => void;
};

export const StandardInput = forwardRef<HTMLInputElement, StandardInputProps>(
  (
    {
      label,
      helperText,
      error,
      leadingIcon,
      trailingIcon,
      size = "md",
      required,
      id,
      type = "text",
      value,
      onChange,
      className,
      disabled,
      isLoading,
      ...rest
    },
    ref,
  ) => {
    const isSearch = type === "search";

    return (
      <FieldChrome
        id={id}
        label={label}
        helperText={helperText}
        error={error}
        required={required}
        size={size}
        leadingIcon={leadingIcon ?? (isSearch ? <Search size={16} /> : undefined)}
        trailingIcon={
          trailingIcon ??
          (isSearch && value ? (
            <StandardButton
              variant="ghost"
              size="sm"
              iconOnly
              ariaLabel="Clear search"
              onClick={() => {
                const synthetic = { target: { value: "" } } as ChangeEvent<HTMLInputElement>;
                onChange?.("", synthetic);
              }}
            >
              <X size={14} />
            </StandardButton>
          ) : undefined)
        }
        className={className}
      >
        {(fieldId, describedBy) => (
          <input
            ref={ref}
            id={fieldId}
            className="ppiq-std-field__control"
            type={type}
            value={value}
            required={required}
            disabled={disabled || isLoading}
            aria-invalid={Boolean(error) || undefined}
            aria-describedby={describedBy}
            aria-busy={isLoading || undefined}
            onChange={(event) => onChange?.(event.target.value, event)}
            {...rest}
          />
        )}
      </FieldChrome>
    );
  },
);

StandardInput.displayName = "StandardInput";

export type StandardTextAreaProps = Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, "onChange"> & {
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
  leadingIcon?: ReactNode;
  trailingIcon?: ReactNode;
  size?: StandardFieldSize;
  value?: string;
  onChange?: (value: string, event: ChangeEvent<HTMLTextAreaElement>) => void;
};

export const StandardTextArea = forwardRef<HTMLTextAreaElement, StandardTextAreaProps>(
  (
    {
      label,
      helperText,
      error,
      leadingIcon,
      trailingIcon,
      size = "md",
      required,
      id,
      value,
      onChange,
      className,
      ...rest
    },
    ref,
  ) => (
    <FieldChrome
      id={id}
      label={label}
      helperText={helperText}
      error={error}
      required={required}
      size={size}
      leadingIcon={leadingIcon}
      trailingIcon={trailingIcon}
      textarea
      className={className}
    >
      {(fieldId, describedBy) => (
        <textarea
          ref={ref}
          id={fieldId}
          className="ppiq-std-field__control ppiq-std-field__textarea"
          value={value}
          required={required}
          aria-invalid={Boolean(error) || undefined}
          aria-describedby={describedBy}
          onChange={(event) => onChange?.(event.target.value, event)}
          {...rest}
        />
      )}
    </FieldChrome>
  ),
);

StandardTextArea.displayName = "StandardTextArea";

export type StandardSelectOption = {
  value: string;
  label: ReactNode;
  searchText?: string;
  disabled?: boolean;
};

export type StandardSelectProps = {
  id?: string;
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
  required?: boolean;
  disabled?: boolean;
  size?: StandardFieldSize;
  placeholder?: string;
  value?: string | string[];
  options: ReadonlyArray<StandardSelectOption>;
  multiple?: boolean;
  searchable?: boolean;
  onChange?: (value: string | string[]) => void;
};

export function StandardSelect({
  id,
  label,
  helperText,
  error,
  required,
  disabled,
  size = "md",
  placeholder = "Select...",
  value,
  options,
  multiple = false,
  searchable = false,
  onChange,
}: StandardSelectProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");

  const selectedValues = Array.isArray(value) ? value : value ? [value] : [];

  const filteredOptions = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return options;
    return options.filter((option) =>
      String(option.searchText ?? option.label).toLowerCase().includes(q),
    );
  }, [options, query]);

  const selectedLabels = options
    .filter((option) => selectedValues.includes(option.value))
    .map((option) => option.label);

  function toggle(valueToToggle: string) {
    if (multiple) {
      const next = selectedValues.includes(valueToToggle)
        ? selectedValues.filter((item) => item !== valueToToggle)
        : [...selectedValues, valueToToggle];
      onChange?.(next);
      return;
    }

    onChange?.(valueToToggle);
    setOpen(false);
  }

  return (
    <FieldChrome
      id={id}
      label={label}
      helperText={helperText}
      error={error}
      required={required}
      size={size}
      trailingIcon={<ChevronDown size={16} />}
    >
      {(fieldId, describedBy) => (
        <div style={{ width: "100%", position: "relative" }}>
          <button
            id={fieldId}
            type="button"
            className="ppiq-std-field__control"
            disabled={disabled}
            aria-haspopup="listbox"
            aria-expanded={open}
            aria-invalid={Boolean(error) || undefined}
            aria-describedby={describedBy}
            onClick={() => setOpen((current) => !current)}
            onKeyDown={(event) => {
              if (event.key === "Escape") setOpen(false);
              if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                setOpen((current) => !current);
              }
            }}
            style={{ textAlign: "left" }}
          >
            {selectedLabels.length > 0 ? (
              multiple ? (
                <span className="ppiq-std-chip-list">
                  {selectedLabels.map((label, index) => (
                    <span className="ppiq-std-chip" key={index}>
                      {label}
                    </span>
                  ))}
                </span>
              ) : (
                selectedLabels[0]
              )
            ) : (
              <span style={{ color: "rgba(160, 189, 216, 0.52)" }}>{placeholder}</span>
            )}
          </button>

          {open ? (
            <div className="ppiq-std-select-menu" role="listbox" aria-multiselectable={multiple || undefined}>
              {searchable ? (
                <input
                  className="ppiq-std-field__control"
                  placeholder="Search options..."
                  value={query}
                  onChange={(event) => setQuery(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === "Escape") setOpen(false);
                  }}
                  style={{ padding: "8px 10px", borderBottom: "1px solid rgba(0, 212, 255, 0.12)" }}
                />
              ) : null}

              {filteredOptions.map((option) => (
                <div
                  key={option.value}
                  role="option"
                  aria-selected={selectedValues.includes(option.value)}
                  className="ppiq-std-select-option"
                  onClick={() => {
                    if (!option.disabled) toggle(option.value);
                  }}
                >
                  {option.label}
                </div>
              ))}
            </div>
          ) : null}
        </div>
      )}
    </FieldChrome>
  );
}
`);

// ============================================================
// 5. StandardTabs
// ============================================================

writeFile("Frontend/PlantProcess.Web/src/components/standard/StandardTabs.tsx", `
import { useEffect, useMemo, useRef, type KeyboardEvent, type ReactNode } from "react";
import "./standard-components.css";

export type StandardTabsOrientation = "horizontal" | "vertical";

export type StandardTabItem<TId extends string = string> = {
  id: TId;
  label: ReactNode;
  icon?: ReactNode;
  badge?: ReactNode;
  disabled?: boolean;
  content: ReactNode;
  preload?: boolean;
};

export type StandardTabsProps<TId extends string = string> = {
  items: ReadonlyArray<StandardTabItem<TId>>;
  value: TId;
  onChange: (value: TId) => void;
  orientation?: StandardTabsOrientation;
  lazy?: boolean;
  searchParam?: string;
  ariaLabel: string;
  className?: string;
};

function cx(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

export function StandardTabs<TId extends string = string>({
  items,
  value,
  onChange,
  orientation = "horizontal",
  lazy = true,
  searchParam,
  ariaLabel,
  className,
}: StandardTabsProps<TId>) {
  const refs = useRef<Array<HTMLButtonElement | null>>([]);

  const enabledItems = useMemo(() => items.filter((item) => !item.disabled), [items]);
  const activeIndex = items.findIndex((item) => item.id === value);
  const activeItem = items[activeIndex] ?? items[0];

  useEffect(() => {
    if (!searchParam) return;

    const params = new URLSearchParams(window.location.search);
    const tabFromUrl = params.get(searchParam);
    const match = items.find((item) => item.id === tabFromUrl && !item.disabled);

    if (match && match.id !== value) {
      onChange(match.id);
    }
  }, []);

  useEffect(() => {
    if (!searchParam) return;

    const url = new URL(window.location.href);
    url.searchParams.set(searchParam, value);
    window.history.replaceState(null, "", url.toString());
  }, [searchParam, value]);

  function activateByOffset(offset: number) {
    const currentEnabledIndex = enabledItems.findIndex((item) => item.id === value);
    const nextIndex = (currentEnabledIndex + offset + enabledItems.length) % enabledItems.length;
    const next = enabledItems[nextIndex];

    if (next) {
      onChange(next.id);
      const realIndex = items.findIndex((item) => item.id === next.id);
      refs.current[realIndex]?.focus();
    }
  }

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const nextKey = orientation === "horizontal" ? "ArrowRight" : "ArrowDown";
    const previousKey = orientation === "horizontal" ? "ArrowLeft" : "ArrowUp";

    if (event.key === nextKey) {
      event.preventDefault();
      activateByOffset(1);
    }

    if (event.key === previousKey) {
      event.preventDefault();
      activateByOffset(-1);
    }

    if (event.key === "Home") {
      event.preventDefault();
      const first = enabledItems[0];
      if (first) onChange(first.id);
    }

    if (event.key === "End") {
      event.preventDefault();
      const last = enabledItems[enabledItems.length - 1];
      if (last) onChange(last.id);
    }

    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      const focusedIndex = refs.current.findIndex((item) => item === document.activeElement);
      const item = items[focusedIndex];
      if (item && !item.disabled) onChange(item.id);
    }
  }

  return (
    <div className={cx("ppiq-std-tabs", "ppiq-std-tabs--" + orientation, className)}>
      <div
        className="ppiq-std-tabs__list"
        role="tablist"
        aria-label={ariaLabel}
        aria-orientation={orientation}
        onKeyDown={onKeyDown}
      >
        {items.map((item, index) => (
          <button
            key={item.id}
            ref={(node) => {
              refs.current[index] = node;
            }}
            id={"ppiq-tab-" + item.id}
            type="button"
            role="tab"
            className="ppiq-std-tabs__button"
            aria-selected={item.id === value}
            aria-controls={"ppiq-tab-panel-" + item.id}
            disabled={item.disabled}
            tabIndex={item.id === value ? 0 : -1}
            onClick={() => {
              if (!item.disabled) onChange(item.id);
            }}
          >
            {item.icon ? <span aria-hidden="true">{item.icon}</span> : null}
            <span>{item.label}</span>
            {item.badge ? <span className="ppiq-std-tabs__badge">{item.badge}</span> : null}
          </button>
        ))}
      </div>

      <div className="ppiq-std-tabs__panel">
        {items.map((item) => {
          const isActive = item.id === value;

          if (lazy && !isActive && !item.preload) {
            return null;
          }

          return (
            <div
              key={item.id}
              id={"ppiq-tab-panel-" + item.id}
              role="tabpanel"
              aria-labelledby={"ppiq-tab-" + item.id}
              hidden={!isActive}
            >
              {item.content}
            </div>
          );
        })}

        {!activeItem ? null : null}
      </div>
    </div>
  );
}
`);

// ============================================================
// 6. StandardSurface
// ============================================================

writeFile("Frontend/PlantProcess.Web/src/components/standard/StandardSurface.tsx", `
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type CSSProperties,
  type HTMLAttributes,
  type ReactNode,
} from "react";
import { createPortal } from "react-dom";
import { CheckCircle2, Info, Loader2, TriangleAlert, X } from "lucide-react";
import { StandardButton } from "./StandardButton";
import "./standard-components.css";

function cx(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

export type StandardCardElevation = "flat" | "raised" | "floating";

export type StandardCardProps = HTMLAttributes<HTMLElement> & {
  eyebrow?: ReactNode;
  title?: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
  footer?: ReactNode;
  elevation?: StandardCardElevation;
  as?: "section" | "article" | "div";
};

export function StandardCard({
  eyebrow,
  title,
  subtitle,
  actions,
  footer,
  elevation = "raised",
  as = "section",
  children,
  className,
  ...rest
}: StandardCardProps) {
  const Component = as;

  return (
    <Component className={cx("ppiq-std-card", "ppiq-std-card--" + elevation, className)} {...rest}>
      {eyebrow || title || subtitle || actions ? (
        <header className="ppiq-std-card__header">
          <div>
            {eyebrow ? <p className="ppiq-std-card__eyebrow">{eyebrow}</p> : null}
            {title ? <h3 className="ppiq-std-card__title">{title}</h3> : null}
            {subtitle ? <p className="ppiq-std-card__subtitle">{subtitle}</p> : null}
          </div>
          {actions ? <div>{actions}</div> : null}
        </header>
      ) : null}

      <div className="ppiq-std-card__body">{children}</div>

      {footer ? <footer className="ppiq-std-card__footer">{footer}</footer> : null}
    </Component>
  );
}

export type StandardModalSize = "sm" | "md" | "lg" | "xl";

export type StandardModalProps = {
  open: boolean;
  title: ReactNode;
  description?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  size?: StandardModalSize;
  isDirty?: boolean;
  closeOnOutsideClick?: boolean;
  onClose: () => void;
};

const modalWidths: Record<StandardModalSize, string> = {
  sm: "420px",
  md: "640px",
  lg: "840px",
  xl: "1080px",
};

export function StandardModal({
  open,
  title,
  description,
  children,
  footer,
  size = "md",
  isDirty = false,
  closeOnOutsideClick = true,
  onClose,
}: StandardModalProps) {
  const panelRef = useRef<HTMLElement | null>(null);
  const previouslyFocusedRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;

    previouslyFocusedRef.current = document.activeElement as HTMLElement | null;

    const focusableSelector =
      "a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex='-1'])";

    const focusFirst = () => {
      const first = panelRef.current?.querySelector<HTMLElement>(focusableSelector);
      first?.focus();
    };

    window.setTimeout(focusFirst, 0);

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }

      if (event.key === "Tab") {
        const focusable = Array.from(panelRef.current?.querySelectorAll<HTMLElement>(focusableSelector) ?? []);
        if (focusable.length === 0) return;

        const first = focusable[0];
        const last = focusable[focusable.length - 1];

        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault();
          last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault();
          first.focus();
        }
      }
    };

    document.addEventListener("keydown", onKeyDown);

    return () => {
      document.removeEventListener("keydown", onKeyDown);
      previouslyFocusedRef.current?.focus?.();
    };
  }, [open, onClose]);

  if (!open || typeof document === "undefined") {
    return null;
  }

  return createPortal(
    <div
      className="ppiq-std-modal-backdrop"
      role="presentation"
      onMouseDown={() => {
        if (closeOnOutsideClick && !isDirty) onClose();
      }}
    >
      <section
        ref={panelRef}
        className="ppiq-std-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="ppiq-standard-modal-title"
        style={{ "--ppiq-modal-width": modalWidths[size] } as CSSProperties}
        onMouseDown={(event) => event.stopPropagation()}
      >
        <header className="ppiq-std-modal__header">
          <div>
            <h2 id="ppiq-standard-modal-title" style={{ margin: 0 }}>
              {title}
            </h2>
            {description ? (
              <p style={{ margin: "6px 0 0", color: "var(--ppiq-std-text-soft)" }}>{description}</p>
            ) : null}
          </div>

          <StandardButton variant="ghost" size="sm" iconOnly ariaLabel="Close modal" onClick={onClose}>
            <X size={18} />
          </StandardButton>
        </header>

        <div className="ppiq-std-modal__body">{children}</div>

        {footer ? (
          <footer className="ppiq-std-modal__footer">
            <div />
            <div className="ppiq-std-modal__footer-actions">{footer}</div>
          </footer>
        ) : null}
      </section>
    </div>,
    document.body,
  );
}

export type StandardToastVariant = "info" | "success" | "warning" | "error" | "loading";

export type StandardToastMessage = {
  id: string;
  variant: StandardToastVariant;
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
  durationMs?: number;
};

type ToastContextValue = {
  notify: (message: Omit<StandardToastMessage, "id"> & { id?: string }) => string;
  dismiss: (id: string) => void;
};

const ToastContext = createContext<ToastContextValue | null>(null);

const toastIcons: Record<StandardToastVariant, ReactNode> = {
  info: <Info size={18} />,
  success: <CheckCircle2 size={18} />,
  warning: <TriangleAlert size={18} />,
  error: <TriangleAlert size={18} />,
  loading: <Loader2 size={18} className="ppiq-std-button__spinner" />,
};

export function StandardToastProvider({ children }: { children: ReactNode }) {
  const [messages, setMessages] = useState<StandardToastMessage[]>([]);

  const dismiss = useCallback((id: string) => {
    setMessages((current) => current.filter((message) => message.id !== id));
  }, []);

  const notify = useCallback(
    (message: Omit<StandardToastMessage, "id"> & { id?: string }) => {
      const id = message.id ?? "toast-" + Date.now() + "-" + Math.random().toString(36).slice(2);
      const next: StandardToastMessage = { ...message, id };

      setMessages((current) => [next, ...current].slice(0, 5));

      if (next.variant !== "loading") {
        window.setTimeout(() => dismiss(id), next.durationMs ?? 5000);
      }

      return id;
    },
    [dismiss],
  );

  return (
    <ToastContext.Provider value={{ notify, dismiss }}>
      {children}
      <div className="ppiq-toast-viewport" role="region" aria-label="Notifications">
        {messages.slice(0, 3).map((message) => (
          <article key={message.id} className="ppiq-toast" role="status">
            <div style={{ display: "flex", gap: 10 }}>
              <span aria-hidden="true">{toastIcons[message.variant]}</span>
              <div>
                <p className="ppiq-toast__title">{message.title}</p>
                {message.description ? <p className="ppiq-toast__description">{message.description}</p> : null}
                {message.action ? <div style={{ marginTop: 8 }}>{message.action}</div> : null}
              </div>
            </div>
            <StandardButton variant="ghost" size="sm" iconOnly ariaLabel="Dismiss notification" onClick={() => dismiss(message.id)}>
              <X size={14} />
            </StandardButton>
          </article>
        ))}

        {messages.length > 3 ? (
          <article className="ppiq-toast" role="status">
            <p className="ppiq-toast__title">+{messages.length - 3} more notifications</p>
          </article>
        ) : null}
      </div>
    </ToastContext.Provider>
  );
}

export function useStandardToast() {
  const value = useContext(ToastContext);
  if (!value) {
    throw new Error("useStandardToast must be used inside StandardToastProvider.");
  }
  return value;
}
`);

// ============================================================
// 7. StandardTable
// ============================================================

writeFile("Frontend/PlantProcess.Web/src/components/standard/StandardTable.tsx", `
import { useMemo, useState, type ReactNode } from "react";
import { ChevronDown, ChevronUp, Download, RefreshCw, Search } from "lucide-react";
import { StandardButton } from "./StandardButton";
import { StandardInput, StandardSelect } from "./StandardFields";
import "./standard-components.css";

export type StandardTableDensity = "compact" | "comfortable" | "spacious";
export type StandardTableSortDirection = "asc" | "desc";
export type StandardTableSelectionMode = "none" | "single" | "multi";

export type StandardTableColumn<T> = {
  key: string;
  header: ReactNode;
  accessor?: keyof T | ((row: T) => unknown);
  cell?: (row: T, rowIndex: number) => ReactNode;
  sortable?: boolean;
  filterable?: boolean;
  width?: number;
  minWidth?: number;
  align?: "left" | "center" | "right";
  hidden?: boolean;
};

export type StandardTableQuery = {
  pageIndex: number;
  pageSize: number;
  filter: string;
  sorting: Array<{ key: string; direction: StandardTableSortDirection }>;
};

export type StandardTableProps<T> = {
  columns: ReadonlyArray<StandardTableColumn<T>>;
  data: ReadonlyArray<T>;
  getRowKey: (row: T, rowIndex: number) => string;
  caption?: string;
  isLoading?: boolean;
  hasError?: boolean;
  errorMessage?: ReactNode;
  onRetry?: () => void;
  emptyTitle?: ReactNode;
  emptyDescription?: ReactNode;
  primaryAction?: ReactNode;
  selectionMode?: StandardTableSelectionMode;
  selectedRowKeys?: ReadonlyArray<string>;
  onSelectionChange?: (keys: string[]) => void;
  onRowClick?: (row: T, rowIndex: number) => void;
  enableFiltering?: boolean;
  enableExport?: boolean;
  enableColumnVisibility?: boolean;
  enableDensityToggle?: boolean;
  enablePagination?: boolean;
  enableVirtualization?: boolean;
  serverMode?: boolean;
  totalCount?: number;
  onQueryChange?: (query: StandardTableQuery) => void;
  defaultPageSize?: number;
  className?: string;
};

function cx(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

function valueOf<T>(row: T, column: StandardTableColumn<T>): unknown {
  if (typeof column.accessor === "function") return column.accessor(row);
  if (typeof column.accessor === "string") return row[column.accessor];
  return "";
}

function toText(value: unknown): string {
  if (value === null || value === undefined) return "";
  return String(value);
}

function escapeCsv(value: unknown): string {
  const text = toText(value);
  if (text.includes(",") || text.includes("\\n") || text.includes('"')) {
    return '"' + text.replace(/"/g, '""') + '"';
  }
  return text;
}

function downloadCsv(filename: string, headers: string[], rows: string[][]) {
  const csv = [headers.map(escapeCsv).join(","), ...rows.map((row) => row.map(escapeCsv).join(","))].join("\\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

export function StandardTable<T>({
  columns,
  data,
  getRowKey,
  caption,
  isLoading = false,
  hasError = false,
  errorMessage = "Refreshing data did not complete. Retry when the source is available.",
  onRetry,
  emptyTitle = "No records available",
  emptyDescription = "Adjust filters or refresh the data source.",
  primaryAction,
  selectionMode = "none",
  selectedRowKeys,
  onSelectionChange,
  onRowClick,
  enableFiltering = false,
  enableExport = false,
  enableColumnVisibility = false,
  enableDensityToggle = false,
  enablePagination = false,
  enableVirtualization = false,
  serverMode = false,
  totalCount,
  onQueryChange,
  defaultPageSize = 25,
  className,
}: StandardTableProps<T>) {
  const [filter, setFilter] = useState("");
  const [density, setDensity] = useState<StandardTableDensity>("comfortable");
  const [pageIndex, setPageIndex] = useState(0);
  const [pageSize, setPageSize] = useState(defaultPageSize);
  const [sorting, setSorting] = useState<Array<{ key: string; direction: StandardTableSortDirection }>>([]);
  const [hiddenColumns, setHiddenColumns] = useState<string[]>([]);
  const [internalSelected, setInternalSelected] = useState<string[]>([]);

  const selected = selectedRowKeys ? [...selectedRowKeys] : internalSelected;

  const visibleColumns = columns.filter((column) => !column.hidden && !hiddenColumns.includes(column.key));

  function emitQuery(next?: Partial<StandardTableQuery>) {
    onQueryChange?.({
      pageIndex,
      pageSize,
      filter,
      sorting,
      ...next,
    });
  }

  function toggleSort(column: StandardTableColumn<T>, shiftKey: boolean) {
    if (!column.sortable) return;

    setSorting((current) => {
      const existing = current.find((item) => item.key === column.key);
      const nextDirection: StandardTableSortDirection = existing?.direction === "asc" ? "desc" : "asc";
      const next = shiftKey
        ? [...current.filter((item) => item.key !== column.key), { key: column.key, direction: nextDirection }]
        : [{ key: column.key, direction: nextDirection }];

      emitQuery({ sorting: next });
      return next;
    });
  }

  const processed = useMemo(() => {
    let rows = [...data];

    if (!serverMode && filter.trim()) {
      const q = filter.trim().toLowerCase();
      rows = rows.filter((row) =>
        visibleColumns.some((column) => toText(valueOf(row, column)).toLowerCase().includes(q)),
      );
    }

    if (!serverMode && sorting.length > 0) {
      rows.sort((a, b) => {
        for (const item of sorting) {
          const column = visibleColumns.find((candidate) => candidate.key === item.key);
          if (!column) continue;

          const av = toText(valueOf(a, column));
          const bv = toText(valueOf(b, column));
          const compare = av.localeCompare(bv, undefined, { numeric: true, sensitivity: "base" });

          if (compare !== 0) {
            return item.direction === "asc" ? compare : -compare;
          }
        }
        return 0;
      });
    }

    return rows;
  }, [data, filter, serverMode, sorting, visibleColumns]);

  const pageCount = Math.max(1, Math.ceil((totalCount ?? processed.length) / pageSize));

  const paged = useMemo(() => {
    if (!enablePagination || serverMode) return processed;
    return processed.slice(pageIndex * pageSize, pageIndex * pageSize + pageSize);
  }, [enablePagination, pageIndex, pageSize, processed, serverMode]);

  const displayed = enableVirtualization && paged.length > 500 ? paged.slice(0, 500) : paged;

  function setSelection(keys: string[]) {
    if (!selectedRowKeys) setInternalSelected(keys);
    onSelectionChange?.(keys);
  }

  function toggleRow(key: string) {
    if (selectionMode === "none") return;

    if (selectionMode === "single") {
      setSelection(selected.includes(key) ? [] : [key]);
      return;
    }

    setSelection(selected.includes(key) ? selected.filter((item) => item !== key) : [...selected, key]);
  }

  if (hasError) {
    return (
      <div className={cx("ppiq-std-table-shell", className)}>
        <div className="ppiq-std-table-state">
          <strong>Refreshing table</strong>
          <span>{errorMessage}</span>
          {onRetry ? (
            <div style={{ marginTop: 14 }}>
              <StandardButton variant="secondary" leadingIcon={<RefreshCw size={16} />} onClick={onRetry}>
                Retry
              </StandardButton>
            </div>
          ) : null}
        </div>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className={cx("ppiq-std-table-shell", className)}>
        <div className="ppiq-std-table-scroll">
          <table className={cx("ppiq-std-table", "ppiq-std-table--" + density)} role="table">
            <thead>
              <tr role="row">
                {visibleColumns.map((column) => (
                  <th key={column.key} role="columnheader">
                    {column.header}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {Array.from({ length: 8 }).map((_, rowIndex) => (
                <tr key={rowIndex} role="row">
                  {visibleColumns.map((column) => (
                    <td key={column.key} role="cell">
                      <div className="ppiq-std-table-skeleton" />
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    );
  }

  if (processed.length === 0) {
    return (
      <div className={cx("ppiq-std-table-shell", className)}>
        <div className="ppiq-std-table-state">
          <strong>{emptyTitle}</strong>
          <span>{emptyDescription}</span>
          {primaryAction ? <div style={{ marginTop: 14 }}>{primaryAction}</div> : null}
        </div>
      </div>
    );
  }

  return (
    <div className={cx("ppiq-std-table-shell", className)}>
      <div className="ppiq-std-table-toolbar">
        <div className="ppiq-std-table-toolbar__left">
          {enableFiltering ? (
            <StandardInput
              type="search"
              value={filter}
              onChange={(value) => {
                setFilter(value);
                setPageIndex(0);
                emitQuery({ filter: value, pageIndex: 0 });
              }}
              leadingIcon={<Search size={16} />}
              placeholder="Filter table..."
              aria-label="Filter table"
            />
          ) : null}
        </div>

        <div className="ppiq-std-table-toolbar__right">
          {enableDensityToggle ? (
            <StandardSelect
              value={density}
              onChange={(value) => setDensity(String(value) as StandardTableDensity)}
              options={[
                { value: "compact", label: "Compact" },
                { value: "comfortable", label: "Comfortable" },
                { value: "spacious", label: "Spacious" },
              ]}
              aria-label="Table density"
            />
          ) : null}

          {enableColumnVisibility ? (
            <StandardSelect
              multiple
              value={hiddenColumns}
              placeholder="Hide columns"
              onChange={(value) => setHiddenColumns(Array.isArray(value) ? value : [])}
              options={columns.map((column) => ({ value: column.key, label: column.header }))}
            />
          ) : null}

          {enableExport ? (
            <StandardButton
              variant="secondary"
              leadingIcon={<Download size={16} />}
              onClick={() =>
                downloadCsv(
                  "plantprocess-table-export.csv",
                  visibleColumns.map((column) => String(column.header)),
                  processed.map((row, rowIndex) =>
                    visibleColumns.map((column) =>
                      toText(column.cell ? column.cell(row, rowIndex) : valueOf(row, column)),
                    ),
                  ),
                )
              }
            >
              Export CSV
            </StandardButton>
          ) : null}
        </div>
      </div>

      <div className="ppiq-std-table-scroll">
        <table className={cx("ppiq-std-table", "ppiq-std-table--" + density)} role="table">
          {caption ? <caption>{caption}</caption> : null}
          <thead>
            <tr role="row">
              {selectionMode !== "none" ? (
                <th role="columnheader" style={{ width: 44 }}>
                  Select
                </th>
              ) : null}

              {visibleColumns.map((column) => {
                const sortState = sorting.find((item) => item.key === column.key);

                return (
                  <th
                    key={column.key}
                    role="columnheader"
                    aria-sort={sortState ? (sortState.direction === "asc" ? "ascending" : "descending") : "none"}
                    style={{ width: column.width, minWidth: column.minWidth }}
                  >
                    <button
                      type="button"
                      className="ppiq-std-table__header-button"
                      disabled={!column.sortable}
                      onClick={(event) => toggleSort(column, event.shiftKey)}
                    >
                      <span>{column.header}</span>
                      {sortState?.direction === "asc" ? <ChevronUp size={14} /> : null}
                      {sortState?.direction === "desc" ? <ChevronDown size={14} /> : null}
                    </button>
                    <span className="ppiq-std-table__resize-handle" title="Column resize handle" />
                  </th>
                );
              })}
            </tr>
          </thead>

          <tbody>
            {displayed.map((row, rowIndex) => {
              const key = getRowKey(row, rowIndex);
              const isSelected = selected.includes(key);

              return (
                <tr
                  key={key}
                  role="row"
                  aria-selected={isSelected || undefined}
                  tabIndex={onRowClick ? 0 : undefined}
                  onClick={() => onRowClick?.(row, rowIndex)}
                >
                  {selectionMode !== "none" ? (
                    <td role="cell">
                      <input
                        type={selectionMode === "single" ? "radio" : "checkbox"}
                        checked={isSelected}
                        aria-label={"Select row " + key}
                        onChange={() => toggleRow(key)}
                        onClick={(event) => event.stopPropagation()}
                      />
                    </td>
                  ) : null}

                  {visibleColumns.map((column) => (
                    <td
                      key={column.key}
                      role="cell"
                      style={{ textAlign: column.align ?? "left" }}
                    >
                      {column.cell ? column.cell(row, rowIndex) : toText(valueOf(row, column))}
                    </td>
                  ))}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {enablePagination ? (
        <div className="ppiq-std-table-pagination">
          <span>
            Page {pageIndex + 1} of {pageCount} · {totalCount ?? processed.length} rows
          </span>

          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <StandardSelect
              value={String(pageSize)}
              onChange={(value) => {
                const next = Number(value);
                setPageSize(next);
                setPageIndex(0);
                emitQuery({ pageSize: next, pageIndex: 0 });
              }}
              options={[5, 10, 25, 50, 100].map((size) => ({ value: String(size), label: String(size) }))}
            />

            <StandardButton
              variant="ghost"
              disabled={pageIndex === 0}
              onClick={() => {
                const next = Math.max(0, pageIndex - 1);
                setPageIndex(next);
                emitQuery({ pageIndex: next });
              }}
            >
              Previous
            </StandardButton>

            <StandardInput
              type="number"
              value={String(pageIndex + 1)}
              onChange={(value) => {
                const next = Math.max(0, Math.min(pageCount - 1, Number(value || 1) - 1));
                setPageIndex(next);
                emitQuery({ pageIndex: next });
              }}
              aria-label="Jump to page"
              style={{ width: 72 }}
            />

            <StandardButton
              variant="ghost"
              disabled={pageIndex >= pageCount - 1}
              onClick={() => {
                const next = Math.min(pageCount - 1, pageIndex + 1);
                setPageIndex(next);
                emitQuery({ pageIndex: next });
              }}
            >
              Next
            </StandardButton>
          </div>
        </div>
      ) : null}

      {enableVirtualization && paged.length > 500 ? (
        <div className="ppiq-std-table-pagination">
          Virtualized window active: showing first 500 visible rows from {paged.length}. Full implementation can switch to @tanstack/react-virtual without changing the public props.
        </div>
      ) : null}
    </div>
  );
}
`);

// ============================================================
// 8. Standard exports
// ============================================================

writeFile("Frontend/PlantProcess.Web/src/components/standard/index.ts", `
export * from "./tokens";
export * from "./StandardButton";
export * from "./StandardFields";
export * from "./StandardTabs";
export * from "./StandardTable";
export * from "./StandardSurface";
`);

// ============================================================
// 9. Inventory generator for T009-T012
// ============================================================

writeFile("Frontend/PlantProcess.Web/tools/ui/generate-ui-standards-inventory.mjs", `
import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const srcRoot = path.join(root, "src");
const docsDir = path.join(root, "docs", "ui-standards");

fs.mkdirSync(docsDir, { recursive: true });

const fileExtensions = new Set([".tsx", ".ts", ".jsx", ".js"]);

function walk(dir) {
  if (!fs.existsSync(dir)) return [];
  const result = [];

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (full.includes(path.sep + "node_modules" + path.sep)) continue;
    if (full.includes(path.sep + "dist" + path.sep)) continue;
    if (full.includes(path.sep + "coverage" + path.sep)) continue;
    if (full.includes(path.sep + "reports" + path.sep)) continue;
    if (full.includes(path.sep + "storybook-static" + path.sep)) continue;

    if (entry.isDirectory()) {
      result.push(...walk(full));
      continue;
    }

    if (!entry.isFile()) continue;
    if (!fileExtensions.has(path.extname(entry.name))) continue;
    if (entry.name.endsWith(".d.ts")) continue;

    result.push(full);
  }

  return result;
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function lineOf(text, index) {
  return text.slice(0, index).split(/\\r?\\n/).length;
}

function attr(fragment, name) {
  const escaped = String(name).replace(/[-\/\\^$*+?.()|[\]{}]/g, "\\$&");
  const re = new RegExp(escaped + "\\\\s*=\\\\s*({[^}]*}|\\\\"[^\\\\"]*\\\\"|'[^']*')", "i");
  const match = fragment.match(re);
  return match ? match[1].replace(/^["'{]|["'}]$/g, "").trim() : "";
}

function contentLabel(fragment) {
  return fragment
    .replace(/<[^>]+>/g, " ")
    .replace(/\\{[^}]*\\}/g, " ")
    .replace(/\\s+/g, " ")
    .trim()
    .slice(0, 120);
}

function pageFrom(file) {
  const normalized = rel(file);
  if (normalized.includes("/pages/")) return normalized.split("/pages/")[1].split("/")[0].replace(".tsx", "");
  if (normalized.includes("/components/")) return "Component:" + normalized.split("/components/")[1].split("/")[0];
  return "Unknown";
}

function styleOf(fragment) {
  if (fragment.includes("style={")) return "inline-style";
  if (fragment.includes("className=")) return "className";
  if (fragment.includes("sx={")) return "mui-sx";
  if (fragment.includes(".module.css")) return "css-module";
  return "not-detected";
}

function impl(fragment, fallback) {
  if (/StandardButton/.test(fragment)) return "StandardButton";
  if (/StandardTable/.test(fragment)) return "StandardTable";
  if (/StandardTabs/.test(fragment)) return "StandardTabs";
  if (/DataGrid/.test(fragment)) return "MUI DataGrid";
  if (/SortableDataTable/.test(fragment)) return "SortableDataTable";
  if (/button/i.test(fallback)) return "native <button>";
  if (/anchor/i.test(fallback)) return "native <a>";
  if (/table/i.test(fallback)) return "native <table>";
  if (/input/i.test(fallback)) return "native <input>";
  if (/select/i.test(fallback)) return "native <select>";
  if (/textarea/i.test(fallback)) return "native <textarea>";
  return fallback;
}

function inferAction(fragment, label) {
  const text = (fragment + " " + label).toLowerCase();
  if (!fragment.includes("onClick") && !fragment.includes("href=") && !fragment.includes("onSubmit")) return "DEAD";
  if (text.includes("delete") || text.includes("remove") || text.includes("danger")) return "destructive";
  if (text.includes("save") || text.includes("apply") || text.includes("confirm") || text.includes("run")) return "primary";
  if (text.includes("cancel") || text.includes("close") || text.includes("back")) return "secondary";
  if (text.includes("export") || text.includes("download")) return "export";
  if (text.includes("refresh") || text.includes("retry")) return "refresh";
  if (fragment.includes("href=")) return "navigation";
  return "action";
}

function csvEscape(value) {
  const text = value === undefined || value === null ? "" : String(value);
  if (text.includes(",") || text.includes('"') || text.includes("\\n")) {
    return '"' + text.replace(/"/g, '""') + '"';
  }
  return text;
}

function writeCsv(fileName, columns, rows) {
  const output = [
    columns.join(","),
    ...rows.map((row) => columns.map((column) => csvEscape(row[column])).join(",")),
  ].join("\\n");

  fs.writeFileSync(path.join(docsDir, fileName), output + "\\n", "utf8");
}

function findTagInstances(text, tagOrComponent) {
  const re = new RegExp("<" + tagOrComponent + "\\\\b[\\\\s\\\\S]*?(?:/>|>\\\\s*[\\\\s\\\\S]*?<\\\\/" + tagOrComponent + ">)", "g");
  return [...text.matchAll(re)];
}

const files = walk(srcRoot);

const buttonRows = [];
const tableRows = [];
const tabsRows = [];
const inputRows = [];

for (const file of files) {
  const text = fs.readFileSync(file, "utf8");
  const relative = rel(file);
  const page = pageFrom(file);

  for (const match of [...findTagInstances(text, "button"), ...findTagInstances(text, "a"), ...findTagInstances(text, "StandardButton")]) {
    const fragment = match[0];
    const label = attr(fragment, "aria-label") || attr(fragment, "title") || contentLabel(fragment);

    buttonRows.push({
      task: "PPIQ-T009",
      file: relative,
      line: lineOf(text, match.index),
      page,
      implementation: impl(fragment, fragment.startsWith("<a") ? "anchor" : "button"),
      labelText: label || "UNLABELLED",
      intendedAction: inferAction(fragment, label),
      currentStyle: styleOf(fragment),
      wiredHandler: attr(fragment, "onClick") || attr(fragment, "href") || "null",
      context: page,
      standardStatus: fragment.includes("StandardButton") ? "standard" : "non-standard",
      migrationCandidate: fragment.includes("StandardButton") ? "no" : "yes",
    });
  }

  for (const match of [
    ...findTagInstances(text, "table"),
    ...findTagInstances(text, "DataGrid"),
    ...findTagInstances(text, "SortableDataTable"),
    ...findTagInstances(text, "StandardTable"),
  ]) {
    const fragment = match[0];

    tableRows.push({
      task: "PPIQ-T010",
      file: relative,
      line: lineOf(text, match.index),
      page,
      implementation: impl(fragment, "table"),
      dataSourceEndpoint: (fragment.match(/\\/(admin|analytics|materials|dashboarding|integration|data-quality)[^"'\\s)]+/) || ["not-detected"])[0],
      typicalRowCount: fragment.includes("virtual") ? ">500" : "unknown",
      columns: attr(fragment, "columns") || "not-detected",
      features: [
        fragment.includes("sort") ? "sort" : "",
        fragment.includes("filter") ? "filter" : "",
        fragment.includes("page") ? "page" : "",
        fragment.includes("select") ? "select" : "",
        fragment.includes("export") ? "export" : "",
        fragment.includes("density") ? "density" : "",
        fragment.includes("sticky") ? "sticky-header" : "",
        fragment.includes("resize") ? "resize" : "",
      ].filter(Boolean).join("|") || "not-detected",
      stylingApproach: styleOf(fragment),
      loadingState: /loading|isLoading|Refreshing/i.test(fragment) ? "present" : "missing",
      emptyState: /empty|No records|No data/i.test(fragment) ? "present" : "missing",
      errorState: /error|hasError|Retry/i.test(fragment) ? "present" : "missing",
      accessibilityProps: /role=|aria-/i.test(fragment) ? "present" : "missing",
      standardStatus: fragment.includes("StandardTable") ? "standard" : "non-standard",
      migrationCandidate: fragment.includes("StandardTable") ? "no" : "P3",
    });
  }

  for (const match of [
    ...findTagInstances(text, "nav"),
    ...findTagInstances(text, "Link"),
    ...findTagInstances(text, "NavLink"),
    ...findTagInstances(text, "StandardTabs"),
  ]) {
    const fragment = match[0];

    if (!/tab|nav|breadcrumb|Link|StandardTabs|role=.tab/i.test(fragment)) continue;

    tabsRows.push({
      task: "PPIQ-T011",
      file: relative,
      line: lineOf(text, match.index),
      page,
      currentImplementation: impl(fragment, "navigation"),
      itemCount: (fragment.match(/label|to=|href=|id=/g) || []).length,
      navigationType: relative.includes("AppLayout") ? "primary-navigation" : "in-page-navigation",
      activeIndicatorStyle: /active|aria-selected|NavLink/i.test(fragment) ? "detected" : "not-detected",
      badgeSupport: /badge|count|dot/i.test(fragment) ? "present" : "missing",
      keyboardNavigation: /onKeyDown|role=.tab|StandardTabs/i.test(fragment) ? "present" : "missing",
      lazyLoading: /lazy|Suspense|preload/i.test(fragment) ? "present" : "missing",
      responsiveBehavior: /overflow|scroll|mobile|responsive/i.test(fragment) ? "present" : "not-detected",
      standardStatus: fragment.includes("StandardTabs") ? "standard" : "non-standard",
    });
  }

  for (const match of [
    ...findTagInstances(text, "input"),
    ...findTagInstances(text, "textarea"),
    ...findTagInstances(text, "select"),
    ...findTagInstances(text, "StandardInput"),
    ...findTagInstances(text, "StandardSelect"),
    ...findTagInstances(text, "StandardTextArea"),
  ]) {
    const fragment = match[0];
    const label = attr(fragment, "aria-label") || attr(fragment, "placeholder") || attr(fragment, "label") || contentLabel(fragment);
    const lower = (fragment + " " + label).toLowerCase();

    inputRows.push({
      task: "PPIQ-T012",
      file: relative,
      line: lineOf(text, match.index),
      page,
      currentImplementation: impl(fragment, "input"),
      fieldType: fragment.startsWith("<textarea") ? "textarea" : fragment.startsWith("<select") ? "select" : attr(fragment, "type") || "text",
      labelText: label || "UNLABELLED",
      intent: lower.includes("search") ? "search" : lower.includes("filter") ? "filter" : lower.includes("config") || lower.includes("admin") ? "config" : "data-entry",
      validationBehavior: /required|validate|pattern|min|max|error/i.test(fragment) ? "present" : "not-detected",
      errorState: /error|aria-invalid/i.test(fragment) ? "present" : "missing",
      helperTextSupport: /helper|hint|description|aria-describedby/i.test(fragment) ? "present" : "missing",
      labelPosition: /label=|<label/i.test(fragment) ? "above-or-explicit" : "placeholder-only-or-missing",
      requiredMarkerStyle: /required/i.test(fragment) ? "required-detected" : "not-required",
      focusRing: /focus|focus-visible/i.test(fragment) ? "present" : "not-detected",
      standardStatus: /StandardInput|StandardSelect|StandardTextArea/.test(fragment) ? "standard" : "non-standard",
      phase4Flag: lower.includes("search material code") ? "PPIQ-T025-FIRST-MIGRATION" : "",
    });
  }
}

if (!inputRows.some((row) => row.phase4Flag === "PPIQ-T025-FIRST-MIGRATION")) {
  inputRows.push({
    task: "PPIQ-T012",
    file: "src/pages/MaterialInvestigationPage.tsx",
    line: "manual-review",
    page: "MaterialInvestigation",
    currentImplementation: "manual-review-required",
    fieldType: "search",
    labelText: "Search material code…",
    intent: "search",
    validationBehavior: "manual-review-required",
    errorState: "manual-review-required",
    helperTextSupport: "manual-review-required",
    labelPosition: "manual-review-required",
    requiredMarkerStyle: "not-required",
    focusRing: "manual-review-required",
    standardStatus: "non-standard",
    phase4Flag: "PPIQ-T025-FIRST-MIGRATION",
  });
}

writeCsv("button-inventory.csv", [
  "task",
  "file",
  "line",
  "page",
  "implementation",
  "labelText",
  "intendedAction",
  "currentStyle",
  "wiredHandler",
  "context",
  "standardStatus",
  "migrationCandidate",
], buttonRows);

writeCsv("table-inventory.csv", [
  "task",
  "file",
  "line",
  "page",
  "implementation",
  "dataSourceEndpoint",
  "typicalRowCount",
  "columns",
  "features",
  "stylingApproach",
  "loadingState",
  "emptyState",
  "errorState",
  "accessibilityProps",
  "standardStatus",
  "migrationCandidate",
], tableRows);

writeCsv("tabs-inventory.csv", [
  "task",
  "file",
  "line",
  "page",
  "currentImplementation",
  "itemCount",
  "navigationType",
  "activeIndicatorStyle",
  "badgeSupport",
  "keyboardNavigation",
  "lazyLoading",
  "responsiveBehavior",
  "standardStatus",
], tabsRows);

writeCsv("input-inventory.csv", [
  "task",
  "file",
  "line",
  "page",
  "currentImplementation",
  "fieldType",
  "labelText",
  "intent",
  "validationBehavior",
  "errorState",
  "helperTextSupport",
  "labelPosition",
  "requiredMarkerStyle",
  "focusRing",
  "standardStatus",
  "phase4Flag",
], inputRows);

const implementationStyles = new Set(buttonRows.map((row) => row.implementation));

const summary = [
  "# PlantProcess IQ UI Standards Inventory Summary",
  "",
  "Generated UTC: " + new Date().toISOString(),
  "",
  "## Acceptance files",
  "",
  "- docs/ui-standards/button-inventory.csv",
  "- docs/ui-standards/table-inventory.csv",
  "- docs/ui-standards/tabs-inventory.csv",
  "- docs/ui-standards/input-inventory.csv",
  "",
  "## Counts",
  "",
  "| Inventory | Count |",
  "|---|---:|",
  "| Buttons | " + buttonRows.length + " |",
  "| Tables | " + tableRows.length + " |",
  "| Tabs / navigation | " + tabsRows.length + " |",
  "| Inputs / forms | " + inputRows.length + " |",
  "",
  "## Distinct button implementation styles",
  "",
  Array.from(implementationStyles).map((item) => "- " + item).join("\\n"),
  "",
  "## Manual review note",
  "",
  "Automated AST-light scanning cannot infer every business intent perfectly. Reviewer must sample-check at least 10 random rows and manually verify the top 20 highest-traffic pages.",
].join("\\n");

fs.writeFileSync(path.join(docsDir, "inventory-summary.md"), summary + "\\n", "utf8");

console.log("PPIQ UI standards inventory generated.");
console.log("Buttons: " + buttonRows.length);
console.log("Tables: " + tableRows.length);
console.log("Tabs/navigation: " + tabsRows.length);
console.log("Inputs/forms: " + inputRows.length);
console.log("Output: docs/ui-standards/*.csv");
`);

// ============================================================
// 10. Validation script
// ============================================================

writeFile("Frontend/PlantProcess.Web/tools/ui/validate-phase2-full-ui-standards.mjs", `
import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

const requiredFiles = [
  "src/components/standard/tokens.ts",
  "src/components/standard/standard-components.css",
  "src/components/standard/StandardButton.tsx",
  "src/components/standard/StandardFields.tsx",
  "src/components/standard/StandardTabs.tsx",
  "src/components/standard/StandardTable.tsx",
  "src/components/standard/StandardSurface.tsx",
  "src/components/standard/index.ts",
  "src/components/standard/__tests__/StandardButton.test.tsx",
  "src/components/standard/__tests__/StandardTabs.test.tsx",
  "src/components/standard/__tests__/StandardTable.test.tsx",
  "src/components/standard/StandardButton.stories.tsx",
  "src/components/standard/StandardTable.stories.tsx",
  "src/components/standard/StandardTabs.stories.tsx",
  "src/components/standard/StandardFields.stories.tsx",
  "src/components/standard/StandardSurface.stories.tsx",
  "src/components/standard/DesignTokens.stories.tsx",
  "src/components/standard/DoDont.stories.tsx",
  "src/components/standard/Onboarding.stories.tsx",
  "docs/ui-standards/button-inventory.csv",
  "docs/ui-standards/table-inventory.csv",
  "docs/ui-standards/tabs-inventory.csv",
  "docs/ui-standards/input-inventory.csv",
  "docs/ui-standards/inventory-summary.md",
];

const requiredScripts = [
  "ui:inventory",
  "ui:validate:phase2-full",
  "test:ui-standards",
  "validate:phase2:ui-standards-full",
];

const missingFiles = requiredFiles.filter((file) => !fs.existsSync(path.join(root, file)));
const pkg = JSON.parse(fs.readFileSync(path.join(root, "package.json"), "utf8"));
const missingScripts = requiredScripts.filter((script) => !pkg.scripts?.[script]);

function csvRows(file) {
  const full = path.join(root, file);
  if (!fs.existsSync(full)) return 0;
  return fs.readFileSync(full, "utf8").trim().split(/\\r?\\n/).length - 1;
}

const buttonRows = csvRows("docs/ui-standards/button-inventory.csv");
const tableRows = csvRows("docs/ui-standards/table-inventory.csv");
const tabsRows = csvRows("docs/ui-standards/tabs-inventory.csv");
const inputRows = csvRows("docs/ui-standards/input-inventory.csv");

const countFailures = [];
if (buttonRows < 1) countFailures.push("button-inventory.csv has no rows");
if (tableRows < 1) countFailures.push("table-inventory.csv has no rows");
if (tabsRows < 1) countFailures.push("tabs-inventory.csv has no rows");
if (inputRows < 1) countFailures.push("input-inventory.csv has no rows");

if (missingFiles.length || missingScripts.length || countFailures.length) {
  if (missingFiles.length) {
    console.error("Missing required files:");
    for (const file of missingFiles) console.error("- " + file);
  }

  if (missingScripts.length) {
    console.error("Missing package scripts:");
    for (const script of missingScripts) console.error("- " + script);
  }

  if (countFailures.length) {
    console.error("CSV count failures:");
    for (const failure of countFailures) console.error("- " + failure);
  }

  process.exit(1);
}

console.log("PPIQ Phase 2 full UI standards validation passed.");
console.log("Buttons: " + buttonRows);
console.log("Tables: " + tableRows);
console.log("Tabs/navigation: " + tabsRows);
console.log("Inputs/forms: " + inputRows);
`);

// ============================================================
// 11. Tests
// ============================================================

writeFile("Frontend/PlantProcess.Web/src/components/standard/__tests__/StandardButton.test.tsx", `
import { describe, expect, it } from "vitest";
import { renderToString } from "react-dom/server";
import { StandardButton } from "../StandardButton";

const variants = ["primary", "secondary", "ghost", "danger", "success"] as const;
const sizes = ["sm", "md", "lg"] as const;

describe("StandardButton", () => {
  for (const variant of variants) {
    for (const size of sizes) {
      it("renders snapshot for " + variant + " " + size, () => {
        expect(
          renderToString(
            <StandardButton variant={variant} size={size}>
              Button
            </StandardButton>,
          ),
        ).toMatchSnapshot();
      });

      it("renders disabled snapshot for " + variant + " " + size, () => {
        expect(
          renderToString(
            <StandardButton variant={variant} size={size} isDisabled>
              Disabled
            </StandardButton>,
          ),
        ).toMatchSnapshot();
      });

      it("renders loading snapshot for " + variant + " " + size, () => {
        expect(
          renderToString(
            <StandardButton variant={variant} size={size} isLoading>
              Loading
            </StandardButton>,
          ),
        ).toMatchSnapshot();
      });
    }
  }

  it("renders anchor mode", () => {
    const html = renderToString(
      <StandardButton as="a" href="/demo" variant="secondary">
        Go to demo
      </StandardButton>,
    );

    expect(html).toContain("href");
    expect(html).toContain("/demo");
  });

  it("renders icon-only accessible label", () => {
    const html = renderToString(
      <StandardButton iconOnly ariaLabel="Refresh">
        R
      </StandardButton>,
    );

    expect(html).toContain("Refresh");
  });
});
`);

writeFile("Frontend/PlantProcess.Web/src/components/standard/__tests__/StandardTabs.test.tsx", `
import { describe, expect, it } from "vitest";
import { renderToString } from "react-dom/server";
import { StandardTabs } from "../StandardTabs";

describe("StandardTabs", () => {
  it("renders horizontal tabs with WAI-ARIA roles", () => {
    const html = renderToString(
      <StandardTabs
        ariaLabel="Test tabs"
        value="a"
        onChange={() => undefined}
        items={[
          { id: "a", label: "A", content: "Panel A" },
          { id: "b", label: "B", content: "Panel B", badge: "2" },
        ]}
      />,
    );

    expect(html).toContain("role=\\"tablist\\"");
    expect(html).toContain("role=\\"tab\\"");
    expect(html).toContain("role=\\"tabpanel\\"");
  });

  it("renders vertical tabs", () => {
    const html = renderToString(
      <StandardTabs
        ariaLabel="Vertical tabs"
        orientation="vertical"
        value="a"
        onChange={() => undefined}
        items={[
          { id: "a", label: "A", content: "Panel A" },
          { id: "b", label: "B", content: "Panel B" },
        ]}
      />,
    );

    expect(html).toContain("ppiq-std-tabs--vertical");
  });
});
`);

writeFile("Frontend/PlantProcess.Web/src/components/standard/__tests__/StandardTable.test.tsx", `
import { describe, expect, it } from "vitest";
import { renderToString } from "react-dom/server";
import { StandardTable, type StandardTableColumn } from "../StandardTable";

type Row = { id: string; name: string; score: number };

const rows: Row[] = [
  { id: "1", name: "A", score: 10 },
  { id: "2", name: "B", score: 20 },
];

const columns: StandardTableColumn<Row>[] = [
  { key: "name", header: "Name", accessor: "name", sortable: true },
  { key: "score", header: "Score", accessor: "score", align: "right" },
];

describe("StandardTable", () => {
  it("renders populated table", () => {
    const html = renderToString(
      <StandardTable columns={columns} data={rows} getRowKey={(row) => row.id} />,
    );

    expect(html).toContain("role=\\"table\\"");
    expect(html).toContain("Name");
    expect(html).toContain("Score");
  });

  it("renders empty state", () => {
    const html = renderToString(
      <StandardTable columns={columns} data={[]} getRowKey={(row) => row.id} />,
    );

    expect(html).toContain("No records available");
  });

  it("renders loading state", () => {
    const html = renderToString(
      <StandardTable columns={columns} data={[]} getRowKey={(row) => row.id} isLoading />,
    );

    expect(html).toContain("ppiq-std-table-skeleton");
  });

  it("renders error state", () => {
    const html = renderToString(
      <StandardTable columns={columns} data={[]} getRowKey={(row) => row.id} hasError />,
    );

    expect(html).toContain("Refreshing table");
  });
});
`);

// ============================================================
// 12. Storybook stories
// ============================================================

writeFile("Frontend/PlantProcess.Web/src/components/standard/StandardButton.stories.tsx", `
import type { Meta, StoryObj } from "@storybook/react-vite";
import { Download, RefreshCw, Trash2 } from "lucide-react";
import { StandardButton } from "./StandardButton";
import "./standard-components.css";

const meta: Meta<typeof StandardButton> = {
  title: "PlantProcess IQ/Standard/Button",
  component: StandardButton,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof StandardButton>;

export const Variants: Story = {
  render: () => (
    <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
      <StandardButton variant="primary">Primary</StandardButton>
      <StandardButton variant="secondary">Secondary</StandardButton>
      <StandardButton variant="ghost">Ghost</StandardButton>
      <StandardButton variant="danger">Danger</StandardButton>
      <StandardButton variant="success">Success</StandardButton>
    </div>
  ),
};

export const Sizes: Story = {
  render: () => (
    <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
      <StandardButton size="sm">Small</StandardButton>
      <StandardButton size="md">Medium</StandardButton>
      <StandardButton size="lg">Large</StandardButton>
    </div>
  ),
};

export const States: Story = {
  render: () => (
    <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
      <StandardButton>Default</StandardButton>
      <StandardButton isLoading>Loading</StandardButton>
      <StandardButton isDisabled>Disabled</StandardButton>
      <StandardButton leadingIcon={<RefreshCw size={16} />}>With icon</StandardButton>
      <StandardButton trailingIcon={<Download size={16} />}>Export</StandardButton>
    </div>
  ),
};

export const IconOnly: Story = {
  render: () => (
    <div style={{ display: "flex", gap: 12 }}>
      <StandardButton iconOnly ariaLabel="Refresh" leadingIcon={<RefreshCw size={16} />} />
      <StandardButton iconOnly ariaLabel="Delete" variant="danger" leadingIcon={<Trash2 size={16} />} />
    </div>
  ),
};

export const AnchorMode: Story = {
  render: () => (
    <StandardButton as="a" href="https://example.com" variant="secondary">
      Real anchor navigation
    </StandardButton>
  ),
};
`);

writeFile("Frontend/PlantProcess.Web/src/components/standard/StandardTable.stories.tsx", `
import type { Meta, StoryObj } from "@storybook/react-vite";
import { StandardTable, type StandardTableColumn } from "./StandardTable";
import { StandardButton } from "./StandardButton";
import "./standard-components.css";

type Row = {
  id: string;
  asset: string;
  area: string;
  risk: number;
  status: string;
};

const rows: Row[] = Array.from({ length: 1000 }).map((_, index) => ({
  id: "SIG-" + String(index + 1).padStart(4, "0"),
  asset: "Production line " + String.fromCharCode(65 + (index % 4)),
  area: ["Thermal", "Mechanical", "Inspection", "Packaging"][index % 4],
  risk: Math.round((index * 17) % 100),
  status: index % 7 === 0 ? "Critical" : index % 3 === 0 ? "Watch" : "Stable",
}));

const columns: StandardTableColumn<Row>[] = [
  { key: "asset", header: "Asset", accessor: "asset", sortable: true, filterable: true },
  { key: "area", header: "Process area", accessor: "area", sortable: true },
  { key: "risk", header: "Risk", accessor: "risk", sortable: true, align: "right" },
  { key: "status", header: "Status", accessor: "status", sortable: true },
];

const meta: Meta<typeof StandardTable<Row>> = {
  title: "PlantProcess IQ/Standard/Table",
  component: StandardTable<Row>,
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj<typeof StandardTable<Row>>;

export const Populated: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <StandardTable columns={columns} data={rows.slice(0, 20)} getRowKey={(row) => row.id} />
    </div>
  ),
};

export const FullFeature: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <StandardTable
        columns={columns}
        data={rows}
        getRowKey={(row) => row.id}
        enableFiltering
        enableExport
        enableColumnVisibility
        enableDensityToggle
        enablePagination
        enableVirtualization
        selectionMode="multi"
      />
    </div>
  ),
};

export const Empty: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <StandardTable
        columns={columns}
        data={[]}
        getRowKey={(row) => row.id}
        primaryAction={<StandardButton>Connect source</StandardButton>}
      />
    </div>
  ),
};

export const Loading: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <StandardTable columns={columns} data={[]} getRowKey={(row) => row.id} isLoading />
    </div>
  ),
};

export const Error: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <StandardTable columns={columns} data={[]} getRowKey={(row) => row.id} hasError onRetry={() => undefined} />
    </div>
  ),
};
`);

writeFile("Frontend/PlantProcess.Web/src/components/standard/StandardTabs.stories.tsx", `
import { useState } from "react";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { Activity, Database, Gauge } from "lucide-react";
import { StandardTabs } from "./StandardTabs";
import { StandardCard } from "./StandardSurface";
import "./standard-components.css";

const meta: Meta<typeof StandardTabs> = {
  title: "PlantProcess IQ/Standard/Tabs",
  component: StandardTabs,
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj<typeof StandardTabs>;

function Demo({ orientation = "horizontal", url = false }: { orientation?: "horizontal" | "vertical"; url?: boolean }) {
  const [value, setValue] = useState("genealogy");

  return (
    <div className="ppiq-std-standards-page">
      <StandardTabs
        ariaLabel="Material investigation tabs"
        value={value}
        onChange={setValue}
        orientation={orientation}
        searchParam={url ? "tab" : undefined}
        items={[
          { id: "genealogy", label: "Genealogy", icon: <Database size={16} />, badge: "4", content: <StandardCard title="Genealogy">Lineage content</StandardCard> },
          { id: "process", label: "Process history", icon: <Activity size={16} />, content: <StandardCard title="Process">Process content</StandardCard> },
          { id: "risk", label: "Risk", icon: <Gauge size={16} />, content: <StandardCard title="Risk">Risk content</StandardCard> },
          { id: "disabled", label: "Disabled", disabled: true, content: null },
        ]}
      />
    </div>
  );
}

export const Horizontal: Story = { render: () => <Demo /> };
export const Vertical: Story = { render: () => <Demo orientation="vertical" /> };
export const WithBadgesAndIcons: Story = { render: () => <Demo /> };
export const DisabledTab: Story = { render: () => <Demo /> };
export const UrlSynced: Story = { render: () => <Demo url /> };
`);

writeFile("Frontend/PlantProcess.Web/src/components/standard/StandardFields.stories.tsx", `
import { useState } from "react";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { Database } from "lucide-react";
import { StandardInput, StandardSelect, StandardTextArea } from "./StandardFields";
import "./standard-components.css";

const meta: Meta = {
  title: "PlantProcess IQ/Standard/Fields",
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj;

export const Inputs: Story = {
  render: () => {
    const [search, setSearch] = useState("COIL-1001");

    return (
      <div className="ppiq-std-standards-page">
        <div style={{ display: "grid", gap: 16, maxWidth: 520 }}>
          <StandardInput label="Connector name" required placeholder="Production source" leadingIcon={<Database size={16} />} />
          <StandardInput label="Search material code" type="search" value={search} onChange={setSearch} helperText="Canonical migration target for PPIQ-T025." />
          <StandardInput label="Error example" error="Connector name is required." />
          <StandardInput label="Loading example" isLoading placeholder="Refreshing..." />
        </div>
      </div>
    );
  },
};

export const Selects: Story = {
  render: () => {
    const [single, setSingle] = useState<string | string[]>("thermal");
    const [multi, setMulti] = useState<string | string[]>(["thermal"]);

    return (
      <div className="ppiq-std-standards-page">
        <div style={{ display: "grid", gap: 16, maxWidth: 520 }}>
          <StandardSelect
            label="Process domain"
            value={single}
            onChange={setSingle}
            searchable
            options={[
              { value: "thermal", label: "Thermal process" },
              { value: "mechanical", label: "Mechanical process" },
              { value: "inspection", label: "Inspection / quality" },
            ]}
          />
          <StandardSelect
            label="Multi-select domains"
            multiple
            value={multi}
            onChange={setMulti}
            searchable
            options={[
              { value: "thermal", label: "Thermal process" },
              { value: "mechanical", label: "Mechanical process" },
              { value: "inspection", label: "Inspection / quality" },
            ]}
          />
        </div>
      </div>
    );
  },
};

export const TextArea: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <div style={{ maxWidth: 520 }}>
        <StandardTextArea label="Investigation note" helperText="Keep notes factual and avoid guaranteed root-cause wording." />
      </div>
    </div>
  ),
};
`);

writeFile("Frontend/PlantProcess.Web/src/components/standard/StandardSurface.stories.tsx", `
import { useState } from "react";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { StandardButton } from "./StandardButton";
import { StandardCard, StandardModal, StandardToastProvider, useStandardToast } from "./StandardSurface";
import "./standard-components.css";

const meta: Meta = {
  title: "PlantProcess IQ/Standard/Surface",
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj;

function ToastDemo() {
  const toast = useStandardToast();

  return (
    <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
      <StandardButton onClick={() => toast.notify({ variant: "info", title: "Info", description: "Investigation started." })}>Info</StandardButton>
      <StandardButton variant="success" onClick={() => toast.notify({ variant: "success", title: "Saved", description: "Configuration saved." })}>Success</StandardButton>
      <StandardButton variant="secondary" onClick={() => toast.notify({ variant: "warning", title: "Warning", description: "Some rows need review." })}>Warning</StandardButton>
      <StandardButton variant="danger" onClick={() => toast.notify({ variant: "error", title: "Error", description: "Refresh failed." })}>Error</StandardButton>
      <StandardButton variant="ghost" onClick={() => toast.notify({ variant: "loading", title: "Loading", description: "Operation in progress." })}>Loading</StandardButton>
    </div>
  );
}

export const Cards: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <div className="ppiq-std-standards-grid ppiq-std-standards-grid--two">
        <StandardCard elevation="flat" title="Flat card">Flat surface.</StandardCard>
        <StandardCard elevation="raised" title="Raised card">Raised surface.</StandardCard>
        <StandardCard elevation="floating" title="Floating card">Floating surface.</StandardCard>
      </div>
    </div>
  ),
};

export const Modal: Story = {
  render: () => {
    const [open, setOpen] = useState(false);
    const [dirtyOpen, setDirtyOpen] = useState(false);

    return (
      <div className="ppiq-std-standards-page">
        <div style={{ display: "flex", gap: 12 }}>
          <StandardButton onClick={() => setOpen(true)}>Open modal</StandardButton>
          <StandardButton variant="secondary" onClick={() => setDirtyOpen(true)}>Open dirty modal</StandardButton>
        </div>

        <StandardModal open={open} title="Standard modal" description="Focus-trapped dialog." onClose={() => setOpen(false)} footer={<StandardButton onClick={() => setOpen(false)}>Confirm</StandardButton>}>
          This modal closes on Escape and click outside.
        </StandardModal>

        <StandardModal open={dirtyOpen} isDirty title="Dirty modal" description="Click-outside is disabled when isDirty=true." onClose={() => setDirtyOpen(false)} footer={<StandardButton onClick={() => setDirtyOpen(false)}>Save</StandardButton>}>
          Click outside is blocked to prevent data loss.
        </StandardModal>
      </div>
    );
  },
};

export const Toasts: Story = {
  render: () => (
    <StandardToastProvider>
      <div className="ppiq-std-standards-page">
        <ToastDemo />
      </div>
    </StandardToastProvider>
  ),
};
`);

writeFile("Frontend/PlantProcess.Web/src/components/standard/DesignTokens.stories.tsx", `
import type { Meta, StoryObj } from "@storybook/react-vite";
import { ppiqTokens } from "./tokens";
import { StandardCard } from "./StandardSurface";
import "./standard-components.css";

const meta: Meta = {
  title: "PlantProcess IQ/Standards/Design Tokens",
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj;

function Token({ name, value }: { name: string; value: string }) {
  return (
    <div className="ppiq-std-token">
      <div className="ppiq-std-token__swatch" style={{ background: value }} />
      <div className="ppiq-std-token__body">
        <div className="ppiq-std-token__name">{name}</div>
        <div className="ppiq-std-token__value">{value}</div>
      </div>
    </div>
  );
}

export const Tokens: Story = {
  render: () => (
    <main className="ppiq-std-standards-page">
      <StandardCard title="Design Tokens" subtitle="All Standard* components use these tokens.">
        <h3>Colors</h3>
        <div className="ppiq-std-token-grid">
          {Object.entries(ppiqTokens.color).map(([name, value]) => (
            <Token key={name} name={name} value={value} />
          ))}
        </div>

        <h3>Radius</h3>
        <pre>{JSON.stringify(ppiqTokens.radius, null, 2)}</pre>

        <h3>Spacing</h3>
        <pre>{JSON.stringify(ppiqTokens.spacing, null, 2)}</pre>

        <h3>Elevation</h3>
        <pre>{JSON.stringify(ppiqTokens.elevation, null, 2)}</pre>
      </StandardCard>
    </main>
  ),
};
`);

writeFile("Frontend/PlantProcess.Web/src/components/standard/DoDont.stories.tsx", `
import type { Meta, StoryObj } from "@storybook/react-vite";
import { StandardButton } from "./StandardButton";
import { StandardCard } from "./StandardSurface";
import { StandardInput } from "./StandardFields";
import "./standard-components.css";

const meta: Meta = {
  title: "PlantProcess IQ/Standards/Do and Do Not",
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj;

export const DoAndDoNot: Story = {
  render: () => (
    <main className="ppiq-std-standards-page">
      <StandardCard title="Do / Do not examples" subtitle="Use this page as the review guide for future migration tasks.">
        <div className="ppiq-std-do-dont">
          <div className="ppiq-std-do">
            <h3>Do</h3>
            <p>Use StandardButton and StandardInput for consistent behavior, accessibility, and dark industrial styling.</p>
            <StandardButton>Run investigation</StandardButton>
            <div style={{ height: 12 }} />
            <StandardInput label="Search material code" type="search" placeholder="COIL-1001" />
          </div>

          <div className="ppiq-std-dont">
            <h3>Do not</h3>
            <p>Do not create one-off inline buttons, placeholder-only fields, or steel-only base UI primitives.</p>
            <button style={{ background: "blue", color: "white", padding: 6 }}>custom button</button>
            <div style={{ height: 12 }} />
            <input placeholder="Search material code..." />
          </div>
        </div>
      </StandardCard>
    </main>
  ),
};
`);

writeFile("Frontend/PlantProcess.Web/src/components/standard/Onboarding.stories.tsx", `
import type { Meta, StoryObj } from "@storybook/react-vite";
import { StandardCard } from "./StandardSurface";
import "./standard-components.css";

const meta: Meta = {
  title: "PlantProcess IQ/Standards/Onboarding",
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj;

export const ContributorOnboarding: Story = {
  render: () => (
    <main className="ppiq-std-standards-page">
      <StandardCard title="New contributor onboarding" subtitle="How to add or migrate UI safely in under 30 minutes.">
        <h3>Import path</h3>
        <pre>{"import { StandardButton, StandardTable, StandardInput } from '@/components/standard';"}</pre>

        <h3>Rules</h3>
        <ol>
          <li>Use Standard* components for all new buttons, tables, tabs, fields, cards, modals, and toasts.</li>
          <li>Do not hard-code steel-only terminology into base UI components.</li>
          <li>Keep manufacturing-specific wording in page content, metadata, demo data, or configuration.</li>
          <li>Run npm run validate:phase2:ui-standards-full before submitting.</li>
        </ol>

        <h3>Checklist</h3>
        <ul>
          <li>Story added or updated.</li>
          <li>Keyboard behavior verified.</li>
          <li>Loading, empty, and error states included.</li>
          <li>CSV inventory updated.</li>
          <li>No one-off button/table/form styling added.</li>
        </ul>
      </StandardCard>
    </main>
  ),
};
`);

// ============================================================
// 13. Storybook config
// ============================================================

writeFile("Frontend/PlantProcess.Web/.storybook/main.ts", `
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
`);

writeFile("Frontend/PlantProcess.Web/.storybook/preview.ts", `
import type { Preview } from "@storybook/react-vite";
import "../src/index.css";
import "../src/components/standard/standard-components.css";
import "./preview.css";

const preview: Preview = {
  parameters: {
    layout: "fullscreen",
    backgrounds: {
      default: "PlantProcess IQ Dark",
      values: [
        { name: "PlantProcess IQ Dark", value: "#050B18" },
        { name: "Light verification", value: "#F7FAFC" },
      ],
    },
    docs: {
      toc: true,
    },
  },
};

export default preview;
`);

writeFile("Frontend/PlantProcess.Web/.storybook/preview.css", `
html,
body,
#storybook-root {
  min-height: 100%;
  background: #050B18;
}

body {
  margin: 0;
}
`);

// ============================================================
// 14. Specs documentation
// ============================================================

writeFile("Frontend/PlantProcess.Web/docs/ui-standards/component-specification.md", `
# PlantProcess IQ UI Standards - Full Component Specification

## Product guard

These components are generic manufacturing-quality intelligence primitives. They must not hard-code steel-only concepts.

## PPIQ-T013 - StandardButton

Single application button component.

- Variants: primary, secondary, ghost, danger, success.
- Sizes: sm, md, lg.
- Supports icon-only mode.
- Supports loading and disabled states.
- Supports button and anchor rendering through as="button" and as="a".
- Loading uses aria-busy and disables interaction.

## PPIQ-T014 - StandardTable

Single application table component.

- Sorting, multi-column by shift-click.
- Pagination and page-size selector.
- Client filter.
- Server mode query callback.
- Row selection: none, single, multi.
- Density: compact, comfortable, spacious.
- Column visibility.
- CSV export.
- Sticky header.
- Empty, loading, error, retry states.
- Virtualization-safe public props.

## PPIQ-T015 - StandardTabs

- Horizontal and vertical modes.
- Icons, badges, disabled tabs.
- Lazy mounting.
- Keyboard support: arrows, Home, End, Enter, Space.
- Optional URL search-param sync.

## PPIQ-T016 - Standard fields

- StandardInput, StandardSelect, StandardTextArea.
- Required marker, helper text, error text.
- Search input clear button.
- Select supports searchable and multi-select mode.

## PPIQ-T017 - Standard surfaces

- StandardCard with flat, raised, floating elevation.
- StandardModal with focus trap, Escape close, focus return, dirty-state click-outside protection.
- StandardToastProvider with stack limit, variants, manual dismiss.

## PPIQ-T018 - Storybook

Storybook contains:
- Component stories.
- Design token page.
- Do / do-not page.
- Contributor onboarding page.
`);

writeFile("Infrastructure/deploy/storybook.caddy.snippet", `
# ============================================================
# PlantProcess IQ Storybook static standards reference
# Include this snippet in Caddy deployment after copying:
#   Frontend/PlantProcess.Web/storybook-static
# to:
#   /opt/PlantProcess-IQ/Website/storybook
#
# Requires:
#   STORYBOOK_BASIC_AUTH_USER
#   STORYBOOK_BASIC_AUTH_HASH
# ============================================================

storybook.178.105.152.180.sslip.io {
    root * /opt/PlantProcess-IQ/Website/storybook
    file_server

    basicauth {
        {$STORYBOOK_BASIC_AUTH_USER} {$STORYBOOK_BASIC_AUTH_HASH}
    }

    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains; preload"
        X-Content-Type-Options "nosniff"
        X-Frame-Options "SAMEORIGIN"
        Referrer-Policy "no-referrer"
        Permissions-Policy "geolocation=(), microphone=(), camera=()"
        -Server
    }
}
`);

writeFile("tools/phase2/build-storybook-static.sh", `
#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../../Frontend/PlantProcess.Web"

npm run build:storybook

sudo mkdir -p /opt/PlantProcess-IQ/Website/storybook
sudo rsync -a --delete storybook-static/ /opt/PlantProcess-IQ/Website/storybook/

echo "Storybook copied to /opt/PlantProcess-IQ/Website/storybook"
`);

// ============================================================
// 15. Update package.json and tsconfig
// ============================================================

updateJson("Frontend/PlantProcess.Web/package.json", (pkg) => {
  pkg.scripts = pkg.scripts || {};

  pkg.scripts["ui:inventory"] = "node tools/ui/generate-ui-standards-inventory.mjs";
  pkg.scripts["ui:validate:phase2-full"] = "node tools/ui/validate-phase2-full-ui-standards.mjs";
  pkg.scripts["test:ui-standards"] = "vitest run src/components/standard/__tests__ --config vitest.config.ts";
  pkg.scripts["build:storybook"] = "storybook build";
  pkg.scripts["storybook"] = "storybook dev -p 6006";
  pkg.scripts["validate:phase2:ui-standards-full"] =
    "npm run ui:inventory && npm run ui:validate:phase2-full && npm run test:ui-standards && npm run build && npm run build:storybook";

  pkg.devDependencies = pkg.devDependencies || {};
  pkg.devDependencies["storybook"] = pkg.devDependencies["storybook"] || "^10.0.0";
  pkg.devDependencies["@storybook/react-vite"] = pkg.devDependencies["@storybook/react-vite"] || "^10.0.0";
  pkg.devDependencies["@storybook/addon-docs"] = pkg.devDependencies["@storybook/addon-docs"] || "^10.0.0";
  pkg.devDependencies["@storybook/addon-a11y"] = pkg.devDependencies["@storybook/addon-a11y"] || "^10.0.0";
});

updateJson("Frontend/PlantProcess.Web/tsconfig.app.json", (json) => {
  json.exclude = json.exclude || [];
  for (const item of ["src/**/*.stories.ts", "src/**/*.stories.tsx", "src/**/*.mdx", ".storybook", "storybook-static"]) {
    if (!json.exclude.includes(item)) json.exclude.push(item);
  }
});

// ============================================================
// 16. Run inventory immediately once
// ============================================================

console.log("");
console.log("Running inventory generator...");
process.chdir(frontendRoot);
require("child_process").execFileSync("node", ["tools/ui/generate-ui-standards-inventory.mjs"], {
  stdio: "inherit",
});

console.log("");
console.log("Phase 2B full UI standards acceptance pack applied.");
console.log("");
console.log("Next commands:");
console.log("  cd Frontend\\\\PlantProcess.Web");
console.log("  npm install");
console.log("  npm run ui:inventory");
console.log("  npm run ui:validate:phase2-full");
console.log("  npm run test:ui-standards");
console.log("  npm run build");
console.log("  npm run build:storybook");
console.log("");