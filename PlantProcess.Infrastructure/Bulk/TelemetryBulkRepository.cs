using Npgsql;
using NpgsqlTypes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PlantProcess.Infrastructure.Bulk;

public interface ITelemetryBulkRepository
{
    Task BulkInsertAsync(IReadOnlyList<ParameterObservationInsertRow> batch, CancellationToken cancellationToken);
}

public class TelemetryBulkRepository : ITelemetryBulkRepository
{
    private readonly string _connectionString;
    private readonly ILogger<TelemetryBulkRepository> _logger;

    public TelemetryBulkRepository(IConfiguration configuration, ILogger<TelemetryBulkRepository> logger)
    {
        // Must bypass EF DbContext and use the raw connection string
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _logger = logger;
    }

    public async Task BulkInsertAsync(IReadOnlyList<ParameterObservationInsertRow> batch, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // NpgsqlBinaryImporter uses PostgreSQL's native COPY command. It is exponentially faster than standard INSERTs.
        var copyCommand = @"
            COPY ""ParameterObservations"" (
                ""Id"", ""CreatedAtUtc"", ""CreatedBy"", ""IsDeleted"", 
                ""SourceSystemId"", ""SourceRecordId"", ""ProcessStepExecutionId"", 
                ""ParameterDefinitionId"", ""TimestampUtc"", ""TimestampLocal"", 
                ""PlantTimeZoneId"", ""NumericValue"", ""TextValue"", ""BooleanValue""
            ) FROM STDIN (FORMAT BINARY)";

        await using var writer = await connection.BeginBinaryImportAsync(copyCommand, cancellationToken);

        foreach (var row in batch)
        {
            await writer.StartRowAsync(cancellationToken);

            await writer.WriteAsync(row.Id, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(row.CreatedAtUtc, NpgsqlDbType.TimestampTz, cancellationToken);
            await writer.WriteAsync(row.CreatedBy, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(false, NpgsqlDbType.Boolean, cancellationToken); // IsDeleted

            await writer.WriteAsync(row.SourceSystemId, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(row.SourceRecordId, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(row.ProcessStepExecutionId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(row.ParameterDefinitionId, NpgsqlDbType.Uuid, cancellationToken);

            await writer.WriteAsync(row.TimestampUtc, NpgsqlDbType.TimestampTz, cancellationToken);
            await writer.WriteAsync(row.TimestampLocal, NpgsqlDbType.Timestamp, cancellationToken);
            await writer.WriteAsync(row.PlantTimeZoneId, NpgsqlDbType.Varchar, cancellationToken);

            // Handle Nullables safely
            if (row.NumericValue.HasValue) await writer.WriteAsync(row.NumericValue.Value, NpgsqlDbType.Numeric, cancellationToken);
            else await writer.WriteNullAsync(cancellationToken);

            if (row.TextValue != null) await writer.WriteAsync(row.TextValue, NpgsqlDbType.Varchar, cancellationToken);
            else await writer.WriteNullAsync(cancellationToken);

            if (row.BooleanValue.HasValue) await writer.WriteAsync(row.BooleanValue.Value, NpgsqlDbType.Boolean, cancellationToken);
            else await writer.WriteNullAsync(cancellationToken);
        }

        // Commit the transaction to the database
        await writer.CompleteAsync(cancellationToken);
    }
}