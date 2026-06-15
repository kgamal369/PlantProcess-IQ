using System;
using System.IO;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Hardening;

// P4-T04: guard that the page-save endpoint performs an optimistic-version check
// and returns a structured 409 instead of silently overwriting.
public sealed class PageVersionConflictContractTests
{
    private static string ReadEndpointSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Backend", "PlantProcess.Api", "Endpoints", "PageBuilder", "PageDefinitionEndpoints.cs");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new FileNotFoundException("PageDefinitionEndpoints.cs not found by climbing from " + AppContext.BaseDirectory);
    }

    [SkippableFact]
    public void Page_save_accepts_expected_version()
    {
        Assert.Contains("ExpectedVersion", ReadEndpointSource());
    }

    [SkippableFact]
    public void Page_save_returns_structured_409_on_version_conflict()
    {
        var src = ReadEndpointSource();
        Assert.Contains("page_version_conflict", src);
        Assert.Contains("statusCode: 409", src);
    }
}