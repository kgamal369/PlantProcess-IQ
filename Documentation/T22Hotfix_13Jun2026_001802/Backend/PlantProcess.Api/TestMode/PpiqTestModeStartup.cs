using Microsoft.Extensions.Options;
using PlantProcess.Api.Security;

namespace PlantProcess.Api.TestMode;

public static class PpiqTestModeStartup
{
    /// <summary>Call BEFORE builder.Build(). Binds, validates (Production refusal), seeds, registers ForceTier.</summary>
    public static WebApplicationBuilder AddPpiqTestMode(this WebApplicationBuilder builder)
    {
        var options = new PpiqTestModeOptions();
        builder.Configuration.GetSection(PpiqTestModeOptions.SectionName).Bind(options);

        PpiqTestModeGuard.Validate(options, builder.Environment.EnvironmentName);

        builder.Services.AddSingleton(options);

        if (options.SeedUsers)
        {
            // Reuses the EXISTING PlantProcess:Auth:Users mechanism (Development login fallback) -
            // no new auth path is introduced. Fixed, documented credentials (docs/TESTMODE.md).
            builder.Services.PostConfigure<AuthOptions>(auth =>
            {
                void Add(string user, string pass, string role) =>
                    auth.Users.Add(new AuthOptions.ConfiguredUser
                    {
                        UserName = user,
                        Password = pass,
                        Role = role,
                        DisplayName = $"TestMode {role}",
                        IsBootstrapAdmin = false,
                        ForcePasswordChangeOnFirstLogin = false
                    });

                if (!auth.Users.Any(u => string.Equals(u.UserName, "tm-admin", StringComparison.OrdinalIgnoreCase)))
                {
                    Add("tm-admin",    "TestMode-Admin-123!",    "Admin");
                    Add("tm-ceo",      "TestMode-Ceo-123!",      "Executive");
                    Add("tm-engineer", "TestMode-Engineer-123!", "ProcessEngineer");
                    Add("tm-operator", "TestMode-Operator-123!", "Operator");
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(options.ForceTier))
        {
            builder.Services.AddHostedService<PpiqTestModeForceTierHostedService>();
        }

        return builder;
    }

    /// <summary>Call AFTER builder.Build(), before app.Run(). Logs loudly + maps the status endpoint.</summary>
    public static WebApplication UsePpiqTestMode(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<PpiqTestModeOptions>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PlantProcess.TestMode");

        if (options.AnySwitchActive)
        {
            logger.LogWarning(
                "PPIQ-T022 TEST MODE ACTIVE. Env={Env} SeedUsers={Seed} ForceTier={Tier} AcceptRisk={Risk} - NEVER present this configuration as production behaviour.",
                app.Environment.EnvironmentName, options.SeedUsers, options.ForceTier ?? "(off)", options.IExplicitlyAcceptRisk);
        }

        if (options.StatusEndpoint && options.AnySwitchActive)
        {
            app.MapGet("/admin/testmode-status", (PpiqTestModeOptions o, IWebHostEnvironment env) => Results.Ok(new
            {
                testModeActive = o.AnySwitchActive,
                environment = env.EnvironmentName,
                seedUsers = o.SeedUsers,
                seededUserNames = o.SeedUsers ? new[] { "tm-admin", "tm-ceo", "tm-engineer", "tm-operator" } : Array.Empty<string>(),
                forceTier = string.IsNullOrWhiteSpace(o.ForceTier) ? null : o.ForceTier,
                acceptRisk = o.IExplicitlyAcceptRisk,
                doc = "docs/TESTMODE.md"
            }))
            .RequireAuthorization()
            .WithSummary("PPIQ-T022: echo of every active test-mode toggle.");
        }

        return app;
    }
}

/// <summary>
/// PPIQ-T022 ForceTier: writes the SAME runtime setting the demo seed uses
/// (demo_runtime_settings key 'license.defaultTier'), so the existing tier resolution
/// path picks it up - no parallel licensing mechanism is introduced.
/// Portable upsert (UPDATE then INSERT-if-missing) inside one transaction.
/// </summary>
public sealed class PpiqTestModeForceTierHostedService : IHostedService
{
    private readonly PpiqTestModeOptions _options;
    private readonly Npgsql.NpgsqlDataSource _dataSource;
    private readonly ILogger<PpiqTestModeForceTierHostedService> _logger;

    public PpiqTestModeForceTierHostedService(
        PpiqTestModeOptions options,
        Npgsql.NpgsqlDataSource dataSource,
        ILogger<PpiqTestModeForceTierHostedService> logger)
    {
        _options = options;
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var tier = _options.ForceTier!.Trim();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        await using (var create = connection.CreateCommand())
        {
            create.Transaction = tx;
            create.CommandText =
                """
                CREATE TABLE IF NOT EXISTS public.demo_runtime_settings(
                    key text PRIMARY KEY,
                    value text NOT NULL,
                    updated_at_utc timestamptz NOT NULL DEFAULT NOW()
                )
                """;
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = tx;
            update.CommandText =
                "UPDATE public.demo_runtime_settings SET value = @v, updated_at_utc = NOW() WHERE key = 'license.defaultTier'";
            update.Parameters.AddWithValue("v", tier);
            var rows = await update.ExecuteNonQueryAsync(cancellationToken);

            if (rows == 0)
            {
                await using var insert = connection.CreateCommand();
                insert.Transaction = tx;
                insert.CommandText =
                    "INSERT INTO public.demo_runtime_settings(key, value, updated_at_utc) VALUES ('license.defaultTier', @v, NOW())";
                insert.Parameters.AddWithValue("v", tier);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await tx.CommitAsync(cancellationToken);
        _logger.LogWarning("PPIQ-T022: ForceTier applied - license.defaultTier = {Tier}.", tier);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}