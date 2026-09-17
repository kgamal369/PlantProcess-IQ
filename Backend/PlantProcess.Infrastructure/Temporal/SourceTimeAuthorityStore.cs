// Source Time Authority store (backlog reference: T-233).
//
// The canonical read and write path for persisted time declarations. Writes go
// through ppiq_meta.declare_source_time_signal, the single database authority for
// idempotent redeclaration, conflict and overlap. Every read carries the tenant
// predicate, and LoadRegistryAsync hands consumers the kernel registry itself, so
// nothing downstream re-interprets a stored row. This Infrastructure type is also
// the production ISourceTimeAuthorityRegistryProvider injected into projection.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Analytics.Core.Kernel;
using PlantProcess.Application.Security.Tenancy;
using PlantProcess.Application.Temporal;

namespace PlantProcess.Infrastructure.Temporal;

public sealed class SourceTimeAuthorityStore : ISourceTimeAuthorityRegistryProvider
{
    private const string SelectColumns =
        "id, source_key, signal_key, time_role, offset_origin, fixed_offset_ticks, zone_key, " +
        "resolution_ticks, max_clock_skew_ticks, uncertainty_convention, effective_from_utc, effective_to_utc";

    private readonly NpgsqlDataSource _dataSource;

    public SourceTimeAuthorityStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<SourceTimeAuthorityWriteResult> DeclareAsync(
        Guid tenantId,
        SourceTimeAuthorityDeclarationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenantId == Guid.Empty)
        {
            return SourceTimeAuthorityWriteResult.Refused(
                SourceTimeCodes.SignalNotDeclared, "A tenant is required.");
        }

        if (!SourceTimeAuthorityDeclarations.TryNormalise(request, out var declaration, out var code) || declaration is null)
        {
            return SourceTimeAuthorityWriteResult.Refused(code, "The declaration is refused by the source time contract.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT ppiq_meta.declare_source_time_signal(" +
            "@tenant_id, @source_key, @signal_key, @time_role, @offset_origin, @fixed_offset_ticks, @zone_key, " +
            "@resolution_ticks, @max_clock_skew_ticks, @uncertainty_convention, @effective_from, @effective_to, " +
            "@created_by, @source_system, @source_record_id);",
            connection);

        Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
        Add(command, "source_key", NpgsqlDbType.Text, declaration.SourceKey);
        Add(command, "signal_key", NpgsqlDbType.Text, declaration.SignalKey);
        Add(command, "time_role", NpgsqlDbType.Text, declaration.Role.ToString());
        Add(command, "offset_origin", NpgsqlDbType.Text, declaration.OffsetOrigin.ToString());
        Add(command, "fixed_offset_ticks", NpgsqlDbType.Bigint, declaration.FixedOffset.Ticks);
        Add(command, "zone_key", NpgsqlDbType.Text, declaration.ZoneKey.Length == 0 ? null : declaration.ZoneKey);
        Add(command, "resolution_ticks", NpgsqlDbType.Bigint, declaration.Resolution.Ticks);
        Add(command, "max_clock_skew_ticks", NpgsqlDbType.Bigint, declaration.MaxClockSkew.Ticks);
        Add(command, "uncertainty_convention", NpgsqlDbType.Text, declaration.UncertaintyConvention.ToString());
        Add(command, "effective_from", NpgsqlDbType.TimestampTz, AsUtc(request.EffectiveFromUtc));
        Add(command, "effective_to", NpgsqlDbType.TimestampTz,
            request.EffectiveToUtc.HasValue ? AsUtc(request.EffectiveToUtc.Value) : null);
        Add(command, "created_by", NpgsqlDbType.Uuid, request.CreatedBy);
        Add(command, "source_system", NpgsqlDbType.Text, request.SourceSystem);
        Add(command, "source_record_id", NpgsqlDbType.Text, request.SourceRecordId);

        try
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is not Guid id)
            {
                throw new InvalidOperationException("The source time declaration returned no identity.");
            }

            return SourceTimeAuthorityWriteResult.Accepted(id);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        {
            return SourceTimeAuthorityWriteResult.Refused(CodeOf(ex.MessageText), ex.MessageText);
        }
    }

    /// <summary>Every declaration in force for the tenant at the given instant.</summary>
    public async Task<IReadOnlyList<PersistedTimeSignalDeclaration>> ListInForceAsync(
        Guid tenantId,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        var sql = TenantSql.AssertScoped(
            "SELECT " + SelectColumns +
            "  FROM ppiq_meta.source_time_authorities" +
            " WHERE tenant_id = @tenant_id" +
            "   AND effective_from_utc <= @as_of" +
            "   AND (effective_to_utc IS NULL OR effective_to_utc > @as_of)" +
            " ORDER BY source_key, signal_key, effective_from_utc;");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
        Add(command, "as_of", NpgsqlDbType.TimestampTz, AsUtc(asOfUtc));

        var rows = new List<PersistedTimeSignalDeclaration>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(Read(reader));
        }

        return rows;
    }

    /// <summary>The declaration in force for one signal, or null when none is declared.</summary>
    public async Task<PersistedTimeSignalDeclaration?> FindInForceAsync(
        Guid tenantId,
        string sourceKey,
        string signalKey,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        if (!DeclaredKey.TryNormalise(sourceKey, out var source) ||
            !DeclaredKey.TryNormalise(signalKey, out var signal))
        {
            return null;
        }

        var sql = TenantSql.AssertScoped(
            "SELECT " + SelectColumns +
            "  FROM ppiq_meta.source_time_authorities" +
            " WHERE tenant_id = @tenant_id" +
            "   AND source_key = @source_key" +
            "   AND signal_key = @signal_key" +
            "   AND effective_from_utc <= @as_of" +
            "   AND (effective_to_utc IS NULL OR effective_to_utc > @as_of)" +
            " ORDER BY effective_from_utc DESC" +
            " LIMIT 1;");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
        Add(command, "source_key", NpgsqlDbType.Text, source);
        Add(command, "signal_key", NpgsqlDbType.Text, signal);
        Add(command, "as_of", NpgsqlDbType.TimestampTz, AsUtc(asOfUtc));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    /// <summary>The kernel registry in force for the tenant, for time consumers.</summary>
    public async Task<SourceTimeAuthorityRegistry> LoadRegistryAsync(
        Guid tenantId,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        var rows = await ListInForceAsync(tenantId, asOfUtc, cancellationToken);
        return SourceTimeAuthorityDeclarations.ToRegistry(rows);
    }

    private static PersistedTimeSignalDeclaration Read(NpgsqlDataReader reader) =>
        SourceTimeAuthorityPersistenceRow.Read(reader);


    private static string CodeOf(string message)
    {
        var colon = message.IndexOf(':');
        return colon > 0 ? message.Substring(0, colon).Trim() : message.Trim();
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static void Add(NpgsqlCommand command, string name, NpgsqlDbType type, object? value)
    {
        command.Parameters.Add(new NpgsqlParameter(name, type)
        {
            Value = value ?? DBNull.Value
        });
    }
}