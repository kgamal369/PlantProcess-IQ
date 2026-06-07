using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace PlantProcess.Api.PlantConnectors;

public sealed record RegisterMockHistorianRequest(
    string ConnectorCode,
    string DisplayName,
    string[]? TagPaths);

public sealed record ReadTagValuesRequest(
    string ConnectorCode,
    string[] TagPaths,
    DateTime FromUtc,
    DateTime ToUtc,
    int MaxPointsPerTag = 50);

public sealed record BackfillConnectorRequest(
    string ConnectorCode,
    string[] TagPaths,
    DateTime FromUtc,
    DateTime ToUtc,
    int MaxPointsPerTag = 500);

public static class V5PlantConnectorEndpoints
{
    
    public static IEndpointRouteBuilder MapV5PlantConnectorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/plant-connectors")
            .WithTags("V5 Plant Connectors");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-plant-connectors",
            readOnly = true,
            mockHistorian = true,
            backfill = true,
            incrementalSync = true,
            connectorTruth = true,
            schemaDrift = true
        }));

        group.MapPost("/mock-historian/register", async (
            [FromBody] RegisterMockHistorianRequest request,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var tags = NormalizeTags(request.TagPaths);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var connectorId = await UpsertConnectorAsync(
                connection,
                tenantId,
                request.ConnectorCode,
                request.DisplayName,
                cancellationToken);

            foreach (var tag in tags)
            {
                await UpsertTagAsync(connection, tenantId, connectorId, tag, cancellationToken);
            }

            await WriteTruthSnapshotAsync(
                connection,
                tenantId,
                connectorId,
                reachable: true,
                liveState: "healthy",
                certifiedState: "mock_certified",
                tags,
                "Mock historian registered and reachable.",
                cancellationToken);

            return Results.Ok(new
            {
                connectorId,
                request.ConnectorCode,
                tagCount = tags.Length,
                readOnly = true,
                certificationStatus = "mock_certified"
            });
        });

        group.MapGet("/truth", async (
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT DISTINCT ON (c.id)
                    c.connector_code,
                    c.display_name,
                    c.connector_type,
                    c.is_read_only,
                    c.certification_status,
                    s.reachable,
                    s.live_state,
                    s.tag_count,
                    s.message,
                    s.captured_at_utc
                FROM public.ppiq_plant_connectors c
                LEFT JOIN public.ppiq_connector_truth_snapshots s ON s.connector_id = c.id
                WHERE c.tenant_id = @tenant_id
                ORDER BY c.id, s.captured_at_utc DESC NULLS LAST
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            var rows = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new
                {
                    connectorCode = reader.GetString(0),
                    displayName = reader.GetString(1),
                    connectorType = reader.GetString(2),
                    isReadOnly = reader.GetBoolean(3),
                    certificationStatus = reader.GetString(4),
                    reachable = !reader.IsDBNull(5) && reader.GetBoolean(5),
                    liveState = reader.IsDBNull(6) ? "unknown" : reader.GetString(6),
                    tagCount = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                    message = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    capturedAtUtc = reader.IsDBNull(9) ? null : reader.GetDateTime(9).ToString("O")
                });
            }

            return Results.Ok(rows);
        });

        group.MapPost("/mock-historian/read", async (
            [FromBody] ReadTagValuesRequest request,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var tags = NormalizeTags(request.TagPaths);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var connectorId = await GetConnectorIdAsync(connection, tenantId, request.ConnectorCode, cancellationToken);

            if (connectorId == Guid.Empty)
                return Results.NotFound(new { message = "Connector not found. Register it first." });

            var values = GenerateValues(tags, request.FromUtc, request.ToUtc, Math.Clamp(request.MaxPointsPerTag, 1, 1000));

            await WriteTruthSnapshotAsync(
                connection,
                tenantId,
                connectorId,
                reachable: true,
                liveState: "healthy",
                certifiedState: "mock_certified",
                tags,
                "Read-only mock historian read succeeded.",
                cancellationToken);

            return Results.Ok(new
            {
                request.ConnectorCode,
                readOnly = true,
                pointCount = values.Length,
                values
            });
        });

        group.MapPost("/mock-historian/write", () =>
            Results.Json(
                new
                {
                    rejected = true,
                    reason = "P07 read-only enforcement: write methods are not reachable for plant historian/OPC connector."
                },
                statusCode: StatusCodes.Status405MethodNotAllowed));

        group.MapPost("/backfill", async (
            [FromBody] BackfillConnectorRequest request,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var tags = NormalizeTags(request.TagPaths);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var connectorId = await GetConnectorIdAsync(connection, tenantId, request.ConnectorCode, cancellationToken);

            if (connectorId == Guid.Empty)
                return Results.NotFound(new { message = "Connector not found. Register it first." });

            var jobId = await CreateBackfillJobAsync(connection, tenantId, connectorId, tags, request.FromUtc, request.ToUtc, cancellationToken);

            var values = GenerateValues(tags, request.FromUtc, request.ToUtc, Math.Clamp(request.MaxPointsPerTag, 1, 5000));

            foreach (var point in values)
            {
                await InsertSampleAsync(connection, tenantId, connectorId, point.TagPath, point.ObservedAtUtc, point.NumericValue, point.SourceOffset, cancellationToken);
                await UpsertCheckpointAsync(connection, tenantId, connectorId, point.TagPath, point.ObservedAtUtc, point.SourceOffset, cancellationToken);
            }

            await CompleteBackfillJobAsync(connection, tenantId, jobId, values.Length, cancellationToken);

            return Results.Ok(new
            {
                jobId,
                request.ConnectorCode,
                rowsWritten = values.Length,
                checkpointed = true,
                resumable = true,
                duplicateSafe = true
            });
        });

        return app;
    }

    private sealed record MockPoint(string TagPath, DateTime ObservedAtUtc, decimal NumericValue, string SourceOffset);

    private static MockPoint[] GenerateValues(string[] tagPaths, DateTime fromUtc, DateTime toUtc, int maxPointsPerTag)
    {
        var from = EnsureUtc(fromUtc);
        var to = EnsureUtc(toUtc);

        if (to <= from)
            to = from.AddHours(1);

        var durationTicks = Math.Max(1, (to - from).Ticks);
        var points = new List<MockPoint>();

        foreach (var tag in tagPaths)
        {
            for (var i = 0; i < maxPointsPerTag; i++)
            {
                var observed = from.AddTicks(durationTicks * i / Math.Max(1, maxPointsPerTag));
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{tag}:{observed:O}"));
                var value = decimal.Round((hash[0] + hash[1] / 10.0m) % 250m, 3);
                var offset = Convert.ToHexString(hash[..8]).ToLowerInvariant();

                points.Add(new MockPoint(tag, observed, value, offset));
            }
        }

        return points.OrderBy(x => x.ObservedAtUtc).ThenBy(x => x.TagPath).ToArray();
    }

    private static string[] NormalizeTags(string[]? tags)
    {
        var normalized = (tags is { Length: > 0 } ? tags : new[]
            {
                "MILL.HSM.F1.ROLL_FORCE",
                "MILL.HSM.F2.TEMP_EXIT",
                "QUALITY.SURFACE.EDGE_CRACK_SCORE"
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0
            ? new[] { "MILL.HSM.F1.ROLL_FORCE" }
            : normalized;
    }

    private static async Task<Guid> UpsertConnectorAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string connectorCode,
        string displayName,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_plant_connectors
            (
                tenant_id,
                connector_code,
                display_name,
                connector_type,
                is_read_only,
                certification_status,
                last_success_at_utc
            )
            VALUES
            (
                @tenant_id,
                @connector_code,
                @display_name,
                'mock_historian',
                true,
                'mock_certified',
                now()
            )
            ON CONFLICT (tenant_id, connector_code)
            DO UPDATE SET
                display_name = EXCLUDED.display_name,
                last_success_at_utc = now(),
                updated_at_utc = now()
            RETURNING id
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("connector_code", connectorCode.Trim());
        cmd.Parameters.AddWithValue("display_name", displayName.Trim());

        return (Guid)(await cmd.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
    }

    private static async Task<Guid> GetConnectorIdAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string connectorCode,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id
            FROM public.ppiq_plant_connectors
            WHERE tenant_id = @tenant_id
              AND connector_code = @connector_code
              AND is_enabled = true
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("connector_code", connectorCode.Trim());

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is Guid id ? id : Guid.Empty;
    }

    private static async Task UpsertTagAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid connectorId,
        string tagPath,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_connector_tag_catalog
            (
                tenant_id,
                connector_id,
                tag_path,
                display_name,
                engineering_unit,
                data_type,
                canonical_target
            )
            VALUES
            (
                @tenant_id,
                @connector_id,
                @tag_path,
                @display_name,
                @engineering_unit,
                'numeric',
                'parameter_observations'
            )
            ON CONFLICT (connector_id, tag_path)
            DO UPDATE SET
                display_name = EXCLUDED.display_name,
                discovered_at_utc = now()
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("connector_id", connectorId);
        cmd.Parameters.AddWithValue("tag_path", tagPath);
        cmd.Parameters.AddWithValue("display_name", tagPath.Split('.').Last());
        cmd.Parameters.AddWithValue("engineering_unit", tagPath.Contains("TEMP", StringComparison.OrdinalIgnoreCase) ? "C" : "unit");

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WriteTruthSnapshotAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid connectorId,
        bool reachable,
        string liveState,
        string certifiedState,
        string[] tags,
        string message,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_connector_truth_snapshots
            (
                tenant_id,
                connector_id,
                reachable,
                live_state,
                certified_state,
                schema_hash,
                tag_count,
                last_success_at_utc,
                message
            )
            VALUES
            (
                @tenant_id,
                @connector_id,
                @reachable,
                @live_state,
                @certified_state,
                @schema_hash,
                @tag_count,
                CASE WHEN @reachable THEN now() ELSE NULL END,
                @message
            )
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("connector_id", connectorId);
        cmd.Parameters.AddWithValue("reachable", reachable);
        cmd.Parameters.AddWithValue("live_state", liveState);
        cmd.Parameters.AddWithValue("certified_state", certifiedState);
        cmd.Parameters.AddWithValue("schema_hash", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tags)))).ToLowerInvariant());
        cmd.Parameters.AddWithValue("tag_count", tags.Length);
        cmd.Parameters.AddWithValue("message", message);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid> CreateBackfillJobAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid connectorId,
        string[] tags,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_connector_backfill_jobs
            (
                tenant_id,
                connector_id,
                tag_paths,
                from_utc,
                to_utc,
                status,
                started_at_utc
            )
            VALUES
            (
                @tenant_id,
                @connector_id,
                @tag_paths::jsonb,
                @from_utc,
                @to_utc,
                'running',
                now()
            )
            RETURNING id
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("connector_id", connectorId);
        cmd.Parameters.AddWithValue("tag_paths", JsonSerializer.Serialize(tags));
        cmd.Parameters.AddWithValue("from_utc", EnsureUtc(fromUtc));
        cmd.Parameters.AddWithValue("to_utc", EnsureUtc(toUtc));

        return (Guid)(await cmd.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
    }

    private static async Task CompleteBackfillJobAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid jobId,
        int rows,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            UPDATE public.ppiq_connector_backfill_jobs
            SET status = 'completed',
                rows_read = @rows,
                rows_written = @rows,
                completed_at_utc = now(),
                checkpoint = @checkpoint
            WHERE tenant_id = @tenant_id
              AND id = @job_id
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("job_id", jobId);
        cmd.Parameters.AddWithValue("rows", rows);
        cmd.Parameters.AddWithValue("checkpoint", $"rows:{rows}");

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertSampleAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid connectorId,
        string tagPath,
        DateTime observedAtUtc,
        decimal numericValue,
        string sourceOffset,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_connector_telemetry_samples
            (
                tenant_id,
                connector_id,
                tag_path,
                observed_at_utc,
                numeric_value,
                source_offset
            )
            VALUES
            (
                @tenant_id,
                @connector_id,
                @tag_path,
                @observed_at_utc,
                @numeric_value,
                @source_offset
            )
            ON CONFLICT (connector_id, tag_path, observed_at_utc)
            DO NOTHING
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("connector_id", connectorId);
        cmd.Parameters.AddWithValue("tag_path", tagPath);
        cmd.Parameters.AddWithValue("observed_at_utc", observedAtUtc);
        cmd.Parameters.AddWithValue("numeric_value", numericValue);
        cmd.Parameters.AddWithValue("source_offset", sourceOffset);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertCheckpointAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid connectorId,
        string tagPath,
        DateTime observedAtUtc,
        string sourceOffset,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_connector_sync_checkpoints
            (
                tenant_id,
                connector_id,
                tag_path,
                last_timestamp_utc,
                last_source_offset,
                rows_synced
            )
            VALUES
            (
                @tenant_id,
                @connector_id,
                @tag_path,
                @last_timestamp_utc,
                @last_source_offset,
                1
            )
            ON CONFLICT (connector_id, tag_path)
            DO UPDATE SET
                last_timestamp_utc = GREATEST(public.ppiq_connector_sync_checkpoints.last_timestamp_utc, EXCLUDED.last_timestamp_utc),
                last_source_offset = EXCLUDED.last_source_offset,
                rows_synced = public.ppiq_connector_sync_checkpoints.rows_synced + 1,
                updated_at_utc = now()
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("connector_id", connectorId);
        cmd.Parameters.AddWithValue("tag_path", tagPath);
        cmd.Parameters.AddWithValue("last_timestamp_utc", observedAtUtc);
        cmd.Parameters.AddWithValue("last_source_offset", sourceOffset);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Guid ResolveTenantId(HttpContext http)
    {
        return PlantProcess.Api.Security.TenantClaimReader.ResolveRequiredTenantId(http);
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

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}