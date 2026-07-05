& {
# ================================================================================================
# PPIQ PACK B (part 1): V1-42 CORRELATION RUN-TO-RESULT FIX + V1-41 STUCK-RUN REAPER + BACKFILL
# ================================================================================================
# ROOT CAUSE (proven from source + your walk forensics):
#   ppiq_ml_run_learning_job_v1 completes its v2 mirror inside a BEGIN..EXCEPTION WHEN OTHERS
#   THEN NULL block whose final UPDATE writes columns that DO NOT EXIST on
#   ml_correlation_compute_runs (finished_at_utc, result_count - that vocabulary belongs to
#   ml_learning_runs_v1). The UPDATE throws, the block rolls back the ALREADY-SUCCESSFUL
#   results_v2 INSERT with it, the error is swallowed, the run row stays 'Running' forever.
#   => 347 zombie runs, 0 rows in ml_correlation_results_v2, function returns 'Completed'.
# THE FIX (at source, per the Solution Doctrine):
#   [A] Corrected function: right columns (completed_at_utc, duration_ms, message), real
#       duration, and BOTH silent swallows replaced with honest failure writes.
#   [B] One-off backfill: existing zombies -> Failed(timeout-backfill), both run tables.
#   [C] Reaper hosted service (V1-41 minimum viable): Running beyond max-runtime ->
#       Failed(timeout), every 5 min, config-driven (default 30 min), logged.
#   [D] Live verification: run the learning job -> expect Completed + results_v2 > 0 +
#       zero Running rows; plus the walk-prover follow-ups (preview with the correct
#       sqlText property, material_units id type, seam-6 staging probe).
# Gates: stop API -> apply SQL -> dotnet build -> dotnet test. Commit gated on PPIQ_COMMIT=1.
# ================================================================================================
$ErrorActionPreference = 'Stop'
$RepoRoot = 'C:\Workspace\PlantProcess-IQ'
$enc = New-Object System.Text.UTF8Encoding($false)
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupDir = Join-Path $RepoRoot ('deploy\.ppiq-backups\correlation-fix-' + $stamp)
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

$PgUser = if ($env:PPIQ_PG_USER) { $env:PPIQ_PG_USER } else { 'ppiq_dev' }
$PgPass = if ($env:PPIQ_PG_PASS) { $env:PPIQ_PG_PASS } else { 'ppiq_dev_local_only' }
$PgDb   = if ($env:PPIQ_PG_DB)   { $env:PPIQ_PG_DB }   else { 'ppiq_app' }
$psql = (Get-Command psql -ErrorAction SilentlyContinue).Source
if (-not $psql) {
    $cand = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
    if ($cand) { $psql = $cand.FullName }
}
if (-not $psql) { throw 'psql not found' }
$env:PGPASSWORD = $PgPass
function Sql([string]$q) {
    $out = & $psql -h localhost -p 5432 -U $PgUser -d $PgDb -v ON_ERROR_STOP=1 -t -A -F '|' -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('psql: ' + ($out -join ' ')) }
    return @($out | Where-Object { $_ -ne '' })
}

# ---------------------------------------------------------------- [1/6] backups + preflight
Write-Host '[1/6] Backups + preflight'
& $psql -h localhost -p 5432 -U $PgUser -d $PgDb -c '\sf public.ppiq_ml_run_learning_job_v1' > (Join-Path $backupDir 'ppiq_ml_run_learning_job_v1.before.sql') 2>&1
Copy-Item (Join-Path $RepoRoot 'Backend\PlantProcess.Api\Program.cs') (Join-Path $backupDir 'Program.cs') -Force
$censusBefore = Sql "SELECT status || '=' || count(*) FROM ml_correlation_compute_runs GROUP BY status ORDER BY 1;"
Write-Host ('      compute-run census BEFORE: ' + ($censusBefore -join '  '))
$v1count = Sql "SELECT count(*) FROM ml_learning_results_v1;"
Write-Host ('      ml_learning_results_v1 rows (the v1 path that always worked): ' + $v1count[0])

# ---------------------------------------------------------------- [2/6] corrected function
Write-Host '[2/6] Applying the corrected ppiq_ml_run_learning_job_v1 (refuse-if-diverged)'
$curDef = (Sql "SELECT pg_get_functiondef('public.ppiq_ml_run_learning_job_v1(text,text,integer)'::regprocedure);") -join "`n"
if ($curDef -notmatch 'finished_at_utc') {
    Write-Host '      already fixed - skipped'
} else {
    $fnSql = @'
CREATE OR REPLACE FUNCTION public.ppiq_ml_run_learning_job_v1(
    p_job_code text DEFAULT 'ML_PROCESS_VS_DEFECT',
    p_outcome_family text DEFAULT NULL,
    p_window_days integer DEFAULT NULL)
RETURNS TABLE(
    run_id uuid,
    result_count integer,
    top_feature_key text,
    top_outcome_key text,
    top_effect_size double precision,
    status text)
LANGUAGE plpgsql
AS $$
DECLARE
    v_started timestamptz := clock_timestamp();
    v_run_id uuid := gen_random_uuid();
    v_family text;
    v_window_days integer;
    v_result_count integer;
    v_top_feature text;
    v_top_outcome text;
    v_top_effect double precision;
    v_compute_run_id uuid := gen_random_uuid();
BEGIN
    PERFORM public.ppiq_ml_seed_phase45_golden_dataset();

    SELECT
        COALESCE(p_outcome_family, outcome_family),
        COALESCE(p_window_days, default_window_days)
    INTO v_family, v_window_days
    FROM public.ml_learning_job_catalog_v1
    WHERE job_code = p_job_code;

    IF v_family IS NULL THEN
        RAISE EXCEPTION 'Unknown ML learning job code: %', p_job_code;
    END IF;

    INSERT INTO public.ml_learning_runs_v1
        (id, job_code, outcome_family, window_days, status, readiness_status, readiness_message, request_json)
    VALUES
        (
            v_run_id,
            p_job_code,
            v_family,
            v_window_days,
            'Running',
            'PassedForDemoLearningCore',
            'Golden dataset contains sufficient deterministic samples; production prediction remains disabled.',
            jsonb_build_object('jobCode', p_job_code, 'outcomeFamily', v_family, 'windowDays', v_window_days)
        );

    -- Existing foundation integration: create a compute run where available.
    BEGIN
        INSERT INTO public.ml_correlation_compute_runs
            (id, engine_key, target_outcome_key, grain, window_days, status, request_json)
        VALUES
            (v_compute_run_id, 'ppiql-deterministic-core-v1', v_family, 'multi-grain', v_window_days, 'Running',
             jsonb_build_object('source', 'ppiq_ml_run_learning_job_v1', 'jobCode', p_job_code));
    EXCEPTION
        WHEN OTHERS THEN
            RAISE WARNING 'ppiq_ml_run_learning_job_v1: compute-run ledger insert skipped: %', SQLERRM;
    END;

    -- Numeric features against numeric/binary outcomes.
    WITH base AS
    (
        SELECT *
        FROM public.ml_learning_observations_v1
        WHERE observed_at_utc >= now() - make_interval(days => v_window_days)
    ),
    features AS
    (
        SELECT coil_id, heat_id, observed_at_utc, 'thermal.true_superheat_c'::text AS feature_key, 'heat'::text AS feature_grain, 'continuous'::text AS feature_type, true_superheat_c AS x FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'casting.speed_delta_mpm', 'strand', 'continuous', casting_speed_delta_mpm FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'rolling.reduction_ratio', 'coil', 'continuous', reduction_ratio FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'rolling.cooling_rate_cps', 'coil', 'continuous', cooling_rate_cps FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'noise.planted_random', 'coil', 'continuous', planted_noise::double precision FROM base
    ),
    outcomes AS
    (
        SELECT coil_id, heat_id, observed_at_utc, 'quality.defect_rate_per_m2'::text AS outcome_key, 'defect'::text AS family, 'continuous'::text AS outcome_type, defect_rate_per_m2 AS y FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'quality.defect_hold_binary', 'defect', 'binary', defect_hold_binary FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'downtime.equipment_stoppage_min', 'downtime', 'continuous', equipment_stoppage_min FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'downtime.production_stoppage_min', 'downtime', 'continuous', production_stoppage_min FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'kpi.prime_yield', 'kpi', 'continuous', kpi_prime_yield FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'kpi.energy_per_ton', 'kpi', 'continuous', kpi_energy_per_ton FROM base
    ),
    pairs AS
    (
        SELECT
            f.feature_key,
            f.feature_grain,
            f.feature_type,
            o.outcome_key,
            o.family,
            o.outcome_type,
            CASE WHEN o.outcome_type = 'binary' THEN 'point_biserial' ELSE 'pearson' END AS method,
            corr(f.x, o.y) AS coefficient,
            abs(corr(f.x, o.y)) AS effect_size,
            count(*)::integer AS sample_size,
            count(DISTINCT f.heat_id)::integer AS effective_n,
            min(f.observed_at_utc) AS window_start_utc,
            max(f.observed_at_utc) AS window_end_utc
        FROM features f
        JOIN outcomes o
          ON o.coil_id = f.coil_id
        WHERE v_family IN ('all', 'overall') OR o.family = v_family
        GROUP BY f.feature_key, f.feature_grain, f.feature_type, o.outcome_key, o.family, o.outcome_type
        HAVING count(*) >= 8
    )
    INSERT INTO public.ml_learning_results_v1
    (
        run_id,
        job_code,
        outcome_family,
        feature_key,
        feature_grain,
        feature_value_type,
        outcome_key,
        outcome_type,
        method,
        coefficient,
        effect_size,
        effect_size_type,
        direction,
        raw_statistic,
        p_value,
        ci_low,
        ci_high,
        sample_size,
        effective_n,
        power_status,
        strength_bucket,
        stability_score,
        is_stable,
        confounding_note,
        vif_group_key,
        finding_status,
        window_start_utc,
        window_end_utc,
        evidence_json
    )
    SELECT
        v_run_id,
        p_job_code,
        family,
        feature_key,
        feature_grain,
        feature_type,
        outcome_key,
        outcome_type,
        method,
        coefficient,
        effect_size,
        'absolute_association',
        CASE WHEN coefficient >= 0 THEN 'positive' ELSE 'negative' END,
        coefficient,
        GREATEST(1e-300::double precision, LEAST(1.0::double precision, exp(GREATEST(-700.0::double precision, -abs(COALESCE(coefficient, 0)) * sqrt(sample_size::double precision))))),
        GREATEST(-1.0, LEAST(1.0, COALESCE(coefficient, 0) - 1.96 * sqrt(GREATEST(0.000001, 1 - power(COALESCE(coefficient, 0), 2)) / GREATEST(sample_size - 2, 1)))),
        GREATEST(-1.0, LEAST(1.0, COALESCE(coefficient, 0) + 1.96 * sqrt(GREATEST(0.000001, 1 - power(COALESCE(coefficient, 0), 2)) / GREATEST(sample_size - 2, 1)))),
        sample_size,
        effective_n,
        CASE WHEN effective_n >= 20 THEN 'EnoughIndependentUnits' ELSE 'UnderPowered' END,
        CASE
            WHEN effect_size >= 0.70 THEN 'very_strong'
            WHEN effect_size >= 0.45 THEN 'strong'
            WHEN effect_size >= 0.25 THEN 'moderate'
            WHEN effect_size >= 0.10 THEN 'weak'
            ELSE 'negligible'
        END,
        CASE
            WHEN feature_key = 'noise.planted_random' THEN 0.20
            WHEN effect_size >= 0.45 THEN 0.86
            WHEN effect_size >= 0.25 THEN 0.72
            ELSE 0.48
        END,
        CASE
            WHEN feature_key = 'noise.planted_random' THEN false
            WHEN effect_size >= 0.25 THEN true
            ELSE false
        END,
        CASE
            WHEN feature_key LIKE 'rolling.%' THEN 'Check grade and product-family stratification before operational action.'
            WHEN feature_key LIKE 'thermal.%' THEN 'Heat-clustered effective-n applied; not raw coil count.'
            ELSE 'No confounding warning at this proof stage.'
        END,
        CASE
            WHEN feature_key LIKE 'thermal.%' THEN 'thermal_group'
            WHEN feature_key LIKE 'casting.%' THEN 'casting_group'
            WHEN feature_key LIKE 'rolling.%' THEN 'rolling_group'
            ELSE 'noise_or_other'
        END,
        'CandidateForReview',
        window_start_utc,
        window_end_utc,
        jsonb_build_object(
            'noLlmInComputePath', true,
            'engine', 'ppiql-deterministic-core-v1',
            'effectiveNDefinition', 'distinct heat_id',
            'honestFraming', 'diagnostic association only, not guaranteed root cause',
            'methodSelection', method
        )
    FROM pairs
    WHERE coefficient IS NOT NULL;

    -- Categorical feature vs binary/categorical outcome proof.
    WITH base AS
    (
        SELECT *
        FROM public.ml_learning_observations_v1
        WHERE observed_at_utc >= now() - make_interval(days => v_window_days)
    ),
    cat_features AS
    (
        SELECT coil_id, heat_id, observed_at_utc, 'product.grade_family'::text AS feature_key, 'heat'::text AS feature_grain, grade_family AS feature_value FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'product.gauge_band', 'coil', gauge_band FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'operations.crew_shift', 'coil', crew_shift FROM base
    ),
    cat_outcomes AS
    (
        SELECT coil_id, heat_id, observed_at_utc, 'quality.defect_hold_binary'::text AS outcome_key, 'defect'::text AS family, defect_hold_binary AS y, defect_hold_binary::text AS y_text FROM base
        UNION ALL SELECT coil_id, heat_id, observed_at_utc, 'downtime.cascade_amplified_flag', 'downtime', CASE WHEN cascade_amplified_flag THEN 1.0 ELSE 0.0 END, cascade_amplified_flag::text FROM base
    ),
    grouped AS
    (
        SELECT
            feature_key,
            feature_grain,
            outcome_key,
            family,
            sum(n)::integer AS sample_size,
            count(DISTINCT heat_id)::integer AS effective_n,
            max(avg_y) - min(avg_y) AS spread
        FROM
        (
            SELECT
                f.feature_key,
                f.feature_grain,
                f.feature_value,
                o.outcome_key,
                o.family,
                f.heat_id,
                avg(o.y) AS avg_y,
                count(*)::integer AS n
            FROM cat_features f
            JOIN cat_outcomes o
              ON o.coil_id = f.coil_id
            WHERE v_family IN ('all', 'overall') OR o.family = v_family
            GROUP BY
                f.feature_key,
                f.feature_grain,
                f.feature_value,
                o.outcome_key,
                o.family,
                f.heat_id
        ) s
        GROUP BY
            feature_key,
            feature_grain,
            outcome_key,
            family
    )
    INSERT INTO public.ml_learning_results_v1
    (
        run_id,
        job_code,
        outcome_family,
        feature_key,
        feature_grain,
        feature_value_type,
        outcome_key,
        outcome_type,
        method,
        coefficient,
        effect_size,
        effect_size_type,
        direction,
        raw_statistic,
        p_value,
        q_value,
        ci_low,
        ci_high,
        sample_size,
        effective_n,
        power_status,
        strength_bucket,
        stability_score,
        is_stable,
        confounding_note,
        vif_group_key,
        finding_status,
        evidence_json
    )
    SELECT
        v_run_id,
        p_job_code,
        family,
        feature_key,
        feature_grain,
        'categorical',
        outcome_key,
        'binary',
        'cramers_v',
        spread,
        abs(spread),
        'cramers_v_proxy',
        CASE WHEN spread >= 0 THEN 'positive_group_spread' ELSE 'negative_group_spread' END,
        spread,
        GREATEST(1e-300::double precision, LEAST(1.0::double precision, exp(GREATEST(-700.0::double precision, -abs(spread) * sqrt(sample_size::double precision))))),
        NULL,
        NULL,
        NULL,
        sample_size,
        effective_n,
        CASE WHEN effective_n >= 20 THEN 'EnoughIndependentUnits' ELSE 'UnderPowered' END,
        CASE
            WHEN abs(spread) >= 0.50 THEN 'strong'
            WHEN abs(spread) >= 0.25 THEN 'moderate'
            WHEN abs(spread) >= 0.10 THEN 'weak'
            ELSE 'negligible'
        END,
        CASE WHEN abs(spread) >= 0.25 THEN 0.75 ELSE 0.45 END,
        CASE WHEN abs(spread) >= 0.25 THEN true ELSE false END,
        'Categorical association proof; validate with stratification before operational action.',
        'categorical_group',
        'CandidateForReview',
        jsonb_build_object(
            'noLlmInComputePath', true,
            'engine', 'ppiql-deterministic-core-v1',
            'methodSelection', 'cramers_v',
            'honestFraming', 'diagnostic categorical association only'
        )
    FROM grouped
    WHERE sample_size >= 8;

    -- Benjamini-Hochberg q-values per run.
    WITH ranked AS
    (
        SELECT
            id,
            p_value,
            count(*) OVER ()::double precision AS m,
            row_number() OVER (ORDER BY p_value NULLS LAST)::double precision AS rn
        FROM public.ml_learning_results_v1
        WHERE public.ml_learning_results_v1.run_id = v_run_id
          AND p_value IS NOT NULL
    )
    UPDATE public.ml_learning_results_v1 r
    SET q_value = LEAST(1.0, ranked.p_value * ranked.m / ranked.rn)
    FROM ranked
    WHERE ranked.id = r.id;

    UPDATE public.ml_learning_results_v1
    SET finding_status =
        CASE
            WHEN power_status <> 'EnoughIndependentUnits' THEN 'BlockedUnderPowered'
            WHEN feature_key = 'noise.planted_random' THEN 'RejectedNoiseControl'
            WHEN q_value <= 0.10 AND effect_size >= 0.25 AND is_stable THEN 'EvidenceForReview'
            ELSE 'NotSurfaced'
        END
    WHERE public.ml_learning_results_v1.run_id = v_run_id;

    SELECT count(*)::integer
    INTO v_result_count
    FROM public.ml_learning_results_v1
    WHERE public.ml_learning_results_v1.run_id = v_run_id;

    SELECT feature_key, outcome_key, effect_size
    INTO v_top_feature, v_top_outcome, v_top_effect
    FROM public.ml_learning_results_v1
    WHERE public.ml_learning_results_v1.run_id = v_run_id
      AND feature_key <> 'noise.planted_random'
    ORDER BY effect_size DESC NULLS LAST
    LIMIT 1;

    -- Mirror into existing v2 result table when available.
    BEGIN
        INSERT INTO public.ml_correlation_results_v2
        (
            compute_run_id,
            feature_key,
            feature_grain,
            outcome_key,
            outcome_type,
            method,
            coefficient,
            effect_size,
            effect_size_type,
            p_value,
            q_value,
            ci_low,
            ci_high,
            sample_size,
            effective_n,
            stability_score,
            is_stable,
            window_start_utc,
            window_end_utc,
            evidence_json
        )
        SELECT
            v_compute_run_id,
            feature_key,
            feature_grain,
            outcome_key,
            outcome_type,
            method,
            coefficient,
            effect_size,
            effect_size_type,
            p_value,
            q_value,
            ci_low,
            ci_high,
            sample_size,
            effective_n,
            stability_score,
            is_stable,
            window_start_utc,
            window_end_utc,
            evidence_json || jsonb_build_object('sourceRunId', v_run_id)
        FROM public.ml_learning_results_v1
        WHERE public.ml_learning_results_v1.run_id = v_run_id;

        UPDATE public.ml_correlation_compute_runs
        SET status = 'Completed',
            completed_at_utc = now(),
            duration_ms = GREATEST(0, (extract(epoch FROM clock_timestamp() - v_started) * 1000)::integer),
            message = 'deterministic-core (golden dataset): ' || v_result_count || ' findings mirrored to results_v2'
        WHERE id = v_compute_run_id;
    EXCEPTION
        WHEN OTHERS THEN
            UPDATE public.ml_correlation_compute_runs
            SET status = 'Failed',
                completed_at_utc = now(),
                duration_ms = GREATEST(0, (extract(epoch FROM clock_timestamp() - v_started) * 1000)::integer),
                message = left('results_v2 mirror failed: ' || SQLERRM, 500)
            WHERE id = v_compute_run_id;
            RAISE WARNING 'ppiq_ml_run_learning_job_v1: results_v2 mirror failed: %', SQLERRM;
    END;

    UPDATE public.ml_learning_runs_v1
    SET status = 'Completed',
        finished_at_utc = now(),
        result_count = v_result_count
    WHERE id = v_run_id;

    UPDATE public.ml_learning_job_catalog_v1
    SET last_status = 'Completed',
        last_run_id = v_run_id,
        last_finished_at_utc = now(),
        next_run_hint_utc =
            CASE
                WHEN job_code = 'ML_WEEKLY_OVERALL' THEN now() + interval '7 days'
                ELSE now() + interval '1 day'
            END,
        last_error = NULL,
        updated_at_utc = now()
    WHERE job_code = p_job_code;

    RETURN QUERY
    SELECT
        v_run_id,
        v_result_count,
        v_top_feature,
        v_top_outcome,
        v_top_effect,
        'Completed'::text;
END;
$$;

'@
    $tmp = Join-Path $env:TEMP ('ppiq-fn-fix-' + $stamp + '.sql')
    [System.IO.File]::WriteAllText($tmp, $fnSql, $enc)
    $out = & $psql -h localhost -p 5432 -U $PgUser -d $PgDb -v ON_ERROR_STOP=1 -f $tmp 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('function apply failed: ' + ($out -join ' ')) }
    Write-Host '      corrected function applied'
    # persist the same fix into the repo migration so fresh installs are correct
    $mig = Join-Path $RepoRoot 'Backend\database\scripts\204_phase04_phase05_ml_learning_core.sql'
    $migText = [System.IO.File]::ReadAllText($mig)
    $isCrlf = $migText.Contains("`r`n")
    $norm = $migText.Replace("`r", "")
    $start = $norm.IndexOf('CREATE OR REPLACE FUNCTION public.ppiq_ml_run_learning_job_v1(')
    if ($start -lt 0) { throw 'function not found in migration 204 - refusing' }
    $endMark = $norm.IndexOf('$$;', $norm.IndexOf('AS $$', $start))
    if ($endMark -lt 0) { throw 'function terminator not found - refusing' }
    $norm = $norm.Substring(0, $start) + $fnSql.Replace("`r", "") + $norm.Substring($endMark + 3)
    if ($isCrlf) { $norm = $norm -replace "`n", "`r`n" }
    [System.IO.File]::WriteAllText($mig, $norm, $enc)
    Write-Host '      migration script 204 updated (fresh installs get the fix)'
}

# ---------------------------------------------------------------- [3/6] zombie backfill
Write-Host '[3/6] One-off zombie backfill (V1-41)'
$n1 = Sql "WITH u AS (UPDATE ml_correlation_compute_runs SET status='Failed', completed_at_utc=now(), message='Failed(timeout-backfill V1-41): zombie Running run terminalized' WHERE status='Running' RETURNING 1) SELECT count(*) FROM u;"
$n2 = Sql "WITH u AS (UPDATE ml_learning_runs_v1 SET status='Failed', finished_at_utc=now(), error_message='Failed(timeout-backfill V1-41)' WHERE status='Running' RETURNING 1) SELECT count(*) FROM u;"
Write-Host ('      terminalized: compute_runs=' + $n1[0] + '  learning_runs=' + $n2[0])

# ---------------------------------------------------------------- [4/6] reaper (V1-41)
Write-Host '[4/6] Reaper hosted service'
$svcPath = Join-Path $RepoRoot 'Backend\PlantProcess.Api\Hosting\ComputeRunReaperHostedService.cs'
$svc = @'
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Hosting;

/// <summary>
/// Stuck-run reaper (minimum viable governance): any analytics run left in 'Running'
/// beyond the configured max runtime is transitioned to Failed(timeout) with an honest
/// message, so the Jobs Monitor and the run ledger never show phantom in-flight work.
/// Config: PlantProcess:Analytics:StuckRunMaxMinutes (default 30),
///         PlantProcess:Analytics:ReaperIntervalMinutes (default 5).
/// </summary>
public sealed class ComputeRunReaperHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ComputeRunReaperHostedService> _logger;
    private readonly int _maxMinutes;
    private readonly TimeSpan _interval;

    public ComputeRunReaperHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ComputeRunReaperHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _maxMinutes = Math.Max(1, configuration.GetValue<int?>("PlantProcess:Analytics:StuckRunMaxMinutes") ?? 30);
        var intervalMinutes = Math.Max(1, configuration.GetValue<int?>("PlantProcess:Analytics:ReaperIntervalMinutes") ?? 5);
        _interval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Stuck-run reaper active. MaxRuntime={MaxMinutes}min Interval={IntervalMinutes}min",
            _maxMinutes,
            _interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReapOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stuck-run reaper tick failed; will retry next interval.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReapOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlantProcessDbContext>();

        var computeReaped = await db.Database.ExecuteSqlRawAsync(
            "UPDATE public.ml_correlation_compute_runs " +
            "SET status = 'Failed', completed_at_utc = now(), " +
            "    message = left(coalesce(message || ' | ', '') || 'Failed(timeout): exceeded max runtime of ' || {0} || ' minutes (reaper)', 500) " +
            "WHERE status = 'Running' AND started_at_utc < now() - make_interval(mins => {0})",
            new object[] { _maxMinutes },
            ct);

        var learningReaped = await db.Database.ExecuteSqlRawAsync(
            "UPDATE public.ml_learning_runs_v1 " +
            "SET status = 'Failed', finished_at_utc = now(), " +
            "    error_message = 'Failed(timeout): exceeded max runtime of ' || {0} || ' minutes (reaper)' " +
            "WHERE status = 'Running' AND started_at_utc < now() - make_interval(mins => {0})",
            new object[] { _maxMinutes },
            ct);

        if (computeReaped > 0 || learningReaped > 0)
        {
            _logger.LogWarning(
                "Stuck-run reaper transitioned {ComputeReaped} compute run(s) and {LearningReaped} learning run(s) to Failed(timeout).",
            computeReaped,
            learningReaped);
        }
    }
}

'@
New-Item -ItemType Directory -Path (Split-Path $svcPath) -Force | Out-Null
[System.IO.File]::WriteAllText($svcPath, $svc, $enc)
$progPath = Join-Path $RepoRoot 'Backend\PlantProcess.Api\Program.cs'
$prog = [System.IO.File]::ReadAllText($progPath)
if ($prog.Contains('ComputeRunReaperHostedService')) {
    Write-Host '      Program.cs already registered - skipped'
} else {
    $a = 'builder.Services.AddHostedService<PlantProcess.Api.Security.FirstRunProvisioningHostedService>();'
    $c = ([regex]::Matches($prog, [regex]::Escape($a))).Count
    if ($c -ne 1) { throw ('Program.cs anchor found ' + $c + ' times - refusing') }
    $prog = $prog.Replace($a, @'
builder.Services.AddHostedService<PlantProcess.Api.Security.FirstRunProvisioningHostedService>();
builder.Services.AddHostedService<PlantProcess.Api.Hosting.ComputeRunReaperHostedService>();
'@)
    [System.IO.File]::WriteAllText($progPath, $prog, $enc)
    Write-Host '      Program.cs: reaper registered after FirstRunProvisioning'
}

# ---------------------------------------------------------------- [5/6] live verification
Write-Host '[5/6] LIVE VERIFICATION'
$run = Sql "SELECT run_id || '|' || result_count || '|' || coalesce(top_feature_key,'-') || '|' || coalesce(top_outcome_key,'-') || '|' || coalesce(top_effect_size::text,'-') || '|' || status FROM ppiq_ml_run_learning_job_v1();"
Write-Host ('      learning run: ' + $run[0])
$res = Sql "SELECT count(*) FROM ml_correlation_results_v2;"
Write-Host ('      ml_correlation_results_v2 rows: ' + $res[0] + '   (was 0 - THIS is V1-42)')
if ([int]$res[0] -le 0) { throw 'results_v2 still empty - send this output' }
$census = Sql "SELECT status || '=' || count(*) FROM ml_correlation_compute_runs GROUP BY status ORDER BY 1;"
Write-Host ('      census AFTER: ' + ($census -join '  '))
$running = Sql "SELECT count(*) FROM ml_correlation_compute_runs WHERE status='Running';"
if ([int]$running[0] -ne 0) { Write-Host ('      NOTE: ' + $running[0] + ' still Running (fresh legitimate runs)') }
$top = Sql "SELECT feature_key || ' -> ' || outcome_key || '  effect=' || round(effect_size::numeric,3) || '  q=' || coalesce(round(q_value::numeric,4)::text,'-') || '  n=' || sample_size FROM ml_correlation_results_v2 ORDER BY effect_size DESC NULLS LAST LIMIT 3;"
Write-Host '      top findings now in results_v2:'
$top | ForEach-Object { Write-Host ('        ' + $_) }
Write-Host '      -- walk-prover follow-ups --'
$mu = Sql "SELECT data_type FROM information_schema.columns WHERE table_name='material_units' AND column_name='id';"
Write-Host ('      material_units.id type: ' + $mu[0])
$coil = Sql "SELECT count(*) FROM material_units WHERE material_code = 'C-0044170';"
Write-Host ('      C-0044170 present: ' + $coil[0] + '  (0 expected on the meltshop-only green-field DB; V1-11 needs the full demo fleet)')
$walkRow = Sql "SELECT count(*) FROM src_meltshop_pg.heats WHERE heat_no LIKE 'WALK-%' OR heat_no LIKE '%WALK%';"
Write-Host ('      seam-6 injected WALK row reached staging: ' + $walkRow[0] + '  (0 => Stage-1 watermark did not pick it up; send output)')

# ---------------------------------------------------------------- [6/6] gates
Write-Host '[6/6] Gates'
$api = Get-Process -Name 'PlantProcess.Api' -ErrorAction SilentlyContinue
if ($api) { $api | Stop-Process -Force; Start-Sleep -Seconds 2; Write-Host '      stopped running API' }
Push-Location (Join-Path $RepoRoot 'Backend')
try {
    dotnet build --nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build FAILED' }
    dotnet test --nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test FAILED' }
} finally { Pop-Location }
Write-Host ''
Write-Host 'GREEN. V1-42 plumbing fixed at source; V1-41 backfill done + reaper active on next API start.'
Write-Host 'Restart the API and watch for: "Stuck-run reaper active. MaxRuntime=30min".'
if ($env:PPIQ_COMMIT -eq '1') {
    Push-Location $RepoRoot
    try {
        git add Backend/database/scripts/204_phase04_phase05_ml_learning_core.sql Backend/PlantProcess.Api/Hosting/ComputeRunReaperHostedService.cs Backend/PlantProcess.Api/Program.cs
        $msgFile = Join-Path $env:TEMP ('ppiq-corr-fix-' + $stamp + '.txt')
        $msg = @(
            'Fix correlation run-to-result at source; add stuck-run reaper (V1-41/V1-42)',
            '',
            '- ppiq_ml_run_learning_job_v1: completion UPDATE wrote finished_at_utc/result_count,',
            '  columns that exist on ml_learning_runs_v1 but NOT on ml_correlation_compute_runs;',
            '  the throw inside WHEN OTHERS THEN NULL rolled back the successful results_v2 insert',
            '  and left every run eternally Running. Corrected columns, real duration, honest',
            '  failure writes replacing both silent swallows; migration 204 updated for fresh installs.',
            '- One-off backfill terminalizes existing zombie Running rows in both run tables.',
            '- ComputeRunReaperHostedService: Running beyond configurable max runtime (default 30min)',
            '  -> Failed(timeout) every 5min tick, logged; registered in Program.cs.'
        )
        [System.IO.File]::WriteAllText($msgFile, ($msg -join "`n"), $enc)
        git commit -F $msgFile
        Write-Host 'Committed.'
    } finally { Pop-Location }
} else {
    Write-Host 'Commit skipped. $env:PPIQ_COMMIT=''1'' and re-run to commit (idempotent).'
}
}
