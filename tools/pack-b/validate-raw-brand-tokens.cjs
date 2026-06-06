const fs = require("fs");
const path = require("path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");
const srcRoot = path.join(frontendRoot, "src");

const forbidden = ["#050B18", "#00D4FF"];
const allowFragments = [
  "/design-system/phase56/phase56Tokens.ts",
  "/styles/phase56/phase56-tokens.css",
  "/docs/",
  "/public/",
  "/brand/"
];

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function rel(file) { return path.relative(root, file).split(path.sep).join("/"); }
function walk(dir) {
  if (!exists(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    return entry.isDirectory() ? walk(full) : [full];
  });
}

const findings = [];

for (const file of walk(srcRoot)) {
  if (!isFile(file)) continue;
  if (!/\.(ts|tsx|css)$/i.test(file)) continue;

  const relative = rel(file);
  const normalized = relative.replace(/\\/g, "/");

  if (allowFragments.some((fragment) => normalized.includes(fragment))) continue;

  const text = fs.readFileSync(file, "utf8");
  for (const color of forbidden) {
    if (text.toLowerCase().includes(color.toLowerCase())) {
      findings.push({ file: relative, color });
    }
  }
}

if (findings.length) {
  console.error("Raw core brand hex values found outside token modules:");
  console.error(JSON.stringify(findings, null, 2));
  process.exit(1);
}

console.log("Pack B brand-token raw-hex gate passed.");
