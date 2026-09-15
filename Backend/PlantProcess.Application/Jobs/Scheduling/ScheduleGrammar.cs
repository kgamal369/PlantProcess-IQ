// T-106 Phase A: governed schedule grammar (Design v4.10.3 Ch4 5.3.2a).
// Pure contract kernel. No persistence, no DI, no executor, no scheduler loop.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace PlantProcess.Application.Jobs.Scheduling;

/// <summary>The kinds of schedule the product persists. UI wording is presentation only.</summary>
public enum ScheduleKind
{
    Manual = 0,
    Cron = 1,
    MicroBatch = 2,
    Event = 3
}

/// <summary>What the product does with occurrences that were due while the scheduler was down.</summary>
public enum MisfirePolicy
{
    SkipToNext = 0,
    RunOnceImmediately = 1,
    RunAllMissed = 2
}

/// <summary>
/// Why a schedule was refused. These are reasons, not canonical product codes: the
/// SCxx code namespace is not declared by any authority this kernel can read, so the
/// gap is named here rather than invented. CENTRAL ruling required before persistence.
/// </summary>
public enum ScheduleRefusalReason
{
    None = 0,
    UnknownKind,
    ExpressionRequired,
    ExpressionNotAllowed,
    MalformedCronExpression,
    MalformedDuration,
    DurationOutOfRange,
    TimeZoneRequired,
    UnknownTimeZone,
    EventCodeMalformed,
    JitterOutOfRange
}

public sealed record ScheduleSpecification(
    ScheduleKind Kind,
    string? Expression,
    string? TimeZoneId,
    MisfirePolicy Misfire,
    int JitterSeconds);

public sealed record ScheduleValidationResult(
    bool IsValid,
    ScheduleRefusalReason Reason,
    string Message,
    ScheduleKind Kind,
    string? CanonicalExpression,
    string? TimeZoneId,
    TimeSpan? Interval,
    CronExpression? Cron)
{
    public static ScheduleValidationResult Refused(ScheduleKind kind, ScheduleRefusalReason reason, string message)
        => new(false, reason, message, kind, null, null, null, null);
}

/// <summary>Parses and validates the persisted machine grammar. One authority, no free text.</summary>
public static class ScheduleGrammar
{
    public static readonly TimeSpan MinimumMicroBatchInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaximumMicroBatchInterval = TimeSpan.FromDays(1);
    public const int MaximumJitterSeconds = 3600;

    public static bool TryParseKind(string? raw, out ScheduleKind kind)
    {
        kind = ScheduleKind.Manual;
        if (string.IsNullOrWhiteSpace(raw)) { return false; }
        switch (raw.Trim().ToLowerInvariant())
        {
            case "manual": kind = ScheduleKind.Manual; return true;
            case "cron": kind = ScheduleKind.Cron; return true;
            case "micro_batch": kind = ScheduleKind.MicroBatch; return true;
            case "event": kind = ScheduleKind.Event; return true;
            default: return false;
        }
    }

    public static string ToPersistedKind(ScheduleKind kind) => kind switch
    {
        ScheduleKind.Manual => "manual",
        ScheduleKind.Cron => "cron",
        ScheduleKind.MicroBatch => "micro_batch",
        ScheduleKind.Event => "event",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static ScheduleValidationResult Validate(ScheduleSpecification specification)
    {
        if (specification is null) { throw new ArgumentNullException(nameof(specification)); }

        if (!Enum.IsDefined(typeof(ScheduleKind), specification.Kind))
        {
            return ScheduleValidationResult.Refused(
                specification.Kind, ScheduleRefusalReason.UnknownKind, "Schedule kind is not a declared kind.");
        }

        if (specification.JitterSeconds < 0 || specification.JitterSeconds > MaximumJitterSeconds)
        {
            return ScheduleValidationResult.Refused(
                specification.Kind, ScheduleRefusalReason.JitterOutOfRange,
                "Jitter must be between 0 and " + MaximumJitterSeconds.ToString(CultureInfo.InvariantCulture) + " seconds.");
        }

        var expression = specification.Expression?.Trim();

        if (specification.Kind == ScheduleKind.Manual)
        {
            if (!string.IsNullOrEmpty(expression))
            {
                return ScheduleValidationResult.Refused(
                    specification.Kind, ScheduleRefusalReason.ExpressionNotAllowed,
                    "A manual schedule carries no expression.");
            }

            return new ScheduleValidationResult(
                true, ScheduleRefusalReason.None, "Manual schedule.", specification.Kind, null, null, null, null);
        }

        if (string.IsNullOrEmpty(expression))
        {
            return ScheduleValidationResult.Refused(
                specification.Kind, ScheduleRefusalReason.ExpressionRequired,
                "A " + ToPersistedKind(specification.Kind) + " schedule requires an expression.");
        }

        if (specification.Kind == ScheduleKind.Event)
        {
            if (!IsWellFormedEventCode(expression))
            {
                return ScheduleValidationResult.Refused(
                    specification.Kind, ScheduleRefusalReason.EventCodeMalformed,
                    "An event schedule requires a registered upper-case event code, not free text.");
            }

            return new ScheduleValidationResult(
                true, ScheduleRefusalReason.None, "Event schedule.", specification.Kind, expression, null, null, null);
        }

        // Wall-clock kinds require a declared zone: UTC is a choice, never a default.
        if (string.IsNullOrWhiteSpace(specification.TimeZoneId))
        {
            return ScheduleValidationResult.Refused(
                specification.Kind, ScheduleRefusalReason.TimeZoneRequired,
                "A time zone is required for a schedule with wall-clock semantics.");
        }

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(specification.TimeZoneId!);
        }
        catch (Exception)
        {
            return ScheduleValidationResult.Refused(
                specification.Kind, ScheduleRefusalReason.UnknownTimeZone,
                "Time zone '" + specification.TimeZoneId + "' is not known to this installation.");
        }

        if (specification.Kind == ScheduleKind.MicroBatch)
        {
            TimeSpan interval;
            if (!TryParseIsoDuration(expression, out interval))
            {
                return ScheduleValidationResult.Refused(
                    specification.Kind, ScheduleRefusalReason.MalformedDuration,
                    "A micro-batch schedule requires an ISO-8601 duration such as PT5M.");
            }

            if (interval < MinimumMicroBatchInterval || interval > MaximumMicroBatchInterval)
            {
                return ScheduleValidationResult.Refused(
                    specification.Kind, ScheduleRefusalReason.DurationOutOfRange,
                    "A micro-batch interval must be between one minute and one day.");
            }

            return new ScheduleValidationResult(
                true, ScheduleRefusalReason.None, "Micro-batch schedule.", specification.Kind,
                XmlConvert.ToString(interval), zone.Id, interval, null);
        }

        CronExpression cron;
        string cronError;
        if (!CronExpression.TryParse(expression, out cron, out cronError))
        {
            return ScheduleValidationResult.Refused(
                specification.Kind, ScheduleRefusalReason.MalformedCronExpression, cronError);
        }

        return new ScheduleValidationResult(
            true, ScheduleRefusalReason.None, "Cron schedule.", specification.Kind,
            cron.Canonical, zone.Id, null, cron);
    }

    private static bool IsWellFormedEventCode(string expression)
    {
        if (expression.Length < 3 || expression.Length > 64) { return false; }
        foreach (var c in expression)
        {
            var ok = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
            if (!ok) { return false; }
        }
        return true;
    }

    /// <summary>Time-only ISO-8601 durations. Calendar components are refused: they are not a fixed interval.</summary>
    public static bool TryParseIsoDuration(string raw, out TimeSpan interval)
    {
        interval = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(raw)) { return false; }
        var text = raw.Trim().ToUpperInvariant();
        if (text.Length < 3 || text[0] != 'P') { return false; }
        if (text.Contains('Y') || text.Contains('W')) { return false; }

        var timePart = text.IndexOf('T');
        if (timePart < 0) { return false; }
        for (var i = 1; i < timePart; i++)
        {
            // Only whole days are allowed before the time separator.
            var c = text[i];
            if (!char.IsDigit(c) && c != 'D') { return false; }
        }

        try
        {
            interval = XmlConvert.ToTimeSpan(text);
        }
        catch (FormatException)
        {
            return false;
        }

        return interval > TimeSpan.Zero;
    }
}

/// <summary>A five-field cron expression: minute hour day-of-month month day-of-week.</summary>
public sealed class CronExpression
{
    private readonly bool[] _minutes = new bool[60];
    private readonly bool[] _hours = new bool[24];
    private readonly bool[] _daysOfMonth = new bool[32];
    private readonly bool[] _months = new bool[13];
    private readonly bool[] _daysOfWeek = new bool[7];
    private readonly bool _dayOfMonthRestricted;
    private readonly bool _dayOfWeekRestricted;

    private CronExpression(string canonical, bool dayOfMonthRestricted, bool dayOfWeekRestricted)
    {
        Canonical = canonical;
        _dayOfMonthRestricted = dayOfMonthRestricted;
        _dayOfWeekRestricted = dayOfWeekRestricted;
    }

    public string Canonical { get; }

    public static bool TryParse(string raw, out CronExpression expression, out string error)
    {
        expression = null!;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "A cron expression is required.";
            return false;
        }

        var fields = raw.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
        {
            error = "A cron expression has exactly five fields: minute hour day-of-month month day-of-week.";
            return false;
        }

        var canonical = string.Join(" ", fields);
        var result = new CronExpression(canonical, fields[2] != "*", fields[4] != "*");

        if (!Fill(fields[0], 0, 59, result._minutes, "minute", ref error)) { return false; }
        if (!Fill(fields[1], 0, 23, result._hours, "hour", ref error)) { return false; }
        if (!Fill(fields[2], 1, 31, result._daysOfMonth, "day-of-month", ref error)) { return false; }
        if (!Fill(fields[3], 1, 12, result._months, "month", ref error)) { return false; }
        if (!Fill(fields[4], 0, 6, result._daysOfWeek, "day-of-week", ref error)) { return false; }

        expression = result;
        return true;
    }

    public bool Matches(DateTime localTime)
    {
        if (!_minutes[localTime.Minute]) { return false; }
        if (!_hours[localTime.Hour]) { return false; }
        if (!_months[localTime.Month]) { return false; }

        var dayOfMonthHit = _daysOfMonth[localTime.Day];
        var dayOfWeekHit = _daysOfWeek[(int)localTime.DayOfWeek];

        if (_dayOfMonthRestricted && _dayOfWeekRestricted) { return dayOfMonthHit || dayOfWeekHit; }
        if (_dayOfMonthRestricted) { return dayOfMonthHit; }
        if (_dayOfWeekRestricted) { return dayOfWeekHit; }
        return true;
    }

    private static bool Fill(string field, int min, int max, bool[] target, string name, ref string error)
    {
        foreach (var part in field.Split(','))
        {
            if (part.Length == 0)
            {
                error = "Empty " + name + " element.";
                return false;
            }

            var step = 1;
            var body = part;
            var slash = part.IndexOf('/');
            if (slash >= 0)
            {
                body = part.Substring(0, slash);
                var stepText = part.Substring(slash + 1);
                if (!int.TryParse(stepText, NumberStyles.None, CultureInfo.InvariantCulture, out step) || step <= 0)
                {
                    error = "Malformed step in " + name + " field.";
                    return false;
                }
            }

            int from;
            int to;
            if (body == "*")
            {
                from = min;
                to = max;
            }
            else
            {
                var dash = body.IndexOf('-');
                if (dash >= 0)
                {
                    if (!TryValue(body.Substring(0, dash), min, max, out from) ||
                        !TryValue(body.Substring(dash + 1), min, max, out to))
                    {
                        error = "Malformed range in " + name + " field.";
                        return false;
                    }
                }
                else
                {
                    if (!TryValue(body, min, max, out from))
                    {
                        error = "Malformed value in " + name + " field.";
                        return false;
                    }
                    to = from;
                }
            }

            if (from > to)
            {
                error = "Descending range in " + name + " field.";
                return false;
            }

            for (var v = from; v <= to; v += step) { target[v] = true; }
        }

        return true;
    }

    private static bool TryValue(string text, int min, int max, out int value)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
}
