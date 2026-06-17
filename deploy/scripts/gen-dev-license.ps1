#requires -Version 5.1
# ==================================================================================================
# deploy/scripts/gen-dev-license.ps1 - DEV-ONLY Ed25519 license generator.
# Generates an Ed25519 keypair + one signed token per tier (light/pro/proplus/enterprise) that
# conforms to the app's V5 Ed25519 verifier. Reuses an existing dev_private.pem so the committed
# dev_public.pem stays valid across runs (-ForceNewKey rotates it). PRODUCTION uses SOU's real key.
#
#   .\deploy\scripts\gen-dev-license.ps1
#   .\deploy\scripts\gen-dev-license.ps1 -ForceNewKey -Days 730
# ==================================================================================================
[CmdletBinding()]
param(
  [string]$Kid = 'ppiq-dev-ed25519',
  [int]$Days = 365,
  [string]$TenantId = '00000000-0000-0000-0000-000000000001',
  [string]$OutDir = 'deploy/fixtures/license',
  [switch]$ForceNewKey
)
$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = $ScriptDir
while($RepoRoot -and -not (Test-Path (Join-Path $RepoRoot '.git'))){
  $p = Split-Path -Parent $RepoRoot
  if(-not $p -or $p -eq $RepoRoot){ break }
  $RepoRoot = $p
}
$mjs = Join-Path $ScriptDir 'gen-ed25519-license.mjs'
if(-not (Test-Path $mjs)){ throw 'gen-ed25519-license.mjs not found next to this script' }
if(-not (Get-Command node -ErrorAction SilentlyContinue)){ throw 'Node.js is required (node not found on PATH)' }
$outAbs = Join-Path $RepoRoot $OutDir
New-Item -ItemType Directory -Force -Path $outAbs | Out-Null
$priv = Join-Path $outAbs 'dev_private.pem'
if($ForceNewKey -and (Test-Path $priv)){ Remove-Item $priv -Force }
$privArg = if(Test-Path $priv){ $priv } else { 'EMPTY' }
Write-Host ("gen-dev-license: kid=" + $Kid + " days=" + $Days + " tenant=" + $TenantId + " out=" + $OutDir) -ForegroundColor Cyan
$prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
$out = & node $mjs $outAbs $Kid ([string]$Days) $TenantId $privArg 2>&1
$code = $LASTEXITCODE
$ErrorActionPreference = $prev
$out | ForEach-Object { Write-Host ("    " + $_) -ForegroundColor DarkGray }
if($code -ne 0){ throw ("node mint failed (exit " + $code + ")") }
Write-Host ("wrote dev_public.pem + dev_public.b64 + light/pro/proplus/enterprise.token + manifest.json under " + $OutDir) -ForegroundColor Green
Write-Host "dev_private.pem is DEV-ONLY and must stay gitignored. Commit only dev_public.pem and the four *.token." -ForegroundColor Yellow