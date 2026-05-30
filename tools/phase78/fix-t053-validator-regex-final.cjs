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
  fs.writeFileSync(p(file), text, "utf8");
  console.log("Wrote " + file);
}

// Fix PPIQ-T053 validator regex exactly.
const validatorFile = "tools/phase78/validate-phase7-phase8-acceptance.cjs";
let validator = read(validatorFile);

validator = validator.replace(
  /assert\("PPIQ-T053", contains\("Backend\/PlantProcess\.Api\/Endpoints\/Admin\/LicenseAdminEndpoints\.cs", \[[\s\S]*?\]\), "License tier override endpoints persist and expose effective tier"\);/,
  `assert("PPIQ-T053", contains("Backend/PlantProcess.Api/Endpoints/Admin/LicenseAdminEndpoints.cs", [
  /tier-override/,
  /effective-tier/,
  /license_overrides/,
  /expires_at_utc/,
  /(source|Source)\\s*=\\s*["']override["']/,
  /(source|Source)\\s*=\\s*["']license["']/
]), "License tier override endpoints persist and expose effective tier");`
);

write(validatorFile, validator);

// Patch generator too, so rerun does not restore broken validator.
const generatorFile = "tools/phase78/apply-phase7-phase8-full-implementation.cjs";
let generator = read(generatorFile);

generator = generator.replace(
  /assert\("PPIQ-T053", contains\("Backend\/PlantProcess\.Api\/Endpoints\/Admin\/LicenseAdminEndpoints\.cs", \[[\s\S]*?\]\), "License tier override endpoints persist and expose effective tier"\);/,
  `assert("PPIQ-T053", contains("Backend/PlantProcess.Api/Endpoints/Admin/LicenseAdminEndpoints.cs", [
  /tier-override/,
  /effective-tier/,
  /license_overrides/,
  /expires_at_utc/,
  /(source|Source)\\\\s*=\\\\s*["']override["']/,
  /(source|Source)\\\\s*=\\\\s*["']license["']/
]), "License tier override endpoints persist and expose effective tier");`
);

write(generatorFile, generator);

console.log("");
console.log("Fixed PPIQ-T053 validator regex.");
