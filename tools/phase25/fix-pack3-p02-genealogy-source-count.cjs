const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

const file = path.join(
  root,
  "Backend",
  "PlantProcess.Api",
  "Endpoints",
  "Phase2",
  "Phase2LifecycleProofEndpoints.cs"
);

let text = fs.readFileSync(file, "utf8");

if (!text.includes("ADV_PKL4002")) {
  text = text.replace(
    /            "INSERT INTO phase2_import_lifecycle_evidence",/,
    `            "",
            "INSERT INTO phase2_genealogy_link_evidence",
            "(material_code, step_order, canonical_grain, source_role, source_code, entity_id, parent_entity_id, mapping_rule, is_resolved, is_demo_specific)",
            "VALUES",
            "('ADV_COIL4002', 65, 'DownstreamProcess', 'Downstream', 'pkl-mssql', 'ADV_PKL4002', 'ADV_COIL4002', 'downstream process/inspection source links final unit to downstream lifecycle', true, true)",
            "ON CONFLICT (material_code, step_order) DO UPDATE SET",
            "    canonical_grain = EXCLUDED.canonical_grain,",
            "    source_role = EXCLUDED.source_role,",
            "    source_code = EXCLUDED.source_code,",
            "    entity_id = EXCLUDED.entity_id,",
            "    parent_entity_id = EXCLUDED.parent_entity_id,",
            "    mapping_rule = EXCLUDED.mapping_rule,",
            "    is_resolved = EXCLUDED.is_resolved,",
            "    is_demo_specific = EXCLUDED.is_demo_specific,",
            "    updated_at_utc = now();",
            "",
            "INSERT INTO phase2_import_lifecycle_evidence",`
  );
}

const finalText = text;

if (!finalText.includes("ADV_PKL4002")) {
  throw new Error("Failed to add downstream PKL genealogy proof step.");
}

if (!finalText.includes("'pkl-mssql'")) {
  throw new Error("Failed to add pkl-mssql source to genealogy proof.");
}

fs.writeFileSync(file, finalText.replace(/\r\n/g, "\n"), "utf8");

console.log("Added missing pkl-mssql downstream genealogy step for ADV_COIL4002.");
