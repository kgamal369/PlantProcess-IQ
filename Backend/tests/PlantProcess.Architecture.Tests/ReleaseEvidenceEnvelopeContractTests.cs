using System.Text.Json;
using Xunit;

namespace PlantProcess.Architecture.Tests;

// Release Evidence Envelope v1 drift guard.  (backlog reference: T-259)
//
// Three authorities describe one contract and they must not drift apart:
//
//   JSON Schema        portable structural authority
//   PowerShell         executable cross-field semantic authority
//   this test          repository drift guard
//
// So this file does two jobs. It re-proves the semantic invariants against the
// committed fixtures, and it asserts that the schema and the validator still
// carry the same frozen field set, version and verdict vocabulary. A contract
// that only one of the three enforces is a contract that has already drifted.
[Trait("Gate", "ReleaseEvidenceEnvelope")]
public sealed class ReleaseEvidenceEnvelopeContractTests
{
    private const string EvidenceRoot = "Backend/database/acceptance/evidence";
    private const string SchemaRelativePath = EvidenceRoot + "/release-evidence-envelope.v1.schema.json";
    private const string ValidatorRelativePath = "tools/validation/Test-ReleaseEvidenceEnvelope.ps1";
    private const string ContractVersion = "1.0";

    private static readonly string[] RequiredFields =
    {
        "contract_version", "task_id", "gate_id", "suite_id", "environment", "profile",
        "started_at", "completed_at", "total_count", "executed_count", "passed_count",
        "failed_count", "skipped_count", "unapproved_skip_count", "skip_reasons",
        "commit_sha", "evidence_paths", "verdict"
    };

    private static readonly string[] RequiredSkipReasonFields =
    {
        "reason_code", "count", "approved", "reason"
    };

    private static readonly string[] Verdicts = { "GREEN", "RED" };

    private static readonly string[] PositiveFixtures =
    {
        "database-certification.example.json",
        "frontend-certification.example.json",
        "all-skipped-certification.example.json"
    };

    // Every negative control, with the invariant it is there to prove.
    private static readonly (string File, string Proves)[] NegativeControls =
    {
        ("unsupported-contract-version.invalid.json",      "an unsupported contract_version is refused"),
        ("total-count-mismatch.invalid.json",              "total_count must equal executed_count + skipped_count"),
        ("executed-count-mismatch.invalid.json",           "executed_count must equal passed_count + failed_count"),
        ("green-with-failure.invalid.json",                "GREEN is impossible while failed_count is above zero"),
        ("green-with-unapproved-skip.invalid.json",        "GREEN is impossible while an unapproved skip remains"),
        ("all-skipped-with-executed-nonzero.invalid.json", "a skipped test is never counted as executed"),
        ("skip-reason-total-mismatch.invalid.json",        "skip reasons must account for every skipped test"),
        ("unapproved-skip-mismatch.invalid.json",          "unapproved reasons must account for unapproved_skip_count"),
        ("no-gate-or-suite-identifier.invalid.json",       "evidence must name a gate or a suite"),
        ("invalid-commit-sha.invalid.json",                "commit_sha must be a full 40-character Git SHA")
    };

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend", "database")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string Absolute(string relative) =>
        Path.Combine(RepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static JsonDocument Load(string relative)
    {
        var path = Absolute(relative);
        Assert.True(File.Exists(path), relative + " must exist");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    // Both sides of every sequence comparison are IEnumerable<string>, so the
    // assertion cannot silently bind to a nullable element type.
    private static IEnumerable<string> Sorted(IEnumerable<string> values) =>
        values.OrderBy(x => x, StringComparer.Ordinal).ToList();

    private static IEnumerable<string> Strings(JsonElement array) =>
        Sorted(array.EnumerateArray().Select(x => x.GetString() ?? string.Empty));

    private static string FixturePath(string name) => EvidenceRoot + "/examples/" + name;

    private static string NegativePath(string name) => EvidenceRoot + "/examples/negative/" + name;

    // ---- schema is the structural authority and says what it froze ----------

    [Fact]
    public void Schema_freezes_the_version_the_field_set_and_the_verdict_vocabulary()
    {
        using var schema = Load(SchemaRelativePath);
        var root = schema.RootElement;

        Assert.Equal(ContractVersion, root.GetProperty("properties").GetProperty("contract_version").GetProperty("const").GetString());

        Assert.Equal(Sorted(RequiredFields), Strings(root.GetProperty("required")));
        Assert.Equal(Sorted(RequiredFields), Sorted(root.GetProperty("properties").EnumerateObject().Select(p => p.Name)));
        Assert.Equal(Sorted(Verdicts), Strings(root.GetProperty("properties").GetProperty("verdict").GetProperty("enum")));
    }

    [Fact]
    public void Schema_refuses_undeclared_fields_at_both_levels()
    {
        using var schema = Load(SchemaRelativePath);
        var root = schema.RootElement;

        Assert.False(
            root.GetProperty("additionalProperties").GetBoolean(),
            "A typo such as fail_count must be refused, not absorbed as an unknown extension.");

        var skipItem = root.GetProperty("properties").GetProperty("skip_reasons").GetProperty("items");
        Assert.False(skipItem.GetProperty("additionalProperties").GetBoolean());

        Assert.Equal(Sorted(RequiredSkipReasonFields), Strings(skipItem.GetProperty("required")));
    }

    [Fact]
    public void Schema_requires_a_full_commit_sha_and_at_least_one_evidence_path()
    {
        using var schema = Load(SchemaRelativePath);
        var props = schema.RootElement.GetProperty("properties");

        Assert.Equal("^[0-9a-fA-F]{40}$", props.GetProperty("commit_sha").GetProperty("pattern").GetString());
        Assert.Equal(1, props.GetProperty("evidence_paths").GetProperty("minItems").GetInt32());
    }

    // ---- the committed fixtures satisfy the semantics -----------------------

    [Fact]
    public void Every_positive_fixture_satisfies_the_contract()
    {
        foreach (var fixture in PositiveFixtures)
        {
            using var doc = Load(FixturePath(fixture));
            var problems = Evaluate(doc.RootElement);
            Assert.True(problems.Count == 0, fixture + " must be valid but reported: " + string.Join("; ", problems));
        }
    }

    [Fact]
    public void The_all_skipped_fixture_reports_zero_executed()
    {
        using var doc = Load(FixturePath("all-skipped-certification.example.json"));
        var root = doc.RootElement;

        Assert.True(root.GetProperty("total_count").GetInt32() > 0);
        Assert.Equal(0, root.GetProperty("executed_count").GetInt32());
        Assert.Equal(root.GetProperty("total_count").GetInt32(), root.GetProperty("skipped_count").GetInt32());
        Assert.Equal(0, root.GetProperty("unapproved_skip_count").GetInt32());
    }

    [Fact]
    public void Each_negative_control_is_rejected_for_its_own_reason()
    {
        foreach (var (file, proves) in NegativeControls)
        {
            using var doc = Load(NegativePath(file));
            var problems = Evaluate(doc.RootElement);
            Assert.True(problems.Count > 0, file + " must be rejected: it exists to prove that " + proves);
        }
    }

    // ---- the validator still speaks the same contract -----------------------

    [Fact]
    public void The_powershell_validator_carries_the_same_frozen_vocabulary()
    {
        var validator = Absolute(ValidatorRelativePath);
        Assert.True(File.Exists(validator), ValidatorRelativePath + " is the executable semantic authority and must exist");

        var source = File.ReadAllText(validator);

        foreach (var field in RequiredFields)
        {
            Assert.Contains(field, source, StringComparison.Ordinal);
        }

        Assert.Contains("\"" + ContractVersion + "\"", source, StringComparison.Ordinal);

        foreach (var code in new[]
                 {
                     "UNSUPPORTED_CONTRACT_VERSION", "NO_PRODUCER_IDENTITY", "COMPLETED_BEFORE_STARTED",
                     "TOTAL_COUNT_MISMATCH", "EXECUTED_COUNT_MISMATCH", "UNAPPROVED_EXCEEDS_SKIPPED",
                     "SKIP_REASON_TOTAL_MISMATCH", "UNAPPROVED_SKIP_MISMATCH", "SKIP_REASONS_WITHOUT_SKIPS",
                     "GREEN_WITH_FAILURE", "GREEN_WITH_UNAPPROVED_SKIP", "NO_EVIDENCE_PATHS", "INVALID_COMMIT_SHA"
                 })
        {
            Assert.Contains(code, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void No_contract_file_carries_a_task_id_in_its_name()
    {
        var root = Path.Combine(RepositoryRoot(), EvidenceRoot.Replace('/', Path.DirectorySeparatorChar));
        var offenders = Directory
            .GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(n => n is not null && System.Text.RegularExpressions.Regex.IsMatch(n, @"[Tt]-?2\d\d"))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Task identity belongs in the payload and the commit subject, never in a filename: " + string.Join(", ", offenders));
    }

    // ---- the semantic rules, expressed once ---------------------------------

    private static List<string> Evaluate(JsonElement e)
    {
        var problems = new List<string>();

        foreach (var field in RequiredFields)
        {
            if (!e.TryGetProperty(field, out _)) problems.Add("MISSING_FIELD: " + field);
        }

        foreach (var property in e.EnumerateObject())
        {
            if (!RequiredFields.Contains(property.Name)) problems.Add("UNKNOWN_FIELD: " + property.Name);
        }

        if (problems.Count > 0) return problems;

        if (e.GetProperty("contract_version").GetString() != ContractVersion)
        {
            problems.Add("UNSUPPORTED_CONTRACT_VERSION");
        }

        var gate = e.GetProperty("gate_id");
        var suite = e.GetProperty("suite_id");
        var hasGate = gate.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(gate.GetString());
        var hasSuite = suite.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(suite.GetString());
        if (!hasGate && !hasSuite) problems.Add("NO_PRODUCER_IDENTITY");

        if (DateTimeOffset.TryParse(e.GetProperty("started_at").GetString(), out var started) &&
            DateTimeOffset.TryParse(e.GetProperty("completed_at").GetString(), out var completed) &&
            completed < started)
        {
            problems.Add("COMPLETED_BEFORE_STARTED");
        }

        var total = e.GetProperty("total_count").GetInt32();
        var executed = e.GetProperty("executed_count").GetInt32();
        var passed = e.GetProperty("passed_count").GetInt32();
        var failed = e.GetProperty("failed_count").GetInt32();
        var skipped = e.GetProperty("skipped_count").GetInt32();
        var unapproved = e.GetProperty("unapproved_skip_count").GetInt32();

        if (total != executed + skipped) problems.Add("TOTAL_COUNT_MISMATCH");
        if (executed != passed + failed) problems.Add("EXECUTED_COUNT_MISMATCH");
        if (unapproved > skipped) problems.Add("UNAPPROVED_EXCEEDS_SKIPPED");

        var reasonSum = 0;
        var unapprovedSum = 0;
        var reasonCount = 0;
        foreach (var reason in e.GetProperty("skip_reasons").EnumerateArray())
        {
            reasonCount++;
            foreach (var field in RequiredSkipReasonFields)
            {
                if (!reason.TryGetProperty(field, out _)) problems.Add("MALFORMED_SKIP_REASON: " + field);
            }

            if (!reason.TryGetProperty("count", out var countElement)) continue;
            var count = countElement.GetInt32();
            if (count < 1) problems.Add("MALFORMED_SKIP_REASON: count");
            reasonSum += count;
            if (reason.TryGetProperty("approved", out var approved) && !approved.GetBoolean()) unapprovedSum += count;
        }

        if (reasonSum != skipped) problems.Add("SKIP_REASON_TOTAL_MISMATCH");
        if (unapprovedSum != unapproved) problems.Add("UNAPPROVED_SKIP_MISMATCH");
        if (skipped == 0 && reasonCount != 0) problems.Add("SKIP_REASONS_WITHOUT_SKIPS");

        if (!System.Text.RegularExpressions.Regex.IsMatch(e.GetProperty("commit_sha").GetString() ?? "", "^[0-9a-fA-F]{40}$"))
        {
            problems.Add("INVALID_COMMIT_SHA");
        }

        var paths = e.GetProperty("evidence_paths").EnumerateArray().ToList();
        if (paths.Count < 1) problems.Add("NO_EVIDENCE_PATHS");
        foreach (var path in paths)
        {
            var value = path.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(value)) problems.Add("EMPTY_EVIDENCE_PATH");
            else if (System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Za-z]:[\\/]") || value.StartsWith("/", StringComparison.Ordinal))
            {
                problems.Add("ABSOLUTE_EVIDENCE_PATH");
            }
        }

        var verdict = e.GetProperty("verdict").GetString() ?? "";
        if (!Verdicts.Contains(verdict)) problems.Add("INVALID_VERDICT");
        else if (verdict == "GREEN")
        {
            if (failed > 0) problems.Add("GREEN_WITH_FAILURE");
            if (unapproved > 0) problems.Add("GREEN_WITH_UNAPPROVED_SKIP");
        }

        return problems;
    }
}
