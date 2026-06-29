using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.BlendedProvenance;

// Pins the transition coil C-0044170 and proves weighted (70/30) provenance across its two parent heats,
// with the population (parent_count) and completeness (weights sum to 1.0, no unattributed remainder)
// reported. Mirrors the genealogy walk test. DB-gated; skips cleanly when the integration DB is
// unreachable or the golden-thread fixture (seed 010) is not loaded.
[Xunit.Collection("GoldenThreadSerial")]
public sealed class TransitionCoilWeightedAttributionTests : AuthenticatedApiTestBase
{
    private const string TransitionCoil = "C-0044170";

    public TransitionCoilWeightedAttributionTests(WebApplicationFactory<Program> factory) : base(factory) { }

    private static string Conn => ResolveIntegrationTestConnectionString();

    private sealed record ParentEdge(Guid ParentId, decimal Weight, bool IsTransition, string Evidence);

    private static async Task<NpgsqlConnection> OpenAsync()
    {
        Skip.IfNot(AuthenticatedApiTestBase.IsIntegrationDbReachable(),
            "Integration Postgres not reachable/authenticated on this machine; runs in CI.");
        var c = new NpgsqlConnection(Conn);
        await c.OpenAsync();
        return c;
    }

    private static async Task<Guid?> ResolveCoilIdAsync(NpgsqlConnection c)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT id FROM public.material_units WHERE material_code = @k AND COALESCE(is_deleted, false) = false LIMIT 1", c);
        cmd.Parameters.AddWithValue("k", TransitionCoil);
        var r = await cmd.ExecuteScalarAsync();
        return r is Guid g ? g : (Guid?)null;
    }

    private static async Task<List<ParentEdge>> AttributionAsync(NpgsqlConnection c, Guid childId)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT parent_material_unit_id, contribution_weight, is_transition, evidence " +
            "FROM public.ppiq_v5_blended_attribution_for_child(@child)", c);
        cmd.Parameters.AddWithValue("child", childId);
        var rows = new List<ParentEdge>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new ParentEdge(
                reader.GetGuid(0),
                reader.GetDecimal(1),
                reader.GetBoolean(2),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3)));
        }
        return rows;
    }

    [SkippableFact]
    public async Task Transition_coil_reports_weighted_attribution_across_two_heats()
    {
        await using var c = await OpenAsync();
        var childId = await ResolveCoilIdAsync(c);
        Skip.If(childId is null, $"Golden-thread fixture not seeded ({TransitionCoil} absent); run seed 010_p3_golden_thread_seed.sql.");

        var parents = await AttributionAsync(c, childId!.Value);

        Assert.Equal(2, parents.Count); // two contributing heats (population)
        Assert.All(parents, p => Assert.True(p.IsTransition, "Both parent edges of a transition coil must be flagged transition."));
        Assert.All(parents, p => Assert.False(string.IsNullOrWhiteSpace(p.Evidence), "Each parent edge must carry an evidence statement."));

        var weights = parents.Select(p => p.Weight).OrderByDescending(w => w).ToList();
        Assert.Equal(0.70m, weights[0], 2); // primary heat
        Assert.Equal(0.30m, weights[1], 2); // transition heat
        Assert.True(Math.Abs(weights.Sum() - 1.0m) <= 0.015m, "Weights must sum to 1.0 (complete population, no unattributed remainder).");
    }

    [SkippableFact]
    public async Task Transition_coil_weight_status_reports_population_and_completeness()
    {
        await using var c = await OpenAsync();
        var childId = await ResolveCoilIdAsync(c);
        Skip.If(childId is null, $"Golden-thread fixture not seeded ({TransitionCoil} absent); run seed 010_p3_golden_thread_seed.sql.");

        await using var cmd = new NpgsqlCommand(
            "SELECT parent_count, contribution_sum, has_transition, is_green " +
            "FROM public.ppiq_v5_child_weight_status WHERE child_material_unit_id = @child", c);
        cmd.Parameters.AddWithValue("child", childId!.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Weight-status row must exist for the transition coil.");

        var parentCount = reader.GetInt64(0);
        var contributionSum = reader.GetDecimal(1);
        var hasTransition = reader.GetBoolean(2);
        var isGreen = reader.GetBoolean(3);

        Assert.Equal(2L, parentCount);                            // population: two heats
        Assert.True(Math.Abs(contributionSum - 1.0m) <= 0.015m);  // no excluded/unattributed share
        Assert.True(hasTransition);                               // flagged as a blended transition
        Assert.True(isGreen);                                     // within tolerance, render is trustworthy
    }
}
