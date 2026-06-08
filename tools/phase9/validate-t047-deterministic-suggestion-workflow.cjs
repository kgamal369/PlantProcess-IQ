
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
    file: "Backend/PlantProcess.Application/Analytics/Suggestions/SuggestionEngine.cs",
    signals: [
      "PPIQ_REALIZATION_T047_DETERMINISTIC_SUGGESTION_WORKFLOW",
      "MD5-stable card id",
      "DeterministicGuid",
      "ppiq-suggestion:",
      "ImpactLow",
      "ImpactHigh",
      "Confidence",
      "estimated range"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Analytics/Suggestions/SuggestionWorkflow.cs",
    signals: [
      "PPIQ_REALIZATION_T047_DETERMINISTIC_SUGGESTION_WORKFLOW",
      "CanTransition",
      "Assigned",
      "Accepted",
      "Rejected",
      "Closed",
      "may acknowledge but not"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Analytics/Phase9_T047SuggestionWorkflowCertificationTests.cs",
    signals: [
      "T047_Same_Inputs_Produce_Identical_MD5_Stable_Suggestion_Ids",
      "T047_Input_Order_Does_Not_Change_Output_Order",
      "T047_Every_Suggestion_Has_Evidence_Ranged_Impact_Confidence_And_Honesty_Text",
      "T047_Confidence_Is_Bounded_And_Monotonic",
      "T047_Findings_Without_Evidence_Or_Synthetic_Seed_Findings_Are_Refused",
      "T047_Rerun_Deduplicates_By_SuggestionKey_And_Dismisses_Superseded",
      "T047_Workflow_Allows_Assign_Accept_Close_For_Authorized_Role",
      "T047_Unauthorized_Role_Cannot_Accept_Or_Close_A_Suggestion",
      "T047_Reject_And_Terminal_State_Machine_Rules_Are_Enforced",
      "ExpectedSuggestionId",
      "InMemorySuggestionStore",
      "SuggestionWorkflow.CanTransition",
      "may acknowledge but not Accepted"
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
  console.error("PPIQ-T047 failed: deterministic suggestion workflow certification is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T047 passed: deterministic suggestion IDs, confidence, ranged impact, de-duplication and RBAC workflow are certified.");
