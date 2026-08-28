using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.Connectors;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Licensing.Interfaces;
using PlantProcess.Application.Integration.Connectors;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;
using PlantProcess.Domain.Entities.Integration;
namespace PlantProcess.Api.Endpoints.Admin;

/// <summary>
/// Phase 3 Connector Foundation API.
/// 
/// This group powers the Admin / DB Configuration page.
/// It introduces generic connection profiles, source datasets,
/// CSV schema discovery, CSV preview and CSV snapshot import.
/// </summary>
public static class ConnectorAdminEndpoints
{
    public static IEndpointRouteBuilder MapConnectorAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/connectors")
            // PPIQ-T14: license paywall - tier resolution via ILicenseService (toggle with T22 ForceTier)
            .RequireLicenseFeature(LicenseFeature.DbLinkConfiguration)
        .WithTags("Admin - Connectors")
        .RequireAuthorization("PlantProcessDataManager");

        group.MapGet("/provider-types", GetProviderTypes)
            .WithSummary("Get supported connector provider types");

        group.MapGet("/connection-profiles", GetConnectionProfilesAsync)
            .WithSummary("Get connection profiles");

        group.MapGet("/connection-profiles/{id:guid}", GetConnectionProfileByIdAsync)
            .WithSummary("Get connection profile by ID");

        group.MapPost("/connection-profiles", CreateConnectionProfileAsync)
            .WithSummary("Create connection profile");

        group.MapPut("/connection-profiles/{id:guid}", UpdateConnectionProfileAsync)
            .WithSummary("Update connection profile");

        group.MapPatch("/connection-profiles/{id:guid}/activate", ActivateConnectionProfileAsync)
            .WithSummary("Activate connection profile");

        group.MapPatch("/connection-profiles/{id:guid}/deactivate", DeactivateConnectionProfileAsync)
            .WithSummary("Deactivate connection profile");

        group.MapPost("/connection-profiles/{id:guid}/test", TestConnectionProfileAsync)
            .AddEndpointFilter(new PlantProcess.Api.Observability.JobLogEndpointFilter("ConnectorTest"))
            .WithSummary("Test connection profile");

        group.MapGet("/connection-profiles/{id:guid}/tables", ListSourceTablesAsync)
            .WithSummary("List live tables/views from the connected source (generic, all DB providers)");

        group.MapGet("/connection-profiles/{id:guid}/tables/{schema}/{table}/columns", ListSourceColumnsAsync)
            .WithSummary("List live columns for a source table (generic)");

        group.MapPost("/connection-profiles/{id:guid}/register", RegisterSourceDatasetAsync)
            .AddEndpointFilter(new PlantProcess.Api.Observability.JobLogEndpointFilter("ConnectorRegister"))
            .WithSummary("Register a source table (with optional column subset + row filter) into the dump registry");


        group.MapGet("/datasets", GetDatasetsAsync)
            .WithSummary("Get source datasets");

        group.MapPost("/datasets", CreateDatasetAsync)
            .WithSummary("Create source dataset");

        group.MapPost("/datasets/{id:guid}/discover-csv-schema", DiscoverCsvSchemaAsync)
            .WithSummary("Discover CSV schema");

        group.MapPost("/datasets/{id:guid}/preview-csv", PreviewCsvAsync)
            .WithSummary("Preview CSV rows");

        group.MapPost("/datasets/{id:guid}/import-csv-snapshot", ImportCsvSnapshotAsync)
            .WithSummary("Import CSV snapshot to raw staging records");

        return app;
    }

    private static IResult GetProviderTypes()
    {
    return Results.Ok(ConnectorProviderCatalog.GetProviderTypes());
    }
    private static async Task<IResult> GetConnectionProfilesAsync(
        Guid? sourceSystemDefinitionId,
        string? providerType,
        bool? includeInactive,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetConnectionProfilesAsync(
            sourceSystemDefinitionId,
            providerType,
            includeInactive ?? true,
            cancellationToken);

        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> GetConnectionProfileByIdAsync(
        Guid id,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetConnectionProfileByIdAsync(id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> CreateConnectionProfileAsync(
        CreateConnectionProfileRequest request,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        ILicenseService licenseService,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connectorGate = licenseService.EnsureConnectorAllowed(request.ProviderType);
        if (connectorGate.IsFailure)
            return connectorGate.ToHttpResult(() => Results.NoContent());

        var activeSourceCount = await dbContext.ConnectionProfiles
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.IsActive, cancellationToken);

        var sourceLimitGate = licenseService.EnsureSourceCountAllowed(activeSourceCount);
        if (sourceLimitGate.IsFailure)
            return sourceLimitGate.ToHttpResult(() => Results.NoContent());

        var result = await service.CreateConnectionProfileAsync(request, cancellationToken);

        return result.ToHttpResult(value =>
            Results.Created($"/admin/connectors/connection-profiles/{value.Id}", value));
    }

    private static async Task<IResult> UpdateConnectionProfileAsync(
        Guid id,
        UpdateConnectionProfileRequest request,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        PlantProcessDbContext dbContext,
        ILicenseService licenseService,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.ConnectionProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (existing is null)
            return ApplicationProblems.NotFound("Connection profile was not found.");

        var connectorGate = licenseService.EnsureConnectorAllowed(existing.ProviderType);
        if (connectorGate.IsFailure)
            return connectorGate.ToHttpResult(() => Results.NoContent());

        var result = await service.UpdateConnectionProfileAsync(id, request, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> ActivateConnectionProfileAsync(
        Guid id,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ActivateConnectionProfileAsync(id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> DeactivateConnectionProfileAsync(
        Guid id,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeactivateConnectionProfileAsync(id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> TestConnectionProfileAsync(
        Guid id,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.TestConnectionProfileAsync(id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> GetDatasetsAsync(
        Guid? connectionProfileId,
        bool? includeInactive,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetDatasetsAsync(
            connectionProfileId,
            includeInactive ?? true,
            cancellationToken);

        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> CreateDatasetAsync(
        CreateSourceDatasetDefinitionRequest request,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateDatasetAsync(request, cancellationToken);

        return result.ToHttpResult(value =>
            Results.Created($"/admin/connectors/datasets/{value.Id}", value));
    }

    private static async Task<IResult> DiscoverCsvSchemaAsync(
        Guid id,
        CsvSchemaDiscoveryRequest request,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DiscoverCsvSchemaAsync(id, request, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> PreviewCsvAsync(
        Guid id,
        CsvPreviewRequest request,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.PreviewCsvAsync(id, request, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> ImportCsvSnapshotAsync(
        Guid id,
        CsvImportSnapshotRequest request,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ImportCsvSnapshotAsync(id, request, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> ListSourceTablesAsync(
        Guid id,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListSourceTablesAsync(id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> ListSourceColumnsAsync(
        Guid id,
        string schema,
        string table,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListSourceColumnsAsync(id, schema, table, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> RegisterSourceDatasetAsync(
        Guid id,
        RegisterSourceTableRequest request,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        PlantProcess.Infrastructure.Persistence.PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TableName))
            return Results.BadRequest(new { error = "Table name is required." });
        if (request.PrimaryKeyColumns is null || request.PrimaryKeyColumns.Count == 0)
            return Results.BadRequest(new { error = "At least one primary key column is required." });

        var profileResult = await service.GetConnectionProfileByIdAsync(id, cancellationToken);
        if (profileResult.IsFailure)
            return profileResult.ToHttpResult(Results.Ok);
        var profile = profileResult.Value!;

        var schemaName = string.IsNullOrWhiteSpace(request.SchemaName)
            ? (profile.SchemaName ?? "public")
            : request.SchemaName.Trim();
        var objectName = request.TableName.Trim();
        var selected = request.SelectedColumns is { Count: > 0 } ? request.SelectedColumns : null;
        var watermark = string.IsNullOrWhiteSpace(request.WatermarkColumn) ? null : request.WatermarkColumn!.Trim();
        var optionsJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            selectedColumns = selected,
            rowFilter = request.RowFilter,
            primaryKeyColumns = request.PrimaryKeyColumns
        });

        var existing = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            dbContext.SourceDatasetDefinitions,
            x => x.ConnectionProfileId == id && x.SourceObjectName == objectName && !x.IsDeleted,
            cancellationToken);

        if (existing is null)
        {
            var datasetCode = ($"{schemaName}_{objectName}").ToUpperInvariant();
            var entity = new SourceDatasetDefinition(
                connectionProfileId: id,
                datasetCode: datasetCode,
                datasetName: objectName,
                datasetKind: "Table",
                sourceObjectName: objectName,
                isSynthetic: false,
                sourceSchemaName: schemaName,
                primaryTimestampField: watermark,
                incrementalCursorField: watermark,
                refreshIntervalSeconds: 300,
                datasetOptionsJson: optionsJson);
            entity.ScheduleNextRunImmediately();
            dbContext.SourceDatasetDefinitions.Add(entity);
        }
        else
        {
            existing.Update(
                datasetName: objectName,
                sourceObjectName: objectName,
                sourceSchemaName: schemaName,
                primaryTimestampField: watermark,
                incrementalCursorField: watermark,
                datasetOptionsJson: optionsJson,
                refreshIntervalSeconds: 300,
                description: null);
            existing.Activate();
            existing.ScheduleNextRunImmediately();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new RegisterSourceTableResult(
            schemaName, objectName,
            selected?.Count ?? 0,
            watermark is not null,
            $"Registered {schemaName}.{objectName} as an Architecture-A source dataset (DB-link import)."));
    }

    private static async Task<IResult> RegisterSourceTableAsync(
        Guid id,
        RegisterSourceTableRequest request,
        [Microsoft.AspNetCore.Mvc.FromServices] IConnectorConfigurationService service,
        PlantProcess.Infrastructure.Persistence.PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TableName))
            return Results.BadRequest(new { error = "Table name is required." });
        if (request.PrimaryKeyColumns is null || request.PrimaryKeyColumns.Count == 0)
            return Results.BadRequest(new { error = "At least one primary key column is required." });

        // Resolve the profile for provider/schema/source-system-code (via the pure-EF service list).
        var profileResult = await service.GetConnectionProfileByIdAsync(id, cancellationToken);
        if (profileResult.IsFailure)
            return profileResult.ToHttpResult(Results.Ok);
        var profile = profileResult.Value!;

        var schemaName = string.IsNullOrWhiteSpace(request.SchemaName)
            ? (profile.SchemaName ?? "public")
            : request.SchemaName.Trim();
        var sourceSystemCode = (profile.ConnectionProfileCode ?? profile.ProviderType).Trim().ToUpperInvariant();

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using (var register = connection.CreateCommand())
            {
                register.CommandText = "SELECT public.ppiq_register_dump_source(@sys, @schema, @table, @pks, @wm, 2, 30);";
                AddRegParam(register, "sys", sourceSystemCode);
                AddRegParam(register, "schema", schemaName);
                AddRegParam(register, "table", request.TableName.Trim());
                AddRegParam(register, "pks", request.PrimaryKeyColumns.ToArray());
                AddRegParam(register, "wm", (object?)request.WatermarkColumn ?? DBNull.Value);
                await register.ExecuteScalarAsync(cancellationToken);
            }

            var selected = request.SelectedColumns is { Count: > 0 } ? request.SelectedColumns : null;
            if (selected is not null || !string.IsNullOrWhiteSpace(request.RowFilter))
            {
                await using var prep = connection.CreateCommand();
                prep.CommandText = @"UPDATE ppiq_staging.source_table_dump_registry
                    SET source_columns_json = COALESCE(@cols::jsonb, source_columns_json), updated_at_utc = now()
                    WHERE source_schema_name = @schema AND source_table_name = @table AND is_deleted = false;";
                AddRegParam(prep, "schema", schemaName);
                AddRegParam(prep, "table", request.TableName.Trim());
                var colsJson = selected is null
                    ? (object)DBNull.Value
                    : System.Text.Json.JsonSerializer.Serialize(new { selectedColumns = selected, rowFilter = request.RowFilter });
                AddRegParam(prep, "cols", colsJson);
                await prep.ExecuteNonQueryAsync(cancellationToken);
            }

            return Results.Ok(new RegisterSourceTableResult(
                schemaName, request.TableName.Trim(),
                selected?.Count ?? 0,
                !string.IsNullOrWhiteSpace(request.WatermarkColumn),
                $"Registered {schemaName}.{request.TableName.Trim()}."));
        }
        catch (System.Data.Common.DbException ex)
        {
            return Results.BadRequest(new { error = $"Registration failed: {ex.Message}" });
        }
    }

    private static void AddRegParam(System.Data.Common.DbCommand command, string name, object? value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        command.Parameters.Add(p);
    }

}
