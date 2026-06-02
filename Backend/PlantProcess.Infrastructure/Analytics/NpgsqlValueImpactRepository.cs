using System.Text.Json;
using Npgsql;
using PlantProcess.Application.Analytics.Value;

namespace PlantProcess.Infrastructure.Analytics;

/// <summary>Persists a computed value-impact result (banded or abstained) to canon.value_impact, tenant-scoped.</summary>
public sealed class NpgsqlValueImpactRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlValueImpactRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<Guid> PersistAsync(Guid tenantId, string findingRef, string? coilId, string? defectCode, ValueImpactResult result, CancellationToken ct)
    {
        var evidence = JsonSerializer.Serialize(new
        {
            terms = result.Terms.Select(t => new
            {
                t.Name, t.InputsJson, t.Low, t.Mid, t.High,
                handle = new { kind = t.Handle.Kind.ToString(), id = t.Handle.Id, detail = t.Handle.Detail }
            }),
            abstained = result.IsAbstained,
            abstainReason = result.AbstainReason
        });

        await using var cmd = _dataSource.CreateCommand(
            "INSERT INTO canon.value_impact (tenant_id, finding_ref, coil_id, defect_code, currency, " +
            "impact_eur_low, impact_eur_mid, impact_eur_high, assumption_version, is_abstained, evidence) " +
            "VALUES (@t,@f,@coil,@defect,@ccy,@low,@mid,@high,@ver,@abstain,@ev::jsonb) RETURNING id");
        cmd.Parameters.AddWithValue("t", tenantId);
        cmd.Parameters.AddWithValue("f", findingRef);
        cmd.Parameters.AddWithValue("coil", (object?)coilId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("defect", (object?)defectCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ccy", result.Currency);
        cmd.Parameters.AddWithValue("low", result.Low);
        cmd.Parameters.AddWithValue("mid", result.Mid);
        cmd.Parameters.AddWithValue("high", result.High);
        cmd.Parameters.AddWithValue("ver", result.AssumptionVersion);
        cmd.Parameters.AddWithValue("abstain", result.IsAbstained);
        cmd.Parameters.AddWithValue("ev", evidence);
        var id = await cmd.ExecuteScalarAsync(ct);
        return id is Guid g ? g : Guid.Empty;
    }
}