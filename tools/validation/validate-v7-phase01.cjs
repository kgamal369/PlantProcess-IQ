const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const failures = [];

function read(rel) {
  const file = path.join(root, rel);
  if (!fs.existsSync(file)) {
    failures.push(`Missing file: ${rel}`);
    return "";
  }
  return fs.readFileSync(file, "utf8");
}

function ok(condition, message) {
  if (!condition) failures.push(message);
}

const authContext = read("Frontend/PlantProcess.Web/src/state/AuthContext.tsx");
ok(authContext.includes("MAX_AUTO_BOOTSTRAP_ATTEMPTS = 3"), "T280 missing max auth attempt cap");
ok(authContext.includes("AUTH_RETRY_BACKOFF_MS"), "T280 missing auth backoff");
ok(authContext.includes("invalid-credentials"), "T281 missing invalid-credentials classification");
ok(authContext.includes("backend-unreachable"), "T281 missing backend-unreachable classification");
ok(authContext.includes("VITE_SMOKE_PASSWORD"), "T285 missing VITE_SMOKE_PASSWORD contract");

const envExample = read("Frontend/PlantProcess.Web/.env.example");
ok(envExample.includes("VITE_SMOKE_USERNAME"), "T285 .env.example missing VITE_SMOKE_USERNAME");
ok(envExample.includes("VITE_SMOKE_PASSWORD"), "T285 .env.example missing VITE_SMOKE_PASSWORD");

const program = read("Backend/PlantProcess.Api/Program.cs");
ok(program.includes('GetSection("PlantProcess:Auth")'), "T282 Program.cs must bind AuthOptions from PlantProcess:Auth");
ok(program.includes("Auth bound from PlantProcess:Auth"), "T282 missing exact auth binding startup log");

const startup = read("Backend/PlantProcess.Api/Configuration/StartupConfigurationValidator.cs");
ok(startup.includes("Legacy Auth:* configuration is not supported"), "T282 missing legacy Auth:* rejection");
ok(startup.includes("Non-development requires at least one real configured admin"), "T287 missing real admin non-development guard");
ok(startup.includes("Bootstrap admin user must not collide"), "T287 missing bootstrap collision guard");

const authEndpoints = read("Backend/PlantProcess.Api/Security/AuthEndpoints.cs");
ok(authEndpoints.includes("Bootstrap admin login rejected because at least one real admin exists"), "T287 missing runtime bootstrap rejection");
ok(authEndpoints.includes("HasRealAdmin"), "T287 missing real-admin guard helper");

const compose = read("Infrastructure/deploy/docker-compose.demo.yml");
ok(!compose.includes('"${POSTGRES_PORT:-5432}:5432"'), "T286 postgres still binds publicly");
ok(compose.includes('"127.0.0.1:${POSTGRES_PORT:-5432}:5432"'), "T286 postgres loopback binding missing");

const exposure = read("Infrastructure/deploy/verify-server-exposure.sh");
ok(exposure.includes("5432") && exposure.includes("publicly open"), "T208/T286 exposure proof script incomplete");

const stopPort = read("tools/dev/Stop-PPIQ-Port.ps1");
ok(stopPort.includes("Get-NetTCPConnection") && stopPort.includes("Stop-Process"), "T284 kill-by-port helper incomplete");

const sqlHygiene = read("tools/validation/validate-sql-script-hygiene.cjs");
ok(sqlHygiene.includes("ON_ERROR_STOP=1"), "T283 SQL hygiene validator missing ON_ERROR_STOP check");
ok(sqlHygiene.includes("UTF-8 BOM"), "T288 SQL hygiene validator missing BOM check");

const standardProof = read("tools/validation/prove-standard-import-gate.cjs");
ok(standardProof.includes("<button>bad</button>"), "T205 negative proof script missing native button violation");

if (failures.length > 0) {
  console.error("PlantProcess IQ V7 Phase 1 validation failed:");
  for (const failure of failures) console.error(" - " + failure);
  process.exit(1);
}

console.log("PlantProcess IQ V7 Phase 1 static implementation validation passed.");
console.log("NOTE: T208/T286 still require real external server scan after deployment.");
