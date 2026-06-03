import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");
const srcRoot = path.join(frontendRoot, "src");
const apiRoot = path.join(srcRoot, "api");
const reportDir = path.join(root, "Documentation", "v5");

fs.mkdirSync(reportDir, { recursive: true });

function exists(file) {
  return fs.existsSync(file);
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, text) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, text, "utf8");
}

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["node_modules", "dist", "build", "coverage", ".vite"].includes(entry.name)) continue;
      walk(full, output);
    } else if (/\.(ts|tsx|js|jsx)$/.test(entry.name)) {
      output.push(full);
    }
  }

  return output;
}

function toPosix(value) {
  return value.replaceAll(path.sep, "/");
}

function stripExtension(value) {
  return value.replace(/\.(ts|tsx|js|jsx)$/, "");
}

function relativeImport(fromFile, toFile) {
  let relative = path.relative(path.dirname(fromFile), toFile);
  relative = stripExtension(toPosix(relative));
  if (!relative.startsWith(".")) relative = `./${relative}`;
  return relative;
}

const oldCandidates = [
  path.join(apiRoot, "plantProcessApi.ts"),
  path.join(apiRoot, "legacy", "plantProcessApi.ts")
];

const sourceCandidate = oldCandidates.find(exists);

if (!sourceCandidate) {
  console.log("[pack-c1] No legacy plantProcessApi.ts found. Assuming already retired.");
} else {
  const target = path.join(apiRoot, "productApiClient.ts");

  if (!exists(target)) {
    write(target, read(sourceCandidate));
    console.log(`[pack-c1] Created ${toPosix(path.relative(root, target))} from ${toPosix(path.relative(root, sourceCandidate))}`);
  } else {
    console.log(`[pack-c1] productApiClient.ts already exists; not overwriting.`);
  }
}

const allFiles = walk(srcRoot);
const replacements = [];

for (const file of allFiles) {
  let text = read(file);
  const original = text;

  // Replace import/export module specifiers that reference plantProcessApi.
  text = text.replace(
    /((?:import|export)\s+(?:type\s+)?(?:[^'"]*?\s+from\s+)?['"])([^'"]*plantProcessApi)(['"])/g,
    (match, prefix, specifier, suffix) => {
      const target = path.join(apiRoot, "productApiClient.ts");
      const nextSpecifier = relativeImport(file, target);
      replacements.push({
        file: toPosix(path.relative(root, file)),
        from: specifier,
        to: nextSpecifier
      });
      return `${prefix}${nextSpecifier}${suffix}`;
    }
  );

  // Replace dynamic import("...plantProcessApi")
  text = text.replace(
    /(import\(\s*['"])([^'"]*plantProcessApi)(['"]\s*\))/g,
    (match, prefix, specifier, suffix) => {
      const target = path.join(apiRoot, "productApiClient.ts");
      const nextSpecifier = relativeImport(file, target);
      replacements.push({
        file: toPosix(path.relative(root, file)),
        from: specifier,
        to: nextSpecifier
      });
      return `${prefix}${nextSpecifier}${suffix}`;
    }
  );

  if (text !== original) {
    write(file, text);
  }
}

// Delete old plantProcessApi files from src/api after imports are migrated.
const deleted = [];

for (const candidate of oldCandidates) {
  if (exists(candidate)) {
    fs.unlinkSync(candidate);
    deleted.push(toPosix(path.relative(root, candidate)));
  }
}

// Remove empty src/api/legacy folder if it is empty.
const legacyDir = path.join(apiRoot, "legacy");
if (exists(legacyDir) && fs.readdirSync(legacyDir).length === 0) {
  fs.rmdirSync(legacyDir);
  deleted.push(toPosix(path.relative(root, legacyDir)));
}

// Report remaining references.
const remaining = [];
for (const file of walk(srcRoot)) {
  const text = read(file);
  if (text.includes("plantProcessApi")) {
    remaining.push({
      file: toPosix(path.relative(root, file)),
      matches: (text.match(/plantProcessApi/g) ?? []).length
    });
  }
}

const report = {
  generatedAtUtc: new Date().toISOString(),
  createdProductClient: exists(path.join(apiRoot, "productApiClient.ts")),
  replacements,
  deleted,
  remainingPlantProcessApiReferences: remaining
};

write(
  path.join(reportDir, "pack-c1-legacy-api-retirement-report.json"),
  JSON.stringify(report, null, 2)
);

console.log(JSON.stringify(report, null, 2));

if (!exists(path.join(apiRoot, "productApiClient.ts"))) {
  console.error("productApiClient.ts was not created.");
  process.exit(2);
}

if (remaining.length > 0) {
  console.error("Remaining plantProcessApi references still exist.");
  process.exit(3);
}

console.log("[pack-c1] Legacy API client import retirement completed.");