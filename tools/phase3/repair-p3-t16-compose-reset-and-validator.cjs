const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const marker = "PPIQ_REALIZATION_T016_CANONICAL_ENV_DEPLOY_V2";

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function write(rel, content) {
  const target = full(rel);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content.replace(/\r?\n/g, "\r\n"), "utf8");
  console.log("[P3-T16-REPAIR] wrote " + rel);
}

function read(rel) {
  return fs.readFileSync(full(rel), "utf8");
}

function exists(rel) {
  return fs.existsSync(full(rel));
}

function fail(message) {
  console.error("[RED] P3-T16 validation failed: " + message);
  process.exit(1);
}

write("deploy/compose/docker-compose.local-native-main-db.yml", `
# ${marker}
# P3-T16 local laptop profile.
# Main PlantProcess IQ Postgres is native on the laptop/host.
# Demo/customer-source DB containers are separate and should be started through demo-source compose files.

services:
  # Keep the base DB service available for server/demo profiles, but hide it from default local-native-main-db usage.
  ppiq-postgres:
    profiles:
      - server-docker-main-db

  plantprocess-api:
    depends_on: !reset []
    environment:
      PPIQ_DB_TOPOLOGY: "local-native-main-db"
      ASPNETCORE_ENVIRONMENT: "Production"
      ASPNETCORE_URLS: "http://0.0.0.0:5063"
      ConnectionStrings__DefaultConnection: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set PPIQ_MAIN_DB_CONNECTION_STRING for local native DB}"
      ConnectionStrings__PlantProcessDb: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set PPIQ_MAIN_DB_CONNECTION_STRING for local native DB}"
      PlantProcess__Auth__SigningKey: "\${PPIQ_SIGNING_KEY:?Set strong PPIQ_SIGNING_KEY}"
      PlantProcess__Auth__BootstrapAdminPassword: "\${PPIQ_BOOTSTRAP_ADMIN_PASSWORD:-__DISABLED__}"
    extra_hosts:
      - "host.docker.internal:host-gateway"
    ports:
      - "127.0.0.1:\${PPIQ_API_HOST_PORT:-5063}:5063"

  plantprocess-workers:
    depends_on: !reset []
    environment:
      PPIQ_DB_TOPOLOGY: "local-native-main-db"
      DOTNET_ENVIRONMENT: "Production"
      ConnectionStrings__DefaultConnection: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set PPIQ_MAIN_DB_CONNECTION_STRING for local native DB}"
      ConnectionStrings__PlantProcessDb: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set PPIQ_MAIN_DB_CONNECTION_STRING for local native DB}"
    extra_hosts:
      - "host.docker.internal:host-gateway"

  plantprocess-app-web:
    ports:
      - "127.0.0.1:\${PPIQ_APP_HOST_PORT:-5173}:80"

  plantprocess-website:
    ports:
      - "127.0.0.1:\${PPIQ_WEBSITE_HOST_PORT:-5174}:80"
`);

write("deploy/compose/docker-compose.customer-template.yml", `
# ${marker}
# P3-T16 customer-flexible profile.
# Customer DB topology can be native, managed, VM-hosted, Docker, or platform service.
# The only required input is PPIQ_MAIN_DB_CONNECTION_STRING.

services:
  plantprocess-api:
    depends_on: !reset []
    environment:
      PPIQ_DB_TOPOLOGY: "\${PPIQ_DB_TOPOLOGY:-customer-managed-or-external}"
      ASPNETCORE_ENVIRONMENT: "Production"
      ASPNETCORE_URLS: "http://0.0.0.0:5063"
      ConnectionStrings__DefaultConnection: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set customer-specific main DB connection string}"
      ConnectionStrings__PlantProcessDb: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set customer-specific main DB connection string}"
      PlantProcess__Auth__SigningKey: "\${PPIQ_SIGNING_KEY:?Set strong PPIQ_SIGNING_KEY}"
      PlantProcess__Auth__BootstrapAdminPassword: "\${PPIQ_BOOTSTRAP_ADMIN_PASSWORD:-__DISABLED__}"

  plantprocess-workers:
    depends_on: !reset []
    environment:
      PPIQ_DB_TOPOLOGY: "\${PPIQ_DB_TOPOLOGY:-customer-managed-or-external}"
      DOTNET_ENVIRONMENT: "Production"
      ConnectionStrings__DefaultConnection: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set customer-specific main DB connection string}"
      ConnectionStrings__PlantProcessDb: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set customer-specific main DB connection string}"
`);

write("deploy/compose/env/.env.local-native-main-db.example", `
# ${marker}
# Local laptop development profile.
# Main PlantProcess IQ DB is native Windows PostgreSQL, not Docker.

PPIQ_DB_TOPOLOGY=local-native-main-db
PPIQ_MAIN_DB_CONNECTION_STRING=Host=host.docker.internal;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=CHANGE_ME_LOCAL_NATIVE_DB_PASSWORD;Include Error Detail=true

PPIQ_SIGNING_KEY=CHANGE_ME_64_PLUS_CHAR_RANDOM_SECRET_FOR_LOCAL_RUNTIME_ONLY_000000
PPIQ_BOOTSTRAP_ADMIN_PASSWORD=__DISABLED__

PPIQ_API_HOST_PORT=5063
PPIQ_APP_HOST_PORT=5173
PPIQ_WEBSITE_HOST_PORT=5174

SITE_HOST=localhost
WEBSITE_HOST=website.localhost
ACME_EMAIL=admin@example.invalid
CADDY_AUTO_HTTPS=off
PPIQ_API_UPSTREAM=plantprocess-api:5063
PPIQ_APP_UPSTREAM=plantprocess-app-web:80
PPIQ_WEBSITE_UPSTREAM=plantprocess-website:80
`);

write("tools/phase3/validate-p3-t16-canonical-env-deploy.cjs", `
const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const marker = "${marker}";

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

function runComposeConfig(files, label) {
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

  const args = ["compose"];
  for (const file of files) args.push("-f", full(file));
  args.push("config");

  const result = childProcess.spawnSync("docker", args, {
    cwd: root,
    env,
    encoding: "utf8",
    shell: false,
  });

  if (result.error) {
    fail("docker compose config could not start for " + label + ": " + result.error.message);
  }

  if (result.status !== 0) {
    fail(
      "docker compose config failed for " +
      label +
      "\\nSTDOUT:\\n" +
      (result.stdout || "") +
      "\\nSTDERR:\\n" +
      (result.stderr || "")
    );
  }

  return result.stdout || "";
}

function assertNoRealSecretsInEnv(rel) {
  const text = read(rel);
  const lines = text.split(/\\r?\\n/);

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

runComposeConfig(
  [
    "deploy/compose/docker-compose.yml",
    "deploy/compose/docker-compose.local-native-main-db.yml",
  ],
  "local-native-main-db"
);

runComposeConfig(
  [
    "deploy/compose/docker-compose.yml",
    "deploy/compose/docker-compose.server-docker-main-db.yml",
  ],
  "server-docker-main-db"
);

runComposeConfig(
  [
    "deploy/compose/docker-compose.yml",
    "deploy/compose/docker-compose.customer-template.yml",
  ],
  "customer-template"
);

console.log("[GREEN] P3-T16 static validation passed.");
`);

console.log("[GREEN] P3-T16 repair files written.");
