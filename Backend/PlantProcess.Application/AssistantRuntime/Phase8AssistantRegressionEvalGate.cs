using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlantProcess.Application.AssistantRuntime;

/// <summary>
/// PPIQ_REALIZATION_T049_ASSISTANT_EVAL_REGRESSION_GATE.
/// Golden assistant eval gate for groundedness, tool selection, correctness,
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
    private static readonly Regex NumberRegex = new(@"(?<![A-Za-z0-9])\d+(\.\d+)?%?", RegexOptions.Compiled);

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