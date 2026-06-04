$ErrorActionPreference = "Stop"
$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$ConfigDocRoot = "$RepoRoot\Documentation\config"
$Latest = Get-ChildItem "$ConfigDocRoot\Pack2B_TrackedSecretSeparation_*.csv" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $Latest) { throw "No Pack 2B tracked secret separation CSV found." }
$Rows = Import-Csv $Latest.FullName
$Blockers = @($Rows | Where-Object { $_.FinalRisk -eq "BLOCKER_TRACKED_HIGH" }).Count
$TrackedMedium = @($Rows | Where-Object { $_.FinalRisk -eq "WARN_TRACKED_MEDIUM" }).Count
$AcceptableLocal = @($Rows | Where-Object { $_.FinalRisk -eq "ACCEPTABLE_LOCAL_IGNORED" }).Count
$GitIgnore = Get-Content "$RepoRoot\.gitignore" -Raw -ErrorAction SilentlyContinue
$RequiredPatterns = @("env/profiles/local.env", "Frontend/PlantProcess.Web/.env.local", "Website/.env.local", "deploy/server/.env.production", "storybook-static")
$MissingPatterns = New-Object System.Collections.Generic.List[string]
foreach ($Pattern in $RequiredPatterns) {
    if ($GitIgnore -notmatch [regex]::Escape($Pattern)) { $MissingPatterns.Add($Pattern) | Out-Null }
}
Write-Host "[GREEN] Pack 2B env/secret separation validation executed." -ForegroundColor Green
Write-Host "Rows                 : $(@($Rows).Count)" -ForegroundColor Green
Write-Host "Tracked HIGH blockers: $Blockers" -ForegroundColor Yellow
Write-Host "Tracked MEDIUM warns : $TrackedMedium" -ForegroundColor Yellow
Write-Host "Accepted local files : $AcceptableLocal" -ForegroundColor Yellow
Write-Host "Missing ignore rules : $($MissingPatterns.Count)" -ForegroundColor Yellow
if ($MissingPatterns.Count -gt 0) {
    foreach ($M in $MissingPatterns) { Write-Host "Missing ignore: $M" -ForegroundColor Red }
    throw "Pack2B .gitignore hardening incomplete."
}
