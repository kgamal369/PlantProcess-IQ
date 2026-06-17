<#
====================================================================================================
 PlantProcess IQ - M1-T05 : one user per role via config        (pure ASCII; fixes .env.dev + validates)
====================================================================================================
 Backlog M1-T05 (Security / Auth-Users, dep M1-T02):
   Seed five users (admin/exec/engineer/operator/viewer) under PlantProcess__Auth__Users__N with known
   dev passwords; the app hashes them with Argon2id on load; bootstrap admin stays disabled. This gives
   e2e + the live demo a deterministic identity per role with zero runtime user creation.
 Acceptance:
   POST /auth/login for EACH user -> 200 + bearer token; each token's role matches the seeded role;
   the bootstrap path is NOT used (no provisioning) and a disabled bootstrap login returns 401.

 ---------------------------------------------------------------------------------------------------
 TWO THINGS THIS SCRIPT PROVES / FIXES THAT THE RAW BACKLOG TEXT DOES NOT SPELL OUT (verified in code):

 (1) DEFECT - "exec" cannot be seeded as Role=Executive.
     PlantRoles.NormalizePlantRole (Backend/PlantProcess.Api/Security/PlantAccessControl.cs) has NO
     "Executive" case; the switch default is  _ => Viewer. So a user seeded Role=Executive collapses to
     plantRole=Viewer and becomes indistinguishable from the viewer user - the per-role acceptance
     silently passes while the identity is wrong. This script rewrites exec to Role=CommercialAdmin
     (a real, recognized leadership/commercial plantRole; compatibility role = Admin). .env.dev is
     backed up first. After the fix the five normalized plantRoles are all DISTINCT.

 (2) ASSERT ON plantRole / ppiq_role, NOT the compatibility "role".
     LoginResponse.role and the JWT "role" claim carry the COMPATIBILITY role, which deliberately
     collapses many plantRoles onto a few legacy buckets:  SuperAdmin|TenantOwner|PlantAdmin|
     CommercialAdmin -> "Admin";  ProcessEngineer|QualityEngineer|ReliabilityEngineer|Operator ->
     "Engineer". So admin and exec BOTH report role="Admin", and engineer and operator BOTH report
     role="Engineer". The only claim that distinguishes all five is the REAL role: LoginResponse.plantRole
     and the JWT "ppiq_role" claim. This script asserts on those.

 The dev login path also REQUIRES the app DB to be migrated: AuthEndpoints calls ValidateUserAsync
 (a raw query over app_users) BEFORE the in-memory config fallback, with no try/catch - so if app_users
 is missing the endpoint 500s before the config user is ever resolved. Bring the app up with a migrated
 DB first:   .\deploy\scripts\ppiq.ps1 demo    (or  ppiq.ps1 migrate  then  ppiq.ps1 up).

 Run:   .\PPIQ_M1_T05_Implementation.ps1            (api at http://localhost:5063)
        .\PPIQ_M1_T05_Implementation.ps1 -ApiBase http://localhost:5063
====================================================================================================
#>

param(
  [string]$ApiBase = 'http://localhost:5063'
)

& {
  $ErrorActionPreference = 'Stop'
  Set-StrictMode -Version 2.0

  $script:Results = New-Object System.Collections.Generic.List[object]
  function Add-Result([string]$Check,[bool]$Pass,[string]$Detail){
    $script:Results.Add([pscustomobject]@{ Pass=$Pass; Check=$Check; Detail=$Detail })
    $tag = if($Pass){'PASS'}else{'FAIL'}; $col = if($Pass){'Green'}else{'Red'}
    Write-Host ("  [{0}] {1} :: {2}" -f $tag,$Check,$Detail) -ForegroundColor $col
  }
  function Info([string]$m){ Write-Host $m -ForegroundColor Cyan }
  function Warn([string]$m){ Write-Host $m -ForegroundColor Yellow }
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
  function Strip-Quotes([string]$v){
    if($null -eq $v){ return $v }
    $v = $v.Trim()
    if($v.Length -ge 2){
      $a = $v.Substring(0,1); $b = $v.Substring($v.Length-1,1)
      if(($a -eq '"' -and $b -eq '"') -or ($a -eq "'" -and $b -eq "'")){ return $v.Substring(1,$v.Length-2) }
    }
    return $v
  }
  function Write-LfNoBom([string]$Path,[string]$Text){
    $lf  = $Text -replace "`r`n","`n"
    [System.IO.File]::WriteAllText($Path,$lf,(New-Object System.Text.UTF8Encoding($false)))
  }

  $RepoRoot = Get-RepoRoot; Set-Location $RepoRoot
  $EnvFile  = Join-Path $RepoRoot 'deploy/compose/.env.dev'
  $BackupDir= Join-Path $RepoRoot '.ppiq-script-backups'

  Info "============================================================================"
  Info " PPIQ M1-T05 - per-role users + login validation"
  Info " repo root: $RepoRoot"
  Info " api base : $ApiBase"
  Info "============================================================================"

  if(-not (Test-Path $EnvFile)){
    Add-Result '.env.dev present' $false $EnvFile
    Warn "Cannot continue without deploy/compose/.env.dev (created by M1-T01/T02)."
    return
  }

  # ---- the contract: config Role -> normalized PlantRole (PlantRoles.NormalizePlantRole) -----------
  $NormMap = @{
    'Admin'='SuperAdmin'; 'DataManager'='DataEngineer'; 'Engineer'='ProcessEngineer'; 'Viewer'='Viewer';
    'SuperAdmin'='SuperAdmin'; 'TenantOwner'='TenantOwner'; 'PlantAdmin'='PlantAdmin';
    'DataEngineer'='DataEngineer'; 'ProcessEngineer'='ProcessEngineer'; 'QualityEngineer'='QualityEngineer';
    'ReliabilityEngineer'='ReliabilityEngineer'; 'Operator'='Operator'; 'CommercialAdmin'='CommercialAdmin';
    'Support'='Support'
  }
  function Norm([string]$role){ if($role -and $NormMap.ContainsKey($role)){ return $NormMap[$role] } else { return 'Viewer' } }

  # the five seeded identities and the role each MUST carry after the exec fix
  $Expected = @(
    [pscustomobject]@{ User='admin';    Role='Admin' },
    [pscustomobject]@{ User='exec';     Role='CommercialAdmin' },
    [pscustomobject]@{ User='engineer'; Role='Engineer' },
    [pscustomobject]@{ User='operator'; Role='Operator' },
    [pscustomobject]@{ User='viewer';   Role='Viewer' }
  )

  # ---- parse the auth users (UserName/Password/Role/IsBootstrapAdmin) out of .env.dev --------------
  $raw = Get-Content $EnvFile
  function Get-UserField([int]$idx,[string]$field){
    $pat = '^\s*PlantProcess__Auth__Users__' + $idx + '__' + $field + '\s*=\s*(.+?)\s*$'
    foreach($l in $raw){ if($l -match $pat){ return (Strip-Quotes $Matches[1]) } }
    return $null
  }
  $byName = @{}
  for($i=0; $i -lt 50; $i++){
    $u = Get-UserField $i 'UserName'
    if([string]::IsNullOrEmpty($u)){ if($i -gt 12){ break } else { continue } }
    $byName[$u.ToLower()] = [pscustomobject]@{
      Index = $i
      UserName = $u
      Password = (Get-UserField $i 'Password')
      Role = (Get-UserField $i 'Role')
      Boot = (Get-UserField $i 'IsBootstrapAdmin')
    }
  }

  Info "`n--- static: users found in .env.dev ---"
  foreach($e in $Expected){
    $have = $byName[$e.User]
    if($have){
      $pwShown = if([string]::IsNullOrEmpty($have.Password)){ '<none>' } else { '<set:' + $have.Password.Length + ' chars>' }
      Write-Host ("  [{0}] index={1} role={2} bootstrap={3} password={4}" -f $have.UserName,$have.Index,$have.Role,$have.Boot,$pwShown) -ForegroundColor Gray
    } else {
      Write-Host ("  [{0}] MISSING" -f $e.User) -ForegroundColor Red
    }
  }

  # ---- FIX: exec collapsing to Viewer (Role=Executive or anything unrecognized) -> CommercialAdmin --
  $execU = $byName['exec']
  if($execU){
    $collapses = (-not $NormMap.ContainsKey("" + $execU.Role)) -or ((Norm $execU.Role) -eq 'Viewer')
    if($collapses){
      New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
      $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
      Copy-Item $EnvFile (Join-Path $BackupDir (".env.dev." + $stamp + ".bak")) -Force
      $oldPat = '^\s*PlantProcess__Auth__Users__' + $execU.Index + '__Role\s*=.*$'
      $found = $false
      $newRaw = $raw | ForEach-Object {
        if($_ -match $oldPat){ $found = $true; 'PlantProcess__Auth__Users__' + $execU.Index + '__Role=CommercialAdmin' } else { $_ }
      }
      if(-not $found){
        # no Role line at all for exec -> insert one right after its UserName line
        $unPat = '^\s*PlantProcess__Auth__Users__' + $execU.Index + '__UserName\s*=.*$'
        $newRaw = $raw | ForEach-Object {
          if($_ -match $unPat){ $_; 'PlantProcess__Auth__Users__' + $execU.Index + '__Role=CommercialAdmin' } else { $_ }
        }
      }
      Write-LfNoBom $EnvFile (($newRaw -join "`n") + "`n")
      Warn ("  FIXED: exec Role '" + ("" + $execU.Role) + "' resolves to Viewer (invalid/unsupported) -> rewrote to CommercialAdmin (.env.dev backed up to .ppiq-script-backups)")
      $raw = Get-Content $EnvFile
      $byName['exec'].Role = 'CommercialAdmin'
    }
  }

  # ---- static gates -------------------------------------------------------------------------------
  Info "`n--- static gates ---"
  $allPresent = ($Expected | Where-Object { -not $byName.ContainsKey($_.User) }).Count -eq 0
  Add-Result 'all five users present (admin/exec/engineer/operator/viewer)' $allPresent (($byName.Keys | Sort-Object) -join ', ')

  $rolesValid = $true; $roleDetail = @()
  foreach($e in $Expected){
    $h = $byName[$e.User]; if(-not $h){ $rolesValid=$false; continue }
    if(-not $NormMap.ContainsKey("" + $h.Role)){ $rolesValid=$false; $roleDetail += ($e.User + "=" + $h.Role + "(invalid)") }
    else { $roleDetail += ($e.User + "->" + (Norm $h.Role)) }
  }
  Add-Result 'every seeded Role is a recognized PlantRole (none collapse via the switch default)' $rolesValid ($roleDetail -join '  ')

  # distinctness - the gate that actually catches the exec=Viewer collapse
  $normRoles = @(); foreach($e in $Expected){ $h=$byName[$e.User]; if($h){ $normRoles += (Norm $h.Role) } }
  $distinct = (($normRoles | Select-Object -Unique).Count -eq $normRoles.Count) -and ($normRoles.Count -eq 5)
  Add-Result 'the five normalized plantRoles are DISTINCT' $distinct ($normRoles -join ', ')

  $passwordsSet = $true
  foreach($e in $Expected){ $h=$byName[$e.User]; if($h -and [string]::IsNullOrEmpty($h.Password)){ $passwordsSet=$false } }
  Add-Result 'every seeded user has a dev password' $passwordsSet 'passwords are read from .env.dev for the live login below'

  $bootAllFalse = $true
  foreach($e in $Expected){ $h=$byName[$e.User]; if($h -and $h.Boot -and ("" + $h.Boot).ToLower() -ne 'false'){ $bootAllFalse=$false } }
  Add-Result 'no seeded user is IsBootstrapAdmin=true' $bootAllFalse 'all five carry IsBootstrapAdmin=false'

  $bootUserLine = ($raw | Where-Object { $_ -match '^\s*PlantProcess__Auth__BootstrapAdminUser\s*=\s*(.+)$' } | Select-Object -First 1)
  $bootPassLine = ($raw | Where-Object { $_ -match '^\s*PlantProcess__Auth__BootstrapAdminPassword\s*=\s*(.+)$' } | Select-Object -First 1)
  $bootName = Strip-Quotes (("" + $bootUserLine) -replace '^\s*PlantProcess__Auth__BootstrapAdminUser\s*=\s*','')
  $bootDisabled = (("" + $bootUserLine) -match 'disabled') -or (("" + $bootPassLine) -match '__DISABLED__|disabled')
  Add-Result 'bootstrap admin disabled in config (no provisioning path)' $bootDisabled ('BootstrapAdminUser=' + $bootName)

  # ---- LIVE: login per role (assert plantRole + JWT ppiq_role) + bootstrap rejection ---------------
  Info "`n--- live: POST $ApiBase/auth/login ---"
  $reachable = $false
  try { $null = Invoke-WebRequest -Uri ($ApiBase + '/health') -UseBasicParsing -TimeoutSec 4; $reachable=$true } catch {
    try { $null = Invoke-WebRequest -Uri $ApiBase -UseBasicParsing -TimeoutSec 4; $reachable=$true } catch { $reachable=$false }
  }

  if(-not $reachable){
    Warn "  API not reachable at $ApiBase."
    Warn "  Start it WITH a migrated DB first:  .\deploy\scripts\ppiq.ps1 demo   (or  ppiq.ps1 migrate  then  ppiq.ps1 up), then re-run."
    Warn "  The static gates above already stand on their own."
  } else {
    function Decode-JwtClaims([string]$jwt){
      try {
        $parts = $jwt.Split('.')
        if($parts.Count -lt 2){ return $null }
        $h = $parts[0].Replace('-','+').Replace('_','/'); switch($h.Length % 4){ 2{$h+='=='} 3{$h+='='} }
        $p = $parts[1].Replace('-','+').Replace('_','/'); switch($p.Length % 4){ 2{$p+='=='} 3{$p+='='} }
        $hdr = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($h)) | ConvertFrom-Json
        $pl  = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p)) | ConvertFrom-Json
        return [pscustomobject]@{ Alg=("" + $hdr.alg); PpiqRole=("" + $pl.ppiq_role); Role=("" + $pl.role); Tenant=("" + $pl.tenant_id) }
      } catch { return $null }
    }

    $sawDbError = $false
    foreach($e in $Expected){
      $h = $byName[$e.User]
      if(-not $h){ Add-Result ("login " + $e.User) $false 'user missing from .env.dev'; continue }
      $expectedPlant = Norm $h.Role
      $pw = "" + $h.Password
      $body = @{ userName=$h.UserName; password=$pw } | ConvertTo-Json -Compress
      $ok=$false; $detail=''
      try {
        $resp = Invoke-RestMethod -Uri ($ApiBase + '/auth/login') -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 12
        $tok = "" + $resp.accessToken
        $jwtOk = ($tok.Split('.').Count -eq 3 -and $tok.Length -gt 40)
        $plant = "" + $resp.plantRole
        $boot  = $resp.isBootstrapAdmin
        $claims = if($jwtOk){ Decode-JwtClaims $tok } else { $null }
        $jwtRole = if($claims){ $claims.PpiqRole } else { '' }
        $alg = if($claims){ $claims.Alg } else { '' }
        $bootFalse = ($boot -eq $false -or ("" + $boot) -eq 'False')
        $ok = ($jwtOk -and $plant -eq $expectedPlant -and $jwtRole -eq $expectedPlant -and $bootFalse)
        $detail = ("plantRole=" + $plant + " (expected " + $expectedPlant + "); jwt.ppiq_role=" + $jwtRole + "; jwt.alg=" + $alg + "; compatRole=" + ("" + $resp.role) + "; isBootstrap=" + ("" + $boot) + "; token=" + $(if($jwtOk){'JWT(3-part)'}else{'NOT a JWT'}))
      } catch {
        $code = $null; try { $code = $_.Exception.Response.StatusCode.value__ } catch {}
        if($code -eq 500){ $sawDbError = $true }
        $detail = "login failed (HTTP " + ("" + $code) + ")" + $(if($code -eq 500){' - app_users table likely missing; run ppiq.ps1 migrate (or demo) first'}else{''})
      }
      Add-Result ("login " + $e.User + " -> 200 + correct plantRole/ppiq_role") $ok $detail
    }

    if($sawDbError){
      Warn "  At least one login returned HTTP 500. AuthEndpoints queries app_users BEFORE the dev config fallback;"
      Warn "  a 500 means the app DB is not migrated. Run  .\deploy\scripts\ppiq.ps1 migrate  (or demo), then re-run."
    }

    # bootstrap must be rejected (disabled path)
    if([string]::IsNullOrEmpty($bootName)){ $bootName = 'bootstrap-disabled' }
    $rejected=$false; $bdetail=''
    try {
      $bbody = @{ userName=$bootName; password='this-account-is-disabled' } | ConvertTo-Json -Compress
      $null = Invoke-RestMethod -Uri ($ApiBase + '/auth/login') -Method Post -ContentType 'application/json' -Body $bbody -TimeoutSec 12
      $rejected=$false; $bdetail='login UNEXPECTEDLY succeeded for the bootstrap user'
    } catch {
      $code=$null; try { $code = $_.Exception.Response.StatusCode.value__ } catch {}
      $rejected = ($code -eq 401 -or $code -eq 400 -or $code -eq 403)
      $bdetail = "bootstrap '" + $bootName + "' login rejected (HTTP " + ("" + $code) + ")"
    }
    Add-Result 'disabled bootstrap user cannot log in (401/400/403)' $rejected $bdetail
  }

  # ---- ledger -------------------------------------------------------------------------------------
  Info "`n============================================================================"
  Info " RESULT LEDGER - M1-T05"
  Info "============================================================================"
  $script:Results | Format-Table Pass,Check,Detail -AutoSize | Out-String | Write-Host
  $fail = @($script:Results | Where-Object { -not $_.Pass })
  if($fail.Count -eq 0){
    if($reachable){
      Write-Host "M1-T05 GREEN - five DISTINCT role identities seeded and verified live; bootstrap path is dead." -ForegroundColor Green
      Write-Host "Commit:  git add deploy/compose/.env.dev ; git commit -m 'M1-T05: five per-role dev users (exec=CommercialAdmin)'" -ForegroundColor DarkGray
    } else {
      Write-Host "M1-T05 static gates GREEN - bring the app up (ppiq.ps1 demo) and re-run to complete the live login gates." -ForegroundColor Yellow
    }
  } else {
    Write-Host ("{0} CHECK(S) FAILED - see ledger." -f $fail.Count) -ForegroundColor Red
  }
}
