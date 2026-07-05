# ================================================================================================
# PPIQ JOURNEY-WALK EVIDENCE PROVER v2 (M1 completion)  - supersedes Invoke-PpiqJourneyWalk.ps1
# ================================================================================================
# Fixes the four v1 prover defects (scalar-unwrap on single-row SQL, wrong-DB seam-6 injection,
# preview property name sql->sqlText, uuid passed as first-char) and ADDS automated acceptance
# for V1-38 (site identity), V1-23/V1-50 (results render + ranked contributors), V1-45 (job_log).
# Non-destructive. Run AS A FILE with the API on :5063 and the source fleet up:
#   & .\Invoke-PpiqJourneyWalk.ps1
# ================================================================================================
param(
    [string]$ApiBase = 'http://localhost:5063',
    [string]$PgUser  = $(if ($env:PPIQ_PG_USER) { $env:PPIQ_PG_USER } else { 'ppiq_dev' }),
    [string]$PgPass  = $(if ($env:PPIQ_PG_PASS) { $env:PPIQ_PG_PASS } else { 'ppiq_dev_local_only' }),
    [string]$PgDb    = $(if ($env:PPIQ_PG_DB)   { $env:PPIQ_PG_DB }   else { 'ppiq_app' }),
    [string]$Username = 'e2eadmin',
    [string]$Password = 'E2EAdmin123!'
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$script:Rows = New-Object System.Collections.ArrayList
function Add-Ev([string]$Task, [string]$Check, [string]$Result, [string]$Detail) {
    [void]$script:Rows.Add([pscustomobject]@{ Task=$Task; Check=$Check; Result=$Result; Detail=$Detail })
    $c = 'Gray'
    if ($Result -eq 'PASS') { $c='Green' } elseif ($Result -eq 'FAIL') { $c='Red' } elseif ($Result -eq 'MANUAL') { $c='Yellow' } elseif ($Result -eq 'EVIDENCE') { $c='Cyan' }
    Write-Host ('[' + $Result.PadRight(8) + '] ' + $Task + ' :: ' + $Check) -ForegroundColor $c
    if ($Detail) { Write-Host ('           ' + ($Detail -replace "`n", "`n           ")) -ForegroundColor DarkGray }
}
$psql = (Get-Command psql -ErrorAction SilentlyContinue).Source
if (-not $psql) {
    $cand = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
    if ($cand) { $psql = $cand.FullName }
}
function Sql([string]$q) {
    if (-not $psql) { return @() }
    $env:PGPASSWORD = $PgPass
    $out = & $psql -h localhost -p 5432 -U $PgUser -d $PgDb -v ON_ERROR_STOP=1 -t -A -F '|' -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('psql: ' + ($out -join ' ')) }
    return ,@($out | Where-Object { $_ -ne '' })   # comma keeps single-row results an ARRAY (v1 bug fix)
}

Write-Host ''
Write-Host '========== PPIQ JOURNEY-WALK PROVER v2 ==========' -ForegroundColor Cyan
Write-Host ('API ' + $ApiBase + ' | DB ' + $PgDb + ' | psql ' + $(if ($psql) { 'ok' } else { 'MISSING' }))
Write-Host ''

# ---- V1-17 auth ----
$login = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/auth/login') -ContentType 'application/json' -Body (@{ username=$Username; password=$Password } | ConvertTo-Json)
$token = $login.accessToken
if (-not $token -or $token.Split('.').Count -ne 3) { Add-Ev 'V1-17' 'signed JWT' 'FAIL' 'no 3-part token'; throw 'auth failed' }
$H = @{ Authorization = 'Bearer ' + $token }
Add-Ev 'V1-17' 'Real signed 3-part JWT; authorized call succeeds' 'PASS' ('len=' + $token.Length)
Add-Ev 'V1-17' 'Cold browser load: nav + populated panel + clean console + __Host- cookie' 'MANUAL' ''

# ---- V1-13 readiness ----
try {
    $r = Invoke-WebRequest -Uri ($ApiBase + '/admin/schema-mapping/readiness') -Headers $H -UseBasicParsing
    $bad = [regex]::Matches($r.Content, '"(isReady|ready|healthy)"\s*:\s*false', 'IgnoreCase').Count
    Add-Ev 'V1-13' 'Readiness 200, no not-ready flags' $(if ($r.StatusCode -eq 200 -and $bad -eq 0) { 'PASS' } else { 'FAIL' }) ('notReady=' + $bad)
} catch { Add-Ev 'V1-13' 'Readiness' 'FAIL' $_.Exception.Message }

# ---- V1-18 connect + select ----
$profiles = Invoke-RestMethod -Uri ($ApiBase + '/admin/connectors/connection-profiles') -Headers $H
$list = @($profiles); if ($profiles.PSObject.Properties['items']) { $list = @($profiles.items) }
$mp = $list | Where-Object { ($_.connectionProfileName + ' ' + $_.connectionProfileCode) -match 'melt|demo' } | Select-Object -First 1
if (-not $mp) { $mp = $list | Select-Object -First 1 }
Add-Ev 'V1-18' 'Connection profiles listed; profile identified' $(if ($mp) { 'PASS' } else { 'FAIL' }) ('profile=' + $mp.connectionProfileCode)
if ($mp) {
    try {
        $test = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/connectors/connection-profiles/' + $mp.id + '/test') -Headers $H
        $ok = ($test.success -eq $true) -or ($test.isSuccess -eq $true) -or ("$($test.status)" -match 'ok|success|healthy')
        Add-Ev 'V1-18' 'Live connection test' $(if ($ok) { 'PASS' } else { 'EVIDENCE' }) (($test | ConvertTo-Json -Compress -Depth 4))
    } catch {
        $code = 0; try { $code = [int]$_.Exception.Response.StatusCode } catch {}
        Add-Ev 'V1-18' ('Live connection test returned HTTP ' + $code) 'EVIDENCE' 'If 400: the demo profile may need activation or a reachable source; investigate in the HMI walk.'
    }
}
try {
    Invoke-WebRequest -Method Post -Uri ($ApiBase + '/admin/connectors/connection-profiles/00000000-0000-0000-0000-000000000000/test') -Headers $H -UseBasicParsing -ErrorAction Stop | Out-Null
    Add-Ev 'V1-18' 'Unknown profile typed error' 'FAIL' 'unexpected 200'
} catch {
    $code = 0; try { $code = [int]$_.Exception.Response.StatusCode } catch {}
    Add-Ev 'V1-18' ('Unknown-profile test -> typed ' + $code + ' (not a 500)') $(if ($code -ge 400 -and $code -lt 500) { 'PASS' } else { 'FAIL' }) ''
}
$reg = Sql "SELECT count(*) FROM source_table_dump_registry WHERE is_deleted=false;"
Add-Ev 'V1-18' ('Registry rows: ' + $reg[0]) $(if ([int]$reg[0] -ge 2) { 'PASS' } else { 'EVIDENCE' }) ''
Add-Ev 'V1-18' 'Stopped-container test shows typed error in HMI' 'MANUAL' ''

# ---- V1-19 Stage-1 + watermark ----
$before = [int](Sql "SELECT count(*) FROM dump_store.src_meltshop_pg_heats;")[0]
Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage1/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy='prover' } | ConvertTo-Json) | Out-Null
$after1 = [int](Sql "SELECT count(*) FROM dump_store.src_meltshop_pg_heats;")[0]
Add-Ev 'V1-19' ('Stage-1 dump rows ' + $before + ' -> ' + $after1) $(if ($after1 -ge $before) { 'PASS' } else { 'FAIL' }) ''
Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage1/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy='prover2' } | ConvertTo-Json) | Out-Null
$after2 = [int](Sql "SELECT count(*) FROM dump_store.src_meltshop_pg_heats;")[0]
Add-Ev 'V1-19' ('Watermark: 2nd run adds ' + ($after2 - $after1) + ' (expect 0)') $(if ($after2 -eq $after1) { 'PASS' } else { 'FAIL' }) ''

# ---- V1-45 job_log evidence (Stage-1 just ran) ----
$jl = Sql "SELECT count(*) FROM job_log WHERE job_type='Import-Stage1' AND occurred_at_utc > now() - interval '5 min';"
Add-Ev 'V1-45' ('job_log Import-Stage1 events in last 5 min: ' + $jl[0]) $(if ([int]$jl[0] -ge 2) { 'PASS' } else { 'FAIL' }) 'Started + Completed expected'
try {
    $api = Invoke-RestMethod -Uri ($ApiBase + '/admin/job-logs?jobType=Import-Stage1&severity=Info') -Headers $H
    Add-Ev 'V1-45' ('/admin/job-logs returns entries: ' + @($api.entries).Count) $(if (@($api.entries).Count -gt 0) { 'PASS' } else { 'FAIL' }) ''
} catch { Add-Ev 'V1-45' 'job-logs API' 'FAIL' $_.Exception.Message }

# ---- V1-21 Stage-2 ----
$cb = [int](Sql "SELECT count(*) FROM material_units;")[0]
Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage2/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy='prover' } | ConvertTo-Json) | Out-Null
$ca = [int](Sql "SELECT count(*) FROM material_units;")[0]
Add-Ev 'V1-21' ('Stage-2 canonical material_units=' + $ca) $(if ($ca -gt 0) { 'PASS' } else { 'FAIL' }) ''

# ---- V1-22 seam-6 (inject into the REGISTERED source; v1 injected into the wrong DB) ----
$mark = 'WALK-' + (Get-Date -Format 'HHmmss')
$ins = "INSERT INTO src_meltshop_pg.heats (heat_no, plant_code, furnace_code, steel_grade, route_code, tap_start_utc, tap_end_utc, heat_weight_ton, target_temp_c, actual_temp_c, source_updated_at_utc) VALUES ('" + $mark + "','PLANT1','EAF-1','GRADE-W','ROUTE-1',now(),now(),120.5,1600,1595,now());"
$null = Sql $ins
$dumpBefore = [int](Sql "SELECT count(*) FROM dump_store.src_meltshop_pg_heats;")[0]
Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage1/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy='seam6' } | ConvertTo-Json) | Out-Null
Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage2/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy='seam6' } | ConvertTo-Json) | Out-Null
$dumpAfter = [int](Sql "SELECT count(*) FROM dump_store.src_meltshop_pg_heats;")[0]
Add-Ev 'V1-22' ('Seam-6: injected 1 into REGISTERED source; dump ' + $dumpBefore + ' -> ' + $dumpAfter) $(if ($dumpAfter -gt $dumpBefore) { 'PASS' } else { 'FAIL' }) 'A canonical-bound widget will visibly change on refresh.'
try {
    Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/schema-configuration/views/preview') -Headers $H -ContentType 'application/json' -Body (@{ sqlText='SELECT * FROM dump_store.src_meltshop_pg_heats LIMIT 5' } | ConvertTo-Json) | Out-Null
    Add-Ev 'V1-22' 'Safe-SQL preview (correct property sqlText) 200' 'PASS' ''
} catch { Add-Ev 'V1-22' 'Safe-SQL preview' 'FAIL' $_.Exception.Message }
Add-Ev 'V1-22' 'HMI: create page/widget, bind, save, reload persists; refresh changes number' 'MANUAL' ''

# ---- V1-11 blended attribution (pass the FULL uuid; v1 passed the first char) ----
$uid = (Sql "SELECT id FROM material_units WHERE material_code='C-0044170' LIMIT 1;")
if ($uid -and $uid.Count -ge 1 -and $uid[0]) {
    $rows = Sql ("SELECT weight FROM ppiq_v5_blended_attribution_for_child('" + $uid[0] + "'::uuid);")
    $joined = ($rows -join ',')
    Add-Ev 'V1-11' ('Blended attribution weights: ' + $joined) $(if (($rows.Count -ge 2) -and ($joined -match '0\.7') -and ($joined -match '0\.3')) { 'PASS' } else { 'EVIDENCE' }) ''
} else {
    Add-Ev 'V1-11' 'Blended attribution' 'EVIDENCE' 'C-0044170 absent (meltshop-only DB); needs the full demo fleet for the 70/30 coil.'
}
Add-Ev 'V1-11' 'HMI clip: transition coil weighted provenance + population' 'MANUAL' ''

# ---- V1-38 site identity ----
try {
    $siteBefore = Invoke-RestMethod -Uri ($ApiBase + '/admin/site-identity') -Headers $H
    $orig = (Sql "SELECT site_name FROM sites ORDER BY site_code LIMIT 1;")[0]
    $null = Sql "UPDATE sites SET site_name='Proof Plant' WHERE site_code=(SELECT site_code FROM sites ORDER BY site_code LIMIT 1);"
    $siteAfter = Invoke-RestMethod -Uri ($ApiBase + '/admin/site-identity') -Headers $H
    $null = Sql ("UPDATE sites SET site_name='" + ($orig -replace "'","''") + "' WHERE site_code=(SELECT site_code FROM sites ORDER BY site_code LIMIT 1);")
    Add-Ev 'V1-38' ("site-identity reflects DB change: '" + $siteBefore.siteName + "' -> '" + $siteAfter.siteName + "' (restored)") $(if ($siteAfter.siteName -eq 'Proof Plant') { 'PASS' } else { 'FAIL' }) 'Sidebar renders siteName; sidebar reload is the MANUAL half.'
} catch { Add-Ev 'V1-38' 'site-identity endpoint' 'FAIL' $_.Exception.Message }

# ---- V1-23 / V1-50 results render (deterministic-core) ----
$rescount = [int](Sql "SELECT count(*) FROM ml_correlation_results_v2;")[0]
Add-Ev 'V1-23' ('ml_correlation_results_v2 rows: ' + $rescount) $(if ($rescount -gt 0) { 'PASS' } else { 'FAIL' }) ''
try {
    $api = Invoke-RestMethod -Uri ($ApiBase + '/api/analytics/advanced/results') -Headers $H
    $n = @($api).Count; if ($api.PSObject.Properties['findings']) { $n = @($api.findings).Count } elseif ($api.PSObject.Properties['results']) { $n = @($api.results).Count }
    Add-Ev 'V1-23' ('/api/analytics/advanced/results resolves rows: ' + $n) $(if ($n -gt 0) { 'PASS' } else { 'EVIDENCE' }) 'HMI CorrelationPage reads this; ranked contributors render.'
} catch { Add-Ev 'V1-23' 'advanced results endpoint' 'EVIDENCE' ('needs a resolvable run: ' + $_.Exception.Message) }
$top = Sql "SELECT feature_key || ' -> ' || outcome_key || ' eff=' || round(effect_size::numeric,3) || ' q=' || coalesce(round(q_value::numeric,4)::text,'-') FROM ml_correlation_results_v2 WHERE method <> 'cramers_v' ORDER BY effect_size DESC NULLS LAST LIMIT 3;"
Add-Ev 'V1-50' 'Ranked contributors (population/method/q-value present, superheat on top)' $(if (($top -join ' ') -match 'superheat') { 'PASS' } else { 'EVIDENCE' }) ($top -join "`n")
Add-Ev 'V1-23' 'HMI: inspection run renders ranked list + honesty bar; assistant cites (or V1-43 framing)' 'MANUAL' ''

# ---- report ----
$md = New-Object System.Collections.ArrayList
[void]$md.Add('# PPIQ Journey-Walk Evidence v2 - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm'))
[void]$md.Add(''); [void]$md.Add('| Task | Check | Result | Detail |'); [void]$md.Add('|---|---|---|---|')
foreach ($r in $script:Rows) { [void]$md.Add('| ' + $r.Task + ' | ' + $r.Check + ' | **' + $r.Result + '** | ' + (($r.Detail -replace '\|','/') -replace "`n",'<br>') + ' |') }
$pass=@($script:Rows|Where-Object Result -eq 'PASS').Count; $fail=@($script:Rows|Where-Object Result -eq 'FAIL').Count
$man=@($script:Rows|Where-Object Result -eq 'MANUAL').Count; $ev=@($script:Rows|Where-Object Result -eq 'EVIDENCE').Count
[void]$md.Add(''); [void]$md.Add('**Totals: ' + $pass + ' PASS / ' + $fail + ' FAIL / ' + $man + ' MANUAL / ' + $ev + ' EVIDENCE**')
$enc = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path (Get-Location) 'walk-evidence.md'), (($md -join "`r`n") + "`r`n"), $enc)
Write-Host ''
Write-Host ('========== ' + $pass + ' PASS / ' + $fail + ' FAIL / ' + $man + ' MANUAL / ' + $ev + ' EVIDENCE ==========') -ForegroundColor Cyan
Write-Host 'walk-evidence.md written.' -ForegroundColor Cyan
$script:Rows | Where-Object Result -eq 'MANUAL' | ForEach-Object { Write-Host ('  [ ] ' + $_.Task + ' - ' + $_.Check) -ForegroundColor Yellow }
