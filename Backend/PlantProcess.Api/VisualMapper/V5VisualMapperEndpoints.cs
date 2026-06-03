using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using PlantProcess.Application.Integration.Security;

namespace PlantProcess.Api.VisualMapper;

public sealed record VisualMapperCreateSessionRequest(
    string SourceCode,
    string DisplayName,
    string? SourceKind,
    string? TemplateCode);

public sealed record VisualMapperMarkKeyRequest(
    Guid SessionId,
    string KeyCode,
    string DisplayName,
    Guid[] ColumnIds,
    string? NormalizationRule);

public sealed record VisualMapperJoinPreviewRequest(
    Guid SessionId,
    Guid LeftTableId,
    Guid RightTableId,
    string JoinType,
    object[] JoinColumns);

public sealed record VisualMapperSuggestionRequest(
    Guid SessionId,
    string SourceTable,
    string SourceColumn,
    string DataType);

public sealed record VisualMapperDryRunRequest(
    Guid SessionId,
    string? GeneratedSql,
    long TotalRows,
    long MappedRows,
    long NullKeyRows,
    long UnmatchedKeyRows,
    long TypeFailureRows);

public sealed record VisualMapperPublishRequest(
    Guid SessionId,
    Guid? DryRunId,
    object MappingDefinition,
    string? PublishedBy);

public static class V5VisualMapperEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static IEndpointRouteBuilder MapV5VisualMapperEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/visual-mapper")
            .WithTags("V5 Visual Mapper");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-visual-mapper",
            sourceDiscovery = true,
            businessKeys = true,
            joinPreview = true,
            canonicalSuggestion = true,
            dryRun = true,
            versionedPublish = true,
            templates = true
        }));

        group.MapGet("/templates", async (
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT template_code, display_name, source_archetype, template_definition
                FROM public.ppiq_visual_mapper_templates
                ORDER BY template_code
                """;

            var rows = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new
                {
                    templateCode = reader.GetString(0),
                    displayName = reader.GetString(1),
                    sourceArchetype = reader.GetString(2),
                    templateDefinition = reader.GetString(3)
                });
            }

            return Results.Ok(rows);
        });

        group.MapPost("/sessions", async (
            [FromBody] VisualMapperCreateSessionRequest request,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_visual_mapper_sessions
                (
                    tenant_id,
                    source_code,
                    display_name,
                    source_kind,
                    created_by
                )
                VALUES
                (
                    @tenant_id,
                    @source_code,
                    @display_name,
                    @source_kind,
                    @created_by
                )
                ON CONFLICT (tenant_id, source_code)
                DO UPDATE SET
                    display_name = EXCLUDED.display_name,
                    source_kind = EXCLUDED.source_kind,
                    updated_at_utc = now()
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("source_code", request.SourceCode.Trim());
            cmd.Parameters.AddWithValue("display_name", request.DisplayName.Trim());
            cmd.Parameters.AddWithValue("source_kind", string.IsNullOrWhiteSpace(request.SourceKind) ? "generic_relational" : request.SourceKind.Trim());
            cmd.Parameters.AddWithValue("created_by", http.User.Identity?.Name ?? "local");

            var id = (Guid)(await cmd.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);

            return Results.Ok(new
            {
                id,
                tenantId,
                request.SourceCode,
                request.DisplayName,
                request.SourceKind,
                request.TemplateCode
            });
        });

        group.MapPost("/sessions/{sessionId:guid}/discover-demo", async (
            Guid sessionId,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var tables = new[]
            {
                new
                {
                    Name = "source_materials",
                    Columns = new[]
                    {
                        new { Name = "heat_no", Type = "text", Samples = new[] { "H-3361", "H-3362" }, Key = true },
                        new { Name = "coil_no", Type = "text", Samples = new[] { "C-0044180", "C-0044181" }, Key = true },
                        new { Name = "steel_grade", Type = "text", Samples = new[] { "DX51D", "S355" }, Key = false }
                    }
                },
                new
                {
                    Name = "source_quality",
                    Columns = new[]
                    {
                        new { Name = "coil_no", Type = "text", Samples = new[] { "C-0044180", "C-0044181" }, Key = true },
                        new { Name = "defect_code", Type = "text", Samples = new[] { "EDGE_CRACK", "SCALE" }, Key = false },
                        new { Name = "severity", Type = "numeric", Samples = new[] { "0.82", "0.21" }, Key = false }
                    }
                }
            };

            foreach (var table in tables)
            {
                var tableId = await UpsertTableAsync(connection, tenantId, sessionId, table.Name, table.Columns, cancellationToken);

                for (var i = 0; i < table.Columns.Length; i++)
                {
                    await UpsertColumnAsync(connection, tenantId, tableId, table.Columns[i].Name, table.Columns[i].Type, i + 1, table.Columns[i].Samples, table.Columns[i].Key, cancellationToken);
                }
            }

            return Results.Ok(new
            {
                sessionId,
                discoveredTables = tables.Length,
                discoveredColumns = tables.Sum(x => x.Columns.Length),
                mode = "read-only-demo-introspection"
            });
        });

        group.MapPost("/business-keys", async (
            [FromBody] VisualMapperMarkKeyRequest request,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_visual_mapper_business_keys
                (
                    tenant_id,
                    session_id,
                    key_code,
                    display_name,
                    column_ids,
                    normalization_rule,
                    is_composite
                )
                VALUES
                (
                    @tenant_id,
                    @session_id,
                    @key_code,
                    @display_name,
                    @column_ids,
                    @normalization_rule,
                    @is_composite
                )
                ON CONFLICT (session_id, key_code)
                DO UPDATE SET
                    display_name = EXCLUDED.display_name,
                    column_ids = EXCLUDED.column_ids,
                    normalization_rule = EXCLUDED.normalization_rule,
                    is_composite = EXCLUDED.is_composite
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("session_id", request.SessionId);
            cmd.Parameters.AddWithValue("key_code", request.KeyCode.Trim());
            cmd.Parameters.AddWithValue("display_name", request.DisplayName.Trim());
            cmd.Parameters.AddWithValue("column_ids", request.ColumnIds);
            cmd.Parameters.AddWithValue("normalization_rule", string.IsNullOrWhiteSpace(request.NormalizationRule) ? "trim_upper" : request.NormalizationRule.Trim());
            cmd.Parameters.AddWithValue("is_composite", request.ColumnIds.Length > 1);

            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            return Results.Ok(new
            {
                id,
                request.SessionId,
                request.KeyCode,
                request.ColumnIds,
                isComposite = request.ColumnIds.Length > 1
            });
        });

        group.MapPost("/join-preview", async (
            [FromBody] VisualMapperJoinPreviewRequest request,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var previewRows = new[]
            {
                new { heat_no = "H-3361", coil_no = "C-0044180", defect_code = "EDGE_CRACK", severity = 0.82m },
                new { heat_no = "H-3362", coil_no = "C-0044181", defect_code = "SCALE", severity = 0.21m }
            };

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_visual_mapper_joins
                (
                    tenant_id,
                    session_id,
                    left_table_id,
                    right_table_id,
                    join_type,
                    join_columns,
                    preview_rows
                )
                VALUES
                (
                    @tenant_id,
                    @session_id,
                    @left_table_id,
                    @right_table_id,
                    @join_type,
                    @join_columns::jsonb,
                    @preview_rows::jsonb
                )
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("session_id", request.SessionId);
            cmd.Parameters.AddWithValue("left_table_id", request.LeftTableId);
            cmd.Parameters.AddWithValue("right_table_id", request.RightTableId);
            cmd.Parameters.AddWithValue("join_type", string.IsNullOrWhiteSpace(request.JoinType) ? "inner" : request.JoinType.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("join_columns", JsonSerializer.Serialize(request.JoinColumns));
            cmd.Parameters.AddWithValue("preview_rows", JsonSerializer.Serialize(previewRows));

            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            return Results.Ok(new
            {
                id,
                request.SessionId,
                previewRows,
                readOnly = true
            });
        });

        group.MapPost("/suggest", async (
            [FromBody] VisualMapperSuggestionRequest request,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var suggestion = SuggestCanonicalTarget(request.SourceColumn, request.DataType);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_visual_mapper_canonical_suggestions
                (
                    tenant_id,
                    session_id,
                    source_table,
                    source_column,
                    canonical_entity,
                    canonical_field,
                    confidence,
                    explanation
                )
                VALUES
                (
                    @tenant_id,
                    @session_id,
                    @source_table,
                    @source_column,
                    @canonical_entity,
                    @canonical_field,
                    @confidence,
                    @explanation
                )
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("session_id", request.SessionId);
            cmd.Parameters.AddWithValue("source_table", request.SourceTable);
            cmd.Parameters.AddWithValue("source_column", request.SourceColumn);
            cmd.Parameters.AddWithValue("canonical_entity", suggestion.Entity);
            cmd.Parameters.AddWithValue("canonical_field", suggestion.Field);
            cmd.Parameters.AddWithValue("confidence", suggestion.Confidence);
            cmd.Parameters.AddWithValue("explanation", suggestion.Explanation);

            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            return Results.Ok(new
            {
                id,
                suggestion.Entity,
                suggestion.Field,
                suggestion.Confidence,
                suggestion.Explanation
            });
        });

        group.MapPost("/dry-run", async (
            [FromBody] VisualMapperDryRunRequest request,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var sql = string.IsNullOrWhiteSpace(request.GeneratedSql)
                ? "SELECT * FROM staging_records LIMIT 200"
                : request.GeneratedSql.Trim();

            var validation = SafeSqlValidator.Validate(sql);
            var total = Math.Max(request.TotalRows, 0);
            var mapped = Math.Clamp(request.MappedRows, 0, Math.Max(total, 1));
            var coverage = total == 0 ? 0 : decimal.Round((decimal)mapped / total * 100m, 4);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_visual_mapper_dry_runs
                (
                    tenant_id,
                    session_id,
                    generated_sql,
                    safe_sql_passed,
                    total_rows,
                    mapped_rows,
                    null_key_rows,
                    unmatched_key_rows,
                    type_failure_rows,
                    coverage_percent,
                    status,
                    details
                )
                VALUES
                (
                    @tenant_id,
                    @session_id,
                    @generated_sql,
                    @safe_sql_passed,
                    @total_rows,
                    @mapped_rows,
                    @null_key_rows,
                    @unmatched_key_rows,
                    @type_failure_rows,
                    @coverage_percent,
                    @status,
                    @details::jsonb
                )
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("session_id", request.SessionId);
            cmd.Parameters.AddWithValue("generated_sql", sql);
            cmd.Parameters.AddWithValue("safe_sql_passed", validation.IsValid);
            cmd.Parameters.AddWithValue("total_rows", total);
            cmd.Parameters.AddWithValue("mapped_rows", mapped);
            cmd.Parameters.AddWithValue("null_key_rows", Math.Max(request.NullKeyRows, 0));
            cmd.Parameters.AddWithValue("unmatched_key_rows", Math.Max(request.UnmatchedKeyRows, 0));
            cmd.Parameters.AddWithValue("type_failure_rows", Math.Max(request.TypeFailureRows, 0));
            cmd.Parameters.AddWithValue("coverage_percent", coverage);
            cmd.Parameters.AddWithValue("status", validation.IsValid ? "passed" : "rejected_by_safe_sql");
            cmd.Parameters.AddWithValue("details", JsonSerializer.Serialize(new
            {
                validationErrors = validation.Errors,
                dryRunOnly = true,
                canonicalWritePerformed = false
            }));

            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            return Results.Ok(new
            {
                id,
                safeSqlPassed = validation.IsValid,
                validation.Errors,
                coveragePercent = coverage,
                canonicalWritePerformed = false
            });
        });

        group.MapPost("/publish", async (
            [FromBody] VisualMapperPublishRequest request,
            HttpContext http,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var tx = await connection.BeginTransactionAsync(cancellationToken);

            var nextVersion = await GetNextVersionAsync(connection, request.SessionId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_visual_mapper_versions
                (
                    tenant_id,
                    session_id,
                    version_number,
                    mapping_definition,
                    dry_run_id,
                    published_by
                )
                VALUES
                (
                    @tenant_id,
                    @session_id,
                    @version_number,
                    @mapping_definition::jsonb,
                    @dry_run_id,
                    @published_by
                )
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("session_id", request.SessionId);
            cmd.Parameters.AddWithValue("version_number", nextVersion);
            cmd.Parameters.AddWithValue("mapping_definition", JsonSerializer.Serialize(request.MappingDefinition));
            cmd.Parameters.AddWithValue("dry_run_id", request.DryRunId.HasValue ? request.DryRunId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("published_by", request.PublishedBy ?? http.User.Identity?.Name ?? "local");

            var versionId = (Guid)(await cmd.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);

            await using var update = connection.CreateCommand();
            update.Transaction = tx;
            update.CommandText =
                """
                UPDATE public.ppiq_visual_mapper_sessions
                SET active_version_id = @version_id,
                    status = 'published',
                    updated_at_utc = now()
                WHERE id = @session_id
                  AND tenant_id = @tenant_id
                """;
            update.Parameters.AddWithValue("version_id", versionId);
            update.Parameters.AddWithValue("session_id", request.SessionId);
            update.Parameters.AddWithValue("tenant_id", tenantId);
            await update.ExecuteNonQueryAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);

            return Results.Ok(new
            {
                versionId,
                versionNumber = nextVersion,
                immutable = true,
                rollbackSupported = true
            });
        });

        return app;
    }

    private static async Task<Guid> UpsertTableAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid sessionId,
        string tableName,
        object sample,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_visual_mapper_tables
            (
                tenant_id,
                session_id,
                table_name,
                sample_json
            )
            VALUES
            (
                @tenant_id,
                @session_id,
                @table_name,
                @sample_json::jsonb
            )
            ON CONFLICT (session_id, table_schema, table_name)
            DO UPDATE SET
                sample_json = EXCLUDED.sample_json,
                discovered_at_utc = now()
            RETURNING id
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("session_id", sessionId);
        cmd.Parameters.AddWithValue("table_name", tableName);
        cmd.Parameters.AddWithValue("sample_json", JsonSerializer.Serialize(sample));

        return (Guid)(await cmd.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
    }

    private static async Task UpsertColumnAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid tableId,
        string columnName,
        string dataType,
        int ordinal,
        string[] samples,
        bool isKey,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_visual_mapper_columns
            (
                tenant_id,
                table_id,
                column_name,
                data_type,
                ordinal_position,
                sample_values,
                is_business_key
            )
            VALUES
            (
                @tenant_id,
                @table_id,
                @column_name,
                @data_type,
                @ordinal_position,
                @sample_values::jsonb,
                @is_business_key
            )
            ON CONFLICT (table_id, column_name)
            DO UPDATE SET
                data_type = EXCLUDED.data_type,
                ordinal_position = EXCLUDED.ordinal_position,
                sample_values = EXCLUDED.sample_values,
                is_business_key = EXCLUDED.is_business_key
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("table_id", tableId);
        cmd.Parameters.AddWithValue("column_name", columnName);
        cmd.Parameters.AddWithValue("data_type", dataType);
        cmd.Parameters.AddWithValue("ordinal_position", ordinal);
        cmd.Parameters.AddWithValue("sample_values", JsonSerializer.Serialize(samples));
        cmd.Parameters.AddWithValue("is_business_key", isKey);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> GetNextVersionAsync(
        NpgsqlConnection connection,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT COALESCE(MAX(version_number), 0) + 1
            FROM public.ppiq_visual_mapper_versions
            WHERE session_id = @session_id
            """;
        cmd.Parameters.AddWithValue("session_id", sessionId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    private static (string Entity, string Field, decimal Confidence, string Explanation) SuggestCanonicalTarget(
        string sourceColumn,
        string dataType)
    {
        var name = sourceColumn.Trim().ToLowerInvariant();

        if (name.Contains("coil") || name.Contains("material"))
            return ("material_units", "material_code", 0.920000m, "Column name indicates a material / coil business identifier.");

        if (name.Contains("heat"))
            return ("material_aliases", "alias_code", 0.900000m, "Column name indicates a heat identifier that should be retained as a source alias.");

        if (name.Contains("defect"))
            return ("quality_events", "defect_code", 0.880000m, "Column name indicates a defect code or quality event classification.");

        if (name.Contains("time") || name.Contains("date") || dataType.Contains("timestamp", StringComparison.OrdinalIgnoreCase))
            return ("process_step_executions", "started_at_utc", 0.760000m, "Column type/name indicates process time.");

        if (dataType.Contains("numeric", StringComparison.OrdinalIgnoreCase) || dataType.Contains("decimal", StringComparison.OrdinalIgnoreCase))
            return ("parameter_observations", "numeric_value", 0.720000m, "Numeric column can be used as a process parameter observation.");

        return ("staging_records", "raw_json", 0.500000m, "No high-confidence canonical match; keep in raw staging until reviewed.");
    }

    private static Guid ResolveTenantId(HttpContext http)
    {
        var claimValue =
            http.User.FindFirst("tenant_id")?.Value ??
            http.User.FindFirst("tenantId")?.Value ??
            DefaultTenantId.ToString();

        return Guid.TryParse(claimValue, out var tenantId)
            ? tenantId
            : DefaultTenantId;
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
}