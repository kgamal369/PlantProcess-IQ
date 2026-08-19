using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs;

/// <summary>
/// T-065 bridge. The job-class derivation, and the one behaviour that matters
/// most about it: it never guesses.
/// </summary>
public sealed class T065AnalysisJobClassTests
{
    [Theory]
    [InlineData("DbLinkImport", JobDefinitionType.DbLinkImport)]
    [InlineData("CanonicalRefresh", JobDefinitionType.CanonicalRefresh)]
    [InlineData("MlParamsVsDefects", JobDefinitionType.MlParamsVsDefects)]
    [InlineData("MlParamsVsDowntime", JobDefinitionType.MlParamsVsDowntime)]
    [InlineData("MlParamsVsKpis", JobDefinitionType.MlParamsVsKpis)]
    [InlineData("MlWeeklyFull", JobDefinitionType.MlWeeklyFull)]
    [InlineData("DataQualityScan", JobDefinitionType.DataQualityScan)]
    [InlineData("RiskScoring", JobDefinitionType.RiskScoring)]
    public void A_committed_catalogue_job_type_maps_to_its_exact_class(string catalogJobType, JobDefinitionType expected)
    {
        Assert.Equal(expected, AnalysisJobClass.FromCatalogJobType(catalogJobType));
    }

    [Fact]
    public void Custom_is_honoured_only_when_the_catalogue_says_Custom()
    {
        Assert.Equal(JobDefinitionType.Custom, AnalysisJobClass.FromCatalogJobType("Custom"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("MlParamsVsSomethingElse")]
    [InlineData("mlparamsvsdefects")]
    [InlineData("99")]
    public void An_unknown_or_absent_job_type_is_refused_and_never_becomes_Custom(string? catalogJobType)
    {
        // The whole point. Every class is Unconstrained today, so a silent
        // fallback would look harmless right up until a class gains a rule and
        // an unrecognised job inherits it.
        var mapped = AnalysisJobClass.FromCatalogJobType(catalogJobType);

        Assert.Null(mapped);
        Assert.NotEqual(JobDefinitionType.Custom, mapped);
    }

    [Fact]
    public void The_refusal_names_the_engine_job_and_the_catalogue_value()
    {
        var message = AnalysisJobClass.UnmappableMessage("ML_SOMETHING", "MlSomething");

        Assert.Contains("ML_SOMETHING", message);
        Assert.Contains("MlSomething", message);
        Assert.Contains("not guessed", message);
    }

    [Fact]
    public void Every_declared_class_is_reachable_from_a_catalogue_value()
    {
        // If a member is added to JobDefinitionType and not mapped here, an
        // analysis job of that class becomes permanently unresolvable. This
        // fails the moment that happens rather than at runtime months later.
        foreach (JobDefinitionType member in Enum.GetValues<JobDefinitionType>())
        {
            Assert.Equal(member, AnalysisJobClass.FromCatalogJobType(member.ToString()));
        }
    }
}