using System.Text.Json;
using Npgsql;
using PlantProcess.Application.Assistant;

namespace PlantProcess.Infrastructure.Assistant;

/// <summary>
/// T-073. Shared shape of the persisted snapshot: how it is written as JSON, how
/// it is selected, and how it is read back. The producer and the tenant-scoped
/// reader use THE SAME code, so the snapshot a citation resolves to is read
/// exactly as the snapshot the sentence was composed from.
/// </summary>
internal static class WidgetResultEvidenceJson
{
    /// <summary>
    /// Both identity and evidence identity are in the predicate. That is the
    /// tenant boundary: a handle belonging to another tenant matches no row and
    /// the caller is told the evidence is unavailable, never shown content.
    /// </summary>
    internal const string SelectSnapshotSql =
        "SELECT page_code, widget_code, widget_definition_id, query_fingerprint, result_fingerprint, " +
        "       filter_context_json, generated_at_utc, result_json " +
        "FROM canon.assistant_widget_result " +
        "WHERE tenant_id = @tenant AND id = @id";

    internal static string Serialize(WidgetEvidenceIdentity identity, NormalisedWidgetResult result)
        => JsonSerializer.Serialize(new
        {
            identity = new
            {
                pageCode = identity.PageCode,
                widgetCode = identity.WidgetCode,
                widgetType = identity.WidgetType,
                chartType = identity.ChartType,
                dimensionCode = identity.DimensionCode,
                measureCode = identity.MeasureCode,
                parameterCode = identity.ParameterCode
            },
            columns = result.Columns,
            rows = result.Rows,
            hasObservationCount = result.HasObservationCount,
            observationCountTotal = result.ObservationCountTotal
        });

    internal static WidgetResultEvidenceSnapshot ReadSnapshot(NpgsqlDataReader reader, Guid evidenceId)
    {
        var pageCode = reader.GetString(0);
        var widgetCode = reader.GetString(1);
        var definitionId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);
        var queryFingerprint = reader.GetString(3);
        var resultFingerprint = reader.GetString(4);
        var filterContextJson = reader.GetString(5);
        var generatedAtUtc = reader.GetDateTime(6);
        var resultJson = reader.GetString(7);

        using var document = JsonDocument.Parse(resultJson);
        var root = document.RootElement;

        var widgetType = string.Empty;
        var chartType = string.Empty;
        string? dimensionCode = null;
        var measureCode = string.Empty;
        string? parameterCode = null;

        if (root.TryGetProperty("identity", out var identityElement))
        {
            widgetType = StringOrEmpty(identityElement, "widgetType");
            chartType = StringOrEmpty(identityElement, "chartType");
            dimensionCode = StringOrNull(identityElement, "dimensionCode");
            measureCode = StringOrEmpty(identityElement, "measureCode");
            parameterCode = StringOrNull(identityElement, "parameterCode");
        }

        var columns = new List<string>();
        if (root.TryGetProperty("columns", out var columnsElement))
        {
            foreach (var column in columnsElement.EnumerateArray())
            {
                columns.Add(column.GetString() ?? string.Empty);
            }
        }

        var rows = new List<IReadOnlyList<string>>();
        if (root.TryGetProperty("rows", out var rowsElement))
        {
            foreach (var row in rowsElement.EnumerateArray())
            {
                var cells = new List<string>();
                foreach (var cell in row.EnumerateArray())
                {
                    cells.Add(cell.GetString() ?? string.Empty);
                }

                rows.Add(cells);
            }
        }

        var hasObservationCount =
            root.TryGetProperty("hasObservationCount", out var hasElement) &&
            hasElement.ValueKind == JsonValueKind.True;

        var observationCountTotal =
            root.TryGetProperty("observationCountTotal", out var totalElement) &&
            totalElement.TryGetInt32(out var total)
                ? total
                : 0;

        var identity = new WidgetEvidenceIdentity(
            pageCode, widgetCode, definitionId, widgetType, chartType, dimensionCode, measureCode, parameterCode);

        var result = new NormalisedWidgetResult(columns, rows, hasObservationCount, observationCountTotal);

        return new WidgetResultEvidenceSnapshot(
            evidenceId, identity, result, queryFingerprint, resultFingerprint, filterContextJson, generatedAtUtc);
    }

    private static string StringOrEmpty(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string? StringOrNull(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>
/// T-073 validation point 4. Recovers the exact snapshot behind a citation,
/// scoped by tenant AND evidence identity together.
/// </summary>
public sealed class NpgsqlWidgetResultEvidenceReader : IWidgetResultEvidenceReader
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlWidgetResultEvidenceReader(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<WidgetResultEvidenceSnapshot?> ReadAsync(Guid tenantId, Guid evidenceId, CancellationToken cancellationToken)
    {
        await using var cmd = _dataSource.CreateCommand(WidgetResultEvidenceJson.SelectSnapshotSql);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("id", evidenceId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return WidgetResultEvidenceJson.ReadSnapshot(reader, evidenceId);
    }
}