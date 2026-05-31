const fs = require("node:fs");
const path = require("node:path");

const repo = "C:\\Workspace\\PlantProcess-IQ";
const sqlPath = path.join(
  repo,
  "Backend",
  "database",
  "scripts",
  "200_phase02_ml_foundation_feature_store_pgvector.sql"
);

if (!fs.existsSync(sqlPath)) {
  throw new Error("SQL file not found: " + sqlPath);
}

let sql = fs.readFileSync(sqlPath, "utf8");

const backupPath = sqlPath + ".before-dollar-quote-repair.bak";
fs.writeFileSync(backupPath, sql, "utf8");

const safePgVectorBlock = [
  "DO $$",
  "BEGIN",
  "    BEGIN",
  "        CREATE EXTENSION IF NOT EXISTS vector;",
  "    EXCEPTION",
  "        WHEN OTHERS THEN",
  "            RAISE NOTICE 'pgvector extension is not installed or cannot be created in this PostgreSQL instance. SQLSTATE=%, message=%. JSONB embedding fallback remains available.', SQLSTATE, SQLERRM;",
  "    END;",
  "END $$;"
].join("\n");

const patterns = [
  /DO\s+\${1,2}\s*BEGIN\s*BEGIN\s*CREATE EXTENSION IF NOT EXISTS vector;[\s\S]*?END;\s*END\s+\${1,2};/m,
  /DO\s+\${1,2}\s*BEGIN\s*CREATE EXTENSION IF NOT EXISTS vector;[\s\S]*?END\s+\${1,2};/m
];

let patched = false;

for (const pattern of patterns) {
  if (pattern.test(sql)) {
    sql = sql.replace(pattern, () => safePgVectorBlock);
    patched = true;
    break;
  }
}

if (!patched) {
  const idx = sql.indexOf("CREATE EXTENSION IF NOT EXISTS vector");
  const context = idx >= 0
    ? sql.slice(Math.max(0, idx - 300), Math.min(sql.length, idx + 600))
    : "CREATE EXTENSION IF NOT EXISTS vector not found";

  throw new Error("Could not locate pgvector DO block. Context:\n" + context);
}

if (/DO\s+\$\s*\r?\n/.test(sql)) {
  throw new Error("Repair failed: file still contains invalid 'DO $' block.");
}

if (/END\s+\$;/.test(sql)) {
  throw new Error("Repair failed: file still contains invalid 'END $;' block.");
}

if (!sql.includes("DO $$") || !sql.includes("END $$;")) {
  throw new Error("Repair failed: expected DO $$ / END $$ block not found.");
}

fs.writeFileSync(sqlPath, sql, "utf8");

console.log("OK repaired pgvector DO block dollar quoting.");
console.log("Backup written to: " + backupPath);