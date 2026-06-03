import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const srcRoot = path.join(root, "Frontend", "PlantProcess.Web", "src");
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

function toPosix(value) {
  return value.replaceAll(path.sep, "/");
}

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["node_modules", "dist", "build", "coverage", ".vite", "bin", "obj"].includes(entry.name)) continue;
      walk(full, output);
    } else if (/\.(ts|tsx|js|jsx)$/.test(entry.name)) {
      output.push(full);
    }
  }

  return output;
}

const changed = [];

// ------------------------------------------------------------
// A) Stabilize productApiClient facade
// ------------------------------------------------------------
const productApiClient = path.join(apiRoot, "productApiClient.ts");

if (!exists(productApiClient)) {
  throw new Error("Missing productApiClient.ts");
}

let facade = read(productApiClient);

facade = facade.replace(
  'import { postJson } from "./productApiHardening";',
  'import { getJson, postJson } from "./productApiHardening";'
);

facade = facade.replace(
  /getLicenseStatus:\s*\(\)\s*=>\s*coreProductApi\.getLicenseStatus\(\),/g,
  'getLicenseStatus: () => getJson<unknown>("/license/status"),'
);

facade = facade.replace(
  /getLicensePlans:\s*\(\)\s*=>\s*coreProductApi\.getLicensePlans\(\),/g,
  'getLicensePlans: () => getJson<unknown>("/license/plans"),'
);

facade = facade.replace(
  /getMlReadiness:\s*\(\)\s*=>\s*coreProductApi\.getMlReadiness\(\),/g,
  'getMlReadiness: () => getJson<unknown>("/ml/readiness"),'
);

facade = facade.replace(
  /getDemoLifecycle:\s*\(\)\s*=>\s*coreProductApi\.getDemoLifecycle\(\),/g,
  'getDemoLifecycle: () => getJson<unknown>("/demo/lifecycle"),'
);

if (!facade.includes('import { getJson, postJson } from "./productApiHardening";')) {
  throw new Error("productApiClient import was not stabilized.");
}

if (
  facade.includes("coreProductApi.getLicenseStatus") ||
  facade.includes("coreProductApi.getLicensePlans") ||
  facade.includes("coreProductApi.getMlReadiness") ||
  facade.includes("coreProductApi.getDemoLifecycle")
) {
  throw new Error("productApiClient still references non-existing core compatibility methods.");
}

write(productApiClient, facade);
changed.push(toPosix(path.relative(root, productApiClient)));

// ------------------------------------------------------------
// B) Replace legacy hardening imports everywhere in frontend src
// ------------------------------------------------------------
for (const file of walk(srcRoot)) {
  let text = read(file);
  const original = text;

  text = text.replaceAll("@/api/legacy/legacyApiHardening", "@/api/productApiHardening");
  text = text.replaceAll("../legacy/legacyApiHardening", "../productApiHardening");
  text = text.replaceAll("./legacy/legacyApiHardening", "./productApiHardening");
  text = text.replaceAll("../../api/legacy/legacyApiHardening", "../../api/productApiHardening");
  text = text.replaceAll("../../../api/legacy/legacyApiHardening", "../../../api/productApiHardening");

  if (text !== original) {
    write(file, text);
    changed.push(toPosix(path.relative(root, file)));
  }
}

// ------------------------------------------------------------
// C) Patch DashboardFilterContext type/index fixes
// ------------------------------------------------------------
const dashboardFilterContext = path.join(srcRoot, "state", "DashboardFilterContext.tsx");

if (exists(dashboardFilterContext)) {
  let text = read(dashboardFilterContext);
  const original = text;

  // Syntax fix from Hotfix03, safe if rerun.
  text = text.replace(
    /Object\.keys\(([^)]*)\)\s+as\s+string\[\]\.forEach/g,
    "(Object.keys($1) as string[]).forEach"
  );

  // Compile fix for DashboardFilters without a string index signature.
  text = text.replace(
    /const value = filters\[String\(key\)\];/g,
    'const value = (filters as Record<string, unknown>)[String(key)];'
  );

  // Defensive fixes for common index access patterns.
  text = text.replace(
    /\(filters as Record<string, unknown>\)\[String\(String\(key\)\)\]/g,
    "(filters as Record<string, unknown>)[String(key)]"
  );

  if (text !== original) {
    write(dashboardFilterContext, text);
    changed.push(toPosix(path.relative(root, dashboardFilterContext)));
  }
}

// ------------------------------------------------------------
// D) Delete any remaining legacy API folder files if empty/obsolete
// ------------------------------------------------------------
const legacyDir = path.join(apiRoot, "legacy");
const deleted = [];

if (exists(legacyDir)) {
  for (const name of ["plantProcessApi.ts", "legacyApiHardening.ts"]) {
    const file = path.join(legacyDir, name);
    if (exists(file)) {
      fs.unlinkSync(file);
      deleted.push(toPosix(path.relative(root, file)));
    }
  }

  if (fs.existsSync(legacyDir) && fs.readdirSync(legacyDir).length === 0) {
    fs.rmdirSync(legacyDir);
    deleted.push(toPosix(path.relative(root, legacyDir)));
  }
}

const offenders = [];

for (const file of walk(srcRoot)) {
  const text = read(file);

  if (text.includes("plantProcessApi")) {
    offenders.push({
      type: "plantProcessApi",
      file: toPosix(path.relative(root, file))
    });
  }

  if (text.includes("@/api/legacy/legacyApiHardening")) {
    offenders.push({
      type: "legacyApiHardening",
      file: toPosix(path.relative(root, file))
    });
  }
}

const report = {
  generatedAtUtc: new Date().toISOString(),
  changed: [...new Set(changed)].sort(),
  deleted,
  offenders
};

write(
  path.join(reportDir, "pack-c1-hotfix04-stabilization-report.json"),
  JSON.stringify(report, null, 2)
);

console.log(JSON.stringify(report, null, 2));

if (offenders.length > 0) {
  console.error("C1 Hotfix04 offenders remain.");
  process.exit(3);
}

console.log("[pack-c1-hotfix04] stabilization completed.");