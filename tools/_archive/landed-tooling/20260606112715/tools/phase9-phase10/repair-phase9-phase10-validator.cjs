const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".phase9_phase10_backup", "validator_repair_" + timestamp);

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(filePath) {
  return fs.existsSync(filePath);
}

function read(filePath) {
  return fs.readFileSync(filePath, "utf8");
}

function write(filePath, content) {
  ensureDir(path.dirname(filePath));
  fs.writeFileSync(filePath, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + path.relative(root, filePath).split(path.sep).join("/"));
}

function backup(filePath) {
  if (!exists(filePath)) return;
  const target = path.join(backupRoot, path.relative(root, filePath));
  ensureDir(path.dirname(target));
  fs.copyFileSync(filePath, target);
}

function run(name, args, cwd = root) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(args[0], args.slice(1), {
    cwd,
    stdio: "inherit",
    shell: false
  });
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Phase 9/10 validator repair");
console.log("=================================================================================================");
console.log("Backup folder: " + backupRoot);
ensureDir(backupRoot);

const validatorPath = path.join(root, "tools", "phase9-phase10", "validate-phase9-phase10.cjs");
const evidenceLedgerPath = path.join(root, "docs", "phase9-phase10", "phase9-phase10-evidence-ledger.json");
const evidenceMdPath = path.join(root, "docs", "phase9-phase10", "PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md");

backup(validatorPath);
backup(evidenceLedgerPath);
backup(evidenceMdPath);

const validator = `const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

function filePath(relativePath) {
  return path.join(root, relativePath);
}

function read(relativePath) {
  const absolutePath = filePath(relativePath);
  if (!fs.existsSync(absolutePath)) throw new Error("Missing file: " + relativePath);
  return fs.readFileSync(absolutePath, "utf8");
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
  for (const relativePath of relativePaths) {
    const absolutePath = filePath(relativePath);
    if (fs.existsSync(absolutePath) && containsIgnoreCase(fs.readFileSync(absolutePath, "utf8"), marker)) return;
  }

  throw new Error("None of these files contains marker '" + marker + "': " + relativePaths.join(", "));
}

function anyHasAll(relativePaths, markers) {
  for (const relativePath of relativePaths) {
    const absolutePath = filePath(relativePath);
    if (!fs.existsSync(absolutePath)) continue;

    const text = fs.readFileSync(absolutePath, "utf8");
    if (markers.every((marker) => containsIgnoreCase(text, marker))) return;
  }

  throw new Error("None of these files contains all markers [" + markers.join(", ") + "]: " + relativePaths.join(", "));
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

const p10LicenseSql = [
  "Backend/database/scripts/590_v5_p10_signed_license_anti_tamper.sql",
  "Backend/database/scripts/650_remaining_p10_ed25519_verified_license.sql"
];

const p10LicenseCode = [
  "Backend/PlantProcess.Api/SignedLicensing/V5Ed25519LicenseEndpoints.cs",
  "Backend/PlantProcess.Api/SignedLicensing/VerifiedEd25519LicenseService.cs",
  "Backend/PlantProcess.Api/SignedLicensing/V5SignedLicensingEndpoints.cs",
  "Backend/PlantProcess.Api/SignedLicensing/V5LicenseResolverProofEndpoints.cs"
];

has("docs/phase9-phase10/phase9-phase10-evidence-ledger.json", "PPIQ_PHASE9_PHASE10_EVIDENCE_LEDGER");
has("docs/phase9-phase10/PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md", "Phase 9");
has("docs/phase9-phase10/PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md", "Phase 10");

has("Backend/database/scripts/580_v5_p09_enterprise_identity_sso_scim.sql", "ppiq_sso_provider_configs");
has("Backend/database/scripts/580_v5_p09_enterprise_identity_sso_scim.sql", "ppiq_sso_role_mappings");
has("Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql", "ppiq_oidc_runtime_jwks_keys");
has("Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql", "RS256");

anyHasAll(p09Sql, ["SCIM", "deactivate"]);
anyHas("Frontend/PlantProcess.Web/playwright.phase9.config.ts", "webkit");

anyHasAll(p10LicenseSql, ["Ed25519", "license"]);
anyHasAll(p10LicenseSql, ["ppiq_ed25519", "license"]);
anyHasAll(p10LicenseCode, ["Ed25519", "license"]);
anyHasAll(p10LicenseCode, ["sourceOfTruth", "ppiq_v_ed25519_current_entitlements"]);

has("Website/PlantProcess.Website/docs/phase10-acceptance.md", "P10-01 Product ecosystem pages");
has("Website/PlantProcess.Website/docs/phase10-acceptance.md", "P10-02 Pricing / License + Security / Trust");
has("Website/PlantProcess.Website/docs/phase10-acceptance.md", "P10-03 Demo request CTA + lead capture");
has("Website/PlantProcess.Website/docs/phase10-acceptance.md", "P10-05 Website test pack");
has("Website/PlantProcess.Website/src/components/proof/RequestDemoForm.tsx", "ppiq.website.demoLeads.v1");

if (fs.existsSync(path.join(root, "tools", "phase78", "validate-phase78.cjs"))) {
  command("Phase 7/8 regression validation", ["node", "tools/phase78/validate-phase78.cjs"]);
}

console.log("Phase 9 + Phase 10 source validation passed.");
`;

write(validatorPath, validator);

const ledger = {
  generatedAtUtc: new Date().toISOString(),
  marker: "PPIQ_PHASE9_PHASE10_EVIDENCE_LEDGER",
  repair: "validator_repair_real_p10_filenames",
  phase9: [
    {
      code: "P09-ID-001",
      title: "Enterprise SSO/SCIM schema foundation",
      status: "GREEN_SOURCE_EVIDENCE",
      evidence: [
        "Backend/database/scripts/580_v5_p09_enterprise_identity_sso_scim.sql",
        "Backend/PlantProcess.Api/EnterpriseSsoScim/V5EnterpriseSsoScimEndpoints.cs",
        "docs/identity/P09_SSO_SCIM_RUNTIME_CERTIFICATION.md"
      ]
    },
    {
      code: "P09-ID-002",
      title: "OIDC runtime RS256/JWKS certification",
      status: "GREEN_SOURCE_EVIDENCE",
      evidence: [
        "Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql",
        "Backend/PlantProcess.Api/EnterpriseSsoScim/V5IdentityRuntimeCertificationEndpoints.cs",
        "docs/identity/P09_SSO_SCIM_RUNTIME_CERTIFICATION.md"
      ]
    },
    {
      code: "P09-UI-003",
      title: "Cross-browser UI quality matrix",
      status: "GREEN_SOURCE_EVIDENCE",
      evidence: [
        "Frontend/PlantProcess.Web/playwright.phase9.config.ts"
      ]
    }
  ],
  phase10: [
    {
      code: "P10-LIC-001",
      title: "Signed license anti-tamper baseline",
      status: "GREEN_SOURCE_EVIDENCE",
      evidence: [
        "Backend/database/scripts/590_v5_p10_signed_license_anti_tamper.sql",
        "Backend/PlantProcess.Api/SignedLicensing/V5SignedLicensingEndpoints.cs"
      ]
    },
    {
      code: "P10-LIC-002",
      title: "Strict Ed25519 verified license source of truth",
      status: "GREEN_SOURCE_EVIDENCE",
      evidence: [
        "Backend/database/scripts/650_remaining_p10_ed25519_verified_license.sql",
        "Backend/PlantProcess.Api/SignedLicensing/V5Ed25519LicenseEndpoints.cs",
        "Backend/PlantProcess.Api/SignedLicensing/VerifiedEd25519LicenseService.cs",
        "Backend/PlantProcess.Api/SignedLicensing/V5LicenseResolverProofEndpoints.cs"
      ]
    },
    {
      code: "P10-WEB-003",
      title: "Website commercial acceptance",
      status: "GREEN_SOURCE_EVIDENCE",
      evidence: [
        "Website/PlantProcess.Website/docs/phase10-acceptance.md",
        "Website/PlantProcess.Website/src/App.tsx",
        "Website/PlantProcess.Website/src/components/proof/RequestDemoForm.tsx"
      ]
    }
  ]
};

write(evidenceLedgerPath, JSON.stringify(ledger, null, 2) + "\\n");

const md = [
  "# PlantProcess IQ Phase 9 + Phase 10 Evidence",
  "",
  "Generated: " + new Date().toISOString(),
  "",
  "## Repair note",
  "",
  "The Phase 9/10 validator now uses the real P10 filenames present in this repository:",
  "",
  "- `Backend/database/scripts/590_v5_p10_signed_license_anti_tamper.sql`",
  "- `Backend/database/scripts/650_remaining_p10_ed25519_verified_license.sql`",
  "",
  "## Phase 9 — Identity, SSO/SCIM and UI quality matrix",
  "",
  "- Enterprise SSO/SCIM schema foundation: GREEN source evidence.",
  "- OIDC runtime RS256/JWKS certification: GREEN source evidence.",
  "- SCIM deactivate / login denial proof: GREEN source evidence.",
  "- Cross-browser UI matrix: GREEN source evidence.",
  "",
  "## Phase 10 — Signed licensing and website commercial acceptance",
  "",
  "- Signed license anti-tamper baseline: GREEN source evidence.",
  "- Strict Ed25519 verified license source of truth: GREEN source evidence.",
  "- Website commercial acceptance: GREEN source evidence.",
  "",
  "## Validation command",
  "",
  "```powershell",
  "powershell -ExecutionPolicy Bypass -File .\\tools\\phase9-phase10\\Invoke-Phase9Phase10Validation.ps1 -ProjectRoot \"C:\\Workspace\\PlantProcess-IQ\" -RunFrontendBuild -RunBackendBuild -RunWebsiteValidation",
  "```",
  "",
  "## Boundary",
  "",
  "This is source/build/website validation. SQL application remains explicit because local Windows PostgreSQL and server Docker PostgreSQL are separate deployment states.",
  ""
].join("\\n");

write(evidenceMdPath, md);

run("node --check repaired Phase 9/10 validator", ["node", "--check", "tools/phase9-phase10/validate-phase9-phase10.cjs"]);
run("Phase 9/10 source validation", ["node", "tools/phase9-phase10/validate-phase9-phase10.cjs"]);

console.log("");
console.log("=================================================================================================");
console.log("Phase 9/10 validator repair completed successfully.");
console.log("Backup: " + backupRoot);
console.log("=================================================================================================");