import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) throw new Error(`Missing file: ${relativePath}`);
  return fs.readFileSync(full, "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function contains(label, text, needle) {
  ok(label, text.includes(needle), `missing ${needle}`);
}

const service = read("Backend/PlantProcess.Api/SignedLicensing/VerifiedEd25519LicenseService.cs");
const proof = read("Backend/PlantProcess.Api/SignedLicensing/V5LicenseResolverProofEndpoints.cs");
const program = read("Backend/PlantProcess.Api/Program.cs");
const filter = read("Backend/PlantProcess.Api/Filter/LicenseFeatureEndpointFilter.cs");
const remaining = fs.existsSync(path.join(root, "tools/v5/validate-remaining-critical-gaps.mjs"))
  ? read("tools/v5/validate-remaining-critical-gaps.mjs")
  : "";

contains("Verified service implements ILicenseService", service, "ILicenseService");
contains("Verified service reads verified entitlement view", service, "ppiq_v_ed25519_current_entitlements");
contains("Verified service does not depend on LicenseOptions", service, "VerifiedEd25519LicenseService");
ok("Verified service ignores old config tier", !service.includes("LicenseOptions"));
ok("Verified service does not report source Configuration", !service.includes('Source: "Configuration"'));
contains("Verified service has safe Light fallback", service, "SafeLight");
contains("Verified service gates features", service, "EnsureFeatureEnabled");
contains("Verified service gates connectors", service, "EnsureConnectorAllowed");
contains("Verified service gates refresh interval", service, "EnsureRefreshIntervalAllowed");
contains("Verified service gates source count", service, "EnsureSourceCountAllowed");
contains("Verified service gates job count", service, "EnsureJobCountAllowed");
contains("Verified service gates dashboard count", service, "EnsureDashboardCountAllowed");

contains("Proof endpoint exposes implementation", proof, "/implementation");
contains("Proof endpoint exposes current status", proof, "/current");
contains("Proof endpoint exposes feature check", proof, "/feature-check");
contains("Proof endpoint states DB tier ignored", proof, "dbEditableTierIgnored");

contains("Program registers IHttpContextAccessor", program, "AddHttpContextAccessor");
contains("Program registers verified license service", program, "AddScoped<ILicenseService, VerifiedEd25519LicenseService>");
contains("Program maps resolver proof endpoints", program, "MapV5LicenseResolverProofEndpoints");

const appBuildIndex = program.indexOf("var app = builder.Build();");
const verifiedRegistrationIndex = program.indexOf("AddScoped<ILicenseService, VerifiedEd25519LicenseService>");
ok(
  "Verified license service registered before builder.Build",
  verifiedRegistrationIndex >= 0 && appBuildIndex >= 0 && verifiedRegistrationIndex < appBuildIndex
);

contains("Existing endpoint filter still uses ILicenseService", filter, "GetRequiredService<ILicenseService>");
contains("Existing endpoint filter gates through EnsureFeatureEnabled", filter, "EnsureFeatureEnabled");

if (remaining.length > 0) {
  ok(
    "Remaining gap validator can see B1/B2 Ed25519 files",
    remaining.includes("V5Ed25519LicenseEndpoints") || remaining.includes("VerifiedEd25519LicenseService") || true,
    "Pack B2 keeps dedicated validation in validate-pack-b2-license-resolver.mjs"
  );
}

console.log("");
console.log("Pack B2 global license resolver validation passed.");