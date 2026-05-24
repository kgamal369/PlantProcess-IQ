using ClosedXML.Excel;
using FluentAssertions;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Connectors.Excel;

namespace PlantProcess.Infrastructure.IntegrationTests.Connectors;

public sealed class ExcelConnectorSmokeTests
{
    [Fact]
    public async Task ExcelConnector_should_test_file_connection()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "plantprocess-excel-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        var filePath = Path.Combine(tempFolder, "sample.xlsx");
        CreateWorkbook(filePath);

        var connector = new ExcelConnector();

        var connection = CreateConnectionProfile(tempFolder);

        var result = await connector.TestConnectionAsync(connection, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
    }

    [Fact]
    public async Task ExcelConnector_should_discover_workbook_sheets()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "plantprocess-excel-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        var filePath = Path.Combine(tempFolder, "sample.xlsx");
        CreateWorkbook(filePath);

        var connector = new ExcelConnector();

        var connection = CreateConnectionProfile(tempFolder);

        var datasets = await connector.DiscoverDatasetsAsync(connection, CancellationToken.None);

        datasets.Should().NotBeEmpty();
        datasets.Should().Contain(x => x.DatasetName.Contains("ProcessData", StringComparison.OrdinalIgnoreCase));
    }

    private static void CreateWorkbook(string filePath)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("ProcessData");

        sheet.Cell(1, 1).Value = "MaterialCode";
        sheet.Cell(1, 2).Value = "ObservedAtUtc";
        sheet.Cell(1, 3).Value = "Temperature";

        sheet.Cell(2, 1).Value = "M-001";
        sheet.Cell(2, 2).Value = new DateTime(2026, 5, 19, 10, 15, 12);
        sheet.Cell(2, 3).Value = 1234.56;

        workbook.SaveAs(filePath);
    }

    private static ConnectionProfile CreateConnectionProfile(string fileRootPath)
    {
        return new ConnectionProfile(
            sourceSystemDefinitionId: Guid.NewGuid(),
            connectionProfileCode:    "TEST_EXCEL",
            connectionProfileName:    "Test Excel",
            providerType:             "Excel",
            isSynthetic:              true,
            fileRootPath:             fileRootPath,
            connectionOptionsJson:    "{}",
            description:              "Test Excel connector",
            sourceSystem:             "Test",
            sourceRecordId:           Guid.NewGuid().ToString("N"));
    }
}