
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function file(relativePath) {
  return path.join(root, relativePath);
}

function exists(relativePath) {
  return fs.existsSync(file(relativePath));
}

function read(relativePath) {
  return fs.readFileSync(file(relativePath), "utf8");
}

const requiredFiles = [
  "tools/phase9/validate-t047-deterministic-suggestion-workflow.cjs",
  "tools/phase9/validate-t048-assistant-grounding-eval.cjs",
  "tools/phase9/validate-t049-model-gateway-serving-modes.cjs",
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/Phase9_T047SuggestionWorkflowCertificationTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T048AssistantGroundingEvalGateTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T049ModelGatewayServingModesTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T050AssistantRegressionSweepTests.cs"
];

for (const item of requiredFiles) {
  if (!exists(item)) {
    failures.push({ file: item, reason: "missing required Phase 09 artifact" });
  }
}

const checks = [
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T050AssistantRegressionSweepTests.cs",
    signals: [
      "PPIQ_REALIZATION_T050_PHASE9_AI_REGRESSION_SWEEP",
      "T050_Demo_Assistant_Answers_Approved_Question_With_Citation",
      "T050_Assistant_Blocks_Invented_Number_In_Demo_Response",
      "T050_SelfHosted_NoEgress_Gateway_Can_Feed_Grounded_Assistant_Demo",
      "T050_Eval_Gate_Fails_If_Assistant_Tries_Causal_Or_Value_Overclaim",
      "99,999",
      "root cause",
      "No outbound call was made",
      "GroundingCertified"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Assistant/AssistantGroundingEvalGate.cs",
    signals: [
      "PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE",
      "ForbiddenCausalOrValuePhrases",
      "Model version drift"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Assistant/ModelGateway/PrivateModelGatewayService.cs",
    signals: [
      "PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES",
      "SelfHostedNoEgress",
      "Tenant no-egress policy blocks"
    ]
  }
];

for (const check of checks) {
  if (!exists(check.file)) {
    failures.push({ file: check.file, reason: "missing marker file" });
    continue;
  }

  const text = read(check.file);
  for (const signal of check.signals) {
    if (!text.includes(signal)) {
      failures.push({ file: check.file, reason: "missing signal: " + signal });
    }
  }
}

if (failures.length) {
  console.error("PPIQ-T050 failed: Phase 09 AI regression sweep evidence is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T050 passed: Phase 09 AI regression sweep artifacts, eval gate, gateway controls and demo assistant checks are present.");
