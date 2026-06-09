const fs = require("fs");
const path = require("path");

const root = process.cwd();
const files = ["Jenkinsfile", "deploy/ci/Jenkinsfile"].filter(f => fs.existsSync(path.join(root, f)));

const failures = [];

for (const f of files) {
  const text = fs.readFileSync(path.join(root, f), "utf8");

  for (const forbidden of [
    "npm run test:visual -- --list",
    "npm run test:phase56:e2e -- --list",
    "npm run test:a11y -- --list"
  ]) {
    if (text.includes(forbidden)) {
      failures.push(`${f} still contains discovery-only command: ${forbidden}`);
    }
  }

  for (const required of [
    "npm run test:visual",
    "npm run test:phase56:e2e",
    "npm run test:a11y"
  ]) {
    if (!text.includes(required)) {
      failures.push(`${f} missing executable UI gate: ${required}`);
    }
  }
}

if (failures.length) {
  console.error("PPIQ-T016 failed: UI gates are not executing for real.");
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("PPIQ-T016 passed: e2e/visual/a11y CI gates execute real tests, not --list discovery.");