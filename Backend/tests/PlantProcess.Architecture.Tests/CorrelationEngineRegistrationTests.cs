using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-045-R1-B. ONE ENGINE, ONE KEY.
///
/// CorrelationEngineRegistry builds a dictionary keyed on ICorrelationEngine.Key.
/// A second registration of the same engine makes IEnumerable yield it twice and
/// the constructor throws, which takes the ENTIRE canonical correlation path
/// down through DI - no readiness verdict, no refusal, no message, just an
/// ArgumentException at resolution.
///
/// That is a one-line regression with a silent, total blast radius, so it is
/// guarded as source rather than left to be rediscovered by an integration run.
/// </summary>
public sealed class CorrelationEngineRegistrationTests
{
    private static string DependencyInjectionCode()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend")))
            dir = dir.Parent;

        Assert.True(dir is not null, "could not locate the repository root");

        var path = Path.Combine(dir!.FullName, "Backend", "PlantProcess.Application", "DependencyInjection.cs");
        Assert.True(File.Exists(path), "file is missing: " + path);

        var raw = File.ReadAllText(path);
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"(?m)^\s*//.*$", string.Empty);
    }

    [Fact]
    public void The_canonical_engine_is_registered_exactly_once()
    {
        var code = DependencyInjectionCode();

        // Both spellings count: fully qualified and via the using directive.
        var registrations = Regex.Matches(
            code,
            @"AddScoped<\s*(?:[\w.]*\.)?ICorrelationEngine\s*,\s*(?:[\w.]*\.)?CanonicalCorrelationEngine\s*>");

        Assert.Equal(1, registrations.Count);
    }

    [Fact]
    public void The_registry_is_registered_exactly_once()
    {
        var code = DependencyInjectionCode();

        var registrations = Regex.Matches(
            code,
            @"AddScoped<\s*(?:[\w.]*\.)?ICorrelationEngineRegistry\s*,\s*(?:[\w.]*\.)?CorrelationEngineRegistry\s*>");

        Assert.Equal(1, registrations.Count);
    }
}