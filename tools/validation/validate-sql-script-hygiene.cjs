// P00A-TEST-REGISTER: TRANSFER-TO-REAL-TEST
// Date: 2026-05-31T11:07:14.744Z
// Replacement: Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Database/SqlScriptHygieneApplyTests.cs
// Reason: This file is tracked by the P00A Test Register and should not be treated as a final behavioural test.

const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const scriptsDir = path.join(root, "Backend", "database", "scripts");

const required = [
  "200_phase02_ml_foundation_feature_store_pgvector.sql",
  "201_phase02_ml_feature_store_v6_completion.sql",
  "202_phase02_ml_compute_basic_correlations_hotfix.sql",
  "203_phase02_ml_compute_v6_wrapper_hotfix.sql",
];

const failures = [];

for (const name of required) {
  const file = path.join(scriptsDir, name);

  if (!fs.existsSync(file)) {
    failures.push(`Missing required SQL script: ${name}`);
    continue;
  }

  const buffer = fs.readFileSync(file);
  const text = buffer.toString("utf8");

  if (buffer.length >= 3 && buffer[0] === 0xef && buffer[1] === 0xbb && buffer[2] === 0xbf) {
    failures.push(`${name}: UTF-8 BOM detected`);
  }

  if (/DO \$\s/i.test(text) || /END \$;/i.test(text)) {
    failures.push(`${name}: invalid DO $ / END $ block detected; use DO $$ / END $$`);
  }

  if (/CREATE EXTENSION IF NOT EXISTS vector/i.test(text) && !/EXCEPTION\s+WHEN\s+OTHERS/i.test(text)) {
    failures.push(`${name}: pgvector extension is not fallback-safe`);
  }
}

const bootstrap = path.join(root, "tools", "dev-bootstrap.ps1");
if (fs.existsSync(bootstrap)) {
  const text = fs.readFileSync(bootstrap, "utf8");
  if (!text.includes("ON_ERROR_STOP=1")) {
    failures.push("tools/dev-bootstrap.ps1 must use ON_ERROR_STOP=1 for SQL application");
  }
  if (!text.includes("$LASTEXITCODE")) {
    failures.push("tools/dev-bootstrap.ps1 must rely on process exit code, not NOTICE output text");
  }
}

if (failures.length) {
  console.error("PPIQ-T283/T288 validation failed:");
  for (const failure of failures) console.error(" - " + failure);
  process.exit(1);
}

console.log("PPIQ-T283/T288 passed: SQL script hygiene and dev-bootstrap safety checks passed.");
