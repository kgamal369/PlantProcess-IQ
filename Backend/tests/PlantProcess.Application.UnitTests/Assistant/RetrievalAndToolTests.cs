using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

public class RetrievalAndToolTests
{
    // ---- in-memory retrieval index mirroring the SQL tenant/permission/synthetic/stale filter ----
    private sealed record Row(Guid Tenant, string? ScopeRole, string Kind, string Ref, string Content, bool Synthetic, bool Stale);
    private sealed class InMemoryIndex : IRetrievalIndex
    {
        private static readonly string[] RoleRank = { "viewer", "operator", "engineer", "admin" };
        public readonly List<Row> Rows = new();
        private static int Rank(string r) { var i = Array.IndexOf(RoleRank, (r ?? "").ToLowerInvariant()); return i < 0 ? 0 : i; }

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(RetrievalQuery q, CancellationToken ct)
        {
            var hits = Rows
                .Where(r => r.Tenant == q.TenantId && !r.Synthetic && !r.Stale)
                .Where(r => r.ScopeRole is null || Rank(q.Role) >= Rank(r.ScopeRole))
                .Select(r => new RetrievedChunk(Guid.NewGuid(), r.Kind, r.Ref, r.Content, ProvenanceHandle.Finding(r.Ref), 1.0, r.Synthetic))
                .ToList();
            return Task.FromResult((IReadOnlyList<RetrievedChunk>)hits);
        }
        public Task<ReindexResult> ReindexAsync(ReindexRequest request, CancellationToken ct)
        {
            var replaced = Rows.RemoveAll(r => r.Tenant == request.TenantId);
            Rows.AddRange(request.Chunks.Select(c => new Row(request.TenantId, null, c.SourceKind, c.SourceRef, c.Content, c.IsSynthetic, false)));
            return Task.FromResult(new ReindexResult(request.Chunks.Count, replaced, request.CorrelationId));
        }
    }

    [Fact]
    public async Task Retrieval_never_returns_another_tenants_chunk()
    {
        var idx = new InMemoryIndex();
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        idx.Rows.Add(new Row(a, null, "finding", "a1", "tenant A secret", false, false));
        idx.Rows.Add(new Row(b, null, "finding", "b1", "tenant B secret", false, false));

        var hits = await idx.SearchAsync(new RetrievalQuery(a, "admin", "secret"), CancellationToken.None);
        Assert.Single(hits);
        Assert.Equal("a1", hits[0].SourceRef);
    }

    [Fact]
    public async Task Permission_scope_excludes_chunks_above_the_role()
    {
        var idx = new InMemoryIndex();
        var t = Guid.NewGuid();
        idx.Rows.Add(new Row(t, "admin", "report", "secret-report", "admin only", false, false));
        idx.Rows.Add(new Row(t, null, "doc", "public-doc", "everyone", false, false));

        var asViewer = await idx.SearchAsync(new RetrievalQuery(t, "viewer", "x"), CancellationToken.None);
        Assert.DoesNotContain(asViewer, c => c.SourceRef == "secret-report");
        Assert.Contains(asViewer, c => c.SourceRef == "public-doc");
    }

    [Fact]
    public async Task Reindex_replaces_stale_chunks_and_seed_is_excluded()
    {
        var idx = new InMemoryIndex();
        var t = Guid.NewGuid();
        idx.Rows.Add(new Row(t, null, "finding", "f1", "old figure 111", false, false));

        await idx.ReindexAsync(new ReindexRequest(t, new[]
        {
            new RetrievedChunk(Guid.NewGuid(), "finding", "f1", "new figure 222", ProvenanceHandle.Finding("f1"), 0, false),
            new RetrievedChunk(Guid.NewGuid(), "finding", "seed1", "synthetic", ProvenanceHandle.Finding("seed1"), 0, true),
        }, "corr-1"), CancellationToken.None);

        var hits = await idx.SearchAsync(new RetrievalQuery(t, "admin", "figure"), CancellationToken.None);
        Assert.Contains(hits, c => c.Content.Contains("222"));
        Assert.DoesNotContain(hits, c => c.Content.Contains("111"));  // stale replaced
        Assert.DoesNotContain(hits, c => c.SourceRef == "seed1");      // seed excluded
    }

    // ---- tool layer ----
    private sealed class FakeTool : ITool
    {
        private readonly bool _valid;
        public FakeTool(string name, string role, string? license, bool valid = true) { Name = name; RequiredRole = role; RequiredLicense = license; _valid = valid; }
        public string Name { get; }
        public string RequiredRole { get; }
        public string? RequiredLicense { get; }
        public Task<ToolResult> ExecuteAsync(ToolContext ctx, IReadOnlyDictionary<string, string> args, CancellationToken ct)
            => Task.FromResult(_valid
                ? new ToolResult(true, Name, "{\"v\":1}", new[] { ProvenanceHandle.Finding("f1") }, null)
                : new ToolResult(true, Name, "{}", Array.Empty<ProvenanceHandle>(), null)); // no handle -> invalid
    }

    private static ToolContext Ctx(string role = "engineer", string license = "pro") => new(Guid.NewGuid(), role, license);
    private static readonly Dictionary<string, string> NoArgs = new();

    [Fact]
    public async Task A_permitted_tool_returns_a_result_with_resolvable_handles()
    {
        var reg = new ToolRegistry(new[] { new FakeTool("fetch_finding", "viewer", null) });
        var r = await reg.ExecuteAsync("fetch_finding", Ctx("viewer"), NoArgs, CancellationToken.None);
        Assert.True(r.Ok);
        Assert.NotEmpty(r.Handles);
    }

    [Fact]
    public async Task An_unknown_or_unpermitted_tool_is_refused_with_a_typed_error()
    {
        var reg = new ToolRegistry(new[] { new FakeTool("run_kpi", "engineer", null) });
        Assert.Equal("unknown_tool", (await reg.ExecuteAsync("nope", Ctx(), NoArgs, CancellationToken.None)).Error);
        Assert.Equal("unpermitted_role", (await reg.ExecuteAsync("run_kpi", Ctx("viewer"), NoArgs, CancellationToken.None)).Error);
    }

    [Fact]
    public async Task License_is_enforced_independently()
    {
        var reg = new ToolRegistry(new[] { new FakeTool("premium", "viewer", "enterprise") });
        Assert.Equal("unpermitted_license", (await reg.ExecuteAsync("premium", Ctx("admin", "pro"), NoArgs, CancellationToken.None)).Error);
        Assert.True((await reg.ExecuteAsync("premium", Ctx("admin", "enterprise"), NoArgs, CancellationToken.None)).Ok);
    }

    [Fact]
    public async Task A_result_failing_validation_is_not_returned()
    {
        var reg = new ToolRegistry(new[] { new FakeTool("bad", "viewer", null, valid: false) });
        var r = await reg.ExecuteAsync("bad", Ctx(), NoArgs, CancellationToken.None);
        Assert.False(r.Ok);
        Assert.Equal("invalid_output", r.Error);
    }
}