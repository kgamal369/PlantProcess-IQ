using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure;

public sealed class PlantProcessDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<PlantProcessDbContext>
{
    public PlantProcessDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("PLANTPROCESS_DB")
            ?? throw new InvalidOperationException("Set ConnectionStrings__PlantProcessDb or PLANTPROCESS_DESIGNTIME_CONNECTION_STRING before running EF design-time commands.");
        var optionsBuilder = new DbContextOptionsBuilder<PlantProcessDbContext>();

        optionsBuilder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new PlantProcessDbContext(optionsBuilder.Options);
    }
}