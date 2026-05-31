const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

const requiredFiles = [
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/RiskScoreServiceTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/FeatureEngineeringServiceTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/MlReadinessServiceTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/QualityLabelBuilderServiceTests.cs",
  "Backend/tests/PlantProcess.Api.IntegrationTests/Analytics/MlLearningCoreIntegrationTests.cs",
  "Backend/tests/PlantProcess.Api.IntegrationTests/Security/AuthGateMatrixTests.cs",
  "Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Database/SqlScriptHygieneApplyTests.cs",
  "Backend/tests/PlantProcess.Api.IntegrationTests/OpenApi/OpenApiMlAndDynamicEndpointContractTests.cs"
];

const failures = [];

for (const file of requiredFiles) {
  const full = path.join(root, file);
  if (!fs.existsSync(full)) {
    failures.push("Missing required Pack B test file: " + file);
  }
}

const riskService = fs.readFileSync(
  path.join(root, "Backend/PlantProcess.Application/Analytics/Services/RiskScoreService.cs"),
  "utf8");

if (!riskService.includes("riskClass: riskClass,")) {
  failures.push("RiskScoreService does not pass computed riskClass into RiskScore constructor.");
}

if (riskService.includes("riskClass: command.RiskClass,")) {
  failures.push("RiskScoreService still passes command.RiskClass directly.");
}

const sql080 = fs.readFileSync(
  path.join(root, "Backend/database/scripts/080_phase_3_4_connector_schema_foundation.sql"),
  "utf8");

if (sql080.trim().length === 0) {
  failures.push("080 SQL placeholder is still empty.");
}

if (failures.length > 0) {
  console.error("P00B backend critical test validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("P00B backend critical test validation passed.");
console.log("Pack B test files exist and RiskScore/SQL hygiene fixes are present.");
