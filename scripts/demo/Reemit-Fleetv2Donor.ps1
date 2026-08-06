# =============================================================================
# Reemit-Fleetv2Donor.ps1        v2
#
# REPLACE THE INTERNAL src_* DONOR WITH THE FLEET-V2 EMISSION.   T-031.
#
# RESPONSIBILITY, AND NOTHING BEYOND IT
#   prove the frozen capture baseline still reproduces
#   back up the donor schemas and the registry, in TWO dumps
#   pre-clear the donor in one multi-table TRUNCATE
#   apply the fleet-v2 donor emission
#   verify the donor now agrees with canonical
#
#   It does NOT touch dump_store. Refreshing the staging shape is the reset's
#   job, because the reset is what backs staging up. v1 dropped the dump tables
#   here, which destroyed staging before any staging backup existed.
#
# WHY THE PRE-CLEAR EXISTS
#   The generator's ORDER list starts with src_meltshop_pg.heats and the emitted
#   SQL deletes PARENT FIRST, while 110_phase1_demo_source_shapes.sql declares
#   three FKs inside the donor schemas. The first ever load worked only because
#   the tables were empty and the DELETEs were no-ops. Against a populated donor
#   it fails on lf_treatment_heat_no_fkey. One multi-table TRUNCATE satisfies
#   FKs among the listed tables and REFUSES if anything outside the set
#   references them, which is the fail-closed behaviour we want.
#
# CANONICAL IS NEVER TOUCHED. Stage 1 and stage 2 are never called here.
#
# Run from repo root:
#   .\scripts\demo\Reemit-Fleetv2Donor.ps1
#   .\scripts\demo\Reemit-Fleetv2Donor.ps1 -Execute
# =============================================================================
[CmdletBinding()]
param(
    [switch]$Execute,
    [string]$TargetDb = 'ppiq_presentation',
    [string]$DbHost = '127.0.0.1',
    [int]$DbPort = 5432,
    [string]$DbUser = 'ppiq_dev',
    [int]$Scale = 3,
    [int]$Seed = 20260803,
    [string]$ExpectedCaptureSha = '11EDF4B275A106C86D75EA3147D47B56F7763AD9EE2D258487953B7155939AD7',
    [string]$BackupDir = 'deploy\.ppiq-snapshots'
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

if ($TargetDb -notmatch 'presentation') {
    Write-Host "[REFUSED] guard: target database name must contain 'presentation'." -ForegroundColor Red
    exit 1
}

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$EvidenceDir = Join-Path $RepoRoot 'docs\m1\evidence'
if (-not (Test-Path $EvidenceDir)) { New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null }
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s); Write-Host $s }
function Save { [System.IO.File]::WriteAllText($Report, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false))) }

$PgBin = $null
$cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmd) { $PgBin = Split-Path $cmd.Source -Parent } else {
    $c = @(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
    if ($c.Count -gt 0) { $PgBin = Split-Path $c[0].FullName -Parent }
}
if (-not $PgBin) { Write-Host '[FAIL] psql not found.' -ForegroundColor Red; exit 1 }
$Psql = Join-Path $PgBin 'psql.exe'
$PgDump = Join-Path $PgBin 'pg_dump.exe'
$env:PGPASSWORD = 'ppiq_dev_local_only'

function Q1([string]$q) {
    $o = & $Psql -h $DbHost -p $DbPort -U $DbUser -d $TargetDb -w -X -A -t -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { return $null }
    $l = @($o | Where-Object { $_ -and ($_.ToString().Trim() -ne '') }) | Select-Object -First 1
    if ($null -eq $l) { return '' }
    return $l.ToString().Trim()
}
function Rows([string]$q) {
    return @(& $Psql -h $DbHost -p $DbPort -U $DbUser -d $TargetDb -w -X -A -t -F '|' -c $q 2>&1 |
             Where-Object { $_ -and ($_.ToString().Trim() -ne '') })
}
function RunFile([string]$label, [string]$path) {
    $o = & $Psql -h $DbHost -p $DbPort -U $DbUser -d $TargetDb -w -X -v ON_ERROR_STOP=1 -f $path 2>&1
    $code = $LASTEXITCODE
    if ($code -eq 0) { W ('      OK   ' + $label) } else {
        W ('      FAIL ' + $label)
        @($o | Select-Object -Last 8) | ForEach-Object { W ('           ' + $_) }
    }
    return ($code -eq 0)
}
function RunSql([string]$label, [string]$sql) {
    $p = Join-Path $env:TEMP ('ppiq_' + [guid]::NewGuid().ToString('N') + '.sql')
    [System.IO.File]::WriteAllText($p, $sql, (New-Object System.Text.UTF8Encoding($false)))
    $r = RunFile $label $p
    Remove-Item $p -ErrorAction SilentlyContinue
    return $r
}
# TWO DUMPS, NEVER ONE. pg_dump lets -t win over -n, so a single invocation
# carrying both wrote a file holding ONLY the registry table while reporting
# success. Each file is size-checked separately.
function DumpPart([string]$label, [string[]]$dumpArgs, [string]$path, [int]$minBytes) {
    & $PgDump -h $DbHost -p $DbPort -U $DbUser -d $TargetDb -w -Fc @dumpArgs -f $path 2>&1 |
        ForEach-Object { W ('           ' + $_) }
    if ($LASTEXITCODE -ne 0) { W ('      FAIL ' + $label + ' - pg_dump exit ' + $LASTEXITCODE); return $false }
    if (-not (Test-Path $path)) { W ('      FAIL ' + $label + ' - no file produced'); return $false }
    $len = (Get-Item $path).Length
    if ($len -lt $minBytes) {
        W ('      FAIL ' + $label + ' - ' + $len + ' bytes is below the ' + $minBytes + ' byte floor;')
        W '           that is the signature of a dump that selected nothing.'
        return $false
    }
    W ('      OK   ' + $label + '   ' + $path + '   ' + [Math]::Round($len / 1MB, 3) + ' MB')
    return $true
}
$Report = Join-Path $EvidenceDir ('T-031_donor_reemission_' + $Stamp + '.txt')
$Tmp = Join-Path $env:TEMP ('ppiq_t031_donor_' + $Stamp)
New-Item -ItemType Directory -Path $Tmp -Force | Out-Null
$Gen = Join-Path $RepoRoot 'Backend\tools\generate_fleet_v2_donor.py'

function Py([string]$tag, [string[]]$genArgs) {
    $so = Join-Path $Tmp ($tag + '.out')
    $se = Join-Path $Tmp ($tag + '.err')
    $p = Start-Process -FilePath 'python' -ArgumentList (@($Gen) + $genArgs) `
         -WorkingDirectory $RepoRoot -NoNewWindow -Wait -PassThru `
         -RedirectStandardOutput $so -RedirectStandardError $se
    return @{ Code = $p.ExitCode; Out = $so; Err = $se }
}

W ('FLEET V2 DONOR RE-EMISSION v2 - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('Target   : ' + $TargetDb + '   (ppiq_app guarded)')
W ('Mode     : ' + $(if ($Execute) { 'EXECUTE' } else { 'REPORT ONLY - nothing is changed' }))
W ('Emission : --mode fleet-v2 --emit donor --scale ' + $Scale + ' --seed ' + $Seed)
W ('Scope    : the donor only. dump_store belongs to Reset-PresentationStaging.ps1.')
W ('=' * 90)
W ''

W '[1/6] re-prove the frozen capture baseline (retirement gate condition 1)'
if (-not (Test-Path $Gen)) { W '[ABORT] generator not found.'; Save; exit 1 }
$capOut = Join-Path $Tmp 'capture.sql'
$r = Py 'capture' @('--mode', 'capture', '--out', $capOut)
if ($r.Code -ne 0) { W '[ABORT] capture emit failed.'; Save; exit 2 }
$capSha = (Get-FileHash -LiteralPath $capOut -Algorithm SHA256).Hash
W ('      capture sha256 : ' + $capSha)
if ($capSha -ne $ExpectedCaptureSha.ToUpper()) {
    W '[ABORT] the frozen capture has drifted. Retirement gate condition 1 would no longer'
    W '        be re-provable, so the donor must NOT be overwritten. Nothing was changed.'
    Save; exit 3
}
W '      OK   the captured baseline still reproduces from the generator'
W ''

W '[2/6] preflight (fail closed)'
$pf = 0
function Gate([string]$label, [bool]$ok, [string]$detail) {
    if ($ok) { W ('      OK   ' + $label + '   ' + $detail) } else { W ('      FAIL ' + $label + '   ' + $detail); $script:pf = $script:pf + 1 }
}
$alive = Q1 'SELECT 1;'
Gate 'database reachable' ($alive -eq '1') ''
if ($alive -ne '1') { W '[ABORT] cannot reach the database.'; Save; exit 1 }
$fleetCanon = Q1 "SELECT count(*) FROM defect_catalogs WHERE defect_code = 'ROLLED_IN_SCALE' AND coalesce(is_deleted,false)=false;"
Gate 'canonical is the fleet-v2 emission we are aligning to' ($fleetCanon -eq '1') ('ROLLED_IN_SCALE present=' + $fleetCanon)
$schemas = @(Rows @"
SELECT DISTINCT source_schema_name FROM public.source_table_dump_registry
WHERE is_deleted = false AND is_active = true ORDER BY 1;
"@)
Gate 'donor schemas resolved from the registry' ($schemas.Count -gt 0) ('schemas=' + $schemas.Count)
$bad = @($schemas | Where-Object { $_ -notlike 'src_*' })
Gate 'every registered source schema is a src_ donor schema' ($bad.Count -eq 0) ''
$inList = ($schemas | ForEach-Object { "'" + $_ + "'" }) -join ','
$donorTables = @()
if ($schemas.Count -gt 0) {
    $donorTables = @(Rows ("SELECT table_schema || '.' || table_name FROM information_schema.tables WHERE table_schema IN (" + $inList + ") AND table_type = 'BASE TABLE' ORDER BY 1;"))
}
Gate 'donor tables discovered' ($donorTables.Count -gt 0) ('tables=' + $donorTables.Count)
$leased = Q1 "SELECT count(*) FROM public.source_table_dump_registry WHERE is_deleted=false AND is_active=true AND lease_until_utc IS NOT NULL AND lease_until_utc > now();"
Gate 'no live import lease held' ($leased -eq '0') ('held=' + $leased)
W ''
W '      current donor, before'
foreach ($t in $donorTables) { W ('        ' + $t.PadRight(52) + (Q1 ('SELECT count(*) FROM ' + $t + ';'))) }
W ('      donor defect codes: ' + (Q1 "SELECT coalesce(string_agg(DISTINCT defect_code, ', ' ORDER BY defect_code),'<none>') FROM src_inspection_mysql_shape.parsytec_surface_defects;"))
W ''
if ($pf -gt 0) { W ('[ABORT] preflight failed with ' + $pf + ' error(s). NOTHING WAS CHANGED.'); Save; exit 4 }

W '[3/6] emit the fleet-v2 donor'
$donorSql = Join-Path $Tmp 'donor_fleetv2.sql'
$r = Py 'donor' @('--mode', 'fleet-v2', '--emit', 'donor', '--scale', $Scale.ToString(), '--seed', $Seed.ToString(), '--out', $donorSql)
if ($r.Code -ne 0) {
    W '[ABORT] the generator REFUSED to emit:'
    if (Test-Path $r.Err) { Get-Content $r.Err | Select-Object -First 12 | ForEach-Object { W ('      ' + $_) } }
    Save; exit 5
}
if (Test-Path $r.Out) { Get-Content $r.Out | Select-Object -First 16 | ForEach-Object { W ('      ' + $_) } }
$sqlText = [System.IO.File]::ReadAllText($donorSql)
$shape = @{
    BEGIN    = ([regex]::Matches($sqlText, '(?m)^BEGIN;')).Count
    COMMIT   = ([regex]::Matches($sqlText, '(?m)^COMMIT;')).Count
    DELETE   = ([regex]::Matches($sqlText, '(?m)^DELETE FROM ')).Count
    ALTER    = ([regex]::Matches($sqlText, '(?m)^ALTER TABLE ')).Count
    TRUNCATE = ([regex]::Matches($sqlText, '(?m)^TRUNCATE')).Count
    DROP     = ([regex]::Matches($sqlText, '(?m)^DROP')).Count
}
foreach ($k in ($shape.Keys | Sort-Object)) { W ('      ' + $k.PadRight(10) + $shape[$k]) }
W ('      size       ' + [Math]::Round((Get-Item $donorSql).Length / 1MB, 2) + ' MB')
if (-not (($shape.BEGIN -eq 1) -and ($shape.COMMIT -eq 1) -and ($shape.DROP -eq 0) -and ($shape.TRUNCATE -eq 0))) {
    W '[ABORT] the emitted SQL is not the accepted shape: one transaction, no DROP, no TRUNCATE.'
    Save; exit 6
}
W '      OK   one transaction, no DROP, no TRUNCATE'
W ''

if (-not $Execute) {
    W 'REPORT ONLY. Nothing in the database was changed.'
    W ''
    W 'With -Execute this runner would, in order:'
    W ('  4  TWO pg_dump files into ' + $BackupDir + ' - the donor schemas, and the registry - each size-checked')
    W '  5  one multi-table TRUNCATE of every donor table, so the emitted parent-first DELETEs cannot hit an FK'
    W '  6  apply the emitted donor SQL and verify against canonical'
    W ''
    W 'It does NOT touch dump_store. Run Reset-PresentationStaging.ps1 -Execute afterwards.'
    W ('Emitted SQL kept: ' + $donorSql)
    W ('Report: ' + $Report)
    Save
    exit 0
}

W '[4/6] backup, in two dumps'
$bdir = Join-Path $RepoRoot $BackupDir
if (-not (Test-Path $bdir)) { New-Item -ItemType Directory -Path $bdir -Force | Out-Null }
$bDonor = Join-Path $bdir ('ppiq_presentation_donorschemas_preFleetV2_' + $Stamp + '.dump')
$bReg   = Join-Path $bdir ('ppiq_presentation_registry_preFleetV2_' + $Stamp + '.dump')
$nArgs = @()
foreach ($s in $schemas) { $nArgs += @('-n', $s) }
$ok1 = DumpPart 'donor schemas' $nArgs $bDonor 200000
$ok2 = DumpPart 'dump registry' @('-t', 'public.source_table_dump_registry') $bReg 1000
if (-not ($ok1 -and $ok2)) { W '[ABORT] backup failed its own size check. NOTHING WAS CHANGED.'; Save; exit 7 }
W ''

W '[5/6] pre-clear the donor in one multi-table TRUNCATE'
$truncList = ($donorTables -join ', ')
if (-not (RunSql 'truncate donor tables' ('TRUNCATE TABLE ' + $truncList + ';' + "`r`n"))) {
    W '[ABORT] the TRUNCATE was refused. That means something OUTSIDE the donor schemas'
    W '        references them, which must be understood before the donor is replaced.'
    W ('        restore with: pg_restore -h ' + $DbHost + ' -U ' + $DbUser + ' -d ' + $TargetDb + ' --clean "' + $bDonor + '"')
    Save; exit 8
}
W ''

W '[6/6] apply the fleet-v2 donor and verify'
if (-not (RunFile 'donor emission' $donorSql)) {
    W '[ABORT] the donor did not apply. It is one transaction, so nothing was committed,'
    W '        but the pre-clear DID commit, so the donor is now empty. Restore it:'
    W ('        pg_restore -h ' + $DbHost + ' -U ' + $DbUser + ' -d ' + $TargetDb + ' --clean "' + $bDonor + '"')
    Save; exit 9
}
$gf = 0
function V([string]$label, [string]$got, [string]$want) {
    if ($got -eq $want) { W ('      OK   ' + $label.PadRight(54) + $got) } else {
        W ('      FAIL ' + $label.PadRight(54) + $got + '   expected ' + $want); $script:gf = $script:gf + 1
    }
}
V 'donor heats' (Q1 'SELECT count(*) FROM src_meltshop_pg.heats;') (630 * $Scale).ToString()
V 'donor coils' (Q1 'SELECT count(*) FROM src_hsm_oracle_shape.hsm_coils;') (5670 * $Scale).ToString()
V 'donor rows carrying the legacy ROLLED_IN code' (Q1 "SELECT count(*) FROM src_inspection_mysql_shape.parsytec_surface_defects WHERE defect_code = 'ROLLED_IN';") '0'
V 'donor defect codes absent from the catalogue' (Q1 @"
SELECT count(*) FROM (SELECT DISTINCT defect_code AS d FROM src_inspection_mysql_shape.parsytec_surface_defects) s
WHERE NOT EXISTS (SELECT 1 FROM defect_catalogs c WHERE coalesce(c.is_deleted,false)=false AND c.defect_code = s.d);
"@) '0'
V 'donor coils absent from canonical' (Q1 @"
SELECT count(*) FROM src_hsm_oracle_shape.hsm_coils c
WHERE NOT EXISTS (SELECT 1 FROM material_units m WHERE coalesce(m.is_deleted,false)=false AND m.material_code = c.coil_id);
"@) '0'
V 'canonical coils absent from the donor' (Q1 @"
SELECT count(*) FROM material_units m WHERE coalesce(m.is_deleted,false)=false AND m.material_unit_type='Coil'
  AND NOT EXISTS (SELECT 1 FROM src_hsm_oracle_shape.hsm_coils c WHERE c.coil_id = m.material_code);
"@) '0'
V 'donor coils whose canonical parent edge disagrees' (Q1 @"
SELECT count(*) FROM src_hsm_oracle_shape.hsm_coils c
WHERE c.input_piece_id IS NOT NULL AND NOT EXISTS (
  SELECT 1 FROM material_units ch
  JOIN genealogy_edges ge ON ge.child_material_unit_id = ch.id AND coalesce(ge.is_deleted,false)=false
  JOIN material_units pa ON pa.id = ge.parent_material_unit_id
  WHERE coalesce(ch.is_deleted,false)=false AND ch.material_code = c.coil_id AND pa.material_code = c.input_piece_id);
"@) '0'
W ''
if ($gf -gt 0) {
    W ('[GATE RED] ' + $gf + ' verification(s) failed. The donor applied but does not align with canonical.')
    W ('           restore with: pg_restore -h ' + $DbHost + ' -U ' + $DbUser + ' -d ' + $TargetDb + ' --clean "' + $bDonor + '"')
    Save; exit 10
}
W '[GATE GREEN] the donor is the fleet-v2 emission and agrees with canonical on vocabulary,'
W '             identity and genealogy. dump_store was NOT touched and is now stale by design.'
W ('             Backups: ' + $bDonor)
W ('                      ' + $bReg)
W ('             Report:  ' + $Report)
W ''
W 'NEXT, IN THIS ORDER:'
W '  .\scripts\demo\Reset-PresentationStaging.ps1 -Execute'
W '  .\tools\run\Invoke-PpiqT031Certification.ps1 -InjectDivergence'
Save
exit 0