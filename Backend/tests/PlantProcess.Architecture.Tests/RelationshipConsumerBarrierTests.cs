using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Relationships;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// The permanent barrier. Sixteen consumer families, one path authority, and no lawful
/// way for a seventeenth idea to appear.
///
/// T-096 removed the second path authority and routed the first eight families through
/// the canonical one. This finishes the job for the remaining eight and, more
/// importantly, makes the arrangement durable: the failure this guards against is not a
/// mistake anyone is making today, it is the one a future author makes six months from
/// now when a join is needed and the resolver seems like a detour.
///
/// WHAT IS FORBIDDEN, and only this:
///
///   reaching relationship persistence directly;
///   injecting the relationship store;
///   searching for a path;
///   choosing a preferred path;
///   inferring a relationship from names;
///   reading foreign-key metadata to decide how two subjects connect;
///   quietly joining anyway after the authority has refused.
///
/// WHAT IS EXPLICITLY ALLOWED, because forbidding it would be absurd:
///
///   intrinsic navigation on a row's own owning key;
///   fixed joins between known canonical entities;
///   lookups by an identity already resolved;
///   packaging and hashing evidence.
///
/// That distinction is the whole difficulty. A consumer that writes
/// `x.MaterialUnitId == materialUnitId` has not chosen anything - the key is the row's
/// own. A consumer that works out WHICH key connects two subjects has chosen, and that
/// decision belongs to the relationship model where a plant can publish, prefer and
/// retire it. The guard below is written to catch the second without ever flagging the
/// first, because a barrier that cries wolf is a barrier someone eventually deletes.
/// </summary>
[Trait("BacklogTask", "T-097")]
[Trait("Gate", "RelationshipConsumerBarrier")]
public sealed class RelationshipConsumerBarrierTests
{
    // ------------------------------------------------------------------------
    // The sixteen families, by the source they occupy. A family with no engine
    // yet still appears: its directory may not exist, and the guard covering it
    // is what stops the engine being written around the model when it does.
    // ------------------------------------------------------------------------
    private static readonly IReadOnlyDictionary<string, string[]> ConsumerFamilies =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            // Routed by T-096.
            ["Projection"] = new[] { @"Backend\PlantProcess.Application\Dashboarding\Services\Dimensions" },
            ["QueryCompiler"] = new[] { @"Backend\PlantProcess.Application\Dashboarding\Services\Queries" },
            ["AssociativeFiltering"] = new[] { @"Backend\PlantProcess.Application\Dashboarding\Services\Queries" },
            ["DrillDown"] = new[] { @"Backend\PlantProcess.Application\Dashboarding\Services\Queries" },
            ["DrillThrough"] = new[] { @"Backend\PlantProcess.Application\Dashboarding\Services\Queries" },
            ["Genealogy"] = new[] { @"Backend\PlantProcess.Application\Services\Materials" },
            ["StatisticalAnalysis"] = new[] { @"Backend\PlantProcess.Application\Analytics\Services" },
            ["Correlation"] = new[]
            {
                @"Backend\PlantProcess.Application\Analytics\Engines",
                @"Backend\PlantProcess.Application\Analytics\Advanced"
            },

            // Covered by T-097.
            ["FeatureEngineering"] = new[]
            {
                @"Backend\PlantProcess.Application\Analytics\Services",
                @"Backend\PlantProcess.Application\Analytics\Interfaces"
            },
            ["ModelTraining"] = new[] { @"Backend\PlantProcess.Application\Analytics\Training" },
            ["Prediction"] = new[]
            {
                @"Backend\PlantProcess.Application\Analytics\Contracts",
                @"Backend\PlantProcess.Application\Analytics\Prediction"
            },
            ["PracticeLearning"] = new[] { @"Backend\PlantProcess.Application\Analytics\Practice" },
            ["RemediationSearch"] = new[] { @"Backend\PlantProcess.Application\Analytics\Remediation" },
            ["ValueCalculation"] = new[] { @"Backend\PlantProcess.Application\Analytics\Value" },
            ["Assistant"] = new[]
            {
                @"Backend\PlantProcess.Application\Assistant",
                @"Backend\PlantProcess.Application\AssistantRuntime"
            },
            ["Evidence"] = new[]
            {
                @"Backend\PlantProcess.Application\Provenance",
                @"Backend\PlantProcess.Application\Analytics\Value"
            }
        };

    /// <summary>
    /// Each token is a way of deciding a semantic path, and none of them is a way of
    /// following a key you were given. They are matched against comment-stripped source
    /// so that describing the rule is never mistaken for breaking it.
    /// </summary>
    private static readonly (string Token, string Why)[] ForbiddenTokens =
    {
        ("plant_relationships", "reads relationship persistence directly"),
        ("plant_relationship_members", "reads relationship persistence directly"),
        ("plant_relationship_paths", "reads relationship persistence directly"),
        ("IRelationshipStore", "injects the relationship store instead of depending on the resolver"),
        ("NpgsqlRelationshipStore", "names the concrete relationship store"),
        ("GetForeignKeys(", "reads foreign-key metadata to decide how two subjects connect"),
        ("IsPreferredPath", "decides preference itself; preference is governed data"),
        ("ShortestPath", "searches for a path instead of asking for one"),
        ("BreadthFirst", "searches for a path instead of asking for one")
    };

    private static string RepositoryRoot() => ScopeAwareGenericity.RepositoryRoot();

    private static IEnumerable<(string File, int Line, string Token, string Why)> Violations(IEnumerable<string> directories)
    {
        foreach (var relative in directories)
        {
            var directory = Path.Combine(RepositoryRoot(), relative);
            if (!Directory.Exists(directory)) continue;

            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(@"\bin\", StringComparison.Ordinal) || file.Contains(@"\obj\", StringComparison.Ordinal)) continue;

                var lineNumber = 0;
                foreach (var line in File.ReadLines(file))
                {
                    lineNumber++;
                    var code = line.TrimStart();
                    if (code.StartsWith("//", StringComparison.Ordinal) || code.StartsWith("*", StringComparison.Ordinal)) continue;

                    foreach (var (token, why) in ForbiddenTokens)
                    {
                        if (code.Contains(token, StringComparison.Ordinal))
                            yield return (file, lineNumber, token, why);
                    }
                }
            }
        }
    }

    private static void FamilyIsClean(string family)
    {
        var violations = Violations(ConsumerFamilies[family]).ToList();

        Assert.True(violations.Count == 0, family + " may not decide a semantic path: " +
            string.Join("; ", violations.Select(v => Path.GetFileName(v.File) + ":" + v.Line + " " + v.Token + " - " + v.Why)));
    }

    // ========================================================================
    // The barrier itself.
    // ========================================================================

    [Fact]
    public void Every_consumer_family_in_the_catalogue_is_covered_by_this_guard()
    {
        // Explore is the manual exception and RegistryGeneration is not a traversal
        // consumer. Everything else is a family that could one day want a join, and
        // every one of them must be inside the guard rather than beside it.
        var purposes = RelationshipConsumerPurposes.All
            .Where(p => p != RelationshipConsumerPurposes.Explore)
            .Where(p => p != RelationshipConsumerPurposes.RegistryGeneration)
            .ToList();

        Assert.Equal(16, purposes.Count);
        Assert.Equal(16, ConsumerFamilies.Count);
    }

    [Fact]
    public void No_consumer_family_reaches_relationship_persistence_or_decides_a_path()
    {
        var violations = Violations(ConsumerFamilies.Values.SelectMany(v => v).Distinct(StringComparer.Ordinal)).ToList();

        Assert.True(violations.Count == 0, "the relationship authority is being bypassed: " +
            string.Join("; ", violations.Select(v => Path.GetFileName(v.File) + ":" + v.Line + " " + v.Token + " - " + v.Why)));
    }

    [Fact]
    public void Intrinsic_navigation_and_evidence_packaging_are_not_flagged()
    {
        // A barrier that flags ordinary code is one somebody eventually deletes, so the
        // absence of false positives is asserted rather than hoped for. All four of
        // these appear in families the guard covers, and none is a path decision.
        var innocent = new[]
        {
            "var rows = _dbContext.ParameterObservations.Where(x => x.MaterialUnitId == materialUnitId);",
            "join material in _dbContext.MaterialUnits on observation.MaterialUnitId equals material.Id",
            "var lookup = await _dbContext.Equipment.Where(x => equipmentIds.Contains(x.Id)).ToListAsync();",
            "builder.Append(string.Join(\"\\u001f\", result.Columns));"
        };

        foreach (var line in innocent)
        {
            foreach (var (token, _) in ForbiddenTokens)
                Assert.False(line.Contains(token, StringComparison.Ordinal), "false positive on: " + line);
        }
    }

    [Fact]
    public void The_only_path_authority_is_the_resolver_and_it_is_reachable()
    {
        var application = typeof(RelationshipResolver).Assembly;

        var resolvers = application.GetTypes()
            .Where(t => typeof(IRelationshipResolver).IsAssignableFrom(t) && !t.IsInterface)
            .ToList();

        Assert.Single(resolvers);
        Assert.Equal(typeof(RelationshipResolver), resolvers[0]);

        // And the refusals a consumer must be able to receive are the frozen three.
        Assert.Equal("RL01", RelationshipRefusalCodes.AmbiguousPath);
        Assert.Equal("RL02", RelationshipRefusalCodes.UnprovenRelationship);
        Assert.Equal("RL03", RelationshipRefusalCodes.NoPath);
    }

    [Fact]
    public void An_automated_purpose_cannot_traverse_an_unproven_relationship()
    {
        // The RL02 boundary is the reason a future engine cannot quietly use an
        // unvalidated relationship: only manual exploration may.
        foreach (var purpose in RelationshipConsumerPurposes.All.Where(p => p != RelationshipConsumerPurposes.Explore))
            Assert.True(RelationshipConsumerPurposes.IsAutomated(purpose), purpose + " must be automated");

        Assert.False(RelationshipConsumerPurposes.IsAutomated(RelationshipConsumerPurposes.Explore));
    }

    // ========================================================================
    // The eight named T-097 proofs. Each says what is true of that family now,
    // not what would be convenient for a test name.
    // ========================================================================

    [Fact]
    public void Proof_1_FeatureEngineering_follows_a_resolved_identity_and_selects_no_path()
    {
        // Its whole contract is a key it was handed. There is no second subject to
        // connect to, so there is nothing for a resolver to decide and injecting one
        // would be ceremony.
        var method = typeof(IFeatureEngineeringService)
            .GetMethod(nameof(IFeatureEngineeringService.BuildMaterialFeatureVectorAsync))!;

        Assert.Contains(method.GetParameters(), p => p.ParameterType == typeof(Guid));
        FamilyIsClean("FeatureEngineering");
    }

    [Fact]
    public void Proof_2_ModelTraining_has_no_engine_and_its_future_home_is_already_guarded()
    {
        Assert.Contains(RelationshipConsumerPurposes.ModelTraining, RelationshipConsumerPurposes.All);
        FamilyIsClean("ModelTraining");
    }

    [Fact]
    public void Proof_3_Prediction_is_contracts_only_and_cannot_grow_a_private_path_authority()
    {
        Assert.Contains(RelationshipConsumerPurposes.ModelScoring, RelationshipConsumerPurposes.All);
        Assert.Contains(RelationshipConsumerPurposes.PredictionAndRemediation, RelationshipConsumerPurposes.All);
        FamilyIsClean("Prediction");
    }

    [Fact]
    public void Proof_4_PracticeLearning_has_no_engine_and_its_future_home_is_already_guarded()
    {
        Assert.Contains(RelationshipConsumerPurposes.PracticeLearning, RelationshipConsumerPurposes.All);
        FamilyIsClean("PracticeLearning");
    }

    [Fact]
    public void Proof_5_RemediationSearch_has_no_engine_and_its_future_home_is_already_guarded()
    {
        Assert.Contains(RelationshipConsumerPurposes.PredictionAndRemediation, RelationshipConsumerPurposes.All);
        FamilyIsClean("RemediationSearch");
    }

    [Fact]
    public void Proof_6_ValueCalculation_has_no_engine_and_its_future_home_is_already_guarded()
    {
        Assert.Contains(RelationshipConsumerPurposes.ValueCalculation, RelationshipConsumerPurposes.All);
        FamilyIsClean("ValueCalculation");
    }

    [Fact]
    public void Proof_7_Assistant_retrieval_ranks_what_it_was_given_and_is_protected_from_outside()
    {
        // Worker 3 owns this runtime. It is not edited here and does not need to be:
        // retrieval receives candidates and a budget, and packaging is not traversal.
        // The protection is external, which is the correct shape for a boundary.
        Assert.Contains(RelationshipConsumerPurposes.AssistantRetrieval, RelationshipConsumerPurposes.All);
        FamilyIsClean("Assistant");
    }

    [Fact]
    public void Proof_8_Evidence_carries_and_hashes_findings_rather_than_connecting_subjects()
    {
        Assert.Contains(RelationshipConsumerPurposes.EvidenceWalkBack, RelationshipConsumerPurposes.All);
        FamilyIsClean("Evidence");
    }
}
