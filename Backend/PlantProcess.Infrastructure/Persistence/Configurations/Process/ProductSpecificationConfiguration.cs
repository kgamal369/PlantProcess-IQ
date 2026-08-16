using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Process;

public class ProductSpecificationConfiguration : IEntityTypeConfiguration<ProductSpecification>
{
    public void Configure(EntityTypeBuilder<ProductSpecification> builder)
    {
        builder.ToTable("product_specifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SpecificationCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ProductFamily).HasMaxLength(100);
        builder.Property(x => x.GradeOrRecipe).IsRequired().HasMaxLength(100);
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(50);
        builder.Property(x => x.Provenance).HasMaxLength(200);
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.MinValue).HasColumnType("numeric");
        builder.Property(x => x.TargetValue).HasColumnType("numeric");
        builder.Property(x => x.MaxValue).HasColumnType("numeric");

        builder.Property(x => x.EffectiveFromUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EffectiveToUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasOne<ParameterDefinition>()
            .WithMany()
            .HasForeignKey(x => x.ParameterDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ParameterDefinitionId);
        builder.HasIndex(x => x.GradeOrRecipe);
        builder.HasIndex(x => new { x.GradeOrRecipe, x.ParameterDefinitionId });

        // One specification per grade, parameter and effective start. A second
        // row for the same three would be two live limits for one thing.
        builder.HasIndex(x => new { x.GradeOrRecipe, x.ParameterDefinitionId, x.EffectiveFromUtc })
            .IsUnique()
            .HasFilter("is_deleted = false");

        // Matches every other configuration in this project: optimistic
        // concurrency on the Postgres xmin system column.
        builder.UsePostgresXminConcurrencyToken();
    }
}