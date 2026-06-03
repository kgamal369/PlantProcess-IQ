import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const srcRoot = path.join(root, "Frontend", "PlantProcess.Web", "src");
const apiRoot = path.join(srcRoot, "api");

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) throw new Error(`Missing file: ${relativePath}`);
  return fs.readFileSync(full, "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["node_modules", "dist", "build", "coverage", ".vite"].includes(entry.name)) continue;
      walk(full, output);
    } else if (/\.(ts|tsx|js|jsx)$/.test(entry.name)) {
      output.push(full);
    }
  }

  return output;
}

ok("productApiClient exists", exists("Frontend/PlantProcess.Web/src/api/productApiClient.ts"));
ok("productCoreApiClient exists", exists("Frontend/PlantProcess.Web/src/api/productCoreApiClient.ts"));
ok("productApiHardening exists", exists("Frontend/PlantProcess.Web/src/api/productApiHardening.ts"));
ok("src/api/plantProcessApi.ts deleted", !exists("Frontend/PlantProcess.Web/src/api/plantProcessApi.ts"));
ok("src/api/legacy/plantProcessApi.ts deleted", !exists("Frontend/PlantProcess.Web/src/api/legacy/plantProcessApi.ts"));

const files = walk(srcRoot);
const offenders = [];

for (const full of files) {
  const text = fs.readFileSync(full, "utf8");
  if (text.includes("plantProcessApi")) {
    offenders.push(path.relative(root, full).replaceAll(path.sep, "/"));
  }
}

const reportDir = path.join(root, "Documentation", "v5");
fs.mkdirSync(reportDir, { recursive: true });

fs.writeFileSync(
  path.join(reportDir, "pack-c1-legacy-api-offenders.json"),
  JSON.stringify(offenders, null, 2),
  "utf8"
);

ok("zero plantProcessApi references in frontend src", offenders.length === 0, `${offenders.length} offender(s)`);

const retirementReport = read("Documentation/v5/pack-c1-legacy-api-retirement-report.json");
ok("retirement report exists", retirementReport.includes("createdProductClient"));

console.log("");
console.log("Pack C1 legacy API retirement validation passed.");