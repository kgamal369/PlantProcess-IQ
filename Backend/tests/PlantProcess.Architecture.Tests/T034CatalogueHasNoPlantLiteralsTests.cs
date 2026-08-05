// PPIQ T-034 static guard.
//
// The task text is explicit: "Nothing in this tree may be a hardcoded table or
// column name", and its validation says to grep the file for any literal table
// name from the emulated plant. Before T-034 the staged-dataset endpoint marked
// key columns from a name list carrying four column names of the emulated
// plant, so the product knew about one customer's schema.
//
// This test NAMES THE EXACT LITERALS IT FORBIDS rather than matching a shape,
// because a guard that matches a shape reverts correct work and misses the
// thing it was written for.
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Task", "T034")]
public sealed class T034CatalogueHasNoPlantLiteralsTests
{
    // Column and table names that belong to the emulated plant, not to the
    // product. Add to this list whenever a new emulated name appears; never
    // remove one to make a build pass.
    private static readonly string[] ForbiddenLiterals =
    {
        "piece_id",
        "material_id",
        "heat_id",
        "coil_id",
        "src_heats",
        "src_coils",
    };

    private static string EndpointSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName, "Backend", "PlantProcess.Api", "Endpoints", "Prep", "VisualMapperEndpoints.cs");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "VisualMapperEndpoints.cs could not be located from the test base directory.");
    }

    [Fact]
    public void The_catalogue_endpoint_names_no_column_of_the_emulated_plant()
    {
        var source = EndpointSource();
        foreach (var literal in ForbiddenLiterals)
        {
            Assert.False(
                source.Contains(literal, StringComparison.OrdinalIgnoreCase),
                "PPIQ-T034: VisualMapperEndpoints.cs names '" + literal + "', which is a name from the "
                + "emulated plant. The catalogue must describe shapes and read metadata, never a customer's schema.");
        }
    }

    [Fact]
    public void The_key_marker_is_read_from_declared_constraints()
    {
        var source = EndpointSource();
        Assert.Contains("constraint_type IN ('PRIMARY KEY', 'UNIQUE')", source, StringComparison.Ordinal);
        Assert.Contains("declaresKeys.Contains(kv.Key) ? c.DeclaredKey : LooksLikeKey(c.Name)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_catalogue_reports_nullability_and_an_approximate_row_count()
    {
        var source = EndpointSource();
        Assert.Contains("c.is_nullable", source, StringComparison.Ordinal);
        Assert.Contains("reltuples", source, StringComparison.Ordinal);
        // An unanalysed table must not be reported as an empty one.
        Assert.Contains("estimate < 0 ? null : estimate", source, StringComparison.Ordinal);
    }
}