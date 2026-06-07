
using Npgsql;
using PlantProcess.Application.Analytics.Value;

namespace PlantProcess.Infrastructure.Analytics;

/// <summary>
/// PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_REPOSITORY.
/// Persists tracked realized value separately from projected value impact.
/// </summary>
public sealed class NpgsqlValueRealizationRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlValueRealizationRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand("""
            CREATE SCHEMA IF NOT EXISTS canon;

            CREATE TABLE IF NOT EXISTS canon.value_realization_ledger (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                tenant_id uuid NOT NULL,
                tracking_code text NOT NULL,
                source_recommendation_id text NULL,
                source_value_impact_id uuid NULL,
                metric_code text NOT NULL,
                currency text NOT NULL DEFAULT 'EUR',
                baseline_value numeric(18,4) NOT NULL,
                actual_value numeric(18,4) NOT NULL,
                improvement_units numeric(18,4) NOT NULL,
                potential_eur_low numeric(18,2) NOT NULL,
                potential_eur_mid numeric(18,2) NOT NULL,
                potential_eur_high numeric(18,2) NOT NULL,
                realized_eur_low numeric(18,2) NOT NULL,
                realized_eur_mid numeric(18,2) NOT NULL,
                realized_eur_high numeric(18,2) NOT NULL,
                capture_rate_mid numeric(18,4) NULL,
                roi_mid numeric(18,4) NULL,
                status text NOT NULL,
                attribution_caveat text NOT NULL,
                evidence jsonb NOT NULL DEFAULT '{}'::jsonb,
                recorded_at_utc timestamptz NOT NULL DEFAULT now(),
                recorded_by text NULL,
                CONSTRAINT ck_value_realization_realized_band CHECK (realized_eur_low <= realized_eur_mid AND realized_eur_mid <= realized_eur_high),
                CONSTRAINT ck_value_realization_potential_band CHECK (potential_eur_low <= potential_eur_mid AND potential_eur_mid <= potential_eur_high),
                CONSTRAINT ck_value_realization_currency CHECK (char_length(currency) = 3)
            );

            CREATE INDEX IF NOT EXISTS ix_value_realization_tenant_recorded
            ON canon.value_realization_ledger(tenant_id, recorded_at_utc DESC);

            CREATE INDEX IF NOT EXISTS ix_value_realization_source_value_impact
            ON canon.value_realization_ledger(source_value_impact_id);
            """);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Guid> RecordAsync(
        Guid tenantId,
        ValueRealizationResult result,
        string? actor,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);

        await using var cmd = _dataSource.CreateCommand("""
            INSERT INTO canon.value_realization_ledger (
                tenant_id,
                tracking_code,
                source_recommendation_id,
                source_value_impact_id,
                metric_code,
                currency,
                baseline_value,
                actual_value,
                improvement_units,
                potential_eur_low,
                potential_eur_mid,
                potential_eur_high,
                realized_eur_low,
                realized_eur_mid,
                realized_eur_high,
                capture_rate_mid,
                roi_mid,
                status,
                attribution_caveat,
                evidence,
                recorded_by)
            VALUES (
                @tenant_id,
                @tracking_code,
                @source_recommendation_id,
                @source_value_impact_id,
                @metric_code,
                @currency,
                @baseline_value,
                @actual_value,
                @improvement_units,
                @potential_low,
                @potential_mid,
                @potential_high,
                @realized_low,
                @realized_mid,
                @realized_high,
                @capture_rate_mid,
                @roi_mid,
                @status,
                @attribution_caveat,
                @evidence::jsonb,
                @recorded_by)
            RETURNING id;
            """);

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("tracking_code", result.TrackingCode);
        cmd.Parameters.AddWithValue("source_recommendation_id", (object?)result.SourceRecommendationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("source_value_impact_id", (object?)result.SourceValueImpactId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("metric_code", result.MetricCode);
        cmd.Parameters.AddWithValue("currency", result.Currency);
        cmd.Parameters.AddWithValue("baseline_value", result.BaselineValue);
        cmd.Parameters.AddWithValue("actual_value", result.ActualValue);
        cmd.Parameters.AddWithValue("improvement_units", result.ImprovementUnits);
        cmd.Parameters.AddWithValue("potential_low", result.PotentialLow);
        cmd.Parameters.AddWithValue("potential_mid", result.PotentialMid);
        cmd.Parameters.AddWithValue("potential_high", result.PotentialHigh);
        cmd.Parameters.AddWithValue("realized_low", result.RealizedLow);
        cmd.Parameters.AddWithValue("realized_mid", result.RealizedMid);
        cmd.Parameters.AddWithValue("realized_high", result.RealizedHigh);
        cmd.Parameters.AddWithValue("capture_rate_mid", (object?)result.CaptureRateMid ?? DBNull.Value);
        cmd.Parameters.AddWithValue("roi_mid", (object?)result.RoiMid ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", result.Status);
        cmd.Parameters.AddWithValue("attribution_caveat", result.AttributionCaveat);
        cmd.Parameters.AddWithValue("evidence", result.EvidenceJson);
        cmd.Parameters.AddWithValue("recorded_by", (object?)actor ?? DBNull.Value);

        var value = await cmd.ExecuteScalarAsync(ct);
        return value is Guid id ? id : Guid.Empty;
    }

    public async Task<IReadOnlyList<ValueRealizationLedgerEntry>> ListRecentAsync(
        Guid tenantId,
        int take,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);

        await using var cmd = _dataSource.CreateCommand("""
            SELECT
                id,
                tenant_id,
                tracking_code,
                source_recommendation_id,
                source_value_impact_id,
                metric_code,
                currency,
                realized_eur_low,
                realized_eur_mid,
                realized_eur_high,
                roi_mid,
                status,
                attribution_caveat,
                recorded_at_utc
            FROM canon.value_realization_ledger
            WHERE tenant_id = @tenant_id
            ORDER BY recorded_at_utc DESC
            LIMIT @take;
            """);

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("take", Math.Clamp(take, 1, 100));

        var result = new List<ValueRealizationLedgerEntry>();

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            result.Add(new ValueRealizationLedgerEntry(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.IsDBNull(10) ? null : reader.GetDecimal(10),
                reader.GetString(11),
                reader.GetString(12),
                reader.GetFieldValue<DateTimeOffset>(13)));
        }

        return result;
    }
}
