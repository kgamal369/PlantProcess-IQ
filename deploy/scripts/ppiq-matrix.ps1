#requires -Version 5.1
# ==================================================================================================
# deploy/scripts/ppiq-matrix.ps1 - exercise EVERY role x EVERY license against a running app.
#
#   .\deploy\scripts\ppiq-matrix.ps1                 # against http://localhost:5063
#   .\deploy\scripts\ppiq-matrix.ps1 -BaseUrl http://localhost:5063 -RestoreTier Enterprise
#
# Prereq: the app is up (e.g. `.\deploy\scripts\ppiq.ps1 demo`). This script reads the SAME
# committed identities from deploy/compose/.env.dev - it never invents a user or password.
#
# Axis 1 (role):    logs in as each PlantProcess__Auth__Users__N and checks the /admin gate.
# Axis 2 (license): admin activates each *.token (V5 Ed25519) and entitlement-checks features.
# Output:           a role table + a tier x feature PASS/FAIL grid. Non-zero exit on any mismatch.
#
# CONFIRM-ONCE config (kept at the top, not buried): the activate/verify request token field name
# and the feature-key strings come from Backend/PlantProcess.Api/SignedLicensing/
# V5Ed25519LicenseEndpoints.cs (records Ed25519ActivateLicenseRequest / Ed25519EntitlementCheckRequest
# and the RequiredTierByFeature map). Adjust $TokenField / $Features there if your build differs.
# ==================================================================================================
[CmdletBinding()]
param(
  [string]$BaseUrl = 'http://localhost:5063',
  [string]$RepoRoot,                      # optional explicit repo root; auto-detected if omitted
  [ValidateSet('Light','Pro','ProPlus','Enterprise','None')]
  [string]$RestoreTier = 'Enterprise'     # tier to leave active for the demo when finished
)

$ErrorActionPreference = 'Stop'

# ---- locate repo root (works run-as-file, dot-sourced, OR pasted into the console) --------------
# RUN THIS AS A FILE:  .\deploy\scripts\ppiq-matrix.ps1     (do NOT paste it line-by-line)
if([string]::IsNullOrEmpty($RepoRoot)){
  $start = $PSScriptRoot
  if([string]::IsNullOrEmpty($start) -and $MyInvocation.MyCommand.Path){ $start = Split-Path -Parent $MyInvocation.MyCommand.Path }
  if([string]::IsNullOrEmpty($start)){ $start = (Get-Location).Path }   # pasted interactively -> use CWD
  $RepoRoot = $start
  while($RepoRoot -and -not (Test-Path (Join-Path $RepoRoot '.git'))){
    $parent = Split-Path -Parent $RepoRoot
    if([string]::IsNullOrEmpty($parent) -or $parent -eq $RepoRoot){ break }
    $RepoRoot = $parent
  }
  if(-not (Test-Path (Join-Path $RepoRoot '.git'))){
    if(Test-Path (Join-Path (Get-Location).Path '.git')){ $RepoRoot = (Get-Location).Path }
    else { throw "Could not find repo root (.git). Run from inside the repo, or pass -RepoRoot C:\Workspace\PlantProcess-IQ" }
  }
}
$EnvFile     = Join-Path $RepoRoot 'deploy/compose/.env.dev'
$LicenseDir  = Join-Path $RepoRoot 'deploy/fixtures/license'
if(-not (Test-Path $EnvFile)){ throw "$EnvFile not found under $RepoRoot" }

$envMap = @{}
foreach($line in (Get-Content $EnvFile)){
  $t = ("$line").Trim()
  if($t -eq '' -or $t.StartsWith('#')){ continue }
  $i = $t.IndexOf('='); if($i -lt 1){ continue }
  $envMap[$t.Substring(0,$i).Trim()] = $t.Substring($i+1).Trim()
}

# build the role-user list from PlantProcess__Auth__Users__N__*
$users = @()
for($n=0; $n -lt 20; $n++){
  $u = $envMap["PlantProcess__Auth__Users__${n}__UserName"]
  if([string]::IsNullOrEmpty($u)){ continue }
  $users += [pscustomobject]@{
    UserName = $u
    Password = $envMap["PlantProcess__Auth__Users__${n}__Password"]
    Role     = $envMap["PlantProcess__Auth__Users__${n}__Role"]
  }
}
if($users.Count -eq 0){ throw "no PlantProcess__Auth__Users__N found in .env.dev" }

# ---- CONFIRM-ONCE: request shape + feature keys -----------------------------------------------
$TokenField = 'token'   # field name in Ed25519ActivateLicenseRequest / Ed25519OfflineVerifyRequest
# representative feature per minimum tier (key strings = RequiredTierByFeature keys)
$Features = [ordered]@{
  'CsvImport'           = 'Light'        # available Light+
  'PostgreSqlConnector' = 'Pro'          # available Pro+
  'KpiViewBuilder'      = 'ProPlus'      # available ProPlus+
  'OracleConnector'     = 'Enterprise'   # available Enterprise only
}
$TierRank = @{ 'Light'=1; 'Pro'=2; 'ProPlus'=3; 'Enterprise'=4 }
$Tiers    = @('Light','Pro','ProPlus','Enterprise')

# ---- helpers -----------------------------------------------------------------------------------
function Login([string]$user,[string]$pass){
  $body = @{ userName=$user; password=$pass } | ConvertTo-Json
  $r = Invoke-RestMethod -Uri "$BaseUrl/auth/login" -Method Post -ContentType 'application/json' -Body $body
  # token field name on the response may be token/accessToken - take the first string-ish prop
  foreach($p in 'token','accessToken','bearer','jwt'){ if($r.PSObject.Properties.Name -contains $p){ return $r.$p } }
  return ($r | ConvertTo-Json -Compress)
}
function Can-ReachAdmin([string]$bearer){
  try { Invoke-RestMethod -Uri "$BaseUrl/admin/license/current" -Headers @{ Authorization="Bearer $bearer" } | Out-Null; return $true }
  catch { return $false }
}
function Activate-Tier([string]$bearer,[string]$tier){
  $f = Join-Path $LicenseDir ("{0}.token" -f $tier.ToLower())
  if(-not (Test-Path $f)){ throw "token not found: $f" }
  $jws = (Get-Content $f -Raw).Trim()
  $body = @{ $TokenField = $jws } | ConvertTo-Json
  Invoke-RestMethod -Uri "$BaseUrl/api/v5/licensing/ed25519/activate" -Method Post `
    -Headers @{ Authorization="Bearer $bearer" } -ContentType 'application/json' -Body $body | Out-Null
}
function Check-Feature([string]$bearer,[string]$feature){
  $body = @{ feature=$feature; dbTierOverride=$null } | ConvertTo-Json
  $r = Invoke-RestMethod -Uri "$BaseUrl/api/v5/licensing/ed25519/entitlement-check" -Method Post `
        -Headers @{ Authorization="Bearer $bearer" } -ContentType 'application/json' -Body $body
  foreach($p in 'allowed','isAllowed','entitled'){ if($r.PSObject.Properties.Name -contains $p){ return [bool]$r.$p } }
  return $false
}
function Mark($ok){ if($ok){ '  PASS' } else { '  FAIL' } }

# ---- AXIS 1: roles -----------------------------------------------------------------------------
Write-Host ""
Write-Host "ROLE AXIS  (base $BaseUrl)" -ForegroundColor Cyan
Write-Host ("{0,-12} {1,-12} {2,-8} {3}" -f 'user','role','login','admin-gate")
$failures = 0
$adminBearer = $null
foreach($u in $users){
  $bearer = $null; $loginOk = $false
  try { $bearer = Login $u.UserName $u.Password; $loginOk = -not [string]::IsNullOrEmpty($bearer) } catch { $loginOk = $false }
  $admin = $false; if($loginOk){ $admin = Can-ReachAdmin $bearer }
  $expectAdmin = ($u.Role -eq 'Admin')
  $gateOk = ($admin -eq $expectAdmin)
  if(-not $loginOk -or -not $gateOk){ $failures++ }
  if($u.Role -eq 'Admin' -and $loginOk){ $adminBearer = $bearer }
  $gateTxt = if($admin){ 'reachable' } else { '403/denied' }
  if(-not $gateOk){ $gateTxt += ' (UNEXPECTED)' }
  Write-Host ("{0,-12} {1,-12} {2,-8} {3}" -f $u.UserName, $u.Role, ($(if($loginOk){'200'}else{'FAIL'})), $gateTxt)
}
if($null -eq $adminBearer){ Write-Host "no admin login - cannot run license axis" -ForegroundColor Red; exit 1 }

# ---- AXIS 2: licenses --------------------------------------------------------------------------
Write-Host ""
Write-Host "LICENSE AXIS  (admin activates each signed token; entitlement-check per feature)" -ForegroundColor Cyan
$hdr = "{0,-22}" -f 'feature \ tier'
foreach($t in $Tiers){ $hdr += ("{0,-12}" -f $t) }
Write-Host $hdr
foreach($feat in $Features.Keys){
  $minTier = $Features[$feat]
  $row = "{0,-22}" -f $feat
  foreach($t in $Tiers){
    Activate-Tier $adminBearer $t
    $allowed  = Check-Feature $adminBearer $feat
    $expected = ($TierRank[$t] -ge $TierRank[$minTier])
    if($allowed -ne $expected){ $failures++ }
    $cell = if($allowed){ 'allow' } else { 'deny' }
    if($allowed -ne $expected){ $cell += '!' }
    $row += ("{0,-12}" -f $cell)
  }
  Write-Host $row
}

# ---- restore a tier for the demo + summary -----------------------------------------------------
if($RestoreTier -ne 'None'){ Activate-Tier $adminBearer $RestoreTier; Write-Host "`nrestored active tier -> $RestoreTier" -ForegroundColor DarkGray }
Write-Host ""
if($failures -eq 0){ Write-Host "MATRIX OK - all role and license expectations met." -ForegroundColor Green; exit 0 }
else { Write-Host ("MATRIX: $failures mismatch(es) - see '!' / UNEXPECTED above.") -ForegroundColor Red; exit 1 }
