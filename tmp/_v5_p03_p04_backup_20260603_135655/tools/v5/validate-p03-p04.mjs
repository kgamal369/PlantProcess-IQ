import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) {
    throw new Error(`Missing required file: ${relativePath}`);
  }
  return fs.readFileSync(full, "utf8");
}

function assertContains(label, text, needle) {
  if (!text.includes(needle)) {
    throw new Error(`${label} missing: ${needle}`);
  }
  console.log(`✓ ${label}: ${needle}`);
}

const p03Sql = read("Backend/database/scripts/520_v5_p03_timeseries_foundation.sql");
const p04Sql = read("Backend/database/scripts/530_v5_p04_assistant_model_gateway_boundary.sql");
const telemetry = read("Backend/PlantProcess.Infrastructure/TimeSeries/TelemetryIngestionWorker.cs");
const assistantGateway = read("Backend/PlantProcess.Api/AssistantGateway/V5AssistantGateway.cs");
const infraDi = read("Backend/PlantProcess.Infrastructure/DependencyInjection.cs");
const program = read("Backend/PlantProcess.Api/Program.cs");

assertContains("P03 Timescale/fallback", p03Sql, "timescaledb");
assertContains("P03 Hypertable attempt", p03Sql, "create_hypertable");
assertContains("P03 Native fallback", p03Sql, "native_partition_fallback");
assertContains("P03 Rollups", p03Sql, "ppiq_telemetry_rollup_hourly");
assertContains("P03 Rollups", p03Sql, "ppiq_telemetry_rollup_daily");
assertContains("P03 Compression/retention policy", p03Sql, "ppiq_time_series_policy");
assertContains("P03 Acceptance", p03Sql, "ppiq_v5_p03_timeseries_acceptance");
assertContains("P03 Bounded channel", telemetry, "BoundedChannelOptions");
assertContains("P03 Backpressure", telemetry, "FullMode = BoundedChannelFullMode.Wait");
assertContains("P03 Binary COPY", telemetry, "BeginBinaryImportAsync");
assertContains("P03 Metrics", telemetry, "ppiq_telemetry_ingestion_metrics");
assertContains("P03 DI", infraDi, "ITelemetryIngestionQueue");

assertContains("P04 Provider config", p04Sql, "ppiq_assistant_provider_configs");
assertContains("P04 Redaction policies", p04Sql, "ppiq_assistant_redaction_policies");
assertContains("P04 Audit log", p04Sql, "ppiq_assistant_audit_log");
assertContains("P04 Eval cases", p04Sql, "ppiq_assistant_eval_cases");
assertContains("P04 Model pins", p04Sql, "ppiq_assistant_model_pins");
assertContains("P04 Acceptance", p04Sql, "ppiq_v5_p04_acceptance");
assertContains("P04 Gateway service", assistantGateway, "V5AssistantGatewayService");
assertContains("P04 Self-hosted/OpenAI compatible", assistantGateway, "OpenAiCompatibleAssistantProvider");
assertContains("P04 Redactor", assistantGateway, "V5AssistantRedactor");
assertContains("P04 Grounding guard", assistantGateway, "V5AssistantGroundingGuard");
assertContains("P04 Audit write", assistantGateway, "ppiq_assistant_audit_log");
assertContains("P04 API route", assistantGateway, "/api/v5/assistant-gateway");
assertContains("P04 Program registration", program, "AddV5AssistantGateway");
assertContains("P04 Program endpoints", program, "MapV5AssistantGatewayEndpoints");

const forbidden = [
  "guaranteed root cause",
  "root cause guaranteed",
  "fix this and save exactly"
];

const combined = `${assistantGateway}\n${p04Sql}`.toLowerCase();

for (const phrase of forbidden) {
  if (combined.includes(`"${phrase}"`) || combined.includes(`>${phrase}<`)) {
    throw new Error(`Forbidden commercial/AI phrase appears as a claim: ${phrase}`);
  }
}

console.log("");
console.log("P03/P04 Doctrine v5 static acceptance passed.");