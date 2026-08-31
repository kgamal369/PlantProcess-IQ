using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Definitions;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Definitions;

/// <summary>
/// PPIQ T-090. Resolves the canonical tenant and owner identities a definition
/// write needs, from the authorities that already own them.
///
/// WHY THIS EXISTS. IDefinitionService.CreateAsync takes a kind and a payload
/// and nothing else - it was written when each kind kept its own storage and
/// nobody needed a tenant. The canonical store requires real tenant_id and
/// owner_id. Something has to bridge that, and it must be a LOOKUP: an earlier
/// draft hashed a tenant code into a uuid and created a second tenant-identity
/// namespace unrelated to ppiq_meta.tenants.
///
/// UNKNOWN IDENTITY REFUSES. Every method here returns null rather than a
/// fallback guid. A definition written under an invented owner would look
/// governed and be untraceable.
/// </summary>
public sealed class CanonicalIdentityResolver : ICanonicalIdentityResolver
{
    private readonly PlantProcessDbContext _db;

    public CanonicalIdentityResolver(PlantProcessDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// The tenant for a tenant code, or for the single tenant when a caller
    /// carries no code. Two tenants and no code is ambiguous, so it refuses
    /// rather than picking one.
    /// </summary>
    public async Task<Guid?> ResolveTenantAsync(string? tenantCode, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(tenantCode))
        {
            await using var byCode = new NpgsqlCommand(
                // MEASURED SCHEMA. ppiq_meta.tenants has no is_deleted column; the
                // liveness flag on this table is is_active. (W1-T090-IDENTITY-02)
                "SELECT id FROM ppiq_meta.tenants WHERE tenant_code = @code AND is_active = true LIMIT 1;",
                connection);
            Enlist(byCode);
            byCode.Parameters.AddWithValue("code", tenantCode.Trim());
            return await byCode.ExecuteScalarAsync(cancellationToken) as Guid?;
        }

        await using var single = new NpgsqlCommand(
            "SELECT id FROM ppiq_meta.tenants WHERE is_active = true LIMIT 2;", connection);
        Enlist(single);

        var found = new List<Guid>();
        await using (var reader = await single.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                found.Add(reader.GetGuid(0));
            }
        }

        return found.Count == 1 ? found[0] : null;
    }

    /// <summary>
    /// The application user behind a user name, or the permanent system account
    /// for product-owned writes such as system templates. The system account is
    /// provisioned by FirstRunProvisioningHostedService, so this reads an
    /// identity that already exists rather than creating one.
    /// </summary>
    public async Task<Guid?> ResolveOwnerAsync(string? userName, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT id FROM ppiq_meta.app_users
             WHERE is_enabled = true
               AND (@user_name IS NULL OR lower(user_name) = lower(@user_name))
             ORDER BY CASE WHEN @user_name IS NULL THEN 0 ELSE 1 END, created_at_utc
             LIMIT 1;
            """, connection);
        Enlist(command);

        // TYPED EXPLICITLY. A DBNull handed to AddWithValue travels as an
        // untyped parameter, and whether the server can infer its type from the
        // surrounding expression is not a contract worth depending on. An
        // inference failure here (42P18) would fail every caller that resolves
        // the system owner, before a single definition is written.
        command.Parameters.Add(new NpgsqlParameter("user_name", NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(userName) ? (object)DBNull.Value : userName.Trim()
        });

        return await command.ExecuteScalarAsync(cancellationToken) as Guid?;
    }

    private void Enlist(NpgsqlCommand command)
    {
        if (_db.Database.CurrentTransaction?.GetDbTransaction() is NpgsqlTransaction transaction)
        {
            command.Transaction = transaction;
        }
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }
}
