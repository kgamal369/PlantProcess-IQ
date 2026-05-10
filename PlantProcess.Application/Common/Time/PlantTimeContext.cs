namespace PlantProcess.Application.Common.Time;

public sealed record PlantTimeContext(
    string TimeZoneId,
    int UtcOffsetMinutes);