using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace PlantProcess.Api.PlantConnectors;

public sealed record RegisterMockHistorianSourceRequest(
    string ProviderCode,
    string DisplayName,
    bool MarkAsCertifiedProof = false);

public sealed record ReadMockHistorianWindowRequest(
    string ProviderCode,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int MaxPoints = 500);

public sealed record RunBackfillProofRequest(
    string ProviderCode,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int BatchSize = 100,
    bool SimulateKillAfterFirstBatch = false);

public sealed record UpdateTruthStateRequest(
    string ProviderCode,
    bool IsReachable,
    bool IsCertified,
    int LagSeconds,
    string? Status,
    string? Notes);

public static class V5ConnectorRuntimeCertificationEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static IEndpointRouteBuilder MapV5ConnectorRuntimeCertificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/connectors/runtime-certification")
            .WithTags("V5 Connector Runtime Certification");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-connector-runtime-certification",
            connectorKind = "mock-historian",
            readOnly = true,
            writeMethodsExposed = false,
            backfillCheckpointing = true,
            resumeProof = true,
            truthState = new[]
            {
                "reachable",
                "certified",
                "lagSeconds",
                "lastSuccessfulReadUtc",
                "computedTruthStatus"
            },
            note = "This is a deterministic runtime proof connector. Production OPC-UA / historian certification remains environment-specific."
        }));

        group.MapPost("/mock-historian/register-source", async (
            [FromBody] RegisterMockHistorianSourceRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var providerCode = NormalizeProviderCode(request.ProviderCode);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var sourceId = await EnsureSourceAsync(
                connection,
                tenantId,
                providerCode,
                string.IsNullOrWhiteSpace(request.DisplayName) ? providerCode : request.DisplayName.Trim(),
                request.MarkAsCertifiedProof,
                cancellationToken);

            await WriteEventAsync(
                connection,
                tenantId,
                providerCode,
                "register-mock-historian-source",
                "passed",
                new
                {
                    sourceId,
                    readOnly = true,
                    writeMethodsExposed = false,
                    certificationScope = request.MarkAsCertifiedProof ? "certified-proof" : "runtime-proof"
                },
                cancellationToken);

            return Results.Ok(new
            {
                sourceId,
                providerCode,
                connectorKind = "mock-historian",
                readOnly = true,
                certifiedProof = request.MarkAsCertifiedProof,
                status = "ready"
            });
        });

        group.MapPost("/mock-historian/read-window", async (
            [FromBody] ReadMockHistorianWindowRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var providerCode = NormalizeProviderCode(request.ProviderCode);

            if (request.ToUtc <= request.FromUtc)
                return Results.BadRequest(new { error = "ToUtc must be greater than FromUtc." });

            var maxPoints = Math.Clamp(request.MaxPoints, 1, 5000);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await EnsureSourceAsync(connection, tenantId, providerCode, providerCode, false, cancellationToken);
            await EnsureSeedReadingsAsync(connection, tenantId, providerCode, request.FromUtc, request.ToUtc, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    r.tag_path,
                    r.observed_at_utc,
                    r.numeric_value,
                    r.quality
                FROM public.ppiq_connector_runtime_readings r
                WHERE r.tenant_id = @tenant_id
                  AND r.provider_code = @provider_code
                  AND r.observed_at_utc >= @from_utc
                  AND r.observed_at_utc <= @to_utc
                ORDER BY r.observed_at_utc, r.tag_path
                LIMIT @limit
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("provider_code", providerCode);
            cmd.Parameters.AddWithValue("from_utc", request.FromUtc.UtcDateTime);
            cmd.Parameters.AddWithValue("to_utc", request.ToUtc.UtcDateTime);
            cmd.Parameters.AddWithValue("limit", maxPoints);

            var readings = new List<object>();

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                readings.Add(new
                {
                    tagPath = reader.GetString(0),
                    observedAtUtc = reader.GetDateTime(1),
                    numericValue = reader.GetDecimal(2),
                    quality = reader.GetString(3)
                });
            }

            await reader.DisposeAsync();

            await MarkReadSuccessAsync(connection, tenantId, providerCode, 0, cancellationToken);

            await WriteEventAsync(
                connection,
                tenantId,
                providerCode,
                "read-window",
                readings.Count > 0 ? "passed" : "failed",
                new
                {
                    fromUtc = request.FromUtc,
                    toUtc = request.ToUtc,
                    rows = readings.Count,
                    readOnly = true
                },
                cancellationToken);

            return Results.Ok(new
            {
                providerCode,
                readOnly = true,
                fromUtc = request.FromUtc,
                toUtc = request.ToUtc,
                rowCount = readings.Count,
                readings
            });
        });

        group.MapPost("/backfill/run", async (
            [FromBody] RunBackfillProofRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var providerCode = NormalizeProviderCode(request.ProviderCode);

            if (request.ToUtc <= request.FromUtc)
                return Results.BadRequest(new { error = "ToUtc must be greater than FromUtc." });

            var runId = Guid.NewGuid();
            var batchSize = Math.Clamp(request.BatchSize, 1, 10000);

            var checkpointToUtc = request.SimulateKillAfterFirstBatch
                ? request.FromUtc.AddSeconds(Math.Max(60, (request.ToUtc - request.FromUtc).TotalSeconds / 2.0))
                : request.ToUtc;

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await EnsureSourceAsync(connection, tenantId, providerCode, providerCode, false, cancellationToken);

            var rowsInserted = await InsertBackfillReadingsAsync(
                connection,
                tenantId,
                providerCode,
                request.FromUtc,
                checkpointToUtc,
                batchSize,
                cancellationToken);

            var status = request.SimulateKillAfterFirstBatch ? "interrupted" : "completed";

            await UpsertCheckpointAsync(
                connection,
                tenantId,
                providerCode,
                runId,
                request.FromUtc,
                request.ToUtc,
                checkpointToUtc,
                rowsInserted,
                status,
                new
                {
                    simulatedKill = request.SimulateKillAfterFirstBatch,
                    checkpointToUtc,
                    batchSize
                },
                cancellationToken);

            await MarkReadSuccessAsync(connection, tenantId, providerCode, 0, cancellationToken);

            await WriteEventAsync(
                connection,
                tenantId,
                providerCode,
                "backfill-run",
                rowsInserted > 0 ? "passed" : "info",
                new
                {
                    runId,
                    status,
                    rowsInserted,
                    checkpointToUtc,
                    resumable = request.SimulateKillAfterFirstBatch
                },
                cancellationToken);

            return Results.Ok(new
            {
                runId,
                providerCode,
                status,
                rowsInserted,
                checkpointToUtc,
                resumable = request.SimulateKillAfterFirstBatch,
                readOnly = true
            });
        });

        group.MapPost("/backfill/resume-proof/{runId:guid}", async (
            Guid runId,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var checkpoint = await LoadCheckpointAsync(connection, tenantId, runId, cancellationToken);
            if (checkpoint is null)
                return Results.NotFound(new { error = $"Backfill run {runId} was not found." });

            if (checkpoint.Status == "completed")
            {
                return Results.Ok(new
                {
                    runId,
                    checkpoint.ProviderCode,
                    status = "completed",
                    rowsInserted = 0,
                    message = "Run was already completed."
                });
            }

            var rowsInserted = await InsertBackfillReadingsAsync(
                connection,
                tenantId,
                checkpoint.ProviderCode,
                checkpoint.CheckpointToUtc,
                checkpoint.WindowEndUtc,
                10000,
                cancellationToken);

            await UpsertCheckpointAsync(
                connection,
                tenantId,
                checkpoint.ProviderCode,
                runId,
                checkpoint.WindowStartUtc,
                checkpoint.WindowEndUtc,
                checkpoint.WindowEndUtc,
                checkpoint.RowsIngested + rowsInserted,
                "completed",
                new
                {
                    resumeFromUtc = checkpoint.CheckpointToUtc,
                    resumeToUtc = checkpoint.WindowEndUtc,
                    rowsInserted,
                    previousRows = checkpoint.RowsIngested
                },
                cancellationToken);

            await MarkReadSuccessAsync(connection, tenantId, checkpoint.ProviderCode, 0, cancellationToken);

            await WriteEventAsync(
                connection,
                tenantId,
                checkpoint.ProviderCode,
                "backfill-resume-proof",
                "passed",
                new
                {
                    runId,
                    rowsInserted,
                    totalRows = checkpoint.RowsIngested + rowsInserted,
                    checkpointAdvancedTo = checkpoint.WindowEndUtc
                },
                cancellationToken);

            return Results.Ok(new
            {
                runId,
                checkpoint.ProviderCode,
                status = "completed",
                rowsInserted,
                totalRows = checkpoint.RowsIngested + rowsInserted,
                checkpointToUtc = checkpoint.WindowEndUtc,
                resumeProof = true
            });
        });

        group.MapPost("/truth-state", async (
            [FromBody] UpdateTruthStateRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var providerCode = NormalizeProviderCode(request.ProviderCode);
            var normalizedStatus = NormalizeStatus(request.Status, request.IsReachable, request.IsCertified, request.LagSeconds);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await EnsureSourceAsync(connection, tenantId, providerCode, providerCode, request.IsCertified, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE public.ppiq_connector_runtime_sources
                SET
                    is_reachable = @is_reachable,
                    is_certified = @is_certified,
                    lag_seconds = @lag_seconds,
                    status = @status,
                    certification_scope = CASE
                        WHEN @is_certified THEN 'certified-proof'
                        ELSE 'runtime-certification-proof'
                    END,
                    last_error = @notes,
                    updated_at_utc = now()
                WHERE tenant_id = @tenant_id
                  AND provider_code = @provider_code
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("provider_code", providerCode);
            cmd.Parameters.AddWithValue("is_reachable", request.IsReachable);
            cmd.Parameters.AddWithValue("is_certified", request.IsCertified);
            cmd.Parameters.AddWithValue("lag_seconds", Math.Max(0, request.LagSeconds));
            cmd.Parameters.AddWithValue("status", normalizedStatus);
            cmd.Parameters.AddWithValue("notes", string.IsNullOrWhiteSpace(request.Notes) ? (object)DBNull.Value : request.Notes.Trim());

            await cmd.ExecuteNonQueryAsync(cancellationToken);

            await WriteEventAsync(
                connection,
                tenantId,
                providerCode,
                "truth-state-update",
                "passed",
                new
                {
                    request.IsReachable,
                    request.IsCertified,
                    request.LagSeconds,
                    status = normalizedStatus,
                    request.Notes
                },
                cancellationToken);

            return Results.Ok(new
            {
                providerCode,
                isReachable = request.IsReachable,
                isCertified = request.IsCertified,
                lagSeconds = Math.Max(0, request.LagSeconds),
                status = normalizedStatus
            });
        });

        group.MapGet("/truth-state/{providerCode?}", async (
            string? providerCode,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();

            if (string.IsNullOrWhiteSpace(providerCode))
            {
                cmd.CommandText =
                    """
                    SELECT
                        provider_code,
                        connector_kind,
                        display_name,
                        is_read_only,
                        is_certified,
                        is_reachable,
                        lag_seconds,
                        status,
                        reading_count,
                        latest_reading_utc,
                        computed_truth_status
                    FROM public.ppiq_v_connector_runtime_truth_state
                    WHERE tenant_id = @tenant_id
                    ORDER BY provider_code
                    """;
                cmd.Parameters.AddWithValue("tenant_id", tenantId);
            }
            else
            {
                cmd.CommandText =
                    """
                    SELECT
                        provider_code,
                        connector_kind,
                        display_name,
                        is_read_only,
                        is_certified,
                        is_reachable,
                        lag_seconds,
                        status,
                        reading_count,
                        latest_reading_utc,
                        computed_truth_status
                    FROM public.ppiq_v_connector_runtime_truth_state
                    WHERE tenant_id = @tenant_id
                      AND provider_code = @provider_code
                    ORDER BY provider_code
                    """;
                cmd.Parameters.AddWithValue("tenant_id", tenantId);
                cmd.Parameters.AddWithValue("provider_code", NormalizeProviderCode(providerCode));
            }

            var rows = new List<object>();

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new
                {
                    providerCode = reader.GetString(0),
                    connectorKind = reader.GetString(1),
                    displayName = reader.GetString(2),
                    readOnly = reader.GetBoolean(3),
                    certified = reader.GetBoolean(4),
                    reachable = reader.GetBoolean(5),
                    lagSeconds = reader.GetInt32(6),
                    status = reader.GetString(7),
                    readingCount = reader.GetInt64(8),
                    latestReadingUtc = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    computedTruthStatus = reader.GetString(10)
                });
            }

            return Results.Ok(new
            {
                count = rows.Count,
                truthState = rows
            });
        });

        return app;
    }

    private static async Task<Guid> EnsureSourceAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string providerCode,
        string displayName,
        bool markAsCertifiedProof,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_connector_runtime_sources
            (
                tenant_id,
                provider_code,
                connector_kind,
                display_name,
                endpoint_label,
                is_read_only,
                is_certified,
                certification_scope,
                is_reachable,
                lag_seconds,
                status,
                last_successful_read_utc
            )
            VALUES
            (
                @tenant_id,
                @provider_code,
                'mock-historian',
                @display_name,
                'local deterministic mock historian',
                true,
                @is_certified,
                CASE WHEN @is_certified THEN 'certified-proof' ELSE 'runtime-certification-proof' END,
                true,
                0,
                CASE WHEN @is_certified THEN 'certified-proof' ELSE 'ready' END,
                now()
            )
            ON CONFLICT (tenant_id, provider_code)
            DO UPDATE SET
                display_name = EXCLUDED.display_name,
                is_read_only = true,
                is_certified = public.ppiq_connector_runtime_sources.is_certified OR EXCLUDED.is_certified,
                is_reachable = true,
                lag_seconds = 0,
                status = CASE
                    WHEN public.ppiq_connector_runtime_sources.is_certified OR EXCLUDED.is_certified THEN 'certified-proof'
                    ELSE 'ready'
                END,
                last_successful_read_utc = now(),
                updated_at_utc = now()
            RETURNING id
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("provider_code", providerCode);
        cmd.Parameters.AddWithValue("display_name", displayName);
        cmd.Parameters.AddWithValue("is_certified", markAsCertifiedProof);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is Guid id ? id : Guid.Parse(result!.ToString()!);
    }

    private static async Task EnsureSeedReadingsAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string providerCode,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        await InsertBackfillReadingsAsync(
            connection,
            tenantId,
            providerCode,
            fromUtc,
            toUtc,
            5000,
            cancellationToken);
    }

    private static async Task<int> InsertBackfillReadingsAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string providerCode,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int maxRows,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            WITH source_row AS
            (
                SELECT id, tenant_id, provider_code
                FROM public.ppiq_connector_runtime_sources
                WHERE tenant_id = @tenant_id
                  AND provider_code = @provider_code
                LIMIT 1
            ),
            series AS
            (
                SELECT generate_series(
                    @from_utc::timestamptz,
                    @to_utc::timestamptz,
                    interval '1 minute'
                ) AS observed_at_utc
                LIMIT @max_rows
            ),
            inserted AS
            (
                INSERT INTO public.ppiq_connector_runtime_readings
                (
                    tenant_id,
                    source_id,
                    provider_code,
                    tag_path,
                    observed_at_utc,
                    numeric_value,
                    quality,
                    raw_json
                )
                SELECT
                    s.tenant_id,
                    s.id,
                    s.provider_code,
                    tag.tag_path,
                    gs.observed_at_utc,
                    tag.base_value + ((extract(epoch from gs.observed_at_utc)::integer % tag.modulo)::numeric / 10.0),
                    'Good',
                    jsonb_build_object(
                        'source', 'pack-b4-runtime-backfill',
                        'readOnly', true,
                        'providerCode', s.provider_code
                    )
                FROM source_row s
                CROSS JOIN series gs
                CROSS JOIN (
                    VALUES
                        ('HSM.F1.RollForce', 1200.0::numeric, 71),
                        ('HSM.F2.RollForce', 1180.0::numeric, 67),
                        ('HSM.Exit.Temperature', 872.0::numeric, 53),
                        ('Caster.Mould.Level', 72.0::numeric, 29)
                ) AS tag(tag_path, base_value, modulo)
                ON CONFLICT (source_id, tag_path, observed_at_utc) DO NOTHING
                RETURNING 1
            )
            SELECT count(*)::integer FROM inserted
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("provider_code", providerCode);
        cmd.Parameters.AddWithValue("from_utc", fromUtc.UtcDateTime);
        cmd.Parameters.AddWithValue("to_utc", toUtc.UtcDateTime);
        cmd.Parameters.AddWithValue("max_rows", Math.Max(1, maxRows));

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private static async Task UpsertCheckpointAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string providerCode,
        Guid runId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        DateTimeOffset checkpointToUtc,
        int rowsIngested,
        string status,
        object evidence,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_connector_backfill_checkpoints
            (
                tenant_id,
                provider_code,
                run_id,
                window_start_utc,
                window_end_utc,
                checkpoint_to_utc,
                rows_ingested,
                status,
                completed_at_utc,
                evidence_json
            )
            VALUES
            (
                @tenant_id,
                @provider_code,
                @run_id,
                @window_start_utc,
                @window_end_utc,
                @checkpoint_to_utc,
                @rows_ingested,
                @status,
                CASE WHEN @status = 'completed' THEN now() ELSE NULL END,
                @evidence_json::jsonb
            )
            ON CONFLICT (tenant_id, provider_code, run_id)
            DO UPDATE SET
                checkpoint_to_utc = EXCLUDED.checkpoint_to_utc,
                rows_ingested = EXCLUDED.rows_ingested,
                status = EXCLUDED.status,
                completed_at_utc = EXCLUDED.completed_at_utc,
                evidence_json = EXCLUDED.evidence_json
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("provider_code", providerCode);
        cmd.Parameters.AddWithValue("run_id", runId);
        cmd.Parameters.AddWithValue("window_start_utc", windowStartUtc.UtcDateTime);
        cmd.Parameters.AddWithValue("window_end_utc", windowEndUtc.UtcDateTime);
        cmd.Parameters.AddWithValue("checkpoint_to_utc", checkpointToUtc.UtcDateTime);
        cmd.Parameters.AddWithValue("rows_ingested", rowsIngested);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("evidence_json", JsonSerializer.Serialize(evidence));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Checkpoint?> LoadCheckpointAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT
                provider_code,
                window_start_utc,
                window_end_utc,
                checkpoint_to_utc,
                rows_ingested,
                status
            FROM public.ppiq_connector_backfill_checkpoints
            WHERE tenant_id = @tenant_id
              AND run_id = @run_id
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("run_id", runId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new Checkpoint(
            ProviderCode: reader.GetString(0),
            WindowStartUtc: new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc)),
            WindowEndUtc: new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc)),
            CheckpointToUtc: new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc)),
            RowsIngested: reader.GetInt32(4),
            Status: reader.GetString(5));
    }

    private static async Task MarkReadSuccessAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string providerCode,
        int lagSeconds,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            UPDATE public.ppiq_connector_runtime_sources
            SET
                is_reachable = true,
                lag_seconds = @lag_seconds,
                status = CASE WHEN is_certified THEN 'certified-proof' ELSE 'ready' END,
                last_successful_read_utc = now(),
                last_error = NULL,
                updated_at_utc = now()
            WHERE tenant_id = @tenant_id
              AND provider_code = @provider_code
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("provider_code", providerCode);
        cmd.Parameters.AddWithValue("lag_seconds", Math.Max(0, lagSeconds));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WriteEventAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string providerCode,
        string eventCode,
        string eventStatus,
        object evidence,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_connector_runtime_events
            (
                tenant_id,
                provider_code,
                event_code,
                event_status,
                evidence_json
            )
            VALUES
            (
                @tenant_id,
                @provider_code,
                @event_code,
                @event_status,
                @evidence_json::jsonb
            )
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("provider_code", providerCode);
        cmd.Parameters.AddWithValue("event_code", eventCode);
        cmd.Parameters.AddWithValue("event_status", eventStatus);
        cmd.Parameters.AddWithValue("evidence_json", JsonSerializer.Serialize(evidence));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string NormalizeProviderCode(string providerCode)
    {
        return string.IsNullOrWhiteSpace(providerCode)
            ? "mock-historian-runtime"
            : providerCode.Trim().ToLowerInvariant();
    }

    private static string NormalizeStatus(string? status, bool isReachable, bool isCertified, int lagSeconds)
    {
        if (!isReachable) return "offline";
        if (lagSeconds >= 300) return "lagging";
        if (isCertified) return "certified-proof";

        var normalized = string.IsNullOrWhiteSpace(status)
            ? "ready"
            : status.Trim().ToLowerInvariant();

        return normalized switch
        {
            "ready" => "ready",
            "stale" => "stale",
            "lagging" => "lagging",
            "offline" => "offline",
            "error" => "error",
            "certified-proof" => "certified-proof",
            _ => "ready"
        };
    }

    private static Guid ResolveTenantId(HttpContext http)
    {
        var claimValue =
            http.User.FindFirst("tenant_id")?.Value ??
            http.User.FindFirst("tenantId")?.Value ??
            DefaultTenantId.ToString();

        return Guid.TryParse(claimValue, out var tenantId) ? tenantId : DefaultTenantId;
    }

    private static async Task SetTenantAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
        cmd.Parameters.AddWithValue("tenant_id", tenantId.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record Checkpoint(
        string ProviderCode,
        DateTimeOffset WindowStartUtc,
        DateTimeOffset WindowEndUtc,
        DateTimeOffset CheckpointToUtc,
        int RowsIngested,
        string Status);
}