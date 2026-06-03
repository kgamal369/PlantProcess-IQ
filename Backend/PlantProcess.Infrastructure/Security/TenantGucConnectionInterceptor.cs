using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Security.Tenancy;

namespace PlantProcess.Infrastructure.Security;

/// <summary>
/// Doctrine v5 P02:
/// Sets the PostgreSQL app.current_tenant GUC on each opened EF/Npgsql connection.
/// RLS policies read this GUC, so app-level WHERE clauses are no longer the only
/// tenant-isolation defense.
/// </summary>
public sealed class TenantGucConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<TenantGucConnectionInterceptor> _logger;

    public TenantGucConnectionInterceptor(
        ITenantAccessor tenantAccessor,
        ILogger<TenantGucConnectionInterceptor> logger)
    {
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        ApplyTenantGuc(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ApplyTenantGucAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void ApplyTenantGuc(DbConnection connection)
    {
        if (!_tenantAccessor.TryGetTenantId(out var tenantId))
            return;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "tenant_id";
        p.Value = tenantId.ToString();
        cmd.Parameters.Add(p);
        cmd.ExecuteNonQuery();

        _logger.LogDebug("PostgreSQL tenant GUC set. TenantId={TenantId}", tenantId);
    }

    private async Task ApplyTenantGucAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        if (!_tenantAccessor.TryGetTenantId(out var tenantId))
            return;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "tenant_id";
        p.Value = tenantId.ToString();
        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogDebug("PostgreSQL tenant GUC set. TenantId={TenantId}", tenantId);
    }
}