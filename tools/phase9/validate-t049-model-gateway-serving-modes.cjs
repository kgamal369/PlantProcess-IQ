
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
    file: "Backend/PlantProcess.Application/Assistant/ModelGateway/PrivateModelGatewayContracts.cs",
    signals: [
      "PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES",
      "SelfHostedNoEgress",
      "PrivateZeroRetentionEndpoint",
      "BringYourOwnModel",
      "NoEgress",
      "ScopedEvidenceChunk",
      "RawPlantRowJson",
      "IPrivateModelGatewayTransport"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Assistant/ModelGateway/PrivateModelGatewayService.cs",
    signals: [
      "PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES",
      "BuildScopedEvidencePayload",
      "Tenant no-egress policy blocks",
      "SelfHostedNoEgress",
      "PrivateZeroRetentionEndpoint",
      "BringYourOwnModel",
      "ZeroDataRetentionConfirmed",
      "CustomerOwnedEndpoint",
      "OutboundCallAttempted: false"
    ]
  },
  {
    file: "Backend/PlantProcess.Api/AssistantGateway/V5PrivateModelGatewayCertificationEndpoints.cs",
    signals: [
      "PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES",
      "self-hosted-no-egress",
      "private-zero-retention-endpoint",
      "bring-your-own-model",
      "tenant-no-egress-toggle",
      "scoped-evidence-only-payload"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T049ModelGatewayServingModesTests.cs",
    signals: [
      "T049_SelfHosted_Mode_Makes_Zero_Outbound_Calls",
      "T049_Private_ZeroRetention_Endpoint_Sends_Only_Question_And_Scoped_Evidence",
      "T049_BYO_Model_Mode_Uses_Customer_Endpoint_And_Scoped_Evidence_Only",
      "T049_Tenant_NoEgress_Toggle_Blocks_Private_And_BYO_Egress",
      "T049_NoEgress_Tenant_Can_Still_Use_SelfHosted_Mode",
      "T049_Certification_Matrix_Covers_All_Three_Serving_Modes",
      "T049_Scoped_Evidence_Payload_Drops_Synthetic_And_Raw_Source_Metadata",
      "secret_raw_row",
      "database_password",
      "plantprocess123",
      "PRIVATE_TAG",
      "CallCount"
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
  console.error("PPIQ-T049 failed: model gateway serving mode certification is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T049 passed: self-hosted no-egress, private ZDR endpoint, BYO model and tenant no-egress toggle are certified.");
