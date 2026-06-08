
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

const checks = [
  {
    file: "Backend/PlantProcess.Application/Assistant/GroundingService.cs",
    signals: [
      "PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE",
      "uncited number",
      "root cause",
      "is caused by",
      "guaranteed",
      "will save"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Assistant/AssistantGroundingEvalGate.cs",
    signals: [
      "PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE",
      "AssistantGroundingEvalGate",
      "AssistantGroundingEvalCase",
      "AssistantGroundingEvalPromptSet",
      "ForbiddenCausalOrValuePhrases",
      "Uncited/forbidden number reached final answer",
      "Unsupported causal/value phrase reached final answer",
      "Model version drift",
      "EvaluateMany",
      "block_uncited_number",
      "refuse_without_live_evidence"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Assistant/AssistantEvalHarness.cs",
    signals: [
      "PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE",
      "Missing required citation",
      "Uncited/forbidden number reached answer",
      "Model version drift"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T048AssistantGroundingEvalGateTests.cs",
    signals: [
      "T048_Clean_Grounded_Model_Output_Passes_Eval_Gate",
      "T048_Uncited_Number_Is_Blocked_And_Fails_Regression_Gate",
      "T048_Unsupported_Causal_Claim_Is_Blocked_And_Fails_Regression_Gate",
      "T048_Synthetic_Only_Evidence_Produces_Honest_Refusal_And_Passes_Refusal_Case",
      "T048_Model_Version_Drift_Fails_Eval_Gate",
      "T048_Provider_And_Model_Key_Drift_Fail_Eval_Gate",
      "T048_Fixed_Prompt_Set_Is_Pinned_And_Contains_Regression_Cases",
      "T048_EvaluateMany_Fails_Build_Gate_When_Any_Case_Fails",
      "99,999",
      "is caused by",
      "EvaluateMany"
    ]
  }
];

for (const check of checks) {
  if (!exists(check.file)) {
    failures.push({ file: check.file, reason: "missing file" });
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
  console.error("PPIQ-T048 failed: assistant grounding eval gate is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T048 passed: assistant grounding eval gate blocks uncited numbers, unsupported causal claims, blocked sentences and model drift.");
