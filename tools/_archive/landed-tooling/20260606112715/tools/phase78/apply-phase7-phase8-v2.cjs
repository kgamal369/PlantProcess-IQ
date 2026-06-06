const fs = require('fs');
const path = require('path');
const cp = require('child_process');

const root = process.cwd();
const ts = new Date().toISOString().replace(/[-:T.Z]/g, '').slice(0, 14);
const backupRoot = path.join(root, '.phase78_backup', 'v2_' + ts);
const web = path.join(root, 'Frontend', 'PlantProcess.Web');
const src = path.join(web, 'src');

function norm(p) { return p.split(path.sep).join('/'); }
function rel(p) { return norm(path.relative(root, p)); }
function ensureDir(p) { fs.mkdirSync(p, { recursive: true }); }
function exists(p) { return fs.existsSync(p); }
function read(p) { return fs.readFileSync(p, 'utf8'); }
function write(p, content) {
  ensureDir(path.dirname(p));
  fs.writeFileSync(p, content.replace(/\n/g, '\r\n'), 'utf8');
  console.log('Wrote: ' + rel(p));
}
function backup(p) {
  if (!exists(p)) return;
  const target = path.join(backupRoot, path.relative(root, p));
  ensureDir(path.dirname(target));
  fs.copyFileSync(p, target);
}
function patch(p, fn) {
  if (!exists(p)) throw new Error('Missing required file: ' + rel(p));
  backup(p);
  const before = read(p).replace(/\r\n/g, '\n');
  const after = fn(before);
  if (after !== before) write(p, after);
  else console.log('No change needed: ' + rel(p));
}
function walk(dir) {
  if (!exists(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    return entry.isDirectory() ? walk(full) : [full];
  });
}
function lineCount(text) { return text.replace(/\r\n/g, '\n').split('\n').length; }
function writeJson(p, payload) { write(p, JSON.stringify(payload, null, 2) + '\n'); }
function run(name, args, cwd = root) {
  console.log('\n---- ' + name);
  cp.execFileSync(args[0], args.slice(1), { cwd, stdio: 'inherit', shell: false });
}

console.log('=================================================================================================');
console.log('PlantProcess IQ Phase 7 + Phase 8 Implementation Pack v2');
console.log('=================================================================================================');
console.log('Project root : ' + root);
console.log('Backup folder: ' + backupRoot);
ensureDir(backupRoot);
if (!exists(web)) throw new Error('Frontend root not found: ' + web);

write(path.join(src, 'i18n', 'phase78', 'phase78I18n.ts'), `// PlantProcess IQ Phase 7 — i18n + Arabic RTL source of truth.
// PPIQ_PHASE7_I18N_RTL
export type Phase78Locale = "en" | "ar";

export type Phase78MessageKey =
  | "language"
  | "english"
  | "arabic"
  | "toggleLanguage"
  | "readinessTitle"
  | "readinessSubtitle"
  | "direction"
  | "locale"
  | "numberSample"
  | "dateSample"
  | "themeDirectionMatrix"
  | "mobileReady"
  | "rtlReady"
  | "backendHygieneReady";

export const phase78LocaleStorageKey = "plantprocess.locale.v1";

export const phase78Messages: Record<Phase78Locale, Record<Phase78MessageKey, string>> = {
  en: {
    language: "Language",
    english: "English",
    arabic: "Arabic",
    toggleLanguage: "Switch language",
    readinessTitle: "Internationalization and RTL readiness",
    readinessSubtitle: "PlantProcess IQ is prepared for English and Arabic MENA workflows with locale-aware formatting.",
    direction: "Direction",
    locale: "Locale",
    numberSample: "Number sample",
    dateSample: "Date sample",
    themeDirectionMatrix: "Theme, locale, and direction matrix",
    mobileReady: "Mobile/touch target hardening is active.",
    rtlReady: "RTL shell support is active.",
    backendHygieneReady: "Backend API hygiene guard is active."
  },
  ar: {
    language: "اللغة",
    english: "الإنجليزية",
    arabic: "العربية",
    toggleLanguage: "تغيير اللغة",
    readinessTitle: "جاهزية الترجمة واتجاه الواجهة من اليمين إلى اليسار",
    readinessSubtitle: "PlantProcess IQ جاهز لتجارب عمل بالإنجليزية والعربية مع تنسيق محلي للأرقام والتواريخ.",
    direction: "اتجاه الواجهة",
    locale: "اللغة المحلية",
    numberSample: "مثال رقم",
    dateSample: "مثال تاريخ",
    themeDirectionMatrix: "مصفوفة السمة واللغة واتجاه الواجهة",
    mobileReady: "تحسينات الهاتف واللمس مفعلة.",
    rtlReady: "دعم الواجهة العربية من اليمين إلى اليسار مفعل.",
    backendHygieneReady: "بوابة نظافة واجهات الخلفية مفعلة."
  }
};

export function isPhase78Locale(value: string | null | undefined): value is Phase78Locale {
  return value === "en" || value === "ar";
}

export function getPhase78StoredLocale(): Phase78Locale {
  if (typeof window === "undefined") return "en";
  const stored = window.localStorage.getItem(phase78LocaleStorageKey);
  return isPhase78Locale(stored) ? stored : "en";
}

export function getPhase78Direction(locale: Phase78Locale): "ltr" | "rtl" {
  return locale === "ar" ? "rtl" : "ltr";
}

export function t(key: Phase78MessageKey, locale: Phase78Locale = getPhase78StoredLocale()): string {
  return phase78Messages[locale]?.[key] ?? phase78Messages.en[key] ?? key;
}

export function formatPhase78Number(value: number, locale: Phase78Locale = getPhase78StoredLocale()): string {
  return new Intl.NumberFormat(locale === "ar" ? "ar-EG" : "en-US", { maximumFractionDigits: 2 }).format(value);
}

export function formatPhase78Date(value: Date, locale: Phase78Locale = getPhase78StoredLocale()): string {
  return new Intl.DateTimeFormat(locale === "ar" ? "ar-EG" : "en-US", { dateStyle: "medium", timeStyle: "short" }).format(value);
}
`);

write(path.join(src, 'i18n', 'phase78', 'phase78I18nRuntime.ts'), `import { formatPhase78Date, formatPhase78Number, getPhase78Direction, getPhase78StoredLocale, phase78LocaleStorageKey, t, type Phase78Locale } from "./phase78I18n";

function applyLocale(locale: Phase78Locale, announce = false): void {
  const direction = getPhase78Direction(locale);
  document.documentElement.lang = locale;
  document.documentElement.dir = direction;
  document.documentElement.dataset.locale = locale;
  document.documentElement.dataset.phase78Direction = direction;
  window.localStorage.setItem(phase78LocaleStorageKey, locale);

  const state = document.getElementById("ppiq-phase78-language-state");
  if (state) state.textContent = locale.toUpperCase();

  const toggle = document.getElementById("ppiq-phase78-language-toggle");
  if (toggle) {
    toggle.setAttribute("aria-label", t("toggleLanguage", locale));
    toggle.setAttribute("title", t("toggleLanguage", locale));
  }

  const sample = document.getElementById("ppiq-phase78-locale-sample");
  if (sample) {
    sample.textContent = t("numberSample", locale) + ": " + formatPhase78Number(5670.25, locale) + " · " + t("dateSample", locale) + ": " + formatPhase78Date(new Date(), locale);
  }

  const live = document.getElementById("ppiq-phase56-live-region") || document.getElementById("ppiq-phase78-live-region");
  if (announce && live) live.textContent = locale === "ar" ? "تم تغيير اللغة إلى العربية." : "Language changed to English.";

  window.dispatchEvent(new CustomEvent("ppiq:locale-changed", { detail: { locale, direction } }));
}

function ensureLiveRegion(): void {
  if (document.getElementById("ppiq-phase78-live-region")) return;
  const live = document.createElement("div");
  live.id = "ppiq-phase78-live-region";
  live.className = "ppiq-sr-only";
  live.setAttribute("aria-live", "polite");
  live.setAttribute("aria-atomic", "true");
  document.body.appendChild(live);
}

function ensureToggle(): void {
  if (document.getElementById("ppiq-phase78-language-toggle")) return;
  const button = document.createElement("button");
  button.id = "ppiq-phase78-language-toggle";
  button.type = "button";
  button.className = "phase78-language-toggle";
  button.innerHTML = '<span aria-hidden="true">ع/A</span><span id="ppiq-phase78-language-state"></span>';
  button.addEventListener("click", () => {
    const current = getPhase78StoredLocale();
    applyLocale(current === "ar" ? "en" : "ar", true);
  });
  document.body.appendChild(button);
}

function ensureSample(): void {
  if (document.getElementById("ppiq-phase78-locale-sample")) return;
  const sample = document.createElement("div");
  sample.id = "ppiq-phase78-locale-sample";
  sample.className = "ppiq-sr-only";
  document.body.appendChild(sample);
}

export function initializePhase78I18nRuntime(): void {
  if (typeof window === "undefined") return;
  applyLocale(getPhase78StoredLocale());
  window.addEventListener("DOMContentLoaded", () => {
    ensureLiveRegion();
    ensureSample();
    ensureToggle();
    applyLocale(getPhase78StoredLocale());
  });
}

initializePhase78I18nRuntime();
`);

write(path.join(src, 'styles', 'phase78', 'phase78-i18n-rtl.css'), `/* PlantProcess IQ Phase 7 — i18n, Arabic RTL, and mobile parity hardening. */
html[dir="rtl"] { direction: rtl; }
html[dir="ltr"] { direction: ltr; }
body { overflow-x: hidden; }
:where(button, a, input, select, textarea, [role="button"]) { min-block-size: 44px; }
.phase78-language-toggle {
  position: fixed;
  inset-block-end: 4.25rem;
  inset-inline-end: 1rem;
  z-index: 9998;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: .55rem;
  min-inline-size: 5.25rem;
  min-block-size: 2.75rem;
  padding-inline: .85rem;
  border: 1px solid var(--ppiq-border-strong, rgba(0,212,255,.42));
  border-radius: var(--ppiq-radius-control, 14px);
  background: var(--ppiq-surface-strong, #071426);
  color: var(--ppiq-text, #EAF6FF);
  font-weight: 900;
  box-shadow: var(--ppiq-shadow-glow, 0 0 32px rgba(0,212,255,.18));
  cursor: pointer;
}
.phase78-language-toggle:hover { border-color: var(--ppiq-cyan, #00D4FF); }
html[dir="rtl"] :where(.app-layout, .dashboard-page, .admin-page, .material-analytics-page, .mapping-health-page) { text-align: start; }
html[dir="rtl"] :where(.sidebar, .nav, .app-nav, .site-nav) svg { transform: scaleX(-1); }
html[dir="rtl"] :where(table) { direction: rtl; }
html[dir="rtl"] :where(th, td) { text-align: start; }
.phase78-readiness-page { min-height: 100%; padding: clamp(1rem,3vw,2rem); color: var(--ppiq-text,#EAF6FF); }
.phase78-readiness-hero, .phase78-readiness-card { border: 1px solid var(--ppiq-border, rgba(0,212,255,.22)); border-radius: var(--ppiq-radius-card,24px); background: var(--ppiq-surface,rgba(8,20,38,.86)); box-shadow: var(--ppiq-shadow-glow,0 0 32px rgba(0,212,255,.18)); }
.phase78-readiness-hero { padding: clamp(1rem,4vw,2rem); }
.phase78-readiness-hero h1 { margin-block: 0 .75rem; font-size: clamp(2rem,5vw,4rem); }
.phase78-readiness-hero p { max-inline-size: 72ch; color: var(--ppiq-text-muted,#B7D4E8); }
.phase78-readiness-grid { display: grid; grid-template-columns: repeat(3,minmax(0,1fr)); gap: 1rem; margin-block-start: 1rem; }
.phase78-readiness-card { padding: 1rem; }
.phase78-readiness-card strong { display: block; font-size: 1.4rem; margin-block-start: .35rem; }
.phase78-readiness-actions { display: flex; flex-wrap: wrap; gap: .75rem; margin-block-start: 1rem; }
.phase78-readiness-actions button { padding-inline: 1rem; border-radius: 999px; border: 1px solid var(--ppiq-border-strong); background: var(--ppiq-surface-strong); color: var(--ppiq-text); font-weight: 850; }
@media (max-width:760px) {
  .phase78-readiness-grid { grid-template-columns: 1fr; }
  .phase78-language-toggle { inset-block-end: 4rem; inset-inline-end: .75rem; }
  :where(.app-shell, .app-layout, .dashboard-grid, .dashboard-page, .admin-page) { max-inline-size: 100%; overflow-x: clip; }
}
`);

write(path.join(src, 'pages', 'I18nRtlReadinessPage.tsx'), `import { useEffect, useState } from "react";
import { formatPhase78Date, formatPhase78Number, getPhase78Direction, getPhase78StoredLocale, phase78LocaleStorageKey, t, type Phase78Locale } from "../i18n/phase78/phase78I18n";

function applyLocale(locale: Phase78Locale): void {
  window.localStorage.setItem(phase78LocaleStorageKey, locale);
  document.documentElement.lang = locale;
  document.documentElement.dir = getPhase78Direction(locale);
  document.documentElement.dataset.locale = locale;
  window.dispatchEvent(new CustomEvent("ppiq:locale-changed", { detail: { locale, direction: getPhase78Direction(locale) } }));
}

export function I18nRtlReadinessPage() {
  const [locale, setLocale] = useState<Phase78Locale>(() => getPhase78StoredLocale());
  const direction = getPhase78Direction(locale);

  useEffect(() => { applyLocale(locale); }, [locale]);

  return (
    <main className="phase78-readiness-page" id="main-content">
      <section className="phase78-readiness-hero">
        <p>{t("themeDirectionMatrix", locale)}</p>
        <h1>{t("readinessTitle", locale)}</h1>
        <p>{t("readinessSubtitle", locale)}</p>
        <div className="phase78-readiness-actions" aria-label={t("toggleLanguage", locale)}>
          <button type="button" onClick={() => setLocale("en")}>{t("english", locale)}</button>
          <button type="button" onClick={() => setLocale("ar")}>{t("arabic", locale)}</button>
        </div>
      </section>
      <section className="phase78-readiness-grid" aria-label="Phase 7 and Phase 8 readiness cards">
        <article className="phase78-readiness-card"><span>{t("locale", locale)}</span><strong>{locale}</strong></article>
        <article className="phase78-readiness-card"><span>{t("direction", locale)}</span><strong>{direction}</strong></article>
        <article className="phase78-readiness-card"><span>{t("numberSample", locale)}</span><strong>{formatPhase78Number(5670.25, locale)}</strong></article>
        <article className="phase78-readiness-card"><span>{t("dateSample", locale)}</span><strong>{formatPhase78Date(new Date("2026-06-06T08:00:00Z"), locale)}</strong></article>
        <article className="phase78-readiness-card"><span>{t("rtlReady", locale)}</span><strong>RTL</strong></article>
        <article className="phase78-readiness-card"><span>{t("backendHygieneReady", locale)}</span><strong>P08</strong></article>
      </section>
    </main>
  );
}
`);

patch(path.join(src, 'main.tsx'), (s) => {
  if (!s.includes('./i18n/phase78/phase78I18nRuntime')) {
    if (s.includes('import "./accessibility/phase56ThemeRuntime";')) {
      return s.replace('import "./accessibility/phase56ThemeRuntime";', 'import "./accessibility/phase56ThemeRuntime";\nimport "./i18n/phase78/phase78I18nRuntime";');
    }
    if (s.includes('import "./index.css";')) {
      return s.replace('import "./index.css";', 'import "./index.css";\nimport "./i18n/phase78/phase78I18nRuntime";');
    }
    return 'import "./i18n/phase78/phase78I18nRuntime";\n' + s;
  }
  return s;
});

patch(path.join(src, 'styles', 'global.css'), (s) => {
  const importLine = '@import "./phase78/phase78-i18n-rtl.css";';
  return s.includes(importLine) ? s : s.trimEnd() + '\n' + importLine + '\n';
});

patch(path.join(src, 'App.implementation.tsx'), (s) => {
  let next = s;
  if (!next.includes('const I18nRtlReadinessPage = lazy')) {
    const lazyBlock = `
const I18nRtlReadinessPage = lazy(() =>
  import("./pages/I18nRtlReadinessPage").then((m) => ({ default: m.I18nRtlReadinessPage }))
);
`;
    if (next.includes('function withPageBoundary(')) next = next.replace('function withPageBoundary(', lazyBlock + '\nfunction withPageBoundary(');
    else throw new Error('Cannot find lazy insertion anchor in App.implementation.tsx');
  }
  if (!next.includes('path="/i18n-rtl"')) {
    const route = `
                    {/* Phase 7 i18n + Arabic RTL readiness */}
                    <Route
                      path="/i18n-rtl"
                      element={withPageBoundary(
                        "/i18n-rtl",
                        "Internationalization readiness is refreshing",
                        <I18nRtlReadinessPage />
                      )}
                    />
`;
    if (next.includes('path="/analytics-widgets"')) {
      next = next.replace(/\n\s*<Route\n\s*path="\/analytics-widgets"/, route + '\n                    <Route\n                      path="/analytics-widgets"');
    } else if (next.includes('path="/demo-lifecycle"')) {
      next = next.replace(/\n\s*<Route\n\s*path="\/demo-lifecycle"/, route + '\n                    <Route\n                      path="/demo-lifecycle"');
    } else {
      throw new Error('Cannot find route insertion anchor in App.implementation.tsx');
    }
  }
  return next;
});

write(path.join(web, 'e2e', 'i18n', 'phase78-i18n-rtl.spec.ts'), `import { expect, test } from "@playwright/test";

const routes = ["/", "/dashboard", "/materials", "/ml-readiness", "/i18n-rtl"];

for (const theme of ["dark", "light"] as const) {
  for (const locale of ["en", "ar"] as const) {
    test.describe(theme + " " + locale, () => {
      for (const route of routes) {
        test(route + " keeps locale, dir and mobile shell", async ({ page }) => {
          await page.addInitScript(({ themeName, localeName }) => {
            window.localStorage.setItem("plantprocess.theme.v1", themeName);
            window.localStorage.setItem("plantprocess.locale.v1", localeName);
          }, { themeName: theme, localeName: locale });
          await page.setViewportSize({ width: 390, height: 844 });
          await page.goto(route);
          await expect(page.locator("html")).toHaveAttribute("lang", locale);
          await expect(page.locator("html")).toHaveAttribute("dir", locale === "ar" ? "rtl" : "ltr");
          await expect(page.locator("#ppiq-phase78-language-toggle")).toBeAttached();
          const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 2);
          expect(overflow).toBeFalsy();
        });
      }
    });
  }
}
`);

write(path.join(root, 'docs', 'ux', 'I18N_RTL.md'), `# PlantProcess IQ Phase 7 — i18n and Arabic RTL

Runtime key: \`plantprocess.locale.v1\`.

Source files:
- \`Frontend/PlantProcess.Web/src/i18n/phase78/phase78I18n.ts\`
- \`Frontend/PlantProcess.Web/src/i18n/phase78/phase78I18nRuntime.ts\`
- \`Frontend/PlantProcess.Web/src/styles/phase78/phase78-i18n-rtl.css\`

Add a string by extending \`Phase78MessageKey\`, then adding values in both \`phase78Messages.en\` and \`phase78Messages.ar\`.
Add a locale by extending \`Phase78Locale\`, adding a resource bundle, and updating \`getPhase78Direction\` when the locale is RTL.
`);

function backendAnalysis() {
  const important = [
    'Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs',
    'Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs',
    'Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs',
    'Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.cs',
    'Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs',
    'Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs',
    'Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs',
    'Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs'
  ];
  const targets = important.map((p) => {
    const f = path.join(root, p);
    if (!exists(f)) return { path: p, exists: false };
    const t = read(f);
    const n = lineCount(t);
    return {
      path: p,
      exists: true,
      lines: n,
      routeCount: (t.match(/\.Map(Get|Post|Put|Delete|Patch)\s*\(/g) || []).length,
      directDataAccessHits: (t.match(/\b(Npgsql|SqlQueryRaw|ExecuteSqlRaw|Database\.|DbContext)\b/g) || []).length,
      risk: n > 1000 ? 'critical-god-file' : n > 700 ? 'large-file' : n > 500 ? 'watch' : 'ok'
    };
  });
  const allLargeFiles = walk(path.join(root, 'Backend'))
    .filter((f) => f.endsWith('.cs'))
    .map((f) => ({ path: rel(f), lines: lineCount(read(f)) }))
    .filter((x) => x.lines > 500)
    .sort((a, b) => a.path.localeCompare(b.path));
  const payload = { generatedAtUtc: new Date().toISOString(), marker: 'PPIQ_PHASE8_BACKEND_API_HYGIENE', targets, allLargeFiles };
  writeJson(path.join(root, 'docs', 'phase8', 'backend-api-hygiene-report.json'), payload);
  const md = ['# PlantProcess IQ Phase 8 — Backend API Hygiene Report', '', '| File | Lines | Routes | Data-access hits | Risk |', '|---|---:|---:|---:|---|'];
  for (const r of targets) md.push('| `' + r.path + '` | ' + (r.exists ? r.lines : 'missing') + ' | ' + (r.routeCount ?? 0) + ' | ' + (r.directDataAccessHits ?? 0) + ' | ' + (r.risk ?? 'missing') + ' |');
  md.push('', 'This is the safe pre-split guard: current large files are tracked, unknown new oversized backend files are blocked, and a source-route contract snapshot is created before destructive refactoring.');
  write(path.join(root, 'docs', 'phase8', 'backend-api-hygiene-report.md'), md.join('\n'));
  writeJson(path.join(root, 'tools', 'phase78', 'backend-api-hygiene-baseline.json'), payload);
}

function snapshotRoutes() {
  const files = walk(path.join(root, 'Backend', 'PlantProcess.Api')).filter((f) => f.endsWith('.cs'));
  const routes = [];
  for (const f of files) {
    const t = read(f);
    const groupMatch = Array.from(t.matchAll(/MapGroup\s*\(\s*"([^"]+)"\s*\)/g));
    const g = groupMatch.length ? groupMatch[0][1] : '';
    for (const m of t.matchAll(/\.Map(Get|Post|Put|Delete|Patch)\s*\(\s*"([^"]*)"/g)) {
      routes.push({ method: m[1].toUpperCase(), group: g, path: m[2], source: rel(f) });
    }
  }
  routes.sort((a, b) => (a.group + a.path + a.method).localeCompare(b.group + b.path + b.method));
  writeJson(path.join(root, 'docs', 'phase8', 'openapi-source-contract-snapshot.json'), { generatedAtUtc: new Date().toISOString(), marker: 'PPIQ_PHASE8_SOURCE_ROUTE_CONTRACT_SNAPSHOT', routeCount: routes.length, routes });
}

backendAnalysis();
snapshotRoutes();

write(path.join(root, 'tools', 'phase78', 'check-backend-api-hygiene.cjs'), `const fs = require("fs");
const path = require("path");
const root = process.cwd();
const baseline = JSON.parse(fs.readFileSync(path.join(root, "tools", "phase78", "backend-api-hygiene-baseline.json"), "utf8"));
const tracked = new Set([
  ...(baseline.targets || []).filter((x) => x.exists && x.lines > 500).map((x) => x.path.split("\\\\").join("/")),
  ...(baseline.allLargeFiles || []).filter((x) => x.lines > 500).map((x) => x.path.split("\\\\").join("/"))
]);
function walk(dir) {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const f = path.join(dir, entry.name);
    return entry.isDirectory() ? walk(f) : [f];
  });
}
function lines(file) { return fs.readFileSync(file, "utf8").split(/\\r?\\n/).length; }
const scanRoots = [path.join(root, "Backend", "PlantProcess.Api"), path.join(root, "Backend", "PlantProcess.Application")];
const offenders = [];
const warnings = [];
for (const file of scanRoots.flatMap(walk).filter((x) => x.endsWith(".cs"))) {
  const relativePath = path.relative(root, file).split(path.sep).join("/");
  const count = lines(file);
  const isTracked = tracked.has(relativePath);
  if (count > 700 && !isTracked) offenders.push(relativePath + ": " + count + " lines");
  else if (count > 500) warnings.push(relativePath + ": " + count + " lines" + (isTracked ? " (tracked P08 refactor target)" : ""));
}
for (const warning of warnings) console.warn("[WARN] " + warning);
if (offenders.length) {
  console.error("Unknown oversized backend API/Application files:");
  offenders.forEach((x) => console.error(" - " + x));
  process.exit(1);
}
console.log("Phase 8 backend API hygiene gate passed. Existing large files are tracked; unknown new oversized files are blocked.");
`);

write(path.join(root, 'tools', 'phase78', 'validate-phase78.cjs'), `const fs = require("fs");
const path = require("path");
const cp = require("child_process");
const root = process.cwd();
function read(relativePath) {
  const file = path.join(root, relativePath);
  if (!fs.existsSync(file)) throw new Error("Missing file: " + relativePath);
  return fs.readFileSync(file, "utf8");
}
function has(relativePath, marker) {
  const text = read(relativePath);
  if (!text.includes(marker)) throw new Error(relativePath + " missing marker: " + marker);
}
has("Frontend/PlantProcess.Web/src/i18n/phase78/phase78I18n.ts", "PPIQ_PHASE7_I18N_RTL");
has("Frontend/PlantProcess.Web/src/i18n/phase78/phase78I18n.ts", "ar:");
has("Frontend/PlantProcess.Web/src/i18n/phase78/phase78I18nRuntime.ts", "document.documentElement.dir");
has("Frontend/PlantProcess.Web/src/main.tsx", "./i18n/phase78/phase78I18nRuntime");
has("Frontend/PlantProcess.Web/src/styles/global.css", "./phase78/phase78-i18n-rtl.css");
has("Frontend/PlantProcess.Web/src/App.implementation.tsx", 'path="/i18n-rtl"');
has("Frontend/PlantProcess.Web/e2e/i18n/phase78-i18n-rtl.spec.ts", "plantprocess.locale.v1");
has("docs/ux/I18N_RTL.md", "plantprocess.locale.v1");
has("docs/phase8/backend-api-hygiene-report.json", "PPIQ_PHASE8_BACKEND_API_HYGIENE");
has("docs/phase8/openapi-source-contract-snapshot.json", "PPIQ_PHASE8_SOURCE_ROUTE_CONTRACT_SNAPSHOT");
cp.execFileSync("node", ["tools/phase78/check-backend-api-hygiene.cjs"], { cwd: root, stdio: "inherit" });
if (fs.existsSync(path.join(root, "tools", "phase56", "validate-phase56.cjs"))) cp.execFileSync("node", ["tools/phase56/validate-phase56.cjs"], { cwd: root, stdio: "inherit" });
console.log("Phase 7 + Phase 8 source validation passed.");
`);

write(path.join(root, 'tools', 'phase78', 'Invoke-Phase7Phase8Validation.ps1'), `[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunFrontendBuild,
    [switch]$RunBackendBuild,
    [switch]$RunFrontendI18nE2E
)
$ErrorActionPreference = "Stop"
function Run-Step([string]$Name, [scriptblock]$Block) {
    Write-Host ""
    Write-Host "---- $Name" -ForegroundColor Cyan
    & $Block
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
}
Push-Location $ProjectRoot
try {
    if (Test-Path ".\\tools\\phase1-phase2\\Test-Utf8NoBom.ps1") { Run-Step "Phase 1/2 BOM gate" { powershell -ExecutionPolicy Bypass -File ".\\tools\\phase1-phase2\\Test-Utf8NoBom.ps1" -ProjectRoot $ProjectRoot } }
    if (Test-Path ".\\tools\\phase1-phase2\\Invoke-SecretScan.ps1") { Run-Step "Phase 1/2 production-runtime secret scan" { powershell -ExecutionPolicy Bypass -File ".\\tools\\phase1-phase2\\Invoke-SecretScan.ps1" -ProjectRoot $ProjectRoot } }
    Run-Step "Phase 7/8 source validation" { node ".\\tools\\phase78\\validate-phase78.cjs" }
    if ($RunFrontendBuild) { Push-Location ".\\Frontend\\PlantProcess.Web"; try { Run-Step "npm run build" { npm run build } } finally { Pop-Location } }
    if ($RunBackendBuild) { Push-Location ".\\Backend"; try { Run-Step "dotnet build" { dotnet build } } finally { Pop-Location } }
    if ($RunFrontendI18nE2E) { Push-Location ".\\Frontend\\PlantProcess.Web"; try { Run-Step "Playwright i18n/RTL e2e" { npx playwright test e2e/i18n/phase78-i18n-rtl.spec.ts } } finally { Pop-Location } }
    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Host "Phase 7 + Phase 8 validation completed successfully." -ForegroundColor Green
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
} finally { Pop-Location }
`);

write(path.join(root, 'docs', 'phase7-phase8', 'PHASE7_PHASE8_IMPLEMENTATION_EVIDENCE.md'), `# PlantProcess IQ Phase 7 + Phase 8 Evidence

Generated: ${new Date().toISOString()}

## P07 — Internationalization + Arabic RTL

- Locale runtime added with persistent key \`plantprocess.locale.v1\`.
- English and Arabic bundles added.
- Document \`lang\` and \`dir\` are driven by locale.
- RTL/mobile CSS hardening and 44px tap target rule added.
- \`/i18n-rtl\` readiness route and Playwright matrix spec added.

## P08 — Backend God-File Refactor & API Hygiene

- Backend god-file inventory and hygiene report generated.
- Source-route contract snapshot generated before destructive endpoint refactoring.
- Backend hygiene gate blocks unknown new oversized API/Application files.

## Boundary

This pack does not destructively split the largest backend endpoint files yet. It creates the route-contract and file-size guard needed to split them safely one target at a time.
`);

const pkgFile = path.join(web, 'package.json');
if (exists(pkgFile)) {
  backup(pkgFile);
  const pkg = JSON.parse(read(pkgFile));
  pkg.scripts = pkg.scripts || {};
  pkg.scripts['phase78:validate'] = 'node ../../tools/phase78/validate-phase78.cjs';
  pkg.scripts['test:i18n-rtl'] = 'playwright test e2e/i18n/phase78-i18n-rtl.spec.ts';
  write(pkgFile, JSON.stringify(pkg, null, 2) + '\n');
}

run('node --check tools/phase78/check-backend-api-hygiene.cjs', ['node', '--check', 'tools/phase78/check-backend-api-hygiene.cjs']);
run('node --check tools/phase78/validate-phase78.cjs', ['node', '--check', 'tools/phase78/validate-phase78.cjs']);
run('Phase 7/8 source validation', ['node', 'tools/phase78/validate-phase78.cjs']);

console.log('\n=================================================================================================');
console.log('Phase 7 + Phase 8 implementation pack v2 completed.');
console.log('Evidence : ' + rel(path.join(root, 'docs', 'phase7-phase8', 'PHASE7_PHASE8_IMPLEMENTATION_EVIDENCE.md')));
console.log('Validator: ' + rel(path.join(root, 'tools', 'phase78', 'Invoke-Phase7Phase8Validation.ps1')));
console.log('Backup   : ' + backupRoot);
console.log('=================================================================================================');