using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Admin;

public static class P03P04CompletionProofEndpoints
{
    public static IEndpointRouteBuilder MapP03P04CompletionProofEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/p03p04/completion")
            .WithTags("Admin - P03/P04 Completion Proof");

        var environment = app.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        if (environment.IsDevelopment())
            group.AllowAnonymous();
        else
            group.RequireAuthorization("PlantProcessDataManager");

        group.MapGet("/status", async (
            [FromServices] PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            return await QueryAsync(
                dbContext,
                "SELECT * FROM public.ppiq_p03_p04_completion_status() ORDER BY gate_code;",
                cancellationToken);
        });

        group.MapGet("/business-key-validation", async (
            [FromServices] PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            return await QueryAsync(
                dbContext,
                "SELECT * FROM public.ppiq_validate_business_key_dictionary() ORDER BY severity, error_code, object_key;",
                cancellationToken);
        });

        group.MapPost("/safe-sql/resolve", async (
            [FromBody] P03P04SafeSqlRequest request,
            [FromServices] PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            return await QueryAsync(
                dbContext,
                "SELECT * FROM public.ppiq_resolve_safe_sql(@sql, @rowLimit, @timeoutMs);",
                cancellationToken,
                ("sql", request.Sql ?? string.Empty),
                ("rowLimit", request.RowLimit ?? 100),
                ("timeoutMs", request.TimeoutMs ?? 3000));
        });

        group.MapGet("/genealogy-validation", async (
            [FromServices] PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            return await QueryAsync(
                dbContext,
                "SELECT * FROM public.ppiq_validate_genealogy_graph() ORDER BY severity, error_code, material_key LIMIT 200;",
                cancellationToken);
        });

        group.MapGet("/material-investigation/{materialKey}", async (
            string materialKey,
            [FromServices] PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            return await QueryAsync(
                dbContext,
                "SELECT * FROM public.ppiq_material_investigation(@materialKey) ORDER BY sequence_no, event_time_utc NULLS FIRST;",
                cancellationToken,
                ("materialKey", materialKey));
        });

        group.MapPost("/mapping-lifecycle-proof/run", async (
            [FromServices] PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            return await QueryAsync(
                dbContext,
                "SELECT * FROM public.ppiq_run_mapping_lifecycle_proof();",
                cancellationToken);
        });

        return app;
    }

    private static async Task<IResult> QueryAsync(
        PlantProcessDbContext dbContext,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 120;

        foreach (var item in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = item.Name;
            parameter.Value = item.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return Results.Ok(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            rowCount = rows.Count,
            rows
        });
    }

    public sealed record P03P04SafeSqlRequest(
        string? Sql,
        int? RowLimit,
        int? TimeoutMs);
}