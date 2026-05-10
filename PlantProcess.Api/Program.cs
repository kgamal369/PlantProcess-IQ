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
using PlantProcess.Api.Endpoints.Validation;
using PlantProcess.Api.Endpoints.Workflow;
using PlantProcess.Api.Middleware;
using PlantProcess.Infrastructure;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithExceptionDetails()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/plantprocess-api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true,
        restrictedToMinimumLevel: LogEventLevel.Verbose)
    .CreateLogger();

try
{
    Log.Information("Starting PlantProcess IQ API.");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(options =>
    {
        options.CustomSchemaIds(type =>
            type.FullName!
                .Replace("+", "_")
                .Replace(".", "_"));
    });

    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    app.UseMiddleware<RequestResponseLoggingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "PlantProcess IQ API v1");
            options.RoutePrefix = "swagger";
        });
    }

    app.MapGet("/", () => Results.Redirect("/swagger"));

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.MapHealthEndpoints();
    app.MapPlantLayoutEndpoints();
    app.MapConfigurationEndpoints();
    app.MapIntegrationEndpoints();
    app.MapMaterialEndpoints();
    app.MapMaterialInvestigationEndpoints();
    app.MapProcessEndpoints();
    app.MapQualityEndpoints();
    app.MapRiskScoreEndpoints();
    app.MapDataQualityEndpoints();
    app.MapDataQualityScanEndpoints();
    app.MapWorkflowEndpoints();
    app.MapValidationEndpoints();
    app.MapDevSeedEndpoints();

    app.Run();
}
catch (Exception ex) when (ex.GetType().Name == "HostAbortedException")
{
    // EF Core design-time tools intentionally abort the host after discovering services.
    // This is not a real runtime crash.
    Log.Debug(ex, "Host aborted during EF Core design-time operation.");
}
catch (Exception ex)
{
    Log.Fatal(ex, "PlantProcess IQ API terminated unexpectedly.");
}
finally
{
    Log.Information("PlantProcess IQ API stopped.");
    Log.CloseAndFlush();
}