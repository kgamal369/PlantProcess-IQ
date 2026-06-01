using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PlantProcess.Api.Security;

/// <summary>
/// P01/P02 startup security guard.
///
/// Purpose:
/// - API must fail fast in non-development if production/runtime secrets are missing or unsafe.
/// - Source code is allowed to contain dangerous placeholder strings because this class must
///   explicitly block them. Runtime configuration is not allowed to use them.
/// </summary>
public static class P01P02StartupGuard
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var signingKey =
            configuration["PlantProcess:Auth:SigningKey"] ??
            configuration["Auth:SigningKey"] ??
            configuration["Jwt:SigningKey"] ??
            configuration["Jwt:Key"] ??
            configuration["Authentication:Jwt:SigningKey"] ??
            string.Empty;

        var bootstrapAdminPassword =
            configuration["PlantProcess:Auth:BootstrapAdminPassword"] ??
            configuration["Auth:BootstrapAdminPassword"] ??
            configuration["BootstrapAdminPassword"] ??
            string.Empty;

        var bootstrapAdminUserName =
            configuration["PlantProcess:Auth:BootstrapAdminUserName"] ??
            configuration["Auth:BootstrapAdminUserName"] ??
            configuration["BootstrapAdminUserName"] ??
            string.Empty;

        if (environment.IsDevelopment())
        {
            // Development is allowed to run with demo/bootstrap behavior,
            // but still reject extremely dangerous explicit secret values when present.
            RejectDangerous(
                name: "SigningKey",
                value: signingKey,
                required: false,
                allowDisabledSentinel: false,
                minimumLength: 32,
                productionStrict: false);

            return;
        }

        RejectDangerous(
            name: "SigningKey",
            value: signingKey,
            required: true,
            allowDisabledSentinel: false,
            minimumLength: 64,
            productionStrict: true);

        RejectDangerous(
            name: "BootstrapAdminPassword",
            value: bootstrapAdminPassword,
            required: false,
            allowDisabledSentinel: true,
            minimumLength: 0,
            productionStrict: true);

        if (!string.IsNullOrWhiteSpace(bootstrapAdminUserName) &&
            !bootstrapAdminPassword.Equals("__DISABLED__", StringComparison.OrdinalIgnoreCase) &&
            !bootstrapAdminPassword.StartsWith("DISABLED-", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "P01/P02 startup guard rejected production bootstrap configuration. " +
                "Bootstrap admin must be disabled outside Development. Set " +
                "PlantProcess:Auth:BootstrapAdminPassword to '__DISABLED__' or 'DISABLED-<random>'.");
        }
    }

    private static void RejectDangerous(
        string name,
        string? value,
        bool required,
        bool allowDisabledSentinel,
        int minimumLength,
        bool productionStrict)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            if (required)
            {
                throw new InvalidOperationException(
                    $"P01/P02 startup guard rejected missing required runtime secret: {name}.");
            }

            return;
        }

        if (allowDisabledSentinel &&
            (trimmed.Equals("__DISABLED__", StringComparison.OrdinalIgnoreCase) ||
             trimmed.StartsWith("DISABLED-", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var dangerousTokens = new[]
        {
            "CHANGE_THIS_KEY",
            "CHANGE_THIS",
            "CHANGE_ME",
            "__REPLACE_ME__",
            "DEV_ONLY",
            "DEFAULT",
            "ChangeMe123!",
            "ChangeMe123",
            "Admin123!",
            "Password123!",
            "plantprocess123",
            "password",
            "admin"
        };

        foreach (var token in dangerousTokens)
        {
            if (trimmed.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"P01/P02 startup guard rejected unsafe runtime secret '{name}'. " +
                    $"The configured value contains forbidden placeholder/default token '{token}'.");
            }
        }

        if (minimumLength > 0 && trimmed.Length < minimumLength)
        {
            throw new InvalidOperationException(
                $"P01/P02 startup guard rejected weak runtime secret '{name}'. " +
                $"Minimum length is {minimumLength} characters.");
        }

        if (productionStrict && name.Equals("BootstrapAdminPassword", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "P01/P02 startup guard rejected production bootstrap password. " +
                "Bootstrap password must be disabled outside Development using '__DISABLED__' or 'DISABLED-<random>'.");
        }
    }
}
