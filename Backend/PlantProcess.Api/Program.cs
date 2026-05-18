using System.Reflection;
using PlantProcess.Api.Configuration;
using PlantProcess.Api.Endpoints.Analytics;
using PlantProcess.Api.Endpoints.Dashboarding;
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
using PlantProcess.Api.Endpoints.Admin;
using PlantProcess.Application;
using PlantProcess.Infrastructure;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using PlantProcess.Api.Swagger;
using PlantProcess.Application.Integration.Interfaces.Jobs;

// â”€â”€ Resolve a stable absolute log path regardless of working directory â”€â”€â”€â”€â”€â”€
var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
var logFilePath = Path.Combine(logDirectory, "plantprocess-api-.log");

// â”€â”€ Bootstrap logger â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ Serilog â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    builder.Host.UseSerilog();

    // â”€â”€ Options + startup validation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ Infrastructure services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    builder.Services.AddMemoryCache();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // â”€â”€ CORS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ Swagger â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new()
        {
            Title = "PlantProcess IQ API",
            Version = "v1",
            Description =
                "PlantProcess IQ is a generic manufacturing process-to-quality intelligence platform. " +
                $"Version: {appVersion}. " +
                "Use dashboard metadata endpoints to discover supported dimension and measure codes before calling widget query APIs.",
            Contact = new()
            {
                Name = "PlantProcess IQ"
            }
        });

        options.CustomSchemaIds(type =>
            type.FullName!
                .Replace("+", "_")
                .Replace(".", "_"));

        options.OperationFilter<SwaggerExamplesOperationFilter>();
        options.OperationFilter<SwaggerTagGroupingOperationFilter>();
    });

    var app = builder.Build();

    // â”€â”€ Phase 2: Register DB-backed system jobs at API startup â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var jobRegistration = scope.ServiceProvider.GetRequiredService<IJobRegistrationService>();
        var registrationResult = await jobRegistration.RegisterSystemJobsAsync(CancellationToken.None);

        if (registrationResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"System job registration failed: {registrationResult.Error?.Message}");
        }
    }

    // â”€â”€ CORS must be early enough before browser calls endpoints â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    app.UseCors("PlantProcessFrontend");

    // â”€â”€ Middleware pipeline â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<RequestResponseLoggingMiddleware>();

    // â”€â”€ Swagger â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ Root redirect â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    app.MapGet("/", () => Results.Redirect("/swagger"));

    // â”€â”€ HTTPS only outside development â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    // â”€â”€ Endpoint registration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
    app.MapAdminEndpoints();
    app.MapJobAdminEndpoints();
    app.MapConnectorAdminEndpoints();
    app.MapSchemaConfigurationEndpoints();
    
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

public partial class Program { }


