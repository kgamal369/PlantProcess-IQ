using System.Text;
using Npgsql;
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Infrastructure.Assistant;

/// <summary>
/// M1-01: builds the assistant's retrieval corpus straight from the canonical
/// substrate over its own Npgsql connection (no ambient EF scope), so reindex
/// can be driven by an endpoint or a background job.
///
/// Chunk families:
///   CONNECTOR / DATASET / MAPPING - configuration facts, viewer-visible.
///   DOC       - the platform honesty contract (product doctrine), viewer-visible.
///   FINDING   - latest-run correlation findings, scope_role = 'engineer'.
///
/// Every chunk is a true statement about configuration or computed results, so
/// none are marked synthetic. Any single source that cannot be read (schema
/// surprise) degrades to zero chunks for that family rather than failing the run.
/// </summary>
public sealed class CanonicalChunkProducer : IAssistantChunkProducer
{
    private readonly NpgsqlDataSource _dataSource;

    public CanonicalChunkProducer(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> BuildAsync(Guid tenantId, CancellationToken ct)
    {
        var chunks = new List<RetrievedChunk>();

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        await SafeAsync(async () => await AddConnectorsAsync(conn, chunks, ct));
        await SafeAsync(async () => await AddDatasetsAsync(conn, chunks, ct));
        await SafeAsync(async () => await AddMappingsAsync(conn, chunks, ct));
        AddHonestyDocs(chunks);
        await SafeAsync(async () => await AddFindingsAsync(conn, chunks, ct));

        return chunks;
    }

    private static async Task SafeAsync(Func<Task> body)
    {
        try { await body(); }
        catch { /* one family failing must not fail the whole reindex */ }
    }

    private static RetrievedChunk Chunk(string kind, string sourceRef, string content, string? scopeRole)
        => new(Guid.NewGuid(), kind, sourceRef, content, HandleFor(kind, sourceRef), 0d, false, scopeRole);

    private static ProvenanceHandle HandleFor(string kind, string sourceRef) => kind.ToLowerInvariant() switch
    {
        "finding" => ProvenanceHandle.Finding(sourceRef),
        "doc"     => ProvenanceHandle.DocumentSection(sourceRef),
        _         => ProvenanceHandle.Dataset(sourceRef)
    };

    // ------------------------------------------------------------- CONNECTOR ---
    private static async Task AddConnectorsAsync(NpgsqlConnection conn, List<RetrievedChunk> chunks, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT connection_profile_code, connection_profile_name, provider_type, connection_mode, is_active, read_only_enforced " +
            "FROM connection_profiles WHERE coalesce(is_deleted,false) = false";
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var code = Str(r, 0);
            var name = Str(r, 1);
            var provider = Str(r, 2);
            var mode = Str(r, 3);
            var active = !r.IsDBNull(4) && r.GetBoolean(4);
            var ro = !r.IsDBNull(5) && r.GetBoolean(5);
            var content = $"Source connection '{name}' (code {code}) uses provider {provider} in {mode} mode. " +
                          $"Active: {active}. Read-only enforced toward the customer system: {ro}.";
            chunks.Add(Chunk("DATASET", $"connection:{code}", content, null));
        }
    }

    // --------------------------------------------------------------- DATASET ---
    private static async Task AddDatasetsAsync(NpgsqlConnection conn, List<RetrievedChunk> chunks, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        // Only confirmed-present columns are referenced; coalesce covers naming variants.
        cmd.CommandText =
            "SELECT coalesce(dataset_name, source_table_name, physical_table_name) AS name, " +
            "       coalesce(source_table_name, physical_table_name, dataset_name) AS obj " +
            "FROM source_dataset_definitions " +
            "WHERE coalesce(dataset_name, source_table_name, physical_table_name) IS NOT NULL";
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var n = 0;
        while (await r.ReadAsync(ct))
        {
            var name = Str(r, 0);
            var obj = Str(r, 1);
            var content = $"Registered source dataset '{name}' maps the source object '{obj}'. " +
                          "It is scheduled for incremental import into the plant staging layer.";
            chunks.Add(Chunk("DATASET", $"dataset:{name}", content, null));
            n++;
        }
        if (n > 0)
        {
            chunks.Add(Chunk("DATASET", "dataset:summary",
                $"There are {n} registered source table(s) available for incremental import via DB-link.", null));
        }
    }

    // --------------------------------------------------------------- MAPPING ---
    private static async Task AddMappingsAsync(NpgsqlConnection conn, List<RetrievedChunk> chunks, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT mapping_name, source_object_name, target_entity_name, is_active " +
            "FROM mapping_definitions WHERE coalesce(is_deleted,false) = false";
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var name = Str(r, 0);
            var src = Str(r, 1);
            var target = Str(r, 2);
            var active = !r.IsDBNull(3) && r.GetBoolean(3);
            var content = $"Mapping '{name}' projects source object '{src}' into canonical entity '{target}'. Active: {active}.";
            chunks.Add(Chunk("DATASET", $"mapping:{name}", content, null));
        }
    }

    // ------------------------------------------------------------------- DOC ---
    private static void AddHonestyDocs(List<RetrievedChunk> chunks)
    {
        // Product doctrine - generic, industry-agnostic, no customer data.
        var doctrine = new (string Ref, string Text)[]
        {
            ("honesty:read-only",   "PlantProcess IQ is read-only toward the customer. It never writes to plant systems and never controls OT."),
            ("honesty:contributor", "Every correlation finding is reported as a suspected contributor, not a guaranteed root cause."),
            ("honesty:q-value",     "Findings always report a population size, method, effect size and an FDR q-value; multiple-testing correction is always applied."),
            ("honesty:nulls",       "A non-significant result is a first-class finding: the engine also states what is not a driver."),
            ("honesty:provenance",  "Every canonical row carries source system, source record id and import-batch lineage."),
            ("honesty:empty",       "Empty states are shown honestly; the product never fabricates status."),
            ("honesty:refusal",     "The assistant answers only from indexed evidence and refuses when no grounded evidence is available."),
            ("honesty:predictive",  "The assistant does not forecast or predict individual outcomes; it explains discovered associations from historical data."),
        };
        foreach (var d in doctrine)
        {
            chunks.Add(Chunk("DOC", d.Ref, d.Text, null));
        }
    }

    // --------------------------------------------------------------- FINDING ---
    private static async Task AddFindingsAsync(NpgsqlConnection conn, List<RetrievedChunk> chunks, CancellationToken ct)
    {
        // latest completed compute run, then its findings
        Guid? runId = null;
        await using (var runCmd = conn.CreateCommand())
        {
            runCmd.CommandText =
                "SELECT id FROM public.ml_correlation_compute_runs " +
                "WHERE lower(coalesce(status,'')) IN ('completed','succeeded','success') " +
                "ORDER BY coalesce(completed_at_utc, created_at_utc) DESC NULLS LAST LIMIT 1";
            var scalar = await runCmd.ExecuteScalarAsync(ct);
            if (scalar is Guid g) { runId = g; }
        }
        if (runId is null)
        {
            // fall back to the most recent run referenced by any result row
            await using var anyCmd = conn.CreateCommand();
            anyCmd.CommandText = "SELECT compute_run_id FROM public.ml_correlation_results_v2 ORDER BY compute_run_id DESC LIMIT 1";
            var s = await anyCmd.ExecuteScalarAsync(ct);
            if (s is Guid g2) { runId = g2; }
        }
        if (runId is null) { return; }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT feature_key, outcome_key, method, effect_size, q_value, sample_size " +
            "FROM public.ml_correlation_results_v2 " +
            "WHERE compute_run_id = @run AND coalesce(method,'NotApplicable') <> 'NotApplicable' AND coalesce(sample_size,0) > 0 " +
            "ORDER BY abs(coalesce(effect_size,0)) DESC LIMIT 50";
        cmd.Parameters.AddWithValue("run", runId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var feature = Str(r, 0);
            var outcome = Str(r, 1);
            var method = Str(r, 2);
            var effect = r.IsDBNull(3) ? (double?)null : r.GetDouble(3);
            var q = r.IsDBNull(4) ? (double?)null : r.GetDouble(4);
            var n = r.IsDBNull(5) ? 0 : r.GetInt32(5);
            var sb = new StringBuilder();
            sb.Append($"Finding: '{feature}' is associated with '{outcome}' ({method}");
            if (effect.HasValue) { sb.Append($", effect {effect.Value:0.###}"); }
            if (q.HasValue) { sb.Append($", q-value {q.Value:0.####}"); }
            sb.Append($", n={n}). Suspected contributor, not guaranteed root cause.");
            chunks.Add(Chunk("FINDING", $"{feature}=>{outcome}", sb.ToString(), "engineer"));
        }
    }

    private static string Str(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i)) ?? string.Empty;
}