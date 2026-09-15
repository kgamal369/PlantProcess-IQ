// T-106 B2.3. The schedule strings this product already ships, translated once into the
// canonical grammar.
//
// These six shapes are not free text invented for convenience: JobRegistrationService
// writes them for every system job, so they are the compatibility contract. Rather than
// widening the canonical grammar to accept prose, or rewriting the seeds to make a parser
// pass, this translates a documented legacy string into the exact canonical specification
// it already means. Anything it cannot translate is refused, not guessed.
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PlantProcess.Application.Jobs.Scheduling;

public sealed record LegacyScheduleTranslation(
    bool IsRecognised,
    ScheduleKind Kind,
    string? CanonicalExpression,
    string Explanation);

public static class LegacyScheduleText
{
    private static readonly Regex EveryMinutes = new(
        @"^every\s+(\d{1,4})\s+minutes?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EveryHours = new(
        @"^every\s+(\d{1,3})\s+hours?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Daily = new(
        @"^daily\s+(\d{1,2}):(\d{2})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Weekly = new(
        @"^weekly\s+([a-z]+)\s+(\d{1,2}):(\d{2})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Manual = new(
        @"^(manual|on\s*demand|none)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LegacyScheduleTranslation Translate(string? text)
    {
        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return new LegacyScheduleTranslation(false, ScheduleKind.Manual, null, "An empty schedule is not a schedule.");
        }

        if (Manual.IsMatch(value))
        {
            return new LegacyScheduleTranslation(true, ScheduleKind.Manual, null, "Manual: the job runs only when someone runs it.");
        }

        var minutes = EveryMinutes.Match(value);
        if (minutes.Success)
        {
            var n = int.Parse(minutes.Groups[1].Value, CultureInfo.InvariantCulture);
            return n <= 0
                ? new LegacyScheduleTranslation(false, ScheduleKind.MicroBatch, null, "An interval of zero minutes is not an interval.")
                : new LegacyScheduleTranslation(true, ScheduleKind.MicroBatch, "PT" + n.ToString(CultureInfo.InvariantCulture) + "M",
                    "Every " + n + " minutes is a micro-batch interval.");
        }

        var hours = EveryHours.Match(value);
        if (hours.Success)
        {
            var n = int.Parse(hours.Groups[1].Value, CultureInfo.InvariantCulture);
            return n <= 0
                ? new LegacyScheduleTranslation(false, ScheduleKind.MicroBatch, null, "An interval of zero hours is not an interval.")
                : new LegacyScheduleTranslation(true, ScheduleKind.MicroBatch, "PT" + n.ToString(CultureInfo.InvariantCulture) + "H",
                    "Every " + n + " hours is a micro-batch interval.");
        }

        var daily = Daily.Match(value);
        if (daily.Success)
        {
            var hour = int.Parse(daily.Groups[1].Value, CultureInfo.InvariantCulture);
            var minute = int.Parse(daily.Groups[2].Value, CultureInfo.InvariantCulture);
            if (hour > 23 || minute > 59)
            {
                return new LegacyScheduleTranslation(false, ScheduleKind.Cron, null, "That is not a time of day.");
            }

            return new LegacyScheduleTranslation(true, ScheduleKind.Cron,
                minute.ToString(CultureInfo.InvariantCulture) + " " + hour.ToString(CultureInfo.InvariantCulture) + " * * *",
                "Daily at a wall-clock time is a cron expression in the job's time zone.");
        }

        var weekly = Weekly.Match(value);
        if (weekly.Success)
        {
            var day = DayNumber(weekly.Groups[1].Value);
            var hour = int.Parse(weekly.Groups[2].Value, CultureInfo.InvariantCulture);
            var minute = int.Parse(weekly.Groups[3].Value, CultureInfo.InvariantCulture);

            if (day is null)
            {
                return new LegacyScheduleTranslation(false, ScheduleKind.Cron, null,
                    "'" + weekly.Groups[1].Value + "' is not a day of the week.");
            }

            if (hour > 23 || minute > 59)
            {
                return new LegacyScheduleTranslation(false, ScheduleKind.Cron, null, "That is not a time of day.");
            }

            return new LegacyScheduleTranslation(true, ScheduleKind.Cron,
                minute.ToString(CultureInfo.InvariantCulture) + " " + hour.ToString(CultureInfo.InvariantCulture)
                    + " * * " + day.Value.ToString(CultureInfo.InvariantCulture),
                "Weekly on a named day is a cron expression in the job's time zone.");
        }

        return new LegacyScheduleTranslation(false, ScheduleKind.Manual, null,
            "'" + value + "' is not a canonical schedule and is not one of the legacy shapes this product ships.");
    }

    /// <summary>
    /// Canonical first, legacy second, refusal last. One entry point so no write path
    /// invents its own interpretation of a schedule string.
    /// </summary>
    public static ScheduleValidationResult ValidateAny(string? text, string? timeZoneId, MisfirePolicy misfire, int jitterSeconds)
    {
        var zone = string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId;
        var value = (text ?? string.Empty).Trim();

        foreach (var kind in new[] { ScheduleKind.Cron, ScheduleKind.MicroBatch })
        {
            var canonical = ScheduleGrammar.Validate(new ScheduleSpecification(kind, value, zone, misfire, jitterSeconds));
            if (canonical.IsValid) { return canonical; }
        }

        var translated = Translate(value);
        if (!translated.IsRecognised)
        {
            return ScheduleValidationResult.Refused(
                ScheduleKind.Manual, ScheduleRefusalReason.MalformedCronExpression, translated.Explanation);
        }

        return ScheduleGrammar.Validate(
            new ScheduleSpecification(translated.Kind, translated.CanonicalExpression, zone, misfire, jitterSeconds));
    }

    private static int? DayNumber(string name) => name.ToLowerInvariant() switch
    {
        "sunday" or "sun" => 0,
        "monday" or "mon" => 1,
        "tuesday" or "tue" or "tues" => 2,
        "wednesday" or "wed" => 3,
        "thursday" or "thu" or "thurs" => 4,
        "friday" or "fri" => 5,
        "saturday" or "sat" => 6,
        _ => null
    };
}
