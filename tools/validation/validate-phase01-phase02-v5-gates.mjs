import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
let failed = 0;

function exists(file) {
  return fs.existsSync(path.join(root, file));
}

function read(file) {
  return exists(file) ? fs.readFileSync(path.join(root, file), "utf8") : "";
}

function check(task, condition, message) {
  if (condition) {
    console.log(`âœ“ ${task} â€” ${message}`);
  } else {
    failed += 1;
    console.error(`âŒ ${task} â€” ${message}`);
  }
}

console.log("");
console.log("============================================================");
console.log("PlantProcess IQ â€” v5 Phase 01/02 Gate");
console.log("Tasks: PPIQ-T201 .. PPIQ-T213");
console.log("============================================================");
console.log("");

const jenkins = read("Jenkinsfile");
check("PPIQ-T201", /node:20-alpine/.test(jenkins) || /node:20/.test(jenkins), "Jenkins UI gates use Node 20");
check("PPIQ-T203", /ON_ERROR_STOP=1/.test(jenkins), "SQL runner fails loudly with ON_ERROR_STOP=1");
check("PPIQ-T204", exists("Frontend/PlantProcess.Web/e2e/security/auth-matrix-admin.spec.ts"), "full tier auth-matrix spec exists");
check("PPIQ-T205", exists("Frontend/PlantProcess.Web/scripts/validate-standard-imports.mjs"), "standard import/UI validator exists");

const migration117 = read("Backend/database/scripts/117_phase8_widget_script_layer_entity_mapping.sql");
check("PPIQ-T202", /dashboard_widget_expression_audit/i.test(migration117), "117 handles widget expression audit table");
check("PPIQ-T202", /created_at_utc/i.test(migration117), "117 audit table uses created_at_utc");

const schemaEndpoint = read("Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs");
check("PPIQ-T206", /NoSuchView/.test(schemaEndpoint), "resolver exposes NoSuchView typed error");
check("PPIQ-T206", /NoSuchColumn/.test(schemaEndpoint), "resolver exposes NoSuchColumn typed error");
check("PPIQ-T206", /InvalidAggregateForType/.test(schemaEndpoint), "resolver exposes InvalidAggregateForType typed error");

check("PPIQ-T207", exists("Backend/tests/PlantProcess.Api.IntegrationTests/Import/DeltaImportResumabilityTests.cs"), "delta-import resumability tests exist");

const deployReadme = read("Infrastructure/deploy/README.md");
check("PPIQ-T208", /loopback-binding decision/i.test(deployReadme), "deployment README documents exposure decisions");

const mlSql = read("Backend/database/scripts/200_phase02_ml_foundation_feature_store_pgvector.sql");
check("PPIQ-T209", /ml_feature_definitions/.test(mlSql) && /ml_feature_values/.test(mlSql), "multi-grain feature store tables exist");
check("PPIQ-T210", /ppiq_ml_refresh_feature_store/.test(mlSql) && /chemistry\.cev/.test(mlSql) && /thermal\.true_superheat/.test(mlSql), "feature engineering refresh + derived feature catalog exist");
check("PPIQ-T211", /ml_outcome_definitions/.test(mlSql) && /defect\.rate_per_m2/.test(mlSql) && /downtime\.cascade_minutes/.test(mlSql), "outcome definitions exist");
check("PPIQ-T212", exists("Backend/PlantProcess.Application/Analytics/Interfaces/ICorrelationComputeEngine.cs"), "ICorrelationComputeEngine exists");
check("PPIQ-T212", exists("Backend/PlantProcess.Infrastructure/Analytics/PostgresCorrelationComputeEngine.cs"), "PostgreSQL default compute engine exists");
check("PPIQ-T213", /CREATE EXTENSION IF NOT EXISTS vector/.test(mlSql) && /ml_knowledge_base_items/.test(mlSql), "pgvector-ready KB table exists");
check("PPIQ-T213", exists("Backend/PlantProcess.Application/Analytics/Interfaces/IEmbeddingProvider.cs"), "IEmbeddingProvider exists");

const endpoint = read("Backend/PlantProcess.Api/Endpoints/Analytics/MlFoundationEndpoints.cs");
const program = read("Backend/PlantProcess.Api/Program.cs");
check("PPIQ-T209-T213", /MapMlFoundationEndpoints/.test(endpoint) && /MapMlFoundationEndpoints/.test(program), "ML foundation endpoints exist and are mapped");

if (failed > 0) {
  console.error("");
  console.error(`PPIQ v5 Phase 01/02 gate failed with ${failed} issue(s).`);
  process.exit(1);
}

console.log("");
console.log("âœ… PPIQ v5 Phase 01/02 gate passed.");