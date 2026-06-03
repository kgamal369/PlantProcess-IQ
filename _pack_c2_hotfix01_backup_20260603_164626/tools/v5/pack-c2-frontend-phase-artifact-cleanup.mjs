import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");
const srcRoot = path.join(frontendRoot, "src");
const reportDir = path.join(root, "Documentation", "v5");

fs.mkdirSync(reportDir, { recursive: true });

function exists(file) {
  return fs.existsSync(file);
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, text) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, text, "utf8");
}

function toPosix(value) {
  return value.replaceAll(path.sep, "/");
}

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["node_modules", "dist", "build", "coverage", ".vite", "bin", "obj"].includes(entry.name)) continue;
      walk(full, output);
    } else if (/\.(ts|tsx|js|jsx|css)$/.test(entry.name)) {
      output.push(full);
    }
  }

  return output;
}

function moveFile(oldRelative, newRelative) {
  const oldPath = path.join(root, oldRelative);
  const newPath = path.join(root, newRelative);

  if (!exists(oldPath)) {
    return { oldRelative, newRelative, moved: false, reason: "source-missing" };
  }

  if (exists(newPath)) {
    return { oldRelative, newRelative, moved: false, reason: "target-exists" };
  }

  fs.mkdirSync(path.dirname(newPath), { recursive: true });
  fs.renameSync(oldPath, newPath);

  // Remove empty parent folders upward until src root boundary.
  let parent = path.dirname(oldPath);
  while (parent.startsWith(srcRoot) && parent !== srcRoot) {
    if (fs.existsSync(parent) && fs.readdirSync(parent).length === 0) {
      fs.rmdirSync(parent);
      parent = path.dirname(parent);
    } else {
      break;
    }
  }

  return { oldRelative, newRelative, moved: true, reason: "moved" };
}

const moves = [
  {
    old: "Frontend/PlantProcess.Web/src/pages/Admin/Phase1WorkflowTruthPanel.tsx",
    next: "Frontend/PlantProcess.Web/src/pages/Admin/WorkflowTruthPanel.tsx"
  },
  {
    old: "Frontend/PlantProcess.Web/src/pages/Phase56/Phase56Pages.tsx",
    next: "Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.tsx"
  },
  {
    old: "Frontend/PlantProcess.Web/src/pages/Phase78/Phase78Pages.tsx",
    next: "Frontend/PlantProcess.Web/src/pages/DemoAnalytics/DemoAnalyticsPages.tsx"
  },
  {
    old: "Frontend/PlantProcess.Web/src/pages/Phase910/Phase910Pages.tsx",
    next: "Frontend/PlantProcess.Web/src/pages/CommercialReadiness/CommercialReadinessPages.tsx"
  },
  {
    old: "Frontend/PlantProcess.Web/src/pages/Phase1112/Phase1112Pages.tsx",
    next: "Frontend/PlantProcess.Web/src/pages/ActionInternationalization/ActionInternationalizationPages.tsx"
  },
  {
    old: "Frontend/PlantProcess.Web/src/pages/Phase1314/Phase1314Pages.tsx",
    next: "Frontend/PlantProcess.Web/src/pages/DeploymentCompliance/DeploymentCompliancePages.tsx"
  },
  {
    old: "Frontend/PlantProcess.Web/src/components/phase2/SaveInspectionJobModal.tsx",
    next: "Frontend/PlantProcess.Web/src/components/inspection/SaveInspectionJobModal.tsx"
  }
];

const moveResults = moves.map((item) => moveFile(item.old, item.next));

const replacements = [
  ["Phase1WorkflowTruthPanel", "WorkflowTruthPanel"],
  ["./Phase1WorkflowTruthPanel", "./WorkflowTruthPanel"],
  ["@/pages/Admin/Phase1WorkflowTruthPanel", "@/pages/Admin/WorkflowTruthPanel"],

  ["Phase56Pages", "MaterialAnalyticsPages"],
  ["./Phase56/Phase56Pages", "./MaterialAnalytics/MaterialAnalyticsPages"],
  ["../Phase56/Phase56Pages", "../MaterialAnalytics/MaterialAnalyticsPages"],
  ["@/pages/Phase56/Phase56Pages", "@/pages/MaterialAnalytics/MaterialAnalyticsPages"],
  ["pages/Phase56/Phase56Pages", "pages/MaterialAnalytics/MaterialAnalyticsPages"],

  ["Phase78Pages", "DemoAnalyticsPages"],
  ["./Phase78/Phase78Pages", "./DemoAnalytics/DemoAnalyticsPages"],
  ["../Phase78/Phase78Pages", "../DemoAnalytics/DemoAnalyticsPages"],
  ["@/pages/Phase78/Phase78Pages", "@/pages/DemoAnalytics/DemoAnalyticsPages"],
  ["pages/Phase78/Phase78Pages", "pages/DemoAnalytics/DemoAnalyticsPages"],

  ["Phase910Pages", "CommercialReadinessPages"],
  ["./Phase910/Phase910Pages", "./CommercialReadiness/CommercialReadinessPages"],
  ["../Phase910/Phase910Pages", "../CommercialReadiness/CommercialReadinessPages"],
  ["@/pages/Phase910/Phase910Pages", "@/pages/CommercialReadiness/CommercialReadinessPages"],

  ["Phase1112Pages", "ActionInternationalizationPages"],
  ["./Phase1112/Phase1112Pages", "./ActionInternationalization/ActionInternationalizationPages"],
  ["../Phase1112/Phase1112Pages", "../ActionInternationalization/ActionInternationalizationPages"],
  ["@/pages/Phase1112/Phase1112Pages", "@/pages/ActionInternationalization/ActionInternationalizationPages"],

  ["Phase1314Pages", "DeploymentCompliancePages"],
  ["./Phase1314/Phase1314Pages", "./DeploymentCompliance/DeploymentCompliancePages"],
  ["../Phase1314/Phase1314Pages", "../DeploymentCompliance/DeploymentCompliancePages"],
  ["@/pages/Phase1314/Phase1314Pages", "@/pages/DeploymentCompliance/DeploymentCompliancePages"],

  ["@/components/phase2/SaveInspectionJobModal", "@/components/inspection/SaveInspectionJobModal"],
  ["../phase2/SaveInspectionJobModal", "../inspection/SaveInspectionJobModal"],
  ["./phase2/SaveInspectionJobModal", "./inspection/SaveInspectionJobModal"],
  ["components/phase2/SaveInspectionJobModal", "components/inspection/SaveInspectionJobModal"]
];

const changedFiles = [];

for (const file of walk(srcRoot)) {
  let text = read(file);
  const original = text;

  for (const [from, to] of replacements) {
    text = text.split(from).join(to);
  }

  if (text !== original) {
    write(file, text);
    changedFiles.push(toPosix(path.relative(root, file)));
  }
}

// Safe duplicate wizard retirement.
// In old audits there was a huge root-level components/dashboard/WidgetBuilderWizard.tsx
// and a proper product wrapper under components/dashboard/widget-builder.
const rootWizard = path.join(
  srcRoot,
  "components",
  "dashboard",
  "WidgetBuilderWizard.tsx"
);

const nestedWizard = path.join(
  srcRoot,
  "components",
  "dashboard",
  "widget-builder",
  "WidgetBuilderWizard.tsx"
);

let rootWizardRetired = false;

if (exists(rootWizard) && exists(nestedWizard)) {
  const lineCount = read(rootWizard).split(/\r?\n/).length;

  if (lineCount > 80) {
    write(
      rootWizard,
      `import type { ComponentProps } from "react";
import { WidgetBuilderWizard as ProductWidgetBuilderWizard } from "./widget-builder/WidgetBuilderWizard";

export type WidgetBuilderWizardProps = ComponentProps<typeof ProductWidgetBuilderWizard>;

export function WidgetBuilderWizard(props: WidgetBuilderWizardProps) {
  return <ProductWidgetBuilderWizard {...props} />;
}
`
    );
    changedFiles.push(toPosix(path.relative(root, rootWizard)));
    rootWizardRetired = true;
  }
}

// Collect remaining phase path offenders.
// This checks file/folder/module names only. Human-facing text/content is reported for C4.
const pathOffenders = [];

for (const file of walk(srcRoot)) {
  const relative = toPosix(path.relative(root, file));

  if (/(^|\/)(Phase\d+|phase\d+|P\d{2}P\d{2}|P03P04)(\/|\.|$)/.test(relative)) {
    pathOffenders.push(relative);
  }
}

const importOffenders = [];

for (const file of walk(srcRoot)) {
  const relative = toPosix(path.relative(root, file));
  const text = read(file);

  const patterns = [
    "Phase1WorkflowTruthPanel",
    "Phase56Pages",
    "Phase78Pages",
    "Phase910Pages",
    "Phase1112Pages",
    "Phase1314Pages",
    "@/components/phase2",
    "/phase2/"
  ];

  for (const pattern of patterns) {
    if (text.includes(pattern)) {
      importOffenders.push({
        file: relative,
        pattern
      });
    }
  }
}

const contentFindings = [];

for (const file of walk(srcRoot)) {
  const relative = toPosix(path.relative(root, file));
  const text = read(file);
  const matches = text.match(/\b(Phase\d+|phase\d+|P\d{2}P\d{2}|P03P04)\b/g) ?? [];

  if (matches.length > 0) {
    contentFindings.push({
      file: relative,
      matches: [...new Set(matches)].sort()
    });
  }
}

const report = {
  generatedAtUtc: new Date().toISOString(),
  moveResults,
  changedFiles: [...new Set(changedFiles)].sort(),
  rootWizardRetired,
  pathOffenders,
  importOffenders,
  contentFindings,
  note: "Pack C2 fails only path/import offenders. Content findings are C4 cleanup after backend/frontend refactor."
};

write(
  path.join(reportDir, "pack-c2-frontend-phase-artifact-cleanup-report.json"),
  JSON.stringify(report, null, 2)
);

console.log(JSON.stringify({
  moveResults,
  changedFiles: [...new Set(changedFiles)].sort(),
  rootWizardRetired,
  pathOffenders: pathOffenders.length,
  importOffenders: importOffenders.length,
  contentFindings: contentFindings.length,
  report: "Documentation/v5/pack-c2-frontend-phase-artifact-cleanup-report.json"
}, null, 2));

if (pathOffenders.length > 0 || importOffenders.length > 0) {
  console.error("Pack C2 frontend phase artifact cleanup still has path/import offenders.");
  process.exit(3);
}

console.log("[pack-c2] frontend phase artifact cleanup completed.");