// Storage topology convention.
//
// Runs after every IEntityTypeConfiguration has been applied, so the relational
// table name is already decided, and moves each mapped entity onto the schema the
// frozen assignment gives it. It contains no vocabulary and no prefix rule: a
// table the assignment does not name is left exactly where it was, and the
// topology gate fails the build rather than letting it drift silently.

using Microsoft.EntityFrameworkCore;

namespace PlantProcess.Infrastructure.Persistence.Topology;

public static class StorageTopologyConvention
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var table = entityType.GetTableName();

            if (string.IsNullOrWhiteSpace(table))
            {
                continue;
            }

            if (!StorageTopologyMap.TryGetSchema(table, out var schema))
            {
                continue;
            }

            entityType.SetSchema(schema);
        }
    }
}
