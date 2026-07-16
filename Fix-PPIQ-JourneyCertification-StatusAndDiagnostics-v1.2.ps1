[CmdletBinding()]
param(
    [string]$ProjectRoot = "C:\Workspace\PlantProcess-IQ",
    [string]$ConnectionString = "Host=127.0.0.1;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only",
    [string]$SmokeUserName = "e2eadmin",
    [string]$SmokePassword = "E2eAdmin_Local123!",
    [switch]$SkipBackendIntegration,
    [switch]$SkipE2E,
    [switch]$InstallPlaywrightBrowser,
    [switch]$NoRerun,
    [ValidateRange(20, 500)]
    [int]$TailLines = 140
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
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Content
    )

    $directory = Split-Path -Parent $Path
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Protect-DiagnosticText {
    param([AllowNull()][string]$Text)

    if ($null -eq $Text) { return "" }

    $protected = $Text
    $protected = $protected -replace '(?i)(Password|Pwd)\s*=\s*[^;\s"'']+', '$1=***'
    $protected = $protected -replace '(?i)(SmokePassword)\s*[:=]\s*[^\s"'']+', '$1=***'
    $protected = $protected -replace '(?i)(Authorization:\s*Bearer\s+)[A-Za-z0-9\-\._~\+\/]+=*', '$1***'
    $protected = $protected -replace '(?i)(accessToken|refreshToken|token)\s*[=:]\s*["'']?[^,"''\s}]+', '$1=***'
    return $protected
}

function Test-PowerShellSyntax {
    param([Parameter(Mandatory = $true)][string]$Path)

    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile(
        $Path,
        [ref]$tokens,
        [ref]$errors
    )

    if ($errors.Count -gt 0) {
        $message = ($errors | ForEach-Object {
            "Line $($_.Extent.StartLineNumber), column $($_.Extent.StartColumnNumber): $($_.Message)"
        }) -join [Environment]::NewLine
        throw "PowerShell syntax validation failed for $Path`n$message"
    }
}

$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$RunnerPath = Join-Path $ProjectRoot "tools\journey-certification\Invoke-PPIQ-JourneyCertification.ps1"
$ResultRoot = Join-Path $ProjectRoot "Frontend\PlantProcess.Web\test-results\journey-certification"
$LogRoot = Join-Path $ResultRoot "logs"

if (-not (Test-Path -LiteralPath $RunnerPath)) {
    throw "Journey certification runner was not found: $RunnerPath"
}

Write-Section "Repairing command-status collection for Windows PowerShell 5.1"

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupPath = "$RunnerPath.status-list-backup-$stamp"
Copy-Item -LiteralPath $RunnerPath -Destination $backupPath -Force

$source = [System.IO.File]::ReadAllText($RunnerPath)
$original = $source

# Windows PowerShell 5.1 can throw System.ArgumentException: "Argument types do not match"
# when an array subexpression directly wraps a generic List[object]. Use ArrayList plus
# pipeline materialization to guarantee a plain object[] for ConvertTo-Json.
$source = $source.Replace(
    '$script:CommandResults = New-Object System.Collections.Generic.List[object]',
    '$script:CommandResults = New-Object System.Collections.ArrayList'
)
$source = $source.Replace(
    '$script:CommandResults.Add($result)',
    '[void]$script:CommandResults.Add($result)'
)
$source = $source.Replace(
    '$script:CommandResults.Add([pscustomobject]@{',
    '[void]$script:CommandResults.Add([pscustomobject]@{'
)
$source = $source.Replace(
    'commands = @($script:CommandResults)',
    'commands = @($script:CommandResults | ForEach-Object { $_ })'
)

if ($source -eq $original) {
    $alreadyFixed = (
        $source.Contains('New-Object System.Collections.ArrayList') -and
        $source.Contains('commands = @($script:CommandResults | ForEach-Object { $_ })')
    )

    if (-not $alreadyFixed) {
        throw "The expected command-status code was not found. No unsafe best-guess edit was made."
    }

    Write-Host "Runner already contains the Windows PowerShell-safe collection fix." -ForegroundColor Yellow
}
else {
    Write-Utf8NoBom -Path $RunnerPath -Content $source
    Write-Host "PATCHED $RunnerPath" -ForegroundColor Green
    Write-Host "BACKUP  $backupPath" -ForegroundColor DarkGray
}

Test-PowerShellSyntax -Path $RunnerPath

$requiredFragments = @(
    'New-Object System.Collections.ArrayList',
    '[void]$script:CommandResults.Add($result)',
    'commands = @($script:CommandResults | ForEach-Object { $_ })'
)
foreach ($fragment in $requiredFragments) {
    if (-not ([System.IO.File]::ReadAllText($RunnerPath).Contains($fragment))) {
        throw "Runner validation failed. Missing repaired fragment: $fragment"
    }
}

Write-Host "PowerShell syntax and collection-contract validation passed." -ForegroundColor Green

$runnerExitCode = 1
if (-not $NoRerun) {
    Write-Section "Running journey certification and preserving all real failures"

    $runnerParameters = @{
        ProjectRoot = $ProjectRoot
        ConnectionString = $ConnectionString
        SmokeUserName = $SmokeUserName
        SmokePassword = $SmokePassword
    }

    if ($SkipBackendIntegration) { $runnerParameters["SkipBackendIntegration"] = $true }
    if ($SkipE2E) { $runnerParameters["SkipE2E"] = $true }
    if ($InstallPlaywrightBrowser) { $runnerParameters["InstallPlaywrightBrowser"] = $true }

    & $RunnerPath @runnerParameters
    $runnerExitCode = $LASTEXITCODE
    if ($null -eq $runnerExitCode) { $runnerExitCode = 0 }
}
else {
    Write-Host "NoRerun selected. Existing evidence will be diagnosed." -ForegroundColor Yellow
}

Write-Section "Certification decision and redacted failure diagnostics"

$summary = New-Object System.Collections.ArrayList
[void]$summary.Add("PPIQ Journey Certification Diagnostic Summary")
[void]$summary.Add("Generated: $((Get-Date).ToUniversalTime().ToString('o'))")
[void]$summary.Add("Project: $ProjectRoot")
[void]$summary.Add("Runner exit code: $runnerExitCode")
[void]$summary.Add("")

$scorePath = Join-Path $ResultRoot "journey-score.md"
if (Test-Path -LiteralPath $scorePath) {
    $scoreText = Get-Content -LiteralPath $scorePath -Raw
    $scoreText = Protect-DiagnosticText -Text $scoreText
    Write-Host $scoreText
    [void]$summary.Add("===== JOURNEY SCORE =====")
    [void]$summary.Add($scoreText)
    [void]$summary.Add("")
}
else {
    $message = "journey-score.md is not present yet. The failed command logs below remain authoritative."
    Write-Host $message -ForegroundColor Yellow
    [void]$summary.Add($message)
    [void]$summary.Add("")
}

$failedIds = New-Object System.Collections.ArrayList
$statusPath = Join-Path $ResultRoot "command-status.json"
if (Test-Path -LiteralPath $statusPath) {
    try {
        $status = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
        foreach ($command in @($status.commands)) {
            if ($command.status -ne "PASS") {
                [void]$failedIds.Add([string]$command.id)
            }
        }
    }
    catch {
        Write-Host "Could not parse command-status.json: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

if ($failedIds.Count -eq 0) {
    foreach ($knownId in @("backend-integration", "frontend-unit", "journey-e2e", "journey-score")) {
        $knownLog = Join-Path $LogRoot ($knownId + ".log")
        if (Test-Path -LiteralPath $knownLog) {
            $content = Get-Content -LiteralPath $knownLog -Raw
            if ($content -match '(?im)\bFAIL(?:ED)?\b|\berror\b|exception|Tests failed') {
                [void]$failedIds.Add($knownId)
            }
        }
    }
}

$failedIds = @($failedIds | Select-Object -Unique)
if ($failedIds.Count -eq 0) {
    Write-Host "No failed command was found in command-status.json." -ForegroundColor Green
    [void]$summary.Add("No failed command was found in command-status.json.")
}
else {
    foreach ($id in $failedIds) {
        $logPath = Join-Path $LogRoot ($id + ".log")
        Write-Host ""
        Write-Host ("----- {0} -----" -f $id) -ForegroundColor Yellow
        [void]$summary.Add("===== $id =====")

        if (-not (Test-Path -LiteralPath $logPath)) {
            $missing = "Log not found: $logPath"
            Write-Host $missing -ForegroundColor Red
            [void]$summary.Add($missing)
            [void]$summary.Add("")
            continue
        }

        $tail = (Get-Content -LiteralPath $logPath -Tail $TailLines) -join [Environment]::NewLine
        $tail = Protect-DiagnosticText -Text $tail
        Write-Host $tail
        [void]$summary.Add($tail)
        [void]$summary.Add("")
    }
}

$diagnosticPath = Join-Path $ResultRoot ("diagnostic-summary-{0}.txt" -f $stamp)
Write-Utf8NoBom -Path $diagnosticPath -Content ($summary -join [Environment]::NewLine)

Write-Host ""
Write-Host "Redacted diagnostic summary: $diagnosticPath" -ForegroundColor Cyan
Write-Host "Runner backup: $backupPath" -ForegroundColor DarkGray

if ($runnerExitCode -eq 0 -and $failedIds.Count -eq 0) {
    Write-Host "AUTOMATED JOURNEY CERTIFICATION RUNNER COMPLETED." -ForegroundColor Green
    exit 0
}

Write-Host "The runner infrastructure is repaired. Remaining failures are real test/application findings and are preserved above." -ForegroundColor Yellow
exit 1
