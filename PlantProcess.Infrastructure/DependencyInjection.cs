using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PlantProcessDb")
            ?? throw new InvalidOperationException("Missing PlantProcessDb connection string.");

        services.AddDbContext<PlantProcessDbContext>(options =>
        {
            options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MaxBatchSize(100);
                        npgsql.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorCodesToAdd: null);
                    })
                .UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IPlantProcessDbContext>(
            provider => provider.GetRequiredService<PlantProcessDbContext>());

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

        return services;
    }
}