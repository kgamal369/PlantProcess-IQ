const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const marker = "PPIQ_REALIZATION_T019_CLEAN_MACHINE_TO_LOGIN_V2";

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function exists(rel) {
  return fs.existsSync(full(rel));
}

function read(rel) {
  return fs.readFileSync(full(rel), "utf8");
}

function fail(message) {
  console.error("[RED] P3-T19 validation failed: " + message);
  process.exit(1);
}

function run(command, args, cwd = root) {
  const result = childProcess.spawnSync(command, args, {
    cwd,
    encoding: "utf8",
    shell: false,
  });

  if (result.error) {
    fail(command + " could not start: " + result.error.message);
  }

  if (result.status !== 0) {
    fail(
      command +
        " " +
        args.join(" ") +
        " failed\nSTDOUT:\n" +
        (result.stdout || "") +
        "\nSTDERR:\n" +
        (result.stderr || "")
    );
  }
}

const required = [
  "deploy/server/clean-machine-to-login.ps1",
  "tools/phase3/smoke-p3-t19-clean-machine-login.ps1",
  "docs/deployment/PHASE03_CLEAN_MACHINE_TO_LOGIN_RUNBOOK.md",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const deploy = read("deploy/server/clean-machine-to-login.ps1");
const smoke = read("tools/phase3/smoke-p3-t19-clean-machine-login.ps1");
const runbook = read("docs/deployment/PHASE03_CLEAN_MACHINE_TO_LOGIN_RUNBOOK.md");

if (!deploy.includes(marker)) fail("deploy script missing marker");
if (!deploy.includes("local-native-main-db")) fail("deploy script missing local native DB profile");
if (!deploy.includes("server-docker-main-db")) fail("deploy script missing server Docker DB profile");
if (!deploy.includes("customer-template")) fail("deploy script missing customer topology profile");
if (!deploy.includes("docker-compose.server-docker-main-db.yml")) fail("deploy script missing server compose overlay");
if (!deploy.includes("docker-compose.local-native-main-db.yml")) fail("deploy script missing local compose overlay");
if (!deploy.includes("docker-compose.customer-template.yml")) fail("deploy script missing customer compose overlay");
if (!deploy.includes("Assert-NoTrackedRuntimeSecrets")) fail("deploy script missing tracked-secret guard");
if (!deploy.includes("docker") || !deploy.includes("compose") || !deploy.includes("config")) fail("deploy script missing compose config validation");
if (!deploy.includes("COMPOSE_PROFILES")) fail("deploy script missing Compose profile activation for server-docker-main-db");
if (!deploy.includes("dotnet build")) fail("deploy script missing backend build gate");
if (!deploy.includes("npm run build")) fail("deploy script missing frontend build gate");
if (!deploy.includes("smoke-p3-t19-clean-machine-login.ps1")) fail("deploy script missing smoke call");

if (!smoke.includes("PPIQ_REALIZATION_T019_CLEAN_MACHINE_LOGIN_SMOKE")) fail("smoke script missing marker");
if (!smoke.includes("/health")) fail("smoke script missing API health check");
if (!smoke.includes("/auth/login")) fail("smoke script missing login check");
if (!smoke.includes("accessToken")) fail("smoke script missing token check");

if (!runbook.includes(marker)) fail("runbook missing marker");
if (!runbook.includes("Clean machine prerequisites")) fail("runbook missing prerequisites");
if (!runbook.includes("Runtime env preparation")) fail("runbook missing env preparation");
if (!runbook.includes("Dry run")) fail("runbook missing dry run");
if (!runbook.includes("server-docker-main-db")) fail("runbook missing server profile");
if (!runbook.includes("local-native-main-db")) fail("runbook missing local profile");
if (!runbook.includes("customer-template")) fail("runbook missing customer profile");
if (!runbook.includes("Failure modes")) fail("runbook missing failure modes");

const forbiddenSecretPatterns = [
  /PPIQ_SIGNING_KEY\s*=\s*["'][^"']+["']/i,
  /POSTGRES_PASSWORD\s*=\s*["'][^"']+["']/i,
  /SmokePassword\s*=\s*["'][^"']+["']/i,
  /plantprocess123/i,
];

for (const [name, text] of [
  ["deploy script", deploy],
  ["smoke script", smoke],
  ["runbook", runbook],
]) {
  for (const rx of forbiddenSecretPatterns) {
    if (rx.test(text)) fail(name + " appears to contain a committed secret: " + rx);
  }
}

run("powershell", [
  "-NoProfile",
  "-ExecutionPolicy",
  "Bypass",
  "-Command",
  "Get-Command -Syntax './deploy/server/clean-machine-to-login.ps1' | Out-Null; Get-Command -Syntax './tools/phase3/smoke-p3-t19-clean-machine-login.ps1' | Out-Null"
]);

console.log("[GREEN] P3-T19 static validation passed.");