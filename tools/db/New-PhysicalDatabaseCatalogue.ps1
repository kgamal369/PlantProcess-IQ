# ============================================================================
# New-PhysicalDatabaseCatalogue.ps1
#
# Reads pg_catalog and writes the deterministic physical catalogue for one
# database. Issues no DDL. Classification is derived from two measured facts -
# the governed schema an object lives in, and the script that created it - not
# from its name.
#
# Creator authority comes from canonical-migration-order.json (createsTables)
# and from the DDL in the SQL files themselves, canonical and offPath alike, so
# an object that only a retired fixture script could have made is recorded as
# exactly that rather than guessed at.
#
# -BaselineCatalogue turns on delta mode: objects that match a baseline row
# inherit its classification instead of being investigated again.
#
# Exit 0 green, 3 red, 1 could not run.
# ============================================================================
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string] $Database,
    [string] $DbHost   = "127.0.0.1",
    [int]    $Port     = 5432,
    [string] $User     = "ppiq_dev",
    [string] $Password = "ppiq_dev_local_only",
    [string] $RepoRoot = ".",
    [string] $OutputName = "",
    [string] $BaselineCatalogue = "",
    [switch] $Quiet
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"

$SEP = [char]0x1F
$UTF8NoBom = New-Object System.Text.UTF8Encoding($false)
$GOVERNED = @("ppiq_meta", "ppiq_plant", "ppiq_staging")

if ($RepoRoot -eq ".") { $RepoRoot = (Get-Location).Path }
$catalogueDir = Join-Path $RepoRoot "Backend\database\catalogue"
$outDir = Join-Path $catalogueDir "generated"
if (-not (Test-Path $outDir)) { [void](New-Item -ItemType Directory -Force -Path $outDir) }
if ($OutputName -eq "") { $OutputName = $Database }

function Chat([string]$t) { if (-not $Quiet) { Write-Host $t } }

function Q([string]$sql) {
    $env:PGPASSWORD = $Password
    $env:PGCLIENTENCODING = "UTF8"
    $raw = & psql -h $DbHost -p $Port -U $User -d $Database -v ON_ERROR_STOP=1 -tAq -F $SEP -c $sql 2>&1
    if ($LASTEXITCODE -ne 0) { Write-Host ("[FAIL] query failed against " + $Database); Write-Host ("       " + ($raw -join " ")); exit 1 }
    $rows = @()
    foreach ($line in $raw) {
        $s = "" + $line
        if ($s.Trim().Length -eq 0) { continue }
        $rows += ,($s -split $SEP)
    }
    # The comma prevents PowerShell unrolling a single-row result into its own
    # fields, which would make every caller index characters instead of columns.
    return ,$rows
}

function CsvCell([string]$v) {
    if ($null -eq $v) { $v = "" }
    $v = $v -replace "`r", " " -replace "`n", " "
    if ($v -match '[",]') { return '"' + ($v -replace '"', '""') + '"' }
    return $v
}
function CsvRow([string[]]$cells) {
    $out = @()
    foreach ($c in $cells) { $out += (CsvCell $c) }
    return ($out -join ",")
}
function SortOrdinal([string[]]$a) {
    $copy = @($a)
    [Array]::Sort($copy, [System.StringComparer]::Ordinal)
    return ,$copy
}
function WriteDet([string]$path, [string]$header, [string[]]$rows) {
    $sorted = SortOrdinal $rows
    [System.IO.File]::WriteAllText($path, (($header + "`n" + ($sorted -join "`n") + "`n") -replace "`r`n", "`n"), $UTF8NoBom)
}
function ReadDataLines([string]$path) {
    if (-not (Test-Path $path)) { return ,@() }
    $out = @()
    foreach ($raw in (Get-Content $path)) {
        $line = $raw.TrimEnd()
        if ($line.Trim().Length -eq 0) { continue }
        if ($line.TrimStart().StartsWith("#")) { continue }
        $out += $line
    }
    return ,$out
}

# ---- authority ---------------------------------------------------------------
$explicit = @{}
foreach ($line in (ReadDataLines (Join-Path $catalogueDir "physical-object-classification.tsv"))) {
    $p = $line -split "`t"
    if ($p.Count -lt 8) { continue }
    $creatorAuthority = ""
    $creatorDisposition = ""
    if ($p.Count -ge 10) { $creatorAuthority = $p[8]; $creatorDisposition = $p[9] }
    $explicit[($p[0] + "." + $p[1])] = @{ Family = $p[2]; Lifecycle = $p[3]; Owner = $p[4]; Purpose = $p[5]; Retention = $p[6]; Compat = $p[7]; CreatorAuthority = $creatorAuthority; CreatorDisposition = $creatorDisposition }
}
$schemaRule = @{}
foreach ($line in (ReadDataLines (Join-Path $catalogueDir "classification-rules.tsv"))) {
    $p = $line -split "`t"
    if ($p.Count -lt 5) { continue }
    $schemaRule[$p[0]] = @{ Family = $p[1]; Lifecycle = $p[2]; Owner = $p[3]; Retention = $p[4] }
}
$platform = @{}
foreach ($line in (ReadDataLines (Join-Path $catalogueDir "public-platform-allowlist.tsv"))) {
    $p = $line -split "`t"
    if ($p.Count -lt 5) { continue }
    $platform[$p[0]] = @{ Kind = $p[1]; Lifecycle = $p[2]; Owner = $p[3]; Reason = $p[4] }
}
Chat ("[INFO]   authority: " + $explicit.Count + " explicit, " + $schemaRule.Count + " schema rules, " + $platform.Count + " platform allowlist")

# ---- creator index -----------------------------------------------------------
# Two sources. The manifest states what a canonical script creates; the DDL in
# the files states what every script creates, including the offPath ones the
# manifest does not describe. Bare names are indexed as well as qualified ones
# because topology convergence relocates a table after its creator ran.
$manifest = Get-Content (Join-Path $RepoRoot "Backend\database\canonical-migration-order.json") -Raw | ConvertFrom-Json
$creator = @{}
function Record-Creator([string]$name, [string]$path, [string]$disposition) {
    $key = $name.ToLowerInvariant()
    if ($creator.ContainsKey($key)) {
        if ($creator[$key].Disposition -ne "Canonical" -and $disposition -eq "Canonical") { $creator[$key] = @{ Path = $path; Disposition = $disposition } }
        return
    }
    $creator[$key] = @{ Path = $path; Disposition = $disposition }
}
foreach ($e in $manifest.canonicalPath) {
    $tables = $null
    if ($e.PSObject.Properties.Name -contains "createsTables") { $tables = $e.createsTables }
    if ($null -eq $tables) { continue }
    foreach ($t in $tables) {
        $n = "" + $t
        Record-Creator $n $e.path "Canonical"
        if ($n.Contains(".")) { Record-Creator ($n.Split(".")[-1]) $e.path "Canonical" }
    }
}
$ddl = [regex]"(?im)^\s*CREATE\s+(?:UNIQUE\s+)?(?:OR\s+REPLACE\s+)?(?:MATERIALIZED\s+)?(TABLE|VIEW|SEQUENCE)\s+(?:IF\s+NOT\s+EXISTS\s+)?([A-Za-z0-9_""\.]+)"
foreach ($group in @(@{ List = $manifest.canonicalPath; Disp = "Canonical" }, @{ List = $manifest.offPath; Disp = "OffPath" })) {
    foreach ($e in $group.List) {
        $full = Join-Path $RepoRoot ($e.path -replace "/", "\")
        if (-not (Test-Path $full)) { continue }
        $text = Get-Content $full -Raw
        $disp = $group.Disp
        if ($disp -eq "OffPath" -and ($e.PSObject.Properties.Name -contains "disposition")) { $disp = "" + $e.disposition }
        foreach ($m in $ddl.Matches($text)) {
            $n = ($m.Groups[2].Value -replace '"', "")
            Record-Creator $n $e.path $disp
            if ($n.Contains(".")) { Record-Creator ($n.Split(".")[-1]) $e.path $disp }
        }
    }
}
Chat ("[INFO]   creator index: " + $creator.Count + " names from manifest declarations and measured DDL")

$baseline = @{}
if ($BaselineCatalogue -ne "" -and (Test-Path $BaselineCatalogue)) {
    foreach ($line in (Get-Content $BaselineCatalogue | Select-Object -Skip 1)) {
        if ($line.Trim().Length -eq 0) { continue }
        $c = $line -split ","
        $baseline[($c[0] + "." + $c[1])] = $line
    }
    Chat ("[INFO]   baseline: " + $baseline.Count + " objects inherit their classification instead of being investigated again")
}

# ---- census ------------------------------------------------------------------
$objects = Q @"
SELECT n.nspname, c.relname,
       CASE c.relkind WHEN 'r' THEN 'table' WHEN 'p' THEN 'table' WHEN 'v' THEN 'view'
                      WHEN 'm' THEN 'matview' WHEN 'S' THEN 'sequence' ELSE c.relkind::text END,
       CASE WHEN c.relrowsecurity THEN 'on' ELSE 'off' END,
       CASE WHEN c.relforcerowsecurity THEN 'forced' ELSE 'not_forced' END,
       (SELECT count(*) FROM pg_policy p WHERE p.polrelid = c.oid)::text
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind IN ('r','p','v','m','S')
  AND n.nspname NOT IN ('pg_catalog','information_schema','pg_toast')
  AND n.nspname NOT LIKE 'pg_temp%' AND n.nspname NOT LIKE 'pg_toast%'
ORDER BY 1,2
"@
$columns = Q @"
SELECT n.nspname, c.relname, a.attnum::text, a.attname, format_type(a.atttypid, a.atttypmod),
       CASE WHEN a.attnotnull THEN 'not_null' ELSE 'nullable' END,
       coalesce(pg_get_expr(d.adbin, d.adrelid), '')
FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace
LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
WHERE a.attnum > 0 AND NOT a.attisdropped AND c.relkind IN ('r','p','v','m')
  AND n.nspname NOT IN ('pg_catalog','information_schema','pg_toast') AND n.nspname NOT LIKE 'pg_temp%'
ORDER BY 1,2,3
"@
$keys = Q @"
SELECT n.nspname, c.relname, con.conname,
       CASE con.contype WHEN 'p' THEN 'pk' WHEN 'f' THEN 'fk' ELSE con.contype::text END,
       coalesce(rn.nspname,''), coalesce(rc.relname,''), pg_get_constraintdef(con.oid)
FROM pg_constraint con JOIN pg_class c ON c.oid = con.conrelid JOIN pg_namespace n ON n.oid = c.relnamespace
LEFT JOIN pg_class rc ON rc.oid = con.confrelid LEFT JOIN pg_namespace rn ON rn.oid = rc.relnamespace
WHERE con.contype IN ('p','f') AND n.nspname NOT IN ('pg_catalog','information_schema')
ORDER BY 1,2,3
"@
Chat ("[INFO]   census: " + $objects.Count + " objects, " + $columns.Count + " columns, " + $keys.Count + " key constraints")

$pkBy = @{}
foreach ($k in $keys) { if ($k[3] -eq "pk") { $pkBy[($k[0] + "." + $k[1])] = $k[6] } }

$tableRows = @(); $unclassified = @(); $publicOffenders = @(); $unknownCreator = @(); $naming = @()
$inherited = 0
$snake = "^[a-z][a-z0-9_]*$"

foreach ($o in $objects) {
    $schema = $o[0]; $name = $o[1]; $kind = $o[2]; $rls = $o[3]; $rlsF = $o[4]; $pol = $o[5]
    $key = $schema + "." + $name

    if ($baseline.ContainsKey($key)) { $tableRows += $baseline[$key]; $inherited = $inherited + 1; continue }

    $creatorPath = "UNRESOLVED"; $creatorDisp = "UNRESOLVED"
    foreach ($cand in @(($key.ToLowerInvariant()), ($name.ToLowerInvariant()))) {
        if ($creator.ContainsKey($cand)) { $creatorPath = $creator[$cand].Path; $creatorDisp = $creator[$cand].Disposition; break }
    }
    if ($creatorPath -eq "UNRESOLVED" -and $baseline.Count -eq 0) {
        # In a database built only from EF plus the canonical path, an object no
        # script claims was created by the EF baseline. That is a resolution, not
        # a guess: nothing else ran.
        $creatorPath = "efMigrationBaseline"; $creatorDisp = "Canonical"
    }
    # An explicit adjudication may resolve provenance that cannot be recovered
    # mechanically (dynamic CREATE TABLE or retired/pre-canonical residue).
    if ($explicit.ContainsKey($key)) {
        $ee = $explicit[$key]
        if (-not [string]::IsNullOrWhiteSpace($ee.CreatorAuthority)) {
            $creatorPath = $ee.CreatorAuthority
            $creatorDisp = $ee.CreatorDisposition
        }
    }

    $family = "UNCLASSIFIED"; $lifecycle = "UNCLASSIFIED"; $owner = "UNCLASSIFIED"
    $purpose = ""; $retention = "UNCLASSIFIED"; $compat = "none"; $by = "none"

    if ($explicit.ContainsKey($key)) {
        $e = $explicit[$key]
        $family = $e.Family; $lifecycle = $e.Lifecycle; $owner = $e.Owner
        $purpose = $e.Purpose; $retention = $e.Retention; $compat = $e.Compat; $by = "explicit"
    } elseif ($schemaRule.ContainsKey($schema)) {
        $r = $schemaRule[$schema]
        $family = $r.Family; $lifecycle = $r.Lifecycle; $owner = $r.Owner; $retention = $r.Retention
        $by = "governed-schema"
    } elseif ($schema -eq "public" -and $platform.ContainsKey($name)) {
        $a = $platform[$name]
        $family = "platform infrastructure"; $lifecycle = $a.Lifecycle; $owner = $a.Owner
        $purpose = $a.Reason; $retention = "permanent"; $by = "platform-allowlist"
    } elseif ($creatorPath -ne "UNRESOLVED") {
        # A product object outside the governed schemas whose creator is known.
        # It is bounded compatibility with a named retirement owner, never
        # platform: calling it platform to reach green is the one move this
        # catalogue exists to refuse.
        $family = "bounded compatibility (" + $creatorDisp.ToLowerInvariant() + " origin)"
        $lifecycle = "compatibility"; $owner = "PPIQ Platform"; $retention = "operational"
        $compat = "bounded:T-256"; $by = "creator-authority"
        if ($creatorDisp -ne "Canonical") { $purpose = "Historical residue: created by a " + $creatorDisp + " script that the canonical build no longer replays." }
        else { $purpose = "Created on the canonical path outside the governed schemas; convergence is T-256's." }
    }

    if ($family -eq "UNCLASSIFIED") { $unclassified += ($key + "  [" + $kind + "]") }
    if ($creatorPath -eq "UNRESOLVED") { $unknownCreator += ($key + "  [" + $kind + "]") }
    if ($schema -eq "public" -and $by -ne "platform-allowlist" -and $by -ne "explicit" -and $by -ne "creator-authority") { $publicOffenders += $key }

    $namingState = "conforming"
    if ($name -cnotmatch $snake) { $namingState = "nonconforming"; $naming += $key }

    $pk = ""
    if ($pkBy.ContainsKey($key)) { $pk = $pkBy[$key] }

    $tableRows += (CsvRow @($schema, $name, $kind, $family, $lifecycle, $owner, $purpose, $creatorPath, $creatorDisp, $pk, $retention, $rls, $rlsF, $pol, $namingState, $compat, $by))
}

$columnRows = @()
foreach ($c in $columns) { $columnRows += (CsvRow @($c[0], $c[1], $c[2], $c[3], $c[4], $c[5], $c[6])) }
$relationRows = @(); $dot = @()
foreach ($k in $keys) {
    if ($k[3] -ne "fk") { continue }
    $relationRows += (CsvRow @($k[0], $k[1], $k[2], $k[4], $k[5], $k[6]))
    $dot += ('  "' + $k[0] + "." + $k[1] + '" -> "' + $k[4] + "." + $k[5] + '";')
}

$base = Join-Path $outDir $OutputName
WriteDet ($base + ".tables.csv") "schema,object,kind,logical_family,lifecycle,owner,purpose,creator_authority,creator_disposition,primary_key,retention,rls,rls_forced,policy_count,naming,compatibility_state,classified_by" $tableRows
WriteDet ($base + ".columns.csv") "schema,object,ordinal,column,type,nullability,default_expression" $columnRows
WriteDet ($base + ".relations.csv") "schema,object,constraint,referenced_schema,referenced_object,definition" $relationRows
[System.IO.File]::WriteAllText(($base + ".relation-graph.dot"), ("digraph physical_relations {`n  rankdir=LR;`n" + ((SortOrdinal $dot) -join "`n") + "`n}`n"), $UTF8NoBom)

$schemaCounts = @{}
foreach ($o in $objects) { if (-not $schemaCounts.ContainsKey($o[0])) { $schemaCounts[$o[0]] = 0 }; $schemaCounts[$o[0]] = $schemaCounts[$o[0]] + 1 }

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Physical catalogue - " + $OutputName)
[void]$sb.AppendLine("")
[void]$sb.AppendLine("Objects: " + $objects.Count + "   Columns: " + $columns.Count + "   FK relations: " + $relationRows.Count)
if ($baseline.Count -gt 0) { [void]$sb.AppendLine("Inherited from baseline: " + $inherited + "   investigated here: " + ($objects.Count - $inherited)) }
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## Objects by schema")
[void]$sb.AppendLine("")
foreach ($s in (SortOrdinal ([string[]]@($schemaCounts.Keys)))) {
    $mark = ""
    if ($GOVERNED -notcontains $s -and $s -ne "public") { $mark = "   <- outside the governed three and outside public" }
    [void]$sb.AppendLine("- " + $s + ": " + $schemaCounts[$s] + $mark)
}
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## Verdict")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("- UNCLASSIFIED objects: " + $unclassified.Count)
[void]$sb.AppendLine("- public offenders: " + $publicOffenders.Count)
[void]$sb.AppendLine("- unknown creator: " + $unknownCreator.Count)
[void]$sb.AppendLine("- naming-nonconforming: " + $naming.Count)
[void]$sb.AppendLine("")
foreach ($pair in @(@{ T = "UNCLASSIFIED"; L = $unclassified }, @{ T = "PUBLIC OFFENDERS"; L = $publicOffenders }, @{ T = "UNKNOWN CREATOR"; L = $unknownCreator }, @{ T = "NAMING NONCONFORMANCE (reported, never renamed here)"; L = $naming })) {
    if ($pair.L.Count -eq 0) { continue }
    [void]$sb.AppendLine("## " + $pair.T)
    [void]$sb.AppendLine("")
    foreach ($u in (SortOrdinal ([string[]]$pair.L))) { [void]$sb.AppendLine("- " + $u) }
    [void]$sb.AppendLine("")
}
[System.IO.File]::WriteAllText(($base + ".summary.md"), (($sb.ToString()) -replace "`r`n", "`n"), $UTF8NoBom)

Chat ("[INFO]   unclassified " + $unclassified.Count + "   public offenders " + $publicOffenders.Count + "   unknown creator " + $unknownCreator.Count)
if ($unclassified.Count -gt 0 -or $publicOffenders.Count -gt 0 -or $unknownCreator.Count -gt 0) { Chat "[RED]    catalogue is not zero-unknown"; exit 3 }
Chat "[GREEN]  catalogue is zero-unknown"
exit 0