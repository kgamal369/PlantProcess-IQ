const fs = require("fs");
const path = require("path");

const root = process.cwd();
const sql = path.join(root, "Backend", "database", "scripts", "690_phase01_genealogy_recursive_cycle_guard.sql");
const failures = [];

if (!fs.existsSync(sql)) {
  failures.push({ file: path.relative(root, sql), reason: "missing recursive CTE SQL script" });
} else {
  const text = fs.readFileSync(sql, "utf8");
  for (const signal of [
    "PPIQ_REALIZATION_T005_RECURSIVE_GENEALOGY_CYCLE_GUARD",
    "WITH RECURSIVE",
    "ppiq_would_create_genealogy_cycle",
    "depth < 1000"
  ]) {
    if (!text.includes(signal)) failures.push({ file: path.relative(root, sql), reason: "missing signal " + signal });
  }
}

if (failures.length) {
  console.error("PPIQ-T005 failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T005 passed: recursive CTE cycle guard is present.");
