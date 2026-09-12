using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Domain.Common;
using PlantProcess.Infrastructure.Canonical;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// PPIQ T-253. WHAT AN AUTHOR MAY WRITE OUTPUT INTO.
///
/// The rule this file holds is one sentence: A MAPPED ENTITY IS NOT A PROJECTION
/// TARGET. Eligibility is declared by the Domain marker and read through the catalogue,
/// so the picker and the server validator answer from one predicate.
///
/// The context is model-only. It is never opened, nothing is queried, and no database
/// is touched: the relational MODEL is the subject, not any instance of it.
/// </summary>
[Trait("Gate", "CanonicalProjectionTarget")]
public sealed class CanonicalProjectionTargetTests
{
    /// <summary>
    /// The twelve, ordinal-sorted. Written out rather than computed, because a test that
    /// derived the expectation from the same source it checks would prove nothing.
    /// </summary>
    private static readonly string[] Expected =
    {
        "DataQualityIssue",
        "DefectCatalog",
        "DowntimeEvent",
        "GenealogyEdge",
        "MaterialAlias",
        "MaterialUnit",
        "ParameterDefinition",
        "ParameterObservation",
        "ProcessEvent",
        "ProcessStepExecution",
        "QualityEvent",
        "RiskScore",
    };

    /// <summary>
    /// Mapped canonical entities that must NEVER be offered as an output target. Product
    /// plumbing, all of it: an author does not write process output into a job run.
    /// </summary>
    private static readonly string[] MustNotBeTargets =
    {
        "ImportBatch",
        "JobRunHistory",
        "DashboardWidgetDefinition",
        "SourceSystemDefinition",
        "CorrelationResult",
    };

    [Fact]
    public void The_catalogue_enumerates_exactly_the_declared_projection_targets_ordinal_sorted()
    {
        var catalogue = new CanonicalEntityCatalog(BuildContext());

        Assert.Equal(Expected, catalogue.ProjectionTargetNames().ToArray());
    }

    [Fact]
    public void Product_plumbing_is_mapped_and_is_still_not_a_projection_target()
    {
        var catalogue = new CanonicalEntityCatalog(BuildContext());

        foreach (var name in MustNotBeTargets)
        {
            // Mapped: the catalogue resolves it to a type at all.
            Assert.True(
                catalogue.FindType(name) is not null,
                name + " is expected to be a mapped canonical entity; this test's premise has moved.");

            // And still not eligible. That gap IS the rule.
            Assert.False(
                catalogue.IsProjectionTarget(name),
                name + " must never be offered as a governed output target.");
        }
    }

    [Fact]
    public void The_predicate_and_the_enumeration_cannot_disagree()
    {
        var catalogue = new CanonicalEntityCatalog(BuildContext());

        foreach (var name in catalogue.ProjectionTargetNames())
        {
            Assert.True(catalogue.IsProjectionTarget(name));
        }

        Assert.False(catalogue.IsProjectionTarget("   "));
        Assert.False(catalogue.IsProjectionTarget("NotAnEntityAtAll"));
    }

    [Fact]
    public void Every_marked_domain_type_is_a_mapped_entity()
    {
        var catalogue = new CanonicalEntityCatalog(BuildContext());

        var marked = typeof(ICanonicalProjectionTarget).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ICanonicalProjectionTarget).IsAssignableFrom(t))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Expected, marked);

        foreach (var name in marked)
        {
            Assert.True(
                catalogue.FindType(name) is not null,
                name + " declares itself a projection target but the model does not map it.");
        }
    }

    private static PlantProcessDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=model_only;Username=model_only;Password=model_only")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PlantProcessDbContext(options);
    }
}