const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function check(file, signals) {
  const full = path.join(root, file);
  if (!fs.existsSync(full)) {
    failures.push("Missing file: " + file);
    return;
  }

  const text = fs.readFileSync(full, "utf8");
  for (const signal of signals) {
    if (!text.includes(signal)) {
      failures.push(file + " missing signal: " + signal);
    }
  }
}

check("Backend/PlantProcess.Application/Licensing/Phase10/Phase10LicenseRuntime.cs", [
  "PPIQ_REALIZATION_T059_GRACE_EXPIRY_OVERAGE_FLOWS",
  "PPIQ_REALIZATION_T061_OFFLINE_SIGNED_LICENSE_ACTIVATION",
  "DefaultGraceDays = 30",
  "CustomerDataPreserved",
  "Ed25519",
  "Tamper detected"
]);

check("Backend/tests/PlantProcess.Application.UnitTests/Licensing/Phase10LicenseRuntimeTests.cs", [
  "T059_PreExpiry_Warning_Appears_Before_Expiry",
  "T059_Expired_Inside_Grace_Drops_To_ReadOnly_But_Preserves_Dashboards",
  "T061_Offline_Signed_License_Activates_In_Airgap_Mode",
  "T061_Tampered_Offline_License_Is_Rejected_And_Audited"
]);

check("Backend/PlantProcess.Api/Endpoints/Licensing/Phase10LicenseEndpoints.cs", [
  "PPIQ_REALIZATION_T058_LIVE_TIER_TOGGLE_DEMO",
  "/lifecycle/evaluate",
  "/offline-activation/verify"
]);

check("Backend/PlantProcess.Api/Program.cs", [
  "MapPhase10LicenseEndpoints"
]);

check("Frontend/PlantProcess.Web/src/license/phase10License.ts", [
  "Muted Steel",
  "Amber",
  "Electric Blue",
  "Cyan Green",
  "liveTierToggleResult",
  "Upgrade from"
]);

check("Frontend/PlantProcess.Web/src/license/phase10License.test.ts", [
  "T-058 hides Enterprise-only features",
  "T-060 maps every tier",
  "T-059 labels read-only lifecycle states"
]);

check("Frontend/PlantProcess.Web/src/pages/Phase10/Phase10LicenseDemoPage.tsx", [
  "Phase 10 License Runtime Demo",
  "Ed25519 signed-license envelope",
  "data-testid=\"phase10-license-demo\""
]);

check("Frontend/PlantProcess.Web/src/App.implementation.tsx", [
  "/phase10/license",
  "Phase10LicenseDemoPage"
]);

check("Frontend/PlantProcess.Web/e2e/phase10-license-runtime.spec.ts", [
  "T-058/T-062 live tier toggle"
]);

if (failures.length > 0) {
  console.error("PPIQ Phase 10 validation failed.");
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("PPIQ Phase 10 passed: T-058/T-059/T-060/T-061/T-062 implementation files are present.");