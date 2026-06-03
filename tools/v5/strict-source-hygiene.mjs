import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

const blockedPathPatterns = [
  /(^|[\\/])storybook-static([\\/]|$)/i,
  /(^|[\\/])dist([\\/]|$)/i,
  /(^|[\\/])build([\\/]|$)/i,
  /(^|[\\/])coverage([\\/]|$)/i,
  /(^|[\\/])playwright-report([\\/]|$)/i,
  /(^|[\\/])test-results([\\/]|$)/i,
  /(^|[\\/])node_modules([\\/]|$)/i,
  /(^|[\\/])_.*backup.*([\\/]|$)/i,
  /(^|[\\/])_phase.*([\\/]|$)/i,
  /(^|[\\/])_v5_.*([\\/]|$)/i,
  /(^|[\\/])_p\d+_.*([\\/]|$)/i,
  /\.bak($|_)/i,
  /\.fixbak$/i,
  /UltimateAudit_\d{2}[A-Za-z]{3}\d{4}/i
];

const allowedGeneratedRoots = [
  ".git",
  ".audit-cache"
];

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    const relative = path.relative(root, full);

    if (allowedGeneratedRoots.some((x) => relative === x || relative.startsWith(`${x}${path.sep}`))) {
      continue;
    }

    if (entry.isDirectory()) {
      walk(full, output);
    } else {
      output.push(relative);
    }
  }

  return output;
}

const allFiles = walk(root);
const offenders = allFiles.filter((file) => blockedPathPatterns.some((pattern) => pattern.test(file)));

const reportDir = path.join(root, "Documentation", "v5");
fs.mkdirSync(reportDir, { recursive: true });

const reportPath = path.join(reportDir, "strict-source-hygiene-offenders.json");
fs.writeFileSync(reportPath, JSON.stringify(offenders, null, 2), "utf8");

if (offenders.length > 0) {
  console.error("");
  console.error("Strict source hygiene failed.");
  console.error(`Offender count: ${offenders.length}`);
  console.error(`Report: ${reportPath}`);
  console.error("");
  console.error("Top offenders:");
  for (const offender of offenders.slice(0, 50)) {
    console.error(` - ${offender}`);
  }
  console.error("");
  console.error("These files should be removed from tracking/audit exports, not necessarily deleted immediately.");
  process.exit(2);
}

console.log("OK strict source hygiene: no backup/generated offenders found.");