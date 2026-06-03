import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) throw new Error(`Missing file: ${relativePath}`);
  return fs.readFileSync(full, "utf8");
}

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

function ok(label, condition, evidence = "") {
  if (!condition) throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["node_modules", "dist", "build", "coverage", ".git"].includes(entry.name)) continue;
      walk(full, output);
    } else {
      output.push(full);
    }
  }

  return output;
}

const sql = read("Backend/database/scripts/665_pack_b35_mostly_green_task_closure.sql");
const doc = read("docs/closure/PACK_B35_MOSTLY_GREEN_TASK_CLOSURE.md");

const expectedTasks = [
  "P01-T02","P01-T03","P01-T06",
  "P02-T04",
  "P03-T02",
  "P04-T06",
  "P05-T02","P05-T05",
  "P06-T04","P06-T05",
  "P09-T03",
  "P10-T03",
  "P11-T03",
  "P12-T03","P12-T04",
  "P13-T03","P13-T04"
];

for (const task of expectedTasks) {
  ok(`Closure SQL includes ${task}`, sql.includes(task));
  ok(`Closure doc includes ${task}`, doc.includes(task));
}

for (const file of [
  "Backend/database/scripts/500_v5_p01_auth_argon2id_and_secret_hygiene.sql",
  "Backend/database/scripts/510_v5_p02_rls_tenant_isolation_and_secret_vault.sql",
  "Backend/database/scripts/520_v5_p03_timeseries_foundation.sql",
  "Backend/database/scripts/530_v5_p04_assistant_model_gateway_boundary.sql",
  "Backend/database/scripts/540_v5_p05_visual_mapper_foundation.sql",
  "Backend/database/scripts/550_v5_p06_blended_provenance.sql",
  "Backend/database/scripts/580_v5_p09_enterprise_identity_sso_scim.sql",
  "Backend/database/scripts/590_v5_p10_signed_license_anti_tamper.sql",
  "Backend/database/scripts/600_v5_p11_outbound_notifications_leads.sql",
  "Backend/database/scripts/610_v5_p12_i18n_rtl_mobile.sql",
  "Backend/database/scripts/620_v5_p13_deployment_dr_portability.sql",
  "Backend/PlantProcess.Api/SignedLicensing/VerifiedEd25519LicenseService.cs",
  "Backend/PlantProcess.Api/EnterpriseSsoScim/V5IdentityRuntimeCertificationEndpoints.cs",
  "tools/v5/validate-pack-b3-sso-scim.ps1",
  "tools/v5/validate-pack-b2-license-resolver.ps1",
  "tools/v5/validate-p13-p14.ps1"
]) {
  ok(`Evidence file exists ${file}`, exists(file));
}

// P14-T03 readiness report: report only, do not fail Pack B3.5.
// Pack C will turn this into a hard gate.
const srcRoot = path.join(root, "Frontend", "PlantProcess.Web", "src");
const files = walk(srcRoot).filter((x) => /\.(ts|tsx)$/.test(x));
const legacyMatches = [];

for (const full of files) {
  const text = fs.readFileSync(full, "utf8");
  if (text.includes("legacy/plantProcessApi") || text.includes("/plantProcessApi") || text.includes("plantProcessApi")) {
    legacyMatches.push(path.relative(root, full).replaceAll(path.sep, "/"));
  }
}

const phaseNamedMatches = files
  .map((x) => path.relative(root, x).replaceAll(path.sep, "/"))
  .filter((x) => /Phase\d|P03P04|Pack\d/i.test(x));

const reportDir = path.join(root, "Documentation", "v5");
fs.mkdirSync(reportDir, { recursive: true });

const p14Report = {
  generatedAtUtc: new Date().toISOString(),
  taskId: "P14-T03",
  status: legacyMatches.length === 0 && phaseNamedMatches.length === 0 ? "ready-to-close" : "pack-c-required",
  legacyApiMatches: legacyMatches,
  phaseNamedMatches,
  note: "Pack B3.5 does not close P14-T03. Pack C must migrate imports, retire legacy API client, and rename phase-named product artifacts."
};

fs.writeFileSync(
  path.join(reportDir, "p14-t03-legacy-retirement-readiness.json"),
  JSON.stringify(p14Report, null, 2),
  "utf8"
);

console.log("");
console.log(`P14-T03 readiness report written. legacyMatches=${legacyMatches.length}, phaseNamedMatches=${phaseNamedMatches.length}`);
console.log("Pack B3.5 static validation passed.");