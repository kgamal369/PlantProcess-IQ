// The genericity fingerprint ratchet.
//
// Backlog origin: T-206.
//
// A count is not a security boundary: remove two old violations, add two new ones,
// and a count-based gate stays green while the product acquires fresh customer
// identity. The baseline therefore records exact fingerprints.
//
//   known fingerprint present   -> reported, allowed (removal is the sweep's job)
//   known fingerprint gone      -> good, nothing required
//   unknown fingerprint present -> BLOCKING FAILURE
//   retired fingerprint returns -> BLOCKING FAILURE
//
// Generation mode (PPIQ_GENERICITY_WRITE_BASELINE=1) is how the pack creates the
// baseline and the inventory, so one scanner is the single source of truth for both
// the artifact and the gate. Two scanners would drift.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("BacklogTask", "T-206")]
public sealed class GenericityBaselineGateTests
{
    private const string BaselineRelative =
        "Backend/tests/PlantProcess.Architecture.Tests/genericity_violation_baseline.json";

    private const string InventoryRelative = "docs/quality/GenericityViolationInventory.md";

    private static string Abs(string relative) =>
        Path.Combine(ScopeAwareGenericity.RepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static JsonElement Baseline()
    {
        var path = Abs(BaselineRelative);

        Assert.True(
            File.Exists(path),
            "Genericity gate: the committed baseline is missing. Deleting it must fail the build, never silence the gate.");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }

    private static HashSet<string> Set(JsonElement root, string property)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!root.TryGetProperty(property, out var array)) return set;

        foreach (var item in array.EnumerateArray())
        {
            set.Add(item.ValueKind == JsonValueKind.String ? item.GetString()! : item.GetProperty("fingerprint").GetString()!);
        }

        return set;
    }

    [Fact]
    public void Generate_baseline_and_inventory_when_explicitly_asked()
    {
        if (Environment.GetEnvironmentVariable("PPIQ_GENERICITY_WRITE_BASELINE") != "1") return;

        var root = ScopeAwareGenericity.RepositoryRoot();
        var files = ScopeAwareGenericity.ProductGenericFiles(root);
        var findings = ScopeAwareGenericity.ScanRepository(root);

        Assert.True(files.Count > 400, "Genericity gate: refusing to write a baseline from a vacuous scan.");

        var grandfathered = findings
            .GroupBy(f => f.Fingerprint, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ToArray();

        var payload = new
        {
            backlogTask = "T-206",
            release = "M2",
            schema = 2,
            generatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            productGenericFileCount = files.Count,
            grandfatheredCount = grandfathered.Length,
            note = "Fingerprints, not a count. Entries may only disappear. A fingerprint absent from "
                 + "this list is a blocking failure.",
            grandfathered = grandfathered.Select(f => new
            {
                fingerprint = f.Fingerprint,
                path = f.RelativePath,
                rule = f.RuleId,
                construct = f.Construct,
                lineWhenCaptured = f.Line
            }).ToArray(),
            retired = Array.Empty<string>()
        };

        Directory.CreateDirectory(Path.GetDirectoryName(Abs(InventoryRelative))!);

        File.WriteAllText(
            Abs(BaselineRelative),
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        var md = new StringBuilder();
        md.AppendLine("# Genericity violation inventory");
        md.AppendLine();
        md.AppendLine("Backlog origin: T-206. Release: M2.");
        md.AppendLine();
        md.AppendLine("Product-generic files scanned: " + files.Count);
        md.AppendLine("Grandfathered fingerprints: " + grandfathered.Length);
        md.AppendLine();
        md.AppendLine("Handed to the vocabulary-sweep owner for term-list construction and removal.");
        md.AppendLine();

        foreach (var group in grandfathered.GroupBy(f => f.RelativePath, StringComparer.Ordinal)
                                           .OrderByDescending(g => g.Count()))
        {
            md.AppendLine("## " + group.Key + "  (" + group.Count() + ")");
            md.AppendLine();
            foreach (var f in group.OrderBy(x => x.Line))
            {
                md.AppendLine("- [" + f.RuleId + "] L" + f.Line + " `" + f.Construct + "`  fp=" + f.Fingerprint.Substring(0, 12));
            }
            md.AppendLine();
        }

        File.WriteAllText(Abs(InventoryRelative), md.ToString());
    }

    [Fact]
    public void The_baseline_exists_parses_and_is_not_vacuous()
    {
        var baseline = Baseline();

        Assert.True(baseline.GetProperty("productGenericFileCount").GetInt32() > 400,
            "Genericity gate: baseline records an implausibly small product scope.");

        Assert.Equal(baseline.GetProperty("grandfatheredCount").GetInt32(), Set(baseline, "grandfathered").Count);
    }

    [Fact]
    public void No_unapproved_genericity_fingerprint_exists()
    {
        var approved = Set(Baseline(), "grandfathered");
        var current = ScopeAwareGenericity.ScanRepository(ScopeAwareGenericity.RepositoryRoot());

        var unapproved = current
            .Where(f => !approved.Contains(f.Fingerprint))
            .GroupBy(f => f.Fingerprint, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToArray();

        if (unapproved.Length == 0) return;

        var detail = unapproved.Take(25).Select(f => f.RelativePath + ":" + f.Line + " [" + f.RuleId + "] " + f.Construct);

        Assert.Fail(
            "Genericity gate: " + unapproved.Length + " finding(s) are not in the accepted baseline. " +
            "Product code has acquired customer or industry identity:\n  " +
            string.Join("\n  ", detail) +
            "\n\nThe baseline shrinks by removing violations. It is never widened to accommodate new ones.");
    }

    [Fact]
    public void A_retired_fingerprint_never_returns()
    {
        var retired = Set(Baseline(), "retired");
        if (retired.Count == 0) return;

        var current = ScopeAwareGenericity
            .ScanRepository(ScopeAwareGenericity.RepositoryRoot())
            .Where(f => retired.Contains(f.Fingerprint))
            .Select(f => f.RelativePath + ":" + f.Line + " [" + f.RuleId + "] " + f.Construct)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(current.Length == 0,
            "Genericity gate: a previously removed violation has returned:\n  " + string.Join("\n  ", current));
    }

    [Fact]
    public void The_baseline_and_the_inventory_agree()
    {
        var baseline = Baseline();
        var inventoryPath = Abs(InventoryRelative);

        Assert.True(File.Exists(inventoryPath), "Genericity gate: the inventory is missing.");

        var inventory = File.ReadAllText(inventoryPath);
        var count = baseline.GetProperty("grandfatheredCount").GetInt32();

        Assert.Contains("Grandfathered fingerprints: " + count, inventory, StringComparison.Ordinal);

        foreach (var fp in Set(baseline, "grandfathered").Take(50))
        {
            Assert.Contains(fp.Substring(0, 12), inventory, StringComparison.Ordinal);
        }
    }
}