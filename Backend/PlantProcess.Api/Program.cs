using System.Reflection;
using PlantProcess.Api.Configuration;
using PlantProcess.Api.Endpoints.Analytics;
using PlantProcess.Api.Endpoints.Configuration;
using PlantProcess.Api.Endpoints.DataQuality;
using PlantProcess.Api.Endpoints.Development;
using PlantProcess.Api.Endpoints.Health;
using PlantProcess.Api.Endpoints.Integration;
using PlantProcess.Api.Endpoints.Materials;
using PlantProcess.Api.Endpoints.PlantLayout;
using PlantProcess.Api.Endpoints.Process;
using PlantProcess.Api.Endpoints.Quality;
using PlantProcess.Api.Endpoints.Reporting;
using PlantProcess.Api.Endpoints.Validation;
using PlantProcess.Api.Endpoints.Workflow;
using PlantProcess.Api.Middleware;
using PlantProcess.Api.Options;
using PlantProcess.Application;
using PlantProcess.Infrastructure;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

// ── Resolve a stable absolute log path regardless of working directory ──────
var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
var logFilePath = Path.Combine(logDirectory, "plantprocess-api-.log");

// ── Bootstrap logger ────────────────────────────────────────────────────────
var appVersion = Assembly
    .GetEntryAssembly()
    ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion
    ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
    ?? "dev";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .MinimumLevel.Override("PlantProcess", LogEventLevel.Verbose)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithExceptionDetails()
    .Enrich.WithProperty("AppVersion", appVersion)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId,-32} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: logFilePath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{MachineName}] [{AppVersion}] [{EnvironmentName}] {CorrelationId,-32} {Message:lj}{NewLine}{Properties:j}{NewLine}{Exception}",
        restrictedToMinimumLevel: LogEventLevel.Verbose)
    .CreateLogger();

try
{
    Log.Information(
        "Starting PlantProcess IQ API. Version={AppVersion}, LogPath={LogPath}",
        appVersion,
        logFilePath);

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ────────────────────────────────────────────────────────────
    builder.Host.UseSerilog();

    // ── Options + startup validation ───────────────────────────────────────
    builder.Services.Configure<PlantProcessOptions>(
        builder.Configuration.GetSection(PlantProcessOptions.SectionName));

    var plantProcessOptions =
        builder.Configuration
            .GetSection(PlantProcessOptions.SectionName)
            .Get<PlantProcessOptions>()
        ?? new PlantProcessOptions();

    var allowedOrigins = StartupConfigurationValidator.BuildEffectiveAllowedOrigins(
        plantProcessOptions,
        builder.Configuration);

    StartupConfigurationValidator.Validate(
        builder.Configuration,
        builder.Environment,
        plantProcessOptions,
        allowedOrigins);

    Log.Information(
        "PlantProcess IQ effective CORS origins: {AllowedOrigins}",
        string.Join(", ", allowedOrigins));

    // ── Infrastructure services ────────────────────────────────────────────
    builder.Services.AddMemoryCache();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ── CORS ───────────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("PlantProcessFrontend", policy =>
        {
            policy
                .WithOrigins(allowedOrigins.ToArray())
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    // ── Swagger ────────────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new()
        {
            Title = "PlantProcess IQ API",
            Version = "v1",
            Description =
                "Generic manufacturing process-to-quality intelligence platform. " +
                $"Version: {appVersion}"
        });

        options.CustomSchemaIds(type =>
            type.FullName!
                .Replace("+", "_")
                .Replace(".", "_"));
    });

    var app = builder.Build();

    // ── CORS must be early enough before browser calls endpoints ───────────
    app.UseCors("PlantProcessFrontend");

    // ── Middleware pipeline ────────────────────────────────────────────────
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<RequestResponseLoggingMiddleware>();

    // ── Swagger ────────────────────────────────────────────────────────────
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", $"PlantProcess IQ API v1 ({appVersion})");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "PlantProcess IQ API";
        });
    }

    // ── Root redirect ─────────────────────────────────────────────────────
    app.MapGet("/", () => Results.Redirect("/swagger"));

    // ── HTTPS only outside development ────────────────────────────────────
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    // ── Endpoint registration ─────────────────────────────────────────────
    app.MapHealthEndpoints();
    app.MapPlantLayoutEndpoints();
    app.MapConfigurationEndpoints();
    app.MapIntegrationEndpoints();
    app.MapImportWorkflowEndpoints();
    app.MapMaterialEndpoints();
    app.MapMaterialInvestigationEndpoints();
    app.MapProcessEndpoints();
    app.MapQualityEndpoints();
    app.MapRiskScoreEndpoints();
    app.MapCorrelationEndpoints();
    app.MapFeatureEngineeringEndpoints();
    app.MapDashboardEndpoints();
    app.MapReportingEndpoints();
    app.MapDataQualityEndpoints();
    app.MapDataQualityScanEndpoints();
    app.MapWorkflowEndpoints();
    app.MapValidationEndpoints();
    app.MapDevSeedEndpoints();

    app.Run();
}
catch (Exception ex) when (ex.GetType().Name == "HostAbortedException")
{
    Log.Debug(ex, "Host aborted during EF Core design-time operation. This is expected.");
}
catch (Exception ex)
{
    Log.Fatal(ex, "PlantProcess IQ API terminated unexpectedly. Version={AppVersion}", appVersion);
}
finally
{
    Log.Information("PlantProcess IQ API stopped. Version={AppVersion}", appVersion);
    Log.CloseAndFlush();
}