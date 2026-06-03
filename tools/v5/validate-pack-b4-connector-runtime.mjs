import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) throw new Error(`Missing file: ${relativePath}`);
  return fs.readFileSync(full, "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function contains(label, text, needle) {
  ok(label, text.includes(needle), `found ${needle}`);
}

const sql = read("Backend/database/scripts/670_pack_b4_connector_runtime_certification.sql");
const endpoint = read("Backend/PlantProcess.Api/PlantConnectors/V5ConnectorRuntimeCertificationEndpoints.cs");
const program = read("Backend/PlantProcess.Api/Program.cs");
const docs = read("docs/connectors/P07_CONNECTOR_RUNTIME_CERTIFICATION.md");
const http = read("Documentation/v5/pack-b4-connector-runtime-requests.http");

contains("SQL runtime source table", sql, "ppiq_connector_runtime_sources");
contains("SQL runtime readings table", sql, "ppiq_connector_runtime_readings");
contains("SQL backfill checkpoint table", sql, "ppiq_connector_backfill_checkpoints");
contains("SQL truth state view", sql, "ppiq_v_connector_runtime_truth_state");
contains("SQL P07 acceptance function", sql, "ppiq_v5_p07_runtime_acceptance");
contains("SQL read-only check", sql, "ck_ppiq_connector_runtime_readonly");
contains("SQL deterministic mock historian seed", sql, "mock-historian-runtime");

contains("Endpoint health route", endpoint, "/health");
contains("Endpoint register source route", endpoint, "/mock-historian/register-source");
contains("Endpoint read window route", endpoint, "/mock-historian/read-window");
contains("Endpoint backfill run route", endpoint, "/backfill/run");
contains("Endpoint resume proof route", endpoint, "/backfill/resume-proof");
contains("Endpoint truth state update route", endpoint, "/truth-state");
contains("Endpoint truth state query route", endpoint, "/truth-state/{providerCode?}");
contains("Endpoint no write methods exposure", endpoint, "writeMethodsExposed = false");
contains("Endpoint read-only response", endpoint, "readOnly = true");
contains("Endpoint checkpoint resume function", endpoint, "LoadCheckpointAsync");
contains("Endpoint insert backfill function", endpoint, "InsertBackfillReadingsAsync");
contains("Endpoint event evidence function", endpoint, "WriteEventAsync");

contains("Program maps connector runtime endpoints", program, "MapV5ConnectorRuntimeCertificationEndpoints");

contains("Docs mention read-only", docs, "read-only");
contains("Docs mention checkpoint/resume", docs, "checkpoint/resume");
contains("Docs mention honest certification boundary", docs, "not a customer-certified OPC-UA/Aspen/PI adapter");

contains("HTTP examples include health", http, "/health");
contains("HTTP examples include read-window", http, "/mock-historian/read-window");
contains("HTTP examples include backfill", http, "/backfill/run");

console.log("");
console.log("Pack B4 connector runtime static validation passed.");