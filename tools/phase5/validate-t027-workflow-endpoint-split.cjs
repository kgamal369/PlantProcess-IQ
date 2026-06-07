const fs = require('fs');
const path = require('path');

const root = process.cwd();
const failures = [];

function rel(file) { return path.relative(root, file).split(path.sep).join('/'); }
function read(file) { return fs.readFileSync(file, 'utf8'); }
function lineCount(text) { return text.replace(/\r\n/g, '\n').split('\n').length; }

function collectFiles(dir, className) {
  return fs.readdirSync(path.join(root, dir))
    .filter(function(name) { return name.startsWith(className) && name.endsWith('.cs'); })
    .map(function(name) { return path.join(root, dir, name); });
}

function checkClass(config) {
  const files = collectFiles(config.dir, config.className);

  if (files.length < config.minFiles) {
    failures.push({ className: config.className, reason: 'not enough split files', actual: files.length, expectedAtLeast: config.minFiles });
  }

  for (const file of files) {
    const text = read(file);
    const lines = lineCount(text);
    if (lines > 300) failures.push({ file: rel(file), reason: 'file exceeds 300 lines', lines });
  }

  const route = path.join(root, config.routePath);
  const runtime = path.join(root, config.runtimePath);

  if (!fs.existsSync(route)) failures.push({ file: config.routePath, reason: 'route file missing' });
  else if (!read(route).includes(config.routeMarker)) failures.push({ file: config.routePath, reason: 'route split marker missing' });

  if (!fs.existsSync(runtime)) failures.push({ file: config.runtimePath, reason: 'runtime file missing' });
  else {
    const runtimeText = read(runtime);
    if (!runtimeText.includes(config.runtimeMarker)) failures.push({ file: config.runtimePath, reason: 'runtime retirement marker missing' });
    if (runtimeText.includes('GetConnectorTruthAsync') || runtimeText.includes('GetWorkflowOverview')) {
      failures.push({ file: config.runtimePath, reason: 'runtime implementation still present' });
    }
  }

  const allText = files.map(read).join('\n');

  if (config.requiredMarker && !allText.includes(config.requiredMarker)) {
    failures.push({ className: config.className, reason: 'missing marker: ' + config.requiredMarker });
  }

  for (const signal of config.signals) {
    if (!allText.includes(signal)) failures.push({ className: config.className, reason: 'missing signal: ' + signal });
  }
}

checkClass({
  className: 'Phase1WorkflowTruthEndpoints',
  dir: 'Backend/PlantProcess.Api/Endpoints/Admin',
  routePath: 'Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs',
  runtimePath: 'Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.runtime.cs',
  routeMarker: 'PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_ROUTE_SPLIT',
  runtimeMarker: 'PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_RUNTIME_RETIRED',
  requiredMarker: 'PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_TOP_LEVEL_CONTRACTS',
  minFiles: 12,
  signals: [
    'MapPhase1WorkflowTruthEndpoints',
    'GetConnectorTruthAsync',
    'GetConnectorCertificationAsync',
    'GetSourceScheduleBoardAsync',
    'RunDueSourceImportsAsync',
    'GetStagingSummaryAsync',
    'GetSchemaMappingWorkbenchAsync',
    'GetImportJobConfigurationBoardAsync',
    'RunDueSourceImportsRequest',
    'UpdateDatasetCursorRequest',
    'PreviewSchemaViewRequest',
    'CreateImportJobFromMappingRequest',
    'ConnectorProviderTruthRow',
    'ConnectorCertificationRow',
    'CanonicalTargetRow',
    'SchemaViewPreviewColumn'
  ]
});

checkClass({
  className: 'WorkflowEndpoints',
  dir: 'Backend/PlantProcess.Api/Endpoints/Workflow',
  routePath: 'Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs',
  runtimePath: 'Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.runtime.cs',
  routeMarker: 'PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINTS_ROUTE_SPLIT',
  runtimeMarker: 'PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINTS_RUNTIME_RETIRED',
  requiredMarker: null,
  minFiles: 10,
  signals: [
    'MapWorkflowEndpoints',
    'GetWorkflowOverview',
    'GetWorkflowStatusAsync',
    'RegisterSourceSystemAsync',
    'CreateImportBatchAsync',
    'CreateMaterialAsync',
    'InvestigateMaterialAsync'
  ]
});

if (failures.length) {
  console.error('PPIQ-T027 failed: workflow endpoint split is incomplete.');
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log('PPIQ-T027 passed: workflow runtime mega-files retired, top-level Phase1 contracts restored, and all split files are below 300 lines.');
