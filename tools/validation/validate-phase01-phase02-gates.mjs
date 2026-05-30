import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

function fail(message) {
  console.error(`❌ ${message}`);
  process.exitCode = 1;
}

function pass(message) {
  console.log(`✓ ${message}`);
}

function assert(condition, message) {
  if (condition) pass(message);
  else fail(message);
}

function walk(dir, result = []) {
  if (!fs.existsSync(dir)) return result;

  for (const item of fs.readdirSync(dir)) {
    const full = path.join(dir, item);
    const rel = path.relative(root, full).replaceAll("\\", "/");

    if (
      rel.includes("node_modules/") ||
      rel.includes("dist/") ||
      rel.includes("bin/") ||
      rel.includes("obj/") ||
      rel.includes("test-results/") ||
      rel.includes("playwright-report/")
    ) {
      continue;
    }

    const stat = fs.statSync(full);
    if (stat.isDirectory()) walk(full, result);
    else result.push(full);
  }

  return result;
}

console.log("");
console.log("============================================================");
console.log("PlantProcess IQ — v4 Phase 01/02 Gate");
console.log("Tasks: PPIQ-T101 .. PPIQ-T112");
console.log("============================================================");
console.log("");

assert(exists("Frontend/PlantProcess.Web/e2e/security/auth-matrix-admin.spec.ts"), "PPIQ-T101 auth matrix Playwright spec exists");

assert(
  !exists("Frontend/PlantProcess.Web/src/pages/Admin/Phase1WorkflowTruthPanel.tsx"),
  "PPIQ-T102 orphan Phase1WorkflowTruthPanel is deleted"
);

for (const caddy of ["Infrastructure/deploy/Caddyfile", "deployment/caddy/Caddyfile"]) {
  if (!exists(caddy)) continue;
  const text = read(caddy);
  assert(text.includes("https://api.plantprocessiq.com"), `PPIQ-T103 ${caddy} allows production API`);
  assert(text.includes("https://api.178.105.152.180.sslip.io"), `PPIQ-T103 ${caddy} allows sslip staging API`);
  assert(text.includes("http://localhost:5063"), `PPIQ-T103 ${caddy} allows local API`);
}

const scannedFiles = walk(root).filter((file) =>
  [".cs", ".sql", ".ps1", ".ts", ".tsx", ".js", ".mjs", ".yml", ".yaml", ".env", ".example", ""]
    .includes(path.extname(file))
);

const allowedPlantAdminScannerFile = (rel) =>
  rel.includes("validate-phase01-phase02-gates.mjs") ||
  rel.includes("121_phase01_bootstrap_token_sweep.sql") ||
  rel.includes("Repair-PPIQ-Phase1-Current-State.ps1");

const badPlantAdmin = [];
for (const file of scannedFiles) {
  const rel = path.relative(root, file).replaceAll("\\", "/");
  if (allowedPlantAdminScannerFile(rel)) continue;

  const text = fs.readFileSync(file, "utf8");
  if (text.includes(";plantadmin")) badPlantAdmin.push(rel);
}

assert(badPlantAdmin.length === 0, `PPIQ-T104 ;plantadmin token absent (${badPlantAdmin.join(", ")})`);

const backendCompose = exists("Backend/docker-compose.yml") ? read("Backend/docker-compose.yml") : "";
assert(
  !backendCompose.includes('- "${PLANTPROCESS_API_PORT:-5063}:5063"'),
  "PPIQ-T105 Backend API host port is not exposed on all interfaces"
);
assert(
  backendCompose.includes('127.0.0.1:${PLANTPROCESS_API_PORT:-5063}:5063') ||
    !backendCompose.includes("PLANTPROCESS_API_PORT"),
  "PPIQ-T105 Backend API host port is loopback-bound when present"
);

const demoCompose = read("Infrastructure/deploy/docker-compose.demo.yml");
assert(
  demoCompose.includes('127.0.0.1:${POSTGRES_PORT:-5432}:5432'),
  "PPIQ-T105 demo Postgres is loopback-bound"
);
assert(
  !demoCompose.match(/^\s*-\s*"\$\{POSTGRES_PORT:-5432\}:5432"/m),
  "PPIQ-T105 demo Postgres all-interface binding is absent"
);

assert(exists("Backend/database/scripts/120_phase02_canonical_schema_mapping_engine.sql"), "PPIQ-T107 canonical schema SQL script exists");
assert(exists("Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs"), "PPIQ-T108/T110/T111/T112 backend endpoint exists");

const program = read("Backend/PlantProcess.Api/Program.cs");
assert(program.includes("app.MapGenericSchemaMappingEndpoints();"), "PPIQ-T108 backend endpoint is mapped in Program.cs");

const endpoint = read("Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs");
assert(endpoint.includes("/catalog/register"), "PPIQ-T107 catalog register endpoint exists");
assert(endpoint.includes("/resolve"), "PPIQ-T108 resolver endpoint exists");
assert(endpoint.includes("/joins/preview"), "PPIQ-T110 join preview endpoint exists");
assert(endpoint.includes("/joins/materialize"), "PPIQ-T110 join materialization endpoint exists");
assert(endpoint.includes("/kpi-views"), "PPIQ-T111 KPI-as-view endpoint exists");
assert(endpoint.includes("/execute/{viewCode}"), "PPIQ-T112 mapping execution endpoint exists");

assert(exists("Frontend/PlantProcess.Web/src/api/schemaMapping/schemaMapping.api.ts"), "PPIQ-T109 frontend schema mapping API exists");
assert(exists("Frontend/PlantProcess.Web/src/pages/Admin/CanonicalSchemaMappingPanel.tsx"), "PPIQ-T109 frontend schema mapping panel exists");

const adminTab = read("Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.tsx");
assert(adminTab.includes("CanonicalSchemaMappingPanel"), "PPIQ-T109 panel is mounted in Schema Configuration tab");

const forbiddenCopyPatterns = [
  /could not load/i,
  /could not be loaded/i,
  /failed to load/i,
];

const customerVisibleFiles = walk(path.join(root, "Frontend", "PlantProcess.Web", "src")).filter((file) =>
  [".ts", ".tsx"].includes(path.extname(file)) &&
  !file.includes("__tests__") &&
  !file.includes("/test/") &&
  !file.includes("\\test\\")
);

const forbiddenHits = [];
for (const file of customerVisibleFiles) {
  const text = fs.readFileSync(file, "utf8");
  if (forbiddenCopyPatterns.some((pattern) => pattern.test(text))) {
    forbiddenHits.push(path.relative(root, file).replaceAll("\\", "/"));
  }
}

assert(
  forbiddenHits.length === 0,
  `PPIQ-T106 forbidden customer-visible failure copy absent (${forbiddenHits.join(", ")})`
);

if (process.exitCode && process.exitCode !== 0) {
  console.error("");
  console.error("PPIQ Phase 01/02 gate failed.");
  process.exit(process.exitCode);
}

console.log("");
console.log("✅ PPIQ v4 Phase 01/02 gate passed.");