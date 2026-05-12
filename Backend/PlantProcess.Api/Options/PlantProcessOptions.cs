namespace PlantProcess.Api.Options;

public sealed class PlantProcessOptions
{
    public const string SectionName = "PlantProcess";

    public string PlantTimeZoneId { get; set; } = "Europe/Berlin";

    public int PlantUtcOffsetMinutes { get; set; } = 60;

    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    public bool RequireConfiguredCors { get; set; } = true;

    public bool RequireDatabaseConnectionString { get; set; } = true;
}