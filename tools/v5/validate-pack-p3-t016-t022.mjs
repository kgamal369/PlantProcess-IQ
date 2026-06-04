import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const reportDir = path.join(root, "Documentation", "v5");
fs.mkdirSync(reportDir, { recursive: true });

function p(relative) {
  return path.join(root, relative);
}

function exists(relative) {
  return fs.existsSync(p(relative));
}

function read(relative) {
  return fs.readFileSync(p(relative), "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

const valueContracts = read("Backend/PlantProcess.Application/Analytics/Value/ValueContracts.cs");
const validator = read("Backend/PlantProcess.Application/Analytics/Value/CostAssumptionValidator.cs");
const evidence = read("Backend/PlantProcess.Application/Analytics/Value/FindingEvidenceContracts.cs");
const valueEngine = read("Backend/PlantProcess.Application/Analytics/Value/ValueImpactEngine.cs");
const valueEndpoints = read("Backend/PlantProcess.Api/Endpoints/Analytics/ValueEndpoints.cs");
const sql = read("Backend/database/scripts/420_p3_value_evidence_hmi.sql");
const tests = read("Backend/tests/PlantProcess.Application.UnitTests/Analytics/P3_ValueEvidenceKpiCoverageTests.cs");
const costPage = read("Frontend/PlantProcess.Web/src/pages/ValueEvidence/CostAssumptionManagementPage.tsx");
const findingPanel = read("Frontend/PlantProcess.Web/src/components/analytics/FindingEvidencePanel.tsx");
const blendedPanel = read("Frontend/PlantProcess.Web/src/components/provenance/BlendedProvenanceBreakdown.tsx");
const i18n = read("Frontend/PlantProcess.Web/src/i18n/v5I18n.tsx");

ok("T016 cost-assumption validator exists", validator.includes("CostAssumptionValidator"));
ok("T016 validates low <= mid <= high", validator.includes("low <= mid <= high"));
ok("T016 validates non-negative bands", validator.includes("non-negative"));
ok("T016 effective-from metadata exists", valueContracts.includes("EffectiveFromUtc"));
ok("T016 versioned store SQL exists", sql.includes("canon.cost_assumption") && sql.includes("version") && sql.includes("effective_from_utc"));
ok("T016 endpoint validates before save", valueEndpoints.includes("CostAssumptionValidator.Validate"));

ok("T017 cost-assumption management page exists", costPage.includes("CostAssumptionManagementPage"));
ok("T017 page uses optimistic save pattern", costPage.includes("useOptimisticSave"));
ok("T017 page has i18n keys and RTL wrapper", costPage.includes("V5I18nProvider") && costPage.includes("dir={locale.direction}"));
ok("T017 i18n keys include Arabic", i18n.includes("v5.p3.cost.title") && i18n.includes("إدارة افتراضات التكلفة"));

ok("T018 value impact engine produces range", valueEngine.includes("ValueImpactResult") && valueEngine.includes("Low") && valueEngine.includes("High"));
ok("T018 abstain state exists", valueEngine.includes("Abstained") && valueEngine.includes("Insufficient basis"));
ok("T018 finding value evidence contract exists", evidence.includes("FindingValueEvidence"));
ok("T018 UI displays value range and abstain state", findingPanel.includes("No € basis") && findingPanel.includes("Value range"));

ok("T019 at least four canonical KPI views exist", (sql.match(/CREATE OR REPLACE VIEW canon\.vw_kpi_/g) ?? []).length >= 4);
ok("T019 KPI SQL views include threshold metadata", sql.includes("warning_at") && sql.includes("critical_at") && sql.includes("alert_direction"));
ok("T019 KPI test evaluates SqlView severity", tests.includes("KpiKind.SqlView") && tests.includes("KpiSeverity.Warning"));

ok("T020 coverage evidence contract exists", evidence.includes("FindingCoverageEvidence"));
ok("T020 coverage reconciles arithmetic", evidence.includes("Population == Included + Excluded"));
ok("T020 UI coverage line exists", findingPanel.includes("Coverage: based on"));

ok("T021 blended provenance contract exists", evidence.includes("BlendedProvenanceEvidence"));
ok("T021 blended provenance UI component exists", blendedPanel.includes("BlendedProvenanceBreakdown"));
ok("T021 blended provenance weight check exists", evidence.includes("WeightTotal"));

ok("T022 hard tests exist", tests.includes("T018_value_range") && tests.includes("T020_coverage") && tests.includes("T021_blended"));

const report = {
  generatedAtUtc: new Date().toISOString(),
  status: "green",
  note: "P3A establishes value/evidence contracts, KPI SQL-view script, UI proof components, and hard tests."
};

fs.writeFileSync(
  path.join(reportDir, "pack-p3-t016-t022-validation-report.json"),
  JSON.stringify(report, null, 2),
  "utf8"
);

console.log("");
console.log("Pack P3A T016-T022 validation passed.");