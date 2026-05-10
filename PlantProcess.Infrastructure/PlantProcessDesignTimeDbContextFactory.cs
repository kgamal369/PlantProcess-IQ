using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PlantProcess.Infrastructure.Persistence;

public sealed class PlantProcessDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<PlantProcessDbContext>
{
    public PlantProcessDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("PLANTPROCESS_DB")
            ?? "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123";
        var optionsBuilder = new DbContextOptionsBuilder<PlantProcessDbContext>();

        optionsBuilder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new PlantProcessDbContext(optionsBuilder.Options);
    }
}