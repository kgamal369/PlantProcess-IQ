import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) {
    throw new Error(`Missing required file: ${relativePath}`);
  }

  return fs.readFileSync(full, "utf8");
}

function assertContains(label, text, needle) {
  if (!text.includes(needle)) {
    throw new Error(`${label} missing: ${needle}`);
  }

  console.log(`✓ ${label}: ${needle}`);
}

const p05Sql = read("Backend/database/scripts/540_v5_p05_visual_mapper_foundation.sql");
const p06Sql = read("Backend/database/scripts/550_v5_p06_blended_provenance.sql");
const visualMapper = read("Backend/PlantProcess.Api/VisualMapper/V5VisualMapperEndpoints.cs");
const blended = read("Backend/PlantProcess.Api/BlendedProvenance/V5BlendedProvenanceEndpoints.cs");
const edge = read("Backend/PlantProcess.Domain/Entities/Materials/GenealogyEdge.cs");
const edgeConfig = read("Backend/PlantProcess.Infrastructure/Persistence/Configurations/Materials/GenealogyEdgeConfiguration.cs");
const page = read("Frontend/PlantProcess.Web/src/pages/V5NoCodeMapperPage.tsx");
const program = read("Backend/PlantProcess.Api/Program.cs");

assertContains("P05 Discovery tables", p05Sql, "ppiq_visual_mapper_tables");
assertContains("P05 Discovery columns", p05Sql, "ppiq_visual_mapper_columns");
assertContains("P05 Business keys", p05Sql, "ppiq_visual_mapper_business_keys");
assertContains("P05 Join preview", p05Sql, "ppiq_visual_mapper_joins");
assertContains("P05 Suggestions", p05Sql, "ppiq_visual_mapper_canonical_suggestions");
assertContains("P05 Dry-run", p05Sql, "ppiq_visual_mapper_dry_runs");
assertContains("P05 Version publish", p05Sql, "ppiq_visual_mapper_versions");
assertContains("P05 Templates", p05Sql, "ppiq_visual_mapper_templates");
assertContains("P05 Drift", p05Sql, "ppiq_schema_drift_events");
assertContains("P05 Defect harmonization", p05Sql, "ppiq_defect_catalog_mappings");
assertContains("P05 Acceptance", p05Sql, "ppiq_v5_p05_acceptance");

assertContains("P05 API group", visualMapper, "/api/v5/visual-mapper");
assertContains("P05 Read-only discovery", visualMapper, "read-only-demo-introspection");
assertContains("P05 SafeSqlValidator", visualMapper, "SafeSqlValidator.Validate");
assertContains("P05 Canonical suggestion", visualMapper, "SuggestCanonicalTarget");
assertContains("P05 Publish immutable version", visualMapper, "versionNumber");

assertContains("P06 SQL weight column", p06Sql, "contribution_weight");
assertContains("P06 SQL transition column", p06Sql, "is_transition");
assertContains("P06 SQL confidence column", p06Sql, "provenance_confidence");
assertContains("P06 SQL weight guard", p06Sql, "ppiq_genealogy_edge_weight_guard");
assertContains("P06 SQL blended view", p06Sql, "ppiq_v5_blended_genealogy_edges");
assertContains("P06 SQL attribution function", p06Sql, "ppiq_v5_blended_attribution_for_child");
assertContains("P06 Acceptance", p06Sql, "ppiq_v5_p06_acceptance");

assertContains("P06 Domain weight", edge, "ContributionWeight");
assertContains("P06 Domain transition", edge, "IsTransition");
assertContains("P06 Domain confidence", edge, "ProvenanceConfidence");
assertContains("P06 Domain invariant", edge, "ValidateChildContributionWeights");
assertContains("P06 EF weight", edgeConfig, "ContributionWeight");
assertContains("P06 EF transition", edgeConfig, "IsTransition");
assertContains("P06 EF confidence", edgeConfig, "ProvenanceConfidence");

assertContains("P06 API group", blended, "/api/v5/blended-provenance");
assertContains("P06 API attribution", blended, "ppiq_v5_blended_attribution_for_child");
assertContains("P06 API weights status", blended, "ppiq_v5_child_weight_status");

assertContains("Frontend page", page, "No-Code Visual Mapper + Blended Provenance");
assertContains("Frontend discovery action", page, "Run read-only demo discovery");
assertContains("Program maps visual mapper", program, "MapV5VisualMapperEndpoints");
assertContains("Program maps blended provenance", program, "MapV5BlendedProvenanceEndpoints");

console.log("");
console.log("P05/P06 Doctrine v5 static acceptance passed.");