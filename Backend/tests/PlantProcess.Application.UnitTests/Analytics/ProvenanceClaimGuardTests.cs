using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

public class ProvenanceClaimGuardTests
{
    private sealed class FakeResolver : IProvenanceResolver
    {
        private readonly Dictionary<string, ProvenanceResolution> _map = new();
        public FakeResolver Live(ProvenanceHandle h) { _map[h.Token] = ProvenanceResolution.Live(h, $"artifact:{h.Token}"); return this; }
        public FakeResolver Seed(ProvenanceHandle h) { _map[h.Token] = ProvenanceResolution.Seed(h, $"artifact:{h.Token}"); return this; }
        public Task<ProvenanceResolution> ResolveAsync(ProvenanceHandle handle, CancellationToken ct)
            => Task.FromResult(_map.TryGetValue(handle.Token, out var r) ? r : ProvenanceResolution.Missing(handle, "Unknown handle."));
    }

    [Fact]
    public async Task A_handle_of_each_kind_resolves_to_its_artifact()
    {
        var resolver = new FakeResolver();
        var handles = new[]
        {
            ProvenanceHandle.Finding("f1"), ProvenanceHandle.JobRun("j1"),
            ProvenanceHandle.Dataset("d1"), ProvenanceHandle.SourceTable("public.coils"),
            ProvenanceHandle.DocumentSection("doc1#3")
        };
        foreach (var h in handles) resolver.Live(h);

        foreach (var h in handles)
        {
            var resolution = await resolver.ResolveAsync(h, CancellationToken.None);
            Assert.True(resolution.Resolvable);
            Assert.True(resolution.IsLive);
            Assert.Equal($"artifact:{h.Token}", resolution.ArtifactRef);
        }
    }

    [Fact]
    public async Task A_number_without_a_handle_is_rejected_and_never_serialized()
    {
        var resolver = new FakeResolver();
        var good = ProvenanceHandle.Finding("f1");
        resolver.Live(good);
        var guard = new ProvenanceClaimGuard(resolver);

        var claims = new List<Claim>
        {
            new("defectRate", ClaimValueKind.Numeric, 0.137, good),
            new("secretUncitedNumber", ClaimValueKind.Numeric, 999.42, Handle: null)
        };

        var outcome = await guard.InspectAsync(claims, CancellationToken.None);

        Assert.Contains(outcome.Rejected, r => r.Claim.Key == "secretUncitedNumber");
        Assert.DoesNotContain(outcome.Accepted, a => a.Key == "secretUncitedNumber");

        // Serialize ONLY accepted claims -> prove the uncited number never reaches the client payload.
        var payload = JsonSerializer.Serialize(outcome.Accepted.ToDictionary(a => a.Key, a => a.Value));
        Assert.DoesNotContain("999.42", payload);
        Assert.Contains("defectRate", payload);
    }

    [Fact]
    public async Task A_seed_backed_handle_is_blocked_from_a_live_surface_but_allowed_internally()
    {
        var resolver = new FakeResolver();
        var seed = ProvenanceHandle.SourceTable("public.demo_seed");
        resolver.Seed(seed);
        var guard = new ProvenanceClaimGuard(resolver);

        var live = new List<Claim> { new("kpi", ClaimValueKind.Numeric, 42.0, seed, IsLiveSurface: true) };
        var liveOutcome = await guard.InspectAsync(live, CancellationToken.None);
        Assert.Empty(liveOutcome.Accepted);
        Assert.Contains(liveOutcome.Rejected, r => r.Reason.Contains("non-live") || r.Reason.Contains("live surface"));

        var internalClaims = new List<Claim> { new("kpi", ClaimValueKind.Numeric, 42.0, seed, IsLiveSurface: false) };
        var internalOutcome = await guard.InspectAsync(internalClaims, CancellationToken.None);
        Assert.Single(internalOutcome.Accepted);
    }

    [Fact]
    public async Task The_guard_withholds_an_unhandled_figure_on_the_advanced_result_path()
    {
        var resolver = new FakeResolver();
        var run = ProvenanceHandle.Finding("run-1");
        resolver.Live(run);
        var guard = new ProvenanceClaimGuard(resolver);

        var claims = new List<Claim>
        {
            new("findingA:effectSize", ClaimValueKind.Numeric, 0.52, run),
            new("findingA:qValue", ClaimValueKind.Numeric, 0.03, run),
            new("findingB:effectSize", ClaimValueKind.Numeric, 0.40, Handle: null) // uncited -> withheld
        };

        var outcome = await guard.InspectAsync(claims, CancellationToken.None);
        var acceptedKeys = outcome.Accepted.Select(a => a.Key).ToHashSet();

        Assert.Contains("findingA:effectSize", acceptedKeys);
        Assert.DoesNotContain("findingB:effectSize", acceptedKeys);
    }
}