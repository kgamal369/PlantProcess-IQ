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
  "Frontend/PlantProcess.Web/src/pages/DynamicPage/DynamicPage.tsx",
  /pageBuilderApi\.getBySlug/,
  "DynamicPage must load backend PageDefinition by slug",
);

mustContain(
  "Frontend/PlantProcess.Web/src/pages/DynamicPage/DynamicPage.tsx",
  /data-runtime-widget-id/,
  "DynamicPage must render runtime widgets from layoutJson",
);

mustContain(
  "Frontend/PlantProcess.Web/src/App.tsx",
  /pages\/DynamicPage\/DynamicPage/,
  "App route must use real DynamicPage component",
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs",
  /MapGroup\("\/phase4"\)/,
  "Phase 4 endpoint group must exist",
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs",
  /MapGroup\("\/phase5"\)/,
  "Phase 5 endpoint group must exist",
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs",
  /phase4_feature_store_evidence/,
  "Phase 4 feature evidence table must exist",
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs",
  /phase5_learning_job_evidence/,
  "Phase 5 learning job evidence table must exist",
);

mustContain(
  "Backend/PlantProcess.Api/Program.cs",
  /MapPhase45ClosureEndpoints\(\)/,
  "Program.cs must map Phase 4/5 closure endpoints",
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/phase3-dynamic-page-rendering.spec.ts",
  /\/pages\//,
  "P03 dynamic page E2E must exist",
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/phase4-ml-foundation-proof.spec.ts",
  /phase4\/features\/acceptance/,
  "P04 E2E must call feature acceptance",
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/phase5-scheduled-learning-proof.spec.ts",
  /phase5\/scheduled-learning\/acceptance/,
  "P05 E2E must call scheduled learning acceptance",
);

if (failures.length > 0) {
  console.error("Pack 4-6 validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("Pack 4-6 P03/P04/P05 structural validation passed.");
