import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const reportDir = path.join(root, "Documentation", "v5");
fs.mkdirSync(reportDir, { recursive: true });

const scanRoots = [
  "Backend/PlantProcess.Api",
  "Backend/PlantProcess.Application",
  "Backend/PlantProcess.Infrastructure",
  "Frontend/PlantProcess.Web/src",
  "Website/PlantProcess.Website/src"
];

const ignored = [
  /node_modules/i,
  /dist/i,
  /build/i,
  /bin/i,
  /obj/i,
  /Documentation/i,
  /_.*backup/i
];

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    const relative = path.relative(root, full).replaceAll(path.sep, "/");

    if (ignored.some((x) => x.test(relative))) continue;

    if (entry.isDirectory()) {
      walk(full, output);
    } else if (/\.(cs|ts|tsx|js|jsx|css)$/.test(entry.name)) {
      output.push(full);
    }
  }

  return output;
}

const phasePattern = /(Phase\d+|Phase\d+\d+|P03P04|P\d{2}P\d{2}|Pack\d+)/;
const files = scanRoots.flatMap((x) => walk(path.join(root, x)));

const findings = [];

for (const full of files) {
  const relative = path.relative(root, full).replaceAll(path.sep, "/");
  const basename = path.basename(full);

  if (phasePattern.test(relative)) {
    findings.push({
      type: "path",
      file: relative,
      match: relative.match(phasePattern)?.[0] ?? ""
    });
  }

  const text = fs.readFileSync(full, "utf8");
  const matches = [...text.matchAll(/\b(Phase\d+|Phase\d+\d+|P03P04|P\d{2}P\d{2}|Pack\d+)\b/g)];

  for (const match of matches.slice(0, 20)) {
    findings.push({
      type: "content",
      file: relative,
      match: match[0]
    });
  }
}

const report = {
  generatedAtUtc: new Date().toISOString(),
  count: findings.length,
  status: findings.length === 0 ? "green" : "pack-c2-required",
  note: "Pack C1 reports phase-named artifacts. Pack C2/C3 will rename them after characterization tests.",
  findings
};

fs.writeFileSync(
  path.join(reportDir, "pack-c1-phase-naming-inventory.json"),
  JSON.stringify(report, null, 2),
  "utf8"
);

console.log(JSON.stringify({
  count: report.count,
  status: report.status,
  report: "Documentation/v5/pack-c1-phase-naming-inventory.json"
}, null, 2));