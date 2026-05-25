// ============================================================
// FILE: Backend/PlantProcess.Infrastructure/DependencyInjection.cs
// CHANGES: Registered MsSqlConnector and MySqlConnector alongside
//          the existing CSV, Excel and PostgreSQL connectors.
// ============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Infrastructure.Persistence;
using PlantProcess.Infrastructure.Connectors.Common;
using PlantProcess.Infrastructure.Connectors.Csv;
using PlantProcess.Infrastructure.Connectors.Excel;
using PlantProcess.Infrastructure.Connectors.MySql;
using PlantProcess.Infrastructure.Connectors.PostgreSql;
using PlantProcess.Infrastructure.Connectors.SqlServer;
using PlantProcess.Infrastructure.Connectors.Oracle;

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
        // Data source connectors.
        // Each connector registers itself for all three abstraction interfaces
        // (IDataSourceConnector, ISchemaReader, IDataSourceReader).
        // The DataSourceConnectorFactory resolves the right implementation
        // by ProviderType key.
        // --------------------------------------------------------------------

        // CSV (flat file)
        services.AddScoped<IDataSourceConnector, CsvConnector>();
        services.AddScoped<ISchemaReader, CsvConnector>();
        services.AddScoped<IDataSourceReader, CsvConnector>();

        // Excel (.xlsx)
        services.AddScoped<IDataSourceConnector, ExcelConnector>();
        services.AddScoped<ISchemaReader, ExcelConnector>();
        services.AddScoped<IDataSourceReader, ExcelConnector>();

        // PostgreSQL
        services.AddScoped<PostgreSqlConnector>();
        services.AddScoped<IDataSourceConnector>(sp => sp.GetRequiredService<PostgreSqlConnector>());
        services.AddScoped<ISchemaReader>(sp => sp.GetRequiredService<PostgreSqlConnector>());
        services.AddScoped<IDataSourceReader>(sp => sp.GetRequiredService<PostgreSqlConnector>());

        // SQL Server / MSSQL (Fix 4)
        services.AddScoped<MsSqlConnector>();
        services.AddScoped<IDataSourceConnector>(sp => sp.GetRequiredService<MsSqlConnector>());
        services.AddScoped<ISchemaReader>(sp => sp.GetRequiredService<MsSqlConnector>());
        services.AddScoped<IDataSourceReader>(sp => sp.GetRequiredService<MsSqlConnector>());

        // MySQL / MariaDB (Fix 5)
        services.AddScoped<MySqlDataConnector>();
        services.AddScoped<IDataSourceConnector>(sp => sp.GetRequiredService<MySqlDataConnector>());
        services.AddScoped<ISchemaReader>(sp => sp.GetRequiredService<MySqlDataConnector>());
        services.AddScoped<IDataSourceReader>(sp => sp.GetRequiredService<MySqlDataConnector>());

         // Factory resolves connector by provider type string
        services.AddScoped<IDataSourceConnectorFactory, DataSourceConnectorFactory>();

        //Oracle 
        services.AddScoped<IDataSourceConnector, OracleConnector>();
        services.AddScoped<ISchemaReader, OracleConnector>();
        services.AddScoped<IDataSourceReader, OracleConnector>();

        return services;
    }
}
