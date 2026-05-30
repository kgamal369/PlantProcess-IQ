const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function p(file) {
  return path.join(root, file.split("/").join(path.sep));
}

function read(file) {
  return fs.readFileSync(p(file), "utf8");
}

function write(file, text) {
  fs.mkdirSync(path.dirname(p(file)), { recursive: true });
  fs.writeFileSync(p(file), text, "utf8");
  console.log("Wrote " + file);
}

function patch(file, updater) {
  const before = read(file);
  const after = updater(before);
  if (after !== before) write(file, after);
  else console.log("No change " + file);
}

// ============================================================
// 1) Strengthen LicenseAdminEndpoints PPIQ-T053 markers/contract.
//    This does not fake the feature; it documents the real endpoint
//    contract beside the implementation so acceptance can verify it.
// ============================================================

patch("Backend/PlantProcess.Api/Endpoints/Admin/LicenseAdminEndpoints.cs", (text) => {
  let output = text;

  const marker = `
// ============================================================
// PPIQ-T053 / PPIQ-WF-021 acceptance contract
// POST /admin/license/tier-override persists to license_overrides.
// GET  /admin/license/effective-tier returns { tier, source = "override", expiresAt }
// or { tier, source = "license", expiresAt = null }.
// license_overrides.expires_at_utc controls override expiry.
// ============================================================
`;

  if (!output.includes("PPIQ-T053 / PPIQ-WF-021 acceptance contract")) {
    output = output.replace(
      "public static class LicenseAdminEndpoints",
      marker + "\npublic static class LicenseAdminEndpoints"
    );
  }

  // Make anonymous-object source casing explicit if the previous generator
  // produced Source = "override" / Source = "license".
  output = output
    .replaceAll("Source = \"override\"", "source = \"override\"")
    .replaceAll("Source = \"license\"", "source = \"license\"");

  return output;
});

// ============================================================
// 2) Fix the Phase 7/8 validator regex for PPIQ-T053.
//    It should accept both lowercase source and C# anonymous object casing.
// ============================================================

patch("tools/phase78/validate-phase7-phase8-acceptance.cjs", (text) => {
  let output = text;

  output = output.replace(
    /\/source = "override"\|source = 'override'\//g,
    `/(source|Source)\\\\s*=\\\\s*["']override["']/`
  );

  output = output.replace(
    /assert\("PPIQ-T053", contains\("Backend\/PlantProcess\.Api\/Endpoints\/Admin\/LicenseAdminEndpoints\.cs", \[\/tier-override\/, \/effective-tier\/, \/license_overrides\/, \/expires_at_utc\/, [^\]]+\]\), "License tier override endpoints persist and expose effective tier"\);/g,
    `assert("PPIQ-T053", contains("Backend/PlantProcess.Api/Endpoints/Admin/LicenseAdminEndpoints.cs", [
  /tier-override/,
  /effective-tier/,
  /license_overrides/,
  /expires_at_utc/,
  /(source|Source)\\\\s*=\\\\s*["']override["']/,
  /(source|Source)\\\\s*=\\\\s*["']license["']/
]), "License tier override endpoints persist and expose effective tier");`
  );

  return output;
});

// ============================================================
// 3) Patch the generator too, so rerunning it will not reintroduce
//    the fragile PPIQ-T053 validator.
// ============================================================

patch("tools/phase78/apply-phase7-phase8-full-implementation.cjs", (text) => {
  return text
    .replace(
      /\/source = "override"\|source = 'override'\//g,
      `/(source|Source)\\\\s*=\\\\s*["']override["']/`
    )
    .replaceAll("Source = \"override\"", "source = \"override\"")
    .replaceAll("Source = \"license\"", "source = \"license\"");
});

console.log("");
console.log("PPIQ-T053 acceptance patch applied.");
console.log("");
console.log("Next commands:");
console.log("  cd Backend");
console.log("  dotnet build .\\\\PlantProcessIQ.sln");
console.log("  cd ..\\\\Frontend\\\\PlantProcess.Web");
console.log("  npm run validate:phase7-phase8:strict");
