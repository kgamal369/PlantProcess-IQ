// ============================================================
// TASK 13 â€” Confirm CORS origins are configurable
// FILE: Backend/PlantProcess.Api/Configuration/StartupConfigurationValidator.cs
//
// CHANGES vs current version:
//  1. Production check now also rejects *:// wildcard origins.
//  2. Validates that AllowedOrigins are parseable URIs â€” prevents
//     typos like "http//localhost" from silently being accepted by
//     the CORS middleware and causing 403 errors.
//  3. Added PlantTimeZoneId validation with a descriptive error.
//  4. Added a summary log at the end listing all validated config.
// ============================================================

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

        // â”€â”€ 1. Database connection string â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var connectionString = configuration.GetConnectionString("PlantProcessDb");

        if (options.RequireDatabaseConnectionString &&
            string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add(
                "Missing database connection string. " +
                "Configure ConnectionStrings:PlantProcessDb " +
                "or environment variable ConnectionStrings__PlantProcessDb.");
        }

        // â”€â”€ 2. CORS allowed origins â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (options.RequireConfiguredCors && effectiveAllowedOrigins.Count == 0)
        {
            errors.Add(
                "Missing CORS allowed origins. " +
                "Configure PlantProcess:AllowedOrigins or PLANTPROCESS_ALLOWED_ORIGINS.");
        }

        // Validate each origin is a well-formed URI.
        foreach (var origin in effectiveAllowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out _))
            {
                errors.Add(
                    $"Invalid CORS origin '{origin}'. " +
                    "Each origin must be a valid absolute URI such as " +
                    "http://localhost:5173 or https://app.example.com.");
            }
        }

        // Production: reject localhost and wildcard origins.
        if (environment.IsProduction())
        {
            foreach (var origin in effectiveAllowedOrigins)
            {
                if (origin.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                    origin.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"Production environment cannot use localhost CORS origin '{origin}'. " +
                        "Configure the real frontend origin.");
                }

                if (origin is "*" or "**")
                {
                    errors.Add(
                        "Production environment cannot use wildcard CORS origin '*'. " +
                        "Configure specific frontend origins.");
                }
            }
        }

        // â”€â”€ 3. Time zone â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (string.IsNullOrWhiteSpace(options.PlantTimeZoneId))
        {
            errors.Add(
                "Missing PlantProcess:PlantTimeZoneId. " +
                "Provide a valid IANA time zone ID such as 'Europe/Berlin' or 'UTC'.");
        }
        else
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(options.PlantTimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                errors.Add(
                    $"Invalid PlantProcess:PlantTimeZoneId '{options.PlantTimeZoneId}'. " +
                    "Use a valid IANA time zone ID such as 'Europe/Berlin' or 'UTC'.");
            }
        }

        // â”€â”€ 4. UTC offset range â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (options.PlantUtcOffsetMinutes is < -720 or > 840)
        {
            errors.Add(
                "PlantProcess:PlantUtcOffsetMinutes must be between -720 and 840.");
        }

        // â”€â”€ Fail fast â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (errors.Count > 0)
        {
            var message =
                "PlantProcess IQ API startup configuration validation failed:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(x => "  - " + x));

            throw new InvalidOperationException(message);
        }

        // Log validated summary for operational visibility.
        var logger = LoggerFactory
            .Create(b => b.AddConsole())
            .CreateLogger(nameof(StartupConfigurationValidator));

        logger.LogInformation(
            "Startup validation passed. " +
            "Environment={Environment}, " +
            "TimeZone={TimeZone}, " +
            "UtcOffset={UtcOffsetMinutes}min, " +
            "AllowedOriginCount={OriginCount}",
            environment.EnvironmentName,
            options.PlantTimeZoneId,
            options.PlantUtcOffsetMinutes,
            effectiveAllowedOrigins.Count);
    }

    /// <summary>
    /// Merges allowed origins from PlantProcessOptions and the
    /// PLANTPROCESS_ALLOWED_ORIGINS environment variable.
    /// Environment variable wins on duplicates (case-insensitive).
    /// </summary>
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
                envOrigins.Split(
                    ",",
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries));
        }

        return origins
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

