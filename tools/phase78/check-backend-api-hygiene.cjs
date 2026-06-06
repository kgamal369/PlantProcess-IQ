const fs = require("fs");
const path = require("path");
const root = process.cwd();
const baseline = JSON.parse(fs.readFileSync(path.join(root, "tools", "phase78", "backend-api-hygiene-baseline.json"), "utf8"));
const tracked = new Set([
  ...(baseline.targets || []).filter((x) => x.exists && x.lines > 500).map((x) => x.path.split("\\").join("/")),
  ...(baseline.allLargeFiles || []).filter((x) => x.lines > 500).map((x) => x.path.split("\\").join("/"))
]);
function walk(dir) {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const f = path.join(dir, entry.name);
    return entry.isDirectory() ? walk(f) : [f];
  });
}
function lines(file) { return fs.readFileSync(file, "utf8").split(/\r?\n/).length; }
const scanRoots = [path.join(root, "Backend", "PlantProcess.Api"), path.join(root, "Backend", "PlantProcess.Application")];
const offenders = [];
const warnings = [];
for (const file of scanRoots.flatMap(walk).filter((x) => x.endsWith(".cs"))) {
  const relativePath = path.relative(root, file).split(path.sep).join("/");
  const count = lines(file);
  const isTracked = tracked.has(relativePath);
  if (count > 700 && !isTracked) offenders.push(relativePath + ": " + count + " lines");
  else if (count > 500) warnings.push(relativePath + ": " + count + " lines" + (isTracked ? " (tracked P08 refactor target)" : ""));
}
for (const warning of warnings) console.warn("[WARN] " + warning);
if (offenders.length) {
  console.error("Unknown oversized backend API/Application files:");
  offenders.forEach((x) => console.error(" - " + x));
  process.exit(1);
}
console.log("Phase 8 backend API hygiene gate passed. Existing large files are tracked; unknown new oversized files are blocked.");
