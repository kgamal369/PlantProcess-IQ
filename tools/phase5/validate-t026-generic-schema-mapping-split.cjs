const fs = require('fs');
const path = require('path');

const root = process.cwd();
const dir = path.join(root, 'Backend', 'PlantProcess.Api', 'Endpoints', 'Admin');
const failures = [];

function rel(file) {
  return path.relative(root, file).split(path.sep).join('/');
}

function read(file) {
  return fs.readFileSync(file, 'utf8');
}

function lineCount(text) {
  return text.replace(/\r\n/g, '\n').split('\n').length;
}

const required = [
  'GenericSchemaMappingEndpoints.cs',
  'GenericSchemaMappingEndpoints.Catalog.cs',
  'GenericSchemaMappingEndpoints.Catalog.Query.cs',
  'GenericSchemaMappingEndpoints.Catalog.Registration.cs',
  'GenericSchemaMappingEndpoints.Resolver.cs',
  'GenericSchemaMappingEndpoints.Joins.cs',
  'GenericSchemaMappingEndpoints.Kpi.cs',
  'GenericSchemaMappingEndpoints.Execution.cs',
  'GenericSchemaMappingEndpoints.SqlHelpers.cs',
  'GenericSchemaMappingEndpoints.PreviewRows.cs',
  'GenericSchemaMappingEndpoints.Contracts.cs',
  'GenericSchemaMappingEndpoints.runtime.cs'
];

for (const name of required) {
  const file = path.join(dir, name);

  if (!fs.existsSync(file)) {
    failures.push({ file: rel(file), reason: 'required split file missing' });
    continue;
  }

  const text = read(file);
  const lines = lineCount(text);

  if (lines > 300) {
    failures.push({ file: rel(file), reason: 'file exceeds 300 lines', lines });
  }
}

const route = read(path.join(dir, 'GenericSchemaMappingEndpoints.cs'));
const runtime = read(path.join(dir, 'GenericSchemaMappingEndpoints.runtime.cs'));
const allText = required.map(function(name) { return read(path.join(dir, name)); }).join('\n');

if (!route.includes('PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_ROUTE_SPLIT')) {
  failures.push({ file: 'GenericSchemaMappingEndpoints.cs', reason: 'route split marker missing' });
}

if (!runtime.includes('PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_RUNTIME_RETIRED')) {
  failures.push({ file: 'GenericSchemaMappingEndpoints.runtime.cs', reason: 'runtime retirement marker missing' });
}

if (runtime.includes('GetCatalogAsync') || runtime.includes('RegisterCanonicalViewAsync')) {
  failures.push({ file: 'GenericSchemaMappingEndpoints.runtime.cs', reason: 'runtime implementation still present' });
}

for (const signal of [
  'GetCatalogAsync',
  'RegisterCanonicalViewAsync',
  'ResolveSchemaViewAsync',
  'PreviewJoinAsync',
  'MaterializeJoinAsync',
  'CreateKpiViewAsync',
  'ExecuteMappingAsync',
  'GetReadinessAsync',
  'QueryAsync',
  'PreviewRowsAsync',
  'RegisterCanonicalViewRequest',
  'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_QUERY_SPLIT',
  'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_REGISTRATION_SPLIT',
  'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_PREVIEW_ROWS_SPLIT'
]) {
  if (!allText.includes(signal)) {
    failures.push({ file: 'GenericSchemaMappingEndpoints split', reason: 'missing signal: ' + signal });
  }
}

if (failures.length) {
  console.error('PPIQ-T026 failed: GenericSchemaMappingEndpoints split is incomplete.');
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log('PPIQ-T026 passed: GenericSchemaMappingEndpoints runtime shim retired and all split files are below 300 lines.');
