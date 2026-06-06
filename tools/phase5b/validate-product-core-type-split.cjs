const fs = require('fs');
const path = require('path');
const cp = require('child_process');

const root = process.cwd();

const implementationPath = path.join(root, 'Frontend/PlantProcess.Web/src/api/productCoreApiClient.implementation.ts');
const barrelPath = path.join(root, 'Frontend/PlantProcess.Web/src/api/product-core/types.ts');
const manifestPath = path.join(root, 'Frontend/PlantProcess.Web/src/api/product-core/product-core-types.manifest.json');

function read(file) {
  if (!fs.existsSync(file)) throw new Error('Missing file: ' + path.relative(root, file));
  return fs.readFileSync(file, 'utf8');
}

const implementation = read(implementationPath);
const barrel = read(barrelPath);
const manifest = JSON.parse(read(manifestPath));

if (!implementation.includes('PPIQ_PHASE5B_PRODUCT_CORE_TYPES_SPLIT')) {
  throw new Error('Missing PPIQ_PHASE5B_PRODUCT_CORE_TYPES_SPLIT marker in implementation file.');
}

if (!barrel.includes('PPIQ_PHASE5B_PRODUCT_CORE_TYPES_DOMAIN_SPLIT')) {
  throw new Error('Missing PPIQ_PHASE5B_PRODUCT_CORE_TYPES_DOMAIN_SPLIT marker in product-core/types.ts barrel.');
}

if (!implementation.includes('from "./product-core/types"')) {
  throw new Error('Implementation does not import/export the product-core barrel.');
}

const forbiddenTopLevel = /^export\s+(interface|type)\s+([A-Za-z_$][\w$]*)/gm;
const offenders = [];
let match;

while ((match = forbiddenTopLevel.exec(implementation)) !== null) {
  offenders.push(match[0]);
}

if (offenders.length > 0) {
  throw new Error('Exported type/interface declarations still remain in implementation file: ' + offenders.join(', '));
}

const requiredTypeNames = [
  "AdminJobMonitorRow",
  "AdminJobsMonitor",
  "AdminLatestImportBatch",
  "AdminMetricCard",
  "AdminOverview",
  "AdminStatusCount",
  "ConnectionProfileRecord",
  "CreateConnectionProfileRequest",
  "CreateDashboardWidgetDefinitionPayload",
  "CreateKpiDefinitionRequest",
  "CreateSchemaViewDefinitionRequest",
  "CreateSourceDatasetDefinitionRequest",
  "CsvImportSnapshotRequest",
  "CsvImportSnapshotResult",
  "CsvPreviewRequest",
  "CsvPreviewResult",
  "CsvSchemaDiscoveryRequest",
  "CsvSchemaDiscoveryResult",
  "DashboardChartTypeMetadata",
  "DashboardCompatibilityRule",
  "DashboardDefinitionRecord",
  "DashboardDimensionMetadata",
  "DashboardFilterMetadata",
  "DashboardFilters",
  "DashboardMaterialRow",
  "DashboardMeasureMetadata",
  "DashboardMetadata",
  "DashboardPurposeMetadata",
  "DashboardQuerySafetyLimits",
  "DashboardReferenceData",
  "DashboardWidgetColumn",
  "DashboardWidgetDefinitionRecord",
  "DashboardWidgetFilters",
  "DashboardWidgetQuery",
  "DashboardWidgetQueryOptions",
  "DashboardWidgetQueryResult",
  "DashboardWidgetResolved",
  "DashboardWorkspace",
  "DbConfigurationSourceSystem",
  "DbConfigurationSummary",
  "GenealogyAwareCorrelationBin",
  "GenealogyAwareCorrelationResult",
  "JobActionResponse",
  "JobRunHistoryRecord",
  "KpiDefinitionRecord",
  "MaterialInvestigationRequestOptions",
  "PagedResult",
  "PlannedProvider",
  "ProviderTypeRecord",
  "ReferenceItem",
  "SchemaConfigurationSummary",
  "SchemaMappingSummary",
  "SchemaViewDefinitionRecord",
  "SchemaViewPreviewColumn",
  "SchemaViewPreviewRequest",
  "SchemaViewPreviewResult",
  "SortDirection",
  "SourceDatasetDefinitionRecord",
  "SourceFieldDefinitionRecord",
  "SourceObjectCoverage",
  "TwoStageImportModel",
  "TwoStageImportStage",
  "UpdateConnectionImportScheduleRequest",
  "UpdateMappingRefreshScheduleRequest",
  "UpdateSchemaViewDefinitionRequest",
  "WidgetQueryExpressionRequest",
  "WidgetQueryExpressionResult"
];

for (const typeName of requiredTypeNames) {
  const declarationRegex = new RegExp('export\\s+(interface|type)\\s+' + typeName + '\\b');
  const moduleEntry = manifest.types.find((entry) => entry.name === typeName);

  if (!moduleEntry) {
    throw new Error('Missing manifest entry for type: ' + typeName);
  }

  const modulePath = path.join(root, 'Frontend', 'PlantProcess.Web', 'src', 'api', 'product-core', moduleEntry.file);
  const moduleText = read(modulePath);

  if (!declarationRegex.test(moduleText)) {
    throw new Error('Type not found in declared module: ' + typeName + ' -> ' + moduleEntry.file);
  }

  if (!barrel.includes(moduleEntry.file.replace(/\.ts$/, ''))) {
    throw new Error('Barrel does not re-export module: ' + moduleEntry.file);
  }
}

for (const moduleFile of [
  "admin-mapping-types.ts",
  "analytics-quality-types.ts",
  "dashboard-widget-types.ts",
  "license-commercial-types.ts",
  "material-process-types.ts",
  "shared-types.ts"
]) {
  const modulePath = path.join(root, 'Frontend', 'PlantProcess.Web', 'src', 'api', 'product-core', moduleFile);
  const moduleText = read(modulePath);
  const lines = moduleText.split(/\r?\n/).length;

  if (lines > 700) {
    throw new Error(moduleFile + ' is still too large: ' + lines + ' lines');
  }
}

cp.execFileSync('node', ['tools/phase56/validate-phase56.cjs'], {
  cwd: root,
  stdio: 'inherit'
});

console.log('Phase 5B-1 product-core domain type split validation passed.');
