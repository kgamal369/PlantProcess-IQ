# ============================================================================
# New-PhysicalDatabaseCatalogue.ps1
#
# Reads pg_catalog and writes the deterministic physical catalogue for one
# database. Issues NO DDL. Takes no lock it does not need. If this script
# changes the database it is inspecting, that is a defect in this script.
#
# Determinism: the hashed artifacts carry no timestamp and every row is sorted
# ordinally. A rerun against an unchanged database is byte-identical.
#
#   powershell -ExecutionPolicy Bypass -File tools\db\New-PhysicalDatabaseCatalogue.ps1 `
#       -Database ppiq_acceptance_empty
#
# Exit 0 green, 3 red (unknown/unclassified/out-of-place), 1 could not run.
# ============================================================================
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string] $Database,
    [string] $DbHost   = "127.0.0.1",
    [int]    $Port     = 5432,
    [string] $User     = "ppiq_dev",
    [string] $Password = "ppiq_dev_local_only",
    [string] $RepoRoot = ".",
    [string] $OutDir   = "",
    [switch] $Quiet
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"

$SEP = [char]0x1F
$UTF8NoBom = New-Object System.Text.UTF8Encoding($false)
$GOVERNED = @("ppiq_meta", "ppiq_plant", "ppiq_staging")

if ($RepoRoot -eq ".") { $RepoRoot = (Get-Location).Path }
$catalogueDir = Join-Path $RepoRoot "Backend\database\catalogue"
if ($OutDir -eq "") { $OutDir = Join-Path $catalogueDir "generated" }
if (-not (Test-Path $OutDir)) { [void](New-Item -ItemType Directory -Force -Path $OutDir) }

function Chat([string]$t) { if (-not $Quiet) { Write-Host $t } }

function Q([string]$sql) {
    $env:PGPASSWORD       = $Password
    $env:PGCLIENTENCODING = "UTF8"
    $raw = & psql -h $DbHost -p $Port -U $User -d $Database -v ON_ERROR_STOP=1 -tAq -F $SEP -c $sql 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host ("[FAIL] query failed against " + $Database)
        Write-Host ("       " + ($raw -join " "))
        exit 1
    }
    $rows = @()
    foreach ($line in $raw) {
        $s = "" + $line
        if ($s.Trim().Length -eq 0) { continue }
        $rows += ,($s -split $SEP)
    }
    # The comma is load-bearing. PowerShell unrolls a single-element collection on
    # return, so a database holding exactly one object would return that object's
    # six fields as six separate rows and every caller would then index characters
    # instead of columns. Found by the negative control, which is the one case that
    # reliably produces a single-row census.
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
function WriteDeterministic([string]$path, [string]$header, [string[]]$rows) {
    $sorted = SortOrdinal $rows
    $text = $header + "`n" + (($sorted) -join "`n") + "`n"
    [System.IO.File]::WriteAllText($path, ($text -replace "`r`n", "`n"), $UTF8NoBom)
}

# ---- authority files --------------------------------------------------------
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

$explicit = @{}
foreach ($line in (ReadDataLines (Join-Path $catalogueDir "physical-object-classification.tsv"))) {
    $p = $line -split "`t"
    if ($p.Count -lt 8) { continue }
    $explicit[($p[0] + "." + $p[1])] = @{
        Family = $p[2]; Lifecycle = $p[3]; Owner = $p[4]
        Purpose = $p[5]; Retention = $p[6]; Compat = $p[7]; Source = "explicit"
    }
}

$rules = @()
foreach ($line in (ReadDataLines (Join-Path $catalogueDir "classification-rules.tsv"))) {
    $p = $line -split "`t"
    if ($p.Count -lt 7) { continue }
    $rules += ,@{
        Schema = $p[0]; Pattern = $p[1]; Kind = $p[2]
        Family = $p[3]; Lifecycle = $p[4]; Owner = $p[5]; Retention = $p[6]
        Hits = 0
    }
}

$publicAllow = @{}
foreach ($line in (ReadDataLines (Join-Path $catalogueDir "public-platform-allowlist.tsv"))) {
    $p = $line -split "`t"
    if ($p.Count -lt 5) { continue }
    $publicAllow[$p[0]] = @{ Kind = $p[1]; Lifecycle = $p[2]; Owner = $p[3]; Reason = $p[4] }
}

# creator authority: canonical-migration-order.json already records, per script,
# the tables that script creates. That mapping IS the creator authority; it is
# not re-derived here, because a second derivation would be a second authority.
$creator = @{}
$orderPath = Join-Path $RepoRoot "Backend\database\canonical-migration-order.json"
if (Test-Path $orderPath) {
    $order = Get-Content $orderPath -Raw | ConvertFrom-Json
    foreach ($entry in $order.canonicalPath) {
        $names = $null
        if ($entry.PSObject.Properties.Name -contains "createsTables") { $names = $entry.createsTables }
        if ($null -eq $names) { continue }
        foreach ($t in $names) {
            $key = "" + $t
            if (-not $creator.ContainsKey($key)) { $creator[$key] = $entry.path }
        }
    }
}
Chat ("[INFO]   creator authority: " + $creator.Count + " table names mapped from canonical-migration-order.json")
Chat ("[INFO]   explicit adjudications: " + $explicit.Count)
Chat ("[INFO]   pattern rules: " + $rules.Count)
Chat ("[INFO]   public allowlist: " + $publicAllow.Count)

# ---- census -----------------------------------------------------------------
$objSql = @"
SELECT n.nspname, c.relname,
       CASE c.relkind WHEN 'r' THEN 'table' WHEN 'p' THEN 'table'
                      WHEN 'v' THEN 'view'  WHEN 'm' THEN 'matview'
                      WHEN 'S' THEN 'sequence' ELSE c.relkind::text END,
       CASE WHEN c.relrowsecurity THEN 'on' ELSE 'off' END,
       CASE WHEN c.relforcerowsecurity THEN 'forced' ELSE 'not_forced' END,
       (SELECT count(*) FROM pg_policy p WHERE p.polrelid = c.oid)::text
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind IN ('r','p','v','m','S')
  AND n.nspname NOT IN ('pg_catalog','information_schema','pg_toast')
  AND n.nspname NOT LIKE 'pg_temp%'
  AND n.nspname NOT LIKE 'pg_toast%'
ORDER BY 1,2
"@
$objects = Q $objSql
Chat ("[INFO]   census: " + $objects.Count + " objects in " + $Database)

$colSql = @"
SELECT n.nspname, c.relname, a.attnum::text, a.attname,
       format_type(a.atttypid, a.atttypmod),
       CASE WHEN a.attnotnull THEN 'not_null' ELSE 'nullable' END,
       coalesce(pg_get_expr(d.adbin, d.adrelid), '')
FROM pg_attribute a
JOIN pg_class c ON c.oid = a.attrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
WHERE a.attnum > 0 AND NOT a.attisdropped
  AND c.relkind IN ('r','p','v','m')
  AND n.nspname NOT IN ('pg_catalog','information_schema','pg_toast')
  AND n.nspname NOT LIKE 'pg_temp%'
ORDER BY 1,2,3
"@
$columns = Q $colSql
Chat ("[INFO]   census: " + $columns.Count + " columns")

$keySql = @"
SELECT n.nspname, c.relname, con.conname,
       CASE con.contype WHEN 'p' THEN 'pk' WHEN 'f' THEN 'fk' ELSE con.contype::text END,
       coalesce(rn.nspname,''), coalesce(rc.relname,''),
       pg_get_constraintdef(con.oid)
FROM pg_constraint con
JOIN pg_class c ON c.oid = con.conrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
LEFT JOIN pg_class rc ON rc.oid = con.confrelid
LEFT JOIN pg_namespace rn ON rn.oid = rc.relnamespace
WHERE con.contype IN ('p','f')
  AND n.nspname NOT IN ('pg_catalog','information_schema')
ORDER BY 1,2,3
"@
$keys = Q $keySql
Chat ("[INFO]   census: " + $keys.Count + " key constraints")

# ---- classify ---------------------------------------------------------------
$pkByTable = @{}
foreach ($k in $keys) {
    if ($k[3] -ne "pk") { continue }
    $pkByTable[($k[0] + "." + $k[1])] = $k[6]
}

$tableRows    = @()
$unclassified = @()
$outOfPlace   = @()
$namingBreach = @()

$snake = "^[a-z][a-z0-9_]*$"

foreach ($o in $objects) {
    $schema = $o[0]; $name = $o[1]; $kind = $o[2]
    $rls = $o[3];    $rlsForced = $o[4]; $policies = $o[5]
    $key = $schema + "." + $name

    $family = "UNCLASSIFIED"; $lifecycle = "UNCLASSIFIED"; $owner = "UNCLASSIFIED"
    $purpose = ""; $retention = "UNCLASSIFIED"; $compat = "none"; $source = "none"

    if ($explicit.ContainsKey($key)) {
        $e = $explicit[$key]
        $family = $e.Family; $lifecycle = $e.Lifecycle; $owner = $e.Owner
        $purpose = $e.Purpose; $retention = $e.Retention; $compat = $e.Compat
        $source = "explicit"
    } elseif ($schema -eq "public" -and $publicAllow.ContainsKey($name)) {
        $a = $publicAllow[$name]
        $family = "platform"; $lifecycle = $a.Lifecycle; $owner = $a.Owner
        $purpose = $a.Reason; $retention = "permanent"
        $compat = $(if ($a.Lifecycle -eq "compatibility") { "bounded:unstated" } else { "none" })
        $source = "public-allowlist"
    } else {
        foreach ($r in $rules) {
            if ($r.Schema -ne "*" -and $r.Schema -ne $schema) { continue }
            if ($r.Kind   -ne "*" -and $r.Kind   -ne $kind)   { continue }
            if ($name -clike $r.Pattern) {
                $family = $r.Family; $lifecycle = $r.Lifecycle; $owner = $r.Owner
                $retention = $r.Retention; $source = "rule"
                $r.Hits = $r.Hits + 1
                break
            }
        }
    }

    if ($schema -eq "public" -and $source -ne "public-allowlist" -and $source -ne "explicit") {
        $outOfPlace += $key
    }
    if ($family -eq "UNCLASSIFIED") {
        $unclassified += ($key + "  [" + $kind + "]")
    }

    $namingOk = "conforming"
    if ($name -cnotmatch $snake) {
        $namingOk = "nonconforming"
        $namingBreach += $key
    }

    $creatorAuthority = "UNKNOWN"
    foreach ($cand in @($key, ("public." + $name), $name)) {
        if ($creator.ContainsKey($cand)) { $creatorAuthority = $creator[$cand]; break }
    }

    $pk = ""
    if ($pkByTable.ContainsKey($key)) { $pk = $pkByTable[$key] }

    $tableRows += (CsvRow @(
        $schema, $name, $kind, $family, $lifecycle, $owner, $purpose,
        $creatorAuthority, $pk, $retention, $rls, $rlsForced, $policies,
        $namingOk, $compat, $source
    ))
}

$columnRows = @()
foreach ($c in $columns) {
    $columnRows += (CsvRow @($c[0], $c[1], $c[2], $c[3], $c[4], $c[5], $c[6]))
}

$relationRows = @()
$dotEdges = @()
foreach ($k in $keys) {
    if ($k[3] -ne "fk") { continue }
    $relationRows += (CsvRow @($k[0], $k[1], $k[2], $k[4], $k[5], $k[6]))
    $dotEdges += ('  "' + $k[0] + "." + $k[1] + '" -> "' + $k[4] + "." + $k[5] + '";')
}

# ---- write ------------------------------------------------------------------
$base = Join-Path $OutDir $Database
WriteDeterministic ($base + ".tables.csv") `
    "schema,object,kind,logical_family,lifecycle,owner,purpose,creator_authority,primary_key,retention,rls,rls_forced,policy_count,naming,compatibility_state,classified_by" `
    $tableRows
WriteDeterministic ($base + ".columns.csv") `
    "schema,object,ordinal,column,type,nullability,default_expression" `
    $columnRows
WriteDeterministic ($base + ".relations.csv") `
    "schema,object,constraint,referenced_schema,referenced_object,definition" `
    $relationRows

$dotSorted = SortOrdinal $dotEdges
$dot = "digraph physical_relations {" + "`n" + '  rankdir=LR;' + "`n" + ($dotSorted -join "`n") + "`n" + "}" + "`n"
[System.IO.File]::WriteAllText(($base + ".relation-graph.dot"), $dot, $UTF8NoBom)

# ---- summary and verdict ----------------------------------------------------
$deadRules = @()
foreach ($r in $rules) { if ($r.Hits -eq 0) { $deadRules += ($r.Schema + "  " + $r.Pattern) } }

$schemaCounts = @{}
foreach ($o in $objects) {
    if (-not $schemaCounts.ContainsKey($o[0])) { $schemaCounts[$o[0]] = 0 }
    $schemaCounts[$o[0]] = $schemaCounts[$o[0]] + 1
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Physical catalogue - " + $Database)
[void]$sb.AppendLine("")
[void]$sb.AppendLine("Objects: " + $objects.Count + "   Columns: " + $columns.Count + "   FK relations: " + $relationRows.Count)
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## Objects by schema")
[void]$sb.AppendLine("")
foreach ($s in (SortOrdinal ([string[]]@($schemaCounts.Keys)))) {
    $marker = ""
    if ($GOVERNED -notcontains $s -and $s -ne "public") { $marker = "   <- schema outside the governed three and outside public" }
    [void]$sb.AppendLine("- " + $s + ": " + $schemaCounts[$s] + $marker)
}
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## Verdict")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("- UNCLASSIFIED objects: " + $unclassified.Count)
[void]$sb.AppendLine("- public-schema objects not on the platform allowlist: " + $outOfPlace.Count)
[void]$sb.AppendLine("- naming-nonconforming objects: " + $namingBreach.Count)
[void]$sb.AppendLine("- rules that matched nothing: " + $deadRules.Count)
[void]$sb.AppendLine("")
if ($unclassified.Count -gt 0) {
    [void]$sb.AppendLine("## UNCLASSIFIED - the adjudication queue")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("Each line needs one row in physical-object-classification.tsv. Read the")
    [void]$sb.AppendLine("creator_authority column of the tables CSV before classifying: the script that")
    [void]$sb.AppendLine("created an object is the best available statement of why it exists.")
    [void]$sb.AppendLine("")
    foreach ($u in (SortOrdinal ([string[]]$unclassified))) { [void]$sb.AppendLine("- " + $u) }
    [void]$sb.AppendLine("")
}
if ($outOfPlace.Count -gt 0) {
    [void]$sb.AppendLine("## OUT_OF_PLACE - product objects sitting in the public schema")
    [void]$sb.AppendLine("")
    foreach ($u in (SortOrdinal ([string[]]$outOfPlace))) { [void]$sb.AppendLine("- " + $u) }
    [void]$sb.AppendLine("")
}
if ($namingBreach.Count -gt 0) {
    [void]$sb.AppendLine("## Naming nonconformance (grandfathered - reported, never renamed here)")
    [void]$sb.AppendLine("")
    foreach ($u in (SortOrdinal ([string[]]$namingBreach))) { [void]$sb.AppendLine("- " + $u) }
    [void]$sb.AppendLine("")
}
if ($deadRules.Count -gt 0) {
    [void]$sb.AppendLine("## Rules that matched nothing")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("A rule that protects nothing is a rule somebody believes is protecting something.")
    [void]$sb.AppendLine("")
    foreach ($u in (SortOrdinal ([string[]]$deadRules))) { [void]$sb.AppendLine("- " + $u) }
    [void]$sb.AppendLine("")
}
[System.IO.File]::WriteAllText(($base + ".summary.md"), (($sb.ToString()) -replace "`r`n", "`n"), $UTF8NoBom)

Chat ""
Chat ("[INFO]   objects " + $objects.Count + "   columns " + $columns.Count + "   fk " + $relationRows.Count)
Chat ("[INFO]   UNCLASSIFIED " + $unclassified.Count + "   OUT_OF_PLACE " + $outOfPlace.Count + "   naming " + $namingBreach.Count)

$red = ($unclassified.Count -gt 0) -or ($outOfPlace.Count -gt 0)
if ($red) {
    Chat "[RED]    catalogue is not zero-unknown"
    exit 3
}
Chat "[GREEN]  catalogue is zero-unknown"
exit 0