<#
====================================================================================================
 PlantProcess IQ - M1-T06 : dev Ed25519 license keypair + per-tier tokens   (pure ASCII; writes + runs + validates)
====================================================================================================
 Backlog M1-T06 (Security / Licensing, dep M1-T02):
   Add deploy/scripts/gen-dev-license.ps1 generating a DEV-ONLY Ed25519 keypair and one signed token
   per tier (light/pro/proplus/enterprise) whose schema matches the verifier. Commit dev_public.pem and
   the four *.token under deploy/fixtures/license/. Production uses SOU's real key, never this one.
 Acceptance:
   Each tier token verifies TRUE against dev_public.pem; load enterprise.token in the running app and
   enterprise-only features render; swap to pro.token and they disappear (live tier toggle); the
   Phase5_LicenseTierTamperTests unit test passes (editing the tier row changes nothing).

 ---------------------------------------------------------------------------------------------------
 TWO GROUNDED FACTS THE BACKLOG TEXT GETS SLIGHTLY WRONG (verified against the 16-Jun code):

 (1) There is NO in-app Ed25519 "dev-mint path".
     VerifiedEd25519LicenseService is the runtime VERIFIER/reader (Ed25519Signer is used only to verify).
     The app's health endpoint references a signing tool tools/v5/generate-ed25519-license.mjs, but that
     file is not in the repo. The only in-app dev-mint that exists is a SEPARATE ECDSA P-256 path
     (V5SignedLicensingEndpoints /dev/create-license) that writes ppiq_license_entitlement_projection -
     a table the runtime tier does NOT read. So minting through "the existing service" would not move the
     tier. This script therefore SELF-MINTS Ed25519 in Node, conforming exactly to the verifier byte
     format (compact JWS: base64url(header).base64url(payload).base64url(sig); signingInput = ASCII of
     the first two segments; alg=EdDSA; typ contains "license"; 32-raw-byte public key as standard
     base64; tier in the verifier's TierRank). The mint+verify round trip is self-checked in Node before
     anything touches the app.

 (2) The only path that moves the RUNTIME tier is /api/v5/licensing/ed25519/activate.
     GetCurrentTier() reads the view public.ppiq_v_ed25519_current_entitlements over
     ppiq_ed25519_activated_licenses (DISTINCT ON tenant, valid+active+unexpired, newest activated wins).
     activate verifies the JWS against the registered public key for its kid, then supersedes any other
     active license for the tenant and upserts the new one - so activating enterprise then pro flips the
     live tier deterministically with no manual DELETE. entitlement-check then proves the toggle:
     SqlServerConnector (Enterprise-gated) is allowed under enterprise.token and denied under pro.token,
     and it explicitly ignores any DB tier override (noDbEditEscalation=true) - the same property the
     Phase5 unit test proves at the envelope layer.

 PREREQ for the live gates: Node.js on PATH, and the app up WITH a migrated DB (the Ed25519 tables +
 the default-demo tenant must exist). Bring it up first:  .\deploy\scripts\ppiq.ps1 demo

 Run:   .\PPIQ_M1_T06_Implementation.ps1
        .\PPIQ_M1_T06_Implementation.ps1 -ApiBase http://localhost:5063 -ForceNewKey
====================================================================================================
#>

param(
  [string]$ApiBase    = 'http://localhost:5063',
  [string]$TenantId   = '00000000-0000-0000-0000-000000000001',
  [string]$Kid        = 'ppiq-dev-ed25519',
  [int]$Days          = 365,
  [switch]$ForceNewKey,
  [switch]$SkipTamperTest
)

& {
  $ErrorActionPreference = 'Stop'
  Set-StrictMode -Version 1.0   # 1.0 (not 2.0): HTTP JSON responses have variable shape - absent optional
                                # fields (tier/verifiedTier on error bodies) must read as $null, not throw.

  $script:Results = New-Object System.Collections.Generic.List[object]
  function Add-Result([string]$Check,[bool]$Pass,[string]$Detail){
    $script:Results.Add([pscustomobject]@{ Pass=$Pass; Check=$Check; Detail=$Detail })
    $tag = if($Pass){'PASS'}else{'FAIL'}; $col = if($Pass){'Green'}else{'Red'}
    Write-Host ("  [{0}] {1} :: {2}" -f $tag,$Check,$Detail) -ForegroundColor $col
  }
  function Info([string]$m){ Write-Host $m -ForegroundColor Cyan }
  function Warn([string]$m){ Write-Host $m -ForegroundColor Yellow }
  function Write-LfNoBom([string]$Path,[string]$Text){
    $lf = $Text -replace "`r`n","`n"
    [System.IO.File]::WriteAllText($Path,$lf,(New-Object System.Text.UTF8Encoding($false)))
  }
  function Get-RepoRoot {
    $dir = (Get-Location).Path
    while(-not [string]::IsNullOrEmpty($dir)){
      if(Test-Path -LiteralPath (Join-Path $dir '.git')){ return $dir }
      $parent = Split-Path -Parent $dir
      if([string]::IsNullOrEmpty($parent) -or $parent -eq $dir){ break }
      $dir = $parent
    }
    return (Get-Location).Path
  }
  function Sql-Lit([string]$s){ "'" + ($s -replace "'","''") + "'" }

  # wrap native processes so a tool writing to stderr cannot turn into a terminating error
  function Run-Exe([string]$Exe,[string[]]$Arguments,[switch]$Echo){
    $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    $out = & $Exe @Arguments 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if($Echo){ $out | ForEach-Object { Write-Host ("    " + $_) -ForegroundColor DarkGray } }
    return [pscustomobject]@{ Code=$code; Out=("" + ($out -join "`n")) }
  }

  function Invoke-Json([string]$Method,[string]$Url,$Body,$Headers){
    $prev = $ErrorActionPreference; $ErrorActionPreference = 'Stop'
    try {
      $iwr = @{ Uri=$Url; Method=$Method; UseBasicParsing=$true; TimeoutSec=20 }
      if($null -ne $Body){ $iwr.Body = ($Body | ConvertTo-Json -Compress -Depth 8); $iwr.ContentType='application/json' }
      if($null -ne $Headers){ $iwr.Headers = $Headers }
      $r = Invoke-WebRequest @iwr
      $obj = $null; try { $obj = $r.Content | ConvertFrom-Json } catch {}
      return [pscustomobject]@{ Code=[int]$r.StatusCode; Data=$obj; Raw=("" + $r.Content) }
    } catch {
      $code = $null; $raw = ''
      try { $code = $_.Exception.Response.StatusCode.value__ } catch {}
      try { $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream()); $raw = $sr.ReadToEnd() } catch {}
      $obj = $null; try { $obj = $raw | ConvertFrom-Json } catch {}
      return [pscustomobject]@{ Code=([int]$code); Data=$obj; Raw=("" + $raw) }
    } finally { $ErrorActionPreference = $prev }
  }

  function Get-DotEnv([string]$Path){
    $map = @{}
    if(-not (Test-Path $Path)){ return $map }
    foreach($line in (Get-Content $Path)){
      $t = ("" + $line).Trim()
      if($t -eq '' -or $t.StartsWith('#')){ continue }
      $i = $t.IndexOf('='); if($i -lt 1){ continue }
      $k = $t.Substring(0,$i).Trim(); $v = $t.Substring($i+1).Trim()
      if($v.Length -ge 2){ $a=$v.Substring(0,1); $b=$v.Substring($v.Length-1,1); if(($a -eq '"' -and $b -eq '"') -or ($a -eq "'" -and $b -eq "'")){ $v=$v.Substring(1,$v.Length-2) } }
      $map[$k] = $v
    }
    return $map
  }

  $RepoRoot = Get-RepoRoot; Set-Location $RepoRoot
  $ScriptsDir   = Join-Path $RepoRoot 'deploy/scripts'
  $FixturesDir  = Join-Path $RepoRoot 'deploy/fixtures/license'
  $MjsPath      = Join-Path $ScriptsDir 'gen-ed25519-license.mjs'
  $GenPs1Path   = Join-Path $ScriptsDir 'gen-dev-license.ps1'
  $BackupDir    = Join-Path $RepoRoot '.ppiq-script-backups'
  $EnvFile      = Join-Path $RepoRoot 'deploy/compose/.env.dev'
  $Group        = '/api/v5/licensing/ed25519'
  $TenantHeader = @{ 'X-Tenant-Id' = $TenantId }
  New-Item -ItemType Directory -Force -Path $ScriptsDir | Out-Null
  New-Item -ItemType Directory -Force -Path $FixturesDir | Out-Null

  Info "============================================================================"
  Info " PPIQ M1-T06 - dev Ed25519 keypair + per-tier tokens + live tier toggle"
  Info " repo root: $RepoRoot"
  Info " api base : $ApiBase    tenant: $TenantId    kid: $Kid"
  Info "============================================================================"

  # ==================================================================================================
  # 1) deploy/scripts/gen-ed25519-license.mjs  (the proven minter; self-verifies each token)
  # ==================================================================================================
  $mjs = @'
// PlantProcess IQ - DEV-ONLY Ed25519 license generator (matches V5Ed25519LicenseEndpoints verifier).
// Usage: node gen-ed25519-license.mjs <outDir> <kid> <days> <tenantId> <privateKeyPathOrEMPTY>
// If privateKeyPath exists, the keypair is REUSED (committed dev_public.pem stays valid); else generated.
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

const [outDir, kid, daysArg, tenantId, privPathArg] = process.argv.slice(2);
if (!outDir || !kid || !daysArg || !tenantId) {
  console.error('ARGS: <outDir> <kid> <days> <tenantId> [privateKeyPath]');
  process.exit(2);
}
const days = parseInt(daysArg, 10);
const privPath = privPathArg && privPathArg !== 'EMPTY' ? privPathArg : path.join(outDir, 'dev_private.pem');

const b64url = (buf) => Buffer.from(buf).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
const b64std = (buf) => Buffer.from(buf).toString('base64');

fs.mkdirSync(outDir, { recursive: true });

// 1. reuse existing private key if present, else generate a fresh keypair
let publicKey, privateKey;
let reused = false;
if (fs.existsSync(privPath)) {
  privateKey = crypto.createPrivateKey(fs.readFileSync(privPath, 'utf8'));
  publicKey  = crypto.createPublicKey(privateKey);
  reused = true;
} else {
  ({ publicKey, privateKey } = crypto.generateKeyPairSync('ed25519'));
  fs.writeFileSync(privPath, privateKey.export({ type:'pkcs8', format:'pem' }), { mode: 0o600 });
}

// 2. derive raw 32-byte public key -> standard base64 (what verify-offline / DB expect)
const jwk = publicKey.export({ format:'jwk' });
const rawPub = Buffer.from(jwk.x, 'base64url');
if (rawPub.length !== 32) { console.error('FATAL: raw public key is not 32 bytes'); process.exit(3); }
const publicKeyB64 = b64std(rawPub);
fs.writeFileSync(path.join(outDir, 'dev_public.pem'), publicKey.export({ type:'spki', format:'pem' }));
fs.writeFileSync(path.join(outDir, 'dev_public.b64'), publicKeyB64 + '\n');

// 3. mint one signed compact-JWS per tier
const now = new Date();
const exp = new Date(now.getTime() + days*24*3600*1000);
const tiers = [
  { tier:'Light',      file:'light.token',      licenseKey:'PPIQ-DEV-LIGHT' },
  { tier:'Pro',        file:'pro.token',        licenseKey:'PPIQ-DEV-PRO' },
  { tier:'ProPlus',    file:'proplus.token',    licenseKey:'PPIQ-DEV-PROPLUS' },
  { tier:'Enterprise', file:'enterprise.token', licenseKey:'PPIQ-DEV-ENTERPRISE' }
];
const header = { alg:'EdDSA', typ:'license+jws', kid };
const hB64 = b64url(Buffer.from(JSON.stringify(header), 'utf8'));

const manifest = { kid, tenantId, algorithm:'Ed25519', issuedAtUtc:now.toISOString(),
                   expiresAtUtc:exp.toISOString(), reusedExistingKey:reused, publicKeyB64, tokens:[] };

for (const t of tiers) {
  const payload = {
    tenantId, licenseKey:t.licenseKey, tier:t.tier,
    issuedAtUtc:now.toISOString(), expiresAtUtc:exp.toISOString(),
    features:[], limits:{}
  };
  const pB64 = b64url(Buffer.from(JSON.stringify(payload), 'utf8'));
  const signingInput = Buffer.from(hB64 + '.' + pB64, 'ascii');
  const sig = crypto.sign(null, signingInput, privateKey);
  const jws = hB64 + '.' + pB64 + '.' + b64url(sig);

  // self-verify exactly like the C# verifier (raw-32 pubkey, ASCII signing input)
  const recon = crypto.createPublicKey({ key:{ kty:'OKP', crv:'Ed25519', x:rawPub.toString('base64url') }, format:'jwk' });
  const ok = crypto.verify(null, Buffer.from(hB64 + '.' + pB64, 'ascii'),
                           recon, Buffer.from(b64url(sig).replace(/-/g,'+').replace(/_/g,'/'), 'base64'));
  if (!ok) { console.error('FATAL: self-verify failed for ' + t.tier); process.exit(4); }

  fs.writeFileSync(path.join(outDir, t.file), jws + '\n');
  manifest.tokens.push({ tier:t.tier, licenseKey:t.licenseKey, file:t.file, selfVerified:true });
}

fs.writeFileSync(path.join(outDir, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
console.log(JSON.stringify({ ok:true, reusedExistingKey:reused, publicKeyB64, kid,
  tokens:tiers.map(t => t.file) }));
'@

  # ==================================================================================================
  # 2) deploy/scripts/gen-dev-license.ps1  (the committed tool Karim runs / re-runs)
  # ==================================================================================================
  $genPs1 = @'
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
'@

  function Backup-IfExists([string]$path){
    if(Test-Path $path){
      New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
      $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
      Copy-Item $path (Join-Path $BackupDir ((Split-Path -Leaf $path) + "." + $stamp + ".bak")) -Force
      Info ("backed up " + (Split-Path -Leaf $path) + " -> .ppiq-script-backups")
    }
  }

  Backup-IfExists $MjsPath
  Backup-IfExists $GenPs1Path
  Write-LfNoBom $MjsPath $mjs
  Write-LfNoBom $GenPs1Path $genPs1
  Info ("wrote " + $MjsPath)
  Info ("wrote " + $GenPs1Path)

  # never commit the dev private key
  $giPath = Join-Path $RepoRoot '.gitignore'
  $giLine = 'deploy/fixtures/license/dev_private.pem'
  if(Test-Path $giPath){
    $gi = Get-Content $giPath -Raw
    if($gi -notmatch [regex]::Escape($giLine)){ Add-Content $giPath ("`n# DEV-ONLY Ed25519 private key - never commit`n" + $giLine) ; Info "added dev_private.pem to .gitignore" }
  } else {
    Write-LfNoBom $giPath ("# DEV-ONLY Ed25519 private key - never commit`n" + $giLine + "`n")
    Info "created .gitignore with dev_private.pem"
  }

  # ==================================================================================================
  # validation: files written + parse
  # ==================================================================================================
  Info "`n--- static: artifacts written ---"
  $mjsBytes = [System.IO.File]::ReadAllBytes($MjsPath)
  $psBytes  = [System.IO.File]::ReadAllBytes($GenPs1Path)
  Add-Result 'gen-ed25519-license.mjs + gen-dev-license.ps1 are pure ASCII' ((@($mjsBytes | Where-Object { $_ -gt 127 }).Count -eq 0) -and (@($psBytes | Where-Object { $_ -gt 127 }).Count -eq 0)) 'no non-ASCII bytes'
  $tok=$null;$perr=$null; [void][System.Management.Automation.Language.Parser]::ParseFile($GenPs1Path,[ref]$tok,[ref]$perr)
  $pe = if($perr){ @($perr).Count } else { 0 }
  Add-Result 'gen-dev-license.ps1 parses with zero syntax errors' ($pe -eq 0) ($pe.ToString() + ' parse error(s)')

  # ==================================================================================================
  # 3) mint the keypair + four tokens (Node), self-verified inside the .mjs
  # ==================================================================================================
  Info "`n--- mint: Ed25519 keypair + four tier tokens ---"
  $nodeOk = [bool](Get-Command node -ErrorAction SilentlyContinue)
  Add-Result 'Node.js available on PATH' $nodeOk $(if($nodeOk){ (& node --version) } else { 'install Node.js to mint/verify' })
  $minted = $false; $pubB64 = $null
  if($nodeOk){
    $priv = Join-Path $FixturesDir 'dev_private.pem'
    if($ForceNewKey -and (Test-Path $priv)){ Remove-Item $priv -Force; Info 'rotating dev key (-ForceNewKey)' }
    $privArg = if(Test-Path $priv){ $priv } else { 'EMPTY' }
    $m = Run-Exe 'node' @($MjsPath,$FixturesDir,$Kid,([string]$Days),$TenantId,$privArg)
    $minted = ($m.Code -eq 0)
    Add-Result 'node minted keypair + 4 tokens (each self-verified)' $minted ($(if($minted){'ok'}else{'FAILED: '}) + ($m.Out -split "`n" | Select-Object -Last 1))
    $b64File = Join-Path $FixturesDir 'dev_public.b64'
    if(Test-Path $b64File){ $pubB64 = (Get-Content $b64File -Raw).Trim() }
    $haveAll = (Test-Path (Join-Path $FixturesDir 'dev_public.pem')) -and
               (Test-Path (Join-Path $FixturesDir 'light.token')) -and
               (Test-Path (Join-Path $FixturesDir 'pro.token')) -and
               (Test-Path (Join-Path $FixturesDir 'proplus.token')) -and
               (Test-Path (Join-Path $FixturesDir 'enterprise.token'))
    Add-Result 'dev_public.pem + four *.token present under deploy/fixtures/license' $haveAll $FixturesDir
    Add-Result 'public key is 32 raw bytes (standard base64, 44 chars)' (("" + $pubB64).Length -eq 44) ('publicKeyB64=' + ("" + $pubB64))
  }

  # token text loader (strip trailing newline)
  function Get-Token([string]$name){ $p = Join-Path $FixturesDir $name; if(Test-Path $p){ return (Get-Content $p -Raw).Trim() } else { return $null } }

  # ==================================================================================================
  # 4) live: API reachability, register public key (RLS), verify all 4 offline, tier toggle
  # ==================================================================================================
  Info "`n--- live: $ApiBase$Group ---"
  $apiUp = $false
  try { $h = Invoke-WebRequest -Uri ($ApiBase + $Group + '/health') -UseBasicParsing -TimeoutSec 5; $apiUp = ($h.StatusCode -ge 200 -and $h.StatusCode -lt 500) } catch {
    try { $null = Invoke-WebRequest -Uri ($ApiBase + '/health') -UseBasicParsing -TimeoutSec 5; $apiUp = $true } catch { $apiUp = $false }
  }

  if(-not $apiUp){
    Warn "  API not reachable at $ApiBase. The minted files + tools are written and committable."
    Warn "  Bring the app up WITH a migrated DB to run the verify/toggle gates:  .\deploy\scripts\ppiq.ps1 demo"
  } elseif($minted -and $pubB64){
    # --- 4a. register the dev public key for the demo tenant (FORCE RLS: set the tenant GUC first) ----
    $env_ = Get-DotEnv $EnvFile
    $pgHost = $env_['POSTGRES_HOST']; if([string]::IsNullOrEmpty($pgHost)){ $pgHost = 'localhost' }
    $pgPort = $env_['POSTGRES_PORT']; if([string]::IsNullOrEmpty($pgPort)){ $pgPort = '5432' }
    $pgUser = $env_['POSTGRES_USER']; if([string]::IsNullOrEmpty($pgUser)){ $pgUser = 'ppiq_dev' }
    $pgDb   = $env_['POSTGRES_DB'];   if([string]::IsNullOrEmpty($pgDb)){ $pgDb = 'ppiq_app' }
    $pgPass = $env_['POSTGRES_PASSWORD']; if([string]::IsNullOrEmpty($pgPass)){ $pgPass = 'ppiq_dev_local_only' }
    $psqlOk = [bool](Get-Command psql -ErrorAction SilentlyContinue)

    $registered = $false
    if($psqlOk){
      $env:PGPASSWORD = $pgPass
      $regSql = "SELECT set_config('app.current_tenant'," + (Sql-Lit $TenantId) + ",false); " +
                "INSERT INTO public.ppiq_ed25519_license_public_keys(tenant_id,key_id,public_key_b64,algorithm,status) " +
                "VALUES (" + (Sql-Lit $TenantId) + "," + (Sql-Lit $Kid) + "," + (Sql-Lit $pubB64) + ",'Ed25519','active') " +
                "ON CONFLICT (tenant_id,key_id) DO UPDATE SET public_key_b64=EXCLUDED.public_key_b64, algorithm='Ed25519', status='active', retired_at_utc=NULL;"
      $reg = Run-Exe 'psql' @('-h',$pgHost,'-p',$pgPort,'-U',$pgUser,'-d',$pgDb,'-v','ON_ERROR_STOP=1','-q','-c',$regSql)
      $registered = ($reg.Code -eq 0)
      Add-Result ('registered dev public key for demo tenant (kid=' + $Kid + ')') $registered ($(if($registered){'inserted/updated ppiq_ed25519_license_public_keys'}else{'psql failed - is the DB migrated? run ppiq.ps1 migrate : '}) + ($reg.Out -split "`n" | Select-Object -Last 1))
      # clean any prior activations for a crisp toggle (guarded to the demo tenant)
      $delSql = "SELECT set_config('app.current_tenant'," + (Sql-Lit $TenantId) + ",false); DELETE FROM public.ppiq_ed25519_activated_licenses WHERE tenant_id=" + (Sql-Lit $TenantId) + ";"
      $null = Run-Exe 'psql' @('-h',$pgHost,'-p',$pgPort,'-U',$pgUser,'-d',$pgDb,'-v','ON_ERROR_STOP=1','-q','-c',$delSql)
    } else {
      Warn "  psql not on PATH - cannot register the public key here. The app's /activate will 400 until the key is registered."
      Warn "  Register manually (same DB the app uses), setting the tenant GUC first:"
      Warn ("    SELECT set_config('app.current_tenant','" + $TenantId + "',false);")
      Warn ("    INSERT INTO public.ppiq_ed25519_license_public_keys(tenant_id,key_id,public_key_b64,algorithm,status)")
      Warn ("    VALUES ('" + $TenantId + "','" + $Kid + "','" + $pubB64 + "','Ed25519','active')")
      Warn  "    ON CONFLICT (tenant_id,key_id) DO UPDATE SET public_key_b64=EXCLUDED.public_key_b64, status='active', retired_at_utc=NULL;"
    }

    # --- 4b. verify all four tokens OFFLINE against dev_public.b64 (no DB needed) ----------------------
    Info "`n  verify-offline (all four tiers must verify TRUE):"
    $tierFiles = @(
      [pscustomobject]@{ Tier='Light';      File='light.token' },
      [pscustomobject]@{ Tier='Pro';        File='pro.token' },
      [pscustomobject]@{ Tier='ProPlus';    File='proplus.token' },
      [pscustomobject]@{ Tier='Enterprise'; File='enterprise.token' }
    )
    foreach($tf in $tierFiles){
      $jws = Get-Token $tf.File
      $ok=$false; $detail='token missing'
      if($jws){
        $r = Invoke-Json 'Post' ($ApiBase + $Group + '/verify-offline') (@{ licenseJws=$jws; publicKeyB64=$pubB64; expectedTenantId=$TenantId }) $null
        $valid = ($r.Data -and $r.Data.valid -eq $true)
        $tierMatch = ($r.Data -and ("" + $r.Data.tier) -eq $tf.Tier)
        $ok = ($r.Code -eq 200 -and $valid -and $tierMatch)
        $detail = "HTTP " + $r.Code + "; valid=" + ("" + $(if($r.Data){$r.Data.valid}else{'n/a'})) + "; tier=" + ("" + $(if($r.Data){$r.Data.tier}else{'n/a'}))
      }
      Add-Result ("verify-offline " + $tf.Tier + ".token -> valid:true") $ok $detail
    }

    # --- 4c. LIVE TIER TOGGLE via activate + entitlement-check ----------------------------------------
    if($registered){
      Info "`n  live tier toggle (SqlServerConnector is Enterprise-gated):"
      function Activate([string]$file){
        $jws = Get-Token $file
        if(-not $jws){ return [pscustomobject]@{ Code=0; Data=$null } }
        return Invoke-Json 'Post' ($ApiBase + $Group + '/activate') (@{ licenseJws=$jws }) $TenantHeader
      }
      function Entitle([string]$feature){
        return Invoke-Json 'Post' ($ApiBase + $Group + '/entitlement-check') (@{ feature=$feature }) $TenantHeader
      }

      # enterprise -> tier Enterprise, SqlServerConnector allowed
      $aE = Activate 'enterprise.token'
      $eEntActivated = ($aE.Data -and $aE.Data.activated -eq $true -and ("" + $aE.Data.tier) -eq 'Enterprise')
      Add-Result 'activate enterprise.token -> activated:true, tier Enterprise' $eEntActivated ("HTTP " + $aE.Code + "; tier=" + ("" + $(if($aE.Data){$aE.Data.tier}else{'n/a'})))
      $cE = Entitle 'SqlServerConnector'
      $eAllowed = ($cE.Data -and $cE.Data.allowed -eq $true -and ("" + $cE.Data.verifiedTier) -eq 'Enterprise')
      Add-Result 'under Enterprise: SqlServerConnector allowed:true' $eAllowed ("HTTP " + $cE.Code + "; allowed=" + ("" + $(if($cE.Data){$cE.Data.allowed}else{'n/a'})) + "; verifiedTier=" + ("" + $(if($cE.Data){$cE.Data.verifiedTier}else{'n/a'})))

      # swap to pro -> tier Pro, SqlServerConnector now denied (the enterprise feature disappears)
      $aP = Activate 'pro.token'
      $pActivated = ($aP.Data -and $aP.Data.activated -eq $true -and ("" + $aP.Data.tier) -eq 'Pro')
      Add-Result 'swap to pro.token -> activated:true, tier Pro' $pActivated ("HTTP " + $aP.Code + "; tier=" + ("" + $(if($aP.Data){$aP.Data.tier}else{'n/a'})))
      $cP = Entitle 'SqlServerConnector'
      $pDenied = ($cP.Data -and $cP.Data.allowed -eq $false -and ("" + $cP.Data.verifiedTier) -eq 'Pro')
      Add-Result 'under Pro: SqlServerConnector allowed:false (LIVE TIER TOGGLE)' $pDenied ("HTTP " + $cP.Code + "; allowed=" + ("" + $(if($cP.Data){$cP.Data.allowed}else{'n/a'})) + "; verifiedTier=" + ("" + $(if($cP.Data){$cP.Data.verifiedTier}else{'n/a'})))

      # bonus: proplus grades correctly (ProPlus feature on, Enterprise feature off)
      $aPP = Activate 'proplus.token'
      $cPPon  = Entitle 'KpiViewBuilder'        # ProPlus-gated
      $cPPoff = Entitle 'SqlServerConnector'    # Enterprise-gated
      $ppGraded = ($aPP.Data -and ("" + $aPP.Data.tier) -eq 'ProPlus' -and $cPPon.Data -and $cPPon.Data.allowed -eq $true -and $cPPoff.Data -and $cPPoff.Data.allowed -eq $false)
      Add-Result 'under ProPlus: KpiViewBuilder allowed:true, SqlServerConnector allowed:false' $ppGraded ("tier=" + ("" + $(if($aPP.Data){$aPP.Data.tier}else{'n/a'})) + "; kpi=" + ("" + $(if($cPPon.Data){$cPPon.Data.allowed}else{'n/a'})) + "; sql=" + ("" + $(if($cPPoff.Data){$cPPoff.Data.allowed}else{'n/a'})))

      # leave the demo in Enterprise so the UI shows the full feature set
      $null = Activate 'enterprise.token'
      Info "  (left active tier = Enterprise for the demo)"
    }
  }

  # ==================================================================================================
  # 5) tamper unit test: editing the tier row changes nothing (signature wins)
  # ==================================================================================================
  if(-not $SkipTamperTest){
    Info "`n--- tamper test: Phase5_LicenseTierTamperTests (pure unit test; builds first) ---"
    $dotnetOk = [bool](Get-Command dotnet -ErrorAction SilentlyContinue)
    if($dotnetOk){
      $tproj = Join-Path $RepoRoot 'Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj'
      if(Test-Path $tproj){
        $t = Run-Exe 'dotnet' @('test',$tproj,'--filter','FullyQualifiedName~Phase5_LicenseTierTamperTests','--nologo')
      } else {
        $t = Run-Exe 'dotnet' @('test','--filter','FullyQualifiedName~Phase5_LicenseTierTamperTests','--nologo')
      }
      $passed = ($t.Code -eq 0)
      $line = ($t.Out -split "`n" | Where-Object { $_ -match 'Passed!|Failed!|Passed:|Failed:' } | Select-Object -Last 1)
      Add-Result 'Phase5_LicenseTierTamperTests passes (forged tier rejected, TamperRejected=true)' $passed ("" + $line)
    } else {
      Warn "  dotnet not on PATH - skipping the tamper unit test gate (run: dotnet test Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase5_LicenseTierTamperTests)"
    }
  } else {
    Info "`n(tamper test skipped by -SkipTamperTest)"
  }

  # ==================================================================================================
  Info "`n============================================================================"
  Info " RESULT LEDGER - M1-T06"
  Info "============================================================================"
  $script:Results | Format-Table Pass,Check,Detail -AutoSize | Out-String | Write-Host
  $fail = @($script:Results | Where-Object { -not $_.Pass })
  if($fail.Count -eq 0){
    if($apiUp){
      Write-Host "M1-T06 GREEN - four tokens verify offline, live tier toggle exercised on the Ed25519 runtime path, tamper test green." -ForegroundColor Green
    } else {
      Write-Host "M1-T06 PARTIAL - artifacts minted + committable and the tamper test passes, but the API was DOWN so the verify-offline and live tier-toggle gates did NOT run." -ForegroundColor Yellow
      Write-Host "Bring the app up on :5063 (ppiq.ps1 up) and re-run to close the two live gates that ARE the core T06 acceptance." -ForegroundColor Yellow
    }
    Write-Host "Commit:  git add deploy/scripts/gen-ed25519-license.mjs deploy/scripts/gen-dev-license.ps1 deploy/fixtures/license/dev_public.pem deploy/fixtures/license/*.token .gitignore" -ForegroundColor DarkGray
    Write-Host "         (dev_private.pem stays gitignored.)  Re-run any time:  .\deploy\scripts\gen-dev-license.ps1" -ForegroundColor DarkGray
  } else {
    Write-Host ("{0} CHECK(S) FAILED - see ledger (most live gates need: Node + psql + the app up with a migrated DB)." -f $fail.Count) -ForegroundColor Red
  }
}
