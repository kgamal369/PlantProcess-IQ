const fs = require("node:fs");

const file = "Backend/database/scripts/205_phase04_phase05_completion_governance_jobs_tests.sql";
let text = fs.readFileSync(file, "utf8");

let count = 0;

if (text.includes("ON CONFLICT (id) DO UPDATE SET")) {
  text = text.replace(
    "ON CONFLICT (id) DO UPDATE SET",
    "ON CONFLICT (job_code) WHERE is_deleted = FALSE DO UPDATE SET"
  );
  count++;
}

if (text.includes("ON CONFLICT (job_code) DO UPDATE SET")) {
  text = text.replace(
    "ON CONFLICT (job_code) DO UPDATE SET",
    "ON CONFLICT (job_code) WHERE is_deleted = FALSE DO UPDATE SET"
  );
  count++;
}

if (count === 0) {
  throw new Error("Could not find job_definitions ON CONFLICT block to patch.");
}

fs.writeFileSync(file, text, "utf8");

console.log(`Patched ${count} job_definitions conflict target(s) to partial unique index syntax.`);
