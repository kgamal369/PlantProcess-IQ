using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Targeting;

/// <summary>
/// T-065 bridge. WHICH JOB CLASS AN ANALYSIS DEFINITION RUNS AS.
///
/// The resolver asks for a JobDefinitionType. An analysis definition lives in
/// the compatibility store and carries no class of its own, so the class is
/// derived from the committed engine catalogue: the analysis definition names an
/// engine job code, ml_learning_job_catalog_v1 gives that code a job_type, and
/// the job_type is the enum member. That is a reuse of a committed mapping, not
/// a new authority.
///
/// AN UNKNOWN VALUE IS REFUSED, NEVER DEFAULTED.
///
/// Falling back to Custom would look harmless today because every class is
/// currently Unconstrained. It would stop being harmless the moment Custom - or
/// anything an unknown value happened to land on - gains a target rule: an
/// unrecognised job would quietly acquire whatever policy that class carries.
/// A named refusal now costs one message; a silent default costs a policy
/// bypass nobody goes looking for.
/// </summary>
public static class AnalysisJobClass
{
    /// <summary>
    /// Maps a committed catalogue job_type to its enum member. Returns null when
    /// the value is absent or unrecognised; the caller must refuse rather than
    /// choose a class on the operator's behalf.
    /// </summary>
    public static JobDefinitionType? FromCatalogJobType(string? catalogJobType)
    {
        if (string.IsNullOrWhiteSpace(catalogJobType))
        {
            return null;
        }

        // Exact, case-sensitive, and deliberately not Enum.TryParse with
        // ignoreCase: the catalogue values are committed strings, and a loose
        // parse would accept a numeric literal as a class.
        return catalogJobType.Trim() switch
        {
            "DbLinkImport" => JobDefinitionType.DbLinkImport,
            "CanonicalRefresh" => JobDefinitionType.CanonicalRefresh,
            "MlParamsVsDefects" => JobDefinitionType.MlParamsVsDefects,
            "MlParamsVsDowntime" => JobDefinitionType.MlParamsVsDowntime,
            "MlParamsVsKpis" => JobDefinitionType.MlParamsVsKpis,
            "MlWeeklyFull" => JobDefinitionType.MlWeeklyFull,
            "DataQualityScan" => JobDefinitionType.DataQualityScan,
            "RiskScoring" => JobDefinitionType.RiskScoring,
            "Custom" => JobDefinitionType.Custom,
            _ => null
        };
    }

    /// <summary>The sentence shown when the catalogue cannot name a class.</summary>
    public static string UnmappableMessage(string? engineJobCode, string? catalogJobType)
    {
        string code = string.IsNullOrWhiteSpace(engineJobCode) ? "<none>" : engineJobCode!;
        string type = string.IsNullOrWhiteSpace(catalogJobType) ? "<none>" : catalogJobType!;

        return "This analysis job runs engine job '" + code + "', whose catalogue job type is '"
             + type + "'. That is not a declared job class, so the target cannot be resolved. "
             + "The class is not guessed, because a guessed class would inherit whatever target "
             + "policy that class later carries.";
    }
}