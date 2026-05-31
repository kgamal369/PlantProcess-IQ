const fs = require("node:fs");

const file = "Backend/database/scripts/205_phase04_phase05_completion_governance_jobs_tests.sql";
let text = fs.readFileSync(file, "utf8");

const oldText = "ON CONFLICT (job_code) DO UPDATE SET";
const newText = "ON CONFLICT (id) DO UPDATE SET";

const count = (text.match(new RegExp(oldText.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"), "g")) || []).length;

if (count === 0) {
  throw new Error("Could not find ON CONFLICT (job_code) block to patch.");
}

text = text.replace(oldText, newText);

fs.writeFileSync(file, text, "utf8");

console.log(`Patched ${count} job_definitions conflict target from job_code to id.`);
