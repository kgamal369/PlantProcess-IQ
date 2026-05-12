using PlantProcess.Api.Options;

namespace PlantProcess.Api.Configuration;

public static class StartupConfigurationValidator
{
    public static void Validate(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        PlantProcessOptions options,
        IReadOnlyCollection<string> effectiveAllowedOrigins)
    {
        var errors = new List<string>();

        var connectionString = configuration.GetConnectionString("PlantProcessDb");

        if (options.RequireDatabaseConnectionString && string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add(
                "Missing database connection string. Configure ConnectionStrings:PlantProcessDb or environment variable ConnectionStrings__PlantProcessDb.");
        }

        if (options.RequireConfiguredCors && effectiveAllowedOrigins.Count == 0)
        {
            errors.Add(
                "Missing CORS allowed origins. Configure PlantProcess:AllowedOrigins or PLANTPROCESS_ALLOWED_ORIGINS.");
        }

        if (string.IsNullOrWhiteSpace(options.PlantTimeZoneId))
        {
            errors.Add("Missing PlantProcess:PlantTimeZoneId.");
        }

        if (options.PlantUtcOffsetMinutes is < -720 or > 840)
        {
            errors.Add("PlantProcess:PlantUtcOffsetMinutes must be between -720 and 840 minutes.");
        }

        if (environment.IsProduction())
        {
            var hasLocalhostOrigin = effectiveAllowedOrigins.Any(origin =>
                origin.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                origin.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase));

            if (hasLocalhostOrigin)
            {
                errors.Add(
                    "Production environment should not use localhost CORS origins. Configure real frontend origin.");
            }
        }

        if (errors.Count == 0)
            return;

        var message =
            "PlantProcess IQ startup configuration validation failed:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, errors.Select(x => "- " + x));

        throw new InvalidOperationException(message);
    }

    public static IReadOnlyList<string> BuildEffectiveAllowedOrigins(
        PlantProcessOptions options,
        IConfiguration configuration)
    {
        var origins = new List<string>();

        if (options.AllowedOrigins is { Length: > 0 })
        {
            origins.AddRange(options.AllowedOrigins);
        }

        var envOrigins = configuration["PLANTPROCESS_ALLOWED_ORIGINS"];

        if (!string.IsNullOrWhiteSpace(envOrigins))
        {
            origins.AddRange(
                envOrigins
                    .Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return origins
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}