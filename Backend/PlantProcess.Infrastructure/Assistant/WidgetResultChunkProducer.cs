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
    /// <summary>
    /// Matched by NpgsqlRetrievalIndex.HandleFor and by the focused-widget
    /// composition rule. One definition, in the Application layer.
    /// </summary>
    public const string SourceKind = WidgetResultEvidence.ChunkSourceKind;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IDashboardWidgetQueryService _queryService;
    private readonly IWidgetResultEvidenceWriter _evidenceWriter;

    public WidgetResultChunkProducer(
        NpgsqlDataSource dataSource,
        IDashboardWidgetQueryService queryService,
        IWidgetResultEvidenceWriter evidenceWriter)
    {
        _dataSource = dataSource;
        _queryService = queryService;
        _evidenceWriter = evidenceWriter;
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

            // PR-050-01. The reindex path keeps its empty filter context: this
            // producer genuinely executes each widget without dashboard filters,
            // so "{}" is the true context here and the evidence identity of
            // every already-written row is preserved.
            var evidenceId = await _evidenceWriter.WriteAsync(
                new WidgetResultEvidenceWriteRequest(
                    tenantId,
                    identity,
                    normalised,
                    "{}",
                    result.GeneratedAtUtc),
                ct);

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
            "FROM ppiq_meta.dashboard_widget_definitions w " +
            "JOIN ppiq_meta.dashboard_definitions d ON d.id = w.dashboard_definition_id " +
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