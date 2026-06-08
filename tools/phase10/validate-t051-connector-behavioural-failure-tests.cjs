const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

const checks = [
  {
    file: "Backend/PlantProcess.Application/Connectors/Certification/ConnectorBehaviourCertification.cs",
    signals: [
      "PPIQ_REALIZATION_T051_CONNECTOR_BEHAVIOURAL_FAILURE_TESTS",
      "ConnectorBehaviourCertificationService",
      "ConnectorCertificationResult Success",
      "ConnectorCertificationResult Failure",
      "TestBeforeSave",
      "MaskProfile",
      "SourceShapedStagingTable",
      "ImportBatchLifecycle",
      "FailedRolledBack",
      "bad-credential",
      "malformed-source-row",
      "Csv",
      "Excel",
      "SqlServer",
      "MySql"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Connectors/Phase10_T051ConnectorBehaviouralFailureTests.cs",
    signals: [
      "T051_Connector_TestBeforeSave_Then_Commits_SourceShaped_Staging",
      "T051_ReadBack_Profile_Masks_Secrets_And_Does_Not_Leak_Raw_Credential",
      "T051_Bad_Credential_Fails_Before_Save_And_Rolls_Back_Staging",
      "T051_Malformed_Input_Fails_Import_And_Rolls_Back_Entire_Batch",
      "T051_Certification_Matrix_Covers_Csv_Excel_MsSql_And_MySql",
      "T051_Source_Shaped_Staging_Does_Not_Convert_To_Canonical_Inside_Connector"
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

if (failures.length > 0) {
  console.error("PPIQ-T051 failed: connector behavioural/failure certification is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T051 passed: CSV, Excel, MSSQL and MySQL connector behavioural/failure certification is present.");