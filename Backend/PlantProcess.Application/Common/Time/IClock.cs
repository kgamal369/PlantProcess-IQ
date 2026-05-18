namespace PlantProcess.Application.Common.Time;

public interface IClock
{
    DateTime UtcNow { get; }

    DateTimeOffset UtcNowOffset { get; }
}


