const fs = require("node:fs");

const file = "Backend/database/scripts/204_phase04_phase05_ml_learning_core.sql";
let text = fs.readFileSync(file, "utf8");

const before = /WHERE\s+run_id\s*=\s*v_run_id/g;
const matches = text.match(before) ?? [];

if (matches.length === 0) {
  throw new Error("Could not find any unqualified WHERE run_id = v_run_id expressions.");
}

text = text.replace(
  before,
  "WHERE public.ml_learning_results_v1.run_id = v_run_id"
);

fs.writeFileSync(file, text, "utf8");

console.log(`Patched ${matches.length} ambiguous run_id filter(s) in SQL 204.`);
