const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function exists(file) {
  return fs.existsSync(path.join(root, file));
}

function read(file) {
  return fs.readFileSync(path.join(root, file), "utf8");
}

function check(file, signals) {
  if (!exists(file)) {
    failures.push("Missing file: " + file);
    return;
  }

  const text = read(file);
  for (const signal of signals) {
    if (!text.includes(signal)) {
      failures.push(file + " missing signal: " + signal);
    }
  }
}

check("Backend/PlantProcess.Application/Top15/Phase34/Top15Phase34Runtime.cs", [
  "PPIQ_TOP15_T209_REAL_ASSISTANT_MODEL",
  "PPIQ_TOP15_T210_RETRIEVAL_GROUNDING_CANONICAL_DATA",
  "PPIQ_TOP15_T211_ASSISTANT_REGRESSION_EVAL_GATE",
  "PPIQ_TOP15_T212_GROUNDING_ADVERSARIAL_REGRESSION",
  "PPIQ_TOP15_T213_ROI_REAL_INGESTED_OUTCOMES",
  "PPIQ_TOP15_T214_PERFORMANCE_LOAD_PROOF_VISION_SCALE",
  "PPIQ_TOP15_T215_OBSERVABILITY_SLO_SOAK_REGRESSION",
  "Top15RealAssistantModel",
  "Top15CanonicalRetrievalGrounder",
  "Top15GoldenQaGate",
  "Top15RealOutcomeRoiEngine",
  "Top15VisionScaleLoadProof",
  "Top15ObservabilitySloCatalog"
]);

check("Backend/tests/PlantProcess.Application.UnitTests/Top15/Top15Phase34RuntimeTests.cs", [
  "Top15Phase34RuntimeTests",
  "T209_Real_Model_Adapter_Uses_Configured_Model_And_Keeps_Grounding_Guard",
  "T210_Retrieval_Grounding_Returns_Five_Cited_EdgeCrack_Events_For_C0044170",
  "T211_Golden_Qa_Eval_Gate_Has_Twenty_Cases_And_Fails_On_Regression",
  "T212_Adversarial_Grounding_Abstains_On_Causal_Or_Control_Prompts",
  "T213_Roi_Engine_Uses_Canonical_Outcomes_And_Changes_When_Outcomes_Change",
  "T214_Performance_Load_Proof_Enforces_P95_Targets",
  "T215_Observability_Slo_Evaluates_Metrics_And_Fails_On_Critical_Error_Rate"
]);

check("scripts/top15/run-phase3-phase4-regression.ps1", [
  "Top15Phase34RuntimeTests",
  "PPIQ Top15 Phase 3/4 regression gate"
]);

if (exists("Backend/PlantProcess.Infrastructure/Assistant/AssistantInfrastructureExtensions.cs")) {
  const infra = read("Backend/PlantProcess.Infrastructure/Assistant/AssistantInfrastructureExtensions.cs");
  if (!infra.includes("Top15RealAssistantModel")) {
    failures.push("AssistantInfrastructureExtensions.cs missing Top15RealAssistantModel optional binding.");
  }
}

if (failures.length) {
  console.error("PPIQ Top15 Phase 3/4 validation failed.");
  for (const failure of failures) console.error(" - " + failure);
  process.exit(1);
}

console.log("PPIQ Top15 Phase 3/4 passed: T-209/T-210/T-211/T-212/T-213/T-214/T-215 implementation gates are present.");