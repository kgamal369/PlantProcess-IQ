import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) return "";
  return fs.readFileSync(full, "utf8");
}

function countLines(relativePath) {
  const text = read(relativePath);
  return text ? text.split(/\r?\n/).length : 0;
}

function ok(label, condition, evidence = "") {
  if (!condition) {
    throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  }

  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

const findings = [];
function finding(taskId, severity, message, recommendation) {
  findings.push({ taskId, severity, message, recommendation });
}

// P10 strict Ed25519 detector.
// This does not pretend ECDSA is Ed25519.
const signedLicensing = read("Backend/PlantProcess.Api/SignedLicensing/V5SignedLicensingEndpoints.cs") + "\n" + read("Backend/PlantProcess.Api/SignedLicensing/V5Ed25519LicenseEndpoints.cs") + "\n" + read("Backend/PlantProcess.Api/SignedLicensing/VerifiedEd25519LicenseService.cs");
if (!/Ed25519|EdDSA/i.test(signedLicensing)) {
  finding(
    "P10-T01",
    "critical",
    "Signed licensing endpoint does not show strict Ed25519/EdDSA implementation.",
    "Add vetted Ed25519 verification package/source or formally approve ECDSA-P256 roadmap deviation."
  );
}

// P13 release placeholders.
const imageManifest = read("deploy/airgap/IMAGE_MANIFEST.lock");
if (/PIN_AT_RELEASE/i.test(imageManifest)) {
  finding(
    "P13-T01",
    "critical",
    "Air-gap image manifest still contains PIN_AT_RELEASE placeholders.",
    "Build images, capture real sha256 digests, export image tar, and update IMAGE_MANIFEST.lock."
  );
}

// P14 / P05 large file gate.
const largeFiles = [
  "Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs",
  "Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs",
  "Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Phase56/Phase56Pages.tsx",
  "Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizard.tsx",
  "Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizardContent.tsx",
  "Frontend/PlantProcess.Web/src/api/legacy/plantProcessApi.ts"
];

for (const file of largeFiles) {
  const lines = countLines(file);
  if (lines > 400) {
    finding(
      file.includes("GenericSchemaMapping") ? "P05-T04" : "P14-T02",
      "critical",
      `${file} is ${lines} lines, above the strict refactor target.`,
      "Split by product module/responsibility with characterization tests before route/contract changes."
    );
  }
}

// P07 real connector proof.
const plantConnector = read("Backend/PlantProcess.Api/PlantConnectors/V5PlantConnectorEndpoints.cs") + "\n" + read("Backend/PlantProcess.Api/PlantConnectors/V5ConnectorRuntimeCertificationEndpoints.cs");
if (!/OPC|historian|read-only|readOnly/i.test(plantConnector)) {
  finding(
    "P07-T01",
    "high",
    "Plant connector code does not clearly expose read-only historian/OPC proof language.",
    "Add mock historian/OPC smoke pack and certified/uncertified truth flag."
  );
}

// P09 Keycloak proof.
const ssoScim = read("Backend/PlantProcess.Api/EnterpriseSsoScim/V5EnterpriseSsoScimEndpoints.cs") + "\n" + read("Backend/PlantProcess.Api/EnterpriseSsoScim/V5IdentityRuntimeCertificationEndpoints.cs");
const privateModelGateway = read("Backend/PlantProcess.Api/AssistantGateway/V5AssistantGateway.cs") + "\\n" + read("Backend/PlantProcess.Api/AssistantGateway/V5PrivateModelGatewayCertificationEndpoints.cs");
if (!/Keycloak|issuer|jwks|oidc/i.test(ssoScim)) {
  finding(
    "P09-T01",
    "critical",
    "SSO implementation lacks visible Keycloak/OIDC runtime proof markers.",
    "Add dockerized Keycloak/mock-IdP validation pack with invalid/expired-token tests."
  );
}

// P02 catalog proof.
ok("P02 sensitive catalog script exists", exists("Backend/database/scripts/640_remaining_sensitive_data_catalog.sql"));

// P13/P14 validators.
ok("P13/P14 validator exists", exists("tools/v5/validate-p13-p14.ps1"));
ok("P13 airgap compose exists", exists("deploy/airgap/docker-compose.airgap.yml"));
ok("P13 DR runbook exists", exists("deploy/dr/RPO_RTO_RUNBOOK.md"));

// Save report.
const reportDir = path.join(root, "Documentation", "v5");
fs.mkdirSync(reportDir, { recursive: true });

const reportPath = path.join(reportDir, "remaining-critical-gaps.json");
fs.writeFileSync(reportPath, JSON.stringify(findings, null, 2), "utf8");

if (findings.length > 0) {
  console.error("");
  console.error("Remaining critical gaps still exist.");
  console.error(`Count: ${findings.length}`);
  console.error(`Report: ${reportPath}`);
  console.error("");
  for (const item of findings) {
    console.error(` - [${item.severity}] ${item.taskId}: ${item.message}`);
  }
  console.error("");
  console.error("This is expected until Pack B/C/D are executed. Pack A only installs strict gates and safety foundations.");
  process.exit(3);
}

console.log("OK remaining critical gaps: none detected.");