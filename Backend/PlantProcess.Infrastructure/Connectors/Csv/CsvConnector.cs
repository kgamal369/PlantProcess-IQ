using System.Globalization;
using System.Text;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Connectors.Common;

namespace PlantProcess.Infrastructure.Connectors.Csv;

public sealed class CsvConnector : IDataSourceConnector, ISchemaReader, IDataSourceReader
{
    private string? _lastError;

    public string ProviderType => "Csv";

    public async Task<DataSourceConnectionTestResult> TestConnectionAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = ConnectorOptions.FromJson(connectionProfile.ConnectionOptionsJson);

            if (!string.IsNullOrWhiteSpace(options.CsvText))
            {
                var rows = ParseCsv(options.CsvText!, ResolveDelimiter(options.Delimiter), options.HasHeader ?? true)
                    .Take(2)
                    .ToList();

                return Success("CSV inline text is readable.", new Dictionary<string, string?>
                {
                    ["mode"] = "InlineText",
                    ["sampleRows"] = rows.Count.ToString(CultureInfo.InvariantCulture)
                });
            }

            var rootPath = connectionProfile.FileRootPath;
            if (string.IsNullOrWhiteSpace(rootPath))
                return Failure("CSV connection requires FileRootPath or ConnectionOptionsJson.csvText.");

            if (!Directory.Exists(rootPath))
                return Failure($"CSV root path does not exist: {rootPath}");

            var csvFiles = Directory
                .EnumerateFiles(rootPath, "*.csv", SearchOption.TopDirectoryOnly)
                .Take(5)
                .ToList();

            return Success("CSV root path is readable.", new Dictionary<string, string?>
            {
                ["mode"] = "Directory",
                ["fileRootPath"] = rootPath,
                ["sampleFileCount"] = csvFiles.Count.ToString(CultureInfo.InvariantCulture)
            });
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return Failure($"CSV connection test failed: {ex.Message}");
        }
    }

    public Task<IReadOnlyList<DiscoveredSourceDataset>> DiscoverDatasetsAsync(
        ConnectionProfile connectionProfile,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = ConnectorOptions.FromJson(connectionProfile.ConnectionOptionsJson);
            var datasets = new List<DiscoveredSourceDataset>();

            if (!string.IsNullOrWhiteSpace(options.CsvText))
            {
                datasets.Add(new DiscoveredSourceDataset(
                    DatasetCode: NormalizeCode(options.FileName ?? connectionProfile.ConnectionProfileCode),
                    DatasetName: options.FileName ?? connectionProfile.ConnectionProfileName,
                    DatasetKind: "CsvFile",
                    SourceObjectName: options.FileName ?? $"{connectionProfile.ConnectionProfileCode}.csv",
                    SourceSchemaName: null,
                    DatasetOptionsJson: BuildDatasetOptionsJson(
                        fileName: options.FileName,
                        filePath: null,
                        delimiter: options.Delimiter,
                        hasHeader: options.HasHeader ?? true)));

                return Task.FromResult<IReadOnlyList<DiscoveredSourceDataset>>(datasets);
            }

            if (string.IsNullOrWhiteSpace(connectionProfile.FileRootPath))
                return Task.FromResult<IReadOnlyList<DiscoveredSourceDataset>>(datasets);

            if (!Directory.Exists(connectionProfile.FileRootPath))
                return Task.FromResult<IReadOnlyList<DiscoveredSourceDataset>>(datasets);

            foreach (var filePath in Directory.EnumerateFiles(connectionProfile.FileRootPath, "*.csv"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = Path.GetFileName(filePath);
                var nameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

                datasets.Add(new DiscoveredSourceDataset(
                    DatasetCode: NormalizeCode(nameWithoutExtension),
                    DatasetName: nameWithoutExtension,
                    DatasetKind: "CsvFile",
                    SourceObjectName: fileName,
                    SourceSchemaName: null,
                    DatasetOptionsJson: BuildDatasetOptionsJson(
                        fileName: fileName,
                        filePath: filePath,
                        delimiter: options.Delimiter,
                        hasHeader: options.HasHeader ?? true)));
            }

            return Task.FromResult<IReadOnlyList<DiscoveredSourceDataset>>(datasets);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return Task.FromResult<IReadOnlyList<DiscoveredSourceDataset>>(Array.Empty<DiscoveredSourceDataset>());
        }
    }

    public async Task<IReadOnlyList<DiscoveredSourceField>> DiscoverFieldsForDatasetAsync(
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

            var delimiter = ResolveDelimiter(options.Delimiter);
            var hasHeader = options.HasHeader ?? true;
            var csvText = await ReadCsvTextAsync(connectionProfile, datasetDefinition, options, cancellationToken);

            var rows = ParseCsv(csvText, delimiter, hasHeader)
                .Take(Math.Clamp(options.MaxRowsToAnalyze ?? 200, 1, 10_000))
                .ToList();

            if (rows.Count == 0)
                return Array.Empty<DiscoveredSourceField>();

            var headers = rows[0].Keys.ToList();
            var fields = new List<DiscoveredSourceField>();

            for (var index = 0; index < headers.Count; index++)
            {
                var header = headers[index];
                var samples = rows
                    .Select(x => x.TryGetValue(header, out var value) ? value : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Take(100)
                    .ToList();

                var inferred = InferType(samples);
                var sampleValue = samples.FirstOrDefault();

                fields.Add(new DiscoveredSourceField(
                    FieldName: header,
                    DisplayName: header,
                    SourceDataType: inferred.SourceDataType,
                    Ordinal: index + 1,
                    IsNullable: rows.Any(x => !x.TryGetValue(header, out var value) || string.IsNullOrWhiteSpace(value)),
                    MaxLength: inferred.MaxLength,
                    NumericPrecision: inferred.NumericPrecision,
                    NumericScale: inferred.NumericScale,
                    SampleValue: sampleValue,
                    IsPrimaryKeyCandidate: IsPrimaryKeyCandidate(header, samples, rows.Count),
                    IsTimestampCandidate: IsTimestampCandidate(header, samples)));
            }

            return fields;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return Array.Empty<DiscoveredSourceField>();
        }
    }

    public async Task<IReadOnlyList<DataSourceRow>> ReadRowsAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        DataSourceReadRequest request,
        CancellationToken cancellationToken)
    {
        var options = ConnectorOptions.Merge(
            connectionProfile.ConnectionOptionsJson,
            datasetDefinition.DatasetOptionsJson,
            request.DatasetOptionsJson);

        var csvText = await ReadCsvTextAsync(connectionProfile, datasetDefinition, options, cancellationToken);
        var delimiter = ResolveDelimiter(options.Delimiter);
        var hasHeader = options.HasHeader ?? true;

        var limit = Math.Clamp(request.Limit <= 0 ? 200 : request.Limit, 1, 100_000);

        return ParseCsv(csvText, delimiter, hasHeader)
            .Take(limit)
            .Select((row, index) => new DataSourceRow(index + 1, row))
            .ToList();
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

    private async Task<string> ReadCsvTextAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        ConnectorFileOptions options,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.CsvText))
            return options.CsvText!;

        var path = ResolvePath(connectionProfile, datasetDefinition, options);

        if (!File.Exists(path))
            throw new FileNotFoundException($"CSV source file was not found: {path}", path);

        return await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
    }

    private static string ResolvePath(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        ConnectorFileOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.FilePath))
            return options.FilePath!;

        var fileName = options.FileName ?? datasetDefinition.SourceObjectName;

        if (Path.IsPathRooted(fileName))
            return fileName;

        if (string.IsNullOrWhiteSpace(connectionProfile.FileRootPath))
            return fileName;

        return Path.Combine(connectionProfile.FileRootPath!, fileName);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string?>> ParseCsv(
        string csvText,
        char delimiter,
        bool hasHeader)
    {
        using var reader = new StringReader(csvText);

        var rawRows = new List<List<string?>>();
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            rawRows.Add(ParseCsvLine(line, delimiter));
        }

        if (rawRows.Count == 0)
            return Array.Empty<IReadOnlyDictionary<string, string?>>();

        var maxColumns = rawRows.Max(x => x.Count);

        var headers = hasHeader
            ? EnsureUniqueHeaders(rawRows[0].Select(NormalizeHeader).ToList())
            : Enumerable.Range(1, maxColumns).Select(i => $"Column{i}").ToList();

        var dataRows = hasHeader ? rawRows.Skip(1) : rawRows;

        return dataRows
            .Select(row =>
            {
                var dictionary = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < headers.Count; i++)
                {
                    dictionary[headers[i]] = i < row.Count ? NullIfEmpty(row[i]) : null;
                }

                return (IReadOnlyDictionary<string, string?>)dictionary;
            })
            .ToList();
    }

    private static List<string?> ParseCsvLine(string line, char delimiter)
    {
        var result = new List<string?>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString());
        return result;
    }

    private static string BuildDatasetOptionsJson(
        string? fileName,
        string? filePath,
        string? delimiter,
        bool hasHeader)
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            fileName,
            filePath,
            delimiter = string.IsNullOrWhiteSpace(delimiter) ? "," : delimiter,
            hasHeader
        });
    }

    private static char ResolveDelimiter(string? delimiter)
    {
        if (string.IsNullOrWhiteSpace(delimiter))
            return ',';

        return delimiter.Trim() switch
        {
            "\\t" => '\t',
            "tab" => '\t',
            _ => delimiter.Trim()[0]
        };
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
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

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

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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