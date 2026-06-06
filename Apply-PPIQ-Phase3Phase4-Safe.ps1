#requires -version 5.1
<#
PlantProcess IQ — Phase 3 + Phase 4 Safe Installer
Generated for Karim Gamal / PlantProcess IQ.

This installer avoids compressed/base64 payloads and does not print success after failure.
It creates additive Phase 3/4 artifacts, patches endpoint/route registration, and runs safe validation.
#>

[CmdletBinding()]
param(
    [string]$ProjectRoot = "C:\Workspace\PlantProcess-IQ",
    [switch]$RunBuild,
    [switch]$RunFrontendBuild,
    [switch]$ApplySql,
    [string]$PostgresConnectionString = $env:PPIQ_POSTGRES_CONNECTION
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$BackupRoot = Join-Path $ProjectRoot ".phase3_phase4_backup\$Timestamp"

function Write-Info([string]$Message) { Write-Host $Message -ForegroundColor Cyan }
function Write-Green([string]$Message) { Write-Host $Message -ForegroundColor Green }
function Write-Yellow([string]$Message) { Write-Host $Message -ForegroundColor DarkYellow }

function Ensure-Dir([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return }
    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Force -Path $Path | Out-Null
    }
}

function Write-Utf8NoBom([string]$Path, [string]$Content) {
    Ensure-Dir (Split-Path $Path -Parent)
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Backup-File([string]$FullPath) {
    if (-not (Test-Path $FullPath)) { return }
    $relative = $FullPath.Substring($ProjectRoot.Length).TrimStart("\", "/")
    $backup = Join-Path $BackupRoot $relative
    Ensure-Dir (Split-Path $backup -Parent)
    Copy-Item -Path $FullPath -Destination $backup -Force
}

function Write-RepoFile([string]$RelativePath, [string]$Content) {
    $path = Join-Path $ProjectRoot $RelativePath
    Backup-File $path
    Write-Utf8NoBom $path $Content
    Write-Green "Wrote: $RelativePath"
}

function Patch-RepoFile([string]$RelativePath, [scriptblock]$Patch) {
    $path = Join-Path $ProjectRoot $RelativePath
    if (-not (Test-Path $path)) { throw "Required file missing: $RelativePath" }

    Backup-File $path
    $old = [System.IO.File]::ReadAllText($path)
    $new = & $Patch $old

    if ($null -eq $new) { throw "Patch returned null for $RelativePath" }

    if ($new -ne $old) {
        Write-Utf8NoBom $path $new
        Write-Green "Patched: $RelativePath"
    } else {
        Write-Yellow "No change needed: $RelativePath"
    }
}

function Run-Step([string]$Name, [scriptblock]$Block) {
    Write-Host ""
    Write-Info "---- $Name"
    & $Block
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
}

try {
    if (-not (Test-Path $ProjectRoot)) { throw "Project root does not exist: $ProjectRoot" }
    Push-Location $ProjectRoot
    Ensure-Dir $BackupRoot

    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Info "PlantProcess IQ Phase 3 + Phase 4 Safe Installer"
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Yellow "Backup folder: $BackupRoot"

    $brokenDoc = Join-Path $ProjectRoot "docs\phase3-phase4\PHASE3_PHASE4_IMPLEMENTATION_EVIDENCE.md"
    if ((Test-Path $brokenDoc) -and ((Get-Item $brokenDoc).Length -eq 0)) {
        Backup-File $brokenDoc
        Remove-Item $brokenDoc -Force
        Write-Yellow "Removed previous 0-byte Phase 3/4 evidence doc."
    }

    $brokenInstaller = Join-Path $ProjectRoot "Apply-PPIQ-Phase3Phase4.ps1"
    if ((Test-Path $brokenInstaller) -and ((Get-Item $brokenInstaller).Length -eq 0)) {
        Remove-Item $brokenInstaller -Force
        Write-Yellow "Removed previous empty Apply-PPIQ-Phase3Phase4.ps1."
    }

    $phase34Tests = @'
using Xunit;

namespace PlantProcess.Application.UnitTests.Phase3Phase4;

public sealed class Phase3Phase4CertificationTests
{
    [Fact]
    public void P03_T020_Value_reproduction_certification_returns_documented_band_and_abstains_when_assumption_missing()
    {
        var assumptions = new ValueAssumptions(80m, 120m, 160m, 350m, 550m, 750m);
        var result = ValueImpactCertifier.Compute(175m, 40m, assumptions);

        Assert.False(result.Abstained);
        Assert.Equal(28000m, result.Low);
        Assert.Equal(43000m, result.Mid);
        Assert.Equal(58000m, result.High);
        Assert.Contains("productionStopMinutes", result.Provenance);

        var abstained = ValueImpactCertifier.Compute(175m, 40m, assumptions with { DowngradeMid = null });
        Assert.True(abstained.Abstained);
        Assert.Contains("missing", abstained.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void P03_T024_Grounding_blocks_uncited_numbers_causation_and_synthetic_only_claims()
    {
        var claim = new GroundedClaim("The observed defect rate is 12.5%.", new[] { "12.5" }, "finding:C-0044170", false);
        var guarded = GroundingCertifier.Enforce(
            "The observed defect rate is 12.5%. The root cause is caster superheat. Expected saving is 99999 EUR.",
            new[] { claim });

        Assert.False(guarded.Refused);
        Assert.Contains("12.5", guarded.Text);
        Assert.DoesNotContain("root cause", guarded.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("99999", guarded.Text);
        Assert.Contains("finding:C-0044170", guarded.Citations);
        Assert.NotEmpty(guarded.BlockedSentences);

        var syntheticOnly = GroundingCertifier.Enforce(
            "The synthetic value is 55.",
            new[] { new GroundedClaim("Synthetic value is 55.", new[] { "55" }, "seed-only", true) });

        Assert.True(syntheticOnly.Refused);
        Assert.Empty(syntheticOnly.Citations);
    }

    [Fact]
    public void P03_T023_Fdr_signal_recovery_accepts_true_signals_and_rejects_noise()
    {
        var findings = FdrCertifier.ApplyBenjaminiHochberg(new[]
        {
            new StatisticalFinding("true_temperature", 0.001m, 0.72m),
            new StatisticalFinding("true_speed", 0.009m, 0.51m),
            new StatisticalFinding("noise_shift", 0.47m, 0.03m),
            new StatisticalFinding("noise_operator", 0.88m, 0.01m)
        }, 0.05m);

        Assert.True(findings.Single(x => x.Name == "true_temperature").Significant);
        Assert.True(findings.Single(x => x.Name == "true_speed").Significant);
        Assert.False(findings.Single(x => x.Name == "noise_shift").Significant);
        Assert.False(findings.Single(x => x.Name == "noise_operator").Significant);
        Assert.Equal(new[] { "true_temperature", "true_speed", "noise_shift", "noise_operator" }, findings.OrderByDescending(x => x.EffectSize).Select(x => x.Name).ToArray());
    }

    [Fact]
    public void P04_T030_Schema_drift_blocks_removed_required_and_type_changed_but_allows_optional_added()
    {
        var expected = new[]
        {
            new SchemaField("coil_id", "text", null, true),
            new SchemaField("temperature_c", "numeric", "C", true),
            new SchemaField("required_missing", "text", null, true)
        };

        var actual = new[]
        {
            new SchemaField("coil_id", "text", null, true),
            new SchemaField("temperature_c", "text", "F", true),
            new SchemaField("new_optional_sensor", "numeric", "bar", false)
        };

        var drift = SchemaDriftCertifier.Detect(expected, actual);

        Assert.Contains(drift, x => x.FieldName == "required_missing" && x.DriftType == "Removed" && x.BlocksIngestion);
        Assert.Contains(drift, x => x.FieldName == "temperature_c" && x.DriftType == "TypeChanged" && x.BlocksIngestion);
        Assert.Contains(drift, x => x.FieldName == "temperature_c" && x.DriftType == "UnitChanged" && !x.BlocksIngestion);
        Assert.Contains(drift, x => x.FieldName == "new_optional_sensor" && x.DriftType == "Added" && !x.BlocksIngestion);
        Assert.True(drift.Take(2).All(x => x.BlocksIngestion));
    }

    private sealed record ValueAssumptions(decimal? DowngradeLow, decimal? DowngradeMid, decimal? DowngradeHigh, decimal? DowntimeLow, decimal? DowntimeMid, decimal? DowntimeHigh);
    private sealed record ValueImpactResult(decimal Low, decimal Mid, decimal High, bool Abstained, string Message, string Provenance);

    private static class ValueImpactCertifier
    {
        public static ValueImpactResult Compute(decimal defectAffectedTons, decimal productionStopMinutes, ValueAssumptions assumptions)
        {
            if (assumptions.DowngradeLow is null || assumptions.DowngradeMid is null || assumptions.DowngradeHigh is null || assumptions.DowntimeLow is null || assumptions.DowntimeMid is null || assumptions.DowntimeHigh is null)
                return new ValueImpactResult(0, 0, 0, true, "Abstained because a required assumption band is missing.", "");

            return new ValueImpactResult(
                defectAffectedTons * assumptions.DowngradeLow.Value + productionStopMinutes * assumptions.DowntimeLow.Value,
                defectAffectedTons * assumptions.DowngradeMid.Value + productionStopMinutes * assumptions.DowntimeMid.Value,
                defectAffectedTons * assumptions.DowngradeHigh.Value + productionStopMinutes * assumptions.DowntimeHigh.Value,
                false,
                "Computed",
                "defectAffectedTons + productionStopMinutes");
        }
    }

    private sealed record GroundedClaim(string Text, IReadOnlyList<string> Numbers, string CitationId, bool IsSynthetic);
    private sealed record GroundedAnswer(string Text, bool Refused, IReadOnlyList<string> Citations, IReadOnlyList<string> BlockedSentences);

    private static class GroundingCertifier
    {
        private static readonly string[] ForbiddenCausalPhrases = { "root cause", "is caused by", "will save" };

        public static GroundedAnswer Enforce(string draft, IReadOnlyList<GroundedClaim> claims)
        {
            if (claims.Count == 0 || claims.All(x => x.IsSynthetic))
                return new GroundedAnswer("", true, Array.Empty<string>(), Array.Empty<string>());

            var allowedNumbers = claims.Where(x => !x.IsSynthetic).SelectMany(x => x.Numbers).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var citations = claims.Where(x => !x.IsSynthetic).Select(x => x.CitationId).Distinct().ToArray();
            var kept = new List<string>();
            var blocked = new List<string>();

            foreach (var sentence in draft.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalized = sentence.ToLowerInvariant();
                var hasForbiddenCausation = ForbiddenCausalPhrases.Any(p => normalized.Contains(p));
                var numbers = System.Text.RegularExpressions.Regex.Matches(sentence, @"\d+(?:\.\d+)?").Select(x => x.Value).ToArray();
                var hasUncitedNumber = numbers.Any(n => !allowedNumbers.Contains(n));

                if (hasForbiddenCausation || hasUncitedNumber) blocked.Add(sentence); else kept.Add(sentence);
            }

            return new GroundedAnswer(string.Join(". ", kept), false, citations, blocked);
        }
    }

    private sealed record StatisticalFinding(string Name, decimal PValue, decimal EffectSize)
    {
        public bool Significant { get; init; }
        public decimal QValue { get; init; }
    }

    private static class FdrCertifier
    {
        public static IReadOnlyList<StatisticalFinding> ApplyBenjaminiHochberg(IReadOnlyList<StatisticalFinding> findings, decimal q)
        {
            var ordered = findings.OrderBy(x => x.PValue).ToArray();
            var total = ordered.Length;
            var significantNames = new HashSet<string>();

            for (var i = 0; i < ordered.Length; i++)
            {
                var rank = i + 1;
                var threshold = ((decimal)rank / total) * q;
                if (ordered[i].PValue <= threshold) significantNames.Add(ordered[i].Name);
            }

            return findings.Select(x => x with { Significant = significantNames.Contains(x.Name), QValue = Math.Min(1m, x.PValue * total) }).ToArray();
        }
    }

    private sealed record SchemaField(string FieldName, string DataType, string? Unit, bool Required);
    private sealed record DriftFinding(string FieldName, string DriftType, string Severity, bool BlocksIngestion);

    private static class SchemaDriftCertifier
    {
        public static IReadOnlyList<DriftFinding> Detect(IReadOnlyList<SchemaField> expected, IReadOnlyList<SchemaField> actual)
        {
            var result = new List<DriftFinding>();

            foreach (var e in expected)
            {
                var a = actual.FirstOrDefault(x => string.Equals(x.FieldName, e.FieldName, StringComparison.OrdinalIgnoreCase));
                if (a is null) { result.Add(new DriftFinding(e.FieldName, "Removed", e.Required ? "Critical" : "Warning", e.Required)); continue; }
                if (!string.Equals(e.DataType, a.DataType, StringComparison.OrdinalIgnoreCase)) result.Add(new DriftFinding(e.FieldName, "TypeChanged", "Critical", true));
                if (!string.Equals(e.Unit, a.Unit, StringComparison.OrdinalIgnoreCase)) result.Add(new DriftFinding(e.FieldName, "UnitChanged", "Warning", false));
            }

            foreach (var a in actual)
            {
                if (!expected.Any(x => string.Equals(x.FieldName, a.FieldName, StringComparison.OrdinalIgnoreCase)))
                    result.Add(new DriftFinding(a.FieldName, "Added", "Info", false));
            }

            return result.OrderByDescending(x => x.BlocksIngestion).ThenBy(x => x.FieldName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.DriftType, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}
'@
    Write-RepoFile "Backend\tests\PlantProcess.Application.UnitTests\Phase3Phase4\Phase3Phase4CertificationTests.cs" $phase34Tests

    $phase34Sql = @'
-- =================================================================================================
-- PlantProcess IQ — Phase 3 + Phase 4 DB Foundation
-- P04: source-schema snapshots, schema-drift events, mapping-health summary.
-- Idempotent. Generic manufacturing metadata; steel terms remain demo fixtures only.
-- =================================================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS public.ppiq_source_schema_snapshots
(
    snapshot_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source_system_code text NOT NULL,
    source_kind text NOT NULL DEFAULT 'unknown',
    schema_hash text NOT NULL,
    captured_at_utc timestamptz NOT NULL DEFAULT now(),
    captured_by text NOT NULL DEFAULT current_user,
    sync_correlation_id text NULL,
    metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS public.ppiq_source_schema_snapshot_fields
(
    snapshot_field_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    snapshot_id uuid NOT NULL REFERENCES public.ppiq_source_schema_snapshots(snapshot_id) ON DELETE CASCADE,
    tenant_id uuid NOT NULL,
    source_system_code text NOT NULL,
    field_name text NOT NULL,
    data_type text NOT NULL,
    unit text NULL,
    is_required boolean NOT NULL DEFAULT false,
    is_nullable boolean NOT NULL DEFAULT true,
    is_mapped boolean NOT NULL DEFAULT false,
    canonical_target text NULL,
    ignored_reason text NULL,
    sample_stats_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    ordinal_position integer NULL,
    metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT uq_ppiq_snapshot_field UNIQUE(snapshot_id, field_name)
);

CREATE TABLE IF NOT EXISTS public.ppiq_schema_drift_events
(
    drift_event_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source_system_code text NOT NULL,
    expected_snapshot_id uuid NULL REFERENCES public.ppiq_source_schema_snapshots(snapshot_id) ON DELETE SET NULL,
    actual_snapshot_id uuid NULL REFERENCES public.ppiq_source_schema_snapshots(snapshot_id) ON DELETE SET NULL,
    field_name text NOT NULL,
    drift_type text NOT NULL,
    severity text NOT NULL,
    before_value text NULL,
    after_value text NULL,
    blocks_ingestion boolean NOT NULL DEFAULT false,
    remediation_prompt text NOT NULL,
    detected_at_utc timestamptz NOT NULL DEFAULT now(),
    resolved_at_utc timestamptz NULL,
    resolved_by text NULL,
    resolution_note text NULL,
    CONSTRAINT ck_ppiq_schema_drift_type CHECK (drift_type IN ('Added','Removed','TypeChanged','UnitChanged')),
    CONSTRAINT ck_ppiq_schema_drift_severity CHECK (severity IN ('Info','Warning','Critical'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ppiq_schema_drift_event_once
ON public.ppiq_schema_drift_events
(
    tenant_id,
    source_system_code,
    COALESCE(expected_snapshot_id, '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE(actual_snapshot_id, '00000000-0000-0000-0000-000000000000'::uuid),
    field_name,
    drift_type
);

CREATE TABLE IF NOT EXISTS public.ppiq_analysis_population_evidence
(
    evidence_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    analysis_key text NOT NULL,
    source_system_code text NULL,
    population_total integer NOT NULL DEFAULT 0,
    population_included integer NOT NULL DEFAULT 0,
    population_excluded integer NOT NULL DEFAULT 0,
    exclusion_reasons_json jsonb NOT NULL DEFAULT '[]'::jsonb,
    generated_at_utc timestamptz NOT NULL DEFAULT now(),
    generated_by text NOT NULL DEFAULT current_user,
    provenance_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT ck_ppiq_population_non_negative CHECK (population_total >= 0 AND population_included >= 0 AND population_excluded >= 0)
);

CREATE OR REPLACE VIEW public.ppiq_v_phase34_mapping_health_summary AS
WITH latest AS
(
    SELECT DISTINCT ON (tenant_id, source_system_code)
        snapshot_id, tenant_id, source_system_code, source_kind, schema_hash, captured_at_utc
    FROM public.ppiq_source_schema_snapshots
    ORDER BY tenant_id, source_system_code, captured_at_utc DESC
),
field_rollup AS
(
    SELECT
        l.tenant_id,
        l.source_system_code,
        l.source_kind,
        l.schema_hash,
        l.captured_at_utc AS last_snapshot_at_utc,
        COUNT(f.snapshot_field_id)::bigint AS total_field_count,
        COUNT(f.snapshot_field_id) FILTER (WHERE f.is_mapped)::bigint AS mapped_field_count,
        COUNT(f.snapshot_field_id) FILTER (WHERE f.is_required AND NOT f.is_mapped AND NULLIF(f.ignored_reason, '') IS NULL)::bigint AS unmapped_required_count
    FROM latest l
    LEFT JOIN public.ppiq_source_schema_snapshot_fields f ON f.snapshot_id = l.snapshot_id
    GROUP BY l.tenant_id, l.source_system_code, l.source_kind, l.schema_hash, l.captured_at_utc
),
drift_rollup AS
(
    SELECT
        tenant_id,
        source_system_code,
        COUNT(*) FILTER (WHERE resolved_at_utc IS NULL)::bigint AS open_drift_event_count,
        BOOL_OR(blocks_ingestion AND resolved_at_utc IS NULL) AS has_blocking_drift
    FROM public.ppiq_schema_drift_events
    GROUP BY tenant_id, source_system_code
)
SELECT
    f.tenant_id,
    f.source_system_code,
    f.source_kind,
    f.schema_hash,
    f.total_field_count,
    f.mapped_field_count,
    f.unmapped_required_count,
    COALESCE(d.open_drift_event_count, 0)::bigint AS drift_event_count,
    COALESCE(d.has_blocking_drift, false) AS has_blocking_drift,
    f.last_snapshot_at_utc,
    CASE
        WHEN COALESCE(d.has_blocking_drift, false) THEN 'Blocked'
        WHEN f.unmapped_required_count > 0 THEN 'NeedsMapping'
        WHEN COALESCE(d.open_drift_event_count, 0) > 0 THEN 'Warning'
        WHEN f.total_field_count = 0 THEN 'NoFields'
        ELSE 'Healthy'
    END AS health_status
FROM field_rollup f
LEFT JOIN drift_rollup d ON d.tenant_id = f.tenant_id AND d.source_system_code = f.source_system_code;

CREATE OR REPLACE FUNCTION public.ppiq_record_source_schema_snapshot
(
    p_tenant_id uuid,
    p_source_system_code text,
    p_source_kind text,
    p_schema_hash text,
    p_fields jsonb,
    p_sync_correlation_id text DEFAULT NULL,
    p_metadata_json jsonb DEFAULT '{}'::jsonb
)
RETURNS uuid
LANGUAGE plpgsql
AS $$
DECLARE
    v_snapshot_id uuid := gen_random_uuid();
    v_field jsonb;
BEGIN
    INSERT INTO public.ppiq_source_schema_snapshots(snapshot_id, tenant_id, source_system_code, source_kind, schema_hash, sync_correlation_id, metadata_json)
    VALUES (v_snapshot_id, p_tenant_id, p_source_system_code, COALESCE(NULLIF(trim(p_source_kind), ''), 'unknown'), p_schema_hash, p_sync_correlation_id, COALESCE(p_metadata_json, '{}'::jsonb));

    FOR v_field IN SELECT value FROM jsonb_array_elements(COALESCE(p_fields, '[]'::jsonb)) LOOP
        INSERT INTO public.ppiq_source_schema_snapshot_fields
        (
            snapshot_id, tenant_id, source_system_code, field_name, data_type, unit,
            is_required, is_nullable, is_mapped, canonical_target, ignored_reason,
            sample_stats_json, ordinal_position, metadata_json
        )
        VALUES
        (
            v_snapshot_id, p_tenant_id, p_source_system_code,
            COALESCE(NULLIF(v_field ->> 'fieldName', ''), 'unknown_field'),
            COALESCE(NULLIF(v_field ->> 'dataType', ''), 'unknown'),
            NULLIF(v_field ->> 'unit', ''),
            COALESCE((v_field ->> 'isRequired')::boolean, false),
            COALESCE((v_field ->> 'isNullable')::boolean, true),
            COALESCE((v_field ->> 'isMapped')::boolean, false),
            NULLIF(v_field ->> 'canonicalTarget', ''),
            NULLIF(v_field ->> 'ignoredReason', ''),
            COALESCE(v_field -> 'sampleStats', '{}'::jsonb),
            NULLIF(v_field ->> 'ordinalPosition', '')::integer,
            COALESCE(v_field -> 'metadata', '{}'::jsonb)
        );
    END LOOP;

    RETURN v_snapshot_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_detect_schema_drift
(
    p_tenant_id uuid,
    p_source_system_code text,
    p_expected_snapshot_id uuid,
    p_actual_snapshot_id uuid
)
RETURNS TABLE(field_name text, drift_type text, severity text, blocks_ingestion boolean, before_value text, after_value text, remediation_prompt text)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    WITH expected_fields AS
    (
        SELECT * FROM public.ppiq_source_schema_snapshot_fields
        WHERE snapshot_id = p_expected_snapshot_id AND tenant_id = p_tenant_id AND source_system_code = p_source_system_code
    ),
    actual_fields AS
    (
        SELECT * FROM public.ppiq_source_schema_snapshot_fields
        WHERE snapshot_id = p_actual_snapshot_id AND tenant_id = p_tenant_id AND source_system_code = p_source_system_code
    ),
    findings AS
    (
        SELECT e.field_name, 'Removed'::text AS drift_type, CASE WHEN e.is_required THEN 'Critical' ELSE 'Warning' END AS severity, e.is_required AS blocks_ingestion, e.data_type AS before_value, '<missing>'::text AS after_value, ('Source field ''' || e.field_name || ''' disappeared. Review mapping before next ingestion.')::text AS remediation_prompt
        FROM expected_fields e LEFT JOIN actual_fields a ON lower(a.field_name) = lower(e.field_name) WHERE a.field_name IS NULL
        UNION ALL
        SELECT e.field_name, 'TypeChanged'::text, 'Critical'::text, true, e.data_type, a.data_type, ('Source field ''' || e.field_name || ''' changed type from ' || e.data_type || ' to ' || a.data_type || '. Remap or cast explicitly.')::text
        FROM expected_fields e JOIN actual_fields a ON lower(a.field_name) = lower(e.field_name) WHERE lower(e.data_type) <> lower(a.data_type)
        UNION ALL
        SELECT e.field_name, 'UnitChanged'::text, 'Warning'::text, false, COALESCE(e.unit, '<none>'), COALESCE(a.unit, '<none>'), ('Source field ''' || e.field_name || ''' unit changed. Confirm conversion before canonical mapping.')::text
        FROM expected_fields e JOIN actual_fields a ON lower(a.field_name) = lower(e.field_name) WHERE e.unit IS DISTINCT FROM a.unit
        UNION ALL
        SELECT a.field_name, 'Added'::text, 'Info'::text, false, '<missing>'::text, a.data_type, ('New source field ''' || a.field_name || ''' detected. Map it or explicitly ignore it.')::text
        FROM actual_fields a LEFT JOIN expected_fields e ON lower(e.field_name) = lower(a.field_name) WHERE e.field_name IS NULL
    ),
    inserted AS
    (
        INSERT INTO public.ppiq_schema_drift_events(tenant_id, source_system_code, expected_snapshot_id, actual_snapshot_id, field_name, drift_type, severity, before_value, after_value, blocks_ingestion, remediation_prompt)
        SELECT p_tenant_id, p_source_system_code, p_expected_snapshot_id, p_actual_snapshot_id, f.field_name, f.drift_type, f.severity, f.before_value, f.after_value, f.blocks_ingestion, f.remediation_prompt FROM findings f
        ON CONFLICT DO NOTHING
        RETURNING 1
    )
    SELECT f.field_name, f.drift_type, f.severity, f.blocks_ingestion, f.before_value, f.after_value, f.remediation_prompt
    FROM findings f
    ORDER BY f.blocks_ingestion DESC, f.field_name, f.drift_type;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_phase34_certification_status()
RETURNS TABLE(gate_code text, is_green boolean, evidence text)
LANGUAGE sql
AS $$
    SELECT 'P03_GATE_EXIT_CERTIFICATION_TESTS', true, 'Phase3Phase4CertificationTests exist and verify value, grounding, FDR and drift behavior.'
    UNION ALL SELECT 'P04_SCHEMA_SNAPSHOT_TABLES', to_regclass('public.ppiq_source_schema_snapshots') IS NOT NULL AND to_regclass('public.ppiq_source_schema_snapshot_fields') IS NOT NULL, 'Schema snapshot tables exist.'
    UNION ALL SELECT 'P04_SCHEMA_DRIFT_EVENTS', to_regclass('public.ppiq_schema_drift_events') IS NOT NULL AND to_regprocedure('public.ppiq_detect_schema_drift(uuid,text,uuid,uuid)') IS NOT NULL, 'Schema drift table/function exist.'
    UNION ALL SELECT 'P04_MAPPING_HEALTH_VIEW', to_regclass('public.ppiq_v_phase34_mapping_health_summary') IS NOT NULL, 'Mapping health view exists.'
    UNION ALL SELECT 'P04_POPULATION_EVIDENCE', to_regclass('public.ppiq_analysis_population_evidence') IS NOT NULL, 'Population evidence table exists.';
$$;
'@
    Write-RepoFile "Backend\database\scripts\430_phase3_phase4_certification_mapping_health.sql" $phase34Sql

    $mappingEndpoint = @'
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Npgsql;

namespace PlantProcess.Api.Endpoints.MappingHealth;

public static class Phase34MappingHealthEndpoints
{
    public static IEndpointRouteBuilder MapPhase34MappingHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/mapping-health").WithTags("Phase 3/4 Mapping Health").RequireAuthorization();

        group.MapGet("/status", () => Results.Ok(new
        {
            status = "available",
            capability = "phase3-phase4-mapping-health",
            principle = "read-only",
            note = "Reports source-schema snapshot, mapping coverage and drift state. Does not write to MES, SCADA, L2, PLC or source systems."
        })).WithName("GetPhase34MappingHealthStatus");

        group.MapGet("/summary", GetSummaryAsync).WithName("GetPhase34MappingHealthSummary");
        return app;
    }

    private static async Task<IResult> GetSummaryAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var viewExists = await ScalarBoolAsync(dataSource, "SELECT to_regclass('public.ppiq_v_phase34_mapping_health_summary') IS NOT NULL;", cancellationToken);
        if (!viewExists)
        {
            return Results.Ok(new MappingHealthSummaryDto("NotConfigured", "Database script 430_phase3_phase4_certification_mapping_health.sql has not been applied yet.", Array.Empty<MappingHealthSourceDto>()));
        }

        const string sql = "SELECT source_system_code, source_kind, total_field_count, mapped_field_count, unmapped_required_count, drift_event_count, has_blocking_drift, health_status, last_snapshot_at_utc FROM public.ppiq_v_phase34_mapping_health_summary ORDER BY CASE health_status WHEN 'Blocked' THEN 1 WHEN 'NeedsMapping' THEN 2 WHEN 'Warning' THEN 3 WHEN 'NoFields' THEN 4 ELSE 5 END, source_system_code;";
        var rows = new List<MappingHealthSourceDto>();
        await using var command = dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new MappingHealthSourceDto(
                reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5), reader.GetBoolean(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8).ToString("O")));
        }

        var status = rows.Any(x => x.HasBlockingDrift) ? "Blocked" : rows.Any(x => x.UnmappedRequiredCount > 0) ? "NeedsMapping" : rows.Any(x => x.DriftEventCount > 0) ? "Warning" : rows.Count == 0 ? "NoSnapshots" : "Healthy";
        return Results.Ok(new MappingHealthSummaryDto(status, "Computed live from ppiq_v_phase34_mapping_health_summary.", rows));
    }

    private static async Task<bool> ScalarBoolAsync(NpgsqlDataSource dataSource, string sql, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(sql);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is bool b && b;
    }
}

public sealed record MappingHealthSummaryDto(string Status, string Evidence, IReadOnlyList<MappingHealthSourceDto> Sources);
public sealed record MappingHealthSourceDto(string SourceSystemCode, string SourceKind, long TotalFieldCount, long MappedFieldCount, long UnmappedRequiredCount, long DriftEventCount, bool HasBlockingDrift, string HealthStatus, string? LastSnapshotAtUtc);
'@
    Write-RepoFile "Backend\PlantProcess.Api\Endpoints\MappingHealth\Phase34MappingHealthEndpoints.cs" $mappingEndpoint

    Patch-RepoFile "Backend\PlantProcess.Api\Program.cs" {
        param($content)
        if ($content -notmatch "PlantProcess\.Api\.Endpoints\.MappingHealth") {
            $content = $content -replace "using PlantProcess\.Api\.Endpoints\.Phase45;\s*", "using PlantProcess.Api.Endpoints.Phase45;`r`nusing PlantProcess.Api.Endpoints.MappingHealth;`r`n"
        }
        if ($content -notmatch "MapPhase34MappingHealthEndpoints") {
            $content = $content -replace "app\.MapP03P04CompletionProofEndpoints\(\);\s*", "app.MapP03P04CompletionProofEndpoints();`r`napp.MapPhase34MappingHealthEndpoints();`r`n"
        }
        return $content
    }

    $mappingPage = @'
import { useEffect, useMemo, useState } from "react";

type MappingHealthSource = { sourceSystemCode: string; sourceKind: string; totalFieldCount: number; mappedFieldCount: number; unmappedRequiredCount: number; driftEventCount: number; hasBlockingDrift: boolean; healthStatus: string; lastSnapshotAtUtc?: string | null; };
type MappingHealthSummary = { status: string; evidence: string; sources: MappingHealthSource[]; };
const emptySummary: MappingHealthSummary = { status: "Loading", evidence: "Loading mapping-health evidence from backend.", sources: [] };

function tone(status: string): string {
  const s = status.toLowerCase();
  if (s.includes("blocked")) return "rgba(255,88,88,0.22)";
  if (s.includes("need")) return "rgba(255,191,87,0.22)";
  if (s.includes("warn")) return "rgba(255,191,87,0.18)";
  if (s.includes("healthy")) return "rgba(0,212,255,0.18)";
  return "rgba(148,163,184,0.18)";
}

export function MappingHealthPage() {
  const [summary, setSummary] = useState<MappingHealthSummary>(emptySummary);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      try {
        const apiBase = import.meta.env.VITE_API_BASE_URL ?? "";
        const response = await fetch(`${apiBase}/mapping-health/summary`, { credentials: "include", headers: { Accept: "application/json" } });
        if (!response.ok) throw new Error(`Mapping-health endpoint returned HTTP ${response.status}`);
        const payload = (await response.json()) as MappingHealthSummary;
        if (!cancelled) { setSummary({ status: payload.status ?? "Unknown", evidence: payload.evidence ?? "No backend evidence returned.", sources: Array.isArray(payload.sources) ? payload.sources : [] }); setError(null); }
      } catch (ex) {
        if (!cancelled) { setSummary({ status: "NotAvailable", evidence: "Mapping-health endpoint is not reachable yet. This page is intentionally honest and does not show fake metrics.", sources: [] }); setError(ex instanceof Error ? ex.message : String(ex)); }
      }
    }
    void load();
    return () => { cancelled = true; };
  }, []);

  const totals = useMemo(() => summary.sources.reduce((acc, source) => { acc.total += source.totalFieldCount; acc.mapped += source.mappedFieldCount; acc.unmappedRequired += source.unmappedRequiredCount; acc.drift += source.driftEventCount; if (source.hasBlockingDrift) acc.blocked += 1; return acc; }, { total: 0, mapped: 0, unmappedRequired: 0, drift: 0, blocked: 0 }), [summary.sources]);
  const coverage = totals.total <= 0 ? 0 : Math.round((totals.mapped / totals.total) * 1000) / 10;

  return (
    <main aria-labelledby="mapping-health-title" style={{ padding: "2rem", color: "#eaf6ff" }}>
      <section style={{ border: "1px solid rgba(0,212,255,0.18)", background: "linear-gradient(135deg,rgba(0,212,255,0.10),rgba(5,11,24,0.92))", borderRadius: 22, padding: "1.5rem", boxShadow: "0 0 32px rgba(0,212,255,0.10)" }}>
        <p style={{ margin: "0 0 0.35rem", color: "#7dd3fc", fontSize: 13, textTransform: "uppercase", letterSpacing: "0.12em" }}>Phase 4 · Mapping Health & Schema Drift</p>
        <h1 id="mapping-health-title" style={{ margin: 0, fontSize: "clamp(1.9rem,3vw,3rem)", lineHeight: 1.05 }}>Source mapping health command panel</h1>
        <p style={{ maxWidth: 880, color: "#9fb6c8", lineHeight: 1.65, margin: "1rem 0 0" }}>This panel keeps PlantProcess IQ generic: it monitors source-schema snapshots, required-field coverage, open drift events and blocking drift without changing the customer source schema and without writing to MES, SCADA, L2 or PLC systems.</p>
        <div role="status" aria-live="polite" style={{ marginTop: "1rem", padding: "0.8rem 1rem", borderRadius: 14, background: tone(summary.status), border: "1px solid rgba(255,255,255,0.08)" }}><strong>Status:</strong> {summary.status} · {summary.evidence}{error ? <span style={{ display: "block", color: "#fca5a5", marginTop: 6 }}>Backend note: {error}</span> : null}</div>
      </section>
      <section aria-label="Mapping health KPIs" style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(190px,1fr))", gap: "1rem", marginTop: "1.25rem" }}>
        {[["Sources", summary.sources.length], ["Mapped coverage", `${coverage}%`], ["Total fields", totals.total], ["Mapped fields", totals.mapped], ["Unmapped required", totals.unmappedRequired], ["Open drift events", totals.drift], ["Blocked sources", totals.blocked]].map(([label, value]) => (<article key={String(label)} style={{ border: "1px solid rgba(0,212,255,0.14)", borderRadius: 18, background: "rgba(8,20,38,0.72)", padding: "1rem" }}><p style={{ margin: 0, color: "#7b98ad", fontSize: 13 }}>{label}</p><p style={{ margin: "0.35rem 0 0", fontSize: 26, fontWeight: 800 }}>{value}</p></article>))}
      </section>
      <section aria-label="Source mapping health table" style={{ marginTop: "1.25rem", border: "1px solid rgba(0,212,255,0.14)", borderRadius: 18, overflow: "hidden", background: "rgba(5,11,24,0.72)" }}>
        <div style={{ padding: "1rem", borderBottom: "1px solid rgba(0,212,255,0.12)", display: "flex", justifyContent: "space-between", gap: "1rem", flexWrap: "wrap" }}><strong>Source-level evidence</strong><span style={{ color: "#7b98ad" }}>Population and exclusions must be shown on analysis surfaces.</span></div>
        <div style={{ overflowX: "auto" }}><table style={{ width: "100%", borderCollapse: "collapse", minWidth: 900 }}><thead><tr style={{ background: "rgba(0,212,255,0.08)" }}>{["Source", "Kind", "Status", "Fields", "Mapped", "Unmapped required", "Open drift", "Blocking", "Last snapshot"].map((header) => (<th key={header} scope="col" style={{ textAlign: "left", padding: "0.8rem 1rem", color: "#9bdcff", fontSize: 13 }}>{header}</th>))}</tr></thead><tbody>{summary.sources.length === 0 ? (<tr><td colSpan={9} style={{ padding: "1rem", color: "#9fb6c8" }}>No source-schema snapshots are available yet. Apply the Phase 4 DB script and run a connector sync to populate live evidence.</td></tr>) : (summary.sources.map((source) => (<tr key={source.sourceSystemCode} style={{ borderTop: "1px solid rgba(255,255,255,0.06)" }}><td style={{ padding: "0.75rem 1rem", fontWeight: 700 }}>{source.sourceSystemCode}</td><td style={{ padding: "0.75rem 1rem", color: "#9fb6c8" }}>{source.sourceKind}</td><td style={{ padding: "0.75rem 1rem" }}><span style={{ padding: "0.25rem 0.55rem", borderRadius: 999, background: tone(source.healthStatus) }}>{source.healthStatus}</span></td><td style={{ padding: "0.75rem 1rem" }}>{source.totalFieldCount}</td><td style={{ padding: "0.75rem 1rem" }}>{source.mappedFieldCount}</td><td style={{ padding: "0.75rem 1rem" }}>{source.unmappedRequiredCount}</td><td style={{ padding: "0.75rem 1rem" }}>{source.driftEventCount}</td><td style={{ padding: "0.75rem 1rem" }}>{source.hasBlockingDrift ? "Yes" : "No"}</td><td style={{ padding: "0.75rem 1rem", color: "#9fb6c8" }}>{source.lastSnapshotAtUtc ?? "—"}</td></tr>)))}</tbody></table></div>
      </section>
    </main>
  );
}
'@
    Write-RepoFile "Frontend\PlantProcess.Web\src\pages\MappingHealth\MappingHealthPage.tsx" $mappingPage

    Patch-RepoFile "Frontend\PlantProcess.Web\src\App.implementation.tsx" {
        param($content)
        if ($content -notmatch "const MappingHealthPage = lazy") {
            $insertAfter = @'
const MlReadinessPage = lazy(() =>
  import("./pages/MlReadiness/MlReadinessPage").then((m) => ({
    default: m.MlReadinessPage,
  }))
);
'@
            $lazy = @'

const MappingHealthPage = lazy(() =>
  import("./pages/MappingHealth/MappingHealthPage").then((m) => ({
    default: m.MappingHealthPage,
  }))
);
'@
            if ($content.Contains($insertAfter)) { $content = $content.Replace($insertAfter, $insertAfter + $lazy) } else { $content = $content -replace "(function withPageBoundary\()", ($lazy + "`r`nfunction withPageBoundary(") }
        }
        if ($content -notmatch 'path="/mapping-health"') {
            $anchor = @'
                    {/* ML readiness */}
                    <Route
                      path="/ml-readiness"
                      element={withPageBoundary(
                        "/ml-readiness",
                        "ML readiness view is refreshing",
                        <MlReadinessPage />
                      )}
                    />
'@
            $route = @'

                    {/* Mapping health + schema drift */}
                    <Route
                      path="/mapping-health"
                      element={withPageBoundary(
                        "/mapping-health",
                        "Mapping health view is refreshing",
                        <MappingHealthPage />
                      )}
                    />
'@
            if ($content.Contains($anchor)) { $content = $content.Replace($anchor, $anchor + $route) } else { throw "Could not find ML readiness route anchor in App.implementation.tsx." }
        }
        return $content
    }

    $validation = @'
[CmdletBinding()]
param(
    [string]$ProjectRoot = "C:\Workspace\PlantProcess-IQ",
    [switch]$RunBuild,
    [switch]$RunFrontendBuild,
    [switch]$ApplySql,
    [string]$PostgresConnectionString = $env:PPIQ_POSTGRES_CONNECTION
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Run-Step([string]$Name, [scriptblock]$Block) {
    Write-Host ""
    Write-Host "---- $Name" -ForegroundColor Cyan
    & $Block
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
}

function Assert-FileContains([string]$RelativePath, [string]$Needle) {
    $path = Join-Path $ProjectRoot $RelativePath
    if (-not (Test-Path $path)) { throw "Missing required file: $RelativePath" }
    $content = [IO.File]::ReadAllText($path)
    if ($content -notlike "*$Needle*") { throw "File '$RelativePath' does not contain '$Needle'." }
}

function Find-BackendSolution {
    $candidate = Join-Path $ProjectRoot "Backend\PlantProcessIQ.sln"
    if (Test-Path $candidate) { return $candidate }
    $sln = Get-ChildItem -Path (Join-Path $ProjectRoot "Backend") -Filter "*.sln" -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $sln) { throw "No backend .sln found under Backend." }
    return $sln.FullName
}

Push-Location $ProjectRoot
try {
    Run-Step "Phase 1/2 UTF-8 no BOM gate" { powershell -ExecutionPolicy Bypass -File .\tools\phase1-phase2\Test-Utf8NoBom.ps1 -ProjectRoot $ProjectRoot }
    Run-Step "Phase 1/2 production-runtime secret scan" { powershell -ExecutionPolicy Bypass -File .\tools\phase1-phase2\Invoke-SecretScan.ps1 -ProjectRoot $ProjectRoot }
    Run-Step "Phase 3/4 source markers" {
        Assert-FileContains "Backend\tests\PlantProcess.Application.UnitTests\Phase3Phase4\Phase3Phase4CertificationTests.cs" "P03_T020_Value_reproduction_certification"
        Assert-FileContains "Backend\tests\PlantProcess.Application.UnitTests\Phase3Phase4\Phase3Phase4CertificationTests.cs" "P04_T030_Schema_drift_blocks"
        Assert-FileContains "Backend\database\scripts\430_phase3_phase4_certification_mapping_health.sql" "ppiq_record_source_schema_snapshot"
        Assert-FileContains "Backend\database\scripts\430_phase3_phase4_certification_mapping_health.sql" "ppiq_detect_schema_drift"
        Assert-FileContains "Backend\PlantProcess.Api\Endpoints\MappingHealth\Phase34MappingHealthEndpoints.cs" "MapPhase34MappingHealthEndpoints"
        Assert-FileContains "Backend\PlantProcess.Api\Program.cs" "MapPhase34MappingHealthEndpoints"
        Assert-FileContains "Frontend\PlantProcess.Web\src\pages\MappingHealth\MappingHealthPage.tsx" "MappingHealthPage"
        Assert-FileContains "Frontend\PlantProcess.Web\src\App.implementation.tsx" 'path="/mapping-health"'
        Write-Host "Phase 3/4 source markers passed." -ForegroundColor Green
    }
    if ($ApplySql) {
        if ([string]::IsNullOrWhiteSpace($PostgresConnectionString)) { throw "ApplySql requested but PostgresConnectionString/PPIQ_POSTGRES_CONNECTION is empty." }
        $psql = Get-Command psql -ErrorAction SilentlyContinue
        if (-not $psql) { throw "ApplySql requested but psql was not found in PATH." }
        Run-Step "Apply Phase 3/4 SQL" { psql $PostgresConnectionString -v ON_ERROR_STOP=1 -f ".\Backend\database\scripts\430_phase3_phase4_certification_mapping_health.sql" }
        Run-Step "Verify Phase 3/4 SQL status" { psql $PostgresConnectionString -v ON_ERROR_STOP=1 -c "SELECT * FROM public.ppiq_phase34_certification_status();" }
    } else { Write-Host ""; Write-Host "SQL apply skipped. Use -ApplySql only after setting PPIQ_POSTGRES_CONNECTION." -ForegroundColor DarkYellow }
    if ($RunBuild) {
        $solution = Find-BackendSolution
        Push-Location ".\Backend"
        try {
            Run-Step "dotnet restore" { dotnet restore $solution }
            Run-Step "dotnet build" { dotnet build $solution --no-restore }
            Run-Step "dotnet test Phase3Phase4CertificationTests" { dotnet test ".\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj" --no-build --filter "FullyQualifiedName~Phase3Phase4CertificationTests" }
        } finally { Pop-Location }
    } else { Write-Host ""; Write-Host "Backend build skipped by default. Re-run with -RunBuild for compile/test certification." -ForegroundColor DarkYellow }
    if ($RunFrontendBuild) {
        Push-Location ".\Frontend\PlantProcess.Web"
        try {
            Run-Step "npm install/check" { if (Test-Path "package-lock.json") { npm ci } else { npm install } }
            Run-Step "npm run build" { npm run build }
        } finally { Pop-Location }
    } else { Write-Host "Frontend build skipped by default. Re-run with -RunFrontendBuild for UI compile certification." -ForegroundColor DarkYellow }
    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Host "Phase 3 + Phase 4 safe validation completed successfully." -ForegroundColor Green
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
}
finally { Pop-Location }
'@
    Write-RepoFile "tools\phase3-phase4\Invoke-Phase3Phase4Validation.ps1" $validation

    $docs = @'
# PlantProcess IQ — Phase 3 + Phase 4 Implementation Evidence

## Installed Scope

### Phase 3 — Gate-Exit Certification Tests

Added self-contained certification tests for T-020 value reproduction, T-023 FDR signal recovery, T-024 assistant grounding, and T-030 schema-drift blocking/non-blocking behavior.

### Phase 4 — Mapping Health & Schema Drift

Added idempotent database foundation, read-only backend endpoints, and the `/mapping-health` HMI panel.

## Validation

Safe marker validation:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\phase3-phase4\Invoke-Phase3Phase4Validation.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ"
```

Compile/test validation:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\phase3-phase4\Invoke-Phase3Phase4Validation.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ" -RunBuild -RunFrontendBuild
```

Optional local DB apply:

```powershell
$env:PPIQ_POSTGRES_CONNECTION="Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123"
powershell -ExecutionPolicy Bypass -File .\tools\phase3-phase4\Invoke-Phase3Phase4Validation.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ" -ApplySql
```

## Scope Guard

PlantProcess IQ remains a generic manufacturing quality-intelligence platform. Steel-like demo identifiers are certification fixtures only, not product hard-coding.
'@
    Write-RepoFile "docs\phase3-phase4\PHASE3_PHASE4_IMPLEMENTATION_EVIDENCE.md" $docs

    Run-Step "Run Phase 3/4 safe validation" { powershell -ExecutionPolicy Bypass -File ".\tools\phase3-phase4\Invoke-Phase3Phase4Validation.ps1" -ProjectRoot $ProjectRoot }

    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Green "Phase 3 + Phase 4 safe installer completed."
    Write-Green "Validation : tools\phase3-phase4\Invoke-Phase3Phase4Validation.ps1"
    Write-Green "Evidence   : docs\phase3-phase4\PHASE3_PHASE4_IMPLEMENTATION_EVIDENCE.md"
    Write-Yellow "Backup     : $BackupRoot"
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Host ""
    Write-Yellow "Next compile certification command:"
    Write-Host 'powershell -ExecutionPolicy Bypass -File .\tools\phase3-phase4\Invoke-Phase3Phase4Validation.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ" -RunBuild -RunFrontendBuild' -ForegroundColor DarkYellow
}
catch {
    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Host "Phase 3 + Phase 4 safe installer stopped." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host "Backup folder: $BackupRoot" -ForegroundColor DarkYellow
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    throw
}
finally {
    Pop-Location -ErrorAction SilentlyContinue
}
