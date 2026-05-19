using FluentAssertions;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Connectors.Csv;

namespace PlantProcess.Infrastructure.IntegrationTests.Connectors;

public sealed class CsvConnectorSmokeTests
{
    [Fact]
    public async Task CsvConnector_should_test_inline_csv_connection()
    {
        var connector = new CsvConnector();

        var connection = CreateConnectionProfile(
            connectionOptionsJson: """
            {
              "csvText": "MaterialCode,ObservedAtUtc,Temperature\nM-001,2026-05-19 10:15:12.123,1234.56",
              "delimiter": ",",
              "hasHeader": true
            }
            """);

        var result = await connector.TestConnectionAsync(connection, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);    }

    [Fact]
    public async Task CsvConnector_should_discover_inline_csv_datasets()
    {
        var connector = new CsvConnector();

        var connection = CreateConnectionProfile(
            connectionOptionsJson: """
            {
              "csvText": "MaterialCode;ObservedAtUtc;Temperature\nM-001;19/05/2026 10:15:12;1234.56",
              "delimiter": ";",
              "hasHeader": true
            }
            """);

        var datasets = await connector.DiscoverDatasetsAsync(connection, CancellationToken.None);

        datasets.Should().NotBeEmpty();
        datasets[0].DatasetCode.Should().NotBeNullOrWhiteSpace();
    }

    private static ConnectionProfile CreateConnectionProfile(string connectionOptionsJson)
    {
       return new ConnectionProfile(
        sourceSystemDefinitionId: Guid.NewGuid(),
        connectionProfileCode: "TEST_CSV",
        connectionProfileName: "Test CSV",
        providerType: "Csv",
        isSynthetic: true,
        connectionMode: "Snapshot",
        fileRootPath: null,
        connectionOptionsJson: connectionOptionsJson,
        readOnlyEnforced: true,
        description: "Test CSV connector",
        sourceSystem: "Test",
        sourceRecordId: Guid.NewGuid().ToString("N"));
    }
}