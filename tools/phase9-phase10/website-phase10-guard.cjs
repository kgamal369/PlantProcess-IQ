const fs = require("fs");
const path = require("path");

const root = process.cwd();
const websiteRoot = path.join(root, "Website", "PlantProcess.Website");

function read(relativePath) {
  const file = path.join(root, relativePath);
  if (!fs.existsSync(file)) throw new Error("Missing file: " + relativePath);
  return fs.readFileSync(file, "utf8");
}

const app = read("Website/PlantProcess.Website/src/App.tsx");
const acceptance = read("Website/PlantProcess.Website/docs/phase10-acceptance.md");

const requiredRoutes = ["/product", "/products/mes", "/products/qes", "/products/yard", "/products/energy", "/pricing", "/security"];
for (const route of requiredRoutes) {
  if (!acceptance.includes(route) && !app.includes(route)) throw new Error("Missing Phase 10 website route evidence: " + route);
}

const forbiddenClaims = [
  "guaranteed root cause",
  "guaranteed savings",
  "replaces MES",
  "replaces SCADA",
  "replaces PLC"
];

const websiteFiles = [];
function walk(dir) {
  if (!fs.existsSync(dir)) return;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full);
    else if (/\.(tsx|ts|md|html)$/i.test(full)) websiteFiles.push(full);
  }
}
walk(websiteRoot);

for (const file of websiteFiles) {
  const text = fs.readFileSync(file, "utf8").toLowerCase();
  for (const claim of forbiddenClaims) {
    if (text.includes(claim)) throw new Error("Forbidden overclaim found in " + path.relative(root, file) + ": " + claim);
  }
}

console.log("Phase 10 website commercial guard passed.");
