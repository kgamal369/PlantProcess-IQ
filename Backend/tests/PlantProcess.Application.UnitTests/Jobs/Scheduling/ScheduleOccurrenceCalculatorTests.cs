// T-106 Phase A tests: occurrence determinism, identity, DST and misfire policy.
using System;
using PlantProcess.Application.Jobs.Scheduling;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs.Scheduling;

[Trait("BacklogTask", "T-106")]
public sealed class ScheduleOccurrenceCalculatorTests
{
    private static readonly Guid Job = new Guid("11111111-2222-3333-4444-555555555555");
    private const string Berlin = "Europe/Berlin";

    private static ScheduleValidationResult Cron(string expression, string zone = Berlin)
    {
        var result = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.Cron, expression, zone, MisfirePolicy.SkipToNext, 0));
        Assert.True(result.IsValid, result.Message);
        return result;
    }

    private static ScheduleValidationResult Micro(string expression)
    {
        var result = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.MicroBatch, expression, Berlin, MisfirePolicy.SkipToNext, 0));
        Assert.True(result.IsValid, result.Message);
        return result;
    }

    [Fact]
    public void Daily_cron_resolves_local_wall_clock_to_utc()
    {
        // 02:00 Berlin in January is 01:00 UTC.
        var next = ScheduleOccurrenceCalculator.NextOccurrenceUtc(Cron("0 2 * * *"), new DateTime(2026, 1, 10, 3, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 1, 11, 1, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Summer_time_shifts_the_utc_instant_not_the_local_hour()
    {
        // 02:00 Berlin in July is 00:00 UTC.
        var next = ScheduleOccurrenceCalculator.NextOccurrenceUtc(Cron("0 2 * * *"), new DateTime(2026, 7, 10, 3, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void A_local_time_that_does_not_exist_is_skipped_not_invented()
    {
        // Berlin springs forward on 29 March 2026: 02:30 local does not exist that day.
        var next = ScheduleOccurrenceCalculator.NextOccurrenceUtc(Cron("30 2 * * *"), new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 3, 30, 0, 30, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void An_ambiguous_local_time_runs_once_at_the_earlier_instant()
    {
        // Berlin falls back on 25 October 2026: 02:30 local happens twice.
        var first = ScheduleOccurrenceCalculator.NextOccurrenceUtc(Cron("30 2 * * *"), new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc), first);

        var second = ScheduleOccurrenceCalculator.NextOccurrenceUtc(Cron("30 2 * * *"), first!.Value);
        Assert.Equal(new DateTime(2026, 10, 26, 1, 30, 0, DateTimeKind.Utc), second);
    }

    [Fact]
    public void Micro_batch_occurrences_step_from_the_anchor()
    {
        var anchor = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc);
        var next = ScheduleOccurrenceCalculator.NextOccurrenceUtc(Micro("PT5M"), new DateTime(2026, 9, 15, 10, 7, 0, DateTimeKind.Utc), anchor);
        Assert.Equal(new DateTime(2026, 9, 15, 10, 10, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Manual_and_event_schedules_have_no_occurrence()
    {
        var manual = ScheduleGrammar.Validate(new ScheduleSpecification(ScheduleKind.Manual, null, null, MisfirePolicy.SkipToNext, 0));
        Assert.Null(ScheduleOccurrenceCalculator.NextOccurrenceUtc(manual, DateTime.UtcNow));

        var ev = ScheduleGrammar.Validate(new ScheduleSpecification(ScheduleKind.Event, "IMPORT_READY", null, MisfirePolicy.SkipToNext, 0));
        Assert.Null(ScheduleOccurrenceCalculator.NextOccurrenceUtc(ev, DateTime.UtcNow));
    }

    [Fact]
    public void Occurrence_identity_is_the_nominal_instant_and_repeats_exactly()
    {
        var nominal = new DateTime(2026, 9, 15, 1, 0, 0, DateTimeKind.Utc);
        var a = ScheduleOccurrenceCalculator.BuildOccurrenceKey(Job, nominal);
        var b = ScheduleOccurrenceCalculator.BuildOccurrenceKey(Job, nominal);
        Assert.Equal(a, b);
        Assert.EndsWith("20260915T010000Z", a);
        Assert.NotEqual(a, ScheduleOccurrenceCalculator.BuildOccurrenceKey(Job, nominal.AddMinutes(1)));
    }

    [Fact]
    public void Two_schedulers_planning_the_same_window_claim_the_same_occurrences()
    {
        var schedule = Micro("PT5M");
        var last = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 9, 15, 10, 17, 0, DateTimeKind.Utc);

        var planA = ScheduleOccurrenceCalculator.PlanDispatch(Job, schedule, MisfirePolicy.RunAllMissed, last, now, 0, 512, last);
        var planB = ScheduleOccurrenceCalculator.PlanDispatch(Job, schedule, MisfirePolicy.RunAllMissed, last, now, 0, 512, last);

        Assert.Equal(3, planA.Count);
        for (var i = 0; i < planA.Count; i++)
        {
            Assert.Equal(planA[i].OccurrenceKey, planB[i].OccurrenceKey);
            Assert.Equal(planA[i].NominalUtc, planB[i].NominalUtc);
        }
    }

    [Fact]
    public void Misfire_policy_decides_how_much_of_the_backlog_is_claimed()
    {
        var schedule = Micro("PT5M");
        var last = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 9, 15, 10, 17, 0, DateTimeKind.Utc);

        Assert.Empty(ScheduleOccurrenceCalculator.PlanDispatch(Job, schedule, MisfirePolicy.SkipToNext, last, now, 0, 512, last));

        var once = ScheduleOccurrenceCalculator.PlanDispatch(Job, schedule, MisfirePolicy.RunOnceImmediately, last, now, 0, 512, last);
        Assert.Single(once);
        Assert.Equal(new DateTime(2026, 9, 15, 10, 15, 0, DateTimeKind.Utc), once[0].NominalUtc);

        Assert.Equal(3, ScheduleOccurrenceCalculator.PlanDispatch(Job, schedule, MisfirePolicy.RunAllMissed, last, now, 0, 512, last).Count);
    }

    [Fact]
    public void Jitter_moves_dispatch_but_never_identity()
    {
        var schedule = Micro("PT5M");
        var last = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 9, 15, 10, 6, 0, DateTimeKind.Utc);

        var plain = ScheduleOccurrenceCalculator.PlanDispatch(Job, schedule, MisfirePolicy.RunAllMissed, last, now, 0, 512, last);
        var jittered = ScheduleOccurrenceCalculator.PlanDispatch(Job, schedule, MisfirePolicy.RunAllMissed, last, now, 120, 512, last);

        Assert.Equal(plain[0].OccurrenceKey, jittered[0].OccurrenceKey);
        Assert.Equal(plain[0].NominalUtc, jittered[0].NominalUtc);
        Assert.InRange(jittered[0].DispatchUtc, plain[0].NominalUtc, plain[0].NominalUtc.AddSeconds(120));
        Assert.Equal(jittered[0].DispatchUtc,
            ScheduleOccurrenceCalculator.PlanDispatch(Job, schedule, MisfirePolicy.RunAllMissed, last, now, 120, 512, last)[0].DispatchUtc);
    }

    [Fact]
    public void A_refused_schedule_has_no_occurrences()
    {
        var refused = ScheduleGrammar.Validate(
            new ScheduleSpecification(ScheduleKind.Cron, "not a cron", Berlin, MisfirePolicy.SkipToNext, 0));
        Assert.Throws<InvalidOperationException>(() => { _ = ScheduleOccurrenceCalculator.NextOccurrenceUtc(refused, DateTime.UtcNow); });
    }
}
