const fs = require("fs");
const path = require("path");

const root = process.cwd();

function file(relativePath) {
  return path.join(root, relativePath);
}

function read(relativePath) {
  const absolute = file(relativePath);
  if (!fs.existsSync(absolute)) throw new Error("Missing file: " + relativePath);
  if (!fs.statSync(absolute).isFile()) throw new Error("Expected file but found directory: " + relativePath);
  return fs.readFileSync(absolute, "utf8");
}

function has(relativePath, marker) {
  const text = read(relativePath).toLowerCase();
  if (!text.includes(String(marker).toLowerCase())) {
    throw new Error(relativePath + " missing marker: " + marker);
  }
}

function hasAny(relativePath, markers) {
  const text = read(relativePath).toLowerCase();
  for (const marker of markers) {
    if (text.includes(String(marker).toLowerCase())) return;
  }
  throw new Error(relativePath + " missing any marker: " + markers.join(", "));
}

function anyHas(relativePaths, markers) {
  const candidates = Array.isArray(relativePaths) ? relativePaths : [relativePaths];
  const required = Array.isArray(markers) ? markers : [markers];

  for (const relativePath of candidates) {
    const absolute = file(relativePath);
    if (!fs.existsSync(absolute) || !fs.statSync(absolute).isFile()) continue;

    const text = fs.readFileSync(absolute, "utf8").toLowerCase();
    if (required.every((marker) => text.includes(String(marker).toLowerCase()))) return;
  }

  throw new Error("No candidate contains all markers [" + required.join(", ") + "]: " + candidates.join(", "));
}

const materialInvestigationCandidates = [
  "Frontend/PlantProcess.Web/src/pages/MaterialInvestigation/MaterialInvestigationPage.tsx",
  "Frontend/PlantProcess.Web/src/pages/Materials/MaterialInvestigationPage.tsx",
  "Frontend/PlantProcess.Web/src/pages/MaterialInvestigationPage.tsx"
];

const appRouteCandidates = [
  "Frontend/PlantProcess.Web/src/App.implementation.tsx",
  "Frontend/PlantProcess.Web/src/App.tsx"
];

/*
  P03/P04 validator policy:
  - 310 owns generic mapping/genealogy foundation.
  - 311 owns genealogy walk + safe SQL fixes.
  - 312 owns completion gates and lifecycle proof.
  - 313 owns material-investigation ambiguity, rollback proof, and duplicate business-key hotfixes.
  - Frontend active page is src/pages/MaterialInvestigation/MaterialInvestigationPage.tsx.
*/

has("Backend/database/scripts/310_p03_p04_mapping_genealogy_foundation.sql", "ppiq_business_key_definitions");
has("Backend/database/scripts/310_p03_p04_mapping_genealogy_foundation.sql", "canonical_genealogy_edges");

has("Backend/database/scripts/311_p03_p04_fix_genealogy_walk_and_safe_sql.sql", "ppiq_walk_genealogy");
has("Backend/database/scripts/311_p03_p04_fix_genealogy_walk_and_safe_sql.sql", "ppiq_validate_safe_sql");

has("Backend/database/scripts/312_p03_p04_completion_pack_a.sql", "ppiq_validate_business_key_dictionary");
has("Backend/database/scripts/312_p03_p04_completion_pack_a.sql", "ppiq_resolve_safe_sql");
has("Backend/database/scripts/312_p03_p04_completion_pack_a.sql", "ppiq_run_mapping_lifecycle_proof");
has("Backend/database/scripts/312_p03_p04_completion_pack_a.sql", "ppiq_p03_p04_completion_status");

has("Backend/database/scripts/313_p03_p04_completion_pack_a_hotfix.sql", "P03/P04 Completion Pack A Hotfix");
hasAny("Backend/database/scripts/313_p03_p04_completion_pack_a_hotfix.sql", [
  "ppiq_material_investigation",
  "material_key ambiguity",
  "material investigation"
]);
hasAny("Backend/database/scripts/313_p03_p04_completion_pack_a_hotfix.sql", [
  "rollback proof",
  "mapping lifecycle rollback",
  "previous published version"
]);
hasAny("Backend/database/scripts/313_p03_p04_completion_pack_a_hotfix.sql", [
  "business-key duplicate",
  "duplicate sample classification",
  "cross-source demo match"
]);

has("Backend/database/scripts/430_phase3_phase4_certification_mapping_health.sql", "ppiq_phase34_certification_status");
has("Backend/database/scripts/430_phase3_phase4_certification_mapping_health.sql", "ppiq_v_phase34_mapping_health_summary");

has("Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs", "/admin/p03p04/completion");
has("Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs", "ppiq_p03_p04_completion_status");
has("Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs", "ppiq_validate_business_key_dictionary");
has("Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs", "ppiq_resolve_safe_sql");
has("Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs", "ppiq_run_mapping_lifecycle_proof");

has("Backend/PlantProcess.Api/Endpoints/Admin/P03P04MappingGenealogyEndpoints.cs", "/admin/p03p04");
has("Backend/PlantProcess.Api/Endpoints/Admin/P03P04MappingGenealogyEndpoints.cs", "canonical_genealogy_edges");

anyHas(materialInvestigationCandidates, [
  "/admin/p03p04/completion/status",
  "genealogy-validation",
  "mapping-lifecycle-proof"
]);

anyHas(materialInvestigationCandidates, [
  "safe-sql/resolve",
  "material-investigation",
  "business-key-validation"
]);

anyHas(appRouteCandidates, [
  "MaterialInvestigationPage",
  "/materials"
]);

anyHas(appRouteCandidates, [
  "/material-investigation"
]);

console.log("Phase 3 + Phase 4 source validation passed.");
