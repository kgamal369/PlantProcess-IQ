[CmdletBinding()]
param(
    [string]$ProjectRoot = "C:\Workspace\PlantProcess-IQ",
    [switch]$PatchOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Write-Section {
    param([Parameter(Mandatory = $true)][string]$Title)
    Write-Host ""
    Write-Host ("=" * 104) -ForegroundColor DarkGray
    Write-Host $Title -ForegroundColor Cyan
    Write-Host ("=" * 104) -ForegroundColor DarkGray
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, ($Content -replace "`r?`n", "`r`n"), $utf8)
}

function Assert-PowerShellSyntax {
    param([Parameter(Mandatory = $true)][string]$Path)

    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile(
        $Path,
        [ref]$tokens,
        [ref]$errors
    )

    if ($errors.Count -gt 0) {
        $details = ($errors | ForEach-Object {
            "Line $($_.Extent.StartLineNumber): $($_.Message)"
        }) -join [Environment]::NewLine

        throw "PowerShell syntax validation failed for '$Path':`n$details"
    }
}

if (-not (Test-Path -LiteralPath $ProjectRoot)) {
    throw "Project root not found: $ProjectRoot"
}
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path

$applyScript = Join-Path $ProjectRoot "Apply-PPIQ-JourneyCertification-v1.ps1"
$runnerScript = Join-Path $ProjectRoot "tools\journey-certification\Invoke-PPIQ-JourneyCertification.ps1"

if (-not (Test-Path -LiteralPath $applyScript)) {
    throw "Apply script not found: $applyScript"
}
if (-not (Test-Path -LiteralPath $runnerScript)) {
    throw "Certification runner not found: $runnerScript. The implementation pack may not have completed its file-write phase."
}

Write-Section "Repairing PowerShell runner parameter binding"

$original = [System.IO.File]::ReadAllText($applyScript)

$brokenBlock = @'
$runnerArgs = @(
    "-ProjectRoot", $ProjectRoot,
    "-ConnectionString", $ConnectionString,
    "-SmokeUserName", $SmokeUserName,
    "-SmokePassword", $SmokePassword
)
if ($SkipBackendIntegration) { $runnerArgs += "-SkipBackendIntegration" }
if ($SkipE2E) { $runnerArgs += "-SkipE2E" }
if ($InstallPlaywrightBrowser) { $runnerArgs += "-InstallPlaywrightBrowser" }

& $runner @runnerArgs
'@

$fixedBlock = @'
$runnerParameters = @{
    ProjectRoot = $ProjectRoot
    ConnectionString = $ConnectionString
    SmokeUserName = $SmokeUserName
    SmokePassword = $SmokePassword
}
if ($SkipBackendIntegration) { $runnerParameters["SkipBackendIntegration"] = $true }
if ($SkipE2E) { $runnerParameters["SkipE2E"] = $true }
if ($InstallPlaywrightBrowser) { $runnerParameters["InstallPlaywrightBrowser"] = $true }

& $runner @runnerParameters
'@

$alreadyFixedMarker = '$runnerParameters = @{'

if ($original.Contains($brokenBlock)) {
    $backupPath = "$applyScript.runner-binding-backup-$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Copy-Item -LiteralPath $applyScript -Destination $backupPath -Force

    $patched = $original.Replace($brokenBlock, $fixedBlock)
    Write-Utf8NoBom -Path $applyScript -Content $patched

    Write-Host "PATCHED $applyScript" -ForegroundColor Green
    Write-Host "BACKUP  $backupPath" -ForegroundColor DarkGray
}
elseif ($original.Contains($alreadyFixedMarker)) {
    Write-Host "The apply script is already patched. No source change required." -ForegroundColor Green
}
else {
    throw "The expected runner invocation block was not found. The file has drifted, so no blind replacement was performed."
}

Write-Section "Validating repaired scripts"
Assert-PowerShellSyntax -Path $applyScript
Assert-PowerShellSyntax -Path $runnerScript

$runnerCommand = Get-Command -Name $runnerScript -ErrorAction Stop
$requiredParameters = @(
    "ProjectRoot",
    "ConnectionString",
    "SmokeUserName",
    "SmokePassword",
    "SkipBackendIntegration",
    "SkipE2E",
    "InstallPlaywrightBrowser"
)

foreach ($parameterName in $requiredParameters) {
    if (-not $runnerCommand.Parameters.ContainsKey($parameterName)) {
        throw "Runner parameter '$parameterName' is missing from $runnerScript"
    }
}

Write-Host "PowerShell syntax and runner parameter contract passed." -ForegroundColor Green

if ($PatchOnly) {
    Write-Host "Patch-only mode completed. Certification was not started." -ForegroundColor Yellow
    return
}

Write-Section "Running the already-applied journey certification"
Write-Host "No implementation files will be rewritten. This resumes only the validation runner." -ForegroundColor Cyan

$windowsPowerShell = (Get-Command powershell.exe -ErrorAction Stop).Source
$childArguments = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $runnerScript,
    "-ProjectRoot", $ProjectRoot
)

& $windowsPowerShell @childArguments
$runnerExitCode = $LASTEXITCODE

Write-Host ""
if ($runnerExitCode -eq 0) {
    Write-Host "AUTOMATED JOURNEY CERTIFIED." -ForegroundColor Green
}
else {
    Write-Host "The binding defect is fixed, but one or more real certification gates did not pass." -ForegroundColor Yellow
    Write-Host "Review: Frontend\PlantProcess.Web\test-results\journey-certification\journey-score.md" -ForegroundColor Yellow
    Write-Host "Runner exit code: $runnerExitCode" -ForegroundColor Yellow
}

exit $runnerExitCode
