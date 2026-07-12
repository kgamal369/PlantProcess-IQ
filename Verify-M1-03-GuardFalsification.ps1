#requires -Version 5.1
<#
  Verify-M1-03-GuardFalsification.ps1
  -----------------------------------
  Proves the M1-03 guard is REAL, not a tautology: temporarily comment out the AddAssistant
  registration in Program.cs, run the AssistantServiceGraphRegistration guard test, and confirm
  it goes RED (the guard detects the missing registration even after StripComments()). Then it
  ALWAYS restores Program.cs (try/finally) and re-runs the test to confirm GREEN again.

  Safety: refuses if the API is running (a build lock would corrupt the result), backs up
  Program.cs before touching it, and restores it no matter what happens.

  Run from the repository root.
  Launch (immune to execution policy / mark-of-the-web):
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify-M1-03-GuardFalsification.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
function Section($t){ Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }
function Info($t){ Write-Host "     $t" -ForegroundColor Gray }
function Bad($t){ Write-Host "FAIL: $t" -ForegroundColor Red }
function Good($t){ Write-Host "PASS: $t" -ForegroundColor Green }

if (-not (Test-Path 'Backend' -PathType Container)) { Bad "Run from the repository root."; exit 1 }

# a running API locks build output and would break the test build
$api = Get-Process -Name 'PlantProcess.Api' -ErrorAction SilentlyContinue
if ($api) {
    $ids = ($api | ForEach-Object { $_.Id }) -join ', '
    Bad "PlantProcess.Api is running (PID $ids). Stop it first:  Stop-Process -Id $ids -Force   then re-run."
    exit 1
}

$F = 'Backend\PlantProcess.Api\Program.cs'
if (-not (Test-Path $F -PathType Leaf)) { Bad "MISSING $F"; exit 1 }

# locate the (single, uncommented) AddAssistant registration statement
$lines = [System.IO.File]::ReadAllLines((Resolve-Path $F))
$hits = @()
for ($i=0; $i -lt $lines.Count; $i++) {
    $t = $lines[$i].TrimStart()
    if ($lines[$i] -match 'AddAssistant\s*\(' -and -not $t.StartsWith('//')) { $hits += $i }
}
if ($hits.Count -ne 1) { Bad "expected exactly 1 uncommented AddAssistant(...) call in Program.cs, found $($hits.Count). Not touching the file."; exit 1 }
$idx = $hits[0]
$orig = $lines[$idx]
Info "registration line $($idx+1): $($orig.Trim())"

# discover the guard test's project (fallback: solution)
$testCs = Get-ChildItem -Path 'Backend' -Recurse -Filter 'AssistantServiceGraphRegistration*.cs' -File -ErrorAction SilentlyContinue | Select-Object -First 1
$target = $null
if ($testCs) {
    $dir = $testCs.Directory
    while ($dir -and -not (Get-ChildItem $dir.FullName -Filter *.csproj -File -ErrorAction SilentlyContinue)) { $dir = $dir.Parent }
    if ($dir) { $target = (Get-ChildItem $dir.FullName -Filter *.csproj -File | Select-Object -First 1).FullName }
}
if (-not $target) { $target = (Get-ChildItem . -Filter *.sln -File | Select-Object -First 1).FullName }
if (-not $target) { Bad "could not find the test project or a .sln to run the guard test."; exit 1 }
Info "test target: $target"
$filter = 'FullyQualifiedName~AssistantServiceGraphRegistration'

# backup
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = Join-Path 'deploy\.ppiq-backups' "m1-03-falsification-$stamp\$F"
New-Item -ItemType Directory -Force -Path (Split-Path $backup) | Out-Null
Copy-Item -LiteralPath $F -Destination $backup -Force
Info "backup: $backup"

$redAsExpected = $false
try {
    Section "1. Comment out the registration (falsify)"
    $lines[$idx] = '// [M1-03 falsification - temporary] ' + $orig.TrimStart()
    [System.IO.File]::WriteAllLines((Join-Path (Get-Location) $F), $lines, $Utf8NoBom)
    Info "commented. running guard test (expect RED)..."

    & dotnet test $target --filter $filter --nologo | Write-Host
    $code = $LASTEXITCODE
    if ($code -ne 0) { $redAsExpected = $true; Good "guard test FAILED with the registration commented -> the guard is real (RED as expected)." }
    else { Bad "guard test PASSED even with the registration commented -> the guard does NOT detect absence. This is a real problem to file." }
}
finally {
    Section "2. Restore Program.cs (always)"
    Copy-Item -LiteralPath $backup -Destination $F -Force
    $after = [System.IO.File]::ReadAllText((Resolve-Path $F))
    if ($after -match 'AddAssistant\s*\(' -and $after -notmatch 'M1-03 falsification') { Good "Program.cs restored (registration present, marker gone)." }
    else { Bad "restore check unexpected - compare against backup: $backup" }
}

Section "3. Re-run the guard test (expect GREEN)"
& dotnet test $target --filter $filter --nologo | Write-Host
if ($LASTEXITCODE -eq 0) { Good "guard test GREEN again after restore." } else { Bad "guard test still RED after restore - inspect; restore from $backup if needed." }

Section "Verdict"
if ($redAsExpected) { Good "M1-03 falsification complete: guard goes RED when the registration is removed and GREEN when restored." }
else { Bad "M1-03 falsification did NOT behave as expected - see above." }
