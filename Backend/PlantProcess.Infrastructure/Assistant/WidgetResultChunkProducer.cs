using System.Text.Json;
using Npgsql;
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Interfaces;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Infrastructure.Assistant;

/// <summary>
/// T-073. Turns real widget executions into persisted evidence, then into
/// retrieval chunks built FROM that persisted evidence.
///
/// The order is the point. The snapshot is written first and read back, and the
/// sentence is composed from what came back. A sentence composed from an
/// in-memory result with an evidence row written beside it afterwards would look
/// identical and prove nothing, because nothing would tie the words to the row.
///
/// Widget discovery is READ-ONLY over the definition tables. This producer never
/// writes a definition, never invents a widget and never names one.
/// </summary>
public sealed class WidgetResultChunkProducer
{
    /// <summary>Matched by NpgsqlRetrievalIndex.HandleFor, lower case.</summary>
    public const string SourceKind = "widgetresult";

    private readonly NpgsqlDataSource _dataSource;
    private readonly IDashboardWidgetQueryService _queryService;

    public WidgetResultChunkProducer(NpgsqlDataSource dataSource, IDashboardWidgetQueryService queryService)
    {
        _dataSource = dataSource;
        _queryService = queryService;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> BuildAsync(Guid tenantId, CancellationToken ct)
    {
        var chunks = new List<RetrievedChunk>();

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        var definitions = await DiscoverAsync(conn, ct);

        foreach (var definition in definitions)
        {
            ct.ThrowIfCancellationRequested();

            var query = new DashboardWidgetQueryDto(
                definition.WidgetType,
                definition.ChartType,
                definition.DimensionCode,
                definition.MeasureCode,
                definition.ParameterCode,
                null,
                null);

            var execution = await _queryService.ExecuteAsync(query, ct);

            // A widget this engine cannot execute contributes NOTHING. Not a
            // guess, and not an empty sentence that would read as an answer.
            if (!execution.IsSuccess || execution.Value is null) continue;

            var result = execution.Value;

            var identity = new WidgetEvidenceIdentity(
                definition.PageCode,
                definition.WidgetCode,
                definition.WidgetDefinitionId,
                result.Widget.WidgetType,
                result.Widget.ChartType,
                result.Widget.DimensionCode,
                result.Widget.MeasureCode,
                result.Widget.ParameterCode);

            var columns = result.Columns.Select(column => column.Code).ToList();
            var normalised = WidgetResultEvidence.Normalise(columns, result.Rows);

            const string filterContextJson = "{}";
            var queryFingerprint = WidgetResultEvidence.QueryFingerprint(identity, filterContextJson);
            var resultFingerprint = WidgetResultEvidence.ResultFingerprint(queryFingerprint, normalised);

            var resultJson = WidgetResultEvidenceJson.Serialize(identity, normalised);

            var evidenceId = await PersistAsync(
                conn, tenantId, identity, queryFingerprint, resultFingerprint,
                filterContextJson, normalised.ObservationCountTotal, resultJson, result.GeneratedAtUtc, ct);

            if (evidenceId is null) continue;

            // READ BACK. The sentence is composed from the persisted row, never
            // from the in-memory result above.
            var persisted = await ReadBackAsync(conn, tenantId, evidenceId.Value, ct);
            if (persisted is null) continue;

            chunks.Add(new RetrievedChunk(
                Guid.NewGuid(),
                SourceKind,
                evidenceId.Value.ToString(),
                WidgetResultEvidence.Sentence(persisted.Identity, persisted.Result),
                ProvenanceHandle.WidgetResult(evidenceId.Value.ToString()),
                0d,
                false,
                null));
        }

        return chunks;
    }

    private sealed record WidgetDefinitionRow(
        string PageCode,
        string WidgetCode,
        Guid? WidgetDefinitionId,
        string WidgetType,
        string ChartType,
        string? DimensionCode,
        string MeasureCode,
        string? ParameterCode);

    private static async Task<IReadOnlyList<WidgetDefinitionRow>> DiscoverAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        var rows = new List<WidgetDefinitionRow>();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT d.dashboard_code, w.widget_code, w.id, w.widget_type, w.chart_type, " +
            "       w.dimension_code, w.measure_code, w.parameter_code " +
            "FROM public.dashboard_widget_definitions w " +
            "JOIN public.dashboard_definitions d ON d.id = w.dashboard_definition_id " +
            "WHERE w.is_deleted = false AND w.is_active = true " +
            "  AND d.is_deleted = false AND d.is_active = true " +
            "ORDER BY d.dashboard_code, w.sort_order, w.widget_code";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new WidgetDefinitionRow(
                reader.GetString(0),
                reader.GetString(1),
                await reader.IsDBNullAsync(2, ct) ? null : reader.GetGuid(2),
                reader.GetString(3),
                reader.GetString(4),
                await reader.IsDBNullAsync(5, ct) ? null : reader.GetString(5),
                reader.GetString(6),
                await reader.IsDBNullAsync(7, ct) ? null : reader.GetString(7)));
        }

        return rows;
    }

    /// <summary>
    /// Writes the snapshot, or finds the one an earlier identical run already
    /// wrote. The unique constraint on (tenant_id, result_fingerprint) is what
    /// keeps a repeated reindex from changing the evidence identity, and the
    /// fingerprint binds the widget's semantics, so a definition change writes a
    /// new row rather than silently reusing the old one.
    ///
    /// NOTE ON THE COLUMN NAME: population_count stores the total of the result's
    /// own observationCount column, or zero when the result does not supply one.
    /// The column name predates that correction and is recorded debt, not a
    /// claim: nothing reads it as a population.
    /// </summary>
    private static async Task<Guid?> PersistAsync(
        NpgsqlConnection conn,
        Guid tenantId,
        WidgetEvidenceIdentity identity,
        string queryFingerprint,
        string resultFingerprint,
        string filterContextJson,
        int observationCountTotal,
        string resultJson,
        DateTime generatedAtUtc,
        CancellationToken ct)
    {
        await using (var insert = conn.CreateCommand())
        {
            insert.CommandText =
                "INSERT INTO canon.assistant_widget_result " +
                "(tenant_id, page_code, widget_code, widget_definition_id, query_fingerprint, " +
                " generated_at_utc, filter_context_json, population_count, result_json, result_fingerprint) " +
                "VALUES (@tenant, @page, @widget, @definition, @queryFingerprint, " +
                "        @generated, @filters::jsonb, @observations, @result::jsonb, @resultFingerprint) " +
                "ON CONFLICT (tenant_id, result_fingerprint) DO NOTHING " +
                "RETURNING id";

            insert.Parameters.AddWithValue("tenant", tenantId);
            insert.Parameters.AddWithValue("page", identity.PageCode);
            insert.Parameters.AddWithValue("widget", identity.WidgetCode);
            insert.Parameters.AddWithValue("definition", (object?)identity.WidgetDefinitionId ?? DBNull.Value);
            insert.Parameters.AddWithValue("queryFingerprint", queryFingerprint);
            insert.Parameters.AddWithValue("generated", generatedAtUtc);
            insert.Parameters.AddWithValue("filters", filterContextJson);
            insert.Parameters.AddWithValue("observations", observationCountTotal);
            insert.Parameters.AddWithValue("result", resultJson);
            insert.Parameters.AddWithValue("resultFingerprint", resultFingerprint);

            var inserted = await insert.ExecuteScalarAsync(ct);
            if (inserted is Guid insertedId) return insertedId;
        }

        await using var existing = conn.CreateCommand();
        existing.CommandText =
            "SELECT id FROM canon.assistant_widget_result " +
            "WHERE tenant_id = @tenant AND result_fingerprint = @resultFingerprint";
        existing.Parameters.AddWithValue("tenant", tenantId);
        existing.Parameters.AddWithValue("resultFingerprint", resultFingerprint);

        return await existing.ExecuteScalarAsync(ct) is Guid existingId ? existingId : null;
    }

    private static async Task<WidgetResultEvidenceSnapshot?> ReadBackAsync(
        NpgsqlConnection conn, Guid tenantId, Guid evidenceId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = WidgetResultEvidenceJson.SelectSnapshotSql;
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("id", evidenceId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return WidgetResultEvidenceJson.ReadSnapshot(reader, evidenceId);
    }
}