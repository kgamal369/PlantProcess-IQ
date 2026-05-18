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
            ?? "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123";
        var optionsBuilder = new DbContextOptionsBuilder<PlantProcessDbContext>();

        optionsBuilder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new PlantProcessDbContext(optionsBuilder.Options);
    }
}