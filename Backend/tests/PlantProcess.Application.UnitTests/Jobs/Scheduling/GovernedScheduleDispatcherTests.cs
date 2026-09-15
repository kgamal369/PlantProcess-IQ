// T-106 B2.3c tests: what is due, what its identity is, and what the poll clock may not do.
using System;
using System.Collections.Generic;
using System.Linq;
using PlantProcess.Application.Jobs.Scheduling;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs.Scheduling;

[Trait("BacklogTask", "T-106")]
public sealed class GovernedScheduleDispatcherTests
{
    private static readonly DateTime Anchor = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc);

    private static JobDefinition Job(string code, string schedule, bool enabled = true)
    {
        var job = new JobDefinition(code, code + " name", JobDefinitionType.DbLinkImport, schedule, false);
        if (!enabled) { job.Disable(); }
        return job;
    }

    private static IReadOnlyList<DueOccurrence> Due(JobDefinition job, DateTime pollAtUtc, DateTime? last)
        => GovernedScheduleDispatcher.DueAt(new[] { job }, pollAtUtc, _ => last);

    [Fact]
    public void A_future_occurrence_does_not_run()
    {
        var due = Due(Job("J1", "Every 15 minutes"), Anchor.AddMinutes(7), Anchor);
        Assert.Empty(due);
    }

    [Fact]
    public void A_due_occurrence_runs_and_carries_the_nominal_instant()
    {
        var due = Due(Job("J1", "Every 15 minutes"), Anchor.AddMinutes(17), Anchor);
        Assert.Single(due);
        Assert.Equal(Anchor.AddMinutes(15), due[0].NominalAtUtc);
    }

    [Fact]
    public void The_poll_instant_never_becomes_the_occurrence_identity()
    {
        var job = Job("J1", "Every 15 minutes");
        var early = Due(job, Anchor.AddMinutes(15).AddSeconds(3), Anchor);
        var late = Due(job, Anchor.AddMinutes(15).AddSeconds(57), Anchor);

        Assert.Equal(early[0].NominalAtUtc, late[0].NominalAtUtc);
        Assert.Equal(early[0].OccurrenceKey, late[0].OccurrenceKey);
        Assert.EndsWith("20260915T101500Z", early[0].OccurrenceKey);
    }

    [Fact]
    public void The_same_occurrence_is_identified_identically_by_two_dispatchers()
    {
        var job = Job("J1", "Daily 02:00");
        var a = Due(job, new DateTime(2026, 9, 16, 3, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
        var b = Due(job, new DateTime(2026, 9, 16, 3, 0, 30, DateTimeKind.Utc), new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Single(a);
        Assert.Equal(a[0].OccurrenceKey, b[0].OccurrenceKey);
    }

    [Fact]
    public void A_disabled_job_is_never_due()
    {
        Assert.Empty(Due(Job("J1", "Every 15 minutes", enabled: false), Anchor.AddHours(5), Anchor));
    }

    [Fact]
    public void A_manual_schedule_is_never_due()
    {
        Assert.Empty(Due(Job("J1", "Manual"), Anchor.AddHours(5), Anchor));
    }

    [Fact]
    public void A_schedule_nothing_can_parse_is_skipped_rather_than_guessed()
    {
        Assert.Empty(Due(Job("J1", "whenever"), Anchor.AddHours(5), Anchor));
    }

    [Fact]
    public void A_long_outage_claims_the_newest_due_occurrence_not_the_whole_backlog()
    {
        var due = Due(Job("J1", "Every 15 minutes"), Anchor.AddHours(3), Anchor);
        Assert.Single(due);
        Assert.Equal(Anchor.AddHours(3), due[0].NominalAtUtc);
    }

    [Fact]
    public void Every_shipped_seed_schedule_is_dispatchable()
    {
        foreach (var seed in new[] { "Every 2 minutes", "Every 15 minutes", "Daily 02:00", "Daily 02:30", "Daily 03:00", "Weekly Sunday 03:00" })
        {
            var due = GovernedScheduleDispatcher.DueAt(
                new[] { Job("J_" + seed.Replace(' ', '_'), seed) },
                new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc),
                _ => new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc));

            Assert.True(due.Count == 1, seed + " produced " + due.Count + " due occurrences");
        }
    }

    [Fact]
    public void A_scheduled_run_row_carries_its_occurrence_and_a_manual_one_carries_none()
    {
        var manual = new JobRunHistory(
            Guid.NewGuid(), "J1", "J1 name", JobDefinitionType.DbLinkImport,
            "ManualRunNow", "tester", null, false, "test", null);

        Assert.Null(manual.OccurrenceKey);
        Assert.Null(manual.NominalAtUtc);

        var scheduled = new JobRunHistory(
            Guid.NewGuid(), "J1", "J1 name", JobDefinitionType.DbLinkImport,
            "GovernedSchedule", "dispatcher", null, false, "test", null,
            occurrenceKey: "job:20260915T101500Z", nominalAtUtc: Anchor.AddMinutes(15));

        Assert.Equal("job:20260915T101500Z", scheduled.OccurrenceKey);
        Assert.Equal(Anchor.AddMinutes(15), scheduled.NominalAtUtc);
    }

    [Fact]
    public void Half_an_occurrence_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new JobRunHistory(
            Guid.NewGuid(), "J1", "J1 name", JobDefinitionType.DbLinkImport,
            "GovernedSchedule", "dispatcher", null, false, "test", null,
            occurrenceKey: "job:20260915T101500Z", nominalAtUtc: null));

        Assert.Throws<ArgumentException>(() => new JobRunHistory(
            Guid.NewGuid(), "J1", "J1 name", JobDefinitionType.DbLinkImport,
            "GovernedSchedule", "dispatcher", null, false, "test", null,
            occurrenceKey: null, nominalAtUtc: Anchor));
    }
}
