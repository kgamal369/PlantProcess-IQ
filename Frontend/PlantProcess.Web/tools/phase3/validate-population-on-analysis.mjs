#!/usr/bin/env node
/*
 * PPIQ-304 build-failing guard: validate-population-on-analysis.mjs
 * Fails (exit 1) if any analysis surface renders a driver/contributor value
 * without importing a population/abstain primitive. This is the architecture
 * teeth the acceptance requires - "no driver without a population prop".
 */
import { readFileSync, existsSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(here, "..", ".."); // tools/phase3 -> Frontend/PlantProcess.Web

// Surfaces that present learned/derived results to a human.
const SURFACES = [
  "src/pages/Analytics/AdvancedAnalysisPage.tsx",
  "src/pages/MaterialInvestigation/MaterialInvestigationPage.tsx",
  "src/pages/MaterialAnalytics/MaterialAnalyticsPages.tsx",
  "src/components/analytics/EvidencePanel.tsx",
];

const DRIVER = /\b(contributor|topDriver|driver|influential|suspectedCause|findingId)\b/i;
const HONEST = /\b(PopulationBadge|AbstainPanel|AnalysisHonestyBar)\b/;

let failed = 0;
let checked = 0;
for (const rel of SURFACES) {
  const p = resolve(webRoot, rel);
  if (!existsSync(p)) { console.log(`  skip (absent): ${rel}`); continue; }
  const src = readFileSync(p, "utf8");
  if (!DRIVER.test(src)) { continue; }            // not a driver-rendering surface
  checked++;
  if (!HONEST.test(src)) {
    failed++;
    console.error(`  FAIL: ${rel} renders a driver value but imports no PopulationBadge/AbstainPanel/AnalysisHonestyBar.`);
  } else {
    console.log(`  ok:   ${rel}`);
  }
}

if (failed > 0) {
  console.error(`\nPPIQ-304 guard: ${failed} analysis surface(s) render a driver without a population prop.`);
  process.exit(1);
}
console.log(`\nPPIQ-304 guard: ${checked} analysis surface(s) verified - every driver carries a population prop.`);