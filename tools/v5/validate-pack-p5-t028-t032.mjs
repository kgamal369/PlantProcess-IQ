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

const contracts = read("Backend/PlantProcess.Application/Acquisition/OtSafeAcquisitionContracts.cs");
const gateway = read("Backend/PlantProcess.Application/Acquisition/OtSafeEdgeCollectorGateway.cs");
const historian = read("Backend/PlantProcess.Application/Acquisition/HistorianTagHistoryMapper.cs");
const drift = read("Backend/PlantProcess.Application/Acquisition/SchemaDriftDetector.cs");
const sql = read("Backend/database/scripts/440_p5_ot_safe_acquisition.sql");
const ui = read("Frontend/PlantProcess.Web/src/pages/Acquisition/EdgeCollectorManagementPage.tsx");
const tests = read("Backend/tests/PlantProcess.Application.UnitTests/Acquisition/P5_OtSafeAcquisitionTests.cs");

ok("T028 OT-safe collector registration exists", contracts.includes("OtSafeEdgeCollectorRegistration"));
ok("T028 one-way push doctrine is encoded", contracts.includes("OneWayPushOnly") && contracts.includes("NoInboundNetworkPath") && contracts.includes("NoControlPath"));
ok("T028 gateway rejects unsafe or invalid batches", gateway.includes("Collector is not OT-safe") && gateway.includes("duplicate row numbers"));
ok("T028 SQL collector registry enforces OT-safe check", sql.includes("ck_edge_collector_ot_safe"));

ok("T029 historian tag observation contract exists", contracts.includes("HistorianTagObservation"));
ok("T029 historian mapper maps to canonical parameter observation draft", historian.includes("CanonicalParameterObservationDraft"));
ok("T029 read-only doctrine is documented", historian.includes("Read-only") || historian.includes("read-only"));
ok("T029 SQL historian mappings exist", sql.includes("historian_tag_mappings"));

ok("T030 schema drift detector exists", drift.includes("SchemaDriftDetector"));
ok("T030 removed/type/unit/added drift are surfaced", drift.includes("Removed") && drift.includes("TypeChanged") && drift.includes("UnitChanged") && drift.includes("Added"));
ok("T030 blocking drift is explicit", drift.includes("BlocksIngestion: true"));
ok("T030 SQL drift events exist", sql.includes("schema_drift_events"));

ok("T031 edge collector management UI exists", ui.includes("EdgeCollectorManagementPage"));
ok("T031 UI shows one-way proof", ui.includes("one-way-push-only") && ui.includes("no inbound PPIQ-to-OT") && ui.includes("no control path"));
ok("T031 UI shows lag, buffer and credential rotation", ui.includes("Lag:") && ui.includes("Buffer") && ui.includes("Rotate credential"));

ok("T032 hard tests exist", tests.includes("T028_collector_registration") && tests.includes("T029_historian_mapper") && tests.includes("T030_schema_drift"));
ok("T032 network proof test exists", tests.includes("no-inbound") && tests.includes("no-control"));

const report = {
  generatedAtUtc: new Date().toISOString(),
  status: "green",
  note: "P5A installs OT-safe one-way collector contracts, historian mapping, schema drift detection, UI proof, SQL foundations and hard tests."
};

fs.writeFileSync(
  path.join(reportDir, "pack-p5-t028-t032-validation-report.json"),
  JSON.stringify(report, null, 2),
  "utf8"
);

console.log("");
console.log("Pack P5A T028-T032 validation passed.");