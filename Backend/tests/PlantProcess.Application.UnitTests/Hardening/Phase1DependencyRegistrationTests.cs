using Xunit;

namespace PlantProcess.Application.UnitTests.Hardening;

public sealed class Phase1DependencyRegistrationTests
{
    [Fact]
    public void Application_dependency_injection_must_bind_real_services_not_notimplemented_null_objects()
    {
        var root = FindRepoRoot();
        var file = Path.Combine(root, "Backend", "PlantProcess.Application", "DependencyInjection.cs");

        Assert.True(File.Exists(file), $"DependencyInjection.cs not found at {file}");

        var source = File.ReadAllText(file);
        var compact = source.Replace(" ", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);

        Assert.DoesNotContain("NotImplementedImportBatchService", source);
        Assert.DoesNotContain("NotImplementedSourceSystemService", source);
        Assert.DoesNotContain("NotImplementedGenealogyService", source);
        Assert.DoesNotContain("NotImplementedMaterialService", source);

        Assert.Contains("AddScoped<IImportBatchService,ImportBatchService>", compact);
        Assert.Contains("AddScoped<ISourceSystemService,SourceSystemService>", compact);
        Assert.Contains("AddScoped<IGenealogyService,GenealogyService>", compact);
        Assert.Contains("AddScoped<IMaterialService,MaterialService>", compact);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Backend")) &&
                Directory.Exists(Path.Combine(current.FullName, "Frontend")))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root could not be found.");
    }
}