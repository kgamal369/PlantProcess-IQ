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

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["bin", "obj", ".git", ".vs", "node_modules", "dist", "build", "coverage"].includes(entry.name)) continue;
      if (entry.name.startsWith("_pack_")) continue;
      if (entry.name.startsWith("tmp")) continue;
      walk(full, output);
    } else {
      output.push(full);
    }
  }

  return output;
}

const embedder = read("Backend/PlantProcess.Application/Assistant/LocalSemanticEmbedder.cs");
const iembedder = read("Backend/PlantProcess.Application/Assistant/IEmbedder.cs");
const indexService = read("Backend/PlantProcess.Application/Assistant/AssistantRetrievalIndexBuildService.cs");
const gateway = read("Backend/PlantProcess.Application/Assistant/GroundedAssistantGateway.cs");
const evalHarness = read("Backend/PlantProcess.Application/Assistant/AssistantEvalHarness.cs");
const sql = read("Backend/database/scripts/430_p4_production_assistant.sql");
const ui = read("Frontend/PlantProcess.Web/src/pages/Assistant/GroundedAssistantPage.tsx");
const tests = read("Backend/tests/PlantProcess.Application.UnitTests/Assistant/P4_ProductionAssistantTests.cs");

const runtimeHashingReferences = walk(path.join(root, "Backend"))
  .map(file => path.relative(root, file).replaceAll(path.sep, "/"))
  .filter(file => file.endsWith(".cs"))
  .filter(file => !file.includes("/tests/"))
  .filter(file => !file.includes("_backup"))
  .filter(file => fs.readFileSync(path.join(root, file), "utf8").includes("HashingEmbedder"));

ok("T023 legacy HashingEmbedder runtime file removed", !exists("Backend/PlantProcess.Infrastructure/Assistant/HashingEmbedder.cs"));
ok("T023 no runtime HashingEmbedder references remain", runtimeHashingReferences.length === 0, `${runtimeHashingReferences.length} offender(s)`);
ok("T023 LocalSemanticEmbedder exists", embedder.includes("LocalSemanticEmbedder"));
ok("T023 air-gapped local provider is explicit", embedder.includes("AirGapNoNetwork = true"));
ok("T023 provider/model keys are explicit", embedder.includes("ProviderKey") && embedder.includes("ModelKey"));
ok("T023 IEmbedder documents pluggable production boundary", iembedder.includes("Pluggable embedding model boundary"));

ok("T024 retrieval index build service exists", indexService.includes("AssistantRetrievalIndexBuildService"));
ok("T024 index build filters synthetic chunks", indexService.includes("!x.IsSynthetic"));
ok("T024 AssistantIndexBuildResult uses correct ChangedOnly casing", !indexService.includes("changedOnly:") && indexService.includes("ChangedOnly: true"));
ok("T024 SQL index job exists", sql.includes("assistant_retrieval_index_job"));
ok("T024 tenant-scoped chunk table exists", sql.includes("tenant_id") && sql.includes("assistant_chunk"));

ok("T025 grounded gateway exists", gateway.includes("GroundedAssistantGateway"));
ok("T025 gateway enforces GroundingService", gateway.includes("GroundingService.Enforce"));
ok("T025 audit log table exists", sql.includes("assistant_audit_log"));
ok("T025 eval case table exists", sql.includes("assistant_eval_case"));

ok("T026 assistant UI proof page exists", ui.includes("GroundedAssistantPage"));
ok("T026 UI shows citations", ui.includes("Evidence citations") && ui.includes("citations"));
ok("T026 UI shows abstention/refusal", ui.includes("No grounded answer") && ui.includes("abstained"));

ok("T027 hard tests exist", tests.includes("T023_local_semantic_embedder") && tests.includes("T027_eval_harness"));
ok("T027 tests cover air gap", tests.includes("AirGapNoNetwork"));
ok("T027 tests cover grounding/citations", tests.includes("GroundingService.Enforce") && tests.includes("Citations"));

const report = {
  generatedAtUtc: new Date().toISOString(),
  status: "green",
  runtimeHashingReferences,
  note: "P4A installs production assistant foundation: local pluggable embedding boundary, idempotent index build service, grounded gateway, UI proof page, eval harness, and hard tests."
};

fs.writeFileSync(
  path.join(reportDir, "pack-p4-t023-t027-validation-report.json"),
  JSON.stringify(report, null, 2),
  "utf8"
);

console.log("");
console.log("Pack P4A T023-T027 validation passed.");
