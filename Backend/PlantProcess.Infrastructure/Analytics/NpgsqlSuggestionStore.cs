using System.Text.Json;
using Npgsql;
using PlantProcess.Application.Analytics.Suggestions;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Infrastructure.Analytics;

/// <summary>
/// T-047/T-049: tenant-scoped persistence for suggestions. Upsert keyed on (tenant, suggestion_key):
/// create new, update changed in place, dismiss rows whose key is no longer generated. Workflow
/// transitions are RBAC-checked (pure rules in SuggestionWorkflow) and audited before/after.
/// </summary>
public sealed class NpgsqlSuggestionStore : ISuggestionStore
{
    private readonly NpgsqlDataSource _dataSource;
    public NpgsqlSuggestionStore(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<SuggestionSyncResult> SyncAsync(Guid tenantId, IReadOnlyList<SuggestionCard> generated, string correlationId, CancellationToken ct)
    {
        int created = 0, updated = 0, dismissed = 0;
        var generatedKeys = generated.Select(c => c.SuggestionKey).ToHashSet(StringComparer.Ordinal);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        foreach (var card in generated)
        {
            var evidenceJson = JsonSerializer.Serialize(card.EvidenceHandles.Select(h => new { kind = h.Kind.ToString(), id = h.Id, detail = h.Detail }));
            var findingsJson = JsonSerializer.Serialize(card.SourceFindingRefs);

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO canon.suggestion (id, tenant_id, suggestion_key, title, action_type, status, confidence, " +
                "impact_eur_low, impact_eur_high, evidence, honesty_text, source_findings) " +
                "VALUES (@id,@t,@key,@title,@action,'open',@conf,@low,@high,@ev::jsonb,@honest,@find::jsonb) " +
                "ON CONFLICT (tenant_id, suggestion_key) WHERE suggestion_key IS NOT NULL AND status NOT IN ('dismissed','closed','rejected') " +
                "DO UPDATE SET title=EXCLUDED.title, action_type=EXCLUDED.action_type, confidence=EXCLUDED.confidence, " +
                "impact_eur_low=EXCLUDED.impact_eur_low, impact_eur_high=EXCLUDED.impact_eur_high, " +
                "evidence=EXCLUDED.evidence, honesty_text=EXCLUDED.honesty_text, source_findings=EXCLUDED.source_findings, updated_at=now() " +
                "RETURNING (xmax = 0) AS inserted";
            cmd.Parameters.AddWithValue("id", card.Id);
            cmd.Parameters.AddWithValue("t", tenantId);
            cmd.Parameters.AddWithValue("key", card.SuggestionKey);
            cmd.Parameters.AddWithValue("title", card.Title);
            cmd.Parameters.AddWithValue("action", card.ActionType);
            cmd.Parameters.AddWithValue("conf", (decimal)card.Confidence);
            cmd.Parameters.AddWithValue("low", (object?)card.ImpactLow ?? DBNull.Value);
            cmd.Parameters.AddWithValue("high", (object?)card.ImpactHigh ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ev", evidenceJson);
            cmd.Parameters.AddWithValue("honest", card.HonestyText);
            cmd.Parameters.AddWithValue("find", findingsJson);
            var inserted = await cmd.ExecuteScalarAsync(ct) as bool? ?? false;
            if (inserted) created++; else updated++;
        }

        // Dismiss active suggestions whose key is no longer generated (superseded).
        await using (var stale = conn.CreateCommand())
        {
            stale.Transaction = tx;
            stale.CommandText =
                "SELECT id, suggestion_key FROM canon.suggestion " +
                "WHERE tenant_id=@t AND suggestion_key IS NOT NULL AND status NOT IN ('dismissed','closed','rejected')";
            stale.Parameters.AddWithValue("t", tenantId);
            var toDismiss = new List<Guid>();
            await using (var reader = await stale.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var key = reader.GetString(1);
                    if (!generatedKeys.Contains(key)) toDismiss.Add(reader.GetGuid(0));
                }
            }
            foreach (var id in toDismiss)
            {
                await using var upd = conn.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = "UPDATE canon.suggestion SET status='dismissed', updated_at=now() WHERE id=@id AND tenant_id=@t";
                upd.Parameters.AddWithValue("id", id);
                upd.Parameters.AddWithValue("t", tenantId);
                await upd.ExecuteNonQueryAsync(ct);

                await using var aud = conn.CreateCommand();
                aud.Transaction = tx;
                aud.CommandText = "INSERT INTO canon.suggestion_audit (tenant_id, suggestion_id, actor, from_status, to_status, note) " +
                                  "VALUES (@t,@id,'suggestion-job',NULL,'dismissed',@n)";
                aud.Parameters.AddWithValue("t", tenantId);
                aud.Parameters.AddWithValue("id", id);
                aud.Parameters.AddWithValue("n", $"superseded; correlation {correlationId}");
                await aud.ExecuteNonQueryAsync(ct);
                dismissed++;
            }
        }

        await tx.CommitAsync(ct);
        return new SuggestionSyncResult(created, updated, dismissed, correlationId);
    }

    public async Task<IReadOnlyList<SuggestionCard>> ListActiveAsync(Guid tenantId, CancellationToken ct)
    {
        var cards = new List<SuggestionCard>();
        await using var cmd = _dataSource.CreateCommand(
            "SELECT id, COALESCE(suggestion_key,''), title, action_type, status, confidence, impact_eur_low, impact_eur_high, evidence, honesty_text, source_findings " +
            "FROM canon.suggestion WHERE tenant_id=@t AND status NOT IN ('dismissed','closed','rejected') " +
            "ORDER BY COALESCE(impact_eur_high,0) DESC, confidence DESC, suggestion_key ASC");
        cmd.Parameters.AddWithValue("t", tenantId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var handles = ParseHandles(reader.GetString(8));
            var findings = JsonSerializer.Deserialize<List<string>>(reader.GetString(10)) ?? new();
            cards.Add(new SuggestionCard(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                handles,
                reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                (double)reader.GetDecimal(5),
                reader.GetString(9),
                findings,
                Enum.TryParse<SuggestionStatus>(reader.GetString(4), true, out var st) ? st : SuggestionStatus.Open));
        }
        return cards;
    }

    public async Task<TransitionDecision> TransitionAsync(Guid tenantId, Guid suggestionId, SuggestionStatus to, string role, string actor, string? note, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        SuggestionStatus from;
        await using (var read = conn.CreateCommand())
        {
            read.Transaction = tx;
            read.CommandText = "SELECT status FROM canon.suggestion WHERE id=@id AND tenant_id=@t FOR UPDATE";
            read.Parameters.AddWithValue("id", suggestionId);
            read.Parameters.AddWithValue("t", tenantId);
            var current = await read.ExecuteScalarAsync(ct) as string;
            if (current is null) { await tx.RollbackAsync(ct); return new TransitionDecision(false, "Suggestion not found for this tenant."); }
            from = Enum.TryParse<SuggestionStatus>(current, true, out var st) ? st : SuggestionStatus.Open;
        }

        var decision = SuggestionWorkflow.CanTransition(from, to, role);
        if (!decision.Allowed) { await tx.RollbackAsync(ct); return decision; }

        await using (var upd = conn.CreateCommand())
        {
            upd.Transaction = tx;
            upd.CommandText = "UPDATE canon.suggestion SET status=@s, updated_at=now() WHERE id=@id AND tenant_id=@t";
            upd.Parameters.AddWithValue("s", to.ToString().ToLowerInvariant());
            upd.Parameters.AddWithValue("id", suggestionId);
            upd.Parameters.AddWithValue("t", tenantId);
            await upd.ExecuteNonQueryAsync(ct);
        }
        await using (var aud = conn.CreateCommand())
        {
            aud.Transaction = tx;
            aud.CommandText = "INSERT INTO canon.suggestion_audit (tenant_id, suggestion_id, actor, from_status, to_status, note) " +
                              "VALUES (@t,@id,@a,@from,@to,@n)";
            aud.Parameters.AddWithValue("t", tenantId);
            aud.Parameters.AddWithValue("id", suggestionId);
            aud.Parameters.AddWithValue("a", string.IsNullOrWhiteSpace(actor) ? "system" : actor);
            aud.Parameters.AddWithValue("from", from.ToString().ToLowerInvariant());
            aud.Parameters.AddWithValue("to", to.ToString().ToLowerInvariant());
            aud.Parameters.AddWithValue("n", (object?)note ?? DBNull.Value);
            await aud.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return decision;
    }

    private static IReadOnlyList<ProvenanceHandle> ParseHandles(string json)
    {
        var result = new List<ProvenanceHandle>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                var kind = Enum.TryParse<ProvenanceKind>(e.GetProperty("kind").GetString(), true, out var k) ? k : ProvenanceKind.Finding;
                var id = e.GetProperty("id").GetString() ?? "";
                var detail = e.TryGetProperty("detail", out var d) ? d.GetString() : null;
                result.Add(new ProvenanceHandle(kind, id, detail));
            }
        }
        catch { /* malformed evidence -> empty; card renders as unsupported on the client */ }
        return result;
    }
}