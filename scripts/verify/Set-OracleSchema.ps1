# ============================================================================
# Set-OracleSchema.ps1  v1.1   M1-19 without the broken form
# (v1.1: fixed single-element array unrolling in Q1 - it was returning the
#  first CHARACTER of each column name; added a sanity guard on discovery)
#
# WHY NOT THE UI FORM: the Edit dialog has two defects (both now backlog rows)
#   D1  PROVIDER TYPE shows "CSV Snapshot" on an Oracle profile - the dropdown
#       does not bind the stored value. Saving may rewrite provider_type and
#       destroy a connection that currently tests green.
#   D2  SECRET REFERENCE renders empty and is REQUIRED - the read endpoint
#       does not return secrets, so the form demands re-entry of a value it
#       will not show. You cannot save without re-typing it from memory.
# ...so the form is currently a trap for exactly this edit.
#
# WHAT THIS DOES INSTEAD: shows you what is really stored (including the
# secret reference the form hides), then updates ONLY the schema column -
# one field, no round-trip through a form that mangles the others - and
# proves the result through the product's own discovery endpoint.
#
# This is config, not product data: Rule 2 governs plant content arriving by
# DB-link, not a connection profile's schema field.
#
# Run from repo root (API up):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Set-OracleSchema.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Set-OracleSchema.ps1 -Execute
# ============================================================================
[CmdletBinding()]
param(
    [switch]$Execute,
    [string]$Schema = 'PPIQ_SRC',
    [string]$TargetDb = 'ppiq_presentation',
    [string]$ApiBase = 'http://localhost:5063',
    [string]$ApiUser = 'e2eadmin',
    [string]$ApiPassword = 'E2EAdmin123!'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$Out = Join-Path $RepoRoot ('OracleSchema_' + $Stamp + '.txt')
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s); Write-Host $s }
function Save { [System.IO.File]::WriteAllText($Out, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false))) }

$Psql = $null
$cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmd) { $Psql = $cmd.Source } else {
    $c = @(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
    if ($c.Count -gt 0) { $Psql = $c[0].FullName }
}
if (-not $Psql) { Write-Host "[FAIL] psql not found." -ForegroundColor Red; exit 1 }
$env:PGPASSWORD = 'ppiq_dev_local_only'
function Rows([string]$q) {
    return @(& $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -F ' | ' -c $q 2>&1 | Where-Object { $_ -and ($_.ToString().Trim() -ne '') })
}
function Q1([string]$q) {
    # NOTE: @() is mandatory - PowerShell unrolls a single-element array on
    # return, after which $r[0] yields the first CHARACTER, not the value.
    $r = @(Rows $q)
    if ($r.Count -eq 0) { return $null }
    return ([string]$r[0]).Trim()
}

W ("SET ORACLE SCHEMA - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ("DB: " + $TargetDb + "   schema to set: " + $Schema)
W ("=" * 78)
W ""

# ---- discover the real column names ----------------------------------------
$schemaCol = Q1 "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='connection_profiles' AND column_name ~* 'schema' ORDER BY length(column_name) LIMIT 1;"
$provCol = Q1 "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='connection_profiles' AND column_name ~* 'provider' ORDER BY length(column_name) LIMIT 1;"
$secretCol = Q1 "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='connection_profiles' AND column_name ~* 'secret' ORDER BY length(column_name) LIMIT 1;"
$codeCol = Q1 "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='connection_profiles' AND column_name ~* 'code' ORDER BY length(column_name) LIMIT 1;"
W ("[SCHEMA] columns discovered: code=" + $codeCol + "  provider=" + $provCol + "  schema=" + $schemaCol + "  secret=" + $secretCol)
foreach ($pair in @(@{ N = 'code'; V = $codeCol }, @{ N = 'provider'; V = $provCol }, @{ N = 'schema'; V = $schemaCol })) {
    if (-not $pair.V -or $pair.V.Length -lt 3) {
        W ("[ABORT] " + $pair.N + " column resolved to '" + $pair.V + "' - that is not a column name.")
        W "        Paste the output of:  \d connection_profiles"
        Save; exit 1
    }
}
W ""

# ---- what is REALLY stored (incl. the secret ref the form hides) -----------
W "[CURRENT] every profile as stored (this is the truth the form fails to show):"
W ("    " + $codeCol + " | " + $provCol + " | " + $schemaCol + " | " + $secretCol)
Rows ("SELECT " + $codeCol + " || ' | ' || COALESCE(" + $provCol + "::text,'-') || ' | ' || COALESCE(" + $schemaCol + ",'(empty)') || ' | ' || COALESCE(" + $secretCol + ",'(none)') FROM connection_profiles ORDER BY " + $codeCol + ";") |
    ForEach-Object { W ("    " + $_) }
W ""
W "    ^ THE SECRET REFERENCE COLUMN ABOVE is what the Edit form leaves blank."
W "      If you ever must use the form, type that exact value back into it."
W ""

$targets = Rows ("SELECT " + $codeCol + " FROM connection_profiles WHERE " + $provCol + "::text ~* 'oracle' ORDER BY 1;")
W ("[TARGETS] Oracle profiles: " + (@($targets) -join ', '))
W ""

if (-not $Execute) {
    W ("DRY-RUN. Would set " + $schemaCol + " = '" + $Schema + "' on the Oracle profiles above.")
    W "Nothing else is touched - provider, secret, host, database all stay exactly as stored."
    W ""
    W "Re-run with -Execute."
    Save; exit 0
}

# ---- the one-field update --------------------------------------------------
$sql = "UPDATE connection_profiles SET " + $schemaCol + " = '" + $Schema + "' WHERE " + $provCol + "::text ~* 'oracle';"
$tmp = Join-Path $env:TEMP ("ppiq_oraschema_" + [guid]::NewGuid().ToString('N') + ".sql")
[System.IO.File]::WriteAllText($tmp, $sql, (New-Object System.Text.UTF8Encoding($false)))
$o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -v ON_ERROR_STOP=1 -f $tmp 2>&1
$ok = ($LASTEXITCODE -eq 0)
Remove-Item $tmp -ErrorAction SilentlyContinue
if (-not $ok) {
    W "[FAIL] update:"
    @($o | Select-Object -First 4) | ForEach-Object { W ("    " + $_) }
    Save; exit 1
}
W ("[APPLIED] " + $schemaCol + " = '" + $Schema + "' on the Oracle profiles.")
W ""
W "[VERIFY] stored state now:"
Rows ("SELECT " + $codeCol + " || ' | ' || COALESCE(" + $provCol + "::text,'-') || ' | ' || COALESCE(" + $schemaCol + ",'(empty)') || ' | ' || COALESCE(" + $secretCol + ",'(none)') FROM connection_profiles WHERE " + $provCol + "::text ~* 'oracle' ORDER BY 1;") |
    ForEach-Object { W ("    " + $_) }
W "    (provider and secret unchanged - which is the whole point)"
W ""

# ---- prove it through the product ------------------------------------------
W "[PROOF] discovery through the product API (the call the Tables button makes):"
$token = $null
foreach ($u in @('/api/auth/login', '/auth/login')) {
    foreach ($body in @((@{ username = $ApiUser; password = $ApiPassword } | ConvertTo-Json), (@{ email = $ApiUser; password = $ApiPassword } | ConvertTo-Json))) {
        if ($token) { break }
        try {
            $r = Invoke-RestMethod -Uri ($ApiBase + $u) -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 10 -ErrorAction Stop
            if ($r.PSObject.Properties['accessToken']) { $token = $r.accessToken }
            elseif ($r.PSObject.Properties['token']) { $token = $r.token }
        } catch { }
    }
}
if (-not $token) {
    W "    could not authenticate - start the API, then run:"
    W "    powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify-OracleDiscovery.ps1"
    Save; exit 0
}
$H = @{ Authorization = 'Bearer ' + $token }
$idCol = Q1 "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='connection_profiles' AND column_name='id';"
$pairs = @(Rows ("SELECT id::text || '|' || " + $codeCol + " FROM connection_profiles WHERE " + $provCol + "::text ~* 'oracle';"))
$allOk = $true
foreach ($p in $pairs) {
    $parts = $p.ToString().Split('|')
    $id = $parts[0].Trim(); $code = $parts[1].Trim()
    try {
        $d = Invoke-RestMethod -Uri ($ApiBase + '/admin/connectors/connection-profiles/' + $id + '/tables') -Headers $H -TimeoutSec 45 -ErrorAction Stop
        $tl = $d
        foreach ($k in @('tables', 'items', 'data')) { if ($d.PSObject.Properties[$k]) { $tl = $d.$k; break } }
        W ("    " + $code + ": PASS - " + @($tl).Count + " object(s)")
        @($tl | Select-Object -First 6) | ForEach-Object {
            $n = $_
            foreach ($k in @('name', 'tableName', 'objectName')) { if ($_.PSObject.Properties[$k]) { $n = $_.$k; break } }
            W ("        " + $n)
        }
    } catch {
        $allOk = $false
        W ("    " + $code + ": FAIL - " + $_.Exception.Message)
    }
}
W ""
if ($allOk) {
    W "M1-19 EARNED. 'Live Oracle connector' is now a true sentence."
    W "REMAINING: click Tables on both Oracle rows in the HMI and screenshot it."
    W "           (the acceptance says 'in the UI' - the API proof is not enough)"
} else {
    W "Discovery still failing - paste the error and we go deeper."
}
W ""
W "NEW BACKLOG ROWS FROM THIS SESSION (M1, demo-path, small):"
W "  D1 connection-profile form: provider dropdown does not bind stored value"
W "     (shows 'CSV Snapshot' on an Oracle profile - saving may corrupt it)"
W "  D2 connection-profile form: secret reference blank but required - the"
W "     form cannot round-trip a value the read endpoint withholds"
Save
Write-Host ""
Write-Host ("[DONE] -> " + $Out) -ForegroundColor Green
exit 0
