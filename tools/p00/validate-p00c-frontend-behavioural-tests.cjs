const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");

const requiredFiles = [
  "src/api/__tests__/apiClient.retry-backoff.test.ts",
  "src/state/__tests__/AuthContext.bootstrap.test.tsx",
  "src/state/__tests__/LicenseContext.gating.test.tsx",
  "src/components/standard/__tests__/DataFetchBoundary.test.tsx"
];

const failures = [];

for (const relativePath of requiredFiles) {
  const file = path.join(frontendRoot, relativePath);

  if (!fs.existsSync(file)) {
    failures.push("Missing Pack C test file: " + relativePath);
    continue;
  }

  const text = fs.readFileSync(file, "utf8");

  if (!text.includes("describe(") || !text.includes("it(")) {
    failures.push("Pack C test file has no Vitest tests: " + relativePath);
  }
}

if (failures.length > 0) {
  console.error("P00C frontend behavioural validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("P00C frontend behavioural validation passed.");
console.log("Pack C frontend behavioural test files are present.");
