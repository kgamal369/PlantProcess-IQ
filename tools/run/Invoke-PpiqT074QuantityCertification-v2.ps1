<#
    PPIQ runner: Invoke-PpiqT074QuantityCertification

    T-074 live validation, both levels.

    MANDATORY: the natural casting-speed question, exactly as the frozen task
    requires. On THIS registry the expected pass is an HONEST REFUSAL, because
    the only definitions naming that quantity are synthetic and they disagree
    about the range. The runner proves the answer never presents a date, a mass,
    an arbitrary number or either synthetic range as authoritative.

    OPTIONAL: a second question built mechanically from an APPROVED row's own
    parameter name, to exercise the guard against a real definition. It is
    reported, never substituted for the mandatory check, and if no approved row
    is usable the runner says so rather than manufacturing one.

    READ-ONLY. It asks questions and reads the registry. It writes nothing to the
    database and takes no -Apply.
#>

[CmdletBinding()]
param(
    [string]$Question = "what is the casting speed",
    [string]$ApiBase = "http://localhost:5063",
    [string]$EvidenceDir = "docs\m1\evidence"
)

$ErrorActionPreference = "Continue"
$Stamp = Get-Date -Format "yyyyMMdd_HHmmss"

$Script:Lines = New-Object System.Collections.ArrayList
$Script:Fail = 0
$Script:Pass = 0

function W([string]$Text) { Write-Host $Text; [void]$Script:Lines.Add($Text) }
function Head([string]$Text) { W ""; W ("-" * 78); W $Text; W ("-" * 78) }
function Ok([string]$Text)   { $Script:Pass = $Script:Pass + 1; W ("PASS  " + $Text) }
function Bad([string]$Text)  { $Script:Fail = $Script:Fail + 1; W ("FAIL  " + $Text) }
function Note([string]$Text) { W ("      " + $Text) }

Head "PPIQ T-074 QUANTITY CERTIFICATION"
W ("Started  : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
W ("API      : " + $ApiBase)
W ("Question : " + $Question)

if (-not (Test-Path "Jenkinsfile")) { W "FAIL  run this from the repository root"; exit 1 }

$EnvFile = "env\profiles\presentation.env"
$UserName = $null; $Password = $null
$PgUser = "ppiq_dev"; $PgPass = "ppiq_dev_local_only"; $PgDb = "ppiq_presentation"
if (Test-Path $EnvFile) {
    foreach ($line in (Get-Content $EnvFile)) {
        if ($line -match '^\s*#') { continue }
        if ($line -match 'PPIQ_SMOKE_USERNAME\s*=\s*(.+)$') { $UserName = $matches[1].Trim() }
        if ($line -match 'PPIQ_SMOKE_PASSWORD\s*=\s*(.+)$') { $Password = $matches[1].Trim() }
        if ($line -match 'POSTGRES_USER\s*=\s*(.+)$')       { $PgUser = $matches[1].Trim() }
        if ($line -match 'POSTGRES_PASSWORD\s*=\s*(.+)$')   { $PgPass = $matches[1].Trim() }
        if ($line -match 'POSTGRES_DB\s*=\s*(.+)$')         { $PgDb = $matches[1].Trim() }
    }
}
$env:PGPASSWORD = $PgPass
if ($null -eq (Get-Command psql -ErrorAction SilentlyContinue)) { W "FAIL  psql is not on PATH"; exit 1 }

function Sql([string]$Query) {
    $out = & psql -h 127.0.0.1 -p 5432 -U $PgUser -d $PgDb -w -X -A -t -v ON_ERROR_STOP=1 -c $Query 2>&1
    if ($LASTEXITCODE -ne 0) { return @() }
    return @($out | Where-Object { $_ -and ($_.ToString().Trim() -ne '') })
}

$NormalisedQuestion = (($Question.ToLowerInvariant() -replace '[._-]', ' ') -replace '\s+', ' ').Trim()

Head "1. WHAT THE REGISTRY SAYS ABOUT THIS QUESTION"

$matched = Sql @"
WITH n AS (
  SELECT parameter_code, parameter_name, unit_of_measure, expected_min_value, expected_max_value, is_synthetic,
         regexp_replace(lower(translate(parameter_code, '._-', '   ')), '\s+', ' ', 'g') AS nc,
         regexp_replace(lower(translate(parameter_name, '._-', '   ')), '\s+', ' ', 'g') AS nn
  FROM public.parameter_definitions WHERE is_deleted = false
)
SELECT parameter_code, coalesce(unit_of_measure,'(none)'),
       coalesce(expected_min_value::text,'(none)'), coalesce(expected_max_value::text,'(none)'),
       is_synthetic
FROM n
WHERE position(btrim(nc) in '$NormalisedQuestion') > 0 OR position(btrim(nn) in '$NormalisedQuestion') > 0
ORDER BY is_synthetic, parameter_code;
"@

if ($matched.Count -eq 0) {
    Note "no registry vocabulary matches this question at all (NoMatch)"
} else {
    foreach ($row in $matched) {
        $bits = $row.ToString().Split('|')
        Note ("matched: " + $bits[0] + "  unit=" + $bits[1] + "  min=" + $bits[2] + "  max=" + $bits[3] + "  synthetic=" + $bits[4])
    }
}

$approvedMatches = @($matched | Where-Object { $_.ToString().Split('|')[4] -eq 'f' })
Note ("approved (non-synthetic) definitions matching: " + $approvedMatches.Count)

if ($matched.Count -gt 0 -and $approvedMatches.Count -eq 0) {
    Ok "the registry knows this vocabulary and can vouch for NO definition (KnownButUntrustedOrAmbiguous)"
    Note "so the only correct live outcomes are an honest refusal or a blocked numeric answer"
} elseif ($approvedMatches.Count -eq 1) {
    Note "one approved definition matches; the typed guard is armed for it"
} elseif ($approvedMatches.Count -gt 1) {
    Note "more than one approved definition matches; ambiguous, and the guard must not guess"
}

Head "SESSION"
$token = $null
try {
    $login = Invoke-RestMethod -Method Post -Uri ($ApiBase + "/auth/login") -ContentType "application/json" `
        -Body (@{ userName = $UserName; password = $Password } | ConvertTo-Json)
    $token = $login.accessToken
} catch { W ("FAIL  login failed: " + $_.Exception.Message); exit 1 }
if ([string]::IsNullOrWhiteSpace($token)) { W "FAIL  login returned no access token"; exit 1 }
Ok "authenticated against the running API"
$Headers = @{ Authorization = ("Bearer " + $token) }

function Ask([string]$Text) {
    $body = @{ question = $Text; contextChips = @("grounded", "approved findings"); tools = @(); context = $null } | ConvertTo-Json -Depth 5
    return Invoke-RestMethod -Method Post -Uri ($ApiBase + "/api/assistant/ask") -Headers $Headers -ContentType "application/json" -Body $body
}

Head "2. MANDATORY - THE CASTING-SPEED QUESTION"

$answer = $null
try { $answer = Ask $Question } catch { }
if ($null -eq $answer) { Bad "the ask failed"; }
else {
    W ("      isRefusal : " + $answer.isRefusal)
    if ($answer.refusalReason) { W ("      reason    : " + $answer.refusalReason) }
    if ($answer.text)          { W ("      text      : " + $answer.text.Substring(0, [Math]::Min(300, $answer.text.Length))) }
    if ($answer.blockedSentences -and $answer.blockedSentences.Count -gt 0) {
        W ("      blocked   : " + $answer.blockedSentences.Count + " sentence(s)")
        foreach ($b in ($answer.blockedSentences | Select-Object -First 4)) {
            W ("                  " + $b.Substring(0, [Math]::Min(120, $b.Length)))
        }
    }

    $text = if ($answer.text) { $answer.text } else { "" }

    if ($answer.isRefusal) {
        Ok "honest refusal - the expected outcome for a quantity the registry cannot vouch for"
        if ($text -match '\d') { Bad "the refusal text carries a number" } else { Ok "the refusal states no number" }
    } else {
        # A non-refusal is only acceptable if it presents no numeric material at
        # all. There is no approved definition here, so no number can be a
        # correct casting-speed answer.
        if ($text -match '-?\d[\d.,]*') {
            Bad "answered with numeric material although no approved definition exists for this quantity"
        } else {
            Ok "answered without presenting any number as the quantity"
        }
    }

    # The four things the answer must never do, checked explicitly.
    foreach ($pair in @(
        @{ Name = "a date presented as the quantity"; Pattern = '\d{4}-\d{2}-\d{2}' },
        @{ Name = "the first synthetic range presented as authoritative"; Pattern = '0\.5\D{0,6}2\.5' },
        @{ Name = "the second synthetic range presented as authoritative"; Pattern = '0\D{0,6}3\.0' })) {
        if ($text -match $pair.Pattern) { Bad ($pair.Name) } else { Ok ("not present: " + $pair.Name) }
    }

    # Any unit token from the registry other than the ones the matched rows
    # declare would be a mislabelled quantity. Derived from the registry, not
    # from a vocabulary list.
    $foreignUnits = New-Object System.Collections.ArrayList
    foreach ($row in (Sql "SELECT DISTINCT unit_of_measure FROM public.parameter_definitions WHERE is_deleted = false AND unit_of_measure IS NOT NULL;")) {
        $unit = $row.ToString().Trim()
        if ($unit -eq "") { continue }
        $escaped = [regex]::Escape($unit)
        if ($text -match ('-?\d[\d.,]*\s*' + $escaped + '(?![A-Za-z0-9])')) { [void]$foreignUnits.Add($unit) }
    }
    if ($foreignUnits.Count -eq 0) {
        Ok "no value is presented with any registry unit, so nothing is labelled as this quantity"
    } else {
        Bad ("a value is presented with registry unit(s): " + ($foreignUnits -join ", "))
    }
}

Head "3. OPTIONAL - AN APPROVED DEFINITION, CHOSEN MECHANICALLY"

# One column, joined with a separator no code or name can contain, so the split
# cannot be confused by whatever the values happen to hold. The previous version
# relied on the default field separator and silently produced a one-field line.
$approved = Sql @"
SELECT parameter_code || '~|~' || parameter_name || '~|~' || unit_of_measure || '~|~' ||
       expected_min_value::text || '~|~' || expected_max_value::text
FROM public.parameter_definitions
WHERE is_deleted = false AND is_synthetic = false
  AND unit_of_measure IS NOT NULL
  AND expected_min_value IS NOT NULL AND expected_max_value IS NOT NULL
ORDER BY parameter_code
LIMIT 1;
"@

if ($approved.Count -eq 0) {
    Note "no approved row carries both a unit and bounds; the crafted unit tests remain the positive proof"
} else {
    $raw = $approved[0].ToString()
    Note ("raw row: " + $raw)

    $bits = $raw -split '~\|~'
    if ($bits.Count -lt 5) {
        Bad ("the approved row did not parse into five fields; skipping the optional check")
        $bits = $null
    }
}

# The optional check runs only with a fully parsed row AND a non-empty unit. An
# empty unit would turn the candidate pattern into one that matches EVERY number,
# which is exactly how the previous version invented twelve failures.
if ($approved.Count -gt 0 -and $null -ne $bits -and $bits.Count -ge 5) {
    $code = $bits[0].Trim(); $name = $bits[1].Trim(); $unit = $bits[2].Trim()
    $min = $bits[3].Trim();  $max = $bits[4].Trim()
    Note ("chosen: " + $code + " (" + $name + ")  unit=" + $unit + "  min=" + $min + "  max=" + $max)

    if ([string]::IsNullOrWhiteSpace($unit) -or [string]::IsNullOrWhiteSpace($name)) {
        Bad "the chosen row has no usable unit or name; skipping rather than checking every number"
        $name = $null
    }

    if ($null -eq $name) { Note "optional check skipped"; $secondQuestion = $null } else { $secondQuestion = "what is the " + $name.ToLowerInvariant() }
    Note ("question built from the row's own name: " + $secondQuestion)

    $second = $null
    if ($null -ne $secondQuestion) { try { $second = Ask $secondQuestion } catch { } }

    if ($null -eq $secondQuestion) {
        # already reported
    } elseif ($null -eq $second) {
        Bad "the approved-definition ask failed"
    } else {
        W ("      isRefusal : " + $second.isRefusal)
        if ($second.text) { W ("      text      : " + $second.text.Substring(0, [Math]::Min(240, $second.text.Length))) }

        $secondText = if ($second.text) { $second.text } else { "" }
        $escapedUnit = [regex]::Escape($unit)
        $values = [regex]::Matches($secondText, '(-?\d[\d.,]*)\s*' + $escapedUnit + '(?![A-Za-z0-9])')

        if ($second.isRefusal) {
            Ok "honest refusal for the approved quantity - a valid outcome when evidence is absent"
        } elseif ($values.Count -eq 0) {
            Ok "answered without presenting a value in the registry unit"
        } else {
            $bad = $false
            foreach ($m in $values) {
                $raw = $m.Groups[1].Value.Replace(",", "").TrimEnd(".")
                [decimal]$v = 0
                if ([decimal]::TryParse($raw, [System.Globalization.NumberStyles]::Number, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$v)) {
                    if (($min -ne "" -and $v -lt [decimal]$min) -or ($max -ne "" -and $v -gt [decimal]$max)) {
                        Bad ("a value outside the registry range survived: " + $raw + " " + $unit)
                        $bad = $true
                    }
                }
            }
            if (-not $bad) { Ok ("every value presented in " + $unit + " lies inside the registry range") }
        }
    }
}

Head "RESULT"
W ("Checks passed : " + $Script:Pass)
W ("Checks failed : " + $Script:Fail)
W ""
if ($Script:Fail -eq 0) {
    W "T-074 live validation PASSED."
    W "No data was altered to produce a numeric answer, and none was needed:"
    W "an honest refusal for a quantity the registry cannot vouch for IS the pass."
} else {
    W ("NOT CERTIFIED. " + $Script:Fail + " check(s) failed above.")
}

if (-not (Test-Path $EvidenceDir)) { New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null }
$EvidenceFile = Join-Path $EvidenceDir ("T-074_quantity_certification_" + $Stamp + ".txt")
[System.IO.File]::WriteAllText($EvidenceFile, ($Script:Lines -join "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
W ""
W ("Evidence written: " + $EvidenceFile)
W "Stage that ONE file."

if ($Script:Fail -eq 0) { exit 0 } else { exit 1 }
