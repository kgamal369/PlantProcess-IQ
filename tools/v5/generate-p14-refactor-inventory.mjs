import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

const candidates = [
  "Backend/PlantProcess.Api/Endpoints/Workflow/Phase1WorkflowTruthEndpoints.cs",
  "Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs",
  "Backend/PlantProcess.Infrastructure/Services/ConnectorConfigurationService.cs",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Phase56Pages.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.tsx",
  "Backend/PlantProcess.Application/Services/DashboardDefinitionService.cs",
  "Frontend/PlantProcess.Web/src/components/widgets/WidgetBuilderWizard.tsx",
  "Frontend/PlantProcess.Web/src/api/plantProcessApi.ts"
];

function countLines(text) {
  return text.split(/\r?\n/).length;
}

function targetModule(file) {
  if (/connector|configuration/i.test(file)) return "Acquisition";
  if (/workflow/i.test(file)) return "Suggestions";
  if (/dashboard|widget/i.test(file)) return "Analytics";
  if (/license/i.test(file)) return "Licensing";
  if (/admin/i.test(file)) return "Demo/Admin";
  if (/plantProcessApi/i.test(file)) return "Feature API Clients";
  return "Canonical";
}

const rows = [];

for (const relative of candidates) {
  const full = path.join(root, relative);

  if (!fs.existsSync(full)) {
    rows.push({
      artifactPath: relative,
      exists: false,
      currentLineCount: 0,
      targetModule: targetModule(relative),
      legacyImportCount: 0,
      phaseNamedArtifact: /Phase|P03|P04|Pack3/i.test(relative),
      refactorStatus: "missing"
    });
    continue;
  }

  const text = fs.readFileSync(full, "utf8");

  rows.push({
    artifactPath: relative,
    exists: true,
    currentLineCount: countLines(text),
    targetModule: targetModule(relative),
    legacyImportCount: (text.match(/plantProcessApi/g) ?? []).length,
    phaseNamedArtifact: /Phase|P03|P04|Pack3/i.test(relative),
    refactorStatus: countLines(text) > 400 ? "inventory" : "complete"
  });
}

const outDir = path.join(root, "Documentation", "v5");
fs.mkdirSync(outDir, { recursive: true });

const outFile = path.join(outDir, "p14-refactor-inventory.json");
fs.writeFileSync(outFile, JSON.stringify(rows, null, 2), "utf8");

console.log(`P14 refactor inventory written to ${outFile}`);

for (const row of rows) {
  console.log(`${row.currentLineCount.toString().padStart(5)} ${row.refactorStatus.padEnd(12)} ${row.artifactPath}`);
}