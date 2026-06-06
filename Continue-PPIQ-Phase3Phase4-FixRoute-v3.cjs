#!/usr/bin/env node
'use strict';

const fs = require('fs');
const path = require('path');

const projectRoot = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, '').slice(0, 14);
const backupRoot = path.join(projectRoot, '.phase3_phase4_backup', `route_resume_v3_${timestamp}`);

function log(message) {
  process.stdout.write(`${message}\n`);
}

function rel(...parts) {
  return path.join(projectRoot, ...parts);
}

function ensureDir(dir) {
  if (!dir) return;
  fs.mkdirSync(dir, { recursive: true });
}

function readText(file) {
  return fs.readFileSync(file, 'utf8');
}

function writeText(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\r?\n/g, '\r\n'), { encoding: 'utf8' });
  log(`Wrote: ${path.relative(projectRoot, file)}`);
}

function backupFile(file) {
  if (!fs.existsSync(file)) return;
  const relative = path.relative(projectRoot, file);
  const backup = path.join(backupRoot, relative);
  ensureDir(path.dirname(backup));
  fs.copyFileSync(file, backup);
}

function assertFileContains(file, marker) {
  if (!fs.existsSync(file)) {
    throw new Error(`Missing required file: ${path.relative(projectRoot, file)}`);
  }
  const content = readText(file);
  if (!content.includes(marker)) {
    throw new Error(`File ${path.relative(projectRoot, file)} does not contain required marker: ${marker}`);
  }
}

function patchProgramRegistration() {
  const file = rel('Backend', 'PlantProcess.Api', 'Program.cs');
  if (!fs.existsSync(file)) throw new Error('Missing Backend/PlantProcess.Api/Program.cs');
  backupFile(file);
  let text = readText(file);
  const original = text;

  if (!text.includes('PlantProcess.Api.Endpoints.MappingHealth')) {
    const usingLine = 'using PlantProcess.Api.Endpoints.MappingHealth;\r\n';
    text = usingLine + text;
    log('Inserted MappingHealth using in Program.cs.');
  }

  if (!text.includes('MapPhase34MappingHealthEndpoints')) {
    if (!text.includes('app.Run();')) {
      throw new Error('Could not find app.Run(); in Program.cs.');
    }
    text = text.replace('app.Run();', 'app.MapPhase34MappingHealthEndpoints();\r\n\r\napp.Run();');
    log('Registered MapPhase34MappingHealthEndpoints before app.Run().');
  }

  if (text !== original) writeText(file, text);
  else log('Program.cs registration already present.');
}

function patchAppImplementationRoute() {
  const file = rel('Frontend', 'PlantProcess.Web', 'src', 'App.implementation.tsx');
  if (!fs.existsSync(file)) throw new Error('Missing Frontend/PlantProcess.Web/src/App.implementation.tsx');
  backupFile(file);
  let text = readText(file);
  const original = text;

  const lazyBlock = [
    'const MappingHealthPage = lazy(() =>',
    '  import("./pages/MappingHealth/MappingHealthPage").then((module) => ({',
    '    default: module.MappingHealthPage,',
    '  }))',
    ');',
    ''
  ].join('\r\n');

  if (!text.includes('MappingHealthPage')) {
    const markers = [
      'function withPageBoundary',
      'function AppRoutes',
      'export default function App'
    ];
    let inserted = false;
    for (const marker of markers) {
      const idx = text.indexOf(marker);
      if (idx >= 0) {
        text = text.slice(0, idx) + lazyBlock + text.slice(idx);
        inserted = true;
        log(`Inserted MappingHealthPage lazy loader before ${marker}.`);
        break;
      }
    }
    if (!inserted) throw new Error('Could not find safe insertion point for MappingHealthPage lazy loader.');
  } else {
    log('MappingHealthPage reference already present.');
  }

  const routeBlock = [
    '                    {/* Mapping health + schema drift */}',
    '                    <Route',
    '                      path="/mapping-health"',
    '                      element={withPageBoundary(',
    '                        "/mapping-health",',
    '                        "Mapping health view is refreshing",',
    '                        <MappingHealthPage />',
    '                      )}',
    '                    />',
    ''
  ].join('\r\n');

  if (!text.includes('path="/mapping-health"')) {
    const routePatterns = [
      /\s*<Route\s*\r?\n\s*path="\/analytics-widgets"/m,
      /\s*<Route\s*\r?\n\s*path="\/demo-lifecycle"/m,
      /\s*<Route\s*\r?\n\s*path="\/admin-preview"/m,
      /\s*<Route\s*\r?\n\s*path="\/commercial\/license"/m,
      /\s*<Route\s+path="\/dashboard"/m,
      /\s*<\/Routes>/m
    ];
    let inserted = false;
    for (const pattern of routePatterns) {
      const match = text.match(pattern);
      if (match && typeof match.index === 'number') {
        text = text.slice(0, match.index) + routeBlock + text.slice(match.index);
        inserted = true;
        log(`Inserted /mapping-health route using pattern ${pattern}.`);
        break;
      }
    }
    if (!inserted) throw new Error('Could not find a safe route insertion point in App.implementation.tsx.');
  } else {
    log('/mapping-health route already present.');
  }

  if (text !== original) writeText(file, text);
  else log('App.implementation.tsx already contains route and lazy loader.');
}

function writeValidationScript() {
  const file = rel('tools', 'phase3-phase4', 'Invoke-Phase3Phase4Validation.ps1');
  backupFile(file);
  const content = String.raw`param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunBuild,
    [switch]$RunFrontendBuild,
    [switch]$ApplySql,
    [string]$PostgresConnectionString = $env:PPIQ_POSTGRES_CONNECTION
)

$ErrorActionPreference = "Stop"

function Run-Step([string]$Name, [scriptblock]$Block) {
    Write-Host ""
    Write-Host "---- $Name" -ForegroundColor Cyan
    & $Block
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
}

function Assert-FileContains([string]$Path, [string]$Needle) {
    if (-not (Test-Path $Path)) { throw "Missing required file: $Path" }
    $content = [System.IO.File]::ReadAllText($Path)
    if ($content -notlike "*$Needle*") { throw "File $Path does not contain required marker: $Needle" }
}

Push-Location $ProjectRoot
try {
    Run-Step "Phase 1/2 BOM gate" {
        powershell -ExecutionPolicy Bypass -File .\tools\phase1-phase2\Test-Utf8NoBom.ps1 -ProjectRoot $ProjectRoot
    }

    Run-Step "Phase 1/2 production-runtime secret scan" {
        powershell -ExecutionPolicy Bypass -File .\tools\phase1-phase2\Invoke-SecretScan.ps1 -ProjectRoot $ProjectRoot
    }

    Run-Step "Phase 3/4 source markers" {
        Assert-FileContains ".\Backend\tests\PlantProcess.Application.UnitTests\Phase3Phase4\Phase3Phase4CertificationTests.cs" "Phase3Phase4CertificationTests"
        Assert-FileContains ".\Backend\database\scripts\430_phase3_phase4_certification_mapping_health.sql" "ppiq_detect_schema_drift"
        Assert-FileContains ".\Backend\PlantProcess.Api\Endpoints\MappingHealth\Phase34MappingHealthEndpoints.cs" "MapPhase34MappingHealthEndpoints"
        Assert-FileContains ".\Backend\PlantProcess.Api\Program.cs" "MapPhase34MappingHealthEndpoints"
        Assert-FileContains ".\Frontend\PlantProcess.Web\src\pages\MappingHealth\MappingHealthPage.tsx" "MappingHealthPage"
        Assert-FileContains ".\Frontend\PlantProcess.Web\src\App.implementation.tsx" "MappingHealthPage"
        Assert-FileContains ".\Frontend\PlantProcess.Web\src\App.implementation.tsx" 'path="/mapping-health"'
        Write-Host "Phase 3/4 source markers passed." -ForegroundColor Green
    }

    if ($ApplySql) {
        if ([string]::IsNullOrWhiteSpace($PostgresConnectionString)) { throw "ApplySql was requested but PPIQ_POSTGRES_CONNECTION/PostgresConnectionString is empty." }
        $psql = Get-Command psql -ErrorAction SilentlyContinue
        if (-not $psql) { throw "ApplySql was requested but psql was not found in PATH." }
        Run-Step "Apply Phase 3/4 SQL" {
            psql $PostgresConnectionString -v ON_ERROR_STOP=1 -f ".\Backend\database\scripts\430_phase3_phase4_certification_mapping_health.sql"
        }
        Run-Step "Verify Phase 3/4 SQL certification function" {
            psql $PostgresConnectionString -v ON_ERROR_STOP=1 -c "SELECT * FROM public.ppiq_phase34_certification_status();"
        }
    } else {
        Write-Host ""
        Write-Host "SQL apply skipped. This is intentional until local/server DB target is selected." -ForegroundColor DarkYellow
    }

    if ($RunBuild) {
        Push-Location ".\Backend"
        try {
            Run-Step "dotnet restore" { dotnet restore }
            Run-Step "dotnet build" { dotnet build --no-restore }
            Run-Step "dotnet test Phase3Phase4CertificationTests" { dotnet test ".\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj" --no-build --filter "FullyQualifiedName~Phase3Phase4CertificationTests" }
        } finally { Pop-Location }
    }

    if ($RunFrontendBuild) {
        Push-Location ".\Frontend\PlantProcess.Web"
        try {
            Run-Step "npm install/check" { if (Test-Path "package-lock.json") { npm ci } else { npm install } }
            Run-Step "npm run build" { npm run build }
        } finally { Pop-Location }
    }

    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Host "Phase 3 + Phase 4 validation completed successfully." -ForegroundColor Green
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
} finally {
    Pop-Location
}
`;
  writeText(file, content);
}

function writeEvidenceDoc() {
  const file = rel('docs', 'phase3-phase4', 'PHASE3_PHASE4_IMPLEMENTATION_EVIDENCE.md');
  backupFile(file);
  const lines = [
    '# PlantProcess IQ - Phase 3 + Phase 4 Implementation Evidence',
    '',
    `Generated: ${new Date().toISOString()}`,
    '',
    '## Implemented scope',
    '',
    '- Phase 3/4 backend certification tests are installed under Backend/tests/PlantProcess.Application.UnitTests/Phase3Phase4.',
    '- Phase 4 schema snapshot, schema drift, mapping-health and population-evidence SQL foundation is installed as 430_phase3_phase4_certification_mapping_health.sql.',
    '- Backend read-only mapping-health endpoints are installed under /mapping-health/status and /mapping-health/summary.',
    '- Frontend mapping-health page is installed under /mapping-health in App.implementation.tsx.',
    '- Validation script is installed at tools/phase3-phase4/Invoke-Phase3Phase4Validation.ps1.',
    '',
    '## Scope guard',
    '',
    'This implementation keeps PlantProcess IQ generic. Mapping health and drift detection are generic manufacturing metadata capabilities. Steel examples remain demo fixtures, not hard-coded product concepts.',
    '',
    '## Validation commands',
    '',
    'Safe marker validation:',
    '',
    'powershell -ExecutionPolicy Bypass -File .\\tools\\phase3-phase4\\Invoke-Phase3Phase4Validation.ps1 -ProjectRoot "C:\\Workspace\\PlantProcess-IQ"',
    '',
    'Compile validation:',
    '',
    'powershell -ExecutionPolicy Bypass -File .\\tools\\phase3-phase4\\Invoke-Phase3Phase4Validation.ps1 -ProjectRoot "C:\\Workspace\\PlantProcess-IQ" -RunBuild -RunFrontendBuild',
    ''
  ];
  writeText(file, lines.join('\n'));
}

function runNodeMarkerValidation() {
  const required = [
    [rel('Backend', 'tests', 'PlantProcess.Application.UnitTests', 'Phase3Phase4', 'Phase3Phase4CertificationTests.cs'), 'Phase3Phase4CertificationTests'],
    [rel('Backend', 'database', 'scripts', '430_phase3_phase4_certification_mapping_health.sql'), 'ppiq_detect_schema_drift'],
    [rel('Backend', 'PlantProcess.Api', 'Endpoints', 'MappingHealth', 'Phase34MappingHealthEndpoints.cs'), 'MapPhase34MappingHealthEndpoints'],
    [rel('Backend', 'PlantProcess.Api', 'Program.cs'), 'MapPhase34MappingHealthEndpoints'],
    [rel('Frontend', 'PlantProcess.Web', 'src', 'pages', 'MappingHealth', 'MappingHealthPage.tsx'), 'MappingHealthPage'],
    [rel('Frontend', 'PlantProcess.Web', 'src', 'App.implementation.tsx'), 'MappingHealthPage'],
    [rel('Frontend', 'PlantProcess.Web', 'src', 'App.implementation.tsx'), 'path="/mapping-health"'],
    [rel('tools', 'phase3-phase4', 'Invoke-Phase3Phase4Validation.ps1'), 'Phase 3/4 source markers'],
    [rel('docs', 'phase3-phase4', 'PHASE3_PHASE4_IMPLEMENTATION_EVIDENCE.md'), 'Phase 3 + Phase 4 Implementation Evidence']
  ];

  for (const [file, marker] of required) assertFileContains(file, marker);
  log('Node marker validation passed.');
}

function main() {
  ensureDir(backupRoot);
  log('=================================================================================================');
  log('PlantProcess IQ Phase 3/4 route resume patch v3');
  log('=================================================================================================');
  log(`Project root : ${projectRoot}`);
  log(`Backup folder: ${backupRoot}`);

  const requiredBeforePatch = [
    [rel('Backend', 'tests', 'PlantProcess.Application.UnitTests', 'Phase3Phase4', 'Phase3Phase4CertificationTests.cs'), 'Phase3Phase4CertificationTests'],
    [rel('Backend', 'database', 'scripts', '430_phase3_phase4_certification_mapping_health.sql'), 'ppiq_detect_schema_drift'],
    [rel('Backend', 'PlantProcess.Api', 'Endpoints', 'MappingHealth', 'Phase34MappingHealthEndpoints.cs'), 'MapPhase34MappingHealthEndpoints'],
    [rel('Frontend', 'PlantProcess.Web', 'src', 'pages', 'MappingHealth', 'MappingHealthPage.tsx'), 'MappingHealthPage']
  ];
  for (const [file, marker] of requiredBeforePatch) assertFileContains(file, marker);

  patchProgramRegistration();
  patchAppImplementationRoute();
  writeValidationScript();
  writeEvidenceDoc();
  runNodeMarkerValidation();

  log('=================================================================================================');
  log('Phase 3/4 route resume patch v3 completed successfully.');
  log('Next command: powershell -ExecutionPolicy Bypass -File .\\tools\\phase3-phase4\\Invoke-Phase3Phase4Validation.ps1 -ProjectRoot "C:\\Workspace\\PlantProcess-IQ"');
  log('=================================================================================================');
}

try {
  main();
} catch (error) {
  console.error('=================================================================================================');
  console.error('Phase 3/4 route resume patch v3 failed.');
  console.error(error && error.stack ? error.stack : String(error));
  console.error(`Backup folder: ${backupRoot}`);
  console.error('=================================================================================================');
  process.exit(1);
}
