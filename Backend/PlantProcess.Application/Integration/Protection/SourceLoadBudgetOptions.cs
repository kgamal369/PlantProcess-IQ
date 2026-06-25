namespace PlantProcess.Application.Integration.Protection;

/// <summary>
/// Configurable per-source load budget. Bound from configuration section
/// "PlantProcess:SourceLoad"; defaults are generous so existing reads pass and
/// only genuinely abusive (unbounded / over-cap / too-frequent) reads are throttled.
/// </summary>
public sealed class SourceLoadBudgetOptions
{
    public const string SectionName = "PlantProcess:SourceLoad";

    public int MaxRows { get; set; } = 50_000;
    public int StatementTimeoutSeconds { get; set; } = 30;
    public int MaxQueriesPerMinute { get; set; } = 120;
    public string? WindowStartUtc { get; set; }
    public string? WindowEndUtc { get; set; }

    public SourceLoadBudget ToBudget()
    {
        return new SourceLoadBudget(
            MaxRows,
            StatementTimeoutSeconds,
            MaxQueriesPerMinute,
            ParseWindow(WindowStartUtc),
            ParseWindow(WindowEndUtc));
    }

    private static TimeOnly? ParseWindow(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        return TimeOnly.TryParse(value, out var parsed) ? parsed : (TimeOnly?)null;
    }
}