// Current-engine compatibility probe. DIAGNOSTIC, NEVER BLOCKING.
//
// Backlog origin: T-208. Findings route to the aggregation semantics implementation.
//
// EVIDENCE RULE: a symbol name does not prove a semantic. "Integrate" matches
// IntegratedAnything; "Interpolation" matches a charting enum; "WeightedMean" matches
// a weighted mean over anything at all, with no notion of duration. Name discovery
// therefore yields a CANDIDATE, never a supported capability.
//
// A capability may only be reported as supported when an executable call returns the
// corresponding value from ContinuousProcessKnownAnswers. Safe invocation of an
// arbitrarily discovered member is not available in this task - signatures and side
// effects are unknown, and calling blind to manufacture a green line is precisely the
// dishonesty this correction removes. Every candidate is therefore UNVERIFIED.
//
// The verdict vocabulary contains no "supported" value. A guard test below asserts it
// can never be emitted, so this file cannot drift back into name-based claims.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace PlantProcess.Analytics.Core.Tests.GenericValidation;

public enum EngineCapabilityVerdict
{
    // Nothing in the loaded product assemblies carries a plausible name.
    NotFound,

    // A plausibly named symbol exists. This is a lead to investigate, not a capability.
    UnverifiedCandidate
}

public sealed record EngineCapabilityCandidate(string Assembly, string Type, string Member);

public sealed record EngineCapabilityFinding(
    string Capability,
    string ExpectedKnownAnswer,
    EngineCapabilityVerdict Verdict,
    IReadOnlyList<EngineCapabilityCandidate> Candidates);

[Trait("BacklogTask", "T-208-Probe")]
public sealed class CurrentEngineCompatibilityTests
{
    private const string RouteTo = "ROUTE TO AGGREGATION SEMANTICS IMPLEMENTATION";

    private readonly ITestOutputHelper _out;

    public CurrentEngineCompatibilityTests(ITestOutputHelper output) => _out = output;

    private static IReadOnlyList<EngineCapabilityCandidate> FindCandidates(params string[] names)
    {
        var found = new List<EngineCapabilityCandidate>();

        var assemblies = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => a.FullName is not null && a.FullName.StartsWith("PlantProcess.", StringComparison.Ordinal));

        foreach (var assembly in assemblies)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).ToArray()!; }

            var assemblyName = assembly.GetName().Name ?? "unknown";

            foreach (var type in types)
            {
                if (names.Any(n => type.Name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    found.Add(new EngineCapabilityCandidate(assemblyName, type.FullName ?? type.Name, "(type name)"));
                }

                MemberInfo[] members;
                try
                {
                    members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static);
                }
                catch (TypeLoadException) { continue; }

                foreach (var member in members)
                {
                    if (names.Any(n => member.Name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        found.Add(new EngineCapabilityCandidate(assemblyName, type.FullName ?? type.Name, member.Name));
                    }
                }
            }
        }

        return found
            .GroupBy(c => c.Assembly + "|" + c.Type + "|" + c.Member, StringComparer.Ordinal)
            .Select(g => g.First())
            .Take(8)
            .ToArray();
    }

    private static EngineCapabilityFinding Probe(string capability, string expected, params string[] names)
    {
        var candidates = FindCandidates(names);

        return new EngineCapabilityFinding(
            capability,
            expected,
            candidates.Count == 0 ? EngineCapabilityVerdict.NotFound : EngineCapabilityVerdict.UnverifiedCandidate,
            candidates);
    }

    private static IReadOnlyList<EngineCapabilityFinding> RunProbes() => new[]
    {
        Probe("time-weighted mean", "715/6 for the fixture continuous signal",
            "TimeWeighted", "TimeWeightedMean"),

        Probe("declared interpolation rule", "1435/12 under linear, 715/6 under last-value-held",
            "Interpolation", "LastValueHeld", "StepHold"),

        Probe("rate integration over a window", "60 for the fixture rate signal, not the naive sum of 180",
            "Integrate", "TimeIntegral", "AreaUnder"),

        Probe("duration-weighted grain conversion", "365/3, not the unweighted 340/3",
            "WeightedMean", "GrainConversion", "DurationWeighted"),

        Probe("categorical aggregation refusal", "AggregationUndefinedForCategorical",
            "AggregationUndefined", "CategoricalAggregation")
    };

    [Fact]
    public void Probe_the_current_engine_for_continuous_process_aggregation_semantics()
    {
        var findings = RunProbes();

        _out.WriteLine("ENGINE COMPATIBILITY PROBE (diagnostic only; fixture acceptance does not depend on it)");
        _out.WriteLine("Name discovery cannot establish a semantic. No capability is reported as supported.");
        _out.WriteLine("");

        foreach (var f in findings)
        {
            _out.WriteLine(f.Verdict == EngineCapabilityVerdict.NotFound
                ? "  NOT_FOUND              " + f.Capability + " -> " + RouteTo
                : "  UNVERIFIED_CANDIDATE   " + f.Capability + " -> " + RouteTo);

            _out.WriteLine("      expected known answer: " + f.ExpectedKnownAnswer);

            foreach (var c in f.Candidates)
            {
                _out.WriteLine("      CANDIDATE_FOUND  assembly=" + c.Assembly + "  type=" + c.Type + "  member=" + c.Member);
            }

            _out.WriteLine("");
        }

        _out.WriteLine("VERIFIED CAPABILITIES: 0 - executable verification against the known answers is not "
                     + "performed here; it belongs to the aggregation semantics implementation.");
        _out.WriteLine("CAPABILITIES ROUTED ONWARD: " + findings.Count);

        // Deliberately unconditional. This probe records leads; it does not hold the
        // fixture hostage to the engine's current state.
        Assert.True(true);
    }

    [Fact]
    public void The_probe_can_never_report_a_capability_as_supported()
    {
        // The verdict vocabulary has exactly two values, neither of which asserts a
        // working capability. If someone later adds one, this test fails and forces the
        // question of what evidence backs it.
        var verdicts = Enum.GetNames(typeof(EngineCapabilityVerdict));

        Assert.Equal(2, verdicts.Length);
        Assert.DoesNotContain(verdicts, v => v.IndexOf("support", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.All(RunProbes(), f => Assert.True(
            f.Verdict == EngineCapabilityVerdict.NotFound || f.Verdict == EngineCapabilityVerdict.UnverifiedCandidate));
    }

    [Fact]
    public void Every_probe_names_the_known_answer_that_would_settle_it()
    {
        // A route-onward line is only actionable if it says what proof would look like.
        Assert.All(RunProbes(), f => Assert.False(string.IsNullOrWhiteSpace(f.ExpectedKnownAnswer)));
    }
}