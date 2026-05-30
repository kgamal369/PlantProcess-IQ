const fs = require("node:fs");

function patch(file, updater) {
  let text = fs.readFileSync(file, "utf8");
  const next = updater(text);
  fs.writeFileSync(file, next, "utf8");
  console.log("Wrote " + file);
}

// 1) Fix Phase78 page to call the real API client method.
patch("src/pages/Phase78/Phase78Pages.tsx", (text) => {
  let output = text;

  output = output.replaceAll(
    "phase1WorkflowApi.getWorkflowTruth()",
    "phase1WorkflowApi.getConnectorTruth()"
  );

  // Make connector truth rows understand the actual ConnectorTruthMatrixResponse shape:
  // { generatedAtUtc, operatingRule, providers: [...] }
  output = output.replace(
    `(raw.connectorTruth as Row[] | undefined) ??
      (raw.connectors as Row[] | undefined) ??
      (raw.sourceConnectorTruth as Row[] | undefined) ??
      [];`,
    `(raw.providers as Row[] | undefined) ??
      (raw.connectorTruth as Row[] | undefined) ??
      (raw.connectors as Row[] | undefined) ??
      (raw.sourceConnectorTruth as Row[] | undefined) ??
      (raw.connectorTruthRows as Row[] | undefined) ??
      (raw.items as Row[] | undefined) ??
      [];`
  );

  return output;
});

// 2) Fix Phase78 validator so it accepts the correct API name.
patch("../../tools/phase78/validate-phase7-phase8-acceptance.cjs", (text) => {
  return text
    .replace(
      /phase1WorkflowApi\\\\\\.getWorkflowTruth/g,
      "phase1WorkflowApi\\\\.getConnectorTruth"
    )
    .replace(
      /phase1WorkflowApi\\\.getWorkflowTruth/g,
      "phase1WorkflowApi\\.getConnectorTruth"
    )
    .replaceAll(
      "phase1WorkflowApi.getWorkflowTruth",
      "phase1WorkflowApi.getConnectorTruth"
    );
});

// 3) Patch the generator too, so rerunning Phase78 generator does not bring back the wrong API name.
patch("../../tools/phase78/apply-phase7-phase8-full-implementation.cjs", (text) => {
  return text
    .replaceAll("phase1WorkflowApi.getWorkflowTruth()", "phase1WorkflowApi.getConnectorTruth()")
    .replaceAll("phase1WorkflowApi\\\\.getWorkflowTruth", "phase1WorkflowApi\\\\.getConnectorTruth")
    .replaceAll("phase1WorkflowApi\\.getWorkflowTruth", "phase1WorkflowApi\\.getConnectorTruth");
});

console.log("");
console.log("Fixed Phase78 connector truth API usage.");
