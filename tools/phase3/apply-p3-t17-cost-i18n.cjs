const fs = require("fs");
const path = require("path");

const root = process.cwd();
const marker = "PPIQ_P3_T17_COST_I18N_CONTRACT";

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function exists(rel) {
  return fs.existsSync(full(rel));
}

function read(rel) {
  return fs.readFileSync(full(rel), "utf8");
}

function write(rel, content) {
  const target = full(rel);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content.replace(/\r?\n/g, "\r\n"), "utf8");
  console.log("[P3-T17] wrote " + rel);
}

function backup(rel) {
  if (!exists(rel)) return;

  const stamp = new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14);
  const target = path.join(root, ".phase3_backups", "P3-T17_JS_" + stamp, rel.replaceAll("/", path.sep));
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(full(rel), target);
}

function walk(dir, predicate) {
  const out = [];
  if (!fs.existsSync(dir)) return out;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const item = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (entry.name === "node_modules" || entry.name === "dist" || entry.name === "bin" || entry.name === "obj") continue;
      out.push(...walk(item, predicate));
    } else if (!predicate || predicate(item)) {
      out.push(item);
    }
  }

  return out;
}

write("Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.ts", `
export const P3_T17_COST_I18N_CONTRACT = "${marker}";

export type P3T17LocaleCode = "en" | "de" | "ar";

export type P3T17Direction = "ltr" | "rtl";

export type P3T17CostI18nKey = "title" | "description" | "save";

export type P3T17QualifiedCostI18nKey =
  | "v5.p3.cost.title"
  | "v5.p3.cost.description"
  | "v5.p3.cost.save";

export type P3T17CostI18nRow = {
  localeCode: P3T17LocaleCode;
  namespace: "v5.p3.cost";
  stringKey: P3T17CostI18nKey;
  qualifiedKey: P3T17QualifiedCostI18nKey;
  translatedText: string;
  direction: P3T17Direction;
  screenCode: "v5-p3-cost-assumptions";
  isHighTraffic: true;
};

export const p3T17CostI18nNamespace = "v5.p3.cost" as const;

export const p3T17CostI18nScreenCode = "v5-p3-cost-assumptions" as const;

export const p3T17RequiredLocales: P3T17LocaleCode[] = ["en", "de", "ar"];

export const p3T17LocaleDirections: Record<P3T17LocaleCode, P3T17Direction> = {
  en: "ltr",
  de: "ltr",
  ar: "rtl",
};

export const p3T17RequiredCostKeys: P3T17CostI18nKey[] = [
  "title",
  "description",
  "save",
];

export const p3T17CostTranslations: Record<P3T17LocaleCode, Record<P3T17CostI18nKey, string>> = {
  en: {
    title: "Cost Assumption Management",
    description: "Versioned tenant cost bands for credible value-impact ranges.",
    save: "Save cost bands",
  },
  de: {
    title: "Kostenannahmen verwalten",
    description: "Versionierte Kostenbänder pro Mandant für glaubwürdige Wertspannen.",
    save: "Kostenbänder speichern",
  },
  ar: {
    title: "إدارة افتراضات التكلفة",
    description: "نطاقات تكلفة بإصدارات لكل مستأجر لعرض تأثير مالي موثوق.",
    save: "حفظ نطاقات التكلفة",
  },
};

export function p3T17QualifiedKey(key: P3T17CostI18nKey): P3T17QualifiedCostI18nKey {
  return (p3T17CostI18nNamespace + "." + key) as P3T17QualifiedCostI18nKey;
}

export function p3T17TranslateCostKey(
  localeCode: P3T17LocaleCode | string,
  key: P3T17CostI18nKey,
): string {
  const safeLocale = p3T17RequiredLocales.includes(localeCode as P3T17LocaleCode)
    ? (localeCode as P3T17LocaleCode)
    : "en";

  return p3T17CostTranslations[safeLocale][key] ?? p3T17CostTranslations.en[key];
}

export function p3T17CostI18nRows(): P3T17CostI18nRow[] {
  const rows: P3T17CostI18nRow[] = [];

  for (const localeCode of p3T17RequiredLocales) {
    for (const stringKey of p3T17RequiredCostKeys) {
      rows.push({
        localeCode,
        namespace: p3T17CostI18nNamespace,
        stringKey,
        qualifiedKey: p3T17QualifiedKey(stringKey),
        translatedText: p3T17CostTranslations[localeCode][stringKey],
        direction: p3T17LocaleDirections[localeCode],
        screenCode: p3T17CostI18nScreenCode,
        isHighTraffic: true,
      });
    }
  }

  return rows;
}

export function p3T17ValidateCostI18nCatalog() {
  const missing: string[] = [];
  const duplicateKeys = new Set<string>();
  const seen = new Set<string>();
  const rows = p3T17CostI18nRows();

  for (const localeCode of p3T17RequiredLocales) {
    for (const stringKey of p3T17RequiredCostKeys) {
      const value = p3T17CostTranslations[localeCode]?.[stringKey];

      if (!value || !value.trim()) {
        missing.push(localeCode + ":" + p3T17QualifiedKey(stringKey));
      }
    }
  }

  for (const row of rows) {
    const id = row.localeCode + ":" + row.qualifiedKey;

    if (seen.has(id)) {
      duplicateKeys.add(id);
    }

    seen.add(id);
  }

  const arabicTitle = p3T17CostTranslations.ar.title;
  const arabicLooksArabic = /[\\u0600-\\u06FF]/.test(arabicTitle);

  if (p3T17LocaleDirections.ar !== "rtl") {
    missing.push("ar:direction:rtl");
  }

  if (!arabicLooksArabic) {
    missing.push("ar:arabic-script");
  }

  return {
    marker: P3_T17_COST_I18N_CONTRACT,
    isGreen: missing.length === 0 && duplicateKeys.size === 0 && rows.length === 9,
    namespace: p3T17CostI18nNamespace,
    screenCode: p3T17CostI18nScreenCode,
    localeCount: p3T17RequiredLocales.length,
    keyCount: p3T17RequiredCostKeys.length,
    rowCount: rows.length,
    missing,
    duplicateKeys: Array.from(duplicateKeys),
    rows,
  };
}

export function p3T17BuildDatabaseSeedPreview(): string {
  return p3T17CostI18nRows()
    .map((row) =>
      row.localeCode +
      "|" +
      row.namespace +
      "|" +
      row.stringKey +
      "|" +
      row.direction +
      "|" +
      row.translatedText,
    )
    .join("\\n");
}
`);

write("Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.test.ts", `
import { describe, expect, it } from "vitest";
import {
  p3T17BuildDatabaseSeedPreview,
  p3T17CostI18nRows,
  p3T17CostTranslations,
  p3T17LocaleDirections,
  p3T17QualifiedKey,
  p3T17RequiredCostKeys,
  p3T17RequiredLocales,
  p3T17TranslateCostKey,
  p3T17ValidateCostI18nCatalog,
} from "./p3T17CostAssumptionI18n";

describe("P3-T17 cost-assumption i18n contract", () => {
  it("has every Cost Assumption Management key in English, German, and Arabic", () => {
    const validation = p3T17ValidateCostI18nCatalog();

    expect(validation.isGreen).toBe(true);
    expect(validation.localeCount).toBe(3);
    expect(validation.keyCount).toBe(3);
    expect(validation.rowCount).toBe(9);
    expect(validation.missing).toEqual([]);
  });

  it("uses stable qualified keys under v5.p3.cost namespace", () => {
    expect(p3T17QualifiedKey("title")).toBe("v5.p3.cost.title");
    expect(p3T17QualifiedKey("description")).toBe("v5.p3.cost.description");
    expect(p3T17QualifiedKey("save")).toBe("v5.p3.cost.save");
  });

  it("certifies Arabic as RTL and verifies Arabic script is present", () => {
    expect(p3T17LocaleDirections.ar).toBe("rtl");
    expect(p3T17CostTranslations.ar.title).toMatch(/[\\u0600-\\u06FF]/);
    expect(p3T17TranslateCostKey("ar", "save")).toBe("حفظ نطاقات التكلفة");
  });

  it("falls back to English for unknown locale without leaking raw keys to the screen", () => {
    expect(p3T17TranslateCostKey("fr", "title")).toBe("Cost Assumption Management");
    expect(p3T17TranslateCostKey("fr", "title")).not.toBe("v5.p3.cost.title");
  });

  it("builds nine unique database seed rows for the DB i18n catalog", () => {
    const rows = p3T17CostI18nRows();
    const ids = new Set(rows.map((row) => row.localeCode + ":" + row.qualifiedKey));

    expect(rows).toHaveLength(p3T17RequiredLocales.length * p3T17RequiredCostKeys.length);
    expect(ids.size).toBe(rows.length);

    const preview = p3T17BuildDatabaseSeedPreview();
    expect(preview).toContain("en|v5.p3.cost|title|ltr|Cost Assumption Management");
    expect(preview).toContain("de|v5.p3.cost|save|ltr|Kostenbänder speichern");
    expect(preview).toContain("ar|v5.p3.cost|title|rtl|إدارة افتراضات التكلفة");
  });
});
`);

write("Backend/database/scripts/425_p3_t17_cost_assumption_i18n_contract.sql", `
-- ============================================================================
-- PlantProcess IQ — P3-T17 / PPIQ-T017 Cost Assumption Management i18n Contract
-- Marker: ${marker}
--
-- Idempotent catalog for high-traffic Cost Assumption Management strings.
-- Local laptop: apply to native Windows PostgreSQL if DB proof is required.
-- Server/customer: apply through the environment-specific migration/runbook.
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS public.ppiq_i18n_string_keys
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    namespace text NOT NULL,
    string_key text NOT NULL,
    default_text text NOT NULL,
    screen_code text NOT NULL,
    is_high_traffic boolean NOT NULL DEFAULT false,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_i18n_string_key UNIQUE(namespace, string_key)
);

CREATE TABLE IF NOT EXISTS public.ppiq_i18n_translations
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    locale_code text NOT NULL,
    namespace text NOT NULL,
    string_key text NOT NULL,
    translated_text text NOT NULL,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_i18n_translation UNIQUE(locale_code, namespace, string_key)
);

INSERT INTO public.ppiq_i18n_string_keys(namespace, string_key, default_text, screen_code, is_high_traffic)
VALUES
('v5.p3.cost', 'title', 'Cost Assumption Management', 'v5-p3-cost-assumptions', true),
('v5.p3.cost', 'description', 'Versioned tenant cost bands for credible value-impact ranges.', 'v5-p3-cost-assumptions', true),
('v5.p3.cost', 'save', 'Save cost bands', 'v5-p3-cost-assumptions', true)
ON CONFLICT (namespace, string_key) DO UPDATE
SET default_text = EXCLUDED.default_text,
    screen_code = EXCLUDED.screen_code,
    is_high_traffic = EXCLUDED.is_high_traffic;

INSERT INTO public.ppiq_i18n_translations(locale_code, namespace, string_key, translated_text)
VALUES
('en', 'v5.p3.cost', 'title', 'Cost Assumption Management'),
('en', 'v5.p3.cost', 'description', 'Versioned tenant cost bands for credible value-impact ranges.'),
('en', 'v5.p3.cost', 'save', 'Save cost bands'),

('de', 'v5.p3.cost', 'title', 'Kostenannahmen verwalten'),
('de', 'v5.p3.cost', 'description', 'Versionierte Kostenbänder pro Mandant für glaubwürdige Wertspannen.'),
('de', 'v5.p3.cost', 'save', 'Kostenbänder speichern'),

('ar', 'v5.p3.cost', 'title', 'إدارة افتراضات التكلفة'),
('ar', 'v5.p3.cost', 'description', 'نطاقات تكلفة بإصدارات لكل مستأجر لعرض تأثير مالي موثوق.'),
('ar', 'v5.p3.cost', 'save', 'حفظ نطاقات التكلفة')
ON CONFLICT (locale_code, namespace, string_key) DO UPDATE
SET translated_text = EXCLUDED.translated_text,
    updated_at_utc = now();

CREATE OR REPLACE FUNCTION public.ppiq_p3_t17_cost_i18n_status()
RETURNS TABLE
(
    gate_code text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        'CostI18nStringKeys',
        COUNT(*) = 3,
        'Expected 3 v5.p3.cost base keys, found ' || COUNT(*)::text
    FROM public.ppiq_i18n_string_keys
    WHERE namespace = 'v5.p3.cost'
      AND string_key IN ('title', 'description', 'save')
      AND screen_code = 'v5-p3-cost-assumptions'
      AND is_high_traffic = true

    UNION ALL

    SELECT
        'CostI18nTranslations',
        COUNT(*) = 9,
        'Expected 9 translations for en/de/ar x title/description/save, found ' || COUNT(*)::text
    FROM public.ppiq_i18n_translations
    WHERE namespace = 'v5.p3.cost'
      AND locale_code IN ('en', 'de', 'ar')
      AND string_key IN ('title', 'description', 'save')

    UNION ALL

    SELECT
        'ArabicRtlTranslationBasis',
        EXISTS
        (
            SELECT 1
            FROM public.ppiq_i18n_translations
            WHERE locale_code = 'ar'
              AND namespace = 'v5.p3.cost'
              AND string_key = 'title'
              AND translated_text ~ '[\\u0600-\\u06FF]'
        ),
        'Arabic title translation exists and uses Arabic script.';
$$;
`);

write("tools/phase3/validate-p3-t17-cost-i18n.cjs", `
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const marker = "${marker}";

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function exists(rel) {
  return fs.existsSync(full(rel));
}

function read(rel) {
  return fs.readFileSync(full(rel), "utf8");
}

function fail(message) {
  console.error("[RED] P3-T17 validation failed: " + message);
  process.exit(1);
}

function walk(dir, predicate) {
  const out = [];

  if (!fs.existsSync(dir)) return out;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const item = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (entry.name === "node_modules" || entry.name === "dist" || entry.name === "bin" || entry.name === "obj") continue;
      out.push(...walk(item, predicate));
    } else if (!predicate || predicate(item)) {
      out.push(item);
    }
  }

  return out;
}

const required = [
  "Frontend/PlantProcess.Web/src/i18n/v5I18n.tsx",
  "Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.ts",
  "Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.test.ts",
  "Backend/database/scripts/425_p3_t17_cost_assumption_i18n_contract.sql",
  "docs/phase3/P3_T17_COST_I18N.md",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const helper = read("Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.ts");
const v5 = read("Frontend/PlantProcess.Web/src/i18n/v5I18n.tsx");
const test = read("Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.test.ts");
const sql = read("Backend/database/scripts/425_p3_t17_cost_assumption_i18n_contract.sql");

if (!helper.includes("P3_T17_COST_I18N_CONTRACT")) fail("helper missing P3-T17 marker");
if (!helper.includes(marker)) fail("helper missing exact marker");
if (!helper.includes('"en"') || !helper.includes('"de"') || !helper.includes('"ar"')) fail("helper missing required locales");
if (!helper.includes("rtl")) fail("helper missing RTL direction");
if (!helper.includes("إدارة افتراضات التكلفة")) fail("helper missing Arabic title");
if (!helper.includes("Kostenannahmen verwalten")) fail("helper missing German title");
if (!helper.includes("p3T17ValidateCostI18nCatalog")) fail("helper missing validation function");

for (const key of [
  "v5.p3.cost.title",
  "v5.p3.cost.description",
  "v5.p3.cost.save",
]) {
  if (!v5.includes(key)) fail("v5I18n.tsx missing " + key);
}

if (!v5.includes('code: "ar"') || !v5.includes('direction: "rtl"')) {
  fail("v5I18n.tsx missing Arabic RTL locale contract");
}

if (!sql.includes(marker)) fail("SQL missing marker");
if (!sql.includes("CREATE TABLE IF NOT EXISTS public.ppiq_i18n_string_keys")) fail("SQL missing i18n string key table");
if (!sql.includes("CREATE TABLE IF NOT EXISTS public.ppiq_i18n_translations")) fail("SQL missing i18n translations table");
if (!sql.includes("ppiq_p3_t17_cost_i18n_status")) fail("SQL missing status function");
if (!sql.includes("v5-p3-cost-assumptions")) fail("SQL missing screen code");
if (!sql.includes("is_high_traffic")) fail("SQL missing high-traffic flag");
if (!sql.includes("إدارة افتراضات التكلفة")) fail("SQL missing Arabic translation");
if (!sql.includes("Kostenbänder speichern")) fail("SQL missing German save translation");

if (!test.includes("p3T17ValidateCostI18nCatalog")) fail("unit test missing catalog validation");
if (!test.includes("toHaveLength")) fail("unit test missing row-count proof");
if (!test.includes("rtl")) fail("unit test missing RTL proof");
if (!test.includes("v5.p3.cost.title")) fail("unit test missing qualified-key proof");

const frontendFiles = walk(full("Frontend/PlantProcess.Web/src"), (file) => file.endsWith(".tsx") || file.endsWith(".ts"));
const frontendJoined = frontendFiles.map((file) => fs.readFileSync(file, "utf8")).join("\\n");

if (!frontendJoined.includes('t("v5.p3.cost.title")')) {
  fail("no frontend screen uses t(\\"v5.p3.cost.title\\")");
}

if (!frontendJoined.includes('t("v5.p3.cost.description")')) {
  fail("no frontend screen uses t(\\"v5.p3.cost.description\\")");
}

if (!frontendJoined.includes("dir={locale.direction}")) {
  fail("frontend cost/i18n screen does not bind dir to locale.direction");
}

if (!frontendJoined.includes("/api/value/cost-assumptions")) {
  fail("cost-assumption UI/API wiring not found");
}

console.log("[GREEN] P3-T17 static validation passed.");
`);

write("docs/phase3/P3_T17_COST_I18N.md", `
# P3-T17 — Cost Assumption Management i18n + RTL Contract

Marker: ${marker}

## Result

P3-T17 certifies that the high-traffic Cost Assumption Management screen has a stable i18n contract.

## Scope

- Namespace: v5.p3.cost
- Screen code: v5-p3-cost-assumptions
- Required keys:
  - title
  - description
  - save
- Required locales:
  - en
  - de
  - ar
- RTL requirement:
  - Arabic locale must resolve direction = rtl.

## Installed files

- Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.ts
- Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.test.ts
- Backend/database/scripts/425_p3_t17_cost_assumption_i18n_contract.sql
- tools/phase3/validate-p3-t17-cost-i18n.cjs

## Database application

Local laptop main DB is native Windows PostgreSQL, not Docker. Apply manually only when DB proof is required:

    & "C:\\Program Files\\PostgreSQL\\16\\bin\\psql.exe" -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -f Backend\\database\\scripts\\425_p3_t17_cost_assumption_i18n_contract.sql

Then check:

    SELECT * FROM public.ppiq_p3_t17_cost_i18n_status();

## Validation

Run:

    node tools/phase3/validate-p3-t17-cost-i18n.cjs
    cd Frontend/PlantProcess.Web
    npm run build
    npx vitest run src/i18n/p3T17CostAssumptionI18n.test.ts --config vitest.config.ts
`);

console.log("[GREEN] P3-T17 patch applied.");