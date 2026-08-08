using PlantProcess.Application.Assistant;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

/// <summary>
/// A registry that knows nothing. Shared by the other assistant suites so a
/// resolution never happens by accident there, and so there is ONE fake to keep
/// faithful instead of one per suite.
/// </summary>
internal sealed class EmptyParameterQuantityRegistry : IParameterQuantityRegistry
{
    public Task<IReadOnlyList<RegistryQuantity>> GetActiveAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RegistryQuantity>>(Array.Empty<RegistryQuantity>());
}

/// <summary>
/// T-074. The registry is the authority for unit, sign and range.
///
/// Every code, name and unit in these tests is invented for the test and means
/// nothing outside it. That is the point: the guard is proven without a single
/// industry word, so the same proof holds for an oil plant or a water plant.
/// </summary>
public class T074TypedQuantityGuardTests
{
    private static RegistryQuantity Approved(
        string code = "ALPHA_RATE",
        string name = "Alpha Rate",
        string? unit = "u/min",
        decimal? min = 0.5m,
        decimal? max = 2.5m,
        bool synthetic = false)
        => new(code, name, "Numeric", unit, min, max, synthetic);

    private static QuantityResolution ResolveAgainst(string question, params RegistryQuantity[] registry)
        => QuantityResolver.Resolve(question, registry);

    private static QuantityResolution Resolved() => QuantityResolution.Resolved(Approved());

    // -----------------------------------------------------------------------
    // Resolution
    // -----------------------------------------------------------------------

    [Fact]
    public void A_question_naming_no_registry_vocabulary_is_NoMatch()
    {
        var resolution = ResolveAgainst("how many widgets are on this page", Approved());

        Assert.Equal(QuantityResolutionOutcome.NoMatch, resolution.Outcome);
    }

    [Fact]
    public void One_approved_definition_resolves()
    {
        var resolution = ResolveAgainst("what is the alpha rate", Approved());

        Assert.Equal(QuantityResolutionOutcome.Resolved, resolution.Outcome);
        Assert.Equal("ALPHA_RATE", resolution.Quantity!.ParameterCode);
    }

    [Fact]
    public void Synthetic_definitions_alone_are_known_but_untrusted()
    {
        /* The measured presentation state: the only definitions naming the
           quantity are synthetic, with the same unit and DIFFERENT ranges. They
           must not be merged into an invented composite. */
        var resolution = ResolveAgainst(
            "what is the alpha rate",
            Approved(code: "ALPHA_RATE", min: 0.5m, max: 2.5m, synthetic: true),
            Approved(code: "ALPHA_RATE_UPM", name: "Alpha rate", min: 0m, max: 3.0m, synthetic: true));

        Assert.Equal(QuantityResolutionOutcome.KnownButUntrustedOrAmbiguous, resolution.Outcome);
        Assert.Null(resolution.Quantity);
    }

    [Fact]
    public void Two_approved_definitions_matching_equally_are_ambiguous()
    {
        var resolution = ResolveAgainst(
            "what is the alpha rate",
            Approved(code: "ALPHA_RATE_ONE", name: "Alpha Rate"),
            Approved(code: "ALPHA_RATE_TWO", name: "Alpha rate"));

        Assert.Equal(QuantityResolutionOutcome.KnownButUntrustedOrAmbiguous, resolution.Outcome);
    }

    [Fact]
    public void An_approved_definition_beats_a_synthetic_one()
    {
        var resolution = ResolveAgainst(
            "what is the alpha rate",
            Approved(code: "DEMO_ALPHA_RATE", name: "Alpha Rate", synthetic: true),
            Approved(code: "ALPHA_RATE", name: "Alpha Rate", synthetic: false));

        Assert.Equal(QuantityResolutionOutcome.Resolved, resolution.Outcome);
        Assert.False(resolution.Quantity!.IsSynthetic);
    }

    // -----------------------------------------------------------------------
    // The guard
    // -----------------------------------------------------------------------

    [Fact]
    public void A_value_with_the_registry_unit_and_in_range_survives()
    {
        var result = TypedQuantityGuard.Apply("It is 1.31 u/min.", Resolved());

        Assert.Empty(result.Blocked);
        Assert.Contains("1.31", result.DraftText);
    }

    [Fact]
    public void A_band_survives_and_both_endpoints_are_checked()
    {
        var ok = TypedQuantityGuard.Apply("It is 1.20 to 1.45 u/min.", Resolved());
        Assert.Empty(ok.Blocked);

        var highEndpointOut = TypedQuantityGuard.Apply("It is 1.20-9.90 u/min.", Resolved());
        Assert.Single(highEndpointOut.Blocked);

        var lowEndpointOut = TypedQuantityGuard.Apply("It is 0.10-1.45 u/min.", Resolved());
        Assert.Single(lowEndpointOut.Blocked);
    }

    [Fact]
    public void A_number_carrying_a_different_unit_is_blocked_without_any_unit_vocabulary()
    {
        /* No table says what kg is. The sentence fails for one reason: no
           candidate carries the registry unit. */
        var result = TypedQuantityGuard.Apply("It is 450 kg.", Resolved());

        Assert.Single(result.Blocked);
        Assert.Equal(string.Empty, result.DraftText);
    }

    [Fact]
    public void A_date_shaped_answer_is_blocked_without_any_date_rule()
    {
        var result = TypedQuantityGuard.Apply("It is 2026-08-08.", Resolved());

        Assert.Single(result.Blocked);
    }

    [Fact]
    public void A_bare_number_is_blocked_where_the_registry_declares_a_unit()
    {
        var result = TypedQuantityGuard.Apply("It is 17.", Resolved());

        Assert.Single(result.Blocked);
    }

    [Fact]
    public void A_negative_value_is_blocked_by_the_registry_lower_bound_alone()
    {
        /* No generic "this quantity must be positive" rule exists. The bound does
           the work, because the registry is the authority. */
        var result = TypedQuantityGuard.Apply("It is -1.31 u/min.", Resolved());

        Assert.Single(result.Blocked);
    }

    [Fact]
    public void A_negative_band_endpoint_is_blocked_too()
    {
        /* The sign has to survive candidate extraction at BOTH endpoints, not
           only for a scalar. */
        Assert.Single(TypedQuantityGuard.Apply("It is -0.10 to 1.45 u/min.", Resolved()).Blocked);
    }

    [Fact]
    public void Below_the_minimum_and_above_the_maximum_are_both_blocked()
    {
        Assert.Single(TypedQuantityGuard.Apply("It is 0.10 u/min.", Resolved()).Blocked);
        Assert.Single(TypedQuantityGuard.Apply("It is 9.90 u/min.", Resolved()).Blocked);
    }

    [Fact]
    public void Contextual_numbers_beside_a_valid_value_do_not_cause_a_false_rejection()
    {
        var result = TypedQuantityGuard.Apply(
            "It is 1.31 u/min on 8 August across 120 observations.", Resolved());

        Assert.Empty(result.Blocked);
        Assert.Contains("120", result.DraftText);
    }

    [Fact]
    public void A_known_but_untrusted_quantity_refuses_every_numeric_answer()
    {
        var untrusted = QuantityResolution.Untrusted("synthetic only");

        var result = TypedQuantityGuard.Apply(
            "It is 1.31 u/min. It is 450 kg. The chart is a line.", untrusted);

        Assert.Equal(2, result.Blocked.Count);
        /* The sentence carrying no number is not a quantity answer and survives. */
        Assert.Contains("The chart is a line.", result.DraftText);
    }

    [Fact]
    public void NoMatch_leaves_the_draft_completely_untouched()
    {
        const string draft = "It is 450 kg. It is 2026-08-08. It is 9.90 u/min.";

        var result = TypedQuantityGuard.Apply(draft, QuantityResolution.NoMatch());

        Assert.Empty(result.Blocked);
        Assert.Equal(draft, result.DraftText);
    }

    [Fact]
    public void A_unitless_quantity_still_range_checks_a_number_it_can_identify()
    {
        var unitless = QuantityResolution.Resolved(Approved(unit: null, min: 0.5m, max: 2.5m));

        Assert.Empty(TypedQuantityGuard.Apply("The alpha rate is 1.31.", unitless).Blocked);
        Assert.Single(TypedQuantityGuard.Apply("The alpha rate is 9.90.", unitless).Blocked);

        /* Two numbers, no unit to tell them apart: fail closed rather than guess. */
        Assert.Single(TypedQuantityGuard.Apply("The alpha rate is 1.31 over 120 runs.", unitless).Blocked);

        /* A sentence that does not name the quantity is not judged at all. */
        Assert.Empty(TypedQuantityGuard.Apply("Something else is 9.90.", unitless).Blocked);
    }
}