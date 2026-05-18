using System.Globalization;
using ClosedXML.Excel;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Integration.Connectors.Common;

namespace PlantProcess.Infrastructure.Integration.Connectors.Excel;

public sealed class ExcelConnector : IDataSourceConnector, ISchemaReader, IDataSourceReader
{
    private string? _lastError;

    public string ProviderType => "Excel";

    public Task<DataSourceConnectionTestResult> TestConnectionAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken)
    {
        try
        {
            var files = ResolveExcelFiles(connectionProfile).Take(5).ToList();

            if (files.Count == 0)
                return Task.FromResult(Failure("No Excel files found. Provide FileRootPath or filePath in ConnectionOptionsJson."));

            using var workbook = new XLWorkbook(files[0]);

            return Task.FromResult(Success("Excel file is readable.", new Dictionary<string, string?>
            {
                ["file"] = files[0],
                ["worksheetCount"] = workbook.Worksheets.Count.ToString(CultureInfo.InvariantCulture)
            }));
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return Task.FromResult(Failure($"Excel connection test failed: {ex.Message}"));
        }
    }

    public Task<IReadOnlyList<DiscoveredSourceDataset>> DiscoverDatasetsAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<DiscoveredSourceDataset>();

            foreach (var file in ResolveExcelFiles(connectionProfile))
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var workbook = new XLWorkbook(file);

                foreach (var sheet in workbook.Worksheets)
                {
                    var fileName = Path.GetFileName(file);
                    var datasetName = $"{Path.GetFileNameWithoutExtension(file)}_{sheet.Name}";

                    result.Add(new DiscoveredSourceDataset(
                        DatasetCode: NormalizeCode(datasetName),
                        DatasetName: datasetName,
                        DatasetKind: "ExcelSheet",
                        SourceObjectName: fileName,
                        SourceSchemaName: sheet.Name,
                        DatasetOptionsJson: System.Text.Json.JsonSerializer.Serialize(new
                        {
                            fileName,
                            filePath = file,
                            sheetName = sheet.Name,
                            hasHeader = true
                        })));
                }
            }

            return Task.FromResult<IReadOnlyList<DiscoveredSourceDataset>>(result);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return Task.FromResult<IReadOnlyList<DiscoveredSourceDataset>>(Array.Empty<DiscoveredSourceDataset>());
        }
    }

    public Task<IReadOnlyList<DiscoveredSourceField>> DiscoverFieldsForDatasetAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = ConnectorOptions.Merge(
                connectionProfile.ConnectionOptionsJson,
                datasetDefinition.DatasetOptionsJson,
                null);

            var rows = ReadExcelRows(connectionProfile, datasetDefinition, options, 200, cancellationToken);
            if (rows.Count == 0)
                return Task.FromResult<IReadOnlyList<DiscoveredSourceField>>(Array.Empty<DiscoveredSourceField>());

            var headers = rows[0].Keys.ToList();
            var fields = new List<DiscoveredSourceField>();

            for (var i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                var samples = rows
                    .Select(x => x.TryGetValue(header, out var value) ? value : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Take(100)
                    .ToList();

                var inferred = InferType(samples);

                fields.Add(new DiscoveredSourceField(
                    FieldName: header,
                    DisplayName: header,
                    SourceDataType: inferred.SourceDataType,
                    Ordinal: i + 1,
                    IsNullable: rows.Any(x => !x.TryGetValue(header, out var value) || string.IsNullOrWhiteSpace(value)),
                    MaxLength: inferred.MaxLength,
                    NumericPrecision: inferred.NumericPrecision,
                    NumericScale: inferred.NumericScale,
                    SampleValue: samples.FirstOrDefault(),
                    IsPrimaryKeyCandidate: IsPrimaryKeyCandidate(header, samples, rows.Count),
                    IsTimestampCandidate: IsTimestampCandidate(header, samples)));
            }

            return Task.FromResult<IReadOnlyList<DiscoveredSourceField>>(fields);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return Task.FromResult<IReadOnlyList<DiscoveredSourceField>>(Array.Empty<DiscoveredSourceField>());
        }
    }

    public Task<IReadOnlyList<DataSourceRow>> ReadRowsAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        DataSourceReadRequest request,
        CancellationToken cancellationToken)
    {
        var options = ConnectorOptions.Merge(
            connectionProfile.ConnectionOptionsJson,
            datasetDefinition.DatasetOptionsJson,
            request.DatasetOptionsJson);

        var limit = Math.Clamp(request.Limit <= 0 ? 200 : request.Limit, 1, 100_000);
        var rows = ReadExcelRows(connectionProfile, datasetDefinition, options, limit, cancellationToken);

        return Task.FromResult<IReadOnlyList<DataSourceRow>>(
            rows.Select((row, index) => new DataSourceRow(index + 1, row)).ToList());
    }

    public async Task<IReadOnlyList<DataSourceRow>> ReadRowsSinceKeyAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        DataSourceIncrementalReadRequest request,
        CancellationToken cancellationToken)
    {
        var rows = await ReadRowsAsync(
            connectionProfile,
            datasetDefinition,
            new DataSourceReadRequest(
                request.ConnectionProfileId,
                request.SourceDatasetDefinitionId,
                request.SourceObjectName,
                request.SourceSchemaName,
                request.Limit,
                request.DatasetOptionsJson),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(request.LastCursorValue))
            return rows;

        return rows
            .Where(x =>
                x.Values.TryGetValue(request.CursorFieldName, out var value) &&
                string.Compare(value, request.LastCursorValue, StringComparison.OrdinalIgnoreCase) > 0)
            .ToList();
    }

    public string? GetLastError() => _lastError;

    private static IReadOnlyList<IReadOnlyDictionary<string, string?>> ReadExcelRows(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        ConnectorFileOptions options,
        int limit,
        CancellationToken cancellationToken)
    {
        var path = ResolveExcelPath(connectionProfile, datasetDefinition, options);

        using var workbook = new XLWorkbook(path);

        var sheetName = options.SheetName ?? datasetDefinition.SourceSchemaName;
        var worksheet = !string.IsNullOrWhiteSpace(sheetName)
            ? workbook.Worksheets.Worksheet(sheetName)
            : workbook.Worksheets.First();

        var usedRange = worksheet.RangeUsed();
        if (usedRange is null)
            return Array.Empty<IReadOnlyDictionary<string, string?>>();

        var hasHeader = options.HasHeader ?? true;
        var firstRow = usedRange.FirstRowUsed().RowNumber();
        var lastRow = usedRange.LastRowUsed().RowNumber();
        var firstColumn = usedRange.FirstColumnUsed().ColumnNumber();
        var lastColumn = usedRange.LastColumnUsed().ColumnNumber();

        var headers = new List<string>();

        for (var col = firstColumn; col <= lastColumn; col++)
        {
            var raw = hasHeader
                ? worksheet.Cell(firstRow, col).GetFormattedString()
                : $"Column{col - firstColumn + 1}";

            headers.Add(NormalizeHeader(raw));
        }

        headers = EnsureUniqueHeaders(headers);

        var startRow = hasHeader ? firstRow + 1 : firstRow;
        var result = new List<IReadOnlyDictionary<string, string?>>();

        for (var row = startRow; row <= lastRow && result.Count < limit; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dictionary = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var hasAnyValue = false;

            for (var col = firstColumn; col <= lastColumn; col++)
            {
                var value = worksheet.Cell(row, col).GetFormattedString();
                if (!string.IsNullOrWhiteSpace(value))
                    hasAnyValue = true;

                dictionary[headers[col - firstColumn]] = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }

            if (hasAnyValue)
                result.Add(dictionary);
        }

        return result;
    }

    private static IEnumerable<string> ResolveExcelFiles(ConnectionProfile connectionProfile)
    {
        var options = ConnectorOptions.FromJson(connectionProfile.ConnectionOptionsJson);

        if (!string.IsNullOrWhiteSpace(options.FilePath))
            return new[] { options.FilePath! };

        if (!string.IsNullOrWhiteSpace(connectionProfile.FileRootPath) &&
            Directory.Exists(connectionProfile.FileRootPath))
        {
            return Directory
                .EnumerateFiles(connectionProfile.FileRootPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(x =>
                    x.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                    x.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase) ||
                    x.EndsWith(".xls", StringComparison.OrdinalIgnoreCase));
        }

        return Array.Empty<string>();
    }

    private static string ResolveExcelPath(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        ConnectorFileOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.FilePath))
            return options.FilePath!;

        var fileName = options.FileName ?? datasetDefinition.SourceObjectName;

        if (Path.IsPathRooted(fileName))
            return fileName;

        if (!string.IsNullOrWhiteSpace(connectionProfile.FileRootPath))
            return Path.Combine(connectionProfile.FileRootPath!, fileName);

        return fileName;
    }

    private static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Column";

        var cleaned = new string(value
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_')
            .ToArray());

        while (cleaned.Contains("__", StringComparison.Ordinal))
            cleaned = cleaned.Replace("__", "_", StringComparison.Ordinal);

        cleaned = cleaned.Trim('_');

        return string.IsNullOrWhiteSpace(cleaned) ? "Column" : cleaned;
    }

    private static List<string> EnsureUniqueHeaders(IReadOnlyList<string> headers)
    {
        var result = new List<string>();
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            var clean = NormalizeHeader(header);

            if (!used.TryAdd(clean, 1))
            {
                used[clean]++;
                result.Add($"{clean}_{used[clean]}");
            }
            else
            {
                result.Add(clean);
            }
        }

        return result;
    }

    private static string NormalizeCode(string value)
    {
        var clean = new string(value
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_')
            .ToArray());

        while (clean.Contains("__", StringComparison.Ordinal))
            clean = clean.Replace("__", "_", StringComparison.Ordinal);

        return clean.Trim('_');
    }

    private static InferredType InferType(IReadOnlyList<string?> values)
    {
        if (values.Count == 0)
            return new InferredType("Text", null, null, null);

        if (values.All(x => bool.TryParse(x, out _)))
            return new InferredType("Boolean", null, null, null);

        if (values.All(x => long.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
            return new InferredType("Integer", null, 18, 0);

        if (values.All(x => decimal.TryParse(x, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
            return new InferredType("Decimal", null, 18, 6);

        if (values.All(x => DateTime.TryParse(x, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _)))
            return new InferredType("Timestamp", null, null, null);

        return new InferredType("Text", values.Max(x => x?.Length ?? 0), null, null);
    }

    private static bool IsPrimaryKeyCandidate(string header, IReadOnlyList<string?> samples, int rowCount)
    {
        if (!header.Contains("id", StringComparison.OrdinalIgnoreCase) &&
            !header.Contains("key", StringComparison.OrdinalIgnoreCase) &&
            !header.Contains("code", StringComparison.OrdinalIgnoreCase))
            return false;

        return samples.Count > 0 &&
               samples.Count == rowCount &&
               samples.Distinct(StringComparer.OrdinalIgnoreCase).Count() == samples.Count;
    }

    private static bool IsTimestampCandidate(string header, IReadOnlyList<string?> samples)
    {
        if (!header.Contains("time", StringComparison.OrdinalIgnoreCase) &&
            !header.Contains("date", StringComparison.OrdinalIgnoreCase) &&
            !header.EndsWith("at", StringComparison.OrdinalIgnoreCase))
            return false;

        return samples.Count > 0 &&
               samples.All(x => DateTime.TryParse(x, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _));
    }

    private DataSourceConnectionTestResult Success(string message, IReadOnlyDictionary<string, string?> metadata)
    {
        _lastError = null;
        return new DataSourceConnectionTestResult(true, message, DateTime.UtcNow, metadata);
    }

    private DataSourceConnectionTestResult Failure(string message)
    {
        _lastError = message;
        return new DataSourceConnectionTestResult(false, message, DateTime.UtcNow, new Dictionary<string, string?>());
    }

    private sealed record InferredType(
        string SourceDataType,
        int? MaxLength,
        int? NumericPrecision,
        int? NumericScale);
}