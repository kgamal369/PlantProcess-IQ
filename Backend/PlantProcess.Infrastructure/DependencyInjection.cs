using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Infrastructure.Integration.Connectors;
using PlantProcess.Infrastructure.Integration.Connectors.Csv;
using PlantProcess.Infrastructure.Integration.Connectors.Excel;
using PlantProcess.Infrastructure.Persistence;
using PlantProcess.Infrastructure.Connectors.Common;

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

        // --------------------------------------------------------------------
        // Phase 0 H-03 / H-04 / H-05
        // Real connector implementations.
        // Application owns abstractions; Infrastructure owns concrete providers.
        // --------------------------------------------------------------------
        services.AddScoped<IDataSourceConnector, CsvConnector>();
        services.AddScoped<ISchemaReader, CsvConnector>();
        services.AddScoped<IDataSourceReader, CsvConnector>();

        services.AddScoped<IDataSourceConnector, ExcelConnector>();
        services.AddScoped<ISchemaReader, ExcelConnector>();
        services.AddScoped<IDataSourceReader, ExcelConnector>();

        services.AddScoped<IDataSourceConnectorFactory, DataSourceConnectorFactory>();

        return services;
    }
}