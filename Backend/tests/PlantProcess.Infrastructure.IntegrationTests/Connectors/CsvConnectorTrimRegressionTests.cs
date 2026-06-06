using System.Reflection;
using PlantProcess.Infrastructure.Connectors.Csv;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Connectors;

public sealed class CsvConnectorTrimRegressionTests
{
    [Fact]
    public void Csv_parser_trims_unquoted_cell_values_from_yard_fixture()
    {
        var method = typeof(CsvConnector).GetMethod(
            "ParseCsv",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        const string csv = "yard_record_id,coil_id,slab_id,storage_area,bay,position,received_at_utc,shipped_at_utc\n" +
                           "ADV_YARD4002,ADV_COIL4002,ADV_SLAB4002,YARD-HOT, BAY-03, POS-17,2026-05-01T12:20:00Z,\n";

        var rows = (IReadOnlyList<IReadOnlyDictionary<string, string?>>)method!.Invoke(
            null,
            new object[] { csv, ',', true })!;

        Assert.Single(rows);
        Assert.Equal("BAY-03", rows[0]["bay"]);
        Assert.Equal("POS-17", rows[0]["position"]);
    }
}