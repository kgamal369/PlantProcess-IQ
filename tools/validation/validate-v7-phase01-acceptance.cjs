// P00A-TEST-REGISTER: RETIRE-PENDING-REPLACEMENT
// Date: 2026-05-31T11:07:14.744Z
// Replacement: Auth/deploy behavioural tests
// Reason: This file is tracked by the P00A Test Register and should not be treated as a final behavioural test.

const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const failures = [];

function read(rel) {
  const file = path.join(root, rel);
  if (!fs.existsSync(file)) {
    failures.push("Missing file: " + rel);
    return "";
  }
  return fs.readFileSync(file, "utf8");
}

function assert(condition, message) {
  if (!condition) failures.push(message);
}

const program = read("Backend/PlantProcess.Api/Program.cs");
assert(program.includes("Auth bound from PlantProcess:Auth —"), "T282 missing exact auth startup log prefix");
assert(program.includes("signingKeyLen={SigningKeyLen}"), "T282 missing signingKeyLen in startup log");
assert(program.includes("bootstrapCollision={BootstrapCollision}"), "T282 missing bootstrapCollision in startup log");
assert(program.includes("Auth bootstrap collision detected"), "T282 missing bootstrap collision warning");

const startup = read("Backend/PlantProcess.Api/Configuration/StartupConfigurationValidator.cs");
assert(startup.includes("Legacy Auth:* configuration is not supported"), "T282 missing legacy Auth:* rejection");
assert(startup.includes("Non-development requires at least one real configured admin"), "T287 missing real-admin production guard");
assert(startup.includes("Production bootstrap password must be a disabled sentinel"), "T287 missing bootstrap sentinel production guard");
assert(startup.includes("Production PlantProcess:Auth:SigningKey must be a strong non-development key"), "T287 missing production signing-key guard");

const auth = read("Frontend/PlantProcess.Web/src/state/AuthContext.tsx");
assert(auth.includes("MAX_AUTO_BOOTSTRAP_ATTEMPTS = 3"), "T280 missing <=3 auto-bootstrap cap");
assert(auth.includes("AUTH_RETRY_BACKOFF_MS"), "T280 missing retry backoff");
assert(auth.includes("invalid-credentials"), "T281 missing invalid credentials classification");
assert(auth.includes("backend-unreachable"), "T281 missing backend unreachable classification");
assert(auth.includes("server-error"), "T281 missing server-error classification");
assert(auth.includes("VITE_SMOKE_PASSWORD"), "T285 missing smoke password contract");

const envExample = read("Frontend/PlantProcess.Web/.env.example");
assert(envExample.includes("VITE_SMOKE_USERNAME"), "T285 .env.example missing VITE_SMOKE_USERNAME");
assert(envExample.includes("VITE_SMOKE_PASSWORD"), "T285 .env.example missing VITE_SMOKE_PASSWORD");

const bootstrap = read("tools/dev-bootstrap.ps1");
assert(bootstrap.includes("client_min_messages=warning"), "T283 dev-bootstrap missing NOTICE suppression");
assert(bootstrap.includes("PlantProcess:Auth:BootstrapAdminPassword"), "T283 wrong bootstrap secret path");
assert(bootstrap.includes("PlantProcess:Auth:Users:0:Password"), "T283 wrong user secret path");
assert(bootstrap.includes("PlantProcess:Auth:SigningKey"), "T283 missing signing-key path");

const stopPort = read("tools/dev/Stop-PPIQ-Port.ps1");
assert(stopPort.includes("Get-NetTCPConnection"), "T284 stop helper must use port owner lookup");
assert(stopPort.includes("StartTime"), "T284 stop helper must print StartTime");

const showPort = read("tools/dev/Show-PPIQ-PortOwner.ps1");
assert(showPort.includes("StartTime"), "T284 show helper must print listener StartTime");

const exposure = read("deploy/server/verify-server-exposure.sh");
assert(exposure.includes("5432") && exposure.includes("PostgreSQL"), "T286 exposure proof missing PostgreSQL check");
assert(exposure.includes("6379") && exposure.includes("5063"), "T208 exposure proof missing internal port checks");

const proof = read("tools/validation/prove-standard-import-gate.cjs");
assert(proof.includes("<button>bad</button>"), "T205 missing negative native-button proof");
assert(proof.includes("hardening/forbidden"), "T205 missing hardening-import negative proof");
assert(proof.includes("isFrontendRoot"), "T205 proof script does not handle frontend cwd");

const adminFiles = [
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminImportingDataTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminJobsMonitorTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/CanonicalSchemaMappingPanel.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/TwoStageImportMonitorPanel.tsx",
];

for (const rel of adminFiles) {
  const text = read(rel);
  for (const tag of ["button", "input", "select", "textarea", "table"]) {
    const regex = new RegExp("<" + tag + "(?=[\\s>/])", "i");
    assert(!regex.test(text), "T205 native <" + tag + "> still exists in " + rel);
  }
}

if (failures.length > 0) {
  console.error("V7 Phase 1 acceptance validation failed:");
  for (const failure of failures) console.error(" - " + failure);
  process.exit(1);
}

console.log("V7 Phase 1 acceptance static validation passed.");
console.log("NOTE: T208/T286 still require the real external server scan output after deployment.");
