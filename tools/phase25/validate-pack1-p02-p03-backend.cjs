const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const failures = [];

function mustContain(relativePath, regex, message) {
  const file = path.join(root, relativePath);
  if (!fs.existsSync(file)) {
    failures.push("Missing file: " + relativePath);
    return;
  }

  const text = fs.readFileSync(file, "utf8");
  if (!regex.test(text)) {
    failures.push(message + " in " + relativePath);
  }
}

mustContain(
  "Backend/database/scripts/142_phase02_phase03_page_definition_and_demo_source_completion.sql",
  /CREATE TABLE IF NOT EXISTS page_definitions/,
  "page_definitions table must be created"
);

mustContain(
  "Backend/database/scripts/142_phase02_phase03_page_definition_and_demo_source_completion.sql",
  /demo_source_connection_presets/,
  "demo source presets must exist"
);

mustContain(
  "Backend/database/scripts/142_phase02_phase03_page_definition_and_demo_source_completion.sql",
  /demo_genealogy_acceptance_spine/,
  "genealogy acceptance spine must exist"
);

mustContain(
  "Backend/database/scripts/142_phase02_phase03_page_definition_and_demo_source_completion.sql",
  /v_ppiq_phase02_phase03_acceptance/,
  "P02/P03 acceptance view must exist"
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/PageBuilder/PageDefinitionEndpoints.cs",
  /MapGroup\("\/pages"\)/,
  "/pages endpoint group must exist"
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/PageBuilder/PageDefinitionEndpoints.cs",
  /page_definitions/,
  "PageDefinition endpoint must persist to page_definitions"
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/PageBuilder/PageDefinitionEndpoints.cs",
  /owner_user_name/,
  "PageDefinition endpoint must enforce owner_user_name"
);

mustContain(
  "Backend/PlantProcess.Api/Program.cs",
  /using PlantProcess\.Api\.Endpoints\.PageBuilder;/,
  "Program.cs must import PageBuilder endpoints"
);

mustContain(
  "Backend/PlantProcess.Api/Program.cs",
  /MapPageDefinitionEndpoints\(\)/,
  "Program.cs must map PageDefinition endpoints"
);

if (failures.length > 0) {
  console.error("Pack 1 validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("Pack 1 P02/P03 backend structural validation passed.");
