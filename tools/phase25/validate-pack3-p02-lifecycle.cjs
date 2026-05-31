const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const failures = [];

function read(relativePath) {
  const file = path.join(root, relativePath);
  if (!fs.existsSync(file)) {
    failures.push("Missing file: " + relativePath);
    return "";
  }

  return fs.readFileSync(file, "utf8");
}

function mustContain(relativePath, regex, message) {
  const text = read(relativePath);
  if (text && !regex.test(text)) {
    failures.push(message + " in " + relativePath);
  }
}

mustContain(
  "Backend/PlantProcess.Api/Endpoints/Phase2/Phase2LifecycleProofEndpoints.cs",
  /MapGroup\("\/phase2"\)/,
  "Phase 2 endpoint group must exist",
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/Phase2/Phase2LifecycleProofEndpoints.cs",
  /source-lifecycle\/acceptance/,
  "source lifecycle acceptance endpoint must exist",
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/Phase2/Phase2LifecycleProofEndpoints.cs",
  /genealogy\/\{materialCode\}/,
  "genealogy endpoint must exist",
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/Phase2/Phase2LifecycleProofEndpoints.cs",
  /phase2_import_lifecycle_evidence/,
  "import lifecycle proof table must exist",
);

mustContain(
  "Backend/PlantProcess.Api/Program.cs",
  /using PlantProcess\.Api\.Endpoints\.Phase2;/,
  "Program.cs must import Phase2 endpoints",
);

mustContain(
  "Backend/PlantProcess.Api/Program.cs",
  /MapPhase2LifecycleProofEndpoints\(\)/,
  "Program.cs must map Phase2 lifecycle endpoints",
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/phase2-lifecycle-proof.spec.ts",
  /ADV_COIL4002/,
  "P02 E2E must prove ADV_COIL4002 genealogy",
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/phase2-lifecycle-proof.spec.ts",
  /source-lifecycle\/acceptance/,
  "P02 E2E must call source lifecycle acceptance",
);

if (failures.length > 0) {
  console.error("Pack 3 validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("Pack 3 P02 lifecycle structural validation passed.");
