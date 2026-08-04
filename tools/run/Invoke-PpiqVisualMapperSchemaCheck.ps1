#requires -Version 5.1
<#
================================================================================
 PPIQ - VISUAL MAPPER SESSION SCHEMA CHECK  (READ-ONLY, v2)
================================================================================

 WHY v2. Two defects in v1, both mine.

   1. I INVENTED a connection string, postgres/postgres, instead of reading the
      one the product uses. env/profiles/*.env line 18 carries it:
      Username=ppiq_dev, Password=ppiq_dev_local_only. v2 READS that file
      rather than guessing, so it cannot drift from what the API connects with.

   2. I passed SQL with -c and a here-string. Windows argument parsing split
      it, psql reported every fragment as an extra positional argument, and
      nothing ran. v2 writes the SQL to a temp file and uses -f, which has no
      quoting surface at all.

 v2 also checks BOTH databases - ppiq_app and ppiq_presentation - because I do
 not know which profile your API is running under, and guessing that would be
 the same class of error as guessing the password.

 WHAT IT IS TESTING. VisualMapperEndpoints.cs writes session_name (line 69) and
 draft_definition (lines 79 and 152). The only DDL for that table,
 540_v5_p05_visual_mapper_foundation.sql, declares neither, and a
 repository-wide search finds both names in the C# file and nowhere else.

 READ-ONLY. information_schema and one count. Creates nothing, alters nothing.
================================================================================
#>

[CmdletBinding()]
param(
  [string[]]$Databases = @("ppiq_presentation", "ppiq_app"),
  [string]$EnvProfile  = "env\profiles\presentation.env"
)

$ErrorActionPreference = "Stop"
$script:Repo = (Get-Location).Path

function Say([string]$m) { Write-Host $m }
function Ok ([string]$m) { Write-Host ("  [ OK ] " + $m) }
function Bad([string]$m) { Write-Host ("  [FAIL] " + $m) -ForegroundColor Red }
function Info([string]$m){ Write-Host ("  [ .. ] " + $m) -ForegroundColor DarkGray }

Say "=============================================================================="
Say " PPIQ - VISUAL MAPPER SESSION SCHEMA CHECK (read-only, v2)"
Say "=============================================================================="

$psql = $env:PSQL_PATH
if (-not $psql) { $psql = "psql" }
if (-not (Get-Command $psql -ErrorAction SilentlyContinue)) {
  Bad "psql not found. Set PSQL_PATH or add psql to PATH."
  exit 1
}
Ok "psql found"

# ---- credentials, READ from the product's own profile, never invented -------
$profilePath = Join-Path $script:Repo $EnvProfile
if (-not (Test-Path $profilePath)) {
  Bad ("env profile not found: " + $EnvProfile)
  exit 1
}
$line = Select-String -Path $profilePath -Pattern "^ConnectionStrings__PlantProcessDb=" | Select-Object -First 1
if ($null -eq $line) {
  Bad ("no ConnectionStrings__PlantProcessDb line in " + $EnvProfile)
  exit 1
}
$conn = $line.Line.Substring($line.Line.IndexOf("=") + 1)

$pgHost = "localhost"; $pgPort = "5432"; $pgUser = ""; $pgPass = ""
foreach ($part in $conn.Split(";")) {
  $kv = $part.Split("=", 2)
  if ($kv.Count -ne 2) { continue }
  switch ($kv[0].Trim().ToLower()) {
    "host"     { $pgHost = $kv[1].Trim() }
    "port"     { $pgPort = $kv[1].Trim() }
    "username" { $pgUser = $kv[1].Trim() }
    "password" { $pgPass = $kv[1].Trim() }
  }
}
Ok ("credentials read from " + $EnvProfile + " - host " + $pgHost + ":" + $pgPort + ", user " + $pgUser)

$env:PGHOST     = $pgHost
$env:PGPORT     = $pgPort
$env:PGUSER     = $pgUser
$env:PGPASSWORD = $pgPass

# ---- the SQL, written to a file so nothing has to survive argument parsing --
$sqlPath = Join-Path $env:TEMP "ppiq-vm-schema-check.sql"
$sql = @'
\echo
\echo --- 1. does the table exist
SELECT current_database() AS db, to_regclass('public.ppiq_visual_mapper_sessions') AS sessions_table;

\echo
\echo --- 2. every column the table actually has
SELECT ordinal_position AS pos, column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema='public' AND table_name='ppiq_visual_mapper_sessions'
ORDER BY ordinal_position;

\echo
\echo --- 3. the columns the endpoint writes, present or not
WITH needed(col, used_by) AS (
  VALUES ('session_name',     'INSERT in POST /sessions, line 69'),
         ('draft_definition', 'UPDATE line 79 and SELECT line 152'),
         ('tenant_id',        'INSERT in POST /sessions, line 69'),
         ('status',           'INSERT in POST /sessions, line 69')
)
SELECT n.col,
       CASE WHEN c.column_name IS NULL THEN 'MISSING' ELSE 'present' END AS state,
       n.used_by
FROM needed n
LEFT JOIN information_schema.columns c
  ON c.table_schema='public'
 AND c.table_name='ppiq_visual_mapper_sessions'
 AND c.column_name = n.col
ORDER BY n.col;

\echo
\echo --- 4. NOT NULL, no default, and NOT supplied by the INSERT
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema='public'
  AND table_name='ppiq_visual_mapper_sessions'
  AND is_nullable='NO'
  AND column_default IS NULL
  AND column_name NOT IN ('tenant_id','session_name','status')
ORDER BY ordinal_position;

\echo
\echo --- 5. how many sessions have ever been created
SELECT count(*) AS sessions_ever_created FROM public.ppiq_visual_mapper_sessions;
'@
$enc = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($sqlPath, ($sql -replace "`r`n", "`n"), $enc)
Ok ("sql written to " + $sqlPath)

foreach ($db in $Databases) {
  Say ""
  Say "=============================================================================="
  Say ("DATABASE: " + $db)
  Say "=============================================================================="
  $env:PGDATABASE = $db
  & $psql -X -q -f $sqlPath
  if ($LASTEXITCODE -ne 0) { Bad ("psql exited " + $LASTEXITCODE + " for " + $db) }
}

$env:PGPASSWORD = ""

Say ""
Say "=============================================================================="
Say "READING THE RESULT"
Say "=============================================================================="
Say "  Section 3 decides it."
Say ""
Say "  session_name MISSING and draft_definition MISSING"
Say "    -> the endpoint contradicts its own schema and has never been able to"
Say "       create a session. Preview and Publish have never worked."
Say ""
Say "  both present"
Say "    -> my reading is WRONG. The 500 has another cause, and the API console"
Say "       carries the PostgreSQL error text, which is the next thing to read."
Say ""
Say "  Section 4 matters either way: source_code and display_name are NOT NULL"
Say "  with no default in the committed DDL, and the INSERT supplies neither."
