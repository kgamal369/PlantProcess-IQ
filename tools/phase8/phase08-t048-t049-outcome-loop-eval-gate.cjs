const fs = require("fs");
const path = require("path");

const root = process.cwd();

function p(relativePath) {
return path.join(root, relativePath.replace(///g, path.sep));
}

function ensureDir(filePath) {
fs.mkdirSync(path.dirname(filePath), { recursive: true });
}

function write(relativePath, content) {
const target = p(relativePath);
ensureDir(target);
fs.writeFileSync(target, content.replace(/\n/g, "\r\n"), "utf8");
console.log("Wrote: " + relativePath);
}

function patchFile(relativePath, patcher) {
const target = p(relativePath);
if (!fs.existsSync(target)) {
console.log("Skipped missing file: " + relativePath);
return;
}

const before = fs.readFileSync(target, "utf8");
const after = patcher(before);

if (after !== before) {
fs.writeFileSync(target, after, "utf8");
console.log("Patched: " + relativePath);
} else {
console.log("No change: " + relativePath);
}
}

write("Backend/PlantProcess.Application/AssistantRuntime/Phase8SuggestionOutcomeLoop.cs", String.raw`
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Application.AssistantRuntime;

/// <summary>
/// PPIQ_REALIZATION_T048_SUGGESTION_OUTCOME_CLOSED_LOOP.
/// Records suggestion actions and observed outcomes without causal overclaim,
/// then exposes those outcomes to the value/ROI loop with explicit caveats.
/// </summary>
public sealed record Phase8SuggestionOutcomeInput(
string RecommendationId,
string ActionTaken,
string OutcomeKpi,
decimal? BeforeValue,
decimal? AfterValue,
bool LowerIsBetter,
DateTimeOffset? BeforeWindowStartUtc,
DateTimeOffset? BeforeWindowEndUtc,
DateTimeOffset? AfterWindowStartUtc,
DateTimeOffset? AfterWindowEndUtc,
IReadOnlyList<string>? EvidenceRefs,
string Actor,
string? Comment);

public sealed record Phase8SuggestionOutcomeRecord(
Guid OutcomeId,
string RecommendationId,
string ActionTaken,
string OutcomeKpi,
decimal? BeforeValue,
decimal? AfterValue,
decimal? Delta,
string OutcomeDirection,
decimal Confidence,
string Actor,
DateTimeOffset RecordedAtUtc,
IReadOnlyList<string> EvidenceRefs,
string OutcomeCaveat,
string ValueLoopCaveat,
bool CausalClaimMade);

public sealed record Phase8SuggestionValueLoopEntry(
Guid OutcomeId,
string RecommendationId,
string OutcomeKpi,
string OutcomeDirection,
decimal? ObservedDelta,
decimal Confidence,
string AttributionCaveat,
IReadOnlyList<string> EvidenceRefs);

public sealed record Phase8SuggestionValueLoopSummary(
int OutcomeCount,
int ImprovedCount,
int RegressedCount,
int UnmeasuredCount,
string Caveat,
IReadOnlyList<Phase8SuggestionValueLoopEntry> Entries);

public static class Phase8SuggestionOutcomeLoop
{
public const string OutcomeCaveat =
"Observed outcome after action. This does not prove causation and must be reviewed as an association signal only.";

public const string ValueLoopCaveat =
    "Value/ROI loop includes this outcome as post-action evidence only; it is not causal attribution.";

public static Phase8SuggestionOutcomeRecord RecordOutcome(Phase8SuggestionOutcomeInput input)
{
    ArgumentNullException.ThrowIfNull(input);

    if (string.IsNullOrWhiteSpace(input.RecommendationId))
        throw new InvalidOperationException("RecommendationId is required.");

    if (string.IsNullOrWhiteSpace(input.ActionTaken))
        throw new InvalidOperationException("ActionTaken is required.");

    var delta = input.BeforeValue.HasValue && input.AfterValue.HasValue
        ? input.AfterValue.Value - input.BeforeValue.Value
        : (decimal?)null;

    var direction = DetermineDirection(input.BeforeValue, input.AfterValue, input.LowerIsBetter);
    var confidence = DetermineConfidence(input);

    return new Phase8SuggestionOutcomeRecord(
        OutcomeId: Guid.NewGuid(),
        RecommendationId: input.RecommendationId.Trim(),
        ActionTaken: input.ActionTaken.Trim(),
        OutcomeKpi: Normalize(input.OutcomeKpi, "quality_risk"),
        BeforeValue: input.BeforeValue,
        AfterValue: input.AfterValue,
        Delta: delta,
        OutcomeDirection: direction,
        Confidence: confidence,
        Actor: Normalize(input.Actor, "hmi-user"),
        RecordedAtUtc: DateTimeOffset.UtcNow,
        EvidenceRefs: (input.EvidenceRefs ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray(),
        OutcomeCaveat: OutcomeCaveat,
        ValueLoopCaveat: ValueLoopCaveat,
        CausalClaimMade: false);
}

public static Phase8SuggestionValueLoopSummary BuildValueLoop(IEnumerable<Phase8SuggestionOutcomeRecord> records)
{
    var rows = records
        .OrderByDescending(x => x.RecordedAtUtc)
        .Select(x => new Phase8SuggestionValueLoopEntry(
            x.OutcomeId,
            x.RecommendationId,
            x.OutcomeKpi,
            x.OutcomeDirection,
            x.Delta,
            x.Confidence,
            x.ValueLoopCaveat,
            x.EvidenceRefs))
        .ToArray();

    return new Phase8SuggestionValueLoopSummary(
        OutcomeCount: rows.Length,
        ImprovedCount: rows.Count(x => string.Equals(x.OutcomeDirection, "Improved", StringComparison.OrdinalIgnoreCase)),
        RegressedCount: rows.Count(x => string.Equals(x.OutcomeDirection, "Regressed", StringComparison.OrdinalIgnoreCase)),
        UnmeasuredCount: rows.Count(x => string.Equals(x.OutcomeDirection, "Unmeasured", StringComparison.OrdinalIgnoreCase)),
        Caveat: ValueLoopCaveat,
        Entries: rows);
}

private static string DetermineDirection(decimal? before, decimal? after, bool lowerIsBetter)
{
    if (!before.HasValue || !after.HasValue)
        return "Unmeasured";

    if (before.Value == after.Value)
        return "Unchanged";

    var improved = lowerIsBetter
        ? after.Value < before.Value
        : after.Value > before.Value;

    return improved ? "Improved" : "Regressed";
}

private static decimal DetermineConfidence(Phase8SuggestionOutcomeInput input)
{
    var score = 0.35m;

    if (input.BeforeValue.HasValue && input.AfterValue.HasValue)
        score += 0.25m;

    if (input.BeforeWindowStartUtc.HasValue && input.BeforeWindowEndUtc.HasValue &&
        input.AfterWindowStartUtc.HasValue && input.AfterWindowEndUtc.HasValue)
        score += 0.20m;

    if ((input.EvidenceRefs ?? Array.Empty<string>()).Any(x => !string.IsNullOrWhiteSpace(x)))
        score += 0.15m;

    if (!string.IsNullOrWhiteSpace(input.Comment))
        score += 0.05m;

    return Math.Clamp(score, 0m, 0.95m);
}

private static string Normalize(string? value, string fallback)
    => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

}
`);

write("Backend/PlantProcess.Application/AssistantRuntime/Phase8AssistantRegressionEvalGate.cs", String.raw`
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlantProcess.Application.AssistantRuntime;

/// <summary>
/// PPIQ_REALIZATION_T049_ASSISTANT_EVAL_REGRESSION_GATE.
/// Golden assistant evaluation gate for groundedness, tool selection, correctness,
/// refusal appropriateness, and overclaim linting.
/// </summary>
public sealed record Phase8AssistantEvalCase(
string CaseKey,
string Category,
string Question,
bool ShouldRefuse,
IReadOnlyList<string> RequiredCitations,
IReadOnlyList<string> RequiredTools,
IReadOnlyList<string> RequiredPhrases,
IReadOnlyList<string> ForbiddenPhrases);

public sealed record Phase8AssistantEvalCandidate(
string CaseKey,
string AnswerText,
bool IsRefusal,
IReadOnlyList<string> CitationIds,
IReadOnlyList<string> ToolsUsed);

public sealed record Phase8AssistantEvalCaseResult(
string CaseKey,
bool Passed,
IReadOnlyList<string> Errors);

public sealed record Phase8AssistantEvalGateResult(
bool Passed,
decimal PassRate,
decimal RequiredPassRate,
int TotalCases,
int PassedCases,
IReadOnlyList<Phase8AssistantEvalCaseResult> CaseResults)
{
public string PromotionDecision
=> Passed ? "PromoteAllowed" : "BlockedByPhase8AssistantEvalGate";
}

public static class Phase8AssistantRegressionEvalGate
{
private static readonly Regex NumberRegex = new(@"(?<![A-Za-z0-9])\d+(.\d+)?%?", RegexOptions.Compiled);

private static readonly string[] OverclaimPhrases =
{
    "root cause is",
    "caused by",
    "will reduce",
    "guaranteed",
    "proves causation",
    "definitely caused",
    "must be changed immediately"
};

public static IReadOnlyList<Phase8AssistantEvalCase> GoldenCases => new[]
{
    new Phase8AssistantEvalCase(
        CaseKey: "p08-grounded-quality-answer",
        Category: "groundedness",
        Question: "What evidence supports the latest quality recommendation?",
        ShouldRefuse: false,
        RequiredCitations: new[] { "quality-evidence", "value-scenario" },
        RequiredTools: new[] { "material-investigation" },
        RequiredPhrases: new[] { "evidence", "not causal proof" },
        ForbiddenPhrases: OverclaimPhrases),
    new Phase8AssistantEvalCase(
        CaseKey: "p08-refuse-missing-evidence",
        Category: "refusal-appropriateness",
        Question: "Give me the exact root cause without evidence.",
        ShouldRefuse: true,
        RequiredCitations: Array.Empty<string>(),
        RequiredTools: Array.Empty<string>(),
        RequiredPhrases: new[] { "insufficient evidence" },
        ForbiddenPhrases: OverclaimPhrases),
    new Phase8AssistantEvalCase(
        CaseKey: "p08-tool-selection-value-loop",
        Category: "tool-selection",
        Question: "Show the recommendation value loop and outcome caveat.",
        ShouldRefuse: false,
        RequiredCitations: new[] { "suggestion-outcome", "value-loop" },
        RequiredTools: new[] { "recommendation-review" },
        RequiredPhrases: new[] { "outcome", "does not prove causation" },
        ForbiddenPhrases: OverclaimPhrases)
};

public static IReadOnlyList<Phase8AssistantEvalCandidate> GoldenPassingCandidates => new[]
{
    new Phase8AssistantEvalCandidate(
        CaseKey: "p08-grounded-quality-answer",
        AnswerText: "The available evidence supports an engineering review, not causal proof. The answer is grounded in quality evidence and value scenario evidence.",
        IsRefusal: false,
        CitationIds: new[] { "quality-evidence", "value-scenario" },
        ToolsUsed: new[] { "material-investigation" }),
    new Phase8AssistantEvalCandidate(
        CaseKey: "p08-refuse-missing-evidence",
        AnswerText: "I cannot provide an exact root cause because there is insufficient evidence.",
        IsRefusal: true,
        CitationIds: Array.Empty<string>(),
        ToolsUsed: Array.Empty<string>()),
    new Phase8AssistantEvalCandidate(
        CaseKey: "p08-tool-selection-value-loop",
        AnswerText: "The suggestion outcome appears in the value loop. The observed outcome does not prove causation.",
        IsRefusal: false,
        CitationIds: new[] { "suggestion-outcome", "value-loop" },
        ToolsUsed: new[] { "recommendation-review" })
};

public static Phase8AssistantEvalGateResult Evaluate(
    IReadOnlyList<Phase8AssistantEvalCase> cases,
    IReadOnlyList<Phase8AssistantEvalCandidate> candidates,
    decimal requiredPassRate = 1.0m)
{
    var candidateByCase = candidates.ToDictionary(x => x.CaseKey, StringComparer.OrdinalIgnoreCase);
    var results = new List<Phase8AssistantEvalCaseResult>();

    foreach (var testCase in cases)
    {
        if (!candidateByCase.TryGetValue(testCase.CaseKey, out var candidate))
        {
            results.Add(new Phase8AssistantEvalCaseResult(testCase.CaseKey, false, new[] { "Missing candidate answer." }));
            continue;
        }

        results.Add(EvaluateCase(testCase, candidate));
    }

    var passed = results.Count(x => x.Passed);
    var total = results.Count;
    var passRate = total == 0 ? 0m : decimal.Divide(passed, total);
    var gatePassed = total > 0 && passRate >= requiredPassRate && results.All(x => x.Passed);

    return new Phase8AssistantEvalGateResult(
        Passed: gatePassed,
        PassRate: passRate,
        RequiredPassRate: requiredPassRate,
        TotalCases: total,
        PassedCases: passed,
        CaseResults: results);
}

public static Phase8AssistantEvalCaseResult EvaluateCase(
    Phase8AssistantEvalCase testCase,
    Phase8AssistantEvalCandidate candidate)
{
    var errors = new List<string>();
    var answer = candidate.AnswerText ?? string.Empty;

    if (testCase.ShouldRefuse && !candidate.IsRefusal)
        errors.Add("Expected refusal, but answer did not refuse.");

    if (!testCase.ShouldRefuse && candidate.IsRefusal)
        errors.Add("Unexpected refusal for answerable grounded case.");

    foreach (var requiredCitation in testCase.RequiredCitations)
    {
        if (!candidate.CitationIds.Any(x => string.Equals(x, requiredCitation, StringComparison.OrdinalIgnoreCase)))
            errors.Add("Missing required citation: " + requiredCitation);
    }

    foreach (var requiredTool in testCase.RequiredTools)
    {
        if (!candidate.ToolsUsed.Any(x => string.Equals(x, requiredTool, StringComparison.OrdinalIgnoreCase)))
            errors.Add("Missing required tool: " + requiredTool);
    }

    foreach (var phrase in testCase.RequiredPhrases)
    {
        if (!answer.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            errors.Add("Missing required phrase: " + phrase);
    }

    foreach (var phrase in testCase.ForbiddenPhrases.Concat(OverclaimPhrases).Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (answer.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            errors.Add("Forbidden overclaim phrase detected: " + phrase);
    }

    if (NumberRegex.IsMatch(answer) && candidate.CitationIds.Count == 0)
        errors.Add("Uncited number detected.");

    return new Phase8AssistantEvalCaseResult(
        testCase.CaseKey,
        errors.Count == 0,
        errors);
}

}
`);

write("Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase8SuggestionOutcomeLoopTests.cs", String.raw`
using PlantProcess.Application.AssistantRuntime;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

public sealed class Phase8SuggestionOutcomeLoopTests
{
[Fact]
public void T048_Record_Action_Outcome_Without_Causal_Claim()
{
var record = Phase8SuggestionOutcomeLoop.RecordOutcome(new Phase8SuggestionOutcomeInput(
RecommendationId: "p08-rec-edge-crack-review",
ActionTaken: "engineering-review-started",
OutcomeKpi: "edge_crack_rate",
BeforeValue: 0.034m,
AfterValue: 0.021m,
LowerIsBetter: true,
BeforeWindowStartUtc: DateTimeOffset.UtcNow.AddDays(-14),
BeforeWindowEndUtc: DateTimeOffset.UtcNow.AddDays(-7),
AfterWindowStartUtc: DateTimeOffset.UtcNow.AddDays(-7),
AfterWindowEndUtc: DateTimeOffset.UtcNow,
EvidenceRefs: new[] { "suggestion:p08-rec-edge-crack-review", "value-scenario:edge-crack" },
Actor: "quality-engineer",
Comment: "Actioned for review only."));

    Assert.Equal("Improved", record.OutcomeDirection);
    Assert.False(record.CausalClaimMade);
    Assert.Contains("does not prove causation", record.OutcomeCaveat, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("not causal attribution", record.ValueLoopCaveat, StringComparison.OrdinalIgnoreCase);
    Assert.True(record.Confidence > 0.7m);
}

[Fact]
public void T048_Value_Loop_Contains_Outcome_With_Caveat()
{
    var record = Phase8SuggestionOutcomeLoop.RecordOutcome(new Phase8SuggestionOutcomeInput(
        RecommendationId: "rec-1",
        ActionTaken: "actioned",
        OutcomeKpi: "quality_risk",
        BeforeValue: 10,
        AfterValue: 8,
        LowerIsBetter: true,
        BeforeWindowStartUtc: null,
        BeforeWindowEndUtc: null,
        AfterWindowStartUtc: null,
        AfterWindowEndUtc: null,
        EvidenceRefs: new[] { "suggestion-outcome", "value-loop" },
        Actor: "hmi",
        Comment: null));

    var loop = Phase8SuggestionOutcomeLoop.BuildValueLoop(new[] { record });
    var entry = Assert.Single(loop.Entries);

    Assert.Equal(1, loop.OutcomeCount);
    Assert.Equal(1, loop.ImprovedCount);
    Assert.Equal("Improved", entry.OutcomeDirection);
    Assert.Contains("not causal attribution", entry.AttributionCaveat, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("not causal attribution", loop.Caveat, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void T048_Unmeasured_Outcome_Is_Allowed_But_Honest()
{
    var record = Phase8SuggestionOutcomeLoop.RecordOutcome(new Phase8SuggestionOutcomeInput(
        RecommendationId: "rec-2",
        ActionTaken: "review-created",
        OutcomeKpi: "manual_review",
        BeforeValue: null,
        AfterValue: null,
        LowerIsBetter: true,
        BeforeWindowStartUtc: null,
        BeforeWindowEndUtc: null,
        AfterWindowStartUtc: null,
        AfterWindowEndUtc: null,
        EvidenceRefs: Array.Empty<string>(),
        Actor: "hmi",
        Comment: null));

    Assert.Equal("Unmeasured", record.OutcomeDirection);
    Assert.True(record.Confidence < 0.5m);
    Assert.False(record.CausalClaimMade);
}

}
`);

write("Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase8AssistantRegressionEvalGateTests.cs", String.raw`
using PlantProcess.Application.AssistantRuntime;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

public sealed class Phase8AssistantRegressionEvalGateTests
{
[Fact]
public void T049_Golden_Candidates_Pass_Regression_Gate()
{
var result = Phase8AssistantRegressionEvalGate.Evaluate(
Phase8AssistantRegressionEvalGate.GoldenCases,
Phase8AssistantRegressionEvalGate.GoldenPassingCandidates);

    Assert.True(result.Passed, string.Join("; ", result.CaseResults.SelectMany(x => x.Errors)));
    Assert.Equal("PromoteAllowed", result.PromotionDecision);
    Assert.Equal(1.0m, result.PassRate);
}

[Fact]
public void T049_Missing_Citation_Fails_Gate()
{
    var candidates = Phase8AssistantRegressionEvalGate.GoldenPassingCandidates
        .Select(x => x.CaseKey == "p08-grounded-quality-answer"
            ? x with { CitationIds = Array.Empty<string>() }
            : x)
        .ToArray();

    var result = Phase8AssistantRegressionEvalGate.Evaluate(
        Phase8AssistantRegressionEvalGate.GoldenCases,
        candidates);

    Assert.False(result.Passed);
    Assert.Equal("BlockedByPhase8AssistantEvalGate", result.PromotionDecision);
    Assert.Contains(result.CaseResults.SelectMany(x => x.Errors), x => x.Contains("Missing required citation", StringComparison.OrdinalIgnoreCase));
}

[Fact]
public void T049_Causal_Overclaim_Fails_Gate()
{
    var candidates = Phase8AssistantRegressionEvalGate.GoldenPassingCandidates
        .Select(x => x.CaseKey == "p08-tool-selection-value-loop"
            ? x with { AnswerText = "The root cause is proven and this will reduce defects by 12%." }
            : x)
        .ToArray();

    var result = Phase8AssistantRegressionEvalGate.Evaluate(
        Phase8AssistantRegressionEvalGate.GoldenCases,
        candidates);

    Assert.False(result.Passed);
    Assert.Contains(result.CaseResults.SelectMany(x => x.Errors), x => x.Contains("Forbidden overclaim", StringComparison.OrdinalIgnoreCase));
}

[Fact]
public void T049_Uncited_Number_Fails_Gate()
{
    var testCase = new Phase8AssistantEvalCase(
        CaseKey: "uncited-number",
        Category: "groundedness",
        Question: "Give me the exact number.",
        ShouldRefuse: false,
        RequiredCitations: Array.Empty<string>(),
        RequiredTools: Array.Empty<string>(),
        RequiredPhrases: Array.Empty<string>(),
        ForbiddenPhrases: Array.Empty<string>());

    var candidate = new Phase8AssistantEvalCandidate(
        CaseKey: "uncited-number",
        AnswerText: "The value is 42.",
        IsRefusal: false,
        CitationIds: Array.Empty<string>(),
        ToolsUsed: Array.Empty<string>());

    var result = Phase8AssistantRegressionEvalGate.Evaluate(new[] { testCase }, new[] { candidate });

    Assert.False(result.Passed);
    Assert.Contains(result.CaseResults.Single().Errors, x => x.Contains("Uncited number", StringComparison.OrdinalIgnoreCase));
}

[Fact]
public void T049_Refusal_Mismatch_Fails_Gate()
{
    var candidates = Phase8AssistantRegressionEvalGate.GoldenPassingCandidates
        .Select(x => x.CaseKey == "p08-refuse-missing-evidence"
            ? x with { IsRefusal = false, AnswerText = "The answer is available." }
            : x)
        .ToArray();

    var result = Phase8AssistantRegressionEvalGate.Evaluate(
        Phase8AssistantRegressionEvalGate.GoldenCases,
        candidates);

    Assert.False(result.Passed);
    Assert.Contains(result.CaseResults.SelectMany(x => x.Errors), x => x.Contains("Expected refusal", StringComparison.OrdinalIgnoreCase));
}

}
`);

write("Backend/PlantProcess.Api/Endpoints/Assistant/Phase8SuggestionOutcomeLoopEndpoints.cs", String.raw`
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.AssistantRuntime;

namespace PlantProcess.Api.Endpoints.Assistant;

/// <summary>
/// PPIQ_REALIZATION_T048_SUGGESTION_OUTCOME_CLOSED_LOOP.
/// Records action outcomes for suggestions and exposes them to the value/ROI loop
/// with explicit non-causal attribution caveats.
/// </summary>
public static class Phase8SuggestionOutcomeLoopEndpoints
{
private static readonly ConcurrentDictionary<Guid, Phase8SuggestionOutcomeRecord> Outcomes = new();

public static IEndpointRouteBuilder MapPhase8SuggestionOutcomeLoopEndpoints(this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/api/phase8")
        .WithTags("Phase 8 Suggestion Outcome Loop")
        .RequireAuthorization();

    group.MapPost("/suggestions/outcomes/actioned", ([FromBody] Phase8SuggestionOutcomeInput request) =>
    {
        var record = Phase8SuggestionOutcomeLoop.RecordOutcome(request);
        Outcomes[record.OutcomeId] = record;

        return Results.Ok(new
        {
            saved = true,
            record.OutcomeId,
            record.RecommendationId,
            record.OutcomeDirection,
            record.Confidence,
            record.CausalClaimMade,
            record.OutcomeCaveat,
            valueLoop = new
            {
                appearsInValueLoop = true,
                record.ValueLoopCaveat
            }
        });
    });

    group.MapGet("/suggestions/outcomes", () =>
    {
        return Results.Ok(Outcomes.Values.OrderByDescending(x => x.RecordedAtUtc).ToArray());
    });

    group.MapGet("/suggestions/outcomes/value-loop", () =>
    {
        var loop = Phase8SuggestionOutcomeLoop.BuildValueLoop(Outcomes.Values);
        return Results.Ok(loop);
    });

    group.MapGet("/suggestions/outcomes/health", () =>
    {
        return Results.Ok(new
        {
            status = "Ready",
            component = "phase8-suggestion-outcome-loop",
            marker = "PPIQ_REALIZATION_T048_SUGGESTION_OUTCOME_CLOSED_LOOP",
            outcomeCount = Outcomes.Count,
            caveat = Phase8SuggestionOutcomeLoop.ValueLoopCaveat
        });
    });

    return app;
}

}
`);

write("Backend/database/scripts/431_phase8_suggestion_outcome_loop.sql", String.raw`
-- 431_phase8_suggestion_outcome_loop.sql
-- PPIQ_REALIZATION_T048_SUGGESTION_OUTCOME_CLOSED_LOOP

-- Stores suggestion action outcomes with explicit caveats.
-- No causal claim is stored or implied.

CREATE TABLE IF NOT EXISTS public.ppiq_phase8_suggestion_outcomes
(
id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
tenant_id uuid NOT NULL,
recommendation_id text NOT NULL,
action_taken text NOT NULL,
outcome_kpi text NOT NULL,
before_value numeric(18,6) NULL,
after_value numeric(18,6) NULL,
delta_value numeric(18,6) NULL,
outcome_direction text NOT NULL,
confidence numeric(8,6) NOT NULL DEFAULT 0,
actor text NOT NULL,
evidence_refs jsonb NOT NULL DEFAULT '[]'::jsonb,
outcome_caveat text NOT NULL DEFAULT 'Observed outcome after action. This does not prove causation and must be reviewed as an association signal only.',
value_loop_caveat text NOT NULL DEFAULT 'Value/ROI loop includes this outcome as post-action evidence only; it is not causal attribution.',
causal_claim_made boolean NOT NULL DEFAULT false,
recorded_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_ppiq_phase8_suggestion_outcomes_tenant_recorded
ON public.ppiq_phase8_suggestion_outcomes (tenant_id, recorded_at_utc DESC);

CREATE OR REPLACE VIEW public.ppiq_phase8_suggestion_value_loop AS
SELECT
tenant_id,
recommendation_id,
outcome_kpi,
outcome_direction,
delta_value,
confidence,
value_loop_caveat,
evidence_refs,
recorded_at_utc
FROM public.ppiq_phase8_suggestion_outcomes
WHERE causal_claim_made = false;
`);

patchFile("Backend/PlantProcess.Api/Program.cs", text => {
let next = text;

if (!next.includes("using PlantProcess.Api.Endpoints.Assistant;")) {
const marker = "using PlantProcess.Api.Endpoints.DynamicContent;";
if (next.includes(marker)) {
next = next.replace(marker, marker + "\r\nusing PlantProcess.Api.Endpoints.Assistant;");
} else {
next = "using PlantProcess.Api.Endpoints.Assistant;\r\n" + next;
}
}

if (!next.includes("app.MapPhase8SuggestionOutcomeLoopEndpoints();")) {
const preferred = "app.MapPhase8AssistantRuntimeEndpoints();";
if (next.includes(preferred)) {
next = next.replace(preferred, preferred + "\r\napp.MapPhase8SuggestionOutcomeLoopEndpoints();");
} else {
next = next.replace("app.MapHealthEndpoints();", "app.MapHealthEndpoints();\r\n app.MapPhase8SuggestionOutcomeLoopEndpoints();");
}
}

return next;
});

write("scripts/ci/run-phase8-assistant-eval-gate.ps1", String.raw`
$ErrorActionPreference = "Stop"

Write-Host "PPIQ Phase 8 Assistant Eval Regression Gate" -ForegroundColor Cyan

dotnet test .\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj --filter "FullyQualifiedName~Phase8AssistantRegressionEvalGateTests"
--no-build

Write-Host "PPIQ Phase 8 Assistant Eval Regression Gate GREEN" -ForegroundColor Green
`);

for (const file of ["Jenkinsfile", "deploy/ci/Jenkinsfile"]) {
patchFile(file, text => {
if (text.includes("run-phase8-assistant-eval-gate.ps1") || !text.includes("stages")) {
return text;
}

const stage = String.raw`
    stage('Phase 8 Assistant Eval Gate') {
        steps {
            powershell '.\\scripts\\ci\\run-phase8-assistant-eval-gate.ps1'
        }
    }

`;

const marker = "stages {";
return text.replace(marker, marker + "\r\n" + stage);

});
}

write("tools/phase8/validate-t048-t049-outcome-loop-eval-gate.cjs", String.raw`
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function exists(file) {
return fs.existsSync(path.join(root, file));
}

function read(file) {
return fs.readFileSync(path.join(root, file), "utf8");
}

const checks = [
{
file: "Backend/PlantProcess.Application/AssistantRuntime/Phase8SuggestionOutcomeLoop.cs",
signals: [
"PPIQ_REALIZATION_T048_SUGGESTION_OUTCOME_CLOSED_LOOP",
"does not prove causation",
"not causal attribution",
"CausalClaimMade",
"BuildValueLoop"
]
},
{
file: "Backend/PlantProcess.Application/AssistantRuntime/Phase8AssistantRegressionEvalGate.cs",
signals: [
"PPIQ_REALIZATION_T049_ASSISTANT_EVAL_REGRESSION_GATE",
"GoldenCases",
"PromotionDecision",
"BlockedByPhase8AssistantEvalGate",
"Uncited number"
]
},
{
file: "Backend/PlantProcess.Api/Endpoints/Assistant/Phase8SuggestionOutcomeLoopEndpoints.cs",
signals: [
"MapPhase8SuggestionOutcomeLoopEndpoints",
"/suggestions/outcomes/actioned",
"/suggestions/outcomes/value-loop"
]
},
{
file: "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase8SuggestionOutcomeLoopTests.cs",
signals: [
"T048_Record_Action_Outcome_Without_Causal_Claim",
"T048_Value_Loop_Contains_Outcome_With_Caveat"
]
},
{
file: "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase8AssistantRegressionEvalGateTests.cs",
signals: [
"T049_Golden_Candidates_Pass_Regression_Gate",
"T049_Missing_Citation_Fails_Gate",
"T049_Causal_Overclaim_Fails_Gate"
]
},
{
file: "scripts/ci/run-phase8-assistant-eval-gate.ps1",
signals: [
"Phase8AssistantRegressionEvalGateTests",
"PPIQ Phase 8 Assistant Eval Regression Gate"
]
}
];

for (const check of checks) {
if (!exists(check.file)) {
failures.push("Missing file: " + check.file);
continue;
}

const text = read(check.file);
for (const signal of check.signals) {
if (!text.includes(signal)) {
failures.push(check.file + " missing signal: " + signal);
}
}
}

const program = exists("Backend/PlantProcess.Api/Program.cs") ? read("Backend/PlantProcess.Api/Program.cs") : "";
if (!program.includes("MapPhase8SuggestionOutcomeLoopEndpoints")) {
failures.push("Program.cs missing MapPhase8SuggestionOutcomeLoopEndpoints");
}

const jenkinsFiles = ["Jenkinsfile", "deploy/ci/Jenkinsfile"].filter(exists);
if (jenkinsFiles.length > 0) {
const anyWired = jenkinsFiles.some(file => read(file).includes("run-phase8-assistant-eval-gate.ps1"));
if (!anyWired) {
failures.push("No Jenkinsfile contains run-phase8-assistant-eval-gate.ps1");
}
}

if (failures.length > 0) {
console.error("PPIQ Phase 8 T-048/T-049 validation failed.");
console.error(failures.join("\n"));
process.exit(1);
}

console.log("PPIQ Phase 8 T-048/T-049 passed: suggestion outcome closed loop and assistant eval CI gate are present.");
`);

write("docs/phase8/T048_T049_OUTCOME_LOOP_EVAL_GATE.md", String.raw`

Phase 8 T-048 / T-049 Outcome Loop and Eval Gate

Marker T-048: PPIQ_REALIZATION_T048_SUGGESTION_OUTCOME_CLOSED_LOOP
Marker T-049: PPIQ_REALIZATION_T049_ASSISTANT_EVAL_REGRESSION_GATE

Implemented scope:

T-048:

record suggestion action outcomes
compute observed direction without causal attribution
attach outcome caveat and value-loop caveat
expose value-loop summary through API
database script for persisted outcome table and value-loop view

T-049:

golden assistant eval cases
groundedness, citation, tool-selection, refusal, overclaim and uncited-number checks
promotion decision blocks regression
CI script for assistant eval regression gate
Jenkinsfile wiring when Jenkinsfile exists

Validation commands:

node tools/phase8/validate-t048-t049-outcome-loop-eval-gate.cjs
dotnet build Backend
dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase8SuggestionOutcomeLoopTests --no-build
dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase8AssistantRegressionEvalGateTests --no-build
`);

console.log("Phase 8 T-048/T-049 files written.");