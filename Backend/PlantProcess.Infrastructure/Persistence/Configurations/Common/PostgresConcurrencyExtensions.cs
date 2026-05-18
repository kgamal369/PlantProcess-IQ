using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Common;

public static class PostgresConcurrencyExtensions
{
    public static void UsePostgresXminConcurrencyToken<TEntity>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        // Important:
        // Do NOT call this shadow property "Version".
        // Some real domain entities, such as IndustryTemplate, already have a string Version property.
        // PostgreSQL xmin is a system column, so we map a separate shadow property to the xmin column.
        builder.Property<uint>("xmin")
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");
    }
}