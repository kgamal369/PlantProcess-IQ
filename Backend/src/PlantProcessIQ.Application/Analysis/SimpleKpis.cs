namespace PlantProcessIQ.Application.Analysis;

public sealed record DefectByLine(string Line, int DefectCount);
public sealed record SpeedByGrade(string Grade, double AvgCastingSpeed);
public sealed record DowntimeByArea(string Area, double ProductionStopMinutes);

/// Transparent "what happened" primitives. Every result is exact and explainable.
public static class SimpleKpis
{
    public static IReadOnlyList<DefectByLine> DefectCountByLine(IEnumerable<(string line, string defectCode)> rows) =>
        rows.GroupBy(r => r.line)
            .Select(g => new DefectByLine(g.Key, g.Count()))
            .OrderByDescending(x => x.DefectCount).ThenBy(x => x.Line).ToList();

    public static IReadOnlyList<SpeedByGrade> AvgCastingSpeedByGrade(IEnumerable<(string grade, double speed)> rows) =>
        rows.GroupBy(r => r.grade)
            .Select(g => new SpeedByGrade(g.Key, Math.Round(g.Average(x => x.speed), 4)))
            .OrderBy(x => x.Grade).ToList();

    public static IReadOnlyList<DowntimeByArea> DowntimeMinutesByArea(IEnumerable<(string area, double productionStopMinutes)> rows) =>
        rows.GroupBy(r => r.area)
            .Select(g => new DowntimeByArea(g.Key, Math.Round(g.Sum(x => x.productionStopMinutes), 2)))
            .OrderByDescending(x => x.ProductionStopMinutes).ThenBy(x => x.Area).ToList();

    /// First-Pass Strand Yield: prime tons as a percent of cast tons.
    public static double Fpsy(double castTons, double primeTons)
    {
        if (castTons <= 0) throw new ArgumentOutOfRangeException(nameof(castTons), "Cast tons must be positive.");
        return Math.Round(primeTons / castTons * 100.0, 4);
    }
}