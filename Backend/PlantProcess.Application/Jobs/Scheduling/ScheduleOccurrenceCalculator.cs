// T-106 Phase A: deterministic occurrence calculation and occurrence identity.
// Pure kernel. The dispatcher that consumes it is a later, externally gated slice.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PlantProcess.Application.Jobs.Scheduling;

/// <summary>
/// One nominal occurrence. Identity is the nominal instant, never the jittered instant,
/// so two schedulers computing the same occurrence produce the same key.
/// </summary>
public sealed record ScheduleOccurrence(DateTime NominalUtc, DateTime DispatchUtc, string OccurrenceKey);

public static class ScheduleOccurrenceCalculator
{
    private const int MaximumMinuteScan = 366 * 24 * 60;

    /// <summary>Deterministic occurrence identity: job plus nominal instant. No clock, no sequence.</summary>
    public static string BuildOccurrenceKey(Guid jobDefinitionId, DateTime nominalUtc)
    {
        var utc = DateTime.SpecifyKind(nominalUtc, DateTimeKind.Utc);
        return jobDefinitionId.ToString("D", CultureInfo.InvariantCulture)
            + ":" + utc.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The next nominal occurrence strictly after <paramref name="afterUtc"/>.
    /// Returns null for manual and event schedules, which are not time-driven.
    /// </summary>
    public static DateTime? NextOccurrenceUtc(ScheduleValidationResult schedule, DateTime afterUtc, DateTime? anchorUtc = null)
    {
        if (schedule is null) { throw new ArgumentNullException(nameof(schedule)); }
        if (!schedule.IsValid) { throw new InvalidOperationException("A refused schedule has no occurrences."); }

        var after = DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc);

        if (schedule.Kind == ScheduleKind.Manual || schedule.Kind == ScheduleKind.Event) { return null; }

        if (schedule.Kind == ScheduleKind.MicroBatch)
        {
            var interval = schedule.Interval!.Value;
            var anchor = DateTime.SpecifyKind(anchorUtc ?? after, DateTimeKind.Utc);
            if (anchor > after) { return anchor; }
            var elapsed = after - anchor;
            var periods = (long)Math.Floor(elapsed.Ticks / (double)interval.Ticks) + 1;
            return anchor.AddTicks(periods * interval.Ticks);
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId!);
        var cron = schedule.Cron!;

        // Walk minute by minute in local wall-clock time so DST is handled by the zone,
        // not by arithmetic on UTC. Bounded scan: an expression with no reachable minute
        // inside a year returns null instead of looping.
        var cursorLocal = TruncateToMinute(TimeZoneInfo.ConvertTimeFromUtc(after, zone)).AddMinutes(1);

        for (var i = 0; i < MaximumMinuteScan; i++)
        {
            if (cron.Matches(cursorLocal))
            {
                DateTime candidateUtc;
                if (TryResolveLocalToUtc(cursorLocal, zone, out candidateUtc) && candidateUtc > after)
                {
                    return candidateUtc;
                }
            }

            cursorLocal = cursorLocal.AddMinutes(1);
        }

        return null;
    }

    /// <summary>Occurrences due in (afterUtc, nowUtc], oldest first, bounded by <paramref name="maximum"/>.</summary>
    public static IReadOnlyList<DateTime> DueOccurrencesUtc(
        ScheduleValidationResult schedule, DateTime afterUtc, DateTime nowUtc, int maximum = 512, DateTime? anchorUtc = null)
    {
        var due = new List<DateTime>();
        var cursor = DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc);
        var now = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        while (due.Count < maximum)
        {
            var next = NextOccurrenceUtc(schedule, cursor, anchorUtc);
            if (next is null || next.Value > now) { break; }
            due.Add(next.Value);
            cursor = next.Value;
        }

        return due;
    }

    /// <summary>
    /// Applies the misfire policy to the occurrences that were due while nothing was running.
    /// SkipToNext claims nothing now; RunOnceImmediately claims the newest missed occurrence;
    /// RunAllMissed claims every missed occurrence in order.
    /// </summary>
    public static IReadOnlyList<ScheduleOccurrence> PlanDispatch(
        Guid jobDefinitionId,
        ScheduleValidationResult schedule,
        MisfirePolicy misfire,
        DateTime lastNominalUtc,
        DateTime nowUtc,
        int jitterSeconds = 0,
        int maximum = 512,
        DateTime? anchorUtc = null)
    {
        var missed = DueOccurrencesUtc(schedule, lastNominalUtc, nowUtc, maximum, anchorUtc);
        if (missed.Count == 0) { return Array.Empty<ScheduleOccurrence>(); }

        IReadOnlyList<DateTime> claimed = misfire switch
        {
            MisfirePolicy.SkipToNext => Array.Empty<DateTime>(),
            MisfirePolicy.RunOnceImmediately => new[] { missed[missed.Count - 1] },
            MisfirePolicy.RunAllMissed => missed,
            _ => throw new ArgumentOutOfRangeException(nameof(misfire))
        };

        var planned = new List<ScheduleOccurrence>(claimed.Count);
        foreach (var nominal in claimed)
        {
            var dispatch = jitterSeconds <= 0
                ? nominal
                : nominal.AddSeconds(DeterministicJitterSeconds(jobDefinitionId, nominal, jitterSeconds));
            planned.Add(new ScheduleOccurrence(nominal, dispatch, BuildOccurrenceKey(jobDefinitionId, nominal)));
        }

        return planned;
    }

    /// <summary>
    /// Jitter is derived from the occurrence identity, not from a random source, so a
    /// restarted scheduler computes the same dispatch instant for the same occurrence.
    /// </summary>
    public static int DeterministicJitterSeconds(Guid jobDefinitionId, DateTime nominalUtc, int jitterSeconds)
    {
        if (jitterSeconds <= 0) { return 0; }
        unchecked
        {
            var hash = 17;
            foreach (var b in jobDefinitionId.ToByteArray()) { hash = (hash * 31) + b; }
            hash = (hash * 31) + nominalUtc.Ticks.GetHashCode();
            var positive = hash & 0x7fffffff;
            return positive % jitterSeconds;
        }
    }

    private static DateTime TruncateToMinute(DateTime value)
        => new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);

    /// <summary>
    /// Spring-forward local times do not exist and are skipped. Ambiguous fall-back local
    /// times resolve to the earlier UTC instant, deterministically, and run once.
    /// </summary>
    private static bool TryResolveLocalToUtc(DateTime local, TimeZoneInfo zone, out DateTime utc)
    {
        utc = default;
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(unspecified)) { return false; }

        if (zone.IsAmbiguousTime(unspecified))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(unspecified);
            var earliest = offsets[0];
            foreach (var offset in offsets)
            {
                if (offset > earliest) { earliest = offset; }
            }

            utc = DateTime.SpecifyKind(unspecified - earliest, DateTimeKind.Utc);
            return true;
        }

        utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
        return true;
    }
}
