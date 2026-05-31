const fs = require("node:fs");
const path = require("node:path");

const frontendRoot = path.join(process.cwd(), "Frontend", "PlantProcess.Web");

const files = [
  "src/components/dashboard/DashboardGridLayout.tsx",
  "src/components/standard/ErrorBoundary.tsx",
  "src/state/DashboardGridLayoutContext.tsx",
  "src/utils/dragPerformance.ts"
];

const importLine = 'import { recordFrontendDiagnostic } from "@/utils/frontendDiagnostics";';

function fullPath(relativePath) {
  return path.join(frontendRoot, relativePath.replaceAll("/", path.sep));
}

function addImport(relativePath) {
  const file = fullPath(relativePath);

  if (!fs.existsSync(file)) {
    console.log(`Skipping missing file: ${relativePath}`);
    return;
  }

  let text = fs.readFileSync(file, "utf8");

  if (!text.includes("recordFrontendDiagnostic(")) {
    console.log(`No diagnostic calls found: ${relativePath}`);
    return;
  }

  if (text.includes(importLine)) {
    console.log(`Import already exists: ${relativePath}`);
    return;
  }

  const importMatches = [...text.matchAll(/^import[\s\S]*?;\s*$/gm)];

  if (importMatches.length === 0) {
    text = importLine + "\n" + text;
  } else {
    const last = importMatches[importMatches.length - 1];
    const insertAt = last.index + last[0].length;
    text = text.slice(0, insertAt) + "\n" + importLine + text.slice(insertAt);
  }

  fs.writeFileSync(file, text.replace(/\r\n/g, "\n"), "utf8");
  console.log(`Added diagnostic import: ${relativePath}`);
}

for (const file of files) {
  addImport(file);
}

console.log("P00A diagnostic imports fixed.");
