# ============================================================================
# M1-19  Verify-OracleDiscovery.ps1
# Backlog v23 M1-19 - "Oracle live FOR REAL" (senior rec 12).
#
# THE RULE THIS ENFORCES: until discovery is proven green, the phrase "live
# Oracle connector" is banned from every customer-facing sentence. This
# script is the thing that decides whether you have earned the phrase.
#
# WHAT IT DOES (read-only by default):
#   1. containers   - are the two Oracle source containers actually up?
#   2. profiles     - list every Oracle connection profile and show the
#                     schema field that is empty and causing the red banner
#   3. -SetSchema   - OPTIONAL: patch schema=PPIQ_SRC through the product's
#                     own API (not raw SQL). Prefer doing it in the UI: it is
#                     two fields, and the UI path is what you demo.
#   4. test-connect - POST /admin/connectors/connection-profiles/{id}/test
#   5. discovery    - GET  /admin/connectors/connection-profiles/{id}/tables
#                     (the exact call that returns the red banner today)
#   6. VERDICT      - per profile: EARNED / NOT EARNED, with the evidence
#
# ACCEPTANCE (v23 M1-19): discovery lists PPIQ_SRC tables for BOTH profiles
# in the UI, dated screenshot, zero red banners on the connections walk.
# This script proves the API half; the screenshot is yours to take.
#
# Run from repo root (API up on the presentation profile):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify-OracleDiscovery.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify-OracleDiscovery.ps1 -SetSchema
# ============================================================================
[CmdletBinding()]
param(
    [string]$ApiBase = 'http://localhost:5063',
    [string]$ApiUser = 'e2eadmin',
    [string]$ApiPassword = 'E2EAdmin123!',
    [string]$Schema = 'PPIQ_SRC',
    [switch]$SetSchema
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$Report = Join-Path $RepoRoot ('M1-19_Oracle_' + $Stamp + '.txt')
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s); Write-Host $s }
function Save { [System.IO.File]::WriteAllText($Report, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false))) }

W ("M1-19 ORACLE DISCOVERY PROOF - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ("API: " + $ApiBase + "   target schema: " + $Schema)
W ("=" * 78)
W ""

# ---- 1. containers ---------------------------------------------------------
W "[1/5] Oracle source containers:"
$dockerOk = $false
if (Get-Command docker -ErrorAction SilentlyContinue) {
    $ps = @(& docker ps --format "{{.Names}}|{{.Status}}" 2>&1)
    $oracles = @($ps | Where-Object { $_ -match 'oracle' })
    if (@($oracles).Count -eq 0) {
        W "      NO Oracle containers running."
        W "      Expected: ppiq-src-caster-oracle, ppiq-src-hsm-oracle"
        W "      Start them before anything below can pass."
    } else {
        foreach ($o in $oracles) { W ("      " + $o) }
        $dockerOk = $true
    }
} else {
    W "      docker not on PATH - cannot verify containers (continuing)."
}
W ""

# ---- 2. auth ---------------------------------------------------------------
$token = $null
foreach ($body in @(
        (@{ username = $ApiUser; password = $ApiPassword } | ConvertTo-Json),
        (@{ email = $ApiUser; password = $ApiPassword } | ConvertTo-Json))) {
    try {
        $r = Invoke-RestMethod -Uri ($ApiBase + '/api/auth/login') -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 10 -ErrorAction Stop
        if ($r.PSObject.Properties['accessToken']) { $token = $r.accessToken; break }
        if ($r.PSObject.Properties['token']) { $token = $r.token; break }
    } catch { }
}
if (-not $token) {
    foreach ($body in @((@{ username = $ApiUser; password = $ApiPassword } | ConvertTo-Json))) {
        try {
            $r = Invoke-RestMethod -Uri ($ApiBase + '/auth/login') -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 10 -ErrorAction Stop
            if ($r.PSObject.Properties['accessToken']) { $token = $r.accessToken }
            elseif ($r.PSObject.Properties['token']) { $token = $r.token }
        } catch { }
    }
}
if (-not $token) {
    W "[ABORT] could not authenticate. Is the API up on this port?"
    W ("        try: .\scripts\run\start-api.ps1 -Profile presentation   (binds :5063)")
    Save; exit 1
}
$H = @{ Authorization = 'Bearer ' + $token }
W "[2/5] authenticated OK."
W ""

# ---- 3. profiles -----------------------------------------------------------
W "[3/5] connection profiles:"
$profiles = $null
foreach ($u in @('/admin/connectors/connection-profiles?includeSecrets=true', '/admin/connectors/connection-profiles')) {
    try {
        $profiles = Invoke-RestMethod -Uri ($ApiBase + $u) -Headers $H -TimeoutSec 15 -ErrorAction Stop
        break
    } catch { }
}
if ($null -eq $profiles) { W "[ABORT] could not list connection profiles."; Save; exit 1 }

$list = $profiles
if ($profiles.PSObject.Properties['items']) { $list = $profiles.items }
elseif ($profiles.PSObject.Properties['profiles']) { $list = $profiles.profiles }
elseif ($profiles.PSObject.Properties['data']) { $list = $profiles.data }

function Prop($o, [string[]]$names) {
    foreach ($n in $names) { if ($o.PSObject.Properties[$n]) { return $o.$n } }
    return $null
}

$oracleProfiles = @()
foreach ($p in @($list)) {
    $prov = [string](Prop $p @('providerType', 'provider', 'providerTypeCode', 'connectorType'))
    $code = [string](Prop $p @('connectionProfileCode', 'code', 'profileCode'))
    $name = [string](Prop $p @('connectionProfileName', 'name', 'profileName'))
    $sch  = [string](Prop $p @('schemaName', 'schema', 'defaultSchema'))
    $id   = [string](Prop $p @('id', 'connectionProfileId'))
    $line = "      " + $code.PadRight(8) + " " + $prov.PadRight(12) + " schema='" + $sch + "'  " + $name
    W $line
    if ($prov -match '(?i)oracle') {
        $oracleProfiles += [pscustomobject]@{ Id = $id; Code = $code; Name = $name; Schema = $sch; Raw = $p }
    }
}
W ""
if ($oracleProfiles.Count -eq 0) { W "[ABORT] no Oracle profiles found."; Save; exit 1 }
W ("      Oracle profiles: " + (($oracleProfiles | ForEach-Object { $_.Code }) -join ', '))
$missing = @($oracleProfiles | Where-Object { [string]::IsNullOrWhiteSpace($_.Schema) -or $_.Schema -ne $Schema })
if ($missing.Count -gt 0) {
    W ("      SCHEMA MISSING/WRONG on: " + (($missing | ForEach-Object { $_.Code }) -join ', ') + "   <-- this is the red banner")
} else {
    W ("      schema '" + $Schema + "' already set on all Oracle profiles.")
}
W ""

# ---- 3b. optional patch ----------------------------------------------------
if ($SetSchema -and $missing.Count -gt 0) {
    W ("[3b] -SetSchema: patching schema='" + $Schema + "' through the product API...")
    W "     (the UI path is what you demo - this is the shortcut, not the story)"
    foreach ($op in $missing) {
        $obj = $op.Raw
        foreach ($n in @('schemaName', 'schema', 'defaultSchema')) {
            if ($obj.PSObject.Properties[$n]) { $obj.$n = $Schema }
        }
        if (-not ($obj.PSObject.Properties['schemaName'] -or $obj.PSObject.Properties['schema'])) {
            $obj | Add-Member -NotePropertyName 'schemaName' -NotePropertyValue $Schema -Force
        }
        $payload = $obj | ConvertTo-Json -Depth 8
        $done = $false
        foreach ($m in @('Put', 'Patch')) {
            try {
                Invoke-RestMethod -Uri ($ApiBase + '/admin/connectors/connection-profiles/' + $op.Id) -Method $m -Headers $H -Body $payload -ContentType 'application/json' -TimeoutSec 15 -ErrorAction Stop | Out-Null
                W ("     " + $op.Code + ": " + $m.ToUpper() + " OK")
                $done = $true; break
            } catch {
                W ("     " + $op.Code + ": " + $m.ToUpper() + " failed - " + $_.Exception.Message)
            }
        }
        if (-not $done) {
            W ("     " + $op.Code + ": DO IT IN THE UI - Connections -> Edit -> Schema = " + $Schema + " -> Save")
        }
    }
    W ""
}

# ---- 4 + 5. test + discovery per profile ------------------------------------
W "[4/5] test-connect + [5/5] live discovery:"
W ""
$results = @()
foreach ($op in $oracleProfiles) {
    W ("---- " + $op.Code + "  (" + $op.Name + ") ----")
    # re-read the profile so the verdict reflects reality, not our local copy
    $curSchema = $op.Schema
    try {
        $fresh = Invoke-RestMethod -Uri ($ApiBase + '/admin/connectors/connection-profiles/' + $op.Id) -Headers $H -TimeoutSec 10 -ErrorAction Stop
        $s = [string](Prop $fresh @('schemaName', 'schema', 'defaultSchema'))
        if ($s) { $curSchema = $s }
    } catch { }
    W ("    schema now: '" + $curSchema + "'")

    $testOk = $false; $testMsg = ''
    try {
        $t = Invoke-RestMethod -Uri ($ApiBase + '/admin/connectors/connection-profiles/' + $op.Id + '/test') -Method Post -Headers $H -TimeoutSec 30 -ErrorAction Stop
        $succ = Prop $t @('success', 'isSuccess', 'ok', 'connected')
        $testOk = ($null -eq $succ) -or ([bool]$succ)
        $testMsg = [string](Prop $t @('message', 'detail', 'error'))
        W ("    test-connect: " + $(if ($testOk) { 'PASS' } else { 'FAIL' }) + $(if ($testMsg) { '  (' + $testMsg + ')' } else { '' }))
    } catch {
        W ("    test-connect: FAIL - " + $_.Exception.Message)
    }

    $tableCount = -1; $sample = @()
    try {
        $d = Invoke-RestMethod -Uri ($ApiBase + '/admin/connectors/connection-profiles/' + $op.Id + '/tables') -Headers $H -TimeoutSec 45 -ErrorAction Stop
        $tl = $d
        if ($d.PSObject.Properties['tables']) { $tl = $d.tables }
        elseif ($d.PSObject.Properties['items']) { $tl = $d.items }
        elseif ($d.PSObject.Properties['data']) { $tl = $d.data }
        $tableCount = @($tl).Count
        $sample = @($tl | Select-Object -First 6 | ForEach-Object {
                $n = Prop $_ @('name', 'tableName', 'objectName')
                if ($n) { [string]$n } else { [string]$_ }
            })
        W ("    discovery: PASS - " + $tableCount + " object(s)")
        foreach ($s in $sample) { W ("        " + $s) }
    } catch {
        $msg = $_.Exception.Message
        $body = ''
        try {
            $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $body = $sr.ReadToEnd()
        } catch { }
        W ("    discovery: FAIL - " + $msg)
        if ($body) { W ("        body: " + $body.Substring(0, [Math]::Min(220, $body.Length))) }
    }

    $earned = ($testOk -and $tableCount -gt 0 -and $curSchema -eq $Schema)
    $results += [pscustomobject]@{ Code = $op.Code; Schema = $curSchema; Test = $testOk; Tables = $tableCount; Earned = $earned }
    W ("    VERDICT: " + $(if ($earned) { 'EARNED - this profile may be called live' } else { 'NOT EARNED' }))
    W ""
}

# ---- verdict ---------------------------------------------------------------
W "=" * 78
W "M1-19 ACCEPTANCE:"
W ("{0,-8} {1,-12} {2,-6} {3,-8} {4}" -f 'Profile', 'Schema', 'Test', 'Tables', 'Verdict')
foreach ($r in $results) {
    W ("{0,-8} {1,-12} {2,-6} {3,-8} {4}" -f $r.Code, $r.Schema, $r.Test, $r.Tables, $(if ($r.Earned) { 'EARNED' } else { 'NOT EARNED' }))
}
$allEarned = (@($results | Where-Object { -not $_.Earned }).Count -eq 0)
W ""
if ($allEarned) {
    W "PASS - both Oracle profiles discover live objects."
    W "You may now say 'live Oracle connector' to a customer."
    W "REMAINING FOR ACCEPTANCE: take the dated screenshot of the discovery"
    W "list in the HMI (Prepare Import -> HSM Level 2 / Continuous Caster)."
} else {
    W "FAIL - the phrase 'live Oracle connector' stays banned (senior rec 12)."
    W ""
    W "Most likely fix, 30 seconds in the UI:"
    W "    Connections -> CP-04 (HSM Level 2, Oracle) -> Edit"
    W ("    Schema Name = " + $Schema + "  -> Save")
    W "    ...repeat for CP-06 (Continuous Caster, Oracle). Then re-run this."
    W ""
    W "Credentials for reference: ppiq_src / ppiq_src_local_only @ FREEPDB1"
    if (-not $dockerOk) { W "ALSO: the Oracle containers were not confirmed running - start them first." }
}
Save
Write-Host ""
Write-Host ("[DONE] Report -> " + $Report) -ForegroundColor Green
if ($allEarned) { exit 0 } else { exit 1 }
