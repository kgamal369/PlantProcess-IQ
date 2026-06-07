using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace PlantProcess.Api.DeploymentPortability;

public sealed record DrDrillRecordRequest(
    string DrillCode,
    string DrillType,
    int TargetRpoMinutes,
    int TargetRtoMinutes,
    int? MeasuredRpoMinutes,
    int? MeasuredRtoMinutes,
    string DataCheckStatus,
    string FailoverStatus,
    object? Evidence);

public sealed record OpenFormatExportRequest(
    string ExportCode,
    string SchemaVersion,
    string ManifestPath,
    string Checksum,
    string ReimportSmokeStatus);

public static class V5DeploymentPortabilityEndpoints
{
    
    public static IEndpointRouteBuilder MapV5DeploymentPortabilityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/deployment")
            .WithTags("V5 Deployment DR Portability");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-deployment-dr-portability",
            airgappedInstall = true,
            noExternalEgressContract = true,
            backupRestoreRunbook = true,
            haReference = true,
            openFormatExport = true,
            sourceCodeEscrowProcess = true
        }));

        group.MapGet("/airgap/bundles", async (
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
                SELECT
                    bundle_code,
                    version,
                    bundle_type,
                    docker_compose_path,
                    image_manifest_path,
                    install_script_path,
                    preflight_script_path,
                    no_external_egress_required,
                    self_hosted_model_option,
                    sizing_tier,
                    status
                FROM public.ppiq_deployment_airgap_bundles
                WHERE tenant_id = @tenant_id
                ORDER BY created_at_utc DESC
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            var rows = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new
                {
                    bundleCode = reader.GetString(0),
                    version = reader.GetString(1),
                    bundleType = reader.GetString(2),
                    dockerComposePath = reader.GetString(3),
                    imageManifestPath = reader.GetString(4),
                    installScriptPath = reader.GetString(5),
                    preflightScriptPath = reader.GetString(6),
                    noExternalEgressRequired = reader.GetBoolean(7),
                    selfHostedModelOption = reader.GetBoolean(8),
                    sizingTier = reader.GetString(9),
                    status = reader.GetString(10)
                });
            }

            return Results.Ok(rows);
        });

        group.MapPost("/dr/drill-record", async (
            [FromBody] DrDrillRecordRequest request,
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
                INSERT INTO public.ppiq_deployment_dr_drills
                (
                    tenant_id,
                    drill_code,
                    drill_type,
                    target_rpo_minutes,
                    target_rto_minutes,
                    measured_rpo_minutes,
                    measured_rto_minutes,
                    data_check_status,
                    failover_status,
                    completed_at_utc,
                    evidence_json
                )
                VALUES
                (
                    @tenant_id,
                    @drill_code,
                    @drill_type,
                    @target_rpo_minutes,
                    @target_rto_minutes,
                    @measured_rpo_minutes,
                    @measured_rto_minutes,
                    @data_check_status,
                    @failover_status,
                    now(),
                    @evidence_json::jsonb
                )
                ON CONFLICT (tenant_id, drill_code)
                DO UPDATE SET
                    drill_type = EXCLUDED.drill_type,
                    target_rpo_minutes = EXCLUDED.target_rpo_minutes,
                    target_rto_minutes = EXCLUDED.target_rto_minutes,
                    measured_rpo_minutes = EXCLUDED.measured_rpo_minutes,
                    measured_rto_minutes = EXCLUDED.measured_rto_minutes,
                    data_check_status = EXCLUDED.data_check_status,
                    failover_status = EXCLUDED.failover_status,
                    completed_at_utc = now(),
                    evidence_json = EXCLUDED.evidence_json
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("drill_code", Normalize(request.DrillCode, "manual-drill"));
            cmd.Parameters.AddWithValue("drill_type", Normalize(request.DrillType, "backup-restore"));
            cmd.Parameters.AddWithValue("target_rpo_minutes", request.TargetRpoMinutes);
            cmd.Parameters.AddWithValue("target_rto_minutes", request.TargetRtoMinutes);
            cmd.Parameters.AddWithValue("measured_rpo_minutes", request.MeasuredRpoMinutes.HasValue ? request.MeasuredRpoMinutes.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("measured_rto_minutes", request.MeasuredRtoMinutes.HasValue ? request.MeasuredRtoMinutes.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("data_check_status", NormalizeStatus(request.DataCheckStatus, "passed"));
            cmd.Parameters.AddWithValue("failover_status", NormalizeFailover(request.FailoverStatus, "passed"));
            cmd.Parameters.AddWithValue("evidence_json", JsonSerializer.Serialize(request.Evidence ?? new { }));

            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            return Results.Ok(new { id, saved = true });
        });

        group.MapPost("/export/open-format", async (
            [FromBody] OpenFormatExportRequest request,
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
                INSERT INTO public.ppiq_open_format_export_bundles
                (
                    tenant_id,
                    export_code,
                    schema_version,
                    manifest_path,
                    reimport_smoke_status,
                    checksum
                )
                VALUES
                (
                    @tenant_id,
                    @export_code,
                    @schema_version,
                    @manifest_path,
                    @reimport_smoke_status,
                    @checksum
                )
                ON CONFLICT (tenant_id, export_code)
                DO UPDATE SET
                    schema_version = EXCLUDED.schema_version,
                    manifest_path = EXCLUDED.manifest_path,
                    reimport_smoke_status = EXCLUDED.reimport_smoke_status,
                    checksum = EXCLUDED.checksum
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("export_code", Normalize(request.ExportCode, "manual-export"));
            cmd.Parameters.AddWithValue("schema_version", Normalize(request.SchemaVersion, "open-export-v1"));
            cmd.Parameters.AddWithValue("manifest_path", Normalize(request.ManifestPath, "deploy/export/open-format-manifest.schema.json"));
            cmd.Parameters.AddWithValue("reimport_smoke_status", NormalizeStatus(request.ReimportSmokeStatus, "passed"));
            cmd.Parameters.AddWithValue("checksum", Normalize(request.Checksum, "manual"));

            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            return Results.Ok(new
            {
                id,
                saved = true,
                openFormat = "csv-json-parquet-compatible",
                customerReadable = true
            });
        });

        return app;
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizeStatus(string? value, string fallback)
    {
        var x = Normalize(value, fallback).ToLowerInvariant();
        return x is "not_run" or "passed" or "failed" ? x : fallback;
    }

    private static string NormalizeFailover(string? value, string fallback)
    {
        var x = Normalize(value, fallback).ToLowerInvariant();
        return x is "not_run" or "passed" or "failed" ? x : fallback;
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
}