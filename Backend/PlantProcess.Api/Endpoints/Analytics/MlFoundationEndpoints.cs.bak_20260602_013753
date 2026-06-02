using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Analytics;

public static class MlFoundationEndpoints
{
    public static IEndpointRouteBuilder MapMlFoundationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ml/foundation")
            .WithTags("ML Foundation")
            .RequireAuthorization();

        group.MapGet("/readiness", GetReadinessAsync);
        group.MapPost("/feature-store/refresh", RefreshFeatureStoreAsync);
        group.MapGet("/feature-definitions", GetFeatureDefinitionsAsync);
        group.MapGet("/outcomes", GetOutcomeDefinitionsAsync);
        group.MapPost("/compute/correlation", ComputeCorrelationAsync);
        group.MapPost("/kb/upsert", UpsertKnowledgeBaseItemAsync);
        group.MapPost("/kb/search", SearchKnowledgeBaseAsync);

        return app;
    }

    private static async Task<IResult> GetReadinessAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureMlFoundationAsync(db, cancellationToken);

        var rows = await QueryAsync(
            db,
            """
            SELECT
                (SELECT count(*) FROM public.ml_feature_definitions WHERE is_deleted = false) AS feature_definitions,
                (SELECT count(*) FROM public.ml_feature_values) AS feature_values,
                (SELECT count(*) FROM public.ml_outcome_definitions WHERE is_deleted = false) AS outcome_definitions,
                (SELECT count(*) FROM public.ml_outcome_values) AS outcome_values,
                (SELECT count(*) FROM public.ml_correlation_results_v2) AS correlation_results,
                (SELECT count(*) FROM public.ml_knowledge_base_items WHERE is_deleted = false) AS kb_items,
                EXISTS (SELECT 1 FROM pg_type WHERE typname = 'vector') AS pgvector_available;
            """,
            cancellationToken);

        return Results.Ok(new
        {
            phase = "P02",
            taskRange = "PPIQ-T209..PPIQ-T213",
            generatedAtUtc = DateTime.UtcNow,
            readiness = rows.FirstOrDefault() ?? new Dictionary<string, object?>()
        });
    }

    private static async Task<IResult> RefreshFeatureStoreAsync(
        [FromBody] RefreshFeatureStoreRequest request,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureMlFoundationAsync(db, cancellationToken);

        var windowDays = Math.Clamp(request.WindowDays <= 0 ? 90 : request.WindowDays, 1, 3650);

        var rows = await QueryAsync(
            db,
            "SELECT feature_rows, outcome_rows, run_id FROM public.ppiq_ml_refresh_feature_store(@window_days);",
            cancellationToken,
            ("window_days", windowDays));

        return Results.Ok(new
        {
            message = "Feature store refreshed from canonical schema.",
            windowDays,
            result = rows.FirstOrDefault()
        });
    }

    private static async Task<IResult> GetFeatureDefinitionsAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureMlFoundationAsync(db, cancellationToken);

        var rows = await QueryAsync(
            db,
            """
            SELECT
                feature_key,
                display_name,
                feature_group,
                grain,
                value_type,
                unit,
                formula_kind,
                genealogy_required,
                is_missingness_informative,
                version,
                status,
                metadata_json::text AS metadata_json
            FROM public.ml_feature_definitions
            WHERE is_deleted = false
            ORDER BY feature_group, feature_key;
            """,
            cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> GetOutcomeDefinitionsAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureMlFoundationAsync(db, cancellationToken);

        var rows = await QueryAsync(
            db,
            """
            SELECT
                outcome_key,
                display_name,
                outcome_group,
                grain,
                outcome_type,
                unit,
                normalization,
                taxonomy_json::text AS taxonomy_json,
                version,
                status
            FROM public.ml_outcome_definitions
            WHERE is_deleted = false
            ORDER BY outcome_group, outcome_key;
            """,
            cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> ComputeCorrelationAsync(
        [FromBody] CorrelationComputeRequest request,
        ICorrelationComputeEngine engine,
        CancellationToken cancellationToken)
    {
        var result = await engine.ComputeAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpsertKnowledgeBaseItemAsync(
        [FromBody] UpsertKnowledgeBaseItemRequest request,
        IEmbeddingProvider embeddingProvider,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureMlFoundationAsync(db, cancellationToken);

        var embedding = await embeddingProvider.EmbedAsync(
            new EmbeddingRequest($"{request.Title}\n\n{request.Body}"),
            cancellationToken);

        var idRows = await QueryAsync(
            db,
            """
            SELECT public.ppiq_ml_upsert_kb_item(
                @item_key,
                @item_type,
                @title,
                @body,
                CAST(@embedding_json AS jsonb),
                CAST(@metadata_json AS jsonb),
                @area,
                @defect_class,
                @grade,
                @line,
                @window_code,
                @q_value,
                @source_result_id
            ) AS id;
            """,
            cancellationToken,
            ("item_key", request.ItemKey),
            ("item_type", request.ItemType),
            ("title", request.Title),
            ("body", request.Body),
            ("embedding_json", JsonSerializer.Serialize(embedding.Vector)),
            ("metadata_json", request.MetadataJson ?? "{}"),
            ("area", request.Area),
            ("defect_class", request.DefectClass),
            ("grade", request.Grade),
            ("line", request.Line),
            ("window_code", request.WindowCode),
            ("q_value", request.QValue),
            ("source_result_id", request.SourceResultId));

        return Results.Ok(new
        {
            id = idRows.FirstOrDefault()?["id"],
            embedding.ProviderKey,
            embedding.ModelKey,
            dimensions = embedding.Vector.Count
        });
    }

    private static async Task<IResult> SearchKnowledgeBaseAsync(
        [FromBody] SearchKnowledgeBaseRequest request,
        IEmbeddingProvider embeddingProvider,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureMlFoundationAsync(db, cancellationToken);

        var embedding = await embeddingProvider.EmbedAsync(
            new EmbeddingRequest(request.Query),
            cancellationToken);

        var rows = await QueryAsync(
            db,
            """
            SELECT *
            FROM public.ppiq_ml_search_kb(
                CAST(@query_embedding AS jsonb),
                @area,
                @defect_class,
                @grade,
                @line,
                @limit
            );
            """,
            cancellationToken,
            ("query_embedding", JsonSerializer.Serialize(embedding.Vector)),
            ("area", request.Area),
            ("defect_class", request.DefectClass),
            ("grade", request.Grade),
            ("line", request.Line),
            ("limit", request.Limit ?? 10));

        return Results.Ok(new
        {
            embeddingProvider = embedding.ProviderKey,
            rows
        });
    }

    private static async Task EnsureMlFoundationAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            db,
            "SELECT public.ppiq_ml_seed_foundation_catalog();",
            cancellationToken);
    }

    private static async Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(
        PlantProcessDbContext db,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var connection = db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 120;

        foreach (var parameter in parameters)
            AddParameter(command, parameter.Name, parameter.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static async Task ExecuteNonQueryAsync(
        PlantProcessDbContext db,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var connection = db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 120;

        foreach (var parameter in parameters)
            AddParameter(command, parameter.Name, parameter.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    public sealed record RefreshFeatureStoreRequest(int WindowDays);

    public sealed record UpsertKnowledgeBaseItemRequest(
        string ItemKey,
        string ItemType,
        string Title,
        string Body,
        string? MetadataJson,
        string? Area,
        string? DefectClass,
        string? Grade,
        string? Line,
        string? WindowCode,
        double? QValue,
        Guid? SourceResultId);

    public sealed record SearchKnowledgeBaseRequest(
        string Query,
        string? Area,
        string? DefectClass,
        string? Grade,
        string? Line,
        int? Limit);
}