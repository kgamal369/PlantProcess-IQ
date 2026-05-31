const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");

function walk(dir, extensions) {
  const result = [];

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["node_modules", "dist", "playwright-report", "test-results"].includes(entry.name)) {
        continue;
      }

      result.push(...walk(full, extensions));
      continue;
    }

    if (entry.isFile() && extensions.some((ext) => entry.name.endsWith(ext))) {
      result.push(full);
    }
  }

  return result;
}

const failures = [];

for (const file of walk(path.join(frontendRoot, "src"), [".ts", ".tsx"])) {
  const text = fs.readFileSync(file, "utf8");
  if (/credentials\s*:\s*["']include["']/.test(text)) {
    failures.push("credentials include remains in " + path.relative(root, file));
  }
}

const phase03 = fs.readFileSync(
  path.join(frontendRoot, "e2e", "api", "phase03-two-stage-import.spec.ts"),
  "utf8"
);

if (/ChangeMe123!/.test(phase03) || /UserName\s*:\s*["']admin["']/.test(phase03) || /userName\s*:\s*["']admin["']/.test(phase03)) {
  failures.push("phase03 two-stage import spec still contains old bootstrap credentials.");
}

const adminDb = fs.readFileSync(
  path.join(frontendRoot, "e2e", "admin-db-focused.spec.ts"),
  "utf8"
);

if (!/db config\|connection\|source\|configuration\|connector truth/.test(adminDb)) {
  failures.push("admin-db-focused spec was not broadened for current admin shell wording.");
}

if (failures.length > 0) {
  console.error("E2E wave 1 cleanup validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("E2E wave 1 cleanup validation passed.");
