// PPIQ T-036 static guard, plus the T-035 hotfix it carries.
//
// The authored-SQL path is what T-036's Run Test calls. Its validator refusal
// was already named and safe; its EXECUTION failure appended the database's own
// text, which is the same leak T-035 closed on the dry-run path.
//
// This names the exact artifact it forbids rather than matching a shape.
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Task", "T036")]
public sealed class T036AuthoredSqlSafetyTests
{
    private static string SourceOf(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName, "Backend", "PlantProcess.Api", "Endpoints", "Prep", fileName);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            fileName + " was not located from the test base directory.");
    }

    [Fact]
    public void The_authored_sql_path_never_returns_the_database_text()
    {
        var source = SourceOf("AuthoringSupportEndpoints.cs");
        Assert.DoesNotContain("ex.MessageText", source, StringComparison.Ordinal);
        Assert.Contains("VisualMapperEndpoints.SafeDatabaseMessage(ex)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void There_is_one_sanitiser_and_it_is_reused_rather_than_copied()
    {
        var mapper = SourceOf("VisualMapperEndpoints.cs");
        var authoring = SourceOf("AuthoringSupportEndpoints.cs");
        Assert.Contains("internal static string SafeDatabaseMessage(Exception ex)", mapper, StringComparison.Ordinal);
        // A second declaration would be a second place for the rule to drift.
        Assert.DoesNotContain("string SafeDatabaseMessage(", authoring, StringComparison.Ordinal);
    }

    [Fact]
    public void The_returned_columns_carry_a_type_from_the_readers_own_metadata()
    {
        var source = SourceOf("AuthoringSupportEndpoints.cs");
        Assert.Contains("public sealed record AuthoredColumn(string Name, string DatabaseType);", source, StringComparison.Ordinal);
        Assert.Contains("reader.GetDataTypeName(i)", source, StringComparison.Ordinal);
        Assert.Contains("ColumnDetails: columnDetails", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_validator_refusal_is_still_carried_through_by_name()
    {
        var source = SourceOf("AuthoringSupportEndpoints.cs");
        Assert.Contains("Status: \"rejected_by_safe_sql\"", source, StringComparison.Ordinal);
        Assert.Contains("ErrorCode: errorCode", source, StringComparison.Ordinal);
    }
}