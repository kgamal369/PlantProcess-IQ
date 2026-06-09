const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function read(file) {
  return fs.readFileSync(path.join(root, file), "utf8");
}

function check(file, signals) {
  const full = path.join(root, file);
  if (!fs.existsSync(full)) {
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

check("Backend/PlantProcess.Application/AssistantRuntime/Phase8SuggestionOutcomeLoop.cs", [
  "PPIQ_REALIZATION_T048_SUGGESTION_OUTCOME_CLOSED_LOOP",
  "does not prove causation",
  "not causal attribution",
  "CausalClaimMade",
  "BuildValueLoop"
]);

check("Backend/PlantProcess.Application/AssistantRuntime/Phase8AssistantRegressionEvalGate.cs", [
  "PPIQ_REALIZATION_T049_ASSISTANT_EVAL_REGRESSION_GATE",
  "GoldenCases",
  "BlockedByPhase8AssistantEvalGate",
  "Uncited number"
]);

check("Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase8SuggestionOutcomeLoopTests.cs", [
  "Phase8SuggestionOutcomeLoopTests",
  "T048_Record_Action_Outcome_Without_Causal_Claim"
]);

check("Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase8AssistantRegressionEvalGateTests.cs", [
  "Phase8AssistantRegressionEvalGateTests",
  "T049_Golden_Candidates_Pass_Regression_Gate"
]);

check("scripts/ci/run-phase8-assistant-eval-gate.ps1", [
  "Phase8AssistantRegressionEvalGateTests",
  "PPIQ Phase 8 Assistant Eval Regression Gate"
]);

check("Backend/PlantProcess.Api/Program.cs", [
  "MapPhase8SuggestionOutcomeLoopEndpoints"
]);

if (failures.length > 0) {
  console.error("PPIQ Phase 8 T-048/T-049 validation failed.");
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("PPIQ Phase 8 T-048/T-049 passed: outcome loop and eval gate files are present.");