// Scope-aware genericity authority.
//
// Backlog origin: T-206.
//
// This is an architecture gate, not a word filter. A product file is allowed to
// mention several material types in a description precisely in order to privilege
// none of them; that is genericity being asserted, not violated. What is forbidden
// is a CONSTRUCT that bakes one customer's world into the product:
//
//   UA-01 industry identity used as product data
//   UA-02 business logic keyed to one material identity
//   UA-03 silent default grain
//   UA-04 fixed default business parameter or unit system
//   UA-05 typed union that constrains the customer model
//   UA-06 customer schema column treated as universally available
//   UA-07 behaviour inferred from customer vocabulary
//
// Rule validity is proven against synthetic samples, never against real debt in the
// tree. When the vocabulary sweep drives the tree to zero, every rule here still
// works and still blocks regression.
//
// This file classifies itself and its sibling gate files as Excluded: a scanner that
// reads its own definition table reports its own rules as violations.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PlantProcess.Architecture.Tests;

public enum GenericityScope
{
    ProductGeneric,
    CustomerAsset,
    TestOrFixture,
    Documentation,
    Excluded
}

public sealed class GenericityRule
{
    public GenericityRule(string id, string description, string pattern)
    {
        Id = id;
        Description = description;
        Pattern = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    public string Id { get; }
    public string Description { get; }
    public Regex Pattern { get; }
}

// Identity of a violation. Deliberately excludes the physical line number: moving a
// line is not a semantic change, and identity that shifts on reformatting would make
// every grandfathered entry evaporate on the next tidy-up. Line number is human
// evidence and travels beside the fingerprint, never inside it.
public static class GenericityViolationFingerprint
{
    public static string NormaliseConstruct(string matched)
    {
        var s = Regex.Replace(matched.Trim().ToLowerInvariant(), @"\s+", " ");
        if (s.Length > 120) s = s.Substring(0, 120);
        return s;
    }

    public static string Compute(string relativePath, string ruleId, string normalisedConstruct)
    {
        var payload = ScopeAwareGenericity.NormalisePath(relativePath).ToLowerInvariant()
                    + "|" + ruleId + "|" + normalisedConstruct;

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));

        var sb = new StringBuilder(64);
        foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}

public sealed class GenericityViolation
{
    public GenericityViolation(string relativePath, int line, GenericityRule rule, string construct)
    {
        RelativePath = relativePath;
        Line = line;
        RuleId = rule.Id;
        Construct = GenericityViolationFingerprint.NormaliseConstruct(construct);
        Fingerprint = GenericityViolationFingerprint.Compute(relativePath, rule.Id, Construct);
    }

    public string RelativePath { get; }
    public int Line { get; }            // informational only
    public string RuleId { get; }
    public string Construct { get; }
    public string Fingerprint { get; }
}

public static class ScopeAwareGenericity
{
    private static readonly string[] SelfExcluded =
    {
        "scopeawaregenericity.cs",
        "scopeawaregenericityruletests.cs",
        "genericitybaselinegatetests.cs",
        "genericity_violation_baseline.json",
        "genericityviolationinventory.md"
    };

    private static readonly string[] ProductExtensions = { ".cs", ".ts", ".tsx", ".py" };

    public static IReadOnlyList<GenericityRule> Rules { get; } = new List<GenericityRule>
    {
        new GenericityRule(
            "UA-01",
            "industry equipment identity used as product data",
            "\"\\s*(caster|mill|furnace|kiln|roller)\\s*[-_ ]?\\d+\\s*\""),

        new GenericityRule(
            "UA-02",
            "business logic keyed to one material identity",
            "\\b(coil|heat|slab|billet|ingot)(id|_id|key|number)\\b|\"(coil|heat|slab):\""),

        // The captured construct must include the literal. A pattern that stops at the
        // opening quote cannot tell a hardcoded "coil" default from a hardcoded "heat"
        // one, so swapping them would silently inherit the old fingerprint.
        new GenericityRule(
            "UA-03",
            "silent default grain: a grain resolved to a hardcoded literal instead of failing closed",
            "[a-z0-9_.]*grain\\s*\\?\\?\\s*\"[a-z0-9_-]*\"" +
            "|isnullorwhitespace\\s*\\(\\s*[a-z0-9_.]*grain\\s*\\)\\s*\\?\\s*\"[a-z0-9_-]*\"" +
            "|\\?\\s*\"(coil|heat|slab|batch|lot|piece)\"\\s*:"),

        new GenericityRule(
            "UA-04",
            "fixed default business parameter or unit system",
            "\"[a-z0-9_]*_per_(ton|tonne|kg|lb|pound|bbl)\\b[a-z0-9_]*\"|\\b(grade|coil|heat)_premium\\b"),

        new GenericityRule(
            "UA-05",
            "typed union or enum that constrains the customer model to one industry",
            "\"(coil|heat|slab|cast|billet)\"\\s*\\|\\s*\"(coil|heat|slab|cast|billet|process|batch|lot)\""),

        new GenericityRule(
            "UA-06",
            "customer schema column treated as universally available",
            "\\b(heat_id|coil_id|slab_id|piece_id|material_id|src_heats|src_coils)\\b"),

        new GenericityRule(
            "UA-07",
            "behaviour inferred from customer vocabulary rather than declared metadata",
            "(indexof|contains|startswith|endswith)\\s*\\(\\s*\"(grade|coil|heat|slab|cast)\"")
    };

    public static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend"))) dir = dir.Parent;
        if (dir is null) throw new InvalidOperationException("Genericity gate: could not locate the repository root.");
        return dir.FullName;
    }

    public static string NormalisePath(string relativePath) => relativePath.Replace('\\', '/').TrimStart('/');

    public static GenericityScope Classify(string relativePath)
    {
        var p = NormalisePath(relativePath).ToLowerInvariant();
        var name = p.Contains('/') ? p.Substring(p.LastIndexOf('/') + 1) : p;

        if (SelfExcluded.Contains(name)) return GenericityScope.Excluded;
        if (p.Contains("node_modules/") || p.Contains("/bin/") || p.Contains("/obj/") ||
            p.Contains("/dist/") || p.Contains("playwright-report") || p.StartsWith(".git/"))
        {
            return GenericityScope.Excluded;
        }

        if (p.EndsWith(".md") || p.StartsWith("docs/")) return GenericityScope.Documentation;

        if (p.Contains("/tests/") || p.StartsWith("tests/") || p.Contains("__tests__") ||
            p.Contains(".test.") || p.Contains(".spec.") || p.Contains("/e2e/") ||
            p.Contains("/fixtures/") || p.Contains("/_fixtures/"))
        {
            return GenericityScope.TestOrFixture;
        }

        // Emulated-plant dataset generation and repository tooling: one plant's data,
        // legitimately carrying one plant's vocabulary. Product code must not depend on it.
        if (p.StartsWith("tools/") || p.StartsWith("backend/tools/") || p.StartsWith("scripts/"))
        {
            return GenericityScope.CustomerAsset;
        }

        if (p.StartsWith("website/")) return GenericityScope.Excluded;
        if (!ProductExtensions.Contains(Path.GetExtension(p))) return GenericityScope.Excluded;

        return GenericityScope.ProductGeneric;
    }

    // Scans run on comment-stripped text. Prose explaining a forbidden construct must
    // never trip a guard about that construct.
    public static string StripComments(string raw)
    {
        var s = Regex.Replace(raw, @"/\*[\s\S]*?\*/", string.Empty);
        s = Regex.Replace(s, @"(?m)^\s*//.*$", string.Empty);
        s = Regex.Replace(s, @"(?m)^\s*--.*$", string.Empty);
        s = Regex.Replace(s, @"(?m)^\s*#.*$", string.Empty);
        return s;
    }

    public static IReadOnlyList<string> ProductGenericFiles(string root)
    {
        var results = new List<string>();

        foreach (var area in new[] { "Backend", "Frontend" })
        {
            var areaPath = Path.Combine(root, area);
            if (!Directory.Exists(areaPath)) continue;

            foreach (var file in Directory.EnumerateFiles(areaPath, "*.*", SearchOption.AllDirectories))
            {
                var rel = NormalisePath(file.Substring(root.Length + 1));
                if (Classify(rel) == GenericityScope.ProductGeneric) results.Add(rel);
            }
        }

        results.Sort(StringComparer.Ordinal);
        return results;
    }

    // Scans arbitrary in-memory text. Every rule is validated through this entry point
    // with synthetic samples, so rule validity never depends on the tree being dirty.
    public static IReadOnlyList<GenericityViolation> ScanText(string relativePath, string rawText)
    {
        var findings = new List<GenericityViolation>();
        if (Classify(relativePath) != GenericityScope.ProductGeneric) return findings;

        var lines = StripComments(rawText).Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            foreach (var rule in Rules)
            {
                foreach (Match m in rule.Pattern.Matches(lines[i]))
                {
                    findings.Add(new GenericityViolation(relativePath, i + 1, rule, m.Value));
                }
            }
        }

        return findings;
    }

    public static IReadOnlyList<GenericityViolation> ScanRepository(string root)
    {
        var all = new List<GenericityViolation>();

        foreach (var rel in ProductGenericFiles(root))
        {
            string raw;
            try { raw = File.ReadAllText(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar))); }
            catch (IOException) { continue; }
            all.AddRange(ScanText(rel, raw));
        }

        return all;
    }
}