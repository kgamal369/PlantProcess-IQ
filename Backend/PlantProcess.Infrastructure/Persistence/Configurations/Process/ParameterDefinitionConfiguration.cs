using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;


namespace PlantProcess.Infrastructure.Persistence.Configurations.Process;

public class ParameterDefinitionConfiguration : IEntityTypeConfiguration<ParameterDefinition>
{
    public void Configure(EntityTypeBuilder<ParameterDefinition> builder)
    {
        builder.ToTable("parameter_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ParameterCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ParameterName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ValueType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(50);
        builder.Property(x => x.ParameterCategory).HasMaxLength(100);
        builder.Property(x => x.IndustryTemplate).HasMaxLength(100);
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.ExpectedMinValue).HasPrecision(18, 6);
        builder.Property(x => x.ExpectedMaxValue).HasPrecision(18, 6);

        builder.HasIndex(x => new { x.IndustryTemplate, x.ParameterCode }).IsUnique(); builder.HasIndex(x => x.ParameterCategory);
       
        builder.HasIndex(x => x.IndustryTemplate);

        builder.UsePostgresXminConcurrencyToken();
    }
}