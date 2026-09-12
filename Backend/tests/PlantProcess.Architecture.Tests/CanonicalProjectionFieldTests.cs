using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Canonical;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// PPIQ T-262. THE FIELDS OF A PROJECTION TARGET COME FROM THE MODEL.
///
/// Model-only: the context is never opened and nothing is queried. The subject is what
/// the mapped model declares, not any instance of it.
/// </summary>
[Trait("Gate", "CanonicalProjectionField")]
public sealed class CanonicalProjectionFieldTests
{
    /// <summary>
    /// Every member BaseEntity declares. Written out so the structural rule can be
    /// checked against something, not to be consulted at runtime - the product derives
    /// this from the declaring type and never from a list.
    /// </summary>
    private static readonly string[] PlatformOwned =
    {
        "Id", "CreatedAtUtc", "UpdatedAtUtc", "IsSynthetic",
        "SourceSystem", "SourceRecordId", "IsDeleted", "DeletedAtUtc", "DeletedReason",
    };

    [Fact]
    public void A_projection_target_reports_its_fields()
    {
        var catalogue = new CanonicalEntityCatalog(BuildContext());

        var fields = catalogue.ProjectionFieldsOf("QualityEvent");

        Assert.NotEmpty(fields);
        Assert.True(fields.Any(f => f.IsAuthorWritable), "a target with no writable field could never be projected into.");
    }

    [Fact]
    public void Platform_fields_are_system_owned_structurally_and_never_author_writable()
    {
        var catalogue = new CanonicalEntityCatalog(BuildContext());

        foreach (var target in catalogue.ProjectionTargetNames())
        {
            foreach (var field in catalogue.ProjectionFieldsOf(target))
            {
                if (PlatformOwned.Contains(field.Name))
                {
                    Assert.True(field.IsSystemOwned,
                        target + "." + field.Name + " is declared by BaseEntity and must be system-owned.");
                    Assert.False(field.IsAuthorWritable);
                }
            }
        }
    }

    [Fact]
    public void Something_that_is_not_a_projection_target_reports_no_fields()
    {
        var catalogue = new CanonicalEntityCatalog(BuildContext());

        // Mapped, and deliberately not a target. Answering with its fields would invite
        // a binding to product plumbing.
        Assert.Empty(catalogue.ProjectionFieldsOf("ImportBatch"));
        Assert.Empty(catalogue.ProjectionFieldsOf("NotAnEntityAtAll"));
        Assert.Empty(catalogue.ProjectionFieldsOf("   "));
    }

    [Fact]
    public void Every_projection_target_answers_and_none_is_empty()
    {
        var catalogue = new CanonicalEntityCatalog(BuildContext());

        foreach (var target in catalogue.ProjectionTargetNames())
        {
            Assert.NotEmpty(catalogue.ProjectionFieldsOf(target));
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