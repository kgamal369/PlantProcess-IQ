using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure;

public sealed class PlantProcessDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<PlantProcessDbContext>
{
    public PlantProcessDbContext CreateDbContext(string[] args)
    {
        // Use the SAME connection key the app and .env.dev use, so EF design-time
        // commands work after loading .env.dev. Legacy variables kept as fallbacks.
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__PlantProcessDb")
            ?? Environment.GetEnvironmentVariable("PLANTPROCESS_DESIGNTIME_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("PLANTPROCESS_DB")
            ?? throw new InvalidOperationException(
                "No design-time connection string found. Set ConnectionStrings__PlantProcessDb "
                + "(e.g. by loading deploy/compose/.env.dev) before running EF design-time commands.");

        var optionsBuilder = new DbContextOptionsBuilder<PlantProcessDbContext>();
        optionsBuilder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new PlantProcessDbContext(optionsBuilder.Options);
    }
}