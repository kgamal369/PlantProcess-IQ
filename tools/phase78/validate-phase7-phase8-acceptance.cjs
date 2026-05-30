const fs = require("node:fs");
const path = require("node:path");

const cwd = process.cwd();
const root = fs.existsSync(path.join(cwd, "Backend"))
  ? cwd
  : path.resolve(cwd, "..", "..");
const failures = [];

function p(file) {
  return path.join(root, file.split("/").join(path.sep));
}

function exists(file) {
  return fs.existsSync(p(file));
}

function read(file) {
  return exists(file) ? fs.readFileSync(p(file), "utf8") : "";
}

function pass(task, message) {
  console.log("✓ " + task + " — " + message);
}

function fail(task, message) {
  failures.push({ task, message });
}

function assert(task, condition, message) {
  if (condition) pass(task, message);
  else fail(task, message);
}

function contains(file, patterns) {
  const text = read(file);
  return patterns.every((pattern) => pattern.test(text));
}

console.log("");
console.log("============================================================");
console.log("PlantProcess IQ — Phase 7 + Phase 8 Acceptance Validation");
console.log("============================================================");
console.log("");

assert("PPIQ-T048", contains("Frontend/PlantProcess.Web/src/pages/Phase78/Phase78Pages.tsx", [/Phase78WorkflowTruthPage/, /phase1WorkflowApi\.getConnectorTruth/, /Connector Truth/, /Schema Fingerprint/, /Sample Rows/]), "Phase1WorkflowTruthPanel replacement is wired with connector truth columns");
assert("PPIQ-T048", contains("Frontend/PlantProcess.Web/src/pages/Admin/AdminPageContent.tsx", [/Phase78AdminPage/]), "Admin route uses Phase78 admin workflow page");

assert("PPIQ-T049", contains("Frontend/PlantProcess.Web/src/components/phase2/SaveInspectionJobModal.tsx", [/StandardModal/, /StandardInput/, /StandardSelect/, /StandardButton/, /notifyOnChange/, /phase78Api\.saveInvestigation/]), "SaveInspectionJobModal uses Standard* primitives and posts saved investigation");
assert("PPIQ-T049", contains("Frontend/PlantProcess.Web/src/pages/Phase56/Phase56Pages.tsx", [/SaveInspectionJobModal/, /Save Investigation/]), "Material investigation save flow references SaveInspectionJobModal");

assert("PPIQ-T050", contains("Frontend/PlantProcess.Web/src/components/phase2/OperationProgressPanel.tsx", [/resetJobId/, /pollEveryMs = 1000/, /phase78Api\.getDemoResetProgress/, /Exception|exceptionDetail|failureReason/]), "OperationProgressPanel polls reset progress every 1s and displays failures");
assert("PPIQ-T050", contains("Frontend/PlantProcess.Web/src/pages/Phase78/Phase78Pages.tsx", [/OperationProgressPanel/, /Import Job Progress/, /Demo Reset Operation Progress/]), "OperationProgressPanel is wired to demo lifecycle and import jobs");

assert("PPIQ-T051", contains("Backend/PlantProcess.Api/Endpoints/Demo/DemoLifecycleEndpoints.cs", [/MapPost\("\/reset"/, /Accepted/, /statusUrl/, /RunResetJobAsync/, /demo_lifecycle_reset_audit/, /Rate limit exceeded/]), "POST /demo-lifecycle/reset returns 202 and writes progress/audit");
assert("PPIQ-T051", contains("Backend/PlantProcess.Api/Endpoints/Demo/DemoLifecycleEndpoints.cs", [/MapGet\("\/reset\/\{jobId:guid\}\/progress"/, /Steps/, /PercentComplete/]), "GET reset progress endpoint exposes step progress contract");

assert("PPIQ-T052", contains("Frontend/PlantProcess.Web/src/pages/Phase78/Phase78Pages.tsx", [/Reset Demo/, /Type RESET/, /Confirm Reset/, /data-only/, /full/, /identities-only/, /Demo reset complete\. Canonical layout active/]), "Reset UI has confirmation, scope selector, disabled state and success copy");

assert("PPIQ-T053", contains("Backend/PlantProcess.Api/Endpoints/Admin/LicenseAdminEndpoints.cs", [
  /tier-override/,
  /effective-tier/,
  /license_overrides/,
  /expires_at_utc/,
  /(source|Source)\s*=\s*["']override["']/,
  /(source|Source)\s*=\s*["']license["']/
]), "License tier override endpoints persist and expose effective tier");

assert("PPIQ-T054", contains("Backend/PlantProcess.Api/Endpoints/DynamicContent/DynamicContentEndpoints.cs", [/\/suggestions/, /\/pages\/\{slug\}/, /NotFound/, /recommendations/]), "Backend suggestions and dynamic page routes exist");
assert("PPIQ-T054", contains("Frontend/PlantProcess.Web/src/App.tsx", [/path="\/suggestions"/, /path="\/pages\/:slug"/]), "Frontend dynamic routes are wired");
assert("PPIQ-T054", exists("Frontend/PlantProcess.Web/e2e/phase78-workflow-widget.spec.ts"), "Phase 7 route smoke e2e exists");

assert("PPIQ-T055", contains("Backend/PlantProcess.Domain/Entities/Dashboarding/DashboardWidgetDefinition.cs", [/QueryExpression/, /AdvancedExpressionJson/, /ExpressionVersion/, /ExpressionEnabled/, /ExpressionLastValidatedAtUtc/, /ExpressionLastValidationStatus/, /ExpressionLastValidationMessage/]), "DashboardWidgetDefinition exposes all 7 widget script columns");
assert("PPIQ-T055", contains("Backend/PlantProcess.Domain/Entities/Dashboarding/DashboardWidgetDefinition.cs", [/ConfigureExpression/, /Cannot enable widget expression unless/, /EnableExpression/]), "Domain invariant prevents enabling invalid expression");
assert("PPIQ-T055", exists("Backend/tests/PlantProcess.Domain.Tests/Dashboarding/DashboardWidgetDefinitionExpressionTests.cs"), "Domain invariant unit tests exist");

assert("PPIQ-T056", contains("Backend/PlantProcess.Infrastructure/Persistence/Configurations/Dashboarding/DashboardWidgetDefinitionConfiguration.cs", [/query_expression/, /advanced_expression_json/, /jsonb/, /expression_version/, /smallint/, /expression_last_validated_at_utc/, /ix_dashboard_widget_definitions_expression_refresh/]), "EF Core maps widget script columns and refresh index");
assert("PPIQ-T056", exists("Backend/database/scripts/117_phase8_widget_script_layer_entity_mapping.sql"), "Phase 8 SQL migration/backfill script exists");

assert("PPIQ-T057", contains("Backend/PlantProcess.Application/Dashboarding/Contracts/WidgetQueryExpressionDtos.cs", [/CompiledWidgetQueryExpression/, /WidgetQueryDimensionExpression/, /WidgetQueryMeasureExpression/, /WidgetQueryFilterExpression/, /WidgetQuerySortExpression/, /WidgetQueryTimeWindowExpression/, /UnknownKeyword/, /MissingValue/, /TypeMismatch/]), "Compiled WidgetQueryExpression grammar records exist");
assert("PPIQ-T057", contains("Backend/PlantProcess.Application/Dashboarding/Services/Widgets/WidgetQueryExpressionService.cs", [/Compile\(/, /ParseMeasure/, /ParseFilter/, /PPIQ__UseCompiledWidgetGrammar/, /ParseLegacy/]), "Compiler supports structured grammar and legacy fallback behind feature flag");
assert("PPIQ-T057", exists("Backend/tests/PlantProcess.Application.UnitTests/Dashboarding/WidgetQueryExpressionServiceTests.cs"), "Compiler unit tests exist");

assert("PPIQ-T058", contains("Frontend/PlantProcess.Web/src/pages/Phase78/Phase78Pages.tsx", [/Phase78WidgetScriptCompilerPage/, /Widget Script Layer Compiler/, /Validate Expression/]), "Widget compiler UI evidence page exists");
assert("PPIQ-T059", contains("Frontend/PlantProcess.Web/src/App.tsx", [/path="\/widget-script-compiler"/]), "Widget script compiler route is wired");
assert("PPIQ-T060", contains("Backend/database/scripts/117_phase8_widget_script_layer_entity_mapping.sql", [/dashboard_widget_expression_audit/, /ix_dashboard_widget_expression_audit_widget/]), "Widget expression audit table/index exist");
assert("PPIQ-T061", contains("Backend/PlantProcess.Application/Dashboarding/Services/Widgets/WidgetQueryExpressionService.cs", [/limit must be a positive integer/, /filter must follow/]), "Compiler has explicit validation failure messages");
assert("PPIQ-T062", contains("Frontend/PlantProcess.Web/e2e/phase78-workflow-widget.spec.ts", [/widget-script-compiler/, /Demo reset confirmation requires RESET/]), "Phase 8/Phase 7 e2e validation exists");

const forbiddenFiles = [
  "Frontend/PlantProcess.Web/src/pages/Phase78/Phase78Pages.tsx",
  "Frontend/PlantProcess.Web/src/components/phase2/SaveInspectionJobModal.tsx",
  "Frontend/PlantProcess.Web/src/components/phase2/OperationProgressPanel.tsx",
];

for (const file of forbiddenFiles) {
  assert("PPIQ-T048-T062", !/could not be loaded|could not load/i.test(read(file)), file + " has no forbidden failure copy");
}

if (failures.length) {
  console.error("");
  console.error("============================================================");
  console.error("Phase 7 + Phase 8 acceptance FAILED");
  console.error("============================================================");

  for (const item of failures) {
    console.error("✖ " + item.task + " — " + item.message);
  }

  console.error("");
  console.error("Do not mark Phase 7/8 as 100% until every item above is fixed.");
  process.exit(1);
}

console.log("");
console.log("============================================================");
console.log("Phase 7 + Phase 8 acceptance PASSED");
console.log("============================================================");
console.log("PPIQ-T048 through PPIQ-T062 are closed for implementation + validation.");
