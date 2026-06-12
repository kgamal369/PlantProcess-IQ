param(
    [string]$RepoRoot = "C:\Workspace\PlantProcess-IQ",
    [switch]$RunTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Off

Set-Location $RepoRoot

$InvokeScript = Join-Path $RepoRoot "tools\golive\t03\Invoke-T03-DotnetTestGreenMeaningful.ps1"

if (-not (Test-Path $InvokeScript)) {
    throw "Missing T03 invoke script: $InvokeScript"
}

$scriptText = Get-Content -Raw -Path $InvokeScript

foreach ($token in @(
    "PPIQ_T03_DOTNET_TEST_GREEN_MEANINGFUL",
    "Get-TestProjects",
    "Test-NoSkippedTestsInSource",
    "Test-NoFilteredDotnetTestInCi",
    "Parse-Trx",
    "--logger",
    "trx;LogFileName",
    "noFilterUsed"
)) {
    if ($scriptText -notmatch [regex]::Escape($token)) {
        throw "T03 invoke script missing required token: $token"
    }
}

if ($scriptText -match 'dotnet\s+test[^\r\n]*(--filter|TestCaseFilter|VSTestTestCaseFilter)') {
    throw "T03 invoke script must not run filtered dotnet test."
}

powershell -NoProfile -ExecutionPolicy Bypass -File $InvokeScript -RepoRoot $RepoRoot -StaticOnly

if ($LASTEXITCODE -ne 0) {
    throw "T03 static-only gate failed."
}

if ($RunTests) {
    powershell -NoProfile -ExecutionPolicy Bypass -File $InvokeScript -RepoRoot $RepoRoot

    if ($LASTEXITCODE -ne 0) {
        throw "T03 full dotnet test gate failed."
    }
}

Write-Host "[GREEN] T03 validator passed." -ForegroundColor Green