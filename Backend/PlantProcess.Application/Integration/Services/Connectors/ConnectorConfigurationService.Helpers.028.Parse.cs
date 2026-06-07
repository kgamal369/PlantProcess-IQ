using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.Connectors;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.Connectors;

// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_HELPERS_SPLIT
public sealed partial class ConnectorConfigurationService
{
private static class CsvTextParser
    {
        public static CsvParseResult Parse(
            string? csvText,
            char delimiter,
            bool hasHeader,
            int maxRows)
        {
            if (string.IsNullOrWhiteSpace(csvText))
                return new CsvParseResult(Array.Empty<string>(), Array.Empty<IReadOnlyDictionary<string, string?>>());

            var records = ParseRecords(csvText, delimiter)
                .Where(x => x.Count > 0 && x.Any(v => !string.IsNullOrWhiteSpace(v)))
                .Take(maxRows + 1)
                .ToList();

            if (records.Count == 0)
                return new CsvParseResult(Array.Empty<string>(), Array.Empty<IReadOnlyDictionary<string, string?>>());

            var headers = hasHeader
                ? records[0].Select(NormalizeHeader).ToList()
                : Enumerable.Range(1, records[0].Count).Select(x => $"Column{x}").ToList();

            headers = EnsureUniqueHeaders(headers);

            var dataRecords = hasHeader
                ? records.Skip(1).Take(maxRows).ToList()
                : records.Take(maxRows).ToList();

            var rows = new List<IReadOnlyDictionary<string, string?>>();

            foreach (var record in dataRecords)
            {
                var dictionary = new Dictionary<string, string?>();

                for (var i = 0; i < headers.Count; i++)
                {
                    var value = i < record.Count ? record[i] : null;
                    dictionary[headers[i]] = string.IsNullOrWhiteSpace(value)
                        ? null
                        : value;
                }

                rows.Add(dictionary);
            }

            return new CsvParseResult(headers, rows);
        }

        private static IReadOnlyList<IReadOnlyList<string>> ParseRecords(string text, char delimiter)
        {
            var records = new List<IReadOnlyList<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++)
            {
                var current = text[i];

                if (current == '"')
                {
                    if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (current == delimiter && !inQuotes)
                {
                    row.Add(field.ToString().Trim());
                    field.Clear();
                    continue;
                }

                if ((current == '\r' || current == '\n') && !inQuotes)
                {
                    if (current == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    row.Add(field.ToString().Trim());
                    field.Clear();

                    records.Add(row);
                    row = new List<string>();
                    continue;
                }

                field.Append(current);
            }

            row.Add(field.ToString().Trim());

            if (row.Count > 1 || row.Any(x => !string.IsNullOrWhiteSpace(x)))
                records.Add(row);

            return records;
        }

        private static string NormalizeHeader(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Column";

            var cleaned = value.Trim();

            cleaned = string.Concat(cleaned.Select(ch =>
                char.IsLetterOrDigit(ch) || ch == '_'
                    ? ch
                    : '_'));

            while (cleaned.Contains("__", StringComparison.Ordinal))
                cleaned = cleaned.Replace("__", "_", StringComparison.Ordinal);

            cleaned = cleaned.Trim('_');

            return string.IsNullOrWhiteSpace(cleaned)
                ? "Column"
                : cleaned;
        }

        private static List<string> EnsureUniqueHeaders(IReadOnlyList<string> headers)
        {
            var result = new List<string>();
            var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var header in headers)
            {
                if (!used.TryAdd(header, 1))
                {
                    used[header]++;
                    result.Add($"{header}_{used[header]}");
                }
                else
                {
                    result.Add(header);
                }
            }

            return result;
        }
    }
}
