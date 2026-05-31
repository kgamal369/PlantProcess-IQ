
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

const sql205 = read("Backend/database/scripts/205_phase04_phase05_completion_governance_jobs_tests.sql");
const program = read("Backend/PlantProcess.Api/Program.cs");
const di = read("Backend/PlantProcess.Application/DependencyInjection.cs");
const narrativeInterface = read("Backend/PlantProcess.Application/Analytics/Interfaces/INarrativeProvider.cs");
const localProvider = read("Backend/PlantProcess.Application/Analytics/Services/LocalNarrativeProvider.cs");
const apiProvider = read("Backend/PlantProcess.Application/Analytics/Services/ApiNarrativeProvider.cs");
const configuredProvider = read("Backend/PlantProcess.Application/Analytics/Services/ConfiguredNarrativeProvider.cs");
const providerEndpoint = read("Backend/PlantProcess.Api/Endpoints/Analytics/MlProviderEndpoints.cs");

check("SQL 205 exists", sql205.includes("ppiq_ml_phase45_completion_acceptance_v1"));
check("Provider catalog exists", sql205.includes("ml_ai_provider_catalog_v1"));
check("Narrative safety audit exists", sql205.includes("ml_narrative_safety_audit_v1"));
check("All four jobs ensured", sql205.includes("ppiq_ml_ensure_job_definitions_v1") && sql205.includes("SYSTEM_ML_WEEKLY_OVERALL"));
check("Jobs monitor view exists", sql205.includes("v_ml_learning_jobs_monitor_v1"));
check("Readiness gate exists", sql205.includes("ppiq_ml_learning_readiness_v1"));
check("Backoff proof exists", sql205.includes("ppiq_ml_simulate_learning_backoff_v1"));
check("All jobs run wrapper exists", sql205.includes("ppiq_ml_run_all_learning_jobs_v1"));
check("Golden tests exist", sql205.includes("ppiq_ml_run_phase45_golden_tests_v1"));
check("Completion acceptance exists", sql205.includes("ppiq_ml_phase45_completion_acceptance_v1"));
check("Type-aware methods include extended methods", sql205.includes("spearman") && sql205.includes("mutual_information") && sql205.includes("lasso_screening"));

check("INarrativeProvider exists", narrativeInterface.includes("interface INarrativeProvider"));
check("Local provider exists", localProvider.includes("LocalNarrativeProvider") && localProvider.includes("UsedExternalApi: false"));
check("API provider exists", apiProvider.includes("ApiNarrativeProvider") && apiProvider.includes("sanitized"));
check("Configured provider exists", configuredProvider.includes("ConfiguredNarrativeProvider") && configuredProvider.includes("PlantProcess:AI:NarrativeProvider"));
check("DI registers narrative provider", di.includes("INarrativeProvider") && di.includes("ConfiguredNarrativeProvider"));
check("Provider endpoint exists", providerEndpoint.includes("/api/ml/providers") && providerEndpoint.includes("/narrative/proof"));
check("Program maps provider endpoint", program.includes("MapMlProviderEndpoints"));

if (failures.length) {
  console.error("V7 Phase 4/5 completion validation FAILED");
  for (const failure of failures) console.error(" - " + failure);
  process.exit(1);
}

console.log("V7 Phase 4/5 completion static validation passed.");
console.log("Runtime proof still required: apply SQL 205, run completion acceptance, backend build, API smoke.");
