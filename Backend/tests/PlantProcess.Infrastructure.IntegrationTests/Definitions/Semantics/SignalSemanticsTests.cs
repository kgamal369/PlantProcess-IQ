using PlantProcess.Application.Definitions.Semantics;
using PlantProcess.Infrastructure.Definitions.Semantics;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions.Semantics;

/// <summary>
/// PPIQ T-210 acceptance: T210-01 through T210-15. Every failure message names
/// the parameter, signal kind, sampling basis, requested method, resolution
/// source and refusal code, because the resolver puts them there.
/// </summary>
[Collection("T210Semantics")]
public sealed class SignalSemanticsTests
{
    private readonly T210SemanticsFixture _fixture;

    public SignalSemanticsTests(T210SemanticsFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// T210-FIXTURE-00. The prerequisite: two tenants, a parameter under each,
    /// a binding, read back through the same path the gates use. If this is
    /// red the pack stops here and prints the offending column once, instead
    /// of fourteen gates printing the same setup failure.
    /// </summary>
    [Fact] [Trait("Gate", "T210-FIXTURE-00")]
    public async Task Fixture_can_provision_two_tenants_a_parameter_each_and_a_binding()
    {
        Assert.StartsWith("ppiq_t210_probe", _fixture.DatabaseName);
        Assert.NotEqual(_fixture.TenantA, _fixture.TenantB);

        var a = await _fixture.CreateParameterAsync("fixture_a", _fixture.TenantA);
        var b = await _fixture.CreateParameterAsync("fixture_b", _fixture.TenantB);
        var binding = await _fixture.CreateBindingAsync(a, _fixture.TenantA, null, null);

        Assert.Equal(_fixture.TenantA, await _fixture.ReadTenantOfParameterAsync(a));
        Assert.Equal(_fixture.TenantB, await _fixture.ReadTenantOfParameterAsync(b));
        Assert.Equal(1, await _fixture.ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.kpi_parameter_bindings WHERE id = @id AND tenant_id = @tenant;",
            ("id", binding), ("tenant", _fixture.TenantA)));
    }

    private static SignalSemanticsDeclaration Analog(SamplingBasis basis, AggregationKind? def) =>
        new(SignalKind.Analog, basis, def, InterpolationKind.HoldLast, WeightBasis.Time, 300,
            CounterResetPolicy.None, QualityPolicy.GoodOnly, TimeBasis.ObservationTime);

    [Fact] [Trait("Gate", "T210-01")]
    public async Task A_fixed_cadence_sampled_signal_can_declare_SampleMean()
    {
        var (resolver, id) = await SetupAsync("signal_a", Analog(SamplingBasis.FixedCadence, AggregationKind.SampleMean));
        var resolved = await resolver.ResolveAsync(_fixture.TenantA, id, null, null, CancellationToken.None);
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        Assert.Equal(AggregationKind.SampleMean, resolved.Value!.Kind);
        Assert.Equal(AggregationResolutionSource.Parameter, resolved.Value.Source);
        Assert.Equal(SamplingBasis.FixedCadence, resolved.Value.SamplingBasis);
    }

    [Fact] [Trait("Gate", "T210-02")]
    public async Task An_irregular_signal_declares_TimeWeightedMean_with_time_weighting()
    {
        var (resolver, id) = await SetupAsync("signal_b", Analog(SamplingBasis.Irregular, AggregationKind.TimeWeightedMean));
        var resolved = await resolver.ResolveAsync(_fixture.TenantA, id, null, null, CancellationToken.None);
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        Assert.Equal(AggregationKind.TimeWeightedMean, resolved.Value!.Kind);
        Assert.Equal(WeightBasis.Time, resolved.Value.WeightBasis);

        // The sampling-basis known answer: SampleMean on irregular samples is
        // not a time mean, and the contract says so rather than approximating.
        var unlawful = await resolver.ResolveAsync(_fixture.TenantA, id, null, AggregationKind.SampleMean, CancellationToken.None);
        Assert.True(unlawful.IsFailure);
        Assert.StartsWith(AggregationRefusal.InvalidForSignal, unlawful.Error!.Message);
        Assert.Contains("sampling basis FixedCadence", unlawful.Error.Message);
    }

    [Fact] [Trait("Gate", "T210-03")]
    public async Task A_counter_supports_Delta_with_a_reset_policy_and_refuses_a_mean()
    {
        var (resolver, id) = await SetupAsync("counter_a", new SignalSemanticsDeclaration(
            SignalKind.Counter, SamplingBasis.FixedCadence, AggregationKind.Delta, InterpolationKind.None,
            null, null, CounterResetPolicy.ResetToZero, QualityPolicy.GoodOnly, TimeBasis.ObservationTime));

        var resolved = await resolver.ResolveAsync(_fixture.TenantA, id, null, null, CancellationToken.None);
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        Assert.Equal(AggregationKind.Delta, resolved.Value!.Kind);

        var semantics = await resolver.GetAsync(_fixture.TenantA, id, CancellationToken.None);
        Assert.Equal(CounterResetPolicy.ResetToZero, semantics.Value!.CounterResetPolicy);

        var mean = await resolver.ResolveAsync(_fixture.TenantA, id, null, AggregationKind.SampleMean, CancellationToken.None);
        Assert.True(mean.IsFailure);
        Assert.StartsWith(AggregationRefusal.InvalidForSignal, mean.Error!.Message);
    }

    [Fact] [Trait("Gate", "T210-04")]
    public async Task A_rate_supports_Integral_where_declared()
    {
        var (resolver, id) = await SetupAsync("rate_a", new SignalSemanticsDeclaration(
            SignalKind.Rate, SamplingBasis.Irregular, AggregationKind.Integral, InterpolationKind.Linear,
            WeightBasis.Time, 60, CounterResetPolicy.None, QualityPolicy.GoodOnly, TimeBasis.ObservationTime));
        var resolved = await resolver.ResolveAsync(_fixture.TenantA, id, null, null, CancellationToken.None);
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        Assert.Equal(AggregationKind.Integral, resolved.Value!.Kind);
    }

    [Fact] [Trait("Gate", "T210-05")]
    public async Task A_state_signal_supports_StateDuration_and_refuses_a_mean_of_codes()
    {
        var (resolver, id) = await SetupAsync("state_a", new SignalSemanticsDeclaration(
            SignalKind.State, SamplingBasis.OnChange, AggregationKind.StateDuration, InterpolationKind.Step,
            WeightBasis.Time, null, CounterResetPolicy.None, QualityPolicy.All, TimeBasis.ObservationTime));
        var resolved = await resolver.ResolveAsync(_fixture.TenantA, id, null, null, CancellationToken.None);
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        Assert.Equal(AggregationKind.StateDuration, resolved.Value!.Kind);

        var mean = await resolver.ResolveAsync(_fixture.TenantA, id, null, AggregationKind.TimeWeightedMean, CancellationToken.None);
        Assert.True(mean.IsFailure);
        Assert.StartsWith(AggregationRefusal.InvalidForSignal, mean.Error!.Message);
    }

    [Fact] [Trait("Gate", "T210-06")]
    public async Task Count_Min_Max_Last_are_permitted_or_refused_by_the_contract()
    {
        var (resolver, analog) = await SetupAsync("signal_c", Analog(SamplingBasis.Irregular, AggregationKind.Last));
        foreach (var lawful in new[] { AggregationKind.Count, AggregationKind.Min, AggregationKind.Max, AggregationKind.Last })
        {
            var r = await resolver.ResolveAsync(_fixture.TenantA, analog, null, lawful, CancellationToken.None);
            Assert.True(r.IsSuccess, r.Error?.Message);
        }

        var stateId = await _fixture.CreateParameterAsync("state_b");
        await Declare(resolver, stateId, new SignalSemanticsDeclaration(
            SignalKind.State, SamplingBasis.OnChange, null, InterpolationKind.Step, null, null, null, null, null));
        var min = await resolver.ResolveAsync(_fixture.TenantA, stateId, null, AggregationKind.Min, CancellationToken.None);
        Assert.True(min.IsFailure, "Min of state codes must be refused");
        Assert.StartsWith(AggregationRefusal.InvalidForSignal, min.Error!.Message);
    }

    [Fact] [Trait("Gate", "T210-07")]
    public async Task No_declared_semantics_refuses_with_AG01()
    {
        await using var db = _fixture.NewContext();
        var resolver = new SignalSemanticsResolver(db);
        var id = await _fixture.CreateParameterAsync("undeclared_a");

        var resolved = await resolver.ResolveAsync(_fixture.TenantA, id, null, null, CancellationToken.None);
        Assert.True(resolved.IsFailure, "an undeclared parameter must not resolve to anything");
        Assert.StartsWith(AggregationRefusal.SemanticsUndeclared, resolved.Error!.Message);
        Assert.Contains("aggregation_semantics_undeclared", resolved.Error.Message);
        Assert.Contains("parameter=" + id, resolved.Error.Message);
    }

    [Fact] [Trait("Gate", "T210-08")]
    public async Task Declared_signal_with_incompatible_aggregation_refuses_with_AG02()
    {
        var (resolver, id) = await SetupAsync("signal_d", Analog(SamplingBasis.Irregular, AggregationKind.TimeWeightedMean));
        var r = await resolver.ResolveAsync(_fixture.TenantA, id, null, AggregationKind.StateDuration, CancellationToken.None);
        Assert.True(r.IsFailure);
        Assert.StartsWith(AggregationRefusal.InvalidForSignal, r.Error!.Message);
        Assert.Contains("signal=Analog", r.Error.Message);
        Assert.Contains("requested=StateDuration", r.Error.Message);

        // A declaration whose default is indefensible is refused before it lands.
        var bad = await _fixture.CreateParameterAsync("counter_b");
        var declared = await resolver.DeclareAsync(_fixture.TenantA, bad, new SignalSemanticsDeclaration(
            SignalKind.Counter, SamplingBasis.FixedCadence, AggregationKind.TimeWeightedMean,
            null, null, null, CounterResetPolicy.Rollover, null, null), CancellationToken.None);
        Assert.True(declared.IsFailure);
        Assert.StartsWith(AggregationRefusal.InvalidForSignal, declared.Error!.Message);
    }

    [Fact] [Trait("Gate", "T210-09")]
    public async Task A_valid_KPI_override_wins_over_the_parameter_default()
    {
        var (resolver, id) = await SetupAsync("signal_e", Analog(SamplingBasis.Irregular, AggregationKind.TimeWeightedMean));
        var binding = await _fixture.CreateBindingAsync(id, _fixture.TenantA, "Max", null);

        var resolved = await resolver.ResolveAsync(_fixture.TenantA, id, binding, null, CancellationToken.None);
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        Assert.Equal(AggregationKind.Max, resolved.Value!.Kind);
        Assert.Equal(AggregationResolutionSource.KpiBinding, resolved.Value.Source);

        // A binding with no override inherits the parameter default.
        var inherit = await _fixture.CreateBindingAsync(id, _fixture.TenantA, null, null);
        var inherited = await resolver.ResolveAsync(_fixture.TenantA, id, inherit, null, CancellationToken.None);
        Assert.True(inherited.IsSuccess, inherited.Error?.Message);
        Assert.Equal(AggregationKind.TimeWeightedMean, inherited.Value!.Kind);
        Assert.Equal(AggregationResolutionSource.Parameter, inherited.Value.Source);
    }

    [Fact] [Trait("Gate", "T210-10")]
    public async Task An_invalid_KPI_override_is_refused_not_honoured()
    {
        var (resolver, id) = await SetupAsync("counter_c", new SignalSemanticsDeclaration(
            SignalKind.Counter, SamplingBasis.FixedCadence, AggregationKind.Delta, null, null, null,
            CounterResetPolicy.ResetToZero, null, null));
        var binding = await _fixture.CreateBindingAsync(id, _fixture.TenantA, "SampleMean", null);

        var resolved = await resolver.ResolveAsync(_fixture.TenantA, id, binding, null, CancellationToken.None);
        Assert.True(resolved.IsFailure, "a KPI may not bypass the signal contract");
        Assert.StartsWith(AggregationRefusal.InvalidForSignal, resolved.Error!.Message);
        Assert.Contains("source=kpi_binding", resolved.Error.Message);
    }

    [Fact] [Trait("Gate", "T210-11")]
    public async Task Nothing_defaults_to_Average_because_storage_is_numeric()
    {
        // The grammar has no Average at all, and an undeclared numeric-looking
        // parameter resolves to a refusal, never to a method.
        Assert.False(Enum.GetNames<AggregationKind>().Any(n => n.Equals("Average", StringComparison.OrdinalIgnoreCase)));

        var grammarHasAverage = await _fixture.ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.aggregation_kinds WHERE lower(aggregation_kind) = 'average';");
        Assert.Equal(0, grammarHasAverage);

        var defaultedRows = await _fixture.ScalarAsync(
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'ppiq_meta' " +
            "AND table_name = 'parameter_definitions' AND column_name = 'aggregation_kind' AND column_default IS NOT NULL;");
        Assert.Equal(0, defaultedRows);

        await using var db = _fixture.NewContext();
        var resolver = new SignalSemanticsResolver(db);
        var id = await _fixture.CreateParameterAsync("numeric_a");
        var r = await resolver.ResolveAsync(_fixture.TenantA, id, null, null, CancellationToken.None);
        Assert.True(r.IsFailure);
        Assert.StartsWith(AggregationRefusal.SemanticsUndeclared, r.Error!.Message);
    }

    [Fact] [Trait("Gate", "T210-12")]
    public async Task A_semantic_change_versions_and_the_prior_meaning_is_replayable()
    {
        var (resolver, id) = await SetupAsync("signal_f", Analog(SamplingBasis.FixedCadence, AggregationKind.SampleMean));
        var v1 = await resolver.GetAsync(_fixture.TenantA, id, CancellationToken.None);
        Assert.Equal(1, v1.Value!.SemanticsVersion);

        var changed = await resolver.DeclareAsync(_fixture.TenantA, id,
            Analog(SamplingBasis.Irregular, AggregationKind.TimeWeightedMean), CancellationToken.None);
        Assert.True(changed.IsSuccess, changed.Error?.Message);
        Assert.Equal(2, changed.Value!.SemanticsVersion);

        // Version 1 is still exactly what it was, in governed history.
        var history = await _fixture.ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.parameter_signal_semantics_history " +
            "WHERE parameter_definition_id = @id AND semantics_version = 1 " +
            "AND signal_kind = 'Analog' AND sampling_basis = 'FixedCadence' AND aggregation_kind = 'SampleMean';",
            ("id", id));
        Assert.Equal(1, history);
    }

    [Fact] [Trait("Gate", "T210-13")]
    public async Task Semantics_are_owned_by_one_tenant_and_never_cross()
    {
        await using var db = _fixture.NewContext();
        var resolver = new SignalSemanticsResolver(db);
        var declaration = Analog(SamplingBasis.FixedCadence, AggregationKind.SampleMean);

        // Tenant A owns and declares parameter A.
        var owned = await _fixture.CreateParameterAsync("owned_a", _fixture.TenantA);
        Assert.True((await resolver.DeclareAsync(_fixture.TenantA, owned, declaration, CancellationToken.None)).IsSuccess);

        // Tenant B cannot resolve, read or claim it; tenant A still can.
        Assert.StartsWith(AggregationRefusal.SemanticsUndeclared,
            (await resolver.ResolveAsync(_fixture.TenantB, owned, null, null, CancellationToken.None)).Error!.Message);
        Assert.True((await resolver.GetAsync(_fixture.TenantB, owned, CancellationToken.None)).IsFailure);
        Assert.True((await resolver.DeclareAsync(_fixture.TenantB, owned, declaration, CancellationToken.None)).IsFailure,
            "tenant B must not be able to claim or redeclare tenant A's parameter");
        Assert.Equal(_fixture.TenantA, await _fixture.ReadTenantOfParameterAsync(owned));
        Assert.True((await resolver.ResolveAsync(_fixture.TenantA, owned, null, null, CancellationToken.None)).IsSuccess);

        // An unowned legacy row resolves for neither tenant before a claim.
        var legacy = await _fixture.CreateParameterAsync("legacy", null);
        Assert.StartsWith(AggregationRefusal.SemanticsUndeclared,
            (await resolver.ResolveAsync(_fixture.TenantA, legacy, null, null, CancellationToken.None)).Error!.Message);
        Assert.StartsWith(AggregationRefusal.SemanticsUndeclared,
            (await resolver.ResolveAsync(_fixture.TenantB, legacy, null, null, CancellationToken.None)).Error!.Message);

        // Claim by A succeeds and is atomic with the declaration; claim by B afterwards refuses.
        var claimed = await resolver.DeclareAsync(_fixture.TenantA, legacy, declaration, CancellationToken.None);
        Assert.True(claimed.IsSuccess, claimed.Error?.Message);
        Assert.Equal(_fixture.TenantA, await _fixture.ReadTenantOfParameterAsync(legacy));
        Assert.True((await resolver.DeclareAsync(_fixture.TenantB, legacy, declaration, CancellationToken.None)).IsFailure);
        Assert.Equal(_fixture.TenantA, await _fixture.ReadTenantOfParameterAsync(legacy));

        // A binding under tenant B cannot steer tenant A's parameter.
        var foreignBinding = await _fixture.CreateBindingAsync(owned, _fixture.TenantB, "Max", null);
        var viaForeign = await resolver.ResolveAsync(_fixture.TenantA, owned, foreignBinding, null, CancellationToken.None);
        Assert.True(viaForeign.IsSuccess, viaForeign.Error?.Message);
        Assert.Equal(AggregationResolutionSource.Parameter, viaForeign.Value!.Source);
        Assert.Equal(AggregationKind.SampleMean, viaForeign.Value.Kind);
    }

    [Fact] [Trait("Gate", "T210-14")]
    public async Task Identical_redeclaration_is_idempotent()
    {
        var declaration = Analog(SamplingBasis.FixedCadence, AggregationKind.SampleMean);
        var (resolver, id) = await SetupAsync("signal_h", declaration);

        var again = await resolver.DeclareAsync(_fixture.TenantA, id, declaration, CancellationToken.None);
        Assert.True(again.IsSuccess, again.Error?.Message);
        Assert.Equal(1, again.Value!.SemanticsVersion);

        var history = await _fixture.ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.parameter_signal_semantics_history WHERE parameter_definition_id = @id;", ("id", id));
        Assert.Equal(0, history);
    }

    [Fact] [Trait("Gate", "T210-15")]
    public async Task The_grammar_is_generic_and_no_industry_noun_or_grain_is_required()
    {
        var grammar = new List<string>();
        foreach (var table in new[] { "signal_kinds", "aggregation_kinds", "sampling_bases" })
        {
            var count = await _fixture.ScalarAsync("SELECT count(*) FROM ppiq_meta." + table + ";");
            Assert.True(count > 0, table + " is empty");
        }

        foreach (var noun in new[] { "coil", "steel", "oil", "furnace", "grade", "rolling", "well", "pump", "heat", "strip" })
        {
            var hits = await _fixture.ScalarAsync(
                "SELECT count(*) FROM (SELECT signal_kind AS v FROM ppiq_meta.signal_kinds UNION ALL " +
                "SELECT aggregation_kind FROM ppiq_meta.aggregation_kinds UNION ALL " +
                "SELECT sampling_basis FROM ppiq_meta.sampling_bases) g WHERE lower(g.v) LIKE '%' || @noun || '%';",
                ("noun", noun));
            Assert.True(hits == 0, "grammar contains industry vocabulary: " + noun);
        }

        // The same binary declares a discrete counter and a continuous rate with
        // no grain in sight: none of the semantic columns references a grain.
        var grainColumns = await _fixture.ScalarAsync(
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'ppiq_meta' " +
            "AND table_name IN ('signal_kinds','aggregation_kinds','sampling_bases','signal_aggregation_compatibility') " +
            "AND column_name LIKE '%grain%';");
        Assert.Equal(0, grainColumns);
    }

    private async Task<(SignalSemanticsResolver Resolver, Guid ParameterId)> SetupAsync(string suffix, SignalSemanticsDeclaration declaration)
    {
        var db = _fixture.NewContext();
        var resolver = new SignalSemanticsResolver(db);
        var id = await _fixture.CreateParameterAsync(suffix);
        await Declare(resolver, id, declaration);
        return (resolver, id);
    }

    private async Task Declare(SignalSemanticsResolver resolver, Guid id, SignalSemanticsDeclaration declaration)
    {
        var declared = await resolver.DeclareAsync(_fixture.TenantA, id, declaration, CancellationToken.None);
        Assert.True(declared.IsSuccess, declared.Error?.Message);
    }
}
