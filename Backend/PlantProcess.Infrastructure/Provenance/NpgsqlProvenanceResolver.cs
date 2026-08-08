using Npgsql;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Infrastructure.Provenance;

/// <summary>
/// Resolves provenance handles against the live PostgreSQL store. Any verification error fails CLOSED
/// (returns Missing) so an unverifiable claim is never rendered.
/// </summary>
public sealed class NpgsqlProvenanceResolver : IProvenanceResolver
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlProvenanceResolver(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<ProvenanceResolution> ResolveAsync(ProvenanceHandle handle, CancellationToken cancellationToken)
    {
        if (handle is null || string.IsNullOrWhiteSpace(handle.Id))
            return ProvenanceResolution.Missing(handle ?? new ProvenanceHandle(ProvenanceKind.Finding, "<null>"), "Handle or id was empty.");

        try
        {
            return handle.Kind switch
            {
                ProvenanceKind.Finding or ProvenanceKind.JobRun => await ResolveRunOrRiskAsync(handle, cancellationToken),
                ProvenanceKind.SourceTable                      => await ResolveSourceTableAsync(handle, cancellationToken),
                ProvenanceKind.Dataset                          => await ResolveDatasetAsync(handle, cancellationToken),
                ProvenanceKind.DocumentSection                  => await ResolveDocumentAsync(handle, cancellationToken),
                ProvenanceKind.WidgetResult                     => await ResolveWidgetResultAsync(handle, cancellationToken),
                _                                               => ProvenanceResolution.Missing(handle, "Unknown provenance kind.")
            };
        }
        catch
        {
            return ProvenanceResolution.Missing(handle, "Provenance could not be verified (resolver error).");
        }
    }

    private async Task<ProvenanceResolution> ResolveRunOrRiskAsync(ProvenanceHandle handle, CancellationToken ct)
    {
        if (!Guid.TryParse(handle.Id, out var id))
            return ProvenanceResolution.Missing(handle, "Finding/JobRun id is not a GUID.");

        await using (var runCmd = _dataSource.CreateCommand("SELECT 1 FROM public.ml_correlation_compute_runs WHERE id = @id"))
        {
            runCmd.Parameters.AddWithValue("id", id);
            if (await runCmd.ExecuteScalarAsync(ct) is not null)
                return ProvenanceResolution.Live(handle, $"ml_correlation_compute_runs:{id}");
        }

        await using (var riskCmd = _dataSource.CreateCommand(
            "SELECT COALESCE(mu.is_synthetic, false) FROM public.risk_scores rs " +
            "JOIN public.material_units mu ON mu.id = rs.material_unit_id WHERE rs.id = @id"))
        {
            riskCmd.Parameters.AddWithValue("id", id);
            await using var reader = await riskCmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var synthetic = !await reader.IsDBNullAsync(0, ct) && reader.GetBoolean(0);
                return synthetic
                    ? ProvenanceResolution.Seed(handle, $"risk_scores:{id}")
                    : ProvenanceResolution.Live(handle, $"risk_scores:{id}");
            }
        }

        return ProvenanceResolution.Missing(handle, "No finding/job-run/risk artifact matched the handle.");
    }

    private async Task<ProvenanceResolution> ResolveSourceTableAsync(ProvenanceHandle handle, CancellationToken ct)
    {
        var parts  = handle.Id.Split('.', 2);
        var schema = parts.Length == 2 ? parts[0] : "public";
        var table  = parts.Length == 2 ? parts[1] : parts[0];

        await using var cmd = _dataSource.CreateCommand(
            "SELECT 1 FROM information_schema.tables WHERE table_schema = @s AND table_name = @t");
        cmd.Parameters.AddWithValue("s", schema);
        cmd.Parameters.AddWithValue("t", table);

        return await cmd.ExecuteScalarAsync(ct) is not null
            ? ProvenanceResolution.Live(handle, $"{schema}.{table}")
            : ProvenanceResolution.Missing(handle, $"Source table '{schema}.{table}' does not exist.");
    }

    private async Task<ProvenanceResolution> ResolveDatasetAsync(ProvenanceHandle handle, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT 1 FROM information_schema.tables WHERE table_name = @t " +
            "UNION SELECT 1 FROM information_schema.views WHERE table_name = @t");
        cmd.Parameters.AddWithValue("t", handle.Id);

        return await cmd.ExecuteScalarAsync(ct) is not null
            ? ProvenanceResolution.Live(handle, $"dataset:{handle.Id}")
            : ProvenanceResolution.Missing(handle, $"Dataset '{handle.Id}' was not found.");
    }

    private async Task<ProvenanceResolution> ResolveDocumentAsync(ProvenanceHandle handle, CancellationToken ct)
    {
        await using (var existsCmd = _dataSource.CreateCommand("SELECT to_regclass('public.document_sections') IS NOT NULL"))
        {
            var available = await existsCmd.ExecuteScalarAsync(ct) as bool? ?? false;
            if (!available)
                return ProvenanceResolution.Missing(handle, "Document store is not available yet (arrives in a later phase).");
        }

        await using var cmd = _dataSource.CreateCommand("SELECT 1 FROM public.document_sections WHERE id::text = @id");
        cmd.Parameters.AddWithValue("id", handle.Id);
        return await cmd.ExecuteScalarAsync(ct) is not null
            ? ProvenanceResolution.Live(handle, $"document_sections:{handle.Id}")
            : ProvenanceResolution.Missing(handle, "Document section was not found.");
    }

    /// <summary>
    /// T-073. Resolves one exact widget execution snapshot.
    ///
    /// Existence only, and that is deliberate: this method returns an artifact
    /// reference, never the snapshot's rows. Content is reachable solely through
    /// the tenant-filtered read paths, because this interface carries no tenant
    /// for any kind. Fails CLOSED like every other branch.
    /// </summary>
    private async Task<ProvenanceResolution> ResolveWidgetResultAsync(ProvenanceHandle handle, CancellationToken ct)
    {
        if (!Guid.TryParse(handle.Id, out var id))
            return ProvenanceResolution.Missing(handle, "Widget result id is not a GUID.");

        await using (var existsCmd = _dataSource.CreateCommand("SELECT to_regclass('canon.assistant_widget_result') IS NOT NULL"))
        {
            var available = await existsCmd.ExecuteScalarAsync(ct) as bool? ?? false;
            if (!available)
                return ProvenanceResolution.Missing(handle, "Widget result evidence store is not installed.");
        }

        await using var cmd = _dataSource.CreateCommand(
            "SELECT result_fingerprint FROM canon.assistant_widget_result WHERE id = @id");
        cmd.Parameters.AddWithValue("id", id);

        var fingerprint = await cmd.ExecuteScalarAsync(ct) as string;

        return string.IsNullOrWhiteSpace(fingerprint)
            ? ProvenanceResolution.Missing(handle, "No widget result snapshot matched the handle.")
            : ProvenanceResolution.Live(handle, $"assistant_widget_result:{id}");
    }
}