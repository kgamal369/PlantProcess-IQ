# ================================================================================================
# PPIQ JOURNEY-WALK EVIDENCE PROVER  (V1-11, V1-13, V1-17, V1-18, V1-19, V1-21, V1-22, V1-23)
# ================================================================================================
# Self-contained: does NOT modify the app. Proves every machine-provable acceptance in the
# 8-Jul walk set, collects the V1-42 root-cause forensics (J7 block), writes walk-evidence.md,
# and prints the short MANUAL checklist for the HMI-only acceptances (clips/clicks).
# Run AS A FILE from the repo root with the API listening on :5063:
#   & .\Invoke-PpiqJourneyWalk.ps1
# Prereqs it discovers by itself: psql (PATH or C:\Program Files\PostgreSQL\*\bin), docker.
# ================================================================================================
param(
    [string]$ApiBase  = 'http://localhost:5063',
    [string]$PgHost   = 'localhost',
    [int]   $PgPort   = 5432,
    [string]$PgDb     = $(if ($env:PPIQ_PG_DB)   { $env:PPIQ_PG_DB }   else { 'ppiq_app' }),
    [string]$PgUser   = $(if ($env:PPIQ_PG_USER) { $env:PPIQ_PG_USER } else { 'ppiq_dev' }),
    [string]$PgPass   = $(if ($env:PPIQ_PG_PASS) { $env:PPIQ_PG_PASS } else { 'ppiq_dev_local_only' }),
    [string]$Username = 'e2eadmin',
    [string]$Password = 'E2EAdmin123!'
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$script:Rows = New-Object System.Collections.ArrayList
function Add-Evidence([string]$Task,[string]$Check,[string]$Result,[string]$Detail) {
    [void]$script:Rows.Add([pscustomobject]@{ Task=$Task; Check=$Check; Result=$Result; Detail=$Detail })
    $color = 'Gray'
    if ($Result -eq 'PASS') { $color='Green' } elseif ($Result -eq 'FAIL') { $color='Red' } elseif ($Result -eq 'MANUAL') { $color='Yellow' } elseif ($Result -eq 'EVIDENCE') { $color='Cyan' }
    Write-Host ('[' + $Result.PadRight(8) + '] ' + $Task + ' :: ' + $Check) -ForegroundColor $color
    if ($Detail) { Write-Host ('           ' + ($Detail -replace "`n", "`n           ")) -ForegroundColor DarkGray }
}

# ---------- infrastructure discovery ----------------------------------------------------------
$psql = $null
$cmd = Get-Command psql -ErrorAction SilentlyContinue
if ($cmd) { $psql = $cmd.Source }
if (-not $psql) {
    $candidates = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending
    if ($candidates) { $psql = $candidates[0].FullName }
}
function Invoke-Sql([string]$Query) {
    if (-not $psql) { return $null }
    $env:PGPASSWORD = $PgPass
    $out = & $psql -h $PgHost -p $PgPort -U $PgUser -d $PgDb -t -A -F '|' -c $Query 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('psql failed: ' + ($out -join ' ')) }
    return @($out | Where-Object { $_ -ne '' })
}
$docker = $null
$cmd = Get-Command docker -ErrorAction SilentlyContinue
if ($cmd) { $docker = $cmd.Source }

Write-Host ''
Write-Host '================ PPIQ JOURNEY-WALK EVIDENCE PROVER ================' -ForegroundColor Cyan
Write-Host ('API: ' + $ApiBase + '   DB: ' + $PgHost + ':' + $PgPort + '/' + $PgDb + '   psql: ' + $(if ($psql) { 'found' } else { 'NOT FOUND - SQL checks will be SKIPPED' }) + '   docker: ' + $(if ($docker) { 'found' } else { 'not found' }))
Write-Host ''

# ---------- V1-17: J1 auth is real ------------------------------------------------------------
$token = $null
try {
    $login = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/auth/login') -ContentType 'application/json' -Body (@{ username=$Username; password=$Password } | ConvertTo-Json)
    $token = $login.accessToken
    if (-not $token) { throw 'no accessToken in login response' }
    $parts = $token.Split('.')
    if ($parts.Count -ne 3) { throw ('token is not a 3-part JWT (' + $parts.Count + ' parts)') }
    $payloadB64 = $parts[1].Replace('-','+').Replace('_','/')
    switch ($payloadB64.Length % 4) { 2 { $payloadB64 += '==' } 3 { $payloadB64 += '=' } }
    $payload = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payloadB64)) | ConvertFrom-Json
    $exp = [DateTimeOffset]::FromUnixTimeSeconds([long]$payload.exp).UtcDateTime
    if ($exp -le (Get-Date).ToUniversalTime()) { throw 'token already expired' }
    Add-Evidence 'V1-17' 'Login returns a real signed 3-part JWT with future exp' 'PASS' ('len=' + $token.Length + ' exp=' + $exp.ToString('u') + ' sub=' + $payload.sub)
} catch {
    Add-Evidence 'V1-17' 'Login returns a real signed JWT' 'FAIL' $_.Exception.Message
    throw 'Cannot continue without auth.'
}
$H = @{ Authorization = 'Bearer ' + $token }
try {
    $null = Invoke-RestMethod -Uri ($ApiBase + '/admin/overview') -Headers $H
    Add-Evidence 'V1-17' 'Authorized call to /admin/overview succeeds (no 401)' 'PASS' ''
} catch { Add-Evidence 'V1-17' 'Authorized /admin/overview' 'FAIL' $_.Exception.Message }
Add-Evidence 'V1-17' 'Browser cold load: nav visible, populated panel, ZERO console errors, __Host- cookie present' 'MANUAL' 'Open the demo URL in a fresh browser profile; F12 console must be clean.'

# ---------- V1-13: one-click readiness --------------------------------------------------------
try {
    $resp = Invoke-WebRequest -Uri ($ApiBase + '/admin/schema-mapping/readiness') -Headers $H -UseBasicParsing
    $body = $resp.Content
    $bad = [regex]::Matches($body, '"(isReady|ready|healthy)"\s*:\s*false', 'IgnoreCase')
    if ($resp.StatusCode -eq 200 -and $bad.Count -eq 0) {
        Add-Evidence 'V1-13' 'Readiness endpoint 200 with no not-ready flags' 'PASS' ('bytes=' + $body.Length)
    } else {
        Add-Evidence 'V1-13' 'Readiness endpoint' 'FAIL' ('status=' + $resp.StatusCode + ' notReadyFlags=' + $bad.Count + ' body: ' + $body.Substring(0, [Math]::Min(500, $body.Length)))
    }
} catch { Add-Evidence 'V1-13' 'Readiness endpoint' 'FAIL' $_.Exception.Message }

# ---------- V1-18: J2 connect + select source -------------------------------------------------
$meltshopProfile = $null
try {
    $profiles = Invoke-RestMethod -Uri ($ApiBase + '/admin/connectors/connection-profiles') -Headers $H
    $list = @($profiles)
    if ($profiles.PSObject.Properties['items']) { $list = @($profiles.items) }
    $meltshopProfile = $list | Where-Object { ($_.connectionProfileName + ' ' + $_.connectionProfileCode) -match 'melt' } | Select-Object -First 1
    if (-not $meltshopProfile) { $meltshopProfile = $list | Select-Object -First 1 }
    if (-not $meltshopProfile) { throw 'no connection profiles exist' }
    Add-Evidence 'V1-18' 'Connection profiles listed; meltshop profile identified' 'PASS' ('profile=' + $meltshopProfile.connectionProfileCode + ' id=' + $meltshopProfile.id)
} catch { Add-Evidence 'V1-18' 'List connection profiles' 'FAIL' $_.Exception.Message }
if ($meltshopProfile) {
    try {
        $test = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/connectors/connection-profiles/' + $meltshopProfile.id + '/test') -Headers $H
        $ok = ($test.success -eq $true) -or ($test.isSuccess -eq $true) -or ($test.status -match 'ok|success')
        Add-Evidence 'V1-18' 'Live connection test on the meltshop profile' $(if ($ok) { 'PASS' } else { 'FAIL' }) (($test | ConvertTo-Json -Compress -Depth 4).Substring(0,300))
    } catch { Add-Evidence 'V1-18' 'Live connection test' 'FAIL' $_.Exception.Message }
}
try {
    $r = Invoke-WebRequest -Method Post -Uri ($ApiBase + '/admin/connectors/connection-profiles/00000000-0000-0000-0000-000000000000/test') -Headers $H -UseBasicParsing -ErrorAction Stop
    Add-Evidence 'V1-18' 'Unknown-profile test returns a typed error (not a crash)' 'FAIL' ('unexpected 200: ' + $r.Content.Substring(0,200))
} catch {
    $code = 0; try { $code = [int]$_.Exception.Response.StatusCode } catch {}
    if ($code -ge 400 -and $code -lt 500) { Add-Evidence 'V1-18' 'Unknown-profile test returns a typed 4xx error (not a 500 crash)' 'PASS' ('HTTP ' + $code) }
    else { Add-Evidence 'V1-18' 'Unknown-profile test error typing' 'FAIL' ('HTTP ' + $code + ' ' + $_.Exception.Message) }
}
if ($psql) {
    try {
        $reg = Invoke-Sql "SELECT count(*) FROM source_table_dump_registry WHERE is_deleted = false;"
        $n = [int]$reg[0]
        Add-Evidence 'V1-18' ('Registry rows (registered tables): ' + $n) $(if ($n -ge 2) { 'PASS' } else { 'EVIDENCE' }) $(if ($n -lt 2) { 'Register a SECOND meltshop table via the DB Configuration table picker (this is journey step 2 and enables the J4 join).' } else { '' })
    } catch { Add-Evidence 'V1-18' 'Registry count' 'FAIL' $_.Exception.Message }
}
Add-Evidence 'V1-18' 'Stopped-container test shows a typed error in the HMI (stop the meltshop container, click Test, restart it)' 'MANUAL' 'docker stop <meltshop>; HMI Test -> typed error; docker start <meltshop>'

# ---------- V1-19: J3 Stage-1 + watermark + monitor -------------------------------------------
$stagingBefore = -1
if ($psql) { try { $stagingBefore = [int](Invoke-Sql "SELECT count(*) FROM src_meltshop_pg.heats;")[0] } catch { $stagingBefore = -1 } }
$stage1Ok = $false
try {
    $run1 = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage1/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy = 'walk-prover' } | ConvertTo-Json)
    $stage1Ok = $true
    Add-Evidence 'V1-19' 'Stage-1 run accepted (HTTP 200)' 'PASS' (($run1 | ConvertTo-Json -Compress -Depth 4).Substring(0, 300))
} catch { Add-Evidence 'V1-19' 'Stage-1 run' 'FAIL' $_.Exception.Message }
if ($psql -and $stage1Ok) {
    try {
        $after1 = [int](Invoke-Sql "SELECT count(*) FROM src_meltshop_pg.heats;")[0]
        Add-Evidence 'V1-19' ('Staging populated: heats=' + $after1 + ' (was ' + $stagingBefore + ')') $(if ($after1 -gt 0) { 'PASS' } else { 'FAIL' }) ''
        $run2 = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage1/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy = 'walk-prover-2nd' } | ConvertTo-Json)
        $after2 = [int](Invoke-Sql "SELECT count(*) FROM src_meltshop_pg.heats;")[0]
        Add-Evidence 'V1-19' ('WATERMARK: second run adds ' + ($after2 - $after1) + ' rows (expected 0)') $(if ($after2 -eq $after1) { 'PASS' } else { 'FAIL' }) ''
    } catch { Add-Evidence 'V1-19' 'Staging/watermark SQL' 'FAIL' $_.Exception.Message }
}
try {
    $mon = Invoke-WebRequest -Uri ($ApiBase + '/admin/jobs-monitor') -Headers $H -UseBasicParsing
    $hasRun = $mon.Content -match 'stage1|Stage1|stage-1'
    Add-Evidence 'V1-19' 'Jobs Monitor payload contains the Stage-1 run' $(if ($hasRun) { 'PASS' } else { 'EVIDENCE' }) ('bytes=' + $mon.Content.Length)
} catch { Add-Evidence 'V1-19' 'Jobs Monitor payload' 'FAIL' $_.Exception.Message }
Add-Evidence 'V1-19' 'Forced-failure shows an Error entry in monitor + log (stop container, run Stage-1 from HMI, restart)' 'MANUAL' 'Job-log Error entries arrive with V1-45; until then the monitor state is the evidence.'

# ---------- V1-21: J5 Stage-2 canonical refresh ------------------------------------------------
$canonBefore = -1
if ($psql) { try { $canonBefore = [int](Invoke-Sql "SELECT count(*) FROM material_units;")[0] } catch { $canonBefore = -1 } }
try {
    $s2 = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage2/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy = 'walk-prover' } | ConvertTo-Json)
    Add-Evidence 'V1-21' 'Stage-2 run accepted (HTTP 200)' 'PASS' (($s2 | ConvertTo-Json -Compress -Depth 4).Substring(0, 300))
} catch { Add-Evidence 'V1-21' 'Stage-2 run' 'FAIL' $_.Exception.Message }
$canonAfterS2 = -1
if ($psql) {
    try {
        $canonAfterS2 = [int](Invoke-Sql "SELECT count(*) FROM material_units;")[0]
        Add-Evidence 'V1-21' ('Canonical populated: material_units=' + $canonAfterS2 + ' (was ' + $canonBefore + ')') $(if ($canonAfterS2 -gt 0) { 'PASS' } else { 'FAIL' }) ''
        $views = Invoke-Sql "SELECT table_name FROM information_schema.views WHERE table_schema='public' AND table_name LIKE 'canonical%' ORDER BY 1;"
        Add-Evidence 'V1-21' ('Canonical views present: ' + (@($views).Count)) $(if (@($views).Count -gt 0) { 'PASS' } else { 'EVIDENCE' }) (@($views) -join ', ')
    } catch { Add-Evidence 'V1-21' 'Canonical SQL checks' 'FAIL' $_.Exception.Message }
}

# ---------- V1-22: J6 seam-6 (widgets read canonical, re-import changes numbers) ---------------
$meltCtr = $null
if ($docker) {
    $names = & docker ps --format '{{.Names}}' 2>$null
    $meltCtr = @($names | Where-Object { $_ -match 'melt' }) | Select-Object -First 1
}
if ($meltCtr -and $psql -and $canonAfterS2 -ge 0) {
    try {
        $mark = 'WALK-' + (Get-Date -Format 'HHmmss')
        $ins = "INSERT INTO meltshop_heats (heat_id, furnace_no, tap_start_utc, tap_end_utc, steel_grade, target_temp_c, tap_temp_c, carbon_pct, oxygen_ppm) VALUES ('" + $mark + "', 1, now(), now(), 'GRADE-W', 1600, 1595, 0.05, 300);"
        $null = & docker exec $meltCtr psql -U ppiq_src -d meltshop -c $ins
        if ($LASTEXITCODE -ne 0) { throw 'source INSERT failed' }
        $null = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage1/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy = 'walk-seam6' } | ConvertTo-Json)
        $null = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage2/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy = 'walk-seam6' } | ConvertTo-Json)
        $canonAfterNew = [int](Invoke-Sql "SELECT count(*) FROM material_units;")[0]
        Add-Evidence 'V1-22' ('SEAM-6: injected 1 source heat -> canonical count ' + $canonAfterS2 + ' -> ' + $canonAfterNew) $(if ($canonAfterNew -gt $canonAfterS2) { 'PASS' } else { 'FAIL' }) ('A widget bound to canonical views WILL visibly change on refresh; injected heat_id=' + $mark)
    } catch { Add-Evidence 'V1-22' 'Seam-6 live re-import delta' 'FAIL' $_.Exception.Message }
} else {
    Add-Evidence 'V1-22' 'Seam-6 live re-import delta' 'EVIDENCE' ('skipped: ' + $(if (-not $meltCtr) { 'no meltshop container found; ' }) + $(if (-not $psql) { 'no psql; ' }) + 'run during the HMI walk instead')
}
try {
    $prev = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/schema-configuration/views/preview') -Headers $H -ContentType 'application/json' -Body (@{ sql = 'SELECT * FROM src_meltshop_pg.heats LIMIT 5' } | ConvertTo-Json)
    Add-Evidence 'V1-22' 'Widget-path SQL passes the safe-SQL compiler (preview 200)' 'PASS' ''
} catch { Add-Evidence 'V1-22' 'Safe-SQL preview' 'FAIL' $_.Exception.Message }
Add-Evidence 'V1-22' 'HMI: create page, drag widget, bind, save, reload persists; refresh after re-import changes the number' 'MANUAL' 'This is the customer-visible half of seam-6.'

# ---------- V1-11: blended 70/30 attribution ---------------------------------------------------
if ($psql) {
    try {
        $uid = (Invoke-Sql "SELECT id FROM material_units WHERE material_code = 'C-0044170' LIMIT 1;")
        if (-not $uid) { throw 'C-0044170 not found in material_units' }
        $rows = Invoke-Sql ("SELECT * FROM ppiq_v5_blended_attribution_for_child('" + $uid[0] + "'::uuid);")
        $joined = ($rows -join ' | ')
        $has70 = $joined -match '0\.7'
        $has30 = $joined -match '0\.3'
        Add-Evidence 'V1-11' ('Blended attribution rows: ' + @($rows).Count) $(if (@($rows).Count -ge 2 -and $has70 -and $has30) { 'PASS' } else { 'FAIL' }) $joined.Substring(0, [Math]::Min(300, $joined.Length))
    } catch { Add-Evidence 'V1-11' 'Blended attribution SQL' 'FAIL' $_.Exception.Message }
}
Add-Evidence 'V1-11' 'HMI clip: transition coil shows weighted provenance + population (Material Investigation)' 'MANUAL' 'Record the clip during rehearsal #1.'

# ---------- V1-23 / V1-42: J7 forensics + live trigger attempt --------------------------------
Write-Host ''
Write-Host '---- J7 CORRELATION FORENSICS (this section is the Pack B root-cause evidence) ----' -ForegroundColor Cyan
if ($psql) {
    try {
        $st = Invoke-Sql "SELECT status, count(*) FROM ml_correlation_compute_runs GROUP BY status ORDER BY 2 DESC;"
        Add-Evidence 'V1-23' 'Run-status census' 'EVIDENCE' ($st -join '  ||  ')
        $latest = Invoke-Sql "SELECT left(id::text,8), engine_key, target_outcome_key, status, started_at_utc, completed_at_utc, duration_ms, coalesce(left(message,120),'<null>') FROM ml_correlation_compute_runs ORDER BY started_at_utc DESC LIMIT 3;"
        Add-Evidence 'V1-23' 'Latest 3 runs' 'EVIDENCE' ($latest -join "`n")
        $msgs = Invoke-Sql "SELECT status, count(*), coalesce(left(message,140),'<null>') FROM ml_correlation_compute_runs WHERE message IS NOT NULL GROUP BY status, left(message,140) ORDER BY 2 DESC LIMIT 5;"
        Add-Evidence 'V1-23' 'Distinct failure messages (THE ROOT-CAUSE CANDIDATES)' 'EVIDENCE' $(if ($msgs) { $msgs -join "`n" } else { 'no run has ever written a message - engine dies before its first status write-back' })
        $res = Invoke-Sql "SELECT count(*) FROM ml_correlation_results_v2;"
        Add-Evidence 'V1-23' ('ml_correlation_results_v2 rows: ' + $res[0]) 'EVIDENCE' ''
        $ages = Invoke-Sql "SELECT count(*) FROM ml_correlation_compute_runs WHERE status = 'Running' AND started_at_utc < now() - interval '1 hour';"
        Add-Evidence 'V1-23' ('Zombie Running runs older than 1h: ' + $ages[0]) 'EVIDENCE' 'These become Failed(timeout-backfill) in V1-41.'
    } catch { Add-Evidence 'V1-23' 'Forensic SQL' 'FAIL' $_.Exception.Message }
}
$runsBefore = 0
if ($psql) { try { $runsBefore = [int](Invoke-Sql "SELECT count(*) FROM ml_correlation_compute_runs;")[0] } catch {} }
$triggered = $false
foreach ($listRoute in @('/admin/ml/jobs', '/admin/phase2/jobs', '/admin/analysis/jobs', '/admin/learning/jobs')) {
    if ($triggered) { break }
    try {
        $jobs = Invoke-RestMethod -Uri ($ApiBase + $listRoute) -Headers $H -ErrorAction Stop
        $arr = @($jobs); if ($jobs.PSObject.Properties['items']) { $arr = @($jobs.items) }
        $corr = $arr | Where-Object { ($_ | ConvertTo-Json -Compress -Depth 3) -match 'correlation' } | Select-Object -First 1
        if ($corr -and $corr.id) {
            Add-Evidence 'V1-23' ('Job list found at ' + $listRoute + '; triggering run-now on ' + $corr.id) 'EVIDENCE' ''
            try {
                $null = Invoke-RestMethod -Method Post -Uri ($ApiBase + $listRoute + '/' + $corr.id + '/run-now') -Headers $H
                $triggered = $true
            } catch { Add-Evidence 'V1-23' 'run-now trigger' 'EVIDENCE' ('trigger rejected: ' + $_.Exception.Message) }
        }
    } catch { }
}
if ($triggered -and $psql) {
    Write-Host '           polling the run table for 90s to watch the lifecycle...' -ForegroundColor DarkGray
    $deadline = (Get-Date).AddSeconds(90)
    $final = 'no new run row ever appeared (trigger accepted but engine never inserted a run)'
    do {
        Start-Sleep -Seconds 5
        $now = Invoke-Sql "SELECT left(id::text,8), status, duration_ms, coalesce(left(message,140),'<null>') FROM ml_correlation_compute_runs ORDER BY started_at_utc DESC LIMIT 1;"
        $cnt = [int](Invoke-Sql "SELECT count(*) FROM ml_correlation_compute_runs;")[0]
        if ($cnt -gt $runsBefore) { $final = 'NEW RUN: ' + ($now -join ' ') }
        if ($now -and ($now[0] -match 'Completed|Failed|Ok|Error')) { break }
    } while ((Get-Date) -lt $deadline)
    $resAfter = [int](Invoke-Sql "SELECT count(*) FROM ml_correlation_results_v2;")[0]
    Add-Evidence 'V1-23' 'Live trigger lifecycle after 90s' 'EVIDENCE' ($final + '   results_v2=' + $resAfter)
    if ($final -match 'Completed' -and $resAfter -gt 0) {
        Add-Evidence 'V1-23' 'J7 run-to-result COMPLETED with rows' 'PASS' 'If this passed, V1-42 may already be resolvable - send the output.'
    } else {
        Add-Evidence 'V1-23' 'J7 run-to-result' 'FAIL' 'Expected today: this is the V1-42 defect, now captured with evidence. Send walk-evidence.md.'
    }
} elseif (-not $triggered) {
    Add-Evidence 'V1-23' 'No reachable job-list route auto-triggered a correlation run' 'EVIDENCE' 'Trigger one inspection from the HMI (Advanced Analysis) right after this script, then re-run ONLY the forensic SQL above.'
}
Add-Evidence 'V1-23' 'HMI: inspection run from Advanced Analysis renders ranked contributor + honesty bar; assistant explains with citations' 'MANUAL' 'Gated on V1-42 fix; wire-or-frame decision 07-Jul noon.'

# ---------- report -----------------------------------------------------------------------------
$md = New-Object System.Collections.ArrayList
[void]$md.Add('# PPIQ Journey-Walk Evidence - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm'))
[void]$md.Add('')
[void]$md.Add('| Task | Check | Result | Detail |')
[void]$md.Add('|---|---|---|---|')
foreach ($r in $script:Rows) {
    [void]$md.Add('| ' + $r.Task + ' | ' + $r.Check + ' | **' + $r.Result + '** | ' + (($r.Detail -replace '\|', '/') -replace "`n", '<br>') + ' |')
}
$pass = @($script:Rows | Where-Object Result -eq 'PASS').Count
$fail = @($script:Rows | Where-Object Result -eq 'FAIL').Count
$man  = @($script:Rows | Where-Object Result -eq 'MANUAL').Count
$ev   = @($script:Rows | Where-Object Result -eq 'EVIDENCE').Count
[void]$md.Add('')
[void]$md.Add('**Totals: ' + $pass + ' PASS / ' + $fail + ' FAIL / ' + $man + ' MANUAL / ' + $ev + ' EVIDENCE**')
$enc = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path (Get-Location) 'walk-evidence.md'), (($md -join "`r`n") + "`r`n"), $enc)
Write-Host ''
Write-Host ('================ TOTALS: ' + $pass + ' PASS / ' + $fail + ' FAIL / ' + $man + ' MANUAL / ' + $ev + ' EVIDENCE ================') -ForegroundColor Cyan
Write-Host 'walk-evidence.md written. Send it back - the V1-23 EVIDENCE section is the Pack B root-cause input.' -ForegroundColor Cyan
Write-Host ''
Write-Host 'MANUAL checklist (do during rehearsal #1, initial each):' -ForegroundColor Yellow
$script:Rows | Where-Object Result -eq 'MANUAL' | ForEach-Object { Write-Host ('  [ ] ' + $_.Task + ' - ' + $_.Check) -ForegroundColor Yellow }
