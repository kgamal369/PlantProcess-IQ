// PPIQ-T05 static guard. Parses Program.cs and fails if ANY proof/certification endpoint
// registration sits outside the Production gate (except the honesty-certification whitelist).
// Runs inside `dotnet test`, so the build polices its own endpoint-exposure posture.
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Task", "T05")]
public sealed class T05ProofEndpointGatingTests
{
    private const string Whitelist = "MapP15HonestyCertificationEndpoints";

    private static readonly string[] MustBeGated =
    {
        "MapAdminProofEndpoints",
        "MapP03P04CompletionProofEndpoints",
        "MapPhase2LifecycleProofEndpoints",
        "MapV5LicenseResolverProofEndpoints",
        "MapV5IdentityRuntimeCertificationEndpoints",
        "MapV5ConnectorRuntimeCertificationEndpoints",
        "MapV5PrivateModelGatewayCertificationEndpoints",
    };

    private static string ProgramSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Backend", "PlantProcess.Api", "Program.cs");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Program.cs could not be located from the test base directory.");
    }

    // Returns [start,end] character span of the diagnostic gate block braces, or throws.
    private static (int start, int end) GateSpan(string src)
    {
        var flag = src.IndexOf("PPIQ_DIAGNOSTIC_ENDPOINTS", StringComparison.Ordinal);
        Assert.True(flag >= 0, "PPIQ-T05: the diagnostic gate (PPIQ_DIAGNOSTIC_ENDPOINTS) is missing from Program.cs.");
        var open = src.IndexOf('{', flag);
        Assert.True(open >= 0, "PPIQ-T05: gate opening brace not found.");
        var depth = 0;
        for (var i = open; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}') { depth--; if (depth == 0) return (open, i); }
        }
        throw new InvalidOperationException("PPIQ-T05: gate closing brace not found.");
    }

    [Fact]
    public void Every_gated_proof_endpoint_is_registered_inside_the_gate()
    {
        var src = ProgramSource();
        var (start, end) = GateSpan(src);
        foreach (var m in MustBeGated)
        {
            var idx = src.IndexOf("app." + m + "()", StringComparison.Ordinal);
            Assert.True(idx >= 0, $"PPIQ-T05: '{m}' registration not found in Program.cs.");
            Assert.True(idx > start && idx < end,
                $"PPIQ-T05: '{m}' must be registered INSIDE the PPIQ_DIAGNOSTIC_ENDPOINTS gate, not in Production.");
        }
    }

    [Fact]
    public void Honesty_certification_stays_outside_the_gate()
    {
        var src = ProgramSource();
        var (start, end) = GateSpan(src);
        var idx = src.IndexOf("app." + Whitelist + "()", StringComparison.Ordinal);
        Assert.True(idx >= 0, "PPIQ-T05: the honesty-certification registration must remain in Program.cs.");
        Assert.False(idx > start && idx < end,
            "PPIQ-T05: MapP15HonestyCertificationEndpoints is a Production trust surface and must NOT be gated.");
    }

    [Fact]
    public void No_proof_or_certification_registration_leaks_outside_the_gate()
    {
        var src = ProgramSource();
        var (start, end) = GateSpan(src);
        var rx = new Regex(@"app\.(Map[A-Za-z0-9]*(?:Proof|Certification)[A-Za-z0-9]*Endpoints)\s*\(\s*\)");
        foreach (Match match in rx.Matches(src))
        {
            var name = match.Groups[1].Value;
            if (name == Whitelist) continue;
            var i = match.Index;
            Assert.True(i > start && i < end,
                $"PPIQ-T05: '{name}' is a proof/certification surface registered OUTSIDE the gate. " +
                "Move it inside the PPIQ_DIAGNOSTIC_ENDPOINTS block or add it to the whitelist if it is customer-facing.");
        }
    }
}
