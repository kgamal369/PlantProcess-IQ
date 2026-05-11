namespace PlantProcess.Application.Common.Time;

public interface IPlantTimeContextResolver
{
    PlantTimeContext Resolve(
        string? requestedTimeZoneId,
        DateTime referenceUtc);

    DateTime ToPlantLocalTime(
        DateTime utcDateTime,
        PlantTimeContext context);
}