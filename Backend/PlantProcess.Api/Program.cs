using System.Reflection;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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
using System.Text;
using Microsoft.AspNetCore.Authorization;
using PlantProcess.Api.Security;
using Serilog.Events;
using Serilog.Exceptions;
using PlantProcess.Api.Swagger;
using PlantProcess.Application.Integration.Interfaces.Jobs;

// Resolve a stable absolute log path regardless of working directory
var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
var logFilePath = Path.Combine(logDirectory, "plantprocess-api-.log");

// Bootstrap logger
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

    // -- Serilog 
    builder.Host.UseSerilog();

    // -- Options + startup validation 
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

    // -- Infrastructure services 
    builder.Services.AddMemoryCache();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // CORS
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

    // -- Swagger 
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

    builder.Services.Configure<AuthOptions>(
        builder.Configuration.GetSection("PlantProcess:Auth"));

    var authOptions = builder.Configuration
        .GetSection("PlantProcess:Auth")
        .Get<AuthOptions>() ?? new AuthOptions();

    if (string.IsNullOrWhiteSpace(authOptions.SigningKey) ||
    authOptions.SigningKey.Length < 32)
    {
        throw new InvalidOperationException(
            "PlantProcess:Auth:SigningKey must be configured and at least 32 characters.");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.SaveToken = true;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = authOptions.Issuer,

                ValidateAudience = true,
                ValidAudience = authOptions.Audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(authOptions.SigningKey)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        options.AddPolicy("PlantProcessAdmin", policy =>
            policy.RequireRole("Admin"));

        options.AddPolicy("PlantProcessDataManager", policy =>
            policy.RequireRole("Admin", "DataManager"));

        options.AddPolicy("PlantProcessEngineer", policy =>
            policy.RequireRole("Admin", "Engineer"));

        options.AddPolicy("PlantProcessViewer", policy =>
            policy.RequireRole("Admin", "DataManager", "Engineer", "Viewer"));
    });
    
    var app = builder.Build();

    // Phase 2: Register DB-backed system jobs at API startup 
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

    // CORS must be early enough before browser calls endpoints 
    app.UseCors("PlantProcessFrontend");

    // Middleware pipeline
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<RequestResponseLoggingMiddleware>();

    // Swagger 
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

    // Root redirect
    app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();

    // HTTPS only outside development 
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    // Endpoint registration
    app.MapAuthEndpoints();
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
    if (app.Environment.IsDevelopment())
    {
        app.MapDevSeedEndpoints();
    }    
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