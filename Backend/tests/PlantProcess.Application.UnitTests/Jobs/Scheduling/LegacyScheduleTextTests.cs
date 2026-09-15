// T-106 B2.3 seed-compatibility tests: every schedule string this product already ships
// must be translatable, and the canonical grammar must not be widened to accept prose.
using System;
using PlantProcess.Application.Jobs.Scheduling;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs.Scheduling;

[Trait("BacklogTask", "T-106")]
public sealed class LegacyScheduleTextTests
{
    private const string Zone = "Europe/Berlin";

    [Theory]
    [InlineData("Every 2 minutes", "PT2M")]
    [InlineData("Every 15 minutes", "PT15M")]
    public void Shipped_interval_seeds_translate_to_a_canonical_duration(string seed, string expected)
    {
        var t = LegacyScheduleText.Translate(seed);
        Assert.True(t.IsRecognised, seed);
        Assert.Equal(ScheduleKind.MicroBatch, t.Kind);
        Assert.Equal(expected, t.CanonicalExpression);
    }

    [Theory]
    [InlineData("Daily 02:00", "0 2 * * *")]
    [InlineData("Daily 02:30", "30 2 * * *")]
    [InlineData("Daily 03:00", "0 3 * * *")]
    [InlineData("Weekly Sunday 03:00", "0 3 * * 0")]
    public void Shipped_wall_clock_seeds_translate_to_a_canonical_cron(string seed, string expected)
    {
        var t = LegacyScheduleText.Translate(seed);
        Assert.True(t.IsRecognised, seed);
        Assert.Equal(ScheduleKind.Cron, t.Kind);
        Assert.Equal(expected, t.CanonicalExpression);
    }

    [Theory]
    [InlineData("Every 2 minutes")]
    [InlineData("Every 15 minutes")]
    [InlineData("Daily 02:00")]
    [InlineData("Daily 02:30")]
    [InlineData("Daily 03:00")]
    [InlineData("Weekly Sunday 03:00")]
    public void Every_shipped_seed_survives_the_one_validation_entry_point(string seed)
    {
        var result = LegacyScheduleText.ValidateAny(seed, Zone, MisfirePolicy.SkipToNext, 0);
        Assert.True(result.IsValid, seed + " -> " + result.Message);
        Assert.NotNull(result.CanonicalExpression);
    }

    [Fact]
    public void A_shipped_seed_produces_a_real_next_occurrence()
    {
        var daily = LegacyScheduleText.ValidateAny("Daily 02:00", Zone, MisfirePolicy.SkipToNext, 0);
        var next = ScheduleOccurrenceCalculator.NextOccurrenceUtc(daily, new DateTime(2026, 1, 10, 3, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 1, 11, 1, 0, 0, DateTimeKind.Utc), next);

        var micro = LegacyScheduleText.ValidateAny("Every 15 minutes", Zone, MisfirePolicy.SkipToNext, 0);
        var anchor = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(
            new DateTime(2026, 9, 15, 10, 15, 0, DateTimeKind.Utc),
            ScheduleOccurrenceCalculator.NextOccurrenceUtc(micro, new DateTime(2026, 9, 15, 10, 7, 0, DateTimeKind.Utc), anchor));
    }

    [Fact]
    public void The_canonical_grammar_itself_is_not_widened_to_accept_prose()
    {
        var direct = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.Cron, "Every 2 minutes", Zone, MisfirePolicy.SkipToNext, 0));
        Assert.False(direct.IsValid);
        Assert.Equal(ScheduleRefusalReason.MalformedCronExpression, direct.Reason);
    }

    [Fact]
    public void Canonical_expressions_still_win_before_any_legacy_reading()
    {
        var cron = LegacyScheduleText.ValidateAny("*/15 * * * *", Zone, MisfirePolicy.SkipToNext, 0);
        Assert.True(cron.IsValid);
        Assert.Equal("*/15 * * * *", cron.CanonicalExpression);

        var iso = LegacyScheduleText.ValidateAny("PT5M", Zone, MisfirePolicy.SkipToNext, 0);
        Assert.True(iso.IsValid);
        Assert.Equal(ScheduleKind.MicroBatch, iso.Kind);
    }

    [Theory]
    [InlineData("whenever")]
    [InlineData("Daily 25:00")]
    [InlineData("Weekly Funday 03:00")]
    [InlineData("Every 0 minutes")]
    [InlineData("")]
    public void Anything_it_cannot_translate_is_refused_rather_than_guessed(string text)
    {
        Assert.False(LegacyScheduleText.ValidateAny(text, Zone, MisfirePolicy.SkipToNext, 0).IsValid);
    }

    [Fact]
    public void Manual_shapes_translate_to_a_manual_schedule_with_no_expression()
    {
        var t = LegacyScheduleText.Translate("Manual");
        Assert.True(t.IsRecognised);
        Assert.Equal(ScheduleKind.Manual, t.Kind);
        Assert.Null(t.CanonicalExpression);
    }
}
