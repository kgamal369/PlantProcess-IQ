
const fs = require("node:fs");

const failures = [];

function read(file) {
  if (!fs.existsSync(file)) {
    failures.push("Missing file: " + file);
    return "";
  }
  return fs.readFileSync(file, "utf8");
}

function check(name, condition) {
  if (!condition) failures.push(name);
}

const sql = read("Backend/database/scripts/204_phase04_phase05_ml_learning_core.sql");
const endpoint = read("Backend/PlantProcess.Api/Endpoints/Analytics/MlLearningEndpoints.cs");
const program = read("Backend/PlantProcess.Api/Program.cs");

check("P04/P05 SQL creates four-job learning catalog", /ml_learning_job_catalog_v1/.test(sql) && /ML_PROCESS_VS_DEFECT/.test(sql) && /ML_WEEKLY_OVERALL/.test(sql));
check("P04/P05 SQL creates deterministic golden dataset", /ml_learning_observations_v1/.test(sql) && /ppiq_ml_seed_phase45_golden_dataset/.test(sql));
check("P04 feature/outcome proof includes heat effective-n", /effective_n/.test(sql) && /distinct heat_id/i.test(sql));
check("P04/P05 SQL includes no-LLM compute-path evidence", /noLlmInComputePath/.test(sql));
check("P05 SQL includes type-aware methods", /pearson/.test(sql) && /point_biserial/.test(sql) && /cramers_v/.test(sql));
check("P05 SQL includes FDR q-values", /q_value/.test(sql) && /Benjamini-Hochberg|ranked/.test(sql));
check("P05 SQL includes power/stability/finding status", /power_status/.test(sql) && /stability_score/.test(sql) && /finding_status/.test(sql));
check("P05 SQL mirrors into existing v2 result table", /ml_correlation_results_v2/.test(sql));
check("P04/P05 acceptance function exists", /ppiq_ml_phase45_acceptance/.test(sql));

check("API endpoint file exists and exposes learning routes", /MapMlLearningEndpoints/.test(endpoint) && /\/api\/ml\/learning/.test(endpoint));
check("API exposes run endpoint", /jobs\/\{jobCode\}\/run/.test(endpoint));
check("Program.cs maps ML learning endpoints", /MapMlLearningEndpoints/.test(program));

if (failures.length) {
  console.error("");
  console.error("V7 Phase 4/5 acceptance FAILED");
  for (const failure of failures) console.error(" - " + failure);
  process.exit(1);
}

console.log("V7 Phase 4/5 static acceptance validation passed.");
console.log("Runtime proof still required: apply SQL 204, run ppiq_ml_phase45_acceptance(), backend build, API smoke.");
