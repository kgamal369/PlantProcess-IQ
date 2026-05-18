namespace PlantProcess.Application.Common.Time;

public sealed class PlantTimeContextResolver : IPlantTimeContextResolver
{
    public PlantTimeContext Resolve(
        string? requestedTimeZoneId,
        DateTime referenceUtc)
    {
        var utc = EnsureUtc(referenceUtc);

        var timeZoneId = string.IsNullOrWhiteSpace(requestedTimeZoneId)
            ? "UTC"
            : requestedTimeZoneId.Trim();

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var offset = timeZone.GetUtcOffset(utc);

            return new PlantTimeContext(
                timeZone.Id,
                (int)offset.TotalMinutes);
        }
        catch (TimeZoneNotFoundException)
        {
            return new PlantTimeContext("UTC", 0);
        }
        catch (InvalidTimeZoneException)
        {
            return new PlantTimeContext("UTC", 0);
        }
    }

    public DateTime ToPlantLocalTime(
        DateTime utcDateTime,
        PlantTimeContext context)
    {
        var utc = EnsureUtc(utcDateTime);

        return DateTime.SpecifyKind(
            utc.AddMinutes(context.UtcOffsetMinutes),
            DateTimeKind.Unspecified);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return value;

        if (value.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return value.ToUniversalTime();
    }
}


