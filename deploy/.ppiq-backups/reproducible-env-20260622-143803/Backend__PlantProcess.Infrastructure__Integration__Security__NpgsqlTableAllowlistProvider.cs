// PPIQ-GENERATED (T005)
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PlantProcess.Application.Integration.Security;

namespace PlantProcess.Infrastructure.Integration.Security;

/// <summary>
/// PPIQ-T005. Allowlist = canonical tables UNION the tenant's registered source tables, read from
/// source_dataset_definitions. Fails CLOSED to canonical-only on any error (never widens on failure).
/// NOTE: adjust the column names below if your source_dataset_definitions schema differs.
/// </summary>
public sealed class NpgsqlTableAllowlistProvider : ITableAllowlistProvider
{
    private readonly string _connectionString;

    public NpgsqlTableAllowlistProvider(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("Postgres")
            ?? configuration.GetConnectionString("PlantProcess")
            ?? configuration.GetConnectionString("PlantProcessDb")
            ?? configuration["ConnectionStrings:Postgres"]
            ?? throw new InvalidOperationException("No PostgreSQL connection string found for NpgsqlTableAllowlistProvider.");
    }

    public async Task<IReadOnlySet<string>> GetAllowedTablesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var set = new HashSet<string>(CanonicalTableAllowlist.Tables, StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT DISTINCT lower(coalesce(physical_table_name, source_table_name, dataset_name)) AS t
FROM source_dataset_definitions
WHERE tenant_id = @tenant_id
  AND coalesce(physical_table_name, source_table_name, dataset_name) IS NOT NULL";
            var p = cmd.CreateParameter(); p.ParameterName = "tenant_id"; p.Value = tenantId; cmd.Parameters.Add(p);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                var name = r["t"] as string;
                if (!string.IsNullOrWhiteSpace(name)) set.Add(name!);
            }
        }
        catch { /* fail closed to canonical-only */ }
        return set;
    }
}