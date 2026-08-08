using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-073. Every provenance kind must have a real resolver branch.
///
/// The resolver's switch ends in a default that returns Missing, so a kind added
/// without a branch does not break the build - it silently becomes unresolvable,
/// and a citation of that kind would refuse forever with no obvious cause. This
/// reads both source files textually, so it needs no project reference and
/// cannot be satisfied by a comment.
/// </summary>
[Trait("Gate", "ProvenanceResolverExhaustiveness")]
public sealed class ProvenanceResolverExhaustivenessTests
{
    private static string Read(string relativePath)
        => File.ReadAllText(Path.Combine(PipelineSourceText.RepoRoot(), relativePath));

    private static string ResolverSource()
        => PipelineSourceText.StripComments(
            Read(Path.Combine("Backend", "PlantProcess.Infrastructure", "Provenance", "NpgsqlProvenanceResolver.cs")));

    private static string[] DeclaredKinds()
    {
        var source = PipelineSourceText.StripComments(
            Read(Path.Combine("Backend", "PlantProcess.Application", "Provenance", "ProvenanceHandle.cs")));

        var match = Regex.Match(source, @"enum\s+ProvenanceKind\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline);
        Assert.True(match.Success, "Could not locate the ProvenanceKind declaration.");

        return match.Groups["body"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => entry.Length > 0)
            .ToArray();
    }

    [Fact]
    public void Every_declared_kind_has_a_resolver_branch()
    {
        var resolver = ResolverSource();

        foreach (var kind in DeclaredKinds())
        {
            Assert.True(
                resolver.Contains("ProvenanceKind." + kind, StringComparison.Ordinal),
                $"ProvenanceKind.{kind} has no branch in NpgsqlProvenanceResolver.");
        }
    }

    [Fact]
    public void The_widget_result_branch_reads_the_evidence_snapshot_table()
    {
        var resolver = ResolverSource();

        Assert.Contains("canon.assistant_widget_result", resolver, StringComparison.Ordinal);
        /* Fails closed, like every other branch. */
        Assert.Contains("Widget result evidence store is not installed.", resolver, StringComparison.Ordinal);
    }
}