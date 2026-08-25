using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

// =====================================================================================
// System template startup provisioning gate.
//
// The product has one operational authority for system dashboard templates. This gate
// holds the line that the authority is actually invoked on the startup path, which is
// the failure a retired SQL seed hid for months: the templates existed, but not because
// anything in the running product created them.
//
// Self-contained: it resolves the repository root itself and declares no shared type,
// so it compiles regardless of which other gates are present.
// =====================================================================================

[Trait("Gate", "SystemTemplateProvisioning")]
public sealed class SystemTemplateProvisioningTests
{
    private const string ProgramPath =
        "Backend/PlantProcess.Api/Program.cs";

    private const string ServicePath =
        "Backend/PlantProcess.Api/Hosting/SystemTemplateProvisioningHostedService.cs";

    private const string ServiceTypeName = "SystemTemplateProvisioningHostedService";

    [Fact]
    public void The_authority_is_invoked_on_the_startup_path()
    {
        var program = ReadStripped(ProgramPath);

        Assert.True(
            Regex.IsMatch(program, @"AddHostedService<[^>]*" + ServiceTypeName + @">"),
            "Nothing invokes the system-template authority at startup. A clean installation would then " +
            "produce no system templates at all, and a Release Truth pass over them would be vacuous.");
    }

    [Fact]
    public void The_startup_service_calls_the_single_authority_and_nothing_else()
    {
        var source = ReadStripped(ServicePath);

        Assert.Contains("EnsureSystemTemplatesAsync", source, StringComparison.Ordinal);

        Assert.False(
            Regex.IsMatch(source, @"INSERT\s+INTO|CREATE\s+TABLE|ExecuteSqlRaw", RegexOptions.IgnoreCase),
            "Startup provisioning must delegate to the authority. Writing definitions directly would make it a " +
            "second authority, which is the defect this whole correction removes.");
    }

    [Fact]
    public void A_provisioning_failure_does_not_stop_the_api()
    {
        var source = ReadStripped(ServicePath);

        Assert.True(
            Regex.IsMatch(source, @"catch\s*\(\s*Exception"),
            "A template that cannot be reconciled is a degraded surface, not a reason to refuse to serve. " +
            "Startup provisioning must not be able to prevent the API from starting.");
    }

    [Fact]
    public void Startup_provisioning_carries_no_plant_specific_vocabulary()
    {
        var source = ReadStripped(ServicePath);

        var terms = new[] { "Casting" + "Speed", "co" + "il", "he" + "at", "cas" + "ter" };
        var offenders = terms.Where(t => Regex.IsMatch(source, @"\b" + t + @"s?\b", RegexOptions.IgnoreCase)).ToList();

        Assert.True(
            offenders.Count == 0,
            "Startup provisioning must stay generic across industries:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static string ReadStripped(string relativePath)
    {
        var full = Path.Combine(
            FindRepositoryRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(full), "Expected file is missing: " + relativePath);

        var text = File.ReadAllText(full);
        var withoutBlocks = Regex.Replace(text, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"//[^\r\n]*", string.Empty);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                Directory.Exists(Path.Combine(directory.FullName, "Backend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found from " + AppContext.BaseDirectory);
    }
}