using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public sealed class ConnectionProfileConfiguration : IEntityTypeConfiguration<ConnectionProfile>
{
    public void Configure(EntityTypeBuilder<ConnectionProfile> builder)
    {
        builder.ToTable("connection_profiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ConnectionProfileCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ConnectionProfileName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ProviderType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ConnectionMode).IsRequired().HasMaxLength(50);

        builder.Property(x => x.HostName).HasMaxLength(300);
        builder.Property(x => x.DatabaseName).HasMaxLength(200);
        builder.Property(x => x.SchemaName).HasMaxLength(200);
        builder.Property(x => x.FileRootPath).HasMaxLength(1000);
        builder.Property(x => x.ApiBaseUrl).HasMaxLength(1000);
        builder.Property(x => x.SecretReference).HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.LastTestStatus).HasMaxLength(50);
        builder.Property(x => x.LastTestMessage).HasMaxLength(2000);

        builder.Property(x => x.ConnectionOptionsJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(x => x.ImportScheduleExpression)
                .IsRequired()
                .HasMaxLength(250)
                .HasDefaultValue("Every 15 minutes");

        builder.Property(x => x.ImportIntervalMinutes)
                .IsRequired()
                .HasDefaultValue(15);


        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastTestedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasOne<SourceSystemDefinition>()
            .WithMany()
            .HasForeignKey(x => x.SourceSystemDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SourceSystemDefinitionId);
        builder.HasIndex(x => x.ProviderType);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.ImportIntervalMinutes);
        builder.HasIndex(x => x.ConnectionProfileCode)
            .IsUnique()
            .HasFilter("is_deleted = FALSE");

        builder.UsePostgresXminConcurrencyToken();
    }
}