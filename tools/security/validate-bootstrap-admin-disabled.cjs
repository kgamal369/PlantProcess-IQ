const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function walk(dir, predicate, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      const name = entry.name.toLowerCase();

      if ([
        "bin",
        "obj",
        "node_modules",
        ".git",
        "dist",
        "coverage",
        ".realization_backup",
        "documentation"
      ].includes(name)) {
        continue;
      }

      if (name.includes("backup") || name.includes("archived") || name.includes("legacy")) {
        continue;
      }

      walk(full, predicate, output);
      continue;
    }

    if (predicate(full)) output.push(full);
  }

  return output;
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function isIgnoredPath(file) {
  const normalized = rel(file).toLowerCase().replace(/\\/g, "/");

  return normalized.startsWith("documentation/")
    || normalized.includes("/backup")
    || normalized.includes("backup_")
    || normalized.includes("/_legacy")
    || normalized.includes("/legacy")
    || normalized.includes("/archived")
    || normalized.includes(".realization_backup")
    || normalized.includes("/docs/")
    || normalized.startsWith("docs/");
}

const runtimeConfigFiles = walk(root, (file) => {
  if (isIgnoredPath(file)) return false;

  const name = path.basename(file).toLowerCase();

  return /^appsettings.*\.json$/i.test(name)
    || name === ".env"
    || name === ".env.production"
    || name.endsWith(".production");
});

for (const file of runtimeConfigFiles) {
  const text = read(file);

  const activeBootstrapCredential =
    /bootstrap.{0,80}(password|secret|admin)/i.test(text)
    && !/example|template|placeholder|change-me|changeme|disabled|null|false|masked|redacted/i.test(text);

  if (activeBootstrapCredential) {
    failures.push({
      file: rel(file),
      reason: "possible active bootstrap admin credential in active runtime config"
    });
  }
}

const proof = path.join(root, "docs", "security", "BOOTSTRAP_ADMIN_DISABLE_PROOF.md");

if (!fs.existsSync(proof)) {
  failures.push({
    file: rel(proof),
    reason: "missing bootstrap-admin disable proof document"
  });
} else if (!read(proof).includes("PPIQ_REALIZATION_T011_BOOTSTRAP_ADMIN_DISABLED")) {
  failures.push({
    file: rel(proof),
    reason: "missing proof marker"
  });
}

if (failures.length) {
  console.error("PPIQ-T011 failed: bootstrap-admin disable proof is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T011 passed: no active bootstrap credentials found in runtime config and bootstrap disable proof is present.");
