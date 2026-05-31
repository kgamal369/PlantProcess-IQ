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

const backupPath = sqlPath + ".before-local-safe-repair.bak";
const originalBuffer = fs.readFileSync(sqlPath);
fs.writeFileSync(backupPath, originalBuffer);

let sql = originalBuffer.toString("utf8");

// Normalize line endings first.
sql = sql.replace(/^\uFEFF/, "");
sql = sql.replace(/\r\n/g, "\n").replace(/\r/g, "\n");

// Remove all non-ASCII characters.
// This prevents Windows psql/client-encoding problems from corrupted comments.
sql = sql.replace(/[^\x09\x0A\x20-\x7E]/g, " ");

// Repair any corrupted single-dollar PostgreSQL blocks.
sql = sql.replace(/DO\s+\$(?!\$)/g, "DO $$");
sql = sql.replace(/END\s+\$(?=;)/g, "END $$");

// Replace the first pgvector CREATE EXTENSION DO block with a safe fallback block.
const vectorNeedle = "CREATE EXTENSION IF NOT EXISTS vector;";
const vectorIndex = sql.indexOf(vectorNeedle);

if (vectorIndex < 0) {
  throw new Error("Could not find CREATE EXTENSION IF NOT EXISTS vector;");
}

const blockStart = sql.lastIndexOf("DO $$", vectorIndex);
const blockEnd = sql.indexOf("END $$;", vectorIndex);

if (blockStart < 0 || blockEnd < 0) {
  const context = sql.slice(Math.max(0, vectorIndex - 300), Math.min(sql.length, vectorIndex + 500));
  throw new Error("Could not find pgvector DO $$ block around vector extension. Context:\n" + context);
}

const safeVectorBlock = [
  "DO $$",
  "BEGIN",
  "    BEGIN",
  "        CREATE EXTENSION IF NOT EXISTS vector;",
  "    EXCEPTION",
  "        WHEN OTHERS THEN",
  "            RAISE NOTICE 'pgvector extension unavailable; JSONB embedding fallback remains active. SQLSTATE=%, message=%', SQLSTATE, SQLERRM;",
  "    END;",
  "END $$;"
].join("\n");

sql =
  sql.slice(0, blockStart) +
  safeVectorBlock +
  sql.slice(blockEnd + "END $$;".length);

// Required sanity checks.
const requiredTokens = [
  "CREATE TABLE IF NOT EXISTS public.ml_feature_definitions",
  "CREATE TABLE IF NOT EXISTS public.ml_feature_values",
  "CREATE TABLE IF NOT EXISTS public.ml_outcome_definitions",
  "CREATE TABLE IF NOT EXISTS public.ml_outcome_values",
  "CREATE TABLE IF NOT EXISTS public.ml_feature_store_refresh_runs",
  "CREATE TABLE IF NOT EXISTS public.ml_correlation_compute_runs",
  "CREATE TABLE IF NOT EXISTS public.ml_correlation_results_v2",
  "CREATE TABLE IF NOT EXISTS public.ml_knowledge_base_items",
  "CREATE OR REPLACE FUNCTION public.ppiq_ml_seed_foundation_catalog",
  "CREATE OR REPLACE FUNCTION public.ppiq_ml_refresh_feature_store",
  "CREATE OR REPLACE FUNCTION public.ppiq_ml_compute_basic_correlations",
  "CREATE OR REPLACE FUNCTION public.ppiq_ml_upsert_kb_item",
  "CREATE OR REPLACE FUNCTION public.ppiq_ml_search_kb"
];

for (const token of requiredTokens) {
  if (!sql.includes(token)) {
    throw new Error("Repaired SQL is missing required token: " + token);
  }
}

if (/DO\s+\$(?!\$)/.test(sql)) {
  throw new Error("Repaired SQL still contains invalid DO $ block.");
}

if (/END\s+\$(?=;)/.test(sql)) {
  throw new Error("Repaired SQL still contains invalid END $; block.");
}

for (let i = 0; i < sql.length; i++) {
  if (sql.charCodeAt(i) > 127) {
    throw new Error("Repaired SQL still contains non-ASCII character at index " + i);
  }
}

// Write UTF-8 without BOM.
fs.writeFileSync(sqlPath, Buffer.from(sql.replace(/\n/g, "\r\n"), "utf8"));

console.log("OK repaired 200 SQL for local PostgreSQL without pgvector.");
console.log("Backup: " + backupPath);
console.log("Lines: " + sql.split("\n").length);
console.log("");
console.log(sql.split("\n").slice(0, 35).map((line, index) => String(index + 1).padStart(3, " ") + ": " + line).join("\n"));