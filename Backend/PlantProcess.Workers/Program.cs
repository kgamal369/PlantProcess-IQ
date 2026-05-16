// ============================================================
// TASK 9 — Validate worker startup and scheduled jobs
// FILE: Backend/PlantProcess.Workers/Program.cs
//
// CHANGES vs current version:
//  1. DB connection string is validated at startup — Workers now fail
//     fast with a clear error if the connection string is missing,
//     exactly like the API does. Prevents silent misconfiguration where
//     the worker starts but all jobs fail silently.
//  2. Worker options section validation added — warns if all three jobs
//     are disabled so the operator knows the worker is idle.
//  3. Correct dependency order: Workers references Application +
//     Infrastructure only (NOT Api). Dependency on Api was incorrect.
// ============================================================

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlantProcess.Application;
using PlantProcess.Infrastructure;
using PlantProcess.Workers;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using PlantProcess.Application.Services.Integration;

// ── Resolve stable absolute log path ─────────────────────────────────────────
var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
var logFilePath  = Path.Combine(logDirectory, "plantprocess-worker-.log");

// ── AppVersion enricher ───────────────────────────────────────────────────────
var appVersion = Assembly
    .GetEntryAssembly()
    ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion
    ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
    ?? "dev";

// ── Bootstrap Serilog (same pipeline as the API) ─────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .MinimumLevel.Override("Microsoft",                                       LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime",                      LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command",  LogEventLevel.Warning)
    .MinimumLevel.Override("PlantProcess",                                    LogEventLevel.Verbose)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithExceptionDetails()
    .Enrich.WithProperty("AppVersion", appVersion)
    .Enrich.WithProperty("ServiceRole", "Worker")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [Worker] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: logFilePath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [Worker] [{MachineName}] [{AppVersion}] [{EnvironmentName}] {Message:lj}{NewLine}{Properties:j}{NewLine}{Exception}",
        restrictedToMinimumLevel: LogEventLevel.Verbose)
    .CreateLogger();

try
{
    Log.Information(
        "Starting PlantProcess IQ Worker. Version={AppVersion}, LogPath={LogPath}",
        appVersion,
        logFilePath);

    var builder = Host.CreateApplicationBuilder(args);

    // ── Serilog for hosted service logging ────────────────────────────────
    builder.Services.AddSerilog();

    // ── Startup validation ────────────────────────────────────────────────
    ValidateWorkerConfiguration(builder.Configuration, builder.Environment);

    // ── Application and Infrastructure DI ────────────────────────────────
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ── Register background worker ────────────────────────────────────────
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    // ── Phase 2: Register DB-backed system jobs at Worker startup ───────────────
    await using (var scope = host.Services.CreateAsyncScope())
    {
        var jobRegistration = scope.ServiceProvider.GetRequiredService<IJobRegistrationService>();
        var registrationResult = await jobRegistration.RegisterSystemJobsAsync(CancellationToken.None);

        if (registrationResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"System job registration failed: {registrationResult.Error?.Message}");
        }
    }

    host.Run();
    }
    catch (Exception ex) when (ex.GetType().Name == "HostAbortedException")
    {
        Log.Debug(ex, "Host aborted during design-time operation (expected).");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "PlantProcess IQ Worker terminated unexpectedly. Version={AppVersion}", appVersion);
        Environment.Exit(1);
    }
    finally
    {
        Log.Information("PlantProcess IQ Worker stopped. Version={AppVersion}", appVersion);
        Log.CloseAndFlush();
    }

    // ── Local helpers ─────────────────────────────────────────────────────────────
    static void ValidateWorkerConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var errors = new List<string>();

        // 1. Database connection string — same check as the API.
        var connectionString = configuration.GetConnectionString("PlantProcessDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add(
                "Missing database connection string. " +
                "Configure ConnectionStrings:PlantProcessDb or " +
                "environment variable ConnectionStrings__PlantProcessDb.");
        }

        // 2. Warn if all scheduled jobs are disabled (worker is effectively idle).
        var importEnabled     = configuration.GetValue<bool>("PlantProcess:Workers:EnableImportQueueProcessorJob", false);
        var dqEnabled         = configuration.GetValue<bool>("PlantProcess:Workers:EnableDataQualityScanJob",     false);
        var riskEnabled       = configuration.GetValue<bool>("PlantProcess:Workers:EnableRiskScoringJob",         false);

        if (!importEnabled && !dqEnabled && !riskEnabled)
        {
            Log.Warning(
                "All Worker jobs are disabled. " +
                "Set PlantProcess:Workers:Enable*Job = true to activate scheduled processing.");
        }

        if (errors.Count == 0)
        {
            Log.Information(
                "Worker startup validation passed. " +
                "ImportQueue={ImportEnabled}, DataQuality={DqEnabled}, RiskScoring={RiskEnabled}",
                importEnabled, dqEnabled, riskEnabled);
            return;
        }

        var message =
            "PlantProcess IQ Worker startup configuration validation failed:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, errors.Select(x => "- " + x));

        throw new InvalidOperationException(message);
    }
