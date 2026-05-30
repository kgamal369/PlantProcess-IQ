Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Get-Location
$self = $MyInvocation.MyCommand.Path

$patterns = @(
    "ChangeMe123!",
    "E2EAdmin123!",
    "admin / ChangeMe123",
    "plantprocess123",
    "CHANGE_ME_STRONG_APP_PASSWORD",
    "REPLACE_WITH_REAL_CADDY_BCRYPT_HASH"
)

$regex = ($patterns | ForEach-Object { [regex]::Escape($_) }) -join "|"

$matches =
    Get-ChildItem -Path $repoRoot -Recurse -File |
    Where-Object {
        $_.FullName -ne $self -and
        $_.FullName -notmatch "\\node_modules\\|\\dist\\|\\build\\|\\bin\\|\\obj\\|\\.git\\|\\coverage\\|\\playwright-report\\|\\test-results\\" -and
        $_.FullName -notmatch "\\Documentation\\|\\GeminiExport_|\\UltimateAudit_" -and
        $_.FullName -notmatch "\\tools\\phase1\\" -and
        $_.Name -notmatch "\.bak$|\.phase1\.bak$|\.phase1-credentials\.bak$|\.phase1-final-secret-cleanup\.bak$|\.before-syntax-fix\.bak$|\.broken\.bak$"
    } |
    Select-String -Pattern $regex

if ($matches) {
    $matches
    throw "PPIQ-T008 failed: default credentials or seeded secret placeholders remain in active source/config."
}

Write-Host "PPIQ-T008 passed: no default credentials or seeded secret placeholders found in active source/config."