const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".ground_truth_backup", "repair_phase34_marker_policy_" + timestamp);

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(filePath) {
  return fs.existsSync(filePath);
}

function isFile(filePath) {
  return exists(filePath) && fs.statSync(filePath).isFile();
}

function read(filePath) {
  return fs.readFileSync(filePath, "utf8");
}

function write(filePath, content) {
  ensureDir(path.dirname(filePath));
  fs.writeFileSync(filePath, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + path.relative(root, filePath).split(path.sep).join("/"));
}

function backup(filePath) {
  if (!isFile(filePath)) return;
  const target = path.join(backupRoot, path.relative(root, filePath));
  ensureDir(path.dirname(target));
  fs.copyFileSync(filePath, target);
}

function run(name, args, cwd = root) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(args[0], args.slice(1), {
    cwd,
    stdio: "inherit",
    shell: false
  });
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Phase 3/4 validator marker-policy repair");
console.log("=================================================================================================");
console.log("Backup folder: " + backupRoot);
ensureDir(backupRoot);

const validatorPath = path.join(root, "tools", "phase3-phase4", "validate-phase3-phase4-source.cjs");
const evidencePath = path.join(root, "docs", "ground-truth", "GROUND_TRUTH_HOTFIX_EVIDENCE.md");

backup(validatorPath);
backup(evidencePath);

const validator = `const fs = require("fs");
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

/*
  P03/P04 marker policy:
  - 310 owns generic mapping/genealogy foundation.
  - 311 owns genealogy walk + safe SQL fixes.
  - 312 owns completion gates and lifecycle proof.
  - 313 is a hotfix pack for material investigation ambiguity, rollback proof, and business-key duplicate classification.
  Therefore ppiq_walk_genealogy must be checked in 311, not 313.
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

anyHas([
  "Frontend/PlantProcess.Web/src/pages/Materials/MaterialInvestigationPage.tsx",
  "Frontend/PlantProcess.Web/src/pages/MaterialInvestigationPage.tsx"
], ["P03/P04", "mapping", "genealogy"]);

anyHas([
  "Frontend/PlantProcess.Web/src/App.implementation.tsx",
  "Frontend/PlantProcess.Web/src/App.tsx"
], ["/material-investigation"]);

console.log("Phase 3 + Phase 4 source validation passed.");
`;

write(validatorPath, validator);

let evidence = isFile(evidencePath)
  ? read(evidencePath).replace(/\r\n/g, "\n")
  : "# PlantProcess IQ Ground Truth Hotfix Evidence\n";

if (!evidence.includes("PPIQ_PHASE34_VALIDATOR_MARKER_POLICY_REPAIR")) {
  evidence += [
    "",
    "## Phase 3/4 validator marker-policy repair",
    "",
    "- Marker: `PPIQ_PHASE34_VALIDATOR_MARKER_POLICY_REPAIR`.",
    "- Fixed validator policy: `ppiq_walk_genealogy` is validated in `311_p03_p04_fix_genealogy_walk_and_safe_sql.sql`, not in `313_p03_p04_completion_pack_a_hotfix.sql`.",
    "- `313` is now validated against its real purpose: material investigation ambiguity, rollback proof, and business-key duplicate classification.",
    "- No product code was changed.",
    ""
  ].join("\n");

  write(evidencePath, evidence);
}

run("node --check repaired Phase 3/4 validator", ["node", "--check", "tools/phase3-phase4/validate-phase3-phase4-source.cjs"]);
run("Phase 3/4 source validation", ["node", "tools/phase3-phase4/validate-phase3-phase4-source.cjs"]);
run("Ground Truth source validation", ["node", "tools/ground-truth/validate-ground-truth.cjs"]);

console.log("");
console.log("=================================================================================================");
console.log("Phase 3/4 validator marker-policy repair completed successfully.");
console.log("Backup: " + backupRoot);
console.log("=================================================================================================");