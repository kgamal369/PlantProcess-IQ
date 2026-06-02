using System.Text.Json;
using Npgsql;
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Infrastructure.Assistant;

/// <summary>
/// T-051: tenant- and permission-scoped retrieval over canon.assistant_chunk. The tenant filter is a SQL
/// predicate (isolation cannot be overridden by similarity); synthetic and stale chunks are excluded;
/// reindex marks old chunks stale, inserts fresh ones, and records per-tenant counts.
/// </summary>
public sealed class NpgsqlRetrievalIndex : IRetrievalIndex
{
    private static readonly string[] RoleRank = { "viewer", "operator", "engineer", "admin" };
    private readonly NpgsqlDataSource _dataSource;
    private readonly IEmbedder _embedder;

    public NpgsqlRetrievalIndex(NpgsqlDataSource dataSource, IEmbedder embedder)
    {
        _dataSource = dataSource;
        _embedder = embedder;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(RetrievalQuery query, CancellationToken ct)
    {
        var roleRank = Rank(query.Role);
        var candidates = new List<(Guid Id, string Kind, string Ref, string Content, float[] Emb)>();

        await using (var cmd = _dataSource.CreateCommand(
            "SELECT id, source_kind, source_ref, content, embedding_json, scope_role " +
            "FROM canon.assistant_chunk " +
            "WHERE tenant_id = @t AND is_synthetic = false AND is_stale = false"))
        {
            cmd.Parameters.AddWithValue("t", query.TenantId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var scopeRole = await reader.IsDBNullAsync(5, ct) ? null : reader.GetString(5);
                if (scopeRole is not null && roleRank < Rank(scopeRole)) continue; // permission scope
                candidates.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), ParseEmbedding(reader.GetString(4))));
            }
        }

        var q = _embedder.Embed(query.Text).ToArray();
        return candidates
            .Select(c => new RetrievedChunk(c.Id, c.Kind, c.Ref, c.Content, HandleFor(c.Kind, c.Ref), Cosine(q, c.Emb)))
            .OrderByDescending(c => c.Score)
            .Take(query.TopK <= 0 ? 6 : query.TopK)
            .ToList();
    }

    public async Task<ReindexResult> ReindexAsync(ReindexRequest request, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        int replaced;
        await using (var stale = conn.CreateCommand())
        {
            stale.Transaction = tx;
            stale.CommandText = "UPDATE canon.assistant_chunk SET is_stale = true WHERE tenant_id = @t";
            stale.Parameters.AddWithValue("t", request.TenantId);
            replaced = await stale.ExecuteNonQueryAsync(ct);
        }

        foreach (var chunk in request.Chunks)
        {
            var emb = JsonSerializer.Serialize(_embedder.Embed(chunk.Content));
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO canon.assistant_chunk (tenant_id, scope_role, source_kind, source_ref, content, embedding_json, content_hash, is_synthetic, is_stale) " +
                "VALUES (@t,@scope,@kind,@ref,@content,@emb::jsonb,@hash,@syn,false) " +
                "ON CONFLICT (tenant_id, source_kind, source_ref) DO UPDATE SET " +
                "content=EXCLUDED.content, embedding_json=EXCLUDED.embedding_json, content_hash=EXCLUDED.content_hash, " +
                "is_synthetic=EXCLUDED.is_synthetic, is_stale=false, indexed_at=now()";
            cmd.Parameters.AddWithValue("t", request.TenantId);
            cmd.Parameters.AddWithValue("scope", (object?)null ?? DBNull.Value);
            cmd.Parameters.AddWithValue("kind", chunk.SourceKind);
            cmd.Parameters.AddWithValue("ref", chunk.SourceRef);
            cmd.Parameters.AddWithValue("content", chunk.Content);
            cmd.Parameters.AddWithValue("emb", emb);
            cmd.Parameters.AddWithValue("hash", Convert.ToString(chunk.Content.GetHashCode()));
            cmd.Parameters.AddWithValue("syn", chunk.IsSynthetic);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Purge rows that stayed stale (no longer present in the fresh set).
        await using (var purge = conn.CreateCommand())
        {
            purge.Transaction = tx;
            purge.CommandText = "DELETE FROM canon.assistant_chunk WHERE tenant_id = @t AND is_stale = true";
            purge.Parameters.AddWithValue("t", request.TenantId);
            await purge.ExecuteNonQueryAsync(ct);
        }

        await using (var run = conn.CreateCommand())
        {
            run.Transaction = tx;
            run.CommandText = "INSERT INTO canon.assistant_index_run (tenant_id, chunk_count, replaced_count, correlation_id, finished_at) " +
                              "VALUES (@t,@c,@r,@corr,now())";
            run.Parameters.AddWithValue("t", request.TenantId);
            run.Parameters.AddWithValue("c", request.Chunks.Count);
            run.Parameters.AddWithValue("r", replaced);
            run.Parameters.AddWithValue("corr", request.CorrelationId);
            await run.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return new ReindexResult(request.Chunks.Count, replaced, request.CorrelationId);
    }

    private static ProvenanceHandle HandleFor(string kind, string sourceRef) => kind.ToLowerInvariant() switch
    {
        "finding" => ProvenanceHandle.Finding(sourceRef),
        "report"  => ProvenanceHandle.DocumentSection(sourceRef),
        "doc"     => ProvenanceHandle.DocumentSection(sourceRef),
        _         => ProvenanceHandle.Dataset(sourceRef)
    };

    private static float[] ParseEmbedding(string json)
    {
        try { return JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>(); }
        catch { return Array.Empty<float>(); }
    }

    private static double Cosine(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        var n = Math.Min(a.Length, b.Length);
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < n; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private static int Rank(string role)
    {
        var i = Array.IndexOf(RoleRank, (role ?? "").Trim().ToLowerInvariant());
        return i < 0 ? 0 : i;
    }
}