using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Api.Endpoints.Analytics;

/// <summary>
/// T-101 wiring: the advanced-analysis (managed-stat-v1) result path emitted THROUGH the claim guard.
/// Every figure carries a JobRun handle; any figure whose handle does not resolve is withheld.
/// </summary>
public static class ProvenanceGuardedAdvancedResultsEndpoints
{
    private const string Caveat = "This is a diagnostic association, not a guaranteed root cause.";

    public static IEndpointRouteBuilder MapProvenanceGuardedAdvancedResultsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics/advanced")
            .WithTags("Analytics Advanced (Guarded)")
            .RequireAuthorization();

        group.MapGet("/runs-guarded", async (string? outcomeKey, ProvenanceClaimGuard guard, NpgsqlDataSource ds, CancellationToken ct) =>
        {
            var runs = new List<(Guid Id, string Engine, string? Outcome, string Status, long DurationMs)>();

            await using (var cmd = ds.CreateCommand(
                "SELECT id, engine_key, target_outcome_key, COALESCE(status,'Unknown'), COALESCE(duration_ms,0) " +
                "FROM public.ml_correlation_compute_runs " +
                (string.IsNullOrWhiteSpace(outcomeKey) ? "" : "WHERE target_outcome_key = @o ") +
                "ORDER BY completed_at_utc DESC NULLS LAST LIMIT 50"))
            {
                if (!string.IsNullOrWhiteSpace(outcomeKey)) cmd.Parameters.AddWithValue("o", outcomeKey!);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    runs.Add((
                        reader.GetGuid(0),
                        reader.GetString(1),
                        await reader.IsDBNullAsync(2, ct) ? null : reader.GetString(2),
                        reader.GetString(3),
                        Convert.ToInt64(reader.GetValue(4))));
                }
            }

            var claims = runs
                .Select(r => new Claim($"run:{r.Id}:durationMs", ClaimValueKind.Numeric, r.DurationMs,
                                       ProvenanceHandle.JobRun(r.Id.ToString(), $"engine:{r.Engine}")))
                .ToList();

            var outcome = await guard.InspectAsync(claims, ct);
            var acceptedKeys = outcome.Accepted.Select(a => a.Key).ToHashSet();

            var emitted = runs
                .Where(r => acceptedKeys.Contains($"run:{r.Id}:durationMs"))
                .Select(r => new
                {
                    runId = r.Id,
                    engineKey = r.Engine,
                    targetOutcomeKey = r.Outcome,
                    status = r.Status,
                    durationMs = r.DurationMs,
                    honestyCaveat = Caveat,
                    evidenceHandle = new { kind = ProvenanceKind.JobRun.ToString(), id = r.Id.ToString() }
                })
                .ToArray();

            return Results.Ok(new
            {
                caveat = Caveat,
                runs = emitted,
                withheld = outcome.Rejected.Select(x => new { key = x.Claim.Key, reason = x.Reason }).ToArray()
            });
        });

        return app;
    }
}