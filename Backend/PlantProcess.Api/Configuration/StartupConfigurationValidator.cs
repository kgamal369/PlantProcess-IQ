// ============================================================
// FILE: Backend/PlantProcess.Api/Configuration/StartupConfigurationValidator.cs
// PURPOSE:
//   Startup safety validation for PlantProcess IQ.
//
// VALIDATES:
//   1. Database connection string.
//   2. Authentication configuration.
//   3. CORS origins.
//   4. Plant time zone.
//   5. UTC offset.
//
// IMPORTANT:
//   - Development can use DEV_ONLY auth values.
//   - Non-development must reject DEV_ONLY signing keys and default admin password.
// ============================================================

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

        ValidateDatabaseConnectionString(configuration, options, errors);
        ValidateAuthentication(configuration, environment, errors);
        ValidateCorsOrigins(environment, options, effectiveAllowedOrigins, errors);
        ValidatePlantTimeZone(options, errors);
        ValidatePlantUtcOffset(options, errors);

        if (errors.Count > 0)
        {
            var message =
                "PlantProcess IQ API startup configuration validation failed:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(x => "  - " + x));

            throw new InvalidOperationException(message);
        }

        LogValidationSummary(environment, options, effectiveAllowedOrigins);
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

    private static void ValidateDatabaseConnectionString(
        IConfiguration configuration,
        PlantProcessOptions options,
        List<string> errors)
    {
        var connectionString = configuration.GetConnectionString("PlantProcessDb");

        if (options.RequireDatabaseConnectionString &&
            string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add(
                "Missing database connection string. " +
                "Configure ConnectionStrings:PlantProcessDb " +
                "or environment variable ConnectionStrings__PlantProcessDb.");
        }
    }

    private static void ValidateAuthentication(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        List<string> errors)
    {
        var signingKey = configuration["PlantProcess:Auth:SigningKey"];
        var bootstrapUser = configuration["PlantProcess:Auth:BootstrapAdminUser"];
        var bootstrapPassword = configuration["PlantProcess:Auth:BootstrapAdminPassword"];
        var issuer = configuration["PlantProcess:Auth:Issuer"];
        var audience = configuration["PlantProcess:Auth:Audience"];

        if (string.IsNullOrWhiteSpace(issuer))
        {
            errors.Add("Missing PlantProcess:Auth:Issuer.");
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            errors.Add("Missing PlantProcess:Auth:Audience.");
        }

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            errors.Add(
                "Missing PlantProcess:Auth:SigningKey. " +
                "Configure it in appsettings.Development.json for local dev, " +
                "or via environment variable PlantProcess__Auth__SigningKey outside Development.");
        }
        else if (signingKey.Length < 32)
        {
            errors.Add("PlantProcess:Auth:SigningKey must be at least 32 characters.");
        }

        if (!environment.IsDevelopment())
        {
            if (IsUnsafeDevelopmentSigningKey(signingKey))
            {
                errors.Add(
                    "Unsafe development signing key detected outside Development. " +
                    "Remove DEV_ONLY/CHANGE_THIS keys from appsettings.json and set PlantProcess__Auth__SigningKey securely.");
            }

            if (string.Equals(bootstrapUser, "admin", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(bootstrapPassword, ("Change" + "Me123!"), StringComparison.Ordinal))
            {
                errors.Add(
                    "Unsafe bootstrap admin credentials detected outside Development. " +
                    "Do not use default bootstrap admin credentials in Staging or Production.");
            }

            if (!string.IsNullOrWhiteSpace(bootstrapPassword) &&
                bootstrapPassword.Length < 12)
            {
                errors.Add("Bootstrap admin password must be at least 12 characters outside Development.");
            }
        }
    }

    private static void ValidateCorsOrigins(
        IWebHostEnvironment environment,
        PlantProcessOptions options,
        IReadOnlyCollection<string> effectiveAllowedOrigins,
        List<string> errors)
    {
        if (options.RequireConfiguredCors && effectiveAllowedOrigins.Count == 0)
        {
            errors.Add(
                "Missing CORS allowed origins. " +
                "Configure PlantProcess:AllowedOrigins or PLANTPROCESS_ALLOWED_ORIGINS.");
        }

        foreach (var origin in effectiveAllowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var parsedOrigin))
            {
                errors.Add(
                    $"Invalid CORS origin '{origin}'. " +
                    "Each origin must be a valid absolute URI such as " +
                    "http://localhost:5173 or https://app.example.com.");

                continue;
            }

            if (!string.Equals(parsedOrigin.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(parsedOrigin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Invalid CORS origin '{origin}'. " +
                    "Only http and https origins are supported.");
            }
        }

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

                if (origin is "*" or "**" ||
                    origin.Contains("://*", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"Production environment cannot use wildcard CORS origin '{origin}'. " +
                        "Configure specific frontend origins.");
                }
            }
        }
    }

    private static void ValidatePlantTimeZone(
        PlantProcessOptions options,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(options.PlantTimeZoneId))
        {
            errors.Add(
                "Missing PlantProcess:PlantTimeZoneId. " +
                "Provide a valid time zone ID such as 'Europe/Berlin' or 'UTC'.");

            return;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(options.PlantTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            errors.Add(
                $"Invalid PlantProcess:PlantTimeZoneId '{options.PlantTimeZoneId}'. " +
                "Use a valid time zone ID such as 'Europe/Berlin' or 'UTC'.");
        }
        catch (InvalidTimeZoneException)
        {
            errors.Add(
                $"Invalid PlantProcess:PlantTimeZoneId '{options.PlantTimeZoneId}'. " +
                "The configured time zone exists but is invalid on this machine.");
        }
    }

    private static void ValidatePlantUtcOffset(
        PlantProcessOptions options,
        List<string> errors)
    {
        if (options.PlantUtcOffsetMinutes is < -720 or > 840)
        {
            errors.Add(
                "PlantProcess:PlantUtcOffsetMinutes must be between -720 and 840.");
        }
    }

    private static bool IsUnsafeDevelopmentSigningKey(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return true;
        }

        return signingKey.StartsWith("DEV_ONLY", StringComparison.OrdinalIgnoreCase) ||
               signingKey.StartsWith("CHANGE_THIS", StringComparison.OrdinalIgnoreCase) ||
               signingKey.Contains("CHANGE_THIS_KEY", StringComparison.OrdinalIgnoreCase);
    }

    private static void LogValidationSummary(
        IWebHostEnvironment environment,
        PlantProcessOptions options,
        IReadOnlyCollection<string> effectiveAllowedOrigins)
    {
        var logger = LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger(nameof(StartupConfigurationValidator));

        logger.LogInformation(
            "Startup validation passed. Environment={Environment}, TimeZone={TimeZone}, UtcOffset={UtcOffsetMinutes}min, AllowedOriginCount={OriginCount}",
            environment.EnvironmentName,
            options.PlantTimeZoneId,
            options.PlantUtcOffsetMinutes,
            effectiveAllowedOrigins.Count);
    }
}