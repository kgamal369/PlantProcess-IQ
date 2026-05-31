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

const oldBlock = `DO $$
BEGIN
    CREATE EXTENSION IF NOT EXISTS vector;
EXCEPTION
    WHEN undefined_file THEN
        RAISE NOTICE 'pgvector extension is not installed. JSONB embedding fallback remains available.';
END $$;`;

const newBlock = `DO $$
BEGIN
    BEGIN
        CREATE EXTENSION IF NOT EXISTS vector;
    EXCEPTION
        WHEN OTHERS THEN
            RAISE NOTICE 'pgvector extension is not installed or cannot be created in this PostgreSQL instance. SQLSTATE=%, message=%. JSONB embedding fallback remains available.', SQLSTATE, SQLERRM;
    END;
END $$;`;

if (!sql.includes(oldBlock)) {
  const regex = /DO \$\$\s*BEGIN\s*CREATE EXTENSION IF NOT EXISTS vector;\s*EXCEPTION\s*WHEN undefined_file THEN\s*RAISE NOTICE 'pgvector extension is not installed\. JSONB embedding fallback remains available\.';\s*END \$\$;/m;

  if (!regex.test(sql)) {
    throw new Error("Could not find old pgvector extension block to patch.");
  }

  sql = sql.replace(regex, newBlock);
} else {
  sql = sql.replace(oldBlock, newBlock);
}

fs.writeFileSync(sqlPath, sql, "utf8");

console.log("OK patched pgvector extension block to JSONB fallback-safe mode.");