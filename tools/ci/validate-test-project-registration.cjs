const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

function walk(dir, predicate, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["bin", "obj", "node_modules", ".git", "dist", "coverage"].includes(entry.name)) continue;
      walk(full, predicate, output);
      continue;
    }

    if (predicate(full)) output.push(full);
  }

  return output;
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function normalizeFull(file) {
  return path.resolve(file).toLowerCase().replace(/\\/g, "/");
}

const slns = walk(root, (file) => file.toLowerCase().endsWith(".sln"));

const sln = slns.find((file) => /Backend[\\/]PlantProcessIQ\.sln$/i.test(file))
  || slns.find((file) => /PlantProcessIQ\.sln$/i.test(file))
  || slns.find((file) => /PlantProcess.*\.sln$/i.test(file))
  || slns[0];

if (!sln) {
  console.error("No solution file found.");
  process.exit(1);
}

const slnDir = path.dirname(sln);

const testProjects = walk(path.join(root, "Backend", "tests"), (file) =>
  file.endsWith(".csproj")
);

let slnList = "";
try {
  slnList = cp.execFileSync("dotnet", ["sln", sln, "list"], {
    cwd: root,
    encoding: "utf8",
    shell: false
  });
} catch (error) {
  console.error("Failed to run dotnet sln list.");
  console.error(error.message || String(error));
  process.exit(1);
}

const registeredFullPaths = new Set();

for (const rawLine of slnList.split(/\r?\n/)) {
  const line = rawLine.trim();

  if (!line || line.startsWith("Project(s)") || line.startsWith("-")) continue;
  if (!line.toLowerCase().endsWith(".csproj")) continue;

  const full = path.resolve(slnDir, line);
  registeredFullPaths.add(normalizeFull(full));
}

const missing = [];

for (const project of testProjects) {
  if (!registeredFullPaths.has(normalizeFull(project))) {
    missing.push({
      project: rel(project),
      expectedFullPath: normalizeFull(project)
    });
  }
}

if (missing.length) {
  console.error("PPIQ-T002 failed: unregistered test projects:");
  for (const item of missing) console.error(" - " + item.project);
  process.exit(1);
}

console.log("PPIQ-T002 passed: every Backend/tests/*.csproj project is registered in " + rel(sln) + ".");
