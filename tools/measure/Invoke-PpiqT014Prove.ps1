#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-014 step 2 - prove the generator reproduces the captured donor state.

.DESCRIPTION
    Creates a SCRATCH database, applies the donor DDL, loads the generated data,
    runs the SAME capture profile against it, and compares the two profiles
    across the nine retirement-gate dimensions.

    Steps:
      1  Drop and create the scratch database. It is never the presentation one.
      2  Apply Backend/database/scripts/110_phase1_demo_source_shapes.sql.
      3  Run Backend/tools/generate_fleet_v2_donor.py to a SQL file.
      4  Load that SQL into the scratch database.
      5  Run tools/measure/Measure-PpiqT014Capture.ps1 against the SCRATCH
         database, writing to a scratch evidence folder.
      6  Compare with tools/measure/Compare-PpiqCaptureProfiles.py.

    SAFETY. The scratch name must not be the presentation or app database, and
    the script refuses to run if it is. Nothing here writes to ppiq_presentation.

.PARAMETER Captured
    The captured profile to compare against. Defaults to the newest
    T-014_capture_profile_*.txt in docs/m1/evidence.

.PARAMETER KeepScratch
    Leave the scratch database in place for inspection.

.EXAMPLE
    .\tools\measure\Invoke-PpiqT014Prove.ps1
#>

[CmdletBinding()]
param(
    [string]$PgHost      = "127.0.0.1",
    [int]   $PgPort      = 5432,
    [string]$PgUser      = "ppiq_dev",
    [string]$PgPassword  = "ppiq_dev_local_only",
    [string]$Scratch     = "ppiq_t014_scratch",
    [string]$Captured    = "",
    [string]$PsqlPath    = "",
    [switch]$KeepScratch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

function Say  { param([string]$T) Write-Host $T }
function Rule { param([string]$T) Write-Host ""; Write-Host ("=" * 78); Write-Host $T; Write-Host ("=" * 78) }

function Resolve-Psql {
    param([string]$Explicit)
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        if (Test-Path -LiteralPath $Explicit) { return $Explicit }
        return $null
    }
    $c = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($null -ne $c) { return $c.Source }
    foreach ($p in @("C:\Program Files\PostgreSQL\16\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\17\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\15\bin\psql.exe")) {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    return $null
}

function Invoke-Psql {
    param([string]$Database, [string]$Sql, [string]$File, [string]$Tag)
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
    $a = @("-X", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Database,
           "-v", "ON_ERROR_STOP=1")
    if ($File -ne "") {
        $a += @("-f", $File)
    } else {
        $s = Join-Path $script:tmp ($Tag + ".sql")
        [System.IO.File]::WriteAllText($s, $Sql, (New-Object System.Text.UTF8Encoding($false)))
        $a += @("-f", $s)
    }
    $a += @("-o", $o)
    $p = Start-Process -FilePath $script:psql -ArgumentList $a -NoNewWindow -Wait -PassThru `
                       -RedirectStandardError $e
    $err = ""
    if (Test-Path -LiteralPath $e) { $err = [System.IO.File]::ReadAllText($e) }
    if ($p.ExitCode -ne 0 -or $err -match "(?i)(ERROR|FATAL):") {
        Say ("[FAIL] psql step '" + $Tag + "' exit " + $p.ExitCode)
        if (-not [string]::IsNullOrWhiteSpace($err)) { Say $err }
        return $false
    }
    return $true
}

Rule "PPIQ T-014 STEP 2 - PROVE THE CAPTURE"

$root = (Get-Location).Path
Say ("Repo root : " + $root)

foreach ($forbidden in @("ppiq_presentation", "ppiq_app", "postgres")) {
    if ($Scratch -eq $forbidden) {
        Say ("[FAIL] refusing to use '" + $Scratch + "' as a scratch database.")
        exit 2
    }
}
Say ("Scratch   : " + $Scratch + "  (ppiq_presentation is never written)")

$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) {
    Say "[FAIL] psql.exe not found. Re-run with -PsqlPath."
    exit 2
}
Say ("psql      : " + $script:psql)

$ddl = Join-Path $root "Backend\database\scripts\110_phase1_demo_source_shapes.sql"
$gen = Join-Path $root "Backend\tools\generate_fleet_v2_donor.py"
$cap = Join-Path $root "tools\measure\Measure-PpiqT014Capture.ps1"
$cmp = Join-Path $root "tools\measure\Compare-PpiqCaptureProfiles.py"
$missing = 0
foreach ($f in @($ddl, $gen, $cap, $cmp)) {
    if (-not (Test-Path -LiteralPath $f)) {
        Say ("[FAIL] missing: " + $f.Substring($root.Length + 1))
        $missing = $missing + 1
    }
}
if ($missing -gt 0) { exit 2 }

if ($Captured -eq "") {
    $found = @(Get-ChildItem -LiteralPath (Join-Path $root "docs\m1\evidence") `
               -Filter "T-014_capture_profile_*.txt" -ErrorAction SilentlyContinue |
               Sort-Object LastWriteTime -Descending)
    if ($found.Count -eq 0) {
        Say "[FAIL] no captured profile found in docs\m1\evidence."
        exit 2
    }
    $Captured = $found[0].FullName
}
Say ("Captured  : " + (Split-Path $Captured -Leaf))

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t014_prove_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
$scratchEvidence = Join-Path $script:tmp "evidence"
New-Item -ItemType Directory -Path $scratchEvidence -Force | Out-Null

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$failed = 0

try {
    Rule "1 - SCRATCH DATABASE"
    $drop = "DROP DATABASE IF EXISTS " + $Scratch + ";`nCREATE DATABASE " + $Scratch + ";"
    if (-not (Invoke-Psql -Database "postgres" -Sql $drop -File "" -Tag "createdb")) { $failed = 1 }
    else { Say ("[OK] " + $Scratch + " created empty") }

    if ($failed -eq 0) {
        Rule "2 - DONOR DDL"
        if (-not (Invoke-Psql -Database $Scratch -Sql "" -File $ddl -Tag "ddl")) { $failed = 1 }
        else { Say "[OK] 110_phase1_demo_source_shapes.sql applied" }
    }

    if ($failed -eq 0) {
        Rule "3 - GENERATE"
        $sqlOut = Join-Path $script:tmp "donor_data.sql"
        $go = Join-Path $script:tmp "gen.out"
        $ge = Join-Path $script:tmp "gen.err"
        $gp = Start-Process -FilePath "python" `
                -ArgumentList @($gen, "--out", $sqlOut) `
                -WorkingDirectory $root -NoNewWindow -Wait -PassThru `
                -RedirectStandardOutput $go -RedirectStandardError $ge
        if (Test-Path -LiteralPath $go) { Say ([System.IO.File]::ReadAllText($go)) }
        if ($gp.ExitCode -ne 0) {
            Say "[FAIL] the generator refused to emit."
            if (Test-Path -LiteralPath $ge) { Say ([System.IO.File]::ReadAllText($ge)) }
            $failed = 1
        } else {
            Say ("[OK] " + [Math]::Round((Get-Item -LiteralPath $sqlOut).Length / 1MB, 2) + " MB of SQL")
        }
    }

    if ($failed -eq 0) {
        Rule "3b - COLUMN MANIFEST AGAINST information_schema"
        Say "Loading 10 MB to discover a wrong column name is the slow way to"
        Say "find out. The generator declares its columns; the database is asked"
        Say "for its own; a mismatch stops here."
        $mo = Join-Path $script:tmp "cols_gen.txt"
        $me = Join-Path $script:tmp "cols_gen.err"
        $mp = Start-Process -FilePath "python" -ArgumentList @($gen, "--columns") `
                -WorkingDirectory $root -NoNewWindow -Wait -PassThru `
                -RedirectStandardOutput $mo -RedirectStandardError $me
        if ($mp.ExitCode -ne 0) {
            Say "[FAIL] the generator could not produce its column manifest."
            $failed = 1
        } else {
            $dbSql = @'
\pset format unaligned
\pset tuples_only on
\pset fieldsep '|'
SELECT c.table_schema || '.' || c.table_name,
       string_agg(c.column_name, ',' ORDER BY c.ordinal_position)
FROM information_schema.columns c
JOIN pg_class pc ON pc.relname = c.table_name
JOIN pg_namespace pn ON pn.oid = pc.relnamespace AND pn.nspname = c.table_schema
WHERE pc.relkind = 'r'
  AND c.table_schema IN ('src_meltshop_pg','src_caster_oracle_shape',
                         'src_hsm_oracle_shape','src_pkl_mssql_shape',
                         'src_inspection_mysql_shape')
GROUP BY 1 ORDER BY 1;
'@
            if (-not (Invoke-Psql -Database $Scratch -Sql $dbSql -File "" -Tag "cols_db")) {
                $failed = 1
            } else {
                $dbMap = @{}
                foreach ($ln in @([System.IO.File]::ReadAllLines((Join-Path $script:tmp "cols_db.out")))) {
                    if ($ln -match "^(\S+?)\|(.+)$") { $dbMap[$Matches[1]] = $Matches[2] }
                }
                $mismatch = 0
                foreach ($ln in @([System.IO.File]::ReadAllLines($mo))) {
                    if ($ln -notmatch "^(\S+?)\|(.+)$") { continue }
                    $tbl = $Matches[1]
                    $genCols = @($Matches[2] -split ",")
                    if (-not $dbMap.ContainsKey($tbl)) {
                        Say ("[FAIL] " + $tbl + " does not exist in the database")
                        $mismatch = $mismatch + 1
                        continue
                    }
                    $dbCols = @($dbMap[$tbl] -split ",")
                    $extra = @($genCols | Where-Object { $dbCols -notcontains $_ })
                    $absent = @($dbCols | Where-Object { $genCols -notcontains $_ })
                    if ($extra.Count -eq 0 -and $absent.Count -eq 0) {
                        Say ("[OK]   " + $tbl.PadRight(52) + $genCols.Count.ToString() + " columns")
                    } else {
                        $mismatch = $mismatch + 1
                        Say ("[FAIL] " + $tbl)
                        if ($extra.Count -gt 0)  { Say ("         generator invents : " + ($extra -join ", ")) }
                        if ($absent.Count -gt 0) { Say ("         generator omits   : " + ($absent -join ", ")) }
                    }
                }
                if ($mismatch -gt 0) {
                    Say ""
                    Say ("[FAIL] " + $mismatch + " table(s) disagree. Nothing loaded.")
                    $failed = 1
                } else {
                    Say ""
                    Say "[OK] every generated column exists in the database, and none is missing."
                }
            }
        }
    }

    if ($failed -eq 0) {
        Rule "4 - LOAD"
        if (-not (Invoke-Psql -Database $Scratch -Sql "" -File $sqlOut -Tag "load")) { $failed = 1 }
        else { Say "[OK] donor data loaded into the scratch database" }
    }

    if ($failed -eq 0) {
        Rule "5 - CAPTURE THE SCRATCH DATABASE"
        $co = Join-Path $script:tmp "cap.out"
        $ce = Join-Path $script:tmp "cap.err"
        $cp = Start-Process -FilePath "powershell.exe" `
                -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $cap,
                                "-Database", $Scratch, "-OutDir", $scratchEvidence,
                                "-PgHost", $PgHost, "-PgPort", "$PgPort",
                                "-PgUser", $PgUser, "-PgPassword", $PgPassword) `
                -WorkingDirectory $root -NoNewWindow -Wait -PassThru `
                -RedirectStandardOutput $co -RedirectStandardError $ce
        $capErr = ""
        if (Test-Path -LiteralPath $ce) { $capErr = [System.IO.File]::ReadAllText($ce) }
        if ($cp.ExitCode -ne 0 -or $capErr -match "(?i)(ERROR|FATAL):") {
            Say ("[FAIL] the capture against scratch failed, exit " + $cp.ExitCode)
            if (Test-Path -LiteralPath $co) { Say ([System.IO.File]::ReadAllText($co)) }
            if (-not [string]::IsNullOrWhiteSpace($capErr)) { Say $capErr }
            $failed = 1
        } else {
            Say "[OK] scratch profile written"
        }
    }

    $regen = ""
    if ($failed -eq 0) {
        $found2 = @(Get-ChildItem -LiteralPath $scratchEvidence -Filter "T-014_capture_profile_*.txt" `
                    -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)
        if ($found2.Count -eq 0) {
            Say "[FAIL] the capture produced no scratch profile."
            $failed = 1
        } else {
            $regen = $found2[0].FullName
        }
    }

    if ($failed -eq 0) {
        Rule "6 - NINE-DIMENSION COMPARISON"
        $ko = Join-Path $script:tmp "cmp.out"
        $ke = Join-Path $script:tmp "cmp.err"
        $kp = Start-Process -FilePath "python" `
                -ArgumentList @($cmp, "--captured", $Captured, "--regenerated", $regen) `
                -WorkingDirectory $root -NoNewWindow -Wait -PassThru `
                -RedirectStandardOutput $ko -RedirectStandardError $ke
        $cmpText = ""
        if (Test-Path -LiteralPath $ko) { $cmpText = [System.IO.File]::ReadAllText($ko) }
        Say $cmpText
        if (Test-Path -LiteralPath $ke) {
            $t = [System.IO.File]::ReadAllText($ke)
            if (-not [string]::IsNullOrWhiteSpace($t)) { Say $t }
        }

        $outDir = Join-Path $root "docs\m1\evidence"
        $proofPath = Join-Path $outDir ("T-014_capture_proof_" + $stamp + ".txt")
        $header = @(
            "================================================================",
            "PPIQ T-014 - CAPTURE PROOF",
            "================================================================",
            ("Generated At : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss")),
            ("Captured     : " + (Split-Path $Captured -Leaf)),
            ("Regenerated  : scratch database " + $Scratch),
            "",
            "The generator reproduces the captured donor state. It does NOT",
            "reproduce row values and was never asked to - it reproduces the nine",
            "retirement-gate dimensions. Six measured faults are reproduced ON",
            "PURPOSE and are listed in the generator header; T-015 corrects them.",
            "================================================================",
            ""
        ) -join "`r`n"
        $clean = New-Object System.Text.StringBuilder
        foreach ($ch in ($cmpText -replace "`r`n", "`n").ToCharArray()) {
            if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
        }
        [System.IO.File]::WriteAllText($proofPath,
            $header + "`r`n" + ($clean.ToString() -replace "`n", "`r`n"),
            (New-Object System.Text.UTF8Encoding($false)))
        Say ("evidence written: docs\m1\evidence\" + (Split-Path $proofPath -Leaf))

        if ($kp.ExitCode -ne 0) { $failed = 1 }
    }
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    if (-not $KeepScratch) {
        $env:PGPASSWORD = $PgPassword
        Invoke-Psql -Database "postgres" -Sql ("DROP DATABASE IF EXISTS " + $Scratch + ";") -File "" -Tag "dropdb" | Out-Null
        Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
        Say ""
        Say ("[OK] scratch database dropped. Use -KeepScratch to inspect it.")
    }
}

Rule "RESULT"
if ($failed -ne 0) {
    Say "[FAIL] T-014 is NOT proven. Read the differences above."
    exit 1
}
Say "[OK] T-014 capture proven. Condition 1 of the retirement gate is met."
Say ""
Say "  git add Backend/tools/generate_fleet_v2_donor.py tools/measure/Compare-PpiqCaptureProfiles.py tools/measure/Invoke-PpiqT014Prove.ps1 docs/m1/evidence/T-014_capture_proof_*.txt"
Say "  git commit -m ""T-014: donor capture generator proven across nine dimensions"""
exit 0
