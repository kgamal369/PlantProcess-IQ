const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const stamp = new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".phase3_backups", "P3-T16_JS_" + stamp);

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

function write(rel, content) {
  const target = full(rel);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content.replace(/\r?\n/g, "\r\n"), "utf8");
  console.log("[P3-T16] wrote " + rel);
}

function backup(rel) {
  if (!exists(rel)) return;

  const target = path.join(backupRoot, rel.replaceAll("/", path.sep));
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(full(rel), target);
}

function fail(message) {
  console.error("[RED] P3-T16 failed: " + message);
  process.exit(1);
}

function runDockerComposeConfig(files, evidenceName) {
  const env = {
    ...process.env,

    // Safe dummy values used only for config parsing.
    POSTGRES_USER: "plantprocess_admin",
    POSTGRES_PASSWORD: "P3T16_config_parse_only_64_chars_xxxxxxxxxxxxxxxxxxxxxxxxx",
    POSTGRES_DB: "plantprocess_app_db",
    POSTGRES_PORT: "55433",

    PPIQ_MAIN_DB_CONNECTION_STRING:
      "Host=host.docker.internal;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=local-only-config-parse",

    PPIQ_SIGNING_KEY:
      "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
    PPIQ_BOOTSTRAP_ADMIN_PASSWORD:
      "Ppiq-Config-Parse-Only-Password-1234567890-Aa!",

    SITE_HOST: "localhost",
    WEBSITE_HOST: "website.localhost",
    ACME_EMAIL: "admin@example.invalid",
    CADDY_AUTO_HTTPS: "off",
    PPIQ_API_UPSTREAM: "plantprocess-api:5063",
    PPIQ_APP_UPSTREAM: "plantprocess-app-web:80",
    PPIQ_WEBSITE_UPSTREAM: "plantprocess-website:80",
  };

  const args = ["compose"];
  for (const file of files) {
    args.push("-f", full(file));
  }
  args.push("config");

  try {
    const output = childProcess.execFileSync("docker", args, {
      cwd: root,
      env,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"],
    });

    write("Documentation/P3-T16_ComposeConfig_" + evidenceName + ".yml", output);
    return output;
  } catch (err) {
    const stdout = err.stdout ? String(err.stdout) : "";
    const stderr = err.stderr ? String(err.stderr) : "";
    fail(
      "docker compose config failed for " +
        files.join(" + ") +
        "\nSTDOUT:\n" +
        stdout +
        "\nSTDERR:\n" +
        stderr
    );
  }
}

[
  "deploy/caddy/Caddyfile",
  "deploy/caddy/README.md",
  "deploy/compose/README.md",
  "deploy/compose/docker-compose.local-native-main-db.yml",
  "deploy/compose/docker-compose.server-docker-main-db.yml",
  "deploy/compose/docker-compose.customer-template.yml",
  "deploy/compose/env/.env.local-native-main-db.example",
  "deploy/compose/env/.env.server-docker-main-db.example",
  "deploy/compose/env/.env.customer-template.example",
  "tools/phase3/validate-p3-t16-canonical-env-deploy.cjs",
  "docs/phase3/P3_T16_CANONICAL_ENV_DEPLOY.md",
].forEach(backup);

write("deploy/caddy/Caddyfile", `
# PPIQ_REALIZATION_T016_CANONICAL_CADDYFILE
# ${marker}
# Single canonical PlantProcess IQ Caddyfile.
#
# Environment variables:
# - SITE_HOST: primary application host, e.g. plantprocessiq.example.com or localhost
# - WEBSITE_HOST: optional public website host, e.g. www.plantprocessiq.example.com
# - ACME_EMAIL: certificate contact email
# - CADDY_AUTO_HTTPS: usually "on" or "off" for local config parsing
# - PPIQ_API_UPSTREAM: default plantprocess-api:5063
# - PPIQ_APP_UPSTREAM: default plantprocess-app-web:80
# - PPIQ_WEBSITE_UPSTREAM: default plantprocess-website:80

{
    email {$ACME_EMAIL:admin@example.invalid}
    auto_https {$CADDY_AUTO_HTTPS:off}
}

{$SITE_HOST:localhost} {
    encode zstd gzip

    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains; preload"
        Content-Security-Policy "default-src 'self'; connect-src 'self' https: http://localhost:* ws://localhost:*; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'; script-src 'self'; font-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'"
        X-Content-Type-Options "nosniff"
        X-Frame-Options "DENY"
        Referrer-Policy "strict-origin-when-cross-origin"
        Permissions-Policy "camera=(), microphone=(), geolocation=()"
    }

    handle_path /api/* {
        reverse_proxy {$PPIQ_API_UPSTREAM:plantprocess-api:5063}
    }

    handle_path /health* {
        reverse_proxy {$PPIQ_API_UPSTREAM:plantprocess-api:5063}
    }

    handle_path /readiness* {
        reverse_proxy {$PPIQ_API_UPSTREAM:plantprocess-api:5063}
    }

    handle_path /website/* {
        reverse_proxy {$PPIQ_WEBSITE_UPSTREAM:plantprocess-website:80}
    }

    handle {
        reverse_proxy {$PPIQ_APP_UPSTREAM:plantprocess-app-web:80}
    }
}

{$WEBSITE_HOST:website.localhost} {
    encode zstd gzip

    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains; preload"
        X-Content-Type-Options "nosniff"
        X-Frame-Options "DENY"
        Referrer-Policy "strict-origin-when-cross-origin"
    }

    handle_path /api/* {
        reverse_proxy {$PPIQ_API_UPSTREAM:plantprocess-api:5063}
    }

    handle {
        reverse_proxy {$PPIQ_WEBSITE_UPSTREAM:plantprocess-website:80}
    }
}
`);

write("deploy/caddy/README.md", `
# PlantProcess IQ — Canonical Caddy Deployment

Marker: ${marker}

This folder owns the single canonical reverse-proxy configuration.

## Rule

Do not create environment-specific Caddyfiles.

Use one Caddyfile and switch behavior through environment variables:

- SITE_HOST
- WEBSITE_HOST
- ACME_EMAIL
- CADDY_AUTO_HTTPS
- PPIQ_API_UPSTREAM
- PPIQ_APP_UPSTREAM
- PPIQ_WEBSITE_UPSTREAM

## Default upstream contract

- API: plantprocess-api:5063
- App frontend: plantprocess-app-web:80
- Website: plantprocess-website:80

## Why

This prevents drift between local, server, and customer deployments. The same reverse-proxy file is used everywhere; only the environment profile changes.
`);

write("deploy/compose/README.md", `
# PlantProcess IQ — Canonical Compose Environment Profiles

Marker: ${marker}

This folder is the canonical deployment root for Compose files.

## Supported main-database topologies

### 1. Local laptop development

Main PlantProcess IQ PostgreSQL is installed directly on the laptop/Windows host.

Use:

    docker-compose.demo.yml
    docker-compose.local-native-main-db.yml

The local overlay points app containers to host.docker.internal and does not require the main DB container.

### 2. Server deployment

All databases, including the main PlantProcess IQ PostgreSQL DB, run as Docker containers.

Use:

    docker-compose.demo.yml
    docker-compose.server-docker-main-db.yml

The server overlay keeps PostgreSQL loopback-bound and lets app containers reach it on the private Docker network.

### 3. Customer deployment

Customer topology can vary: native DB, managed DB, VM DB, Kubernetes service, or Docker DB.

Use:

    docker-compose.demo.yml
    docker-compose.customer-template.yml

The customer overlay relies on PPIQ_MAIN_DB_CONNECTION_STRING and does not hardcode the DB topology.

## Non-negotiables

- Never commit real secrets.
- Never hardcode one DB topology into product scripts.
- Caddy is the only public ingress.
- DB host ports are loopback-only when exposed.
- Runtime environment files are server/customer/local private files, not tracked source.
`);

write("deploy/compose/docker-compose.local-native-main-db.yml", `
# ${marker}
# P3-T16 local laptop profile.
# Main PlantProcess IQ Postgres is native on the laptop/host.
# Demo/customer-source DB containers are separate and should be started through demo-source compose files.

services:
  plantprocess-api:
    environment:
      PPIQ_DB_TOPOLOGY: "local-native-main-db"
      ASPNETCORE_ENVIRONMENT: "Production"
      ASPNETCORE_URLS: "http://0.0.0.0:5063"
      ConnectionStrings__DefaultConnection: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set PPIQ_MAIN_DB_CONNECTION_STRING for local native DB}"
      ConnectionStrings__PlantProcessDb: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set PPIQ_MAIN_DB_CONNECTION_STRING for local native DB}"
      PlantProcess__Auth__SigningKey: "\${PPIQ_SIGNING_KEY:?Set strong PPIQ_SIGNING_KEY}"
      PlantProcess__Auth__BootstrapAdminPassword: "\${PPIQ_BOOTSTRAP_ADMIN_PASSWORD:-__DISABLED__}"
    depends_on: []
    extra_hosts:
      - "host.docker.internal:host-gateway"
    ports:
      - "127.0.0.1:\${PPIQ_API_HOST_PORT:-5063}:5063"

  plantprocess-workers:
    environment:
      PPIQ_DB_TOPOLOGY: "local-native-main-db"
      DOTNET_ENVIRONMENT: "Production"
      ConnectionStrings__DefaultConnection: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set PPIQ_MAIN_DB_CONNECTION_STRING for local native DB}"
      ConnectionStrings__PlantProcessDb: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set PPIQ_MAIN_DB_CONNECTION_STRING for local native DB}"
    depends_on: []
    extra_hosts:
      - "host.docker.internal:host-gateway"

  plantprocess-app-web:
    ports:
      - "127.0.0.1:\${PPIQ_APP_HOST_PORT:-5173}:80"

  plantprocess-website:
    ports:
      - "127.0.0.1:\${PPIQ_WEBSITE_HOST_PORT:-5174}:80"
`);

write("deploy/compose/docker-compose.server-docker-main-db.yml", `
# ${marker}
# P3-T16 server profile.
# Main PlantProcess IQ Postgres is a Docker container.
# Caddy is the only public ingress.

services:
  ppiq-postgres:
    profiles: ["server-docker-main-db"]
    ports:
      - "127.0.0.1:\${POSTGRES_PORT:-5432}:5432"

  plantprocess-api:
    environment:
      PPIQ_DB_TOPOLOGY: "server-docker-main-db"
      ASPNETCORE_ENVIRONMENT: "Production"
      ASPNETCORE_URLS: "http://0.0.0.0:5063"
      ConnectionStrings__DefaultConnection: "Host=ppiq-postgres;Port=5432;Database=\${POSTGRES_DB:-plantprocess_app_db};Username=\${POSTGRES_USER:-plantprocess_admin};Password=\${POSTGRES_PASSWORD:?Set POSTGRES_PASSWORD};Include Error Detail=false"
      ConnectionStrings__PlantProcessDb: "Host=ppiq-postgres;Port=5432;Database=\${POSTGRES_DB:-plantprocess_app_db};Username=\${POSTGRES_USER:-plantprocess_admin};Password=\${POSTGRES_PASSWORD:?Set POSTGRES_PASSWORD};Include Error Detail=false"
      PlantProcess__Auth__SigningKey: "\${PPIQ_SIGNING_KEY:?Set strong PPIQ_SIGNING_KEY}"
      PlantProcess__Auth__BootstrapAdminPassword: "\${PPIQ_BOOTSTRAP_ADMIN_PASSWORD:-__DISABLED__}"
    depends_on:
      ppiq-postgres:
        condition: service_healthy

  plantprocess-workers:
    environment:
      PPIQ_DB_TOPOLOGY: "server-docker-main-db"
      DOTNET_ENVIRONMENT: "Production"
      ConnectionStrings__DefaultConnection: "Host=ppiq-postgres;Port=5432;Database=\${POSTGRES_DB:-plantprocess_app_db};Username=\${POSTGRES_USER:-plantprocess_admin};Password=\${POSTGRES_PASSWORD:?Set POSTGRES_PASSWORD};Include Error Detail=false"
      ConnectionStrings__PlantProcessDb: "Host=ppiq-postgres;Port=5432;Database=\${POSTGRES_DB:-plantprocess_app_db};Username=\${POSTGRES_USER:-plantprocess_admin};Password=\${POSTGRES_PASSWORD:?Set POSTGRES_PASSWORD};Include Error Detail=false"
    depends_on:
      ppiq-postgres:
        condition: service_healthy
`);

write("deploy/compose/docker-compose.customer-template.yml", `
# ${marker}
# P3-T16 customer-flexible profile.
# Customer DB topology can be native, managed, VM-hosted, Docker, or platform service.
# The only required input is PPIQ_MAIN_DB_CONNECTION_STRING.

services:
  plantprocess-api:
    environment:
      PPIQ_DB_TOPOLOGY: "\${PPIQ_DB_TOPOLOGY:-customer-managed-or-external}"
      ASPNETCORE_ENVIRONMENT: "Production"
      ASPNETCORE_URLS: "http://0.0.0.0:5063"
      ConnectionStrings__DefaultConnection: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set customer-specific main DB connection string}"
      ConnectionStrings__PlantProcessDb: "\${PPIQ_MAIN_DB_CONNECTION_STRING:?Set customer-specific main DB connection string}"
      PlantProcess__Auth__SigningKey: "\${PPIQ_SIGNING_KEY:?Set strong PPIQ_SIGNING_KEY}"
      PlantProcess__Auth__BootstrapAdminPassword: "\${PPIQ_BOOTSTRAP_ADMIN_PASSWORD:-__DISABLED__}"

  plantprocess-workers:
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
PPIQ_MAIN_DB_CONNECTION_STRING=Host=host.docker.internal;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=CHANGE_ME_LOCAL_ONLY;Include Error Detail=true

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

write("deploy/compose/env/.env.server-docker-main-db.example", `
# ${marker}
# Server profile.
# Main PlantProcess IQ DB is Docker PostgreSQL.

PPIQ_DB_TOPOLOGY=server-docker-main-db

POSTGRES_USER=plantprocess_admin
POSTGRES_PASSWORD=CHANGE_ME_SERVER_SECRET
POSTGRES_DB=plantprocess_app_db
POSTGRES_PORT=5432

PPIQ_SIGNING_KEY=CHANGE_ME_64_PLUS_CHAR_RANDOM_SECRET_FOR_SERVER_RUNTIME_ONLY_000000
PPIQ_BOOTSTRAP_ADMIN_PASSWORD=__DISABLED__

SITE_HOST=plantprocessiq.example.com
WEBSITE_HOST=www.plantprocessiq.example.com
ACME_EMAIL=admin@example.com
CADDY_AUTO_HTTPS=on
PPIQ_API_UPSTREAM=plantprocess-api:5063
PPIQ_APP_UPSTREAM=plantprocess-app-web:80
PPIQ_WEBSITE_UPSTREAM=plantprocess-website:80
`);

write("deploy/compose/env/.env.customer-template.example", `
# ${marker}
# Customer-flexible profile.
# DB can be native, managed, VM, Docker, Kubernetes service, or customer-provided endpoint.

PPIQ_DB_TOPOLOGY=customer-managed-or-external
PPIQ_MAIN_DB_CONNECTION_STRING=Host=CHANGE_ME;Port=5432;Database=CHANGE_ME;Username=CHANGE_ME;Password=CHANGE_ME;Include Error Detail=false

PPIQ_SIGNING_KEY=CHANGE_ME_64_PLUS_CHAR_RANDOM_SECRET_FOR_CUSTOMER_RUNTIME_ONLY_000000
PPIQ_BOOTSTRAP_ADMIN_PASSWORD=__DISABLED__

SITE_HOST=plantprocessiq.customer.example
WEBSITE_HOST=www.plantprocessiq.customer.example
ACME_EMAIL=admin@customer.example
CADDY_AUTO_HTTPS=on
PPIQ_API_UPSTREAM=plantprocess-api:5063
PPIQ_APP_UPSTREAM=plantprocess-app-web:80
PPIQ_WEBSITE_UPSTREAM=plantprocess-website:80
`);

write("docs/phase3/P3_T16_CANONICAL_ENV_DEPLOY.md", `
# P3-T16 — Canonical Caddyfile and Compose Per Environment

Marker: ${marker}

## Result

P3-T16 establishes a generic deployment contract:

1. One canonical Caddyfile:
   - deploy/caddy/Caddyfile

2. One canonical compose base:
   - deploy/compose/docker-compose.demo.yml

3. Environment-specific overlays:
   - deploy/compose/docker-compose.local-native-main-db.yml
   - deploy/compose/docker-compose.server-docker-main-db.yml
   - deploy/compose/docker-compose.customer-template.yml

4. Safe example env templates:
   - deploy/compose/env/.env.local-native-main-db.example
   - deploy/compose/env/.env.server-docker-main-db.example
   - deploy/compose/env/.env.customer-template.example

## Environment policy

### Local laptop

The main PlantProcess IQ PostgreSQL DB is native Windows PostgreSQL. App containers connect to it through host.docker.internal.

Demo/customer-source DBs remain Docker containers.

### Server

All DBs are Docker containers.

### Customer

Topology is not assumed. Customer deployment uses PPIQ_MAIN_DB_CONNECTION_STRING and PPIQ_DB_TOPOLOGY.

## Validation

Run:

    node tools/phase3/validate-p3-t16-canonical-env-deploy.cjs

The validator checks:

- Caddyfile marker and security headers.
- Caddy upstream uses plantprocess-api:5063, not stale 8080 drift.
- Local, server, and customer compose overlays exist.
- Local profile does not require a Docker main DB.
- Server profile keeps Postgres loopback-bound.
- Customer profile is connection-string driven.
- Docker Compose config parses for all three profiles using safe dummy env values.
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

function assertIncludes(file, value, message) {
  if (!read(file).includes(value)) fail(message + " in " + file);
}

function runComposeConfig(files, label) {
  const env = {
    ...process.env,
    POSTGRES_USER: "plantprocess_admin",
    POSTGRES_PASSWORD: "P3T16_config_parse_only_64_chars_xxxxxxxxxxxxxxxxxxxxxxxxx",
    POSTGRES_DB: "plantprocess_app_db",
    POSTGRES_PORT: "55433",
    PPIQ_MAIN_DB_CONNECTION_STRING:
      "Host=host.docker.internal;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=config-parse-only",
    PPIQ_SIGNING_KEY:
      "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
    PPIQ_BOOTSTRAP_ADMIN_PASSWORD:
      "Ppiq-Config-Parse-Only-Password-1234567890-Aa!",
    SITE_HOST: "localhost",
    WEBSITE_HOST: "website.localhost",
    ACME_EMAIL: "admin@example.invalid",
    CADDY_AUTO_HTTPS: "off",
    PPIQ_API_UPSTREAM: "plantprocess-api:5063",
    PPIQ_APP_UPSTREAM: "plantprocess-app-web:80",
    PPIQ_WEBSITE_UPSTREAM: "plantprocess-website:80",
  };

  const args = ["compose"];
  for (const file of files) {
    args.push("-f", full(file));
  }
  args.push("config");

  try {
    childProcess.execFileSync("docker", args, {
      cwd: root,
      env,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"],
    });
  } catch (err) {
    fail(
      "docker compose config failed for " +
        label +
        "\\n" +
        String(err.stdout ?? "") +
        "\\n" +
        String(err.stderr ?? "")
    );
  }
}

const required = [
  "deploy/caddy/Caddyfile",
  "deploy/caddy/README.md",
  "deploy/compose/README.md",
  "deploy/compose/docker-compose.demo.yml",
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
if (!caddy.includes(marker)) fail("missing P3-T16 v2 marker");
if (!caddy.includes("Strict-Transport-Security")) fail("Caddy missing HSTS");
if (!caddy.includes("Content-Security-Policy")) fail("Caddy missing CSP");
if (!caddy.includes("X-Content-Type-Options")) fail("Caddy missing nosniff header");
if (!caddy.includes("PPIQ_API_UPSTREAM:plantprocess-api:5063")) fail("Caddy API upstream must default to plantprocess-api:5063");
if (caddy.includes("plantprocess-api:8080")) fail("Caddy still contains stale plantprocess-api:8080 drift");

const base = read("deploy/compose/docker-compose.demo.yml");
if (!base.includes("ppiq-postgres")) fail("base compose missing ppiq-postgres");
if (!base.includes("plantprocess-api")) fail("base compose missing plantprocess-api");
if (!base.includes("plantprocess-workers")) fail("base compose missing workers");
if (!base.includes("plantprocess-app-web")) fail("base compose missing app web");
if (!base.includes("plantprocess-website")) fail("base compose missing website");
if (!base.includes("127.0.0.1:")) fail("base compose should loopback-bind DB when host port exists");

const local = read("deploy/compose/docker-compose.local-native-main-db.yml");
if (!local.includes("local-native-main-db")) fail("local overlay missing topology");
if (!local.includes("host.docker.internal")) fail("local overlay must support host-native PostgreSQL");
if (!local.includes("depends_on: []")) fail("local overlay must remove Docker main-DB dependency for API/workers");
if (!local.includes("PPIQ_MAIN_DB_CONNECTION_STRING")) fail("local overlay must be connection-string driven");

const server = read("deploy/compose/docker-compose.server-docker-main-db.yml");
if (!server.includes("server-docker-main-db")) fail("server overlay missing topology");
if (!server.includes("ppiq-postgres")) fail("server overlay must include Docker main DB");
if (!server.includes("127.0.0.1:")) fail("server overlay must loopback-bind DB host port");
if (!server.includes("condition: service_healthy")) fail("server overlay must keep DB health dependency");

const customer = read("deploy/compose/docker-compose.customer-template.yml");
if (!customer.includes("customer-managed-or-external")) fail("customer overlay missing generic topology");
if (!customer.includes("PPIQ_MAIN_DB_CONNECTION_STRING")) fail("customer overlay must be connection-string driven");
if (!customer.includes("PPIQ_DB_TOPOLOGY")) fail("customer overlay must expose topology label");

for (const envFile of [
  "deploy/compose/env/.env.local-native-main-db.example",
  "deploy/compose/env/.env.server-docker-main-db.example",
  "deploy/compose/env/.env.customer-template.example",
]) {
  const text = read(envFile);
  if (!text.includes(marker)) fail(envFile + " missing marker");
  if (/Password=(?!CHANGE_ME|local-only-config-parse)/i.test(text)) {
    fail(envFile + " appears to contain a real password; only placeholders allowed");
  }
}

runComposeConfig(
  [
    "deploy/compose/docker-compose.demo.yml",
    "deploy/compose/docker-compose.local-native-main-db.yml",
  ],
  "local-native-main-db",
);

runComposeConfig(
  [
    "deploy/compose/docker-compose.demo.yml",
    "deploy/compose/docker-compose.server-docker-main-db.yml",
  ],
  "server-docker-main-db",
);

runComposeConfig(
  [
    "deploy/compose/docker-compose.demo.yml",
    "deploy/compose/docker-compose.customer-template.yml",
  ],
  "customer-template",
);

console.log("[GREEN] P3-T16 static validation passed.");
`);

runDockerComposeConfig(
  [
    "deploy/compose/docker-compose.demo.yml",
    "deploy/compose/docker-compose.local-native-main-db.yml",
  ],
  "local_native_main_db"
);

runDockerComposeConfig(
  [
    "deploy/compose/docker-compose.demo.yml",
    "deploy/compose/docker-compose.server-docker-main-db.yml",
  ],
  "server_docker_main_db"
);

runDockerComposeConfig(
  [
    "deploy/compose/docker-compose.demo.yml",
    "deploy/compose/docker-compose.customer-template.yml",
  ],
  "customer_template"
);

console.log("[GREEN] P3-T16 patch applied.");