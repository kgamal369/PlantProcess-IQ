const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const marker = "PPIQ_REALIZATION_T016_CANONICAL_ENV_DEPLOY_V2";

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
  console.error("[RED] P3-T16 validation failed: " + message);
  process.exit(1);
}

function runCommand(command, args, env) {
  const result = childProcess.spawnSync(command, args, {
    cwd: root,
    env,
    encoding: "utf8",
    shell: false,
  });

  return result;
}

function runDockerComposeConfig(files, label) {
  const env = {
    ...process.env,
    COMPOSE_PROFILES: "server-docker-main-db",
    POSTGRES_USER: "plantprocess_admin",
    POSTGRES_PASSWORD: "CHANGE_ME_CONFIG_PARSE_ONLY",
    POSTGRES_DB: "plantprocess_app_db",
    POSTGRES_PORT: "55433",
    PPIQ_MAIN_DB_CONNECTION_STRING:
      "Host=host.docker.internal;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=CHANGE_ME_CONFIG_PARSE_ONLY;Include Error Detail=true",
    PPIQ_SIGNING_KEY:
      "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
    PPIQ_BOOTSTRAP_ADMIN_PASSWORD: "__DISABLED__",
    PPIQ_API_HOST_PORT: "5063",
    PPIQ_APP_HOST_PORT: "5173",
    PPIQ_WEBSITE_HOST_PORT: "5174",
    SITE_HOST: "localhost",
    WEBSITE_HOST: "website.localhost",
    ACME_EMAIL: "admin@example.invalid",
    CADDY_AUTO_HTTPS: "off",
    PPIQ_API_UPSTREAM: "plantprocess-api:5063",
    PPIQ_APP_UPSTREAM: "plantprocess-app-web:80",
    PPIQ_WEBSITE_UPSTREAM: "plantprocess-website:80",
  };

  const composeArgs = [];
  for (const file of files) {
    composeArgs.push("-f", full(file));
  }
  composeArgs.push("config");

  // Windows-safe Docker resolution:
  // 1. Try docker.exe directly.
  // 2. Try docker through cmd.exe, which uses the same command lookup style users see in PowerShell/CMD.
  // 3. If both fail, print a useful diagnostic.
  let result = runCommand("docker.exe", ["compose", ...composeArgs], env);

  if (result.error && result.error.code === "ENOENT") {
    const cmdLine = ["docker", "compose", ...composeArgs.map((x) => `"${String(x).replaceAll('"', '\\"')}"`)].join(" ");
    result = runCommand("cmd.exe", ["/d", "/s", "/c", cmdLine], env);
  }

  if (result.error) {
    fail(
      "docker compose config could not start for " +
        label +
        ": " +
        result.error.message +
        "\nPATH=" +
        (env.PATH || env.Path || "")
    );
  }

  if (result.status !== 0) {
    fail(
      "docker compose config failed for " +
        label +
        "\nSTDOUT:\n" +
        (result.stdout || "") +
        "\nSTDERR:\n" +
        (result.stderr || "")
    );
  }

  return result.stdout || "";
}

function assertNoRealSecretsInEnv(rel) {
  const text = read(rel);
  const lines = text.split(/\r?\n/);

  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) continue;

    const lower = trimmed.toLowerCase();

    const looksSensitive =
      lower.includes("password=") ||
      lower.includes("_password=") ||
      lower.includes("signing_key=") ||
      lower.includes("signingkey=");

    if (!looksSensitive) continue;

    const allowedPlaceholder =
      trimmed.includes("CHANGE_ME") ||
      trimmed.includes("__DISABLED__") ||
      trimmed.includes("example.invalid");

    if (!allowedPlaceholder) {
      fail(rel + " has non-placeholder sensitive value: " + trimmed);
    }
  }
}

const required = [
  "deploy/caddy/Caddyfile",
  "deploy/caddy/README.md",
  "deploy/compose/README.md",
  "deploy/compose/docker-compose.yml",
  "deploy/compose/docker-compose.local-native-main-db.yml",
  "deploy/compose/docker-compose.server-docker-main-db.yml",
  "deploy/compose/docker-compose.customer-template.yml",
  "deploy/compose/env/.env.local-native-main-db.example",
  "deploy/compose/env/.env.server-docker-main-db.example",
  "deploy/compose/env/.env.customer-template.example",
  "docs/phase3/P3_T16_CANONICAL_ENV_DEPLOY.md",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const caddy = read("deploy/caddy/Caddyfile");
if (!caddy.includes("PPIQ_REALIZATION_T016_CANONICAL_CADDYFILE")) fail("missing canonical Caddy marker");
if (!caddy.includes(marker)) fail("missing P3-T16 marker");
if (!caddy.includes("Strict-Transport-Security")) fail("Caddy missing HSTS");
if (!caddy.includes("Content-Security-Policy")) fail("Caddy missing CSP");
if (!caddy.includes("X-Content-Type-Options")) fail("Caddy missing nosniff header");
if (!caddy.includes("PPIQ_API_UPSTREAM:plantprocess-api:5063")) fail("Caddy API upstream must default to plantprocess-api:5063");
if (caddy.includes("plantprocess-api:8080")) fail("Caddy still contains stale plantprocess-api:8080 drift");

const base = read("deploy/compose/docker-compose.yml");
if (!base.includes("ppiq-postgres")) fail("base compose missing ppiq-postgres");
if (!base.includes("plantprocess-api")) fail("base compose missing plantprocess-api");
if (!base.includes("plantprocess-workers")) fail("base compose missing workers");
if (!base.includes("plantprocess-app-web")) fail("base compose missing app web");
if (!base.includes("plantprocess-website")) fail("base compose missing website");

const local = read("deploy/compose/docker-compose.local-native-main-db.yml");
if (!local.includes("local-native-main-db")) fail("local overlay missing topology");
if (!local.includes("host.docker.internal")) fail("local overlay must support host-native PostgreSQL");
if (!local.includes("depends_on: !reset []")) fail("local overlay must reset Docker main-DB dependency");
if (!local.includes("PPIQ_MAIN_DB_CONNECTION_STRING")) fail("local overlay must be connection-string driven");

const server = read("deploy/compose/docker-compose.server-docker-main-db.yml");
if (!server.includes("server-docker-main-db")) fail("server overlay missing topology");
if (!server.includes("ppiq-postgres")) fail("server overlay must include Docker main DB");
if (!server.includes("127.0.0.1:")) fail("server overlay must loopback-bind DB host port");
if (!server.includes("condition: service_healthy")) fail("server overlay must keep DB health dependency");

const customer = read("deploy/compose/docker-compose.customer-template.yml");
if (!customer.includes("customer-managed-or-external")) fail("customer overlay missing generic topology");
if (!customer.includes("depends_on: !reset []")) fail("customer overlay must reset Docker main-DB dependency");
if (!customer.includes("PPIQ_MAIN_DB_CONNECTION_STRING")) fail("customer overlay must be connection-string driven");
if (!customer.includes("PPIQ_DB_TOPOLOGY")) fail("customer overlay must expose topology label");

for (const envFile of [
  "deploy/compose/env/.env.local-native-main-db.example",
  "deploy/compose/env/.env.server-docker-main-db.example",
  "deploy/compose/env/.env.customer-template.example",
]) {
  const text = read(envFile);
  if (!text.includes(marker)) fail(envFile + " missing marker");
  assertNoRealSecretsInEnv(envFile);
}

runDockerComposeConfig(
  [
    "deploy/compose/docker-compose.yml",
    "deploy/compose/docker-compose.local-native-main-db.yml",
  ],
  "local-native-main-db"
);

runDockerComposeConfig(
  [
    "deploy/compose/docker-compose.yml",
    "deploy/compose/docker-compose.server-docker-main-db.yml",
  ],
  "server-docker-main-db"
);

runDockerComposeConfig(
  [
    "deploy/compose/docker-compose.yml",
    "deploy/compose/docker-compose.customer-template.yml",
  ],
  "customer-template"
);

console.log("[GREEN] P3-T16 static validation passed.");
