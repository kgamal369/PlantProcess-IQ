using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Admin;

// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_REGISTRATION_SPLIT
public static partial class GenericSchemaMappingEndpoints
{
private static async Task<IResult> RegisterCanonicalViewAsync(
        [FromBody] RegisterCanonicalViewRequest request,
        PlantProcessDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        var validation = ValidateRegisterRequest(request);
        if (validation is not null)
            return ApplicationProblems.Validation(validation);

        var physicalSchema = CleanIdentifier(request.PhysicalSchema ?? "public", "physicalSchema");
        var physicalViewName = CleanIdentifier(
            string.IsNullOrWhiteSpace(request.PhysicalViewName)
                ? $"cv_{NormalizeCode(request.ViewCode)}"
                : request.PhysicalViewName!,
            "physicalViewName");

        var selectSql = NormalizeSelectSql(request.SqlText);
        var outputSchema = await PreviewSchemaOnlyAsync(db, selectSql, cancellationToken);

        await CreateOrReplaceViewAsync(db, physicalSchema, physicalViewName, selectSql, cancellationToken);

        var id = await UpsertCatalogAsync(
            db,
            request.ViewCode,
            request.ViewName,
            request.ViewKind,
            request.TargetEntity,
            physicalSchema,
            physicalViewName,
            selectSql,
            JsonSerializer.Serialize(outputSchema),
            string.IsNullOrWhiteSpace(request.MappingJson) ? "{}" : request.MappingJson!,
            string.IsNullOrWhiteSpace(request.SourceDatasetIdsJson) ? "[]" : request.SourceDatasetIdsJson!,
            request.AttachedScopeType,
            request.AttachedScopeCode,
            request.IsSystemSeed,
            GetActor(user),
            "Registered by /admin/schema-mapping/catalog/register.",
            cancellationToken);

        var row = await GetCatalogByIdAsync(db, id, cancellationToken);
        return Results.Created($"/admin/schema-mapping/catalog/{id}", row);
    }

private static string? ValidateRegisterRequest(RegisterCanonicalViewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ViewCode))
            return "viewCode is required.";

        if (string.IsNullOrWhiteSpace(request.ViewName))
            return "viewName is required.";

        if (string.IsNullOrWhiteSpace(request.ViewKind))
            return "viewKind is required.";

        if (string.IsNullOrWhiteSpace(request.TargetEntity))
            return "targetEntity is required.";

        if (string.IsNullOrWhiteSpace(request.SqlText))
            return "sqlText is required.";

        return null;
    }

private static async Task<Guid> UpsertCatalogAsync(
        PlantProcessDbContext db,
        string viewCode,
        string viewName,
        string viewKind,
        string targetEntity,
        string physicalSchema,
        string physicalViewName,
        string sqlText,
        string outputSchemaJson,
        string mappingJson,
        string sourceDatasetIdsJson,
        string? attachedScopeType,
        string? attachedScopeCode,
        bool isSystemSeed,
        string actor,
        string message,
        CancellationToken cancellationToken)
    {
        await using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO public.canonical_schema_views
            (
                view_code,
                view_name,
                view_kind,
                target_entity,
                physical_schema,
                physical_view_name,
                sql_text,
                output_schema_json,
                mapping_json,
                source_dataset_ids,
                attached_scope_type,
                attached_scope_code,
                is_registered,
                is_approved,
                is_active,
                is_system_seed,
                last_validated_at_utc,
                last_validation_status,
                last_validation_message,
                created_by
            )
            VALUES
            (
                @view_code,
                @view_name,
                @view_kind,
                @target_entity,
                @physical_schema,
                @physical_view_name,
                @sql_text,
                CAST(@output_schema_json AS jsonb),
                CAST(@mapping_json AS jsonb),
                CAST(@source_dataset_ids AS jsonb),
                @attached_scope_type,
                @attached_scope_code,
                true,
                true,
                true,
                @is_system_seed,
                now(),
                'Success',
                @message,
                @created_by
            )
            ON CONFLICT (lower(view_code)) WHERE is_deleted = false
            DO UPDATE SET
                view_name = EXCLUDED.view_name,
                view_kind = EXCLUDED.view_kind,
                target_entity = EXCLUDED.target_entity,
                physical_schema = EXCLUDED.physical_schema,
                physical_view_name = EXCLUDED.physical_view_name,
                sql_text = EXCLUDED.sql_text,
                output_schema_json = EXCLUDED.output_schema_json,
                mapping_json = EXCLUDED.mapping_json,
                source_dataset_ids = EXCLUDED.source_dataset_ids,
                attached_scope_type = EXCLUDED.attached_scope_type,
                attached_scope_code = EXCLUDED.attached_scope_code,
                is_registered = true,
                is_approved = true,
                is_active = true,
                is_system_seed = EXCLUDED.is_system_seed,
                last_validated_at_utc = now(),
                last_validation_status = 'Success',
                last_validation_message = EXCLUDED.last_validation_message,
                updated_at_utc = now()
            RETURNING id;
            """;

        AddParameter(command, "view_code", viewCode.Trim());
        AddParameter(command, "view_name", viewName.Trim());
        AddParameter(command, "view_kind", viewKind.Trim());
        AddParameter(command, "target_entity", targetEntity.Trim());
        AddParameter(command, "physical_schema", physicalSchema.Trim());
        AddParameter(command, "physical_view_name", physicalViewName.Trim());
        AddParameter(command, "sql_text", sqlText.Trim());
        AddParameter(command, "output_schema_json", outputSchemaJson);
        AddParameter(command, "mapping_json", string.IsNullOrWhiteSpace(mappingJson) ? "{}" : mappingJson);
        AddParameter(command, "source_dataset_ids", string.IsNullOrWhiteSpace(sourceDatasetIdsJson) ? "[]" : sourceDatasetIdsJson);
        AddParameter(command, "attached_scope_type", EmptyToNull(attachedScopeType));
        AddParameter(command, "attached_scope_code", EmptyToNull(attachedScopeCode));
        AddParameter(command, "is_system_seed", isSystemSeed);
        AddParameter(command, "message", message);
        AddParameter(command, "created_by", actor);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return (Guid)value!;
    }
}
