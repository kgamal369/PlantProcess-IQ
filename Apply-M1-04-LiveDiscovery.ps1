# ================================================================================================
# Apply-M1-04-LiveDiscovery.ps1  -  M1-04 Registry/table endpoints hardening + column-aware register
# Wires the EXISTING ISchemaReader live-discovery capability (already implemented in all 4 DB
# connectors) to literal, OpenAPI-annotated REST endpoints, and adds column-subset + row-filter
# registration into source_table_dump_registry via the generic ppiq_register_dump_source function.
# Eradicates the demo-only discovery path. Idempotent; gated on dotnet build.
# ================================================================================================
$ErrorActionPreference = 'Stop'
$repo = 'C:\Workspace\PlantProcess-IQ'
$enc  = New-Object System.Text.UTF8Encoding($false)
function WriteFile($path, $text) {
    $full = Join-Path $repo $path
    $dir = Split-Path $full -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    if (Test-Path $full) { Copy-Item $full "$full.bak" -Force }
    [System.IO.File]::WriteAllText($full, $text, $enc)
    Write-Host ("  wrote " + $path) -ForegroundColor Green
}

Write-Host '[1/5] New DTOs' -ForegroundColor Cyan
$dtos = @'
// ============================================================
// FILE: Backend/PlantProcess.Application/Integration/Contracts/Dtos/SourceDiscoveryDtos.cs
// M1-04: Live source discovery + column-aware registration DTOs.
// Generic across all ISchemaReader providers (PostgreSql, MySql, Oracle, MsSql).
// ============================================================
namespace PlantProcess.Application.Integration.Contracts.Dtos;

public sealed record SourceTableDto(
    string SchemaName,
    string TableName,
    string Kind);

public sealed record SourceColumnDto(
    string ColumnName,
    string DataType,
    int Ordinal,
    bool IsNullable,
    bool IsPrimaryKeyCandidate,
    bool IsTimestampCandidate);

public sealed record RegisterSourceTableRequest(
    string SchemaName,
    string TableName,
    IReadOnlyList<string> PrimaryKeyColumns,
    string? WatermarkColumn,
    IReadOnlyList<string>? SelectedColumns,
    string? RowFilter);

public sealed record RegisterSourceTableResult(
    string SchemaName,
    string TableName,
    int RegisteredColumnCount,
    bool WatermarkResolved,
    string Message);

'@

WriteFile 'Backend\PlantProcess.Application\Integration\Contracts\Dtos\SourceDiscoveryDtos.cs' $dtos

Write-Host '[2/5] New service partial (live discovery via ISchemaReader)' -ForegroundColor Cyan
$service = @'
// ============================================================
// FILE: Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.Discovery.030.LiveDiscovery.cs
// M1-04: Live table + column discovery over a connected source.
// Reuses the EXISTING ISchemaReader.DiscoverDatasetsAsync / DiscoverFieldsForDatasetAsync
// resolved generically via IDataSourceConnectorFactory.GetSchemaReader(providerType).
// No provider-specific code, no raw SQL: pure application-layer wiring.
// Registration (raw registry function) lives in the endpoint, matching the two-stage import pattern.
// ============================================================
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.Connectors;

public sealed partial class ConnectorConfigurationService
{
    public async Task<ApplicationResult<IReadOnlyList<SourceTableDto>>> ListSourceTablesAsync(
        Guid connectionProfileId,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => x.Id == connectionProfileId && !x.IsDeleted, cancellationToken);

        if (profile is null)
            return ApplicationResult<IReadOnlyList<SourceTableDto>>.Failure(
                ApplicationError.NotFound("Connection profile not found."));

        ISchemaReader schemaReader;
        try
        {
            schemaReader = _connectorFactory.GetSchemaReader(profile.ProviderType);
        }
        catch (NotSupportedException ex)
        {
            return ApplicationResult<IReadOnlyList<SourceTableDto>>.Failure(
                ApplicationError.Validation($"Live table discovery is not supported for provider '{profile.ProviderType}'. {ex.Message}"));
        }

        try
        {
            var datasets = await schemaReader.DiscoverDatasetsAsync(profile, cancellationToken);
            var tables = datasets
                .Select(d => new SourceTableDto(
                    SchemaName: d.SourceSchemaName ?? string.Empty,
                    TableName: d.SourceObjectName,
                    Kind: d.DatasetKind))
                .OrderBy(x => x.SchemaName)
                .ThenBy(x => x.TableName)
                .ToList();

            return ApplicationResult<IReadOnlyList<SourceTableDto>>.Success(tables);
        }
        catch (Exception ex)
        {
            return ApplicationResult<IReadOnlyList<SourceTableDto>>.Failure(
                ApplicationError.Validation($"Live table discovery failed: {ex.Message}"));
        }
    }

    public async Task<ApplicationResult<IReadOnlyList<SourceColumnDto>>> ListSourceColumnsAsync(
        Guid connectionProfileId,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return ApplicationResult<IReadOnlyList<SourceColumnDto>>.Failure(
                ApplicationError.Validation("Table name is required."));

        var profile = await _dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => x.Id == connectionProfileId && !x.IsDeleted, cancellationToken);

        if (profile is null)
            return ApplicationResult<IReadOnlyList<SourceColumnDto>>.Failure(
                ApplicationError.NotFound("Connection profile not found."));

        ISchemaReader schemaReader;
        try
        {
            schemaReader = _connectorFactory.GetSchemaReader(profile.ProviderType);
        }
        catch (NotSupportedException ex)
        {
            return ApplicationResult<IReadOnlyList<SourceColumnDto>>.Failure(
                ApplicationError.Validation($"Column discovery is not supported for provider '{profile.ProviderType}'. {ex.Message}"));
        }

        var datasetDefinition = new SourceDatasetDefinition(
            connectionProfileId: profile.Id,
            datasetCode: "DISCOVERY_PROBE",
            datasetName: $"{schemaName}.{tableName}",
            datasetKind: "LiveProbe",
            sourceObjectName: tableName,
            isSynthetic: false,
            sourceSchemaName: string.IsNullOrWhiteSpace(schemaName) ? null : schemaName,
            primaryTimestampField: null,
            incrementalCursorField: null,
            refreshIntervalSeconds: 300,
            datasetOptionsJson: null,
            description: null,
            sourceSystem: null,
            sourceRecordId: null);

        try
        {
            var fields = await schemaReader.DiscoverFieldsForDatasetAsync(profile, datasetDefinition, cancellationToken);
            var columns = fields
                .OrderBy(f => f.Ordinal)
                .Select(f => new SourceColumnDto(
                    ColumnName: f.FieldName,
                    DataType: f.SourceDataType,
                    Ordinal: f.Ordinal,
                    IsNullable: f.IsNullable,
                    IsPrimaryKeyCandidate: f.IsPrimaryKeyCandidate,
                    IsTimestampCandidate: f.IsTimestampCandidate))
                .ToList();

            return ApplicationResult<IReadOnlyList<SourceColumnDto>>.Success(columns);
        }
        catch (Exception ex)
        {
            return ApplicationResult<IReadOnlyList<SourceColumnDto>>.Failure(
                ApplicationError.Validation($"Column discovery failed: {ex.Message}"));
        }
    }
}

'@

WriteFile 'Backend\PlantProcess.Application\Integration\Services\Connectors\ConnectorConfigurationService.Discovery.030.LiveDiscovery.cs' $service

Write-Host '[3/5] Patch IConnectorConfigurationService (append 2 discovery methods)' -ForegroundColor Cyan
$ifaceMethods = @'

    Task<ApplicationResult<IReadOnlyList<SourceTableDto>>> ListSourceTablesAsync(
        Guid connectionProfileId,
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyList<SourceColumnDto>>> ListSourceColumnsAsync(
        Guid connectionProfileId,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken);

'@

$ifacePath = Join-Path $repo 'Backend\PlantProcess.Application\Integration\Interfaces\Connectors\IConnectorConfigurationService.cs'
$ifaceText = [System.IO.File]::ReadAllText($ifacePath)
if ($ifaceText -notmatch 'ListSourceTablesAsync') {
    Copy-Item $ifacePath "$ifacePath.bak" -Force
    $lastBrace = $ifaceText.LastIndexOf('}')
    $ifaceText = $ifaceText.Substring(0, $lastBrace) + $ifaceMethods + "`n}`n"
    [System.IO.File]::WriteAllText($ifacePath, $ifaceText, $enc)
    Write-Host '  interface patched' -ForegroundColor Green
} else { Write-Host '  interface already patched (skip)' -ForegroundColor Yellow }

Write-Host '[4/5] Patch ConnectorAdminEndpoints (routes + handlers)' -ForegroundColor Cyan
$routes = @'

        group.MapGet("/connection-profiles/{id:guid}/tables", ListSourceTablesAsync)
            .WithSummary("List live tables/views from the connected source (generic, all DB providers)");

        group.MapGet("/connection-profiles/{id:guid}/tables/{schema}/{table}/columns", ListSourceColumnsAsync)
            .WithSummary("List live columns for a source table (generic)");

        group.MapPost("/connection-profiles/{id:guid}/register", RegisterSourceTableAsync)
            .AddEndpointFilter(new PlantProcess.Api.Observability.JobLogEndpointFilter("ConnectorRegister"))
            .WithSummary("Register a source table (with optional column subset + row filter) into the dump registry");

'@

$handlers = @'

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
                prep.CommandText = @"UPDATE public.source_table_dump_registry
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

'@

$epPath = Join-Path $repo 'Backend\PlantProcess.Api\Endpoints\Admin\ConnectorAdminEndpoints.cs'
$epText = [System.IO.File]::ReadAllText($epPath)
if ($epText -notmatch 'ListSourceTablesAsync') {
    Copy-Item $epPath "$epPath.bak" -Force
    # insert route registrations right after the /test mapping block (anchor on the test summary line)
    $anchor = '.WithSummary("Test connection profile");'
    $idx = $epText.IndexOf($anchor)
    if ($idx -lt 0) { throw 'route anchor not found' }
    $insertAt = $idx + $anchor.Length
    $epText = $epText.Substring(0, $insertAt) + "`n" + $routes + $epText.Substring($insertAt)
    # insert handlers just before the final closing brace of the class
    $lastBrace = $epText.LastIndexOf('}')
    $epText = $epText.Substring(0, $lastBrace) + $handlers + "`n}`n"
    [System.IO.File]::WriteAllText($epPath, $epText, $enc)
    Write-Host '  endpoints patched' -ForegroundColor Green
} else { Write-Host '  endpoints already patched (skip)' -ForegroundColor Yellow }

Write-Host '[5/5] dotnet build (gate)' -ForegroundColor Cyan
Push-Location $repo
dotnet build Backend\PlantProcess.Api\PlantProcess.Api.csproj -c Debug --nologo -v m
$code = $LASTEXITCODE
Pop-Location
if ($code -ne 0) {
    Write-Host 'BUILD FAILED - restoring .bak files' -ForegroundColor Red
    Get-ChildItem -Recurse $repo -Filter *.bak | ForEach-Object {
        $orig = $_.FullName.Substring(0, $_.FullName.Length-4)
        Move-Item $_.FullName $orig -Force
    }
    throw 'M1-04 build failed; reverted.'
}
Write-Host ''
Write-Host 'M1-04 APPLIED + BUILD GREEN.' -ForegroundColor Green
Write-Host 'New endpoints:' -ForegroundColor Cyan
Write-Host '  GET  /admin/connectors/connection-profiles/{id}/tables'
Write-Host '  GET  /admin/connectors/connection-profiles/{id}/tables/{schema}/{table}/columns'
Write-Host '  POST /admin/connectors/connection-profiles/{id}/register  {schemaName,tableName,primaryKeyColumns,watermarkColumn?,selectedColumns?,rowFilter?}'
Write-Host ''
Write-Host 'Verify live (Docker up, containers running):' -ForegroundColor Cyan
Write-Host '  restart API, then GET .../tables on the Meltshop profile -> should list live source tables.'
Write-Host 'Remove .bak files when satisfied:  Get-ChildItem -Recurse -Filter *.bak | Remove-Item'
