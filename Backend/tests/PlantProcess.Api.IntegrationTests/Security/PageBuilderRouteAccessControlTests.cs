using System.Linq;
using System.Reflection;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Security;

/// PPIQ T-042. THE PAGE BUILDER ROUTE FAMILY IS MAPPED, AND STAYS MAPPED.
///
/// The defect this exists to prevent, measured on 09-Aug: /pages carried no
/// matrix entry at all. Deny-by-default refused POST /pages with 403 - the
/// Page Builder could not persist a page - while GET /pages was served
/// ANONYMOUSLY through the ("/", GET, anonymous) fallback. Half-open for reads,
/// shut for writes, and nothing failing loudly enough to notice until an
/// authoring flow tried to save.
///
/// The Matrix is a private static on AccessControlMiddleware - the file is
/// named PlantAccessControl.cs but that is not the type, and I asserted the
/// filename instead of reading the declaration.
///
/// These proofs read the matrix by reflection rather than by starting a host:
/// the question is what the table says, and a table is a better witness than a
/// round trip that could pass for an unrelated reason.
public sealed class PageBuilderRouteAccessControlTests
{
    private static (string Prefix, string[] Methods, string Permission, bool Anonymous)[] Matrix()
    {
        var type = typeof(PlantProcess.Api.Security.AccessControlMiddleware);
        var field = type.GetField("Matrix", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);

        return ((string Prefix, string[] Methods, string Permission, bool Anonymous)[])field!.GetValue(null)!;
    }

    private static (string Prefix, string[] Methods, string Permission, bool Anonymous) Resolve(string path, string method)
    {
        return Matrix()
            .OrderByDescending(entry => entry.Prefix.Length)
            .First(entry =>
                path.StartsWith(entry.Prefix, System.StringComparison.OrdinalIgnoreCase) &&
                entry.Methods.Contains(method));
    }

    [Theory]
    [InlineData("/pages", "GET")]
    [InlineData("/pages", "POST")]
    [InlineData("/pages/shift-production", "PUT")]
    [InlineData("/pages/shift-production", "PATCH")]
    [InlineData("/pages/shift-production", "DELETE")]
    [InlineData("/pages/shift-production/publish", "POST")]
    [InlineData("/pages/shift-production/unpublish", "POST")]
    public void The_whole_page_route_family_resolves_to_page_design(string path, string method)
    {
        var entry = Resolve(path, method);

        Assert.Equal("/pages", entry.Prefix);
        Assert.Equal("page.design", entry.Permission);
        Assert.False(entry.Anonymous);
    }

    [Fact]
    public void One_entry_covers_the_family_rather_than_one_entry_per_verb()
    {
        var pageEntries = Matrix().Where(entry => entry.Prefix == "/pages").ToArray();

        Assert.Single(pageEntries);
        Assert.Equal(new[] { "GET", "POST", "PUT", "PATCH", "DELETE" }, pageEntries[0].Methods);
    }

    [Fact]
    public void A_page_read_no_longer_falls_through_the_anonymous_root_entry()
    {
        // This is the security half of the defect. Before the /pages entry
        // existed, this resolved to ("/", GET, anonymous) and served page
        // definitions without a token.
        var entry = Resolve("/pages", "GET");

        Assert.NotEqual("/", entry.Prefix);
        Assert.False(entry.Anonymous);
    }

    [Fact]
    public void The_authoring_permission_is_the_one_the_dashboard_definitions_use()
    {
        // Not a new permission, and not assistant.use: the same page.design the
        // sibling authoring route already carries.
        Assert.Equal(
            Resolve("/analytics/dashboard/definitions", "POST").Permission,
            Resolve("/pages", "POST").Permission);
    }
}