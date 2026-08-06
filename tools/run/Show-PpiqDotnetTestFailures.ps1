# PPIQ - print the real failure out of a dotnet test log, inner exceptions included
# REVISION: SHOW-DOTNET-TEST-FAILURES-01 (06-Aug-2026)
# Read only. Reads a log that already exists. Runs nothing, changes nothing.
param([string]$LogPath = "", [int]$Context = 40)
$ErrorActionPreference = "Stop"
function Say([string]$m) { Write-Host $m }

if ([string]::IsNullOrWhiteSpace($LogPath)) {
  $newest = Get-ChildItem (Join-Path (Get-Location).Path "tools\packs") -Filter "*-test_*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
  if ($null -eq $newest) { throw "No dotnet test log found under tools\packs. Pass -LogPath explicitly." }
  $LogPath = $newest.FullName
}
Say ""
Say ("LOG : " + $LogPath)
Say ""

$lines = Get-Content $LogPath
Say ("lines in log : " + $lines.Count)
Say ""

# The summary first, so the counts are on screen with the detail.
Say "SUMMARY"
foreach ($l in $lines) {
  if ($l -match "Passed!|Failed!|Total tests|Passed:|Failed:|Skipped:") { Say ("  " + $l.Trim()) }
}
Say ""

# THE FAILURE BLOCK. A DbUpdateException says nothing on its own - the cause is
# always in the inner exception two lines below it, which is exactly what a
# summary-line filter throws away. Print from each failure marker forwards.
Say "FAILURES, IN FULL"
$printedAny = $false
for ($i = 0; $i -lt $lines.Count; $i++) {
  if ($lines[$i] -notmatch "Error Message:|Failed \[|\[FAIL\]") { continue }
  $printedAny = $true
  Say "----------------------------------------------------------------"
  $end = [Math]::Min($i + $Context, $lines.Count - 1)
  for ($j = $i; $j -le $end; $j++) { Say ("  " + $lines[$j].TrimEnd()) }
  Say ""
  $i = $end
}
if (-not $printedAny) {
  Say "  No failure marker found. Last 60 lines instead:"
  $lines | Select-Object -Last 60 | ForEach-Object { Say ("  " + $_.TrimEnd()) }
}
Say ""
Say "Raise -Context if an inner exception is still cut off."
