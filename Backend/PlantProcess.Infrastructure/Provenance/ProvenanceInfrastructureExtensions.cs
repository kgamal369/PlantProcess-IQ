using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Application.Analytics.Calibration;
using PlantProcess.Application.Provenance;
using PlantProcess.Infrastructure.Analytics;

namespace PlantProcess.Infrastructure.Provenance;

public static class ProvenanceInfrastructureExtensions
{
    /// <summary>Registers the Doctrine §2 provenance guard, resolver, and risk calibration services.</summary>
    public static IServiceCollection AddProvenanceAndCalibration(this IServiceCollection services)
    {
        services.AddScoped<IProvenanceResolver, NpgsqlProvenanceResolver>();
        services.AddScoped<ProvenanceClaimGuard>();
        services.AddSingleton<RiskCalibrationService>();
        services.AddScoped<IRiskCalibrationProvider, NpgsqlRiskCalibrationProvider>();
        return services;
    }
}