using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PlantProcess.Infrastructure.Persistence.Configurations;

public static class PostgresConcurrencyExtensions
{
    public static void UsePostgresXminConcurrencyToken<TEntity>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.Property<uint>("Version")
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");
    }
}