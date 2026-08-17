using Npgsql;
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Infrastructure.Assistant;

/// <summary>
/// PR-050-01. The T-073 WidgetResult evidence write, moved out of
/// WidgetResultChunkProducer and behind IWidgetResultEvidenceWriter.
///
/// The SQL is unchanged from the private method it replaces: the same INSERT,
/// the same ON CONFLICT (tenant_id, result_fingerprint) DO NOTHING, and the
/// same fallback SELECT when the conflict arm swallowed the insert. That is
/// deliberate. A refactor that also changed the statement would have made the
/// evidence identity of an existing installation impossible to reason about.
///
/// Fingerprints are derived HERE rather than by each caller, so two callers
/// cannot drift into two determinism rules for one table.
/// </summary>
public sealed class NpgsqlWidgetResultEvidenceWriter : IWidgetResultEvidenceWriter
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlWidgetResultEvidenceWriter(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<Guid?> WriteAsync(WidgetResultEvidenceWriteRequest request, CancellationToken cancellationToken)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        // Fail loudly rather than persist an unnameable execution. Reaching
        // this line with a blank code is a caller defect, and a caller that
        // cannot name its widget is required to refuse before it gets here.
        if (string.IsNullOrWhiteSpace(request.Identity.PageCode))
            throw new ArgumentException("Widget result evidence requires a page code.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Identity.WidgetCode))
            throw new ArgumentException("Widget result evidence requires a widget code.", nameof(request));

        var filterContextJson = string.IsNullOrWhiteSpace(request.FilterContextJson)
            ? "{}"
            : request.FilterContextJson;

        var queryFingerprint  = WidgetResultEvidence.QueryFingerprint(request.Identity, filterContextJson);
        var resultFingerprint = WidgetResultEvidence.ResultFingerprint(queryFingerprint, request.Result);
        var resultJson        = WidgetResultEvidenceJson.Serialize(request.Identity, request.Result);

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);

        await using (var insert = conn.CreateCommand())
        {
            insert.CommandText =
                "INSERT INTO canon.assistant_widget_result " +
                "(tenant_id, page_code, widget_code, widget_definition_id, query_fingerprint, " +
                " generated_at_utc, filter_context_json, population_count, result_json, result_fingerprint) " +
                "VALUES (@tenant, @page, @widget, @definition, @queryFingerprint, " +
                "        @generated, @filters::jsonb, @observations, @result::jsonb, @resultFingerprint) " +
                "ON CONFLICT (tenant_id, result_fingerprint) DO NOTHING " +
                "RETURNING id";

            insert.Parameters.AddWithValue("tenant", request.TenantId);
            insert.Parameters.AddWithValue("page", request.Identity.PageCode);
            insert.Parameters.AddWithValue("widget", request.Identity.WidgetCode);
            insert.Parameters.AddWithValue("definition", (object?)request.Identity.WidgetDefinitionId ?? DBNull.Value);
            insert.Parameters.AddWithValue("queryFingerprint", queryFingerprint);
            insert.Parameters.AddWithValue("generated", request.GeneratedAtUtc);
            insert.Parameters.AddWithValue("filters", filterContextJson);
            insert.Parameters.AddWithValue("observations", request.Result.ObservationCountTotal);
            insert.Parameters.AddWithValue("result", resultJson);
            insert.Parameters.AddWithValue("resultFingerprint", resultFingerprint);

            var inserted = await insert.ExecuteScalarAsync(cancellationToken);
            if (inserted is Guid insertedId) return insertedId;
        }

        await using var existing = conn.CreateCommand();
        existing.CommandText =
            "SELECT id FROM canon.assistant_widget_result " +
            "WHERE tenant_id = @tenant AND result_fingerprint = @resultFingerprint";
        existing.Parameters.AddWithValue("tenant", request.TenantId);
        existing.Parameters.AddWithValue("resultFingerprint", resultFingerprint);

        return await existing.ExecuteScalarAsync(cancellationToken) is Guid existingId ? existingId : null;
    }
}