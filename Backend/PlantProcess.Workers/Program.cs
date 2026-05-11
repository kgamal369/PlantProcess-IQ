using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlantProcess.Application;
using PlantProcess.Infrastructure;
using PlantProcess.Workers;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

// ── Resolve stable absolute log path ─────────────────────────────────────────
var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
var logFilePath = Path.Combine(logDirectory, "plantprocess-worker-.log");

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
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .MinimumLevel.Override("PlantProcess", LogEventLevel.Verbose)
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

    // ── Application and Infrastructure DI ────────────────────────────────
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ── Register background worker ────────────────────────────────────────
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    host.Run();
}
catch (Exception ex) when (ex.GetType().Name == "HostAbortedException")
{
    Log.Debug(ex, "Host aborted during design-time operation (expected).");
}
catch (Exception ex)
{
    Log.Fatal(ex, "PlantProcess IQ Worker terminated unexpectedly. Version={AppVersion}", appVersion);
}
finally
{
    Log.Information("PlantProcess IQ Worker stopped. Version={AppVersion}", appVersion);
    Log.CloseAndFlush();
}