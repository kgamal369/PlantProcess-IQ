const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");

const failures = [];

const phase1 = fs.readFileSync(
  path.join(frontendRoot, "src", "api", "phase1", "phase1Workflow.api.ts"),
  "utf8"
);

if (!phase1.includes("getAccessToken")) {
  failures.push("phase1Workflow.api.ts does not import/use getAccessToken.");
}

if (!phase1.includes("Authorization")) {
  failures.push("phase1Workflow.api.ts does not send Authorization header.");
}

if (/credentials\s*:\s*["']include["']/.test(phase1)) {
  failures.push("phase1Workflow.api.ts still uses credentials: include.");
}

const phase03 = fs.readFileSync(
  path.join(frontendRoot, "e2e", "api", "phase03-two-stage-import.spec.ts"),
  "utf8"
);

if (!phase03.includes('import { apiBaseUrl, login } from "../helpers/auth";')) {
  failures.push("phase03 spec does not use shared auth helper.");
}

if (/const username|const password|data:\s*\{\s*username\s*,\s*password\s*\}/.test(phase03)) {
  failures.push("phase03 spec still contains old local username/password login.");
}

if (failures.length > 0) {
  console.error("E2E wave 2 auth cleanup validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("E2E wave 2 auth cleanup validation passed.");
