// T-106 Phase A tests: the schedule grammar refuses what the design forbids.
using System;
using PlantProcess.Application.Jobs.Scheduling;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs.Scheduling;

[Trait("BacklogTask", "T-106")]
public sealed class ScheduleGrammarTests
{
    private const string Zone = "Europe/Berlin";

    [Fact]
    public void Free_text_schedule_is_not_a_kind()
    {
        ScheduleKind kind;
        Assert.False(ScheduleGrammar.TryParseKind("Every 2 minutes", out kind));
        Assert.False(ScheduleGrammar.TryParseKind("", out kind));
        Assert.True(ScheduleGrammar.TryParseKind("micro_batch", out kind));
        Assert.Equal(ScheduleKind.MicroBatch, kind);
    }

    [Fact]
    public void Manual_schedule_carries_no_expression()
    {
        var ok = ScheduleGrammar.Validate(new ScheduleSpecification(ScheduleKind.Manual, null, null, MisfirePolicy.SkipToNext, 0));
        Assert.True(ok.IsValid);

        var refused = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.Manual, "0 * * * *", Zone, MisfirePolicy.SkipToNext, 0));
        Assert.False(refused.IsValid);
        Assert.Equal(ScheduleRefusalReason.ExpressionNotAllowed, refused.Reason);
    }

    [Fact]
    public void Wall_clock_kinds_require_a_declared_time_zone()
    {
        var refused = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.Cron, "0 2 * * *", null, MisfirePolicy.SkipToNext, 0));
        Assert.False(refused.IsValid);
        Assert.Equal(ScheduleRefusalReason.TimeZoneRequired, refused.Reason);

        var unknown = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.Cron, "0 2 * * *", "Mars/Olympus", MisfirePolicy.SkipToNext, 0));
        Assert.False(unknown.IsValid);
        Assert.Equal(ScheduleRefusalReason.UnknownTimeZone, unknown.Reason);
    }

    [Theory]
    [InlineData("0 2 * * *")]
    [InlineData("*/15 * * * *")]
    [InlineData("30 6-18 * * 1-5")]
    [InlineData("0 0 1,15 * *")]
    public void Well_formed_cron_is_accepted_and_canonicalised(string expression)
    {
        var result = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.Cron, "  " + expression + "  ", Zone, MisfirePolicy.SkipToNext, 0));
        Assert.True(result.IsValid);
        Assert.Equal(expression, result.CanonicalExpression);
        Assert.NotNull(result.Cron);
    }

    [Theory]
    [InlineData("0 2 * *")]
    [InlineData("60 2 * * *")]
    [InlineData("0 2 * * 9")]
    [InlineData("0 2 * * * *")]
    [InlineData("*/0 * * * *")]
    [InlineData("10-5 * * * *")]
    [InlineData("every minute")]
    public void Malformed_cron_is_refused(string expression)
    {
        var result = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.Cron, expression, Zone, MisfirePolicy.SkipToNext, 0));
        Assert.False(result.IsValid);
        Assert.Equal(ScheduleRefusalReason.MalformedCronExpression, result.Reason);
    }

    [Theory]
    [InlineData("PT5M", 5)]
    [InlineData("PT1H30M", 90)]
    [InlineData("P1DT0H", 1440)]
    public void Iso_durations_are_accepted(string expression, int expectedMinutes)
    {
        var result = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.MicroBatch, expression, Zone, MisfirePolicy.SkipToNext, 0));
        Assert.True(result.IsValid);
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), result.Interval);
    }

    [Theory]
    [InlineData("5 minutes")]
    [InlineData("P1Y")]
    [InlineData("PT")]
    [InlineData("1H")]
    public void Malformed_durations_are_refused(string expression)
    {
        var result = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.MicroBatch, expression, Zone, MisfirePolicy.SkipToNext, 0));
        Assert.False(result.IsValid);
        Assert.Equal(ScheduleRefusalReason.MalformedDuration, result.Reason);
    }

    [Theory]
    [InlineData("PT30S")]
    [InlineData("P2DT0H")]
    public void Durations_outside_the_supported_window_are_refused(string expression)
    {
        var result = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.MicroBatch, expression, Zone, MisfirePolicy.SkipToNext, 0));
        Assert.False(result.IsValid);
        Assert.Equal(ScheduleRefusalReason.DurationOutOfRange, result.Reason);
    }

    [Fact]
    public void Event_schedule_requires_a_registered_code_not_free_text()
    {
        var ok = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.Event, "IMPORT_BATCH_READY", null, MisfirePolicy.SkipToNext, 0));
        Assert.True(ok.IsValid);

        var refused = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.Event, "when the import finishes", null, MisfirePolicy.SkipToNext, 0));
        Assert.False(refused.IsValid);
        Assert.Equal(ScheduleRefusalReason.EventCodeMalformed, refused.Reason);
    }

    [Fact]
    public void Jitter_is_bounded()
    {
        var refused = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.Cron, "0 2 * * *", Zone, MisfirePolicy.SkipToNext, -1));
        Assert.False(refused.IsValid);
        Assert.Equal(ScheduleRefusalReason.JitterOutOfRange, refused.Reason);
    }
}
