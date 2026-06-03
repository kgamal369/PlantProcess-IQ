import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

const scriptExtensions = new Set([".ps1", ".sh", ".mjs", ".cjs", ".js"]);
const ignored = [
  /node_modules/i,
  /dist/i,
  /build/i,
  /storybook-static/i,
  /_.*backup/i,
  /Documentation[\\/]UltimateAudit/i
];

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    const relative = path.relative(root, full);

    if (ignored.some((pattern) => pattern.test(relative))) continue;

    if (entry.isDirectory()) {
      walk(full, output);
    } else if (scriptExtensions.has(path.extname(entry.name))) {
      output.push(relative);
    }
  }

  return output;
}

const files = walk(root);
const findings = [];

for (const relative of files) {
  const full = path.join(root, relative);
  const text = fs.readFileSync(full, "utf8");

  if (relative.endsWith(".ps1")) {
    if (!text.includes("$ErrorActionPreference") || !text.includes('"Stop"')) {
      findings.push({ file: relative, issue: "PowerShell script missing $ErrorActionPreference = Stop" });
    }

    if (/Write-Host\s+["'][^"']*(passed|success|green|completed)[^"']*["']/i.test(text) &&
        !/LASTEXITCODE|throw|Run-Checked|try\s*\{/.test(text)) {
      findings.push({ file: relative, issue: "PowerShell success print may not be exit-code guarded" });
    }
  }

  if (relative.endsWith(".sh")) {
    if (!/set\s+-euo\s+pipefail/.test(text)) {
      findings.push({ file: relative, issue: "Shell script missing set -euo pipefail" });
    }
  }

  if (relative.endsWith(".mjs") || relative.endsWith(".cjs") || relative.endsWith(".js")) {
    if (/console\.log\([^)]*(passed|success|green)/i.test(text) &&
        !/throw new Error|process\.exit\([1-9]/.test(text)) {
      findings.push({ file: relative, issue: "Node validation script prints success without clear failure path" });
    }
  }
}

const reportDir = path.join(root, "Documentation", "v5");
fs.mkdirSync(reportDir, { recursive: true });
const reportPath = path.join(reportDir, "strict-validation-integrity-findings.json");
fs.writeFileSync(reportPath, JSON.stringify(findings, null, 2), "utf8");

if (findings.length > 0) {
  console.error("");
  console.error("Strict validation integrity scan found issues.");
  console.error(`Finding count: ${findings.length}`);
  console.error(`Report: ${reportPath}`);
  console.error("");
  for (const finding of findings.slice(0, 60)) {
    console.error(` - ${finding.file}: ${finding.issue}`);
  }
  console.error("");
  console.error("This is a quality gate report. Fix or explicitly whitelist before claiming full CI integrity green.");
  process.exit(2);
}

console.log("OK strict validation integrity: scripts have guarded failure paths.");