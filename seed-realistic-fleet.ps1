# ================================================================================================
# tools\seed-realistic-fleet.ps1
# PPIQ REALISTIC SOURCE-FLEET GENERATOR (deterministic, seed=42)
# ================================================================================================
# Replaces the disconnected per-source demo rows with ONE coherent, physics-linked steel dataset:
#   meltshop_heats (PG)  ->  caster_sequences (Oracle, +slab identity/dims)
#     -> hsm_passes (Oracle, +slab_id genealogy, coil dims, conservation laws)
#       -> parsytec_surface_defects (MySQL, defects CAUSED by upstream process with noise)
#       -> pkl_coils (MSSQL, same coil ids)   + downtime_events (MySQL, ambient)
#
# PLANTED RELATIONSHIPS (discoverable by correlation/ML, each with noise):
#   R1 CRACK      ~ superheat excess (chain: EAF tap temp -> tundish superheat) + high cast speed
#   R2 INCLUSION  ~ scrap/DRI charge ratio of the ancestor heat
#   R3 WAVY_EDGE  ~ rolling force per mm of gauge (worst on thin strip)
#   R4 SCRATCH    ~ pure noise (control: the engine must NOT find a driver)
# CONSERVATION:  coil_width = slab_width - 2..6mm; coil_weight = slab_weight * 0.98;
#                coil_length = slab_length * (slab_thick/coil_thick) * 0.98; grade inherited.
# Idempotent: clears and reloads all six sources. Run with the source fleet containers up.
# ================================================================================================
$ErrorActionPreference = 'Stop'
$enc = New-Object System.Text.UTF8Encoding($false)
$rng = New-Object System.Random(42)

function N([double]$mu,[double]$sigma) {
    $u1 = $rng.NextDouble(); $u2 = $rng.NextDouble()
    return $mu + $sigma * [math]::Sqrt(-2.0*[math]::Log($u1)) * [math]::Cos(2.0*[math]::PI*$u2)
}
function U([double]$a,[double]$b) { return $a + ($b-$a)*$rng.NextDouble() }
function Poisson([double]$lambda) {
    if ($lambda -le 0) { return 0 }
    $L=[math]::Exp(-$lambda); $k=0; $p=1.0
    do { $k++; $p *= $rng.NextDouble() } while ($p -gt $L)
    return $k-1
}
function Ts([datetime]$d) { return $d.ToString('yyyy-MM-dd HH:mm:ss') }

Write-Host 'Generating physics-linked fleet (seed=42)...'
$t0 = (Get-Date).ToUniversalTime().AddDays(-30)

# grade catalog: liquidus temp, slab thickness mm, slab width mm, density factor
$grades = @(
    @{g='S355J2'; liq=1510.0; thick=220.0; width=1250.0},
    @{g='DD11';   liq=1520.0; thick=220.0; width=1500.0},
    @{g='DC01';   liq=1530.0; thick=250.0; width=1250.0}
)

# ---------- 1. HEATS (300) ----------
$heats = New-Object System.Collections.ArrayList
for ($i=1; $i -le 300; $i++) {
    $gr = $grades[$rng.Next(0,3)]
    $target = $gr.liq + 75.0
    $spike = 0.0; if ($rng.NextDouble() -lt 0.15) { $spike = U 15 30 }   # hot heats -> crack driver
    $tap = $target + (N 0 8) + $spike
    $scrap = [math]::Round((U 0.30 0.90), 3)                              # scrap:DRI ratio
    $arc = $tap + (N 25 6)
    $ox = 380 + 320*$scrap + (N 0 40)                                     # bonus: oxygen tracks scrap
    $c = if ($gr.g -eq 'DC01') { U 0.02 0.06 } else { U 0.08 0.18 }
    $start = $t0.AddMinutes(140*$i + (U -20 20))
    [void]$heats.Add(@{ id=('H-' + (3000+$i)); f=(1+($i%2)); s=$start; e=$start.AddMinutes(55+(U -8 8));
        g=$gr.g; liq=$gr.liq; target=$target; tap=[math]::Round($tap,1); c=[math]::Round($c,4);
        ox=[math]::Round($ox,1); scrap=$scrap; arc=[math]::Round($arc,1);
        sw=$gr.width; st=$gr.thick })
}

# ---------- 2. CASTER SEQUENCES / SLABS (~450) ----------
$slabs = New-Object System.Collections.ArrayList
$seq = 0
foreach ($h in $heats) {
    $n = 1 + $rng.Next(0,2)
    for ($k=0; $k -lt $n; $k++) {
        $seq++
        $superheat = ($h.tap - $h.liq) - (U 38 48) + (N 0 3)             # tundish loss; tracks tap temp
        if ($superheat -lt 8) { $superheat = 8 + (U 0 4) }
        $speed = [math]::Round((U 0.95 1.40),3)
        $len = [math]::Round((U 6.5 11.5),2)
        $wt = [math]::Round(($h.sw/1000.0)*($h.st/1000.0)*$len*7.85,2)   # tonnes
        $cs = $h.e.AddMinutes(15 + 20*$k)
        [void]$slabs.Add(@{ seq=('CS-'+(1000+$seq)); slab=('SL-'+(50000+$seq)); heat=$h.id; strand=(1+$k);
            s=$cs; e=$cs.AddMinutes(18+(U -3 3)); g=$h.g;
            speed=$speed; mold=[math]::Round((U 76 82),2); sh=[math]::Round($superheat,1);
            tund=[math]::Round(($h.liq+$superheat),1); w=$h.sw; t=$h.st; l=$len; wt=$wt;
            scrap=$h.scrap })
    }
}

# ---------- 3. HSM COILS + PASSES (~700 coils, 3 passes each) ----------
$coils = New-Object System.Collections.ArrayList
$hsmRows = New-Object System.Collections.ArrayList
$cn = 0
foreach ($s in $slabs) {
    $n = 1 + $rng.Next(0,2)
    for ($k=0; $k -lt $n; $k++) {
        $cn++
        $coilId = 'C-00' + (44000+$cn)
        $gauge = [math]::Round((U 2.0 6.0),2)
        $cw = [math]::Round(($s.w - (U 2 6)),1)                          # width conservation
        $cwt = [math]::Round(($s.wt * 0.98),2)                           # weight conservation (yield)
        $clen = [math]::Round(($s.l * ($s.t/$gauge) * 0.98),1)           # volume conservation
        $forceBase = if ($s.g -eq 'DC01') { 15000 } elseif ($s.g -eq 'DD11') { 17000 } else { 19000 }
        $force = [math]::Round(($forceBase * (1.0 + 1.8/([math]::Max($gauge,1.5))) * (1 + (N 0 0.05))),1)
        $rs = $s.e.AddMinutes(90 + 12*$k)
        for ($p=1; $p -le 3; $p++) {
            $entry = 1180 - 40*($p-1) + (N 0 8)
            [void]$hsmRows.Add(@{ pid=('HP-'+$cn+'-'+$p); coil=$coilId; slab=$s.slab; stand=$p;
                rs=$rs.AddSeconds(40*$p); et=[math]::Round($entry,1); xt=[math]::Round(($entry-55+(N 0 6)),1);
                rr=[math]::Round((0.30+0.12*$rng.NextDouble()),3);
                f=$(if ($p -eq 3) { $force } else { [math]::Round(($force*(U 0.85 0.95)),1) });
                w=$cw; th=$(if ($p -eq 3) { $gauge } else { [math]::Round(($s.t/(2.5*$p)),2) }) })
        }
        [void]$coils.Add(@{ id=$coilId; slab=$s.slab; heat=$s.heat; g=$s.g; gauge=$gauge; w=$cw; wt=$cwt;
            len=$clen; force=$force; fpg=[math]::Round(($force/$gauge),1);
            sh=$s.sh; speed=$s.speed; scrap=$s.scrap; done=$rs.AddMinutes(4) })
    }
}

# ---------- 4. SURFACE DEFECTS (planted relations + noise control) ----------
$defects = New-Object System.Collections.ArrayList
$dn = 0
foreach ($c in $coils) {
    $lamCrack = 0.15 + 0.09*[math]::Max(0.0, $c.sh - 32.0) + 2.2*[math]::Max(0.0, $c.speed - 1.28)
    $lamIncl  = 0.12 + 2.6*[math]::Max(0.0, $c.scrap - 0.60)
    $lamWavy  = 0.08 + 0.00045*[math]::Max(0.0, $c.fpg - 6500.0)
    $lamNoise = 0.55
    foreach ($def in @(@{code='CRACK';lam=$lamCrack},@{code='INCLUSION';lam=$lamIncl},
                       @{code='WAVY_EDGE';lam=$lamWavy},@{code='SCRATCH';lam=$lamNoise})) {
        $cnt = Poisson $def.lam
        for ($j=0; $j -lt $cnt; $j++) {
            $dn++
            $pos = U 5 ([math]::Max($c.len-5,10))
            $sev = 'Minor'; $r=$rng.NextDouble()
            if ($r -gt 0.85) { $sev='Critical' } elseif ($r -gt 0.55) { $sev='Major' }
            [void]$defects.Add(@{ id=('PD-'+(100000+$dn)); coil=$c.id; at=$c.done.AddMinutes(30);
                cam=('CAM-'+(1+$rng.Next(0,4))); code=$def.code; sev=$sev;
                p1=[math]::Round($pos,2); p2=[math]::Round(($pos+(U 0.2 3.0)),2);
                wmm=[math]::Round((U 2 45),2) })
        }
    }
}

# ---------- 5. PKL + DOWNTIME ----------
$pkl = New-Object System.Collections.ArrayList
foreach ($c in $coils) {
    if ($rng.NextDouble() -lt 0.85) {
        $en = $c.done.AddHours(6)
        [void]$pkl.Add(@{ id=$c.id; en=$en; ex=$en.AddMinutes(25+(U -5 5));
            acid=[math]::Round((U 78 88),1); speed=[math]::Round((150 - 12*$c.gauge + (N 0 6)),1) })
    }
}
$dt = New-Object System.Collections.ArrayList
$eq = @('EAF-1','EAF-2','CASTER-1','HSM-1','PKL-1')
$rs = @(@('MECH','Mechanical fault'),@('ELEC','Electrical trip'),@('PROC','Process hold'),@('MAINT','Planned maintenance'))
for ($i=1; $i -le 120; $i++) {
    $st = $t0.AddMinutes($rng.Next(0, 43200)); $r = $rs[$rng.Next(0,4)]
    [void]$dt.Add(@{ id=('DT-'+(7000+$i)); eq=$eq[$rng.Next(0,5)]; s=$st; e=$st.AddMinutes(10+$rng.Next(0,110));
        rc=$r[0]; rt=$r[1] })
}

Write-Host ("  heats=" + $heats.Count + " slabs=" + $slabs.Count + " coils=" + $coils.Count +
    " hsm_passes=" + $hsmRows.Count + " defects=" + $defects.Count + " pkl=" + $pkl.Count + " downtime=" + $dt.Count)

# ================================ WRITERS ================================
$tmp = Join-Path $env:TEMP 'ppiq-fleet'
New-Item -ItemType Directory -Path $tmp -Force | Out-Null

# ---- Postgres: meltshop ----
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('ALTER TABLE meltshop_heats ADD COLUMN IF NOT EXISTS scrap_dri_ratio numeric(6,3);')
[void]$sb.AppendLine('ALTER TABLE meltshop_heats ADD COLUMN IF NOT EXISTS avg_arc_temp_c numeric(10,3);')
[void]$sb.AppendLine('DELETE FROM meltshop_heats;')
foreach ($h in $heats) {
    [void]$sb.AppendLine("INSERT INTO meltshop_heats VALUES ('" + $h.id + "'," + $h.f + ",'" + (Ts $h.s) + "+00','" + (Ts $h.e) + "+00','" + $h.g + "'," + $h.target + "," + $h.tap + "," + $h.c + "," + $h.ox + "," + $h.scrap + "," + $h.arc + ");")
}
[System.IO.File]::WriteAllText((Join-Path $tmp 'meltshop.sql'), $sb.ToString(), $enc)
Write-Host 'Loading meltshop (postgres)...'
docker cp (Join-Path $tmp 'meltshop.sql') ppiq-src-meltshop-postgres:/tmp/fleet.sql | Out-Null
docker exec ppiq-src-meltshop-postgres psql -U ppiq_src -d meltshop -v ON_ERROR_STOP=1 -q -f /tmp/fleet.sql
if ($LASTEXITCODE -ne 0) { throw 'meltshop load failed' }

# ---- Oracle: caster ----
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('WHENEVER SQLERROR EXIT SQL.SQLCODE')
[void]$sb.AppendLine('ALTER SESSION SET CONTAINER=FREEPDB1;')
[void]$sb.AppendLine("BEGIN EXECUTE IMMEDIATE 'DROP USER ppiq_src CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;")
[void]$sb.AppendLine('/')
[void]$sb.AppendLine('CREATE USER ppiq_src IDENTIFIED BY ppiq_src_local_only QUOTA UNLIMITED ON USERS;')
[void]$sb.AppendLine('GRANT CREATE SESSION, CREATE TABLE TO ppiq_src;')
[void]$sb.AppendLine('CREATE TABLE ppiq_src.caster_sequences (')
[void]$sb.AppendLine('  seq_id VARCHAR2(40) PRIMARY KEY, slab_id VARCHAR2(40) NOT NULL, heat_no VARCHAR2(40) NOT NULL,')
[void]$sb.AppendLine('  strand_no NUMBER(2) NOT NULL, cast_start_utc TIMESTAMP NOT NULL, cast_end_utc TIMESTAMP,')
[void]$sb.AppendLine('  steel_grade VARCHAR2(40), cast_speed_mpm NUMBER(6,3), mold_level_pct NUMBER(5,2),')
[void]$sb.AppendLine('  superheat_c NUMBER(5,1), tundish_temp_c NUMBER(6,1), slab_width_mm NUMBER(6,1),')
[void]$sb.AppendLine('  slab_thick_mm NUMBER(6,1), slab_length_m NUMBER(6,2), slab_weight_t NUMBER(8,2),')
[void]$sb.AppendLine('  source_updated_at_utc TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL);')
foreach ($s in $slabs) {
    [void]$sb.AppendLine("INSERT INTO ppiq_src.caster_sequences VALUES ('" + $s.seq + "','" + $s.slab + "','" + $s.heat + "'," + $s.strand + ",TIMESTAMP '" + (Ts $s.s) + "',TIMESTAMP '" + (Ts $s.e) + "','" + $s.g + "'," + $s.speed + "," + $s.mold + "," + $s.sh + "," + $s.tund + "," + $s.w + "," + $s.t + "," + $s.l + "," + $s.wt + ",SYSTIMESTAMP);")
}
[void]$sb.AppendLine('COMMIT;')
[void]$sb.AppendLine('SELECT COUNT(*) AS caster_rows FROM ppiq_src.caster_sequences;')
[void]$sb.AppendLine('EXIT;')
[System.IO.File]::WriteAllText((Join-Path $tmp 'caster.sql'), $sb.ToString(), (New-Object System.Text.ASCIIEncoding))
Write-Host 'Loading caster (oracle)...'
docker cp (Join-Path $tmp 'caster.sql') ppiq-src-caster-oracle:/tmp/fleet.sql | Out-Null
docker exec ppiq-src-caster-oracle sh -c "sqlplus -S system/ppiq_src_local_only@localhost:1521/FREE @/tmp/fleet.sql 2>&1 | tail -4"

# ---- Oracle: hsm ----
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('WHENEVER SQLERROR EXIT SQL.SQLCODE')
[void]$sb.AppendLine('ALTER SESSION SET CONTAINER=FREEPDB1;')
[void]$sb.AppendLine("BEGIN EXECUTE IMMEDIATE 'DROP USER ppiq_src CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;")
[void]$sb.AppendLine('/')
[void]$sb.AppendLine('CREATE USER ppiq_src IDENTIFIED BY ppiq_src_local_only QUOTA UNLIMITED ON USERS;')
[void]$sb.AppendLine('GRANT CREATE SESSION, CREATE TABLE TO ppiq_src;')
[void]$sb.AppendLine('CREATE TABLE ppiq_src.hsm_passes (')
[void]$sb.AppendLine('  pass_id VARCHAR2(40) PRIMARY KEY, coil_id VARCHAR2(40) NOT NULL, slab_id VARCHAR2(40) NOT NULL,')
[void]$sb.AppendLine('  stand_no NUMBER(2) NOT NULL, roll_start_utc TIMESTAMP NOT NULL, entry_temp_c NUMBER(6,1),')
[void]$sb.AppendLine('  exit_temp_c NUMBER(6,1), reduction_ratio NUMBER(5,3), rolling_force_kn NUMBER(8,1),')
[void]$sb.AppendLine('  strip_width_mm NUMBER(6,1), strip_thick_mm NUMBER(5,2), coil_weight_t NUMBER(8,2),')
[void]$sb.AppendLine('  coil_length_m NUMBER(8,1), source_updated_at_utc TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL);')
$coilByid = @{}; foreach ($c in $coils) { $coilByid[$c.id] = $c }
foreach ($r in $hsmRows) {
    $c = $coilByid[$r.coil]
    $cw = $(if ($r.stand -eq 3) { $c.wt } else { 'NULL' })
    $cl = $(if ($r.stand -eq 3) { $c.len } else { 'NULL' })
    [void]$sb.AppendLine("INSERT INTO ppiq_src.hsm_passes VALUES ('" + $r.pid + "','" + $r.coil + "','" + $r.slab + "'," + $r.stand + ",TIMESTAMP '" + (Ts $r.rs) + "'," + $r.et + "," + $r.xt + "," + $r.rr + "," + $r.f + "," + $r.w + "," + $r.th + "," + $cw + "," + $cl + ",SYSTIMESTAMP);")
}
[void]$sb.AppendLine('COMMIT;')
[void]$sb.AppendLine('SELECT COUNT(*) AS hsm_rows FROM ppiq_src.hsm_passes;')
[void]$sb.AppendLine('EXIT;')
[System.IO.File]::WriteAllText((Join-Path $tmp 'hsm.sql'), $sb.ToString(), (New-Object System.Text.ASCIIEncoding))
Write-Host 'Loading hsm (oracle)...'
docker cp (Join-Path $tmp 'hsm.sql') ppiq-src-hsm-oracle:/tmp/fleet.sql | Out-Null
docker exec ppiq-src-hsm-oracle sh -c "sqlplus -S system/ppiq_src_local_only@localhost:1521/FREE @/tmp/fleet.sql 2>&1 | tail -4"

# ---- MySQL: parsytec ----
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('DELETE FROM parsytec_surface_defects;')
$batch = New-Object System.Collections.ArrayList
foreach ($d in $defects) {
    [void]$batch.Add("('" + $d.id + "','" + $d.coil + "','" + (Ts $d.at) + "','" + $d.cam + "','" + $d.code + "','" + $d.sev + "'," + $d.p1 + "," + $d.p2 + "," + $d.wmm + ")")
    if ($batch.Count -ge 200) {
        [void]$sb.AppendLine('INSERT INTO parsytec_surface_defects VALUES ' + ($batch -join ',') + ';')
        $batch.Clear()
    }
}
if ($batch.Count -gt 0) { [void]$sb.AppendLine('INSERT INTO parsytec_surface_defects VALUES ' + ($batch -join ',') + ';') }
[System.IO.File]::WriteAllText((Join-Path $tmp 'parsytec.sql'), $sb.ToString(), $enc)
Write-Host 'Loading parsytec (mysql)...'
docker cp (Join-Path $tmp 'parsytec.sql') ppiq-src-parsytec-mysql:/tmp/fleet.sql | Out-Null
docker exec ppiq-src-parsytec-mysql sh -c "mysql -uppiq_src -pppiq_src_local_only parsytec < /tmp/fleet.sql" 2>$null
if ($LASTEXITCODE -ne 0) { throw 'parsytec load failed' }

# ---- MySQL: downtime ----
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('DELETE FROM downtime_events;')
foreach ($d in $dt) {
    [void]$sb.AppendLine("INSERT INTO downtime_events VALUES ('" + $d.id + "','" + $d.eq + "','" + (Ts $d.s) + "','" + (Ts $d.e) + "','" + $d.rc + "','" + $d.rt + "');")
}
[System.IO.File]::WriteAllText((Join-Path $tmp 'downtime.sql'), $sb.ToString(), $enc)
Write-Host 'Loading downtime (mysql)...'
docker cp (Join-Path $tmp 'downtime.sql') ppiq-src-downtime-mysql:/tmp/fleet.sql | Out-Null
docker exec ppiq-src-downtime-mysql sh -c "mysql -uppiq_src -pppiq_src_local_only downtime < /tmp/fleet.sql" 2>$null
if ($LASTEXITCODE -ne 0) { throw 'downtime load failed' }

# ---- MSSQL: pkl ----
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('SET NOCOUNT ON;')
[void]$sb.AppendLine('DELETE FROM pkl_coils;')
foreach ($p in $pkl) {
    [void]$sb.AppendLine("INSERT INTO pkl_coils VALUES ('" + $p.id + "','" + (Ts $p.en) + "','" + (Ts $p.ex) + "'," + $p.acid + "," + $p.speed + ");")
}
[void]$sb.AppendLine("SELECT 'pkl_rows='+CAST(COUNT(*) AS varchar) FROM pkl_coils;")
[System.IO.File]::WriteAllText((Join-Path $tmp 'pkl.sql'), $sb.ToString(), (New-Object System.Text.ASCIIEncoding))
Write-Host 'Loading pkl (mssql)...'
docker cp (Join-Path $tmp 'pkl.sql') ppiq-src-pkl-mssql:/tmp/fleet.sql | Out-Null
docker exec ppiq-src-pkl-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Ppiq_Src_Local_Only1' -C -d pkl -i /tmp/fleet.sql

# ================================ PROOF ================================
Write-Host ''
Write-Host '=== PLANTED-CORRELATION SANITY PROOF (source-side) ===' -ForegroundColor Cyan
$hi = @($coils | Where-Object { $_.sh -gt 36 }); $lo = @($coils | Where-Object { $_.sh -le 30 })
function AvgDef($set,$code) {
    if ($set.Count -eq 0) { return 0 }
    $ids = @{}; foreach ($c in $set) { $ids[$c.id]=1 }
    $n = @($defects | Where-Object { $_.code -eq $code -and $ids.ContainsKey($_.coil) }).Count
    return [math]::Round($n / $set.Count, 3)
}
Write-Host ('  R1 CRACK/coil:     high-superheat=' + (AvgDef $hi 'CRACK') + '  vs low=' + (AvgDef $lo 'CRACK'))
$hs = @($coils | Where-Object { $_.scrap -gt 0.72 }); $ls = @($coils | Where-Object { $_.scrap -le 0.50 })
Write-Host ('  R2 INCLUSION/coil: high-scrap=' + (AvgDef $hs 'INCLUSION') + '  vs low=' + (AvgDef $ls 'INCLUSION'))
$hf = @($coils | Where-Object { $_.fpg -gt 8000 }); $lf = @($coils | Where-Object { $_.fpg -le 5500 })
Write-Host ('  R3 WAVY_EDGE/coil: high-force-per-gauge=' + (AvgDef $hf 'WAVY_EDGE') + '  vs low=' + (AvgDef $lf 'WAVY_EDGE'))
Write-Host ('  R4 SCRATCH/coil (control, should be ~equal): hiSH=' + (AvgDef $hi 'SCRATCH') + ' loSH=' + (AvgDef $lo 'SCRATCH'))
$c0 = $coils[0]
Write-Host ('  Genealogy sample: ' + $c0.heat + ' -> ' + $c0.slab + ' -> ' + $c0.id + ' (grade ' + $c0.g + ' inherited)')
Write-Host ''
Write-Host 'DONE. Fleet is coherent, genealogy-linked, physics-correlated. Re-run Stage-1 + Stage-2 to import.'
