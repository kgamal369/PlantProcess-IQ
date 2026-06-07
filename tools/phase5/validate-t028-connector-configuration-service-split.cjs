const fs = require('fs');
const path = require('path');

const root = process.cwd();
const dir = path.join(root, 'Backend', 'PlantProcess.Application', 'Integration', 'Services', 'Connectors');
const failures = [];

function rel(file) { return path.relative(root, file).split(path.sep).join('/'); }
function read(file) { return fs.readFileSync(file, 'utf8'); }
function lineCount(text) { return text.replace(/\r\n/g, '\n').split('\n').length; }

const files = fs.readdirSync(dir)
  .filter(function(name) { return name.startsWith('ConnectorConfigurationService') && name.endsWith('.cs'); })
  .map(function(name) { return path.join(dir, name); });

if (files.length < 25) {
  failures.push({ reason: 'not enough ConnectorConfigurationService split files', actual: files.length, expectedAtLeast: 25 });
}

for (const file of files) {
  const text = read(file);
  const lines = lineCount(text);
  if (lines > 300) failures.push({ file: rel(file), reason: 'file exceeds 300 lines', lines });
}

const service = path.join(dir, 'ConnectorConfigurationService.cs');
const runtime = path.join(dir, 'ConnectorConfigurationService.runtime.cs');

if (!fs.existsSync(service)) failures.push({ file: rel(service), reason: 'service surface file missing' });
else {
  const text = read(service);
  if (!text.includes('PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_SURFACE_SPLIT')) failures.push({ file: rel(service), reason: 'surface split marker missing' });
  if (!text.includes('public sealed partial class ConnectorConfigurationService : IConnectorConfigurationService')) failures.push({ file: rel(service), reason: 'service surface does not implement interface' });
}

if (!fs.existsSync(runtime)) failures.push({ file: rel(runtime), reason: 'runtime file missing' });
else {
  const text = read(runtime);
  if (!text.includes('PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_RUNTIME_RETIRED')) failures.push({ file: rel(runtime), reason: 'runtime retirement marker missing' });
  if (text.includes('GetProviderTypes') || text.includes('CreateConnectionProfileAsync') || text.includes('ImportCsvSnapshotAsync')) failures.push({ file: rel(runtime), reason: 'runtime implementation still present' });
}

const allText = files.map(read).join('\n');

for (const signal of [
  'GetProviderTypes',
  'GetConnectionProfilesAsync',
  'GetConnectionProfileByIdAsync',
  'CreateConnectionProfileAsync',
  'UpdateConnectionProfileAsync',
  'ActivateConnectionProfileAsync',
  'DeactivateConnectionProfileAsync',
  'TestConnectionProfileAsync',
  'DiscoverSchemaAsync',
  'GetDatasetsAsync',
  'CreateDatasetAsync',
  'DiscoverCsvSchemaAsync',
  'PreviewCsvAsync',
  'ImportCsvSnapshotAsync',
  'GetConnectionProfileDtoQuery',
  'GetDatasetDtoQuery',
  'GetFieldDtosAsync',
  'BuildFieldDefinitions',
  'BuildFieldDefinitionDtos',
  'ValidateConnectionProfileRequest',
  'CsvTextParser',
  'CsvParseResult',
  'PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_CSV_TEXT_PARSER_SPLIT'
]) {
  if (!allText.includes(signal)) failures.push({ reason: 'missing signal: ' + signal });
}

if (failures.length) {
  console.error('PPIQ-T028 failed: ConnectorConfigurationService split is incomplete.');
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log('PPIQ-T028 passed: ConnectorConfigurationService runtime mega-file retired and all split files are below 300 lines.');
