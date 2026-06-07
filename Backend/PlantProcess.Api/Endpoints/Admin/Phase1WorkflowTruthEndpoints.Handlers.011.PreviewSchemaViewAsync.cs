using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Admin;

// PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_HANDLERS_SPLIT
public static partial class Phase1WorkflowTruthEndpoints
{
private static async Task<IResult> PreviewSchemaViewAsync(
        [FromBody] PreviewSchemaViewRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateSafePreviewSql(request.SqlText);
        if (validation is not null)
        {
            return Results.BadRequest(new SchemaViewPreviewResponse(
                false,
                validation,
                0,
                0,
                new List<SchemaViewPreviewColumn>(),
                new List<Dictionary<string, object?>>()));
        }

        var maxRows = request.MaxRows is > 0 and <= 500 ? request.MaxRows.Value : 100;
        var sql = WrapSelectWithLimit(request.SqlText, maxRows);

        var rows = new List<Dictionary<string, object?>>();
        var columns = new List<SchemaViewPreviewColumn>();

        var sw = Stopwatch.StartNew();

        try
        {
            var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(sql, connection)
            {
                CommandTimeout = request.TimeoutSeconds is > 0 and <= 30
                    ? request.TimeoutSeconds.Value
                    : 10
            };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(new SchemaViewPreviewColumn(
                    reader.GetName(i),
                    reader.GetDataTypeName(i),
                    i + 1));
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = await reader.IsDBNullAsync(i, cancellationToken)
                        ? null
                        : reader.GetValue(i);

                    row[reader.GetName(i)] = value;
                }

                rows.Add(row);
            }

            sw.Stop();

            return Results.Ok(new SchemaViewPreviewResponse(
                true,
                $"Preview returned {rows.Count} row(s).",
                rows.Count,
                sw.ElapsedMilliseconds,
                columns,
                rows));
        }
        catch (Exception ex)
        {
            sw.Stop();

            return Results.BadRequest(new SchemaViewPreviewResponse(
                false,
                $"Preview failed: {ex.Message}",
                0,
                sw.ElapsedMilliseconds,
                columns,
                rows));
        }
    }
}
