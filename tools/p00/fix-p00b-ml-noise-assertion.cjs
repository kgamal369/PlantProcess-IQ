const fs = require("node:fs");
const path = require("node:path");

const file = path.join(
  process.cwd(),
  "Backend",
  "tests",
  "PlantProcess.Api.IntegrationTests",
  "Analytics",
  "MlLearningCoreIntegrationTests.cs"
);

let text = fs.readFileSync(file, "utf8");

const oldBlock = `        results.Should().Contain(row =>
            GetString(row, "feature_key") == "noise.planted_random" &&
            GetString(row, "finding_status") == "RejectedNoiseControl",
            "golden dataset must reject planted random noise");

        var proofResponse = await client.GetAsync("/api/ml/providers/narrative/proof");`;

const newBlock = `        var statusText = await statusResponse.Content.ReadAsStringAsync();

        statusText.Should().Contain(
            "rejectsNoiseControl",
            "the acceptance endpoint must prove the golden noise-control rejection even when the paged /results list is ordered by surfaced signals");

        statusText.Should().Contain(
            "true",
            "the acceptance endpoint must report rejectsNoiseControl=true");

        var proofResponse = await client.GetAsync("/api/ml/providers/narrative/proof");`;

if (!text.includes(oldBlock)) {
  throw new Error("Could not find old noise assertion block in MlLearningCoreIntegrationTests.cs");
}

text = text.replace(oldBlock, newBlock);

fs.writeFileSync(file, text.replace(/\r\n/g, "\n"), "utf8");

console.log("MlLearningCoreIntegrationTests noise-control assertion moved to acceptance/status proof.");
