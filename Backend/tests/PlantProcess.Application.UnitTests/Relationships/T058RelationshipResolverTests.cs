using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Relationships;
using Xunit;

namespace PlantProcess.Application.UnitTests.Relationships;

/// <summary>
/// T-058. The resolver and its first real consumer, proven against an
/// in-memory published model.
///
/// No table name appears here either. The resolver reads the relationship
/// SERVICE, so a test that stands in for the service is standing in exactly
/// where the real one sits - and everything asserted below survives T-095
/// unchanged.
/// </summary>
public sealed class T058RelationshipResolverTests
{
    /// <summary>A published model, standing in for the service the resolver actually reads.</summary>
    private sealed class PublishedModel : IRelationshipService
    {
        private readonly List<RelationshipDto> _published;
        public PublishedModel(params RelationshipDto[] published) => _published = published.ToList();

        public void Retire(string code) =>
            _published.RemoveAll(r => r.RelationshipCode == code);

        public Task<ApplicationResult<IReadOnlyList<RelationshipDto>>> GetPublishedAsync(string? entity, CancellationToken ct)
        {
            IReadOnlyList<RelationshipDto> rows = _published
                .Where(r => entity is null || r.LeftEntity == entity || r.RightEntity == entity)
                .ToList();
            return Task.FromResult(ApplicationResult<IReadOnlyList<RelationshipDto>>.Success(rows));
        }

        public Task<ApplicationResult<RelationshipDto>> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var found = _published.FirstOrDefault(r => r.Id == id);
            return Task.FromResult(found is null
                ? ApplicationResult<RelationshipDto>.Failure(ApplicationError.NotFound("gone"))
                : ApplicationResult<RelationshipDto>.Success(found));
        }

        public Task<ApplicationResult<IReadOnlyList<RelationshipEntityDto>>> GetEntitiesAsync(CancellationToken ct) =>
            Task.FromResult(ApplicationResult<IReadOnlyList<RelationshipEntityDto>>.Success(
                (IReadOnlyList<RelationshipEntityDto>)new List<RelationshipEntityDto>()));
    }

    private static RelationshipDto Rel(
        string code, string left, string right,
        bool preferred = false,
        string validation = RelationshipValidationStates.Validated,
        string grainLeft = "unit", string grainRight = "unit",
        string? attribution = null) =>
        new(Guid.NewGuid(), code, left, right,
            RelationshipJoinTypes.Inner, RelationshipCardinalities.OneToMany,
            grainLeft, grainRight,
            !string.Equals(grainLeft, grainRight, StringComparison.Ordinal),
            attribution, null, preferred,
            RelationshipAmbiguityStates.Unambiguous, validation,
            Guid.NewGuid(), 1, DateTime.UtcNow, null,
            new List<RelationshipMemberDto>
            {
                new("site_key", "site_ref", 0),
                new("unit_key", "unit_ref", 1)
            });

    private static (RelationshipResolver Resolver, RelationshipJoinPlanner Planner, PublishedModel Model) Build(
        params RelationshipDto[] published)
    {
        var model = new PublishedModel(published);
        var resolver = new RelationshipResolver(model);
        return (resolver, new RelationshipJoinPlanner(resolver, model), model);
    }

    private const string Compiler = RelationshipConsumerPurposes.QueryCompiler;
    private const string Explore = RelationshipConsumerPurposes.Explore;

    // ---- resolution --------------------------------------------------------

    [Fact]
    public async Task A_single_declared_path_resolves()
    {
        var (resolver, _, _) = Build(Rel("R1", "a", "b"));

        var result = await resolver.ResolveAsync("a", "b", Compiler, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Resolved);
        Assert.Single(result.Value!.Path);
        Assert.Null(result.Value!.RefusalCode);
    }

    [Fact]
    public async Task A_relationship_resolves_in_both_directions()
    {
        var (resolver, _, _) = Build(Rel("R1", "a", "b"));

        var forward = await resolver.ResolveAsync("a", "b", Compiler, CancellationToken.None);
        var backward = await resolver.ResolveAsync("b", "a", Compiler, CancellationToken.None);

        Assert.True(forward.Value!.Resolved);
        Assert.True(backward.Value!.Resolved);
        Assert.Equal("b", forward.Value!.Path[0].ToEntity);
        Assert.Equal("a", backward.Value!.Path[0].ToEntity);
    }

    [Fact]
    public async Task A_multi_hop_path_resolves_in_order()
    {
        var (resolver, _, _) = Build(Rel("R1", "a", "b"), Rel("R2", "b", "c"));

        var result = await resolver.ResolveAsync("a", "c", Compiler, CancellationToken.None);

        Assert.True(result.Value!.Resolved);
        Assert.Equal(2, result.Value!.Path.Count);
        Assert.Equal("a", result.Value!.Path[0].FromEntity);
        Assert.Equal("b", result.Value!.Path[0].ToEntity);
        Assert.Equal("c", result.Value!.Path[1].ToEntity);
    }

    // ---- RL03 --------------------------------------------------------------

    [Fact]
    public async Task No_declared_path_is_refused_as_RL03_rather_than_guessed()
    {
        var (resolver, _, _) = Build(Rel("R1", "a", "b"));

        var result = await resolver.ResolveAsync("a", "z", Compiler, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Resolved);
        Assert.Equal(RelationshipRefusalCodes.NoPath, result.Value!.RefusalCode);
        Assert.Empty(result.Value!.Path);
    }

    // ---- RL01 --------------------------------------------------------------

    [Fact]
    public async Task Two_equal_paths_are_refused_as_RL01_and_both_are_named()
    {
        var (resolver, _, _) = Build(Rel("R1", "a", "b"), Rel("R2", "a", "b"));

        var result = await resolver.ResolveAsync("a", "b", Compiler, CancellationToken.None);

        Assert.False(result.Value!.Resolved);
        Assert.Equal(RelationshipRefusalCodes.AmbiguousPath, result.Value!.RefusalCode);
        Assert.Equal(2, result.Value!.CandidatePaths.Count);
        Assert.Contains("R1", result.Value!.CandidatePaths);
        Assert.Contains("R2", result.Value!.CandidatePaths);
        Assert.Contains("R1", result.Value!.RefusalMessage!);
        Assert.Contains("R2", result.Value!.RefusalMessage!);
    }

    [Fact]
    public async Task A_preferred_path_resolves_the_ambiguity()
    {
        var (resolver, _, _) = Build(Rel("R1", "a", "b", preferred: true), Rel("R2", "a", "b"));

        var result = await resolver.ResolveAsync("a", "b", Compiler, CancellationToken.None);

        Assert.True(result.Value!.Resolved);
        Assert.Single(result.Value!.Path);
    }

    [Fact]
    public async Task Two_preferred_paths_are_still_ambiguous()
    {
        // Marking both as preferred is not a decision, it is the same decision
        // twice. Refusing is the only honest answer.
        var (resolver, _, _) = Build(Rel("R1", "a", "b", preferred: true), Rel("R2", "a", "b", preferred: true));

        var result = await resolver.ResolveAsync("a", "b", Compiler, CancellationToken.None);

        Assert.False(result.Value!.Resolved);
        Assert.Equal(RelationshipRefusalCodes.AmbiguousPath, result.Value!.RefusalCode);
    }

    [Fact]
    public async Task Resolution_is_deterministic_across_repeated_calls()
    {
        var (resolver, _, _) = Build(Rel("R2", "a", "b"), Rel("R1", "a", "b"));

        var first = await resolver.ResolveAsync("a", "b", Compiler, CancellationToken.None);
        var second = await resolver.ResolveAsync("a", "b", Compiler, CancellationToken.None);

        Assert.Equal(first.Value!.RefusalMessage, second.Value!.RefusalMessage);
        Assert.Equal(first.Value!.CandidatePaths, second.Value!.CandidatePaths);
    }

    // ---- RL02 --------------------------------------------------------------

    [Fact]
    public async Task An_automated_consumer_may_not_cross_an_unproven_relationship()
    {
        var (resolver, _, _) = Build(Rel("R1", "a", "b", validation: RelationshipValidationStates.Unproven));

        var result = await resolver.ResolveAsync("a", "b", RelationshipConsumerPurposes.ModelTraining, CancellationToken.None);

        Assert.False(result.Value!.Resolved);
        Assert.Equal(RelationshipRefusalCodes.UnprovenRelationship, result.Value!.RefusalCode);
        Assert.Contains("R1", result.Value!.RefusalMessage!);
    }

    [Fact]
    public async Task Manual_exploration_may_cross_an_unproven_relationship()
    {
        var (resolver, _, _) = Build(Rel("R1", "a", "b", validation: RelationshipValidationStates.Unproven));

        var result = await resolver.ResolveAsync("a", "b", Explore, CancellationToken.None);

        Assert.True(result.Value!.Resolved);
        Assert.Single(result.Value!.Path);
    }

    [Fact]
    public async Task An_unknown_purpose_is_refused_rather_than_treated_as_exploration()
    {
        var (resolver, _, _) = Build(Rel("R1", "a", "b"));

        var result = await resolver.ResolveAsync("a", "b", "whatever", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    // ---- grain -------------------------------------------------------------

    [Fact]
    public async Task A_path_that_converts_grain_says_so()
    {
        var (resolver, _, _) = Build(Rel("R1", "a", "b",
            grainLeft: "heat", grainRight: "coil",
            attribution: RelationshipAttributionRules.Weighted));

        var result = await resolver.ResolveAsync("a", "b", Compiler, CancellationToken.None);

        Assert.True(result.Value!.Resolved);
        Assert.True(result.Value!.CrossesGrain);
    }

    // ---- the real consumer -------------------------------------------------

    [Fact]
    public async Task The_planner_produces_executable_predicates_in_declared_order()
    {
        var (_, planner, _) = Build(Rel("R1", "a", "b"));

        var plan = await planner.PlanAsync("a", "b", Compiler, CancellationToken.None);

        Assert.True(plan.Value!.Planned);
        Assert.Single(plan.Value!.Steps);

        var predicates = plan.Value!.Steps[0].Predicates;
        Assert.Equal(2, predicates.Count);
        Assert.Equal("site_key", predicates[0].FromColumn);
        Assert.Equal("site_ref", predicates[0].ToColumn);
        Assert.Equal("unit_key", predicates[1].FromColumn);
        Assert.Equal("=", predicates[0].Comparison);
    }

    [Fact]
    public async Task Traversing_a_relationship_backwards_swaps_the_columns()
    {
        // Keeping the declared order while travelling the other way would join
        // left key to left key: still rows, wrong rows.
        var (_, planner, _) = Build(Rel("R1", "a", "b"));

        var plan = await planner.PlanAsync("b", "a", Compiler, CancellationToken.None);

        var predicates = plan.Value!.Steps[0].Predicates;
        Assert.Equal("site_ref", predicates[0].FromColumn);
        Assert.Equal("site_key", predicates[0].ToColumn);
    }

    [Fact]
    public async Task The_planner_carries_a_refusal_through_unchanged_and_emits_no_steps()
    {
        var (_, planner, _) = Build(Rel("R1", "a", "b"), Rel("R2", "a", "b"));

        var plan = await planner.PlanAsync("a", "b", Compiler, CancellationToken.None);

        Assert.False(plan.Value!.Planned);
        Assert.Equal(RelationshipRefusalCodes.AmbiguousPath, plan.Value!.RefusalCode);
        Assert.Empty(plan.Value!.Steps);
        Assert.Equal(2, plan.Value!.CandidatePaths.Count);
    }

    [Fact]
    public async Task A_grain_converting_plan_reports_that_attribution_is_required()
    {
        var (_, planner, _) = Build(Rel("R1", "a", "b",
            grainLeft: "heat", grainRight: "coil",
            attribution: RelationshipAttributionRules.Weighted));

        var plan = await planner.PlanAsync("a", "b", Compiler, CancellationToken.None);

        Assert.True(plan.Value!.Planned);
        Assert.True(plan.Value!.CrossesGrain);
        Assert.True(plan.Value!.RequiresAttribution);
        Assert.Equal(RelationshipAttributionRules.Weighted, plan.Value!.Steps[0].AttributionRule);
    }

    // ---- the frozen customer behaviour -------------------------------------

    [Fact]
    public async Task Published_then_unpublished_then_restored()
    {
        var relationship = Rel("R1", "a", "b");
        var (_, planner, model) = Build(relationship);

        // Published: the consumer works.
        var working = await planner.PlanAsync("a", "b", Compiler, CancellationToken.None);
        Assert.True(working.Value!.Planned);

        // Unpublished: the consumer refuses with a named reason and returns no
        // steps. It does not quietly return a partial join.
        model.Retire("R1");
        var refused = await planner.PlanAsync("a", "b", Compiler, CancellationToken.None);
        Assert.False(refused.Value!.Planned);
        Assert.Equal(RelationshipRefusalCodes.NoPath, refused.Value!.RefusalCode);
        Assert.Empty(refused.Value!.Steps);
        Assert.False(string.IsNullOrWhiteSpace(refused.Value!.RefusalMessage));

        // Restored: the consumer works again, with no intervention anywhere else.
        var restored = new PublishedModel(relationship);
        var restoredResolver = new RelationshipResolver(restored);
        var restoredPlanner = new RelationshipJoinPlanner(restoredResolver, restored);

        var again = await restoredPlanner.PlanAsync("a", "b", Compiler, CancellationToken.None);
        Assert.True(again.Value!.Planned);
    }

    [Fact]
    public async Task An_entity_joined_to_itself_needs_no_relationship_and_claims_no_path()
    {
        var (resolver, _, _) = Build(Rel("R1", "a", "b"));

        var result = await resolver.ResolveAsync("a", "a", Compiler, CancellationToken.None);

        Assert.True(result.Value!.Resolved);
        Assert.Empty(result.Value!.Path);
    }
}