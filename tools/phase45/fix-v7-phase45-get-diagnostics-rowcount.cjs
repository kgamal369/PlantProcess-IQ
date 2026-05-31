const fs = require("node:fs");

const file = "Backend/database/scripts/205_phase04_phase05_completion_governance_jobs_tests.sql";
let text = fs.readFileSync(file, "utf8");

if (!text.includes("v_inserted integer := 0;")) {
  throw new Error("Could not find v_inserted declaration.");
}

if (!text.includes("v_row_count integer := 0;")) {
  text = text.replace(
    "v_inserted integer := 0;",
    "v_inserted integer := 0;\n    v_row_count integer := 0;"
  );
}

const invalid = "GET DIAGNOSTICS v_inserted = v_inserted + ROW_COUNT;";
const fixed = "GET DIAGNOSTICS v_row_count = ROW_COUNT;\n    v_inserted := v_inserted + v_row_count;";

const count = (text.match(new RegExp(invalid.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"), "g")) || []).length;

if (count === 0) {
  console.log("No invalid cumulative GET DIAGNOSTICS statements found. File may already be patched.");
} else {
  text = text.replaceAll(invalid, fixed);
  console.log(`Patched ${count} invalid cumulative GET DIAGNOSTICS statement(s).`);
}

fs.writeFileSync(file, text, "utf8");
