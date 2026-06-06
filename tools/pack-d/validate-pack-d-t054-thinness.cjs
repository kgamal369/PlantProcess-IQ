const fs = require("fs");
const path = require("path");

const root = process.cwd();
const targets = [
  {
    "task": "T-054",
    "path": "Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs",
    "max": 500
  },
  {
    "task": "T-054",
    "path": "Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs",
    "max": 500
  }
];

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function read(file) { return fs.readFileSync(file, "utf8"); }
function lines(file) { return isFile(file) ? read(file).replace(/\r\n/g, "\n").split("\n").length : 0; }

const failures = [];

for (const target of targets) {
  const absolute = path.join(root, target.path);
  const count = lines(absolute);

  if (!isFile(absolute)) {
    failures.push({ ...target, actual: 0, reason: "missing" });
    continue;
  }

  if (count > target.max) {
    failures.push({ ...target, actual: count, reason: "too-large" });
  }
}

if (failures.length) {
  console.error("Pack D T-054 thinness gate failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack D T-054 thinness gate passed.");
