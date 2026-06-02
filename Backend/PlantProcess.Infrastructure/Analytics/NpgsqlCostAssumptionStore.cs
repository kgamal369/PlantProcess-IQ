using System.Text.Json;
using Npgsql;
using PlantProcess.Application.Analytics.Value;

namespace PlantProcess.Infrastructure.Analytics;

/// <summary>
/// T-042: reads the active (highest-version) assumptions for a tenant and writes a new version plus a
/// before/after audit row on every edit. All queries are tenant-scoped, enforcing isolation.
/// </summary>
public sealed class NpgsqlCostAssumptionStore : ICostAssumptionStore
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlCostAssumptionStore(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    private const string SelectColumns =
        "version, currency, " +
        "cost_per_ton_low, cost_per_ton_mid, cost_per_ton_high, " +
        "downgrade_delta_low, downgrade_delta_mid, downgrade_delta_high, " +
        "scrap_cost_low, scrap_cost_mid, scrap_cost_high, " +
        "downtime_cost_min_low, downtime_cost_min_mid, downtime_cost_min_high, " +
        "grade_premium_low, grade_premium_mid, grade_premium_high, " +
        "energy_price_low, energy_price_mid, energy_price_high";

    public async Task<CostAssumptionSet?> GetActiveAsync(Guid tenantId, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(
            $"SELECT {SelectColumns} FROM canon.cost_assumption WHERE tenant_id = @t ORDER BY version DESC LIMIT 1");
        cmd.Parameters.AddWithValue("t", tenantId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return Map(reader);
    }

    public async Task<int> CreateVersionAsync(Guid tenantId, CostAssumptionSet set, string actor, CancellationToken ct)
    {
        var before = await GetActiveAsync(tenantId, ct);
        var nextVersion = (before?.Version ?? 0) + 1;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var insert = conn.CreateCommand())
        {
            insert.Transaction = tx;
            insert.CommandText =
                "INSERT INTO canon.cost_assumption (tenant_id, version, currency, " +
                "cost_per_ton_low, cost_per_ton_mid, cost_per_ton_high, " +
                "downgrade_delta_low, downgrade_delta_mid, downgrade_delta_high, " +
                "scrap_cost_low, scrap_cost_mid, scrap_cost_high, " +
                "downtime_cost_min_low, downtime_cost_min_mid, downtime_cost_min_high, " +
                "grade_premium_low, grade_premium_mid, grade_premium_high, " +
                "energy_price_low, energy_price_mid, energy_price_high, created_by) " +
                "VALUES (@t, @v, @ccy, " +
                "@cptl,@cptm,@cpth, @ddl,@ddm,@ddh, @scl,@scm,@sch, " +
                "@dcl,@dcm,@dch, @gpl,@gpm,@gph, @epl,@epm,@eph, @actor)";
            insert.Parameters.AddWithValue("t", tenantId);
            insert.Parameters.AddWithValue("v", nextVersion);
            insert.Parameters.AddWithValue("ccy", string.IsNullOrWhiteSpace(set.Currency) ? "EUR" : set.Currency);
            AddBand(insert, "cpt", set.CostPerTon);
            AddBand(insert, "dd", set.DowngradeDeltaPerTon);
            AddBand(insert, "sc", set.ScrapCostPerTon);
            AddBand(insert, "dc", set.DowntimeCostPerMin);
            AddBand(insert, "gp", set.GradePremiumPerTon);
            AddBand(insert, "ep", set.EnergyPricePerMwh);
            insert.Parameters.AddWithValue("actor", string.IsNullOrWhiteSpace(actor) ? "system" : actor);
            await insert.ExecuteNonQueryAsync(ct);
        }

        await using (var audit = conn.CreateCommand())
        {
            audit.Transaction = tx;
            audit.CommandText =
                "INSERT INTO canon.cost_assumption_audit (tenant_id, from_version, to_version, actor, before_json, after_json) " +
                "VALUES (@t, @from, @to, @actor, @before::jsonb, @after::jsonb)";
            audit.Parameters.AddWithValue("t", tenantId);
            audit.Parameters.AddWithValue("from", (object?)before?.Version ?? DBNull.Value);
            audit.Parameters.AddWithValue("to", nextVersion);
            audit.Parameters.AddWithValue("actor", string.IsNullOrWhiteSpace(actor) ? "system" : actor);
            audit.Parameters.AddWithValue("before", before is null ? "{}" : JsonSerializer.Serialize(before));
            audit.Parameters.AddWithValue("after", JsonSerializer.Serialize(set with { Version = nextVersion }));
            await audit.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return nextVersion;
    }

    private static void AddBand(NpgsqlCommand cmd, string prefix, CostBand? band)
    {
        cmd.Parameters.AddWithValue($"{prefix}l", (object?)band?.Low ?? DBNull.Value);
        cmd.Parameters.AddWithValue($"{prefix}m", (object?)band?.Mid ?? DBNull.Value);
        cmd.Parameters.AddWithValue($"{prefix}h", (object?)band?.High ?? DBNull.Value);
    }

    private static CostAssumptionSet Map(NpgsqlDataReader r)
    {
        var i = 0;
        var version = r.GetInt32(i++);
        var currency = r.GetString(i++);
        CostBand? Band()
        {
            var lo = r.IsDBNull(i) ? (decimal?)null : r.GetDecimal(i); i++;
            var md = r.IsDBNull(i) ? (decimal?)null : r.GetDecimal(i); i++;
            var hi = r.IsDBNull(i) ? (decimal?)null : r.GetDecimal(i); i++;
            return lo.HasValue && md.HasValue && hi.HasValue ? new CostBand(lo.Value, md.Value, hi.Value) : null;
        }
        return new CostAssumptionSet(version, currency, Band(), Band(), Band(), Band(), Band(), Band());
    }
}