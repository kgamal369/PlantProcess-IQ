using System.Text.Json;

namespace PlantProcess.Infrastructure.Integration.Connectors.Common;

internal sealed record ConnectorFileOptions(
    string? CsvText,
    string? FilePath,
    string? FileName,
    string? Delimiter,
    bool? HasHeader,
    string? SheetName,
    int? MaxRowsToAnalyze);

internal static class ConnectorOptions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static ConnectorFileOptions FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
            return new ConnectorFileOptions(null, null, null, null, null, null, null);

        try
        {
            return JsonSerializer.Deserialize<ConnectorFileOptions>(json, JsonOptions)
                   ?? new ConnectorFileOptions(null, null, null, null, null, null, null);
        }
        catch
        {
            return new ConnectorFileOptions(null, null, null, null, null, null, null);
        }
    }

    public static ConnectorFileOptions Merge(
        string? connectionOptionsJson,
        string? datasetOptionsJson,
        string? requestOptionsJson)
    {
        var connection = FromJson(connectionOptionsJson);
        var dataset = FromJson(datasetOptionsJson);
        var request = FromJson(requestOptionsJson);

        return new ConnectorFileOptions(
            CsvText: First(request.CsvText, dataset.CsvText, connection.CsvText),
            FilePath: First(request.FilePath, dataset.FilePath, connection.FilePath),
            FileName: First(request.FileName, dataset.FileName, connection.FileName),
            Delimiter: First(request.Delimiter, dataset.Delimiter, connection.Delimiter),
            HasHeader: request.HasHeader ?? dataset.HasHeader ?? connection.HasHeader,
            SheetName: First(request.SheetName, dataset.SheetName, connection.SheetName),
            MaxRowsToAnalyze: request.MaxRowsToAnalyze ?? dataset.MaxRowsToAnalyze ?? connection.MaxRowsToAnalyze);
    }

    private static string? First(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
    }
}