const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

function toArray(value) {
  return Array.isArray(value) ? value : [value];
}

function absolute(relativePath) {
  return path.join(root, relativePath);
}

function existsFile(relativePath) {
  const file = absolute(relativePath);
  return fs.existsSync(file) && fs.statSync(file).isFile();
}

function read(relativePath) {
  const file = absolute(relativePath);

  if (!fs.existsSync(file)) {
    throw new Error("Missing file: " + relativePath);
  }

  if (!fs.statSync(file).isFile()) {
    throw new Error("Expected file but found directory: " + relativePath);
  }

  return fs.readFileSync(file, "utf8");
}

function containsIgnoreCase(text, marker) {
  return text.toLowerCase().includes(marker.toLowerCase());
}

function has(relativePath, marker) {
  const text = read(relativePath);

  if (!containsIgnoreCase(text, marker)) {
    throw new Error(relativePath + " missing required marker: " + marker);
  }
}

function anyHas(relativePaths, marker) {
  const candidates = toArray(relativePaths);

  for (const relativePath of candidates) {
    const file = path.join(root, relativePath);
    if (!fs.existsSync(file) || !fs.statSync(file).isFile()) continue;
    if (containsIgnoreCase(fs.readFileSync(file, "utf8"), marker)) return;
  }

  throw new Error("None of these files contains marker '" + marker + "': " + candidates.join(", "));
}

function anyHasAll(relativePaths, markers) {
  const candidates = toArray(relativePaths);

  for (const relativePath of candidates) {
    if (!existsFile(relativePath)) continue;

    const text = fs.readFileSync(absolute(relativePath), "utf8");

    if (markers.every((marker) => containsIgnoreCase(text, marker))) return;
  }

  throw new Error("None of these files contains all markers [" + markers.join(", ") + "]: " + candidates.join(", "));
}

function walk(dir) {
  if (!fs.existsSync(dir)) return [];

  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    return entry.isDirectory() ? walk(full) : [full];
  });
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function discoverFiles(dirRelativePath, predicate) {
  const dir = absolute(dirRelativePath);

  return walk(dir)
    .filter((file) => fs.statSync(file).isFile())
    .map(rel)
    .filter(predicate);
}

function unique(values) {
  return Array.from(new Set(values));
}

function command(name, args) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(args[0], args.slice(1), { cwd: root, stdio: "inherit", shell: false });
}

const p09Sql = [
  "Backend/database/scripts/580_v5_p09_enterprise_identity_sso_scim.sql",
  "Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql"
];

const p10LicenseSql = unique([
  "Backend/database/scripts/590_v5_p10_signed_license_anti_tamper.sql",
  "Backend/database/scripts/650_remaining_p10_ed25519_verified_license.sql",
  ...discoverFiles("Backend/database/scripts", (file) =>
    file.endsWith(".sql") &&
    /p10|license|ed25519/i.test(file)
  )
]);

const p10LicenseCode = unique([
  "Backend/PlantProcess.Api/SignedLicensing/V5Ed25519LicenseEndpoints.cs",
  "Backend/PlantProcess.Api/SignedLicensing/VerifiedEd25519LicenseService.cs",
  "Backend/PlantProcess.Api/SignedLicensing/V5SignedLicensingEndpoints.cs",
  "Backend/PlantProcess.Api/SignedLicensing/V5LicenseResolverProofEndpoints.cs",
  ...discoverFiles("Backend/PlantProcess.Api/SignedLicensing", (file) =>
    file.endsWith(".cs") &&
    /license|ed25519/i.test(file)
  )
]);

console.log("P10 license SQL candidates:");
for (const file of p10LicenseSql) console.log(" - " + file);

console.log("P10 license code candidates:");
for (const file of p10LicenseCode) console.log(" - " + file);

has("docs/phase9-phase10/phase9-phase10-evidence-ledger.json", "PPIQ_PHASE9_PHASE10_EVIDENCE_LEDGER");
has("docs/phase9-phase10/PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md", "Phase 9");
has("docs/phase9-phase10/PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md", "Phase 10");

has("Backend/database/scripts/580_v5_p09_enterprise_identity_sso_scim.sql", "ppiq_sso_provider_configs");
has("Backend/database/scripts/580_v5_p09_enterprise_identity_sso_scim.sql", "ppiq_sso_role_mappings");
has("Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql", "ppiq_oidc_runtime_jwks_keys");
has("Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql", "RS256");

anyHasAll(p09Sql, ["SCIM", "deactivate"]);
has("Frontend/PlantProcess.Web/playwright.phase9.config.ts", "webkit");

anyHasAll(p10LicenseSql, ["Ed25519", "license"]);
anyHasAll([...p10LicenseSql, ...p10LicenseCode], ["Ed25519", "license"]);
anyHas([...p10LicenseSql, ...p10LicenseCode], "ppiq_v_ed25519_current_entitlements");

has("Website/PlantProcess.Website/docs/phase10-acceptance.md", "P10-01 Product ecosystem pages");
has("Website/PlantProcess.Website/docs/phase10-acceptance.md", "P10-02 Pricing / License + Security / Trust");
has("Website/PlantProcess.Website/docs/phase10-acceptance.md", "P10-03 Demo request CTA + lead capture");
has("Website/PlantProcess.Website/docs/phase10-acceptance.md", "P10-05 Website test pack");
has("Website/PlantProcess.Website/src/components/proof/RequestDemoForm.tsx", "ppiq.website.demoLeads.v1");

if (fs.existsSync(path.join(root, "tools", "phase78", "validate-phase78.cjs"))) {
  command("Phase 7/8 regression validation", ["node", "tools/phase78/validate-phase78.cjs"]);
}

console.log("Phase 9 + Phase 10 source validation passed.");
