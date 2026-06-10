const fs = require("fs");
const path = require("path");

const root = process.cwd();
const marker = "PPIQ_P3_T18_READONLY_QUERY_PREVIEW_ROLE";

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
  console.error("[RED] P3-T18 validation failed: " + message);
  process.exit(1);
}

const required = [
  "Backend/database/scripts/426_p3_t18_readonly_query_preview_role.sql",
  "tools/phase3/apply-p3-t18-local-native-db.ps1",
  "deploy/compose/env/.env.query-preview-readonly.example",
  "docs/phase3/P3_T18_READONLY_QUERY_PREVIEW_ROLE.md",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const sql = read("Backend/database/scripts/426_p3_t18_readonly_query_preview_role.sql");
const ps1 = read("tools/phase3/apply-p3-t18-local-native-db.ps1");
const envExample = read("deploy/compose/env/.env.query-preview-readonly.example");
const doc = read("docs/phase3/P3_T18_READONLY_QUERY_PREVIEW_ROLE.md");

if (!sql.includes(marker)) fail("SQL missing marker");
if (!sql.includes("CREATE ROLE ppiq_query_preview_readonly")) fail("SQL missing read-only group role creation");
if (!sql.includes("NOLOGIN")) fail("read-only group role must be NOLOGIN");
if (!sql.includes("NOSUPERUSER")) fail("role must be NOSUPERUSER");
if (!sql.includes("NOCREATEDB")) fail("role must be NOCREATEDB");
if (!sql.includes("NOCREATEROLE")) fail("role must be NOCREATEROLE");
if (!sql.includes("NOBYPASSRLS")) fail("role must be NOBYPASSRLS");
if (!sql.includes("ppiq.query_preview_password")) fail("SQL must use runtime password setting, not committed password");
if (!sql.includes("ppiq_query_preview_login")) fail("SQL missing optional login role");
if (!sql.includes("GRANT USAGE ON SCHEMA public")) fail("SQL missing schema USAGE grant");
if (!sql.includes("GRANT SELECT ON")) fail("SQL missing approved SELECT grants");
if (!sql.includes("REVOKE INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER")) fail("SQL missing DML revoke");
if (!sql.includes("NoWritePrivilegesOnBaseTables")) fail("SQL status function missing write privilege gate");
if (!sql.includes("NoBroadBaseTableSelect")) fail("SQL status function missing broad select gate");
if (!sql.includes("ppiq_p3_t18_readonly_preview_role_status")) fail("SQL missing DB status function");

if (sql.match(/PASSWORD\s+'[^']+'/i)) {
  fail("SQL appears to contain a hard-coded role password");
}

if (!ps1.includes("PostgreSQL") || !ps1.includes("psql.exe")) fail("local native DB helper must use Windows psql path");
if (!ps1.includes("127.0.0.1")) fail("local native DB helper must default to loopback native Postgres");
if (!ps1.includes(".local-secrets")) fail("helper must write runtime secrets outside tracked source");
if (!ps1.includes("Password will not be printed")) fail("helper must not print generated password");

if (!envExample.includes(marker)) fail("env example missing marker");
if (!envExample.includes("CHANGE_ME_64_PLUS_CHAR_RANDOM_SECRET")) fail("env example must use placeholder password");
if (!envExample.includes("PPIQ_QUERY_PREVIEW_CONNECTION_STRING")) fail("env example missing query preview connection string");
if (/Password=(?!CHANGE_ME)/i.test(envExample)) fail("env example appears to contain non-placeholder password");

if (!doc.includes("native Windows PostgreSQL")) fail("doc must mention local native DB topology");
if (!doc.includes("Docker container")) fail("doc must mention server Docker DB topology");
if (!doc.includes("Customer DBA")) fail("doc must mention customer DBA/topology flexibility");

console.log("[GREEN] P3-T18 static validation passed.");