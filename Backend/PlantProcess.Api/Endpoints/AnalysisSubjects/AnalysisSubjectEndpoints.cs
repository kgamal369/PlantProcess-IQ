using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Security.Tenancy;

namespace PlantProcess.Api.Endpoints.AnalysisSubjects;

/// <summary>
/// T-231 canonical API for the frozen T-209 AnalysisSubject/Grain contract.
/// The API is intentionally generic: every grain kind uses the same SQL authority.
/// Tenant identity comes only from TenantClaims and every read/write predicate carries it.
/// </summary>
public static class AnalysisSubjectEndpoints
{
    public static IEndpointRouteBuilder MapAnalysisSubjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analysis-subjects")
            .RequireAuthorization()
            .WithTags("Analysis Subjects");

        group.MapPost("/grains", DeclareGrainAsync);
        group.MapGet("/grains/{grainCode}", GetGrainAsync);
        group.MapPost("/subjects", DeclareSubjectAsync);
        group.MapGet("/subjects/{subjectKey}", GetSubjectAsync);

        return app;
    }

    public sealed record DeclareAnalysisGrainRequest(
        string GrainCode,
        string GrainKind,
        string TimeSemantics,
        Guid IdentityDefinitionId,
        int IdentityDefinitionVersion,
        string? ParentGrainCode,
        bool IsPrimary,
        long? ExpectedCardinalityPerDay,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        Guid? CreatedBy,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record DeclareAnalysisSubjectRequest(
        Guid GrainDefinitionId,
        string SubjectKind,
        string? EntityKind,
        Guid? EntityId,
        string? SubjectKey,
        DateTime? WindowFromUtc,
        DateTime? WindowToUtc,
        JsonElement? Context,
        Guid? CreatedBy,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record AnalysisGrainResponse(
        Guid Id,
        string GrainCode,
        string GrainKind,
        string TimeSemantics,
        Guid IdentityDefinitionId,
        int IdentityDefinitionVersion,
        string? ParentGrainCode,
        bool IsPrimary,
        long? ExpectedCardinalityPerDay,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc);

    public sealed record AnalysisSubjectResponse(
        Guid SubjectId,
        Guid GrainDefinitionId,
        string SubjectKind,
        string? EntityKind,
        Guid? EntityId,
        string? SubjectKey,
        DateTime? WindowFromUtc,
        DateTime? WindowToUtc,
        JsonElement Context,
        string LineageHash,
        string GrainCode,
        string GrainKind,
        string TimeSemantics,
        Guid IdentityDefinitionId,
        int IdentityDefinitionVersion);

    private static async Task<IResult> DeclareGrainAsync(
        DeclareAnalysisGrainRequest request,
        HttpContext httpContext,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT ppiq_meta.declare_analysis_grain(
                @tenant_id, @grain_code, @grain_kind, @time_semantics,
                @definition_id, @definition_version, @parent_grain_code,
                @is_primary, @cardinality, @effective_from, @effective_to,
                @created_by, @source_system, @source_record_id);
            """, connection);

        Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
        Add(command, "grain_code", NpgsqlDbType.Text, request.GrainCode);
        Add(command, "grain_kind", NpgsqlDbType.Text, request.GrainKind);
        Add(command, "time_semantics", NpgsqlDbType.Text, request.TimeSemantics);
        Add(command, "definition_id", NpgsqlDbType.Uuid, request.IdentityDefinitionId);
        Add(command, "definition_version", NpgsqlDbType.Integer, request.IdentityDefinitionVersion);
        Add(command, "parent_grain_code", NpgsqlDbType.Text, request.ParentGrainCode);
        Add(command, "is_primary", NpgsqlDbType.Boolean, request.IsPrimary);
        Add(command, "cardinality", NpgsqlDbType.Bigint, request.ExpectedCardinalityPerDay);
        Add(command, "effective_from", NpgsqlDbType.TimestampTz, request.EffectiveFromUtc);
        Add(command, "effective_to", NpgsqlDbType.TimestampTz, request.EffectiveToUtc);
        Add(command, "created_by", NpgsqlDbType.Uuid, request.CreatedBy);
        Add(command, "source_system", NpgsqlDbType.Text, request.SourceSystem);
        Add(command, "source_record_id", NpgsqlDbType.Text, request.SourceRecordId);

        try
        {
            var id = (Guid)(await command.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("T-231 grain declaration returned no id."));
            var grain = await ReadGrainByIdAsync(connection, tenantId, id, cancellationToken);
            return grain is null ? Results.NotFound() : Results.Ok(grain);
        }
        catch (PostgresException ex)
        {
            return MapRefusal(ex);
        }
    }

    private static async Task<IResult> GetGrainAsync(
        string grainCode,
        HttpContext httpContext,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT id, grain_code, grain_kind, time_semantics,
                   identity_definition_id, identity_definition_version,
                   parent_grain_code, is_primary, expected_cardinality_per_day,
                   effective_from_utc, effective_to_utc
              FROM ppiq_meta.analysis_grain_definitions
             WHERE tenant_id = @tenant_id
               AND grain_code = btrim(@grain_code)
             ORDER BY effective_from_utc DESC
             LIMIT 1;
            """, connection);
        Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
        Add(command, "grain_code", NpgsqlDbType.Text, grainCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return Results.NotFound(new { code = "GR02 grain_not_declared" });
        }
        return Results.Ok(ReadGrain(reader));
    }

    private static async Task<IResult> DeclareSubjectAsync(
        DeclareAnalysisSubjectRequest request,
        HttpContext httpContext,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        var contextJson = request.Context.HasValue ? request.Context.Value.GetRawText() : "{}";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT ppiq_plant.declare_analysis_subject(
                @tenant_id, @grain_definition_id, @subject_kind,
                @entity_kind, @entity_id, @subject_key,
                @window_from, @window_to, @context,
                @created_by, @source_system, @source_record_id);
            """, connection);

        Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
        Add(command, "grain_definition_id", NpgsqlDbType.Uuid, request.GrainDefinitionId);
        Add(command, "subject_kind", NpgsqlDbType.Text, request.SubjectKind);
        Add(command, "entity_kind", NpgsqlDbType.Text, request.EntityKind);
        Add(command, "entity_id", NpgsqlDbType.Uuid, request.EntityId);
        Add(command, "subject_key", NpgsqlDbType.Text, request.SubjectKey);
        Add(command, "window_from", NpgsqlDbType.TimestampTz, request.WindowFromUtc);
        Add(command, "window_to", NpgsqlDbType.TimestampTz, request.WindowToUtc);
        Add(command, "context", NpgsqlDbType.Jsonb, contextJson);
        Add(command, "created_by", NpgsqlDbType.Uuid, request.CreatedBy);
        Add(command, "source_system", NpgsqlDbType.Text, request.SourceSystem);
        Add(command, "source_record_id", NpgsqlDbType.Text, request.SourceRecordId);

        try
        {
            var id = (Guid)(await command.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("T-231 subject declaration returned no id."));
            var subject = await ReadSubjectByIdAsync(connection, tenantId, id, cancellationToken);
            return subject is null ? Results.NotFound() : Results.Ok(subject);
        }
        catch (PostgresException ex)
        {
            return MapRefusal(ex);
        }
    }

    private static async Task<IResult> GetSubjectAsync(
        string subjectKey,
        HttpContext httpContext,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Resolve(httpContext.User);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT * FROM ppiq_plant.resolve_analysis_subject(@tenant_id, @subject_key);",
                connection);
            Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
            Add(command, "subject_key", NpgsqlDbType.Text, subjectKey);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return Results.NotFound(new { code = "GR01 subject_not_declared" });
            }
            return Results.Ok(ReadResolvedSubject(reader));
        }
        catch (PostgresException ex)
        {
            return MapRefusal(ex);
        }
    }

    private static async Task<AnalysisGrainResponse?> ReadGrainByIdAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT id, grain_code, grain_kind, time_semantics,
                   identity_definition_id, identity_definition_version,
                   parent_grain_code, is_primary, expected_cardinality_per_day,
                   effective_from_utc, effective_to_utc
              FROM ppiq_meta.analysis_grain_definitions
             WHERE tenant_id = @tenant_id AND id = @id;
            """, connection);
        Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
        Add(command, "id", NpgsqlDbType.Uuid, id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadGrain(reader) : null;
    }

    private static AnalysisGrainResponse ReadGrain(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetGuid(4),
        reader.GetInt32(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.GetBoolean(7),
        reader.IsDBNull(8) ? null : reader.GetInt64(8),
        reader.GetDateTime(9),
        reader.IsDBNull(10) ? null : reader.GetDateTime(10));

    private static async Task<AnalysisSubjectResponse?> ReadSubjectByIdAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT s.subject_id, s.grain_definition_id, s.subject_kind,
                   s.entity_kind, s.entity_id, s.subject_key,
                   s.window_from_utc, s.window_to_utc, s.context, s.lineage_hash,
                   g.grain_code, g.grain_kind, g.time_semantics,
                   g.identity_definition_id, g.identity_definition_version
              FROM ppiq_plant.analysis_subjects s
              JOIN ppiq_meta.analysis_grain_definitions g
                ON g.id = s.grain_definition_id AND g.tenant_id = s.tenant_id
             WHERE s.tenant_id = @tenant_id AND s.subject_id = @subject_id;
            """, connection);
        Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
        Add(command, "subject_id", NpgsqlDbType.Uuid, subjectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSubject(reader) : null;
    }

    private static AnalysisSubjectResponse ReadSubject(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetGuid(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetDateTime(6),
        reader.IsDBNull(7) ? null : reader.GetDateTime(7),
        JsonDocument.Parse(reader.GetString(8)).RootElement.Clone(),
        reader.GetString(9),
        reader.GetString(10),
        reader.GetString(11),
        reader.GetString(12),
        reader.GetGuid(13),
        reader.GetInt32(14));

    private static AnalysisSubjectResponse ReadResolvedSubject(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(2),
        reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetGuid(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetDateTime(7),
        reader.IsDBNull(8) ? null : reader.GetDateTime(8),
        JsonDocument.Parse(reader.GetString(9)).RootElement.Clone(),
        reader.GetString(10),
        reader.GetString(11),
        reader.GetString(12),
        reader.GetString(13),
        reader.GetGuid(14),
        reader.GetInt32(15));

    private static void Add(NpgsqlCommand command, string name, NpgsqlDbType type, object? value)
    {
        command.Parameters.Add(new NpgsqlParameter(name, type)
        {
            Value = value ?? DBNull.Value
        });
    }

    private static IResult MapRefusal(PostgresException ex)
    {
        var message = ex.MessageText;
        if (message.StartsWith("GR01", StringComparison.Ordinal) ||
            message.StartsWith("GR02", StringComparison.Ordinal))
        {
            return Results.NotFound(new { code = message });
        }
        if (message.StartsWith("GR06", StringComparison.Ordinal))
        {
            return Results.Conflict(new { code = message });
        }
        if (message.StartsWith("GR07", StringComparison.Ordinal))
        {
            return Results.BadRequest(new { code = message });
        }
        return Results.Problem(title: "Analysis subject/grain declaration refused", detail: message,
            statusCode: StatusCodes.Status400BadRequest);
    }
}
