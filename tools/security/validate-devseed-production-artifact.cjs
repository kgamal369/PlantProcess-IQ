const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const configuration = process.argv.includes("--debug") ? "Debug" : "Release";
const project = path.join(root, "Backend", "PlantProcess.Api", "PlantProcess.Api.csproj");
const publishDir = path.join(root, "artifacts", "production-scan", "api-release");

if (!fs.existsSync(project)) {
  console.error("PlantProcess.Api.csproj not found.");
  process.exit(1);
}

fs.rmSync(publishDir, { recursive: true, force: true });
fs.mkdirSync(publishDir, { recursive: true });

cp.execFileSync("dotnet", ["publish", project, "-c", configuration, "-o", publishDir, "--nologo"], {
  cwd: root,
  stdio: "inherit",
  shell: false
});

function walk(dir, predicate, output = []) {
  if (!fs.existsSync(dir)) return output;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, predicate, output);
    else if (predicate(full)) output.push(full);
  }
  return output;
}

const binaries = walk(publishDir, (file) => /PlantProcess\.Api\.(dll|exe)$/i.test(file));
const badSignals = ["/dev-seed", "MapDevSeed", "Development seed endpoint"];

const findings = [];

for (const binary of binaries) {
  const buffer = fs.readFileSync(binary);
  const text = buffer.toString("latin1");
  for (const signal of badSignals) {
    if (text.includes(signal)) {
      findings.push({ file: path.relative(root, binary), signal });
    }
  }
}

if (findings.length) {
  console.error("PPIQ-T010 failed: Release artifact still contains dev-seed endpoint signals.");
  console.error(JSON.stringify(findings, null, 2));
  process.exit(1);
}

console.log("PPIQ-T010 passed: production artifact scan found no dev-seed endpoint route signals.");
