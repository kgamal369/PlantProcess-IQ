using Npgsql;
using PlantProcess.Application.Jobs.Targeting;

namespace PlantProcess.Infrastructure.Jobs;

/// <summary>
/// T-065 bridge. THE RETIREMENT GUARD MUST SEE BOTH STORES.
///
/// T-064's lookup asks EF which job_definitions rows target a definition. Once
/// an Analysis Job can declare a target too, that answer became incomplete in a
/// way that is worse than no guard at all: JB04 would return zero dependents,
/// the definition would retire, and the analysis job would keep pointing at
/// something that is gone. A protection that is present and wrong is harder to
/// notice than one that is absent.
///
/// So this composes rather than replaces. The original lookup still answers for
/// job_definitions, unchanged and still the T-064 authority; this adds the
/// compatibility store beside it and unions the two.
///
/// It lives in Infrastructure because IPlantProcessDbContext exposes exactly one
/// member - DbSet&lt;JobDefinition&gt; - with no raw connection, and
/// inspection_jobs has no EF entity. Reaching the compatibility table from
/// Application would have meant widening the persistence contract for a bridge
/// that T-106 deletes.
///
/// JobTargetResolver.AssertNotTargetedByJobsAsync is untouched: it still calls
/// one IJobTargetLookup, still raises the same JB04 error, and the analysis job
/// codes simply appear in the sentence it already builds.
/// </summary>
public sealed class AnalysisAwareJobTargetLookup : IJobTargetLookup
{
    private readonly JobTargetLookup _canonical;
    private readonly NpgsqlDataSource _dataSource;

    public AnalysisAwareJobTargetLookup(JobTargetLookup canonical, NpgsqlDataSource dataSource)
    {
        _canonical = canonical;
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<string>> JobCodesTargetingAsync(
        string targetDefinitionKind,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> canonical =
            await _canonical.JobCodesTargetingAsync(targetDefinitionKind, definitionId, cancellationToken);

        List<string> analysis =
            await AnalysisJobCodesTargetingAsync(targetDefinitionKind, definitionId, cancellationToken);

        // Ordinal-distinct and ordered. A guard whose message reorders itself
        // between calls reads like the dependency set changed when it did not,
        // and a duplicated code would overstate how many things are in the way.
        var union = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string code in canonical) union.Add(code);
        foreach (string code in analysis) union.Add(code);

        return union.ToList();
    }

    private async Task<List<string>> AnalysisJobCodesTargetingAsync(
        string targetDefinitionKind,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        var codes = new List<string>();

        await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();

        command.CommandText =
            "SELECT inspection_job_code " +
            "FROM public.inspection_jobs " +
            "WHERE is_deleted = false " +
            "  AND target_definition_id = @definitionId " +
            "  AND target_definition_kind = @kind " +
            "ORDER BY inspection_job_code";

        command.Parameters.AddWithValue("definitionId", definitionId);
        command.Parameters.Add("kind", NpgsqlTypes.NpgsqlDbType.Varchar).Value = targetDefinitionKind;

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            codes.Add(reader.GetString(0));
        }

        return codes;
    }
}