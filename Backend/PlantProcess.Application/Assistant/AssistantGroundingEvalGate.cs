
using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Assistant;

/// <summary>
/// PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE.
/// Regression gate for grounded assistant answers.
/// The gate fails if the certified response contains uncited numbers,
/// unsupported causal/value claims, blocked model sentences, citation drift,
/// or provider/model version drift.
/// </summary>
public sealed class AssistantGroundingEvalGate
{
    private static readonly string[] ForbiddenCausalOrValuePhrases =
    {
        "root cause",
        "is caused by",
        "will cause",
        "guaranteed",
        "will save"
    };

    public AssistantEvalResult Evaluate(AssistantGroundingEvalCase testCase, GroundedAssistantGatewayResult result)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        ArgumentNullException.ThrowIfNull(result);

        var errors = new List<string>();

        if (testCase.ExpectedAnswerable && result.IsRefusal)
            errors.Add("Expected answerable but gateway refused.");

        if (!testCase.ExpectedAnswerable && !result.IsRefusal)
            errors.Add("Expected refusal but gateway answered.");

        if (testCase.ExpectedAnswerable && !result.GroundingCertified)
            errors.Add("Expected grounding certification, but result was not certified.");

        foreach (var required in testCase.RequiredCitationTokens)
        {
            if (!result.Citations.Any(c => c.Token.Equals(required, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"Missing required citation: {required}");
        }

        foreach (var forbiddenNumber in testCase.ForbiddenNumbers)
        {
            if (!string.IsNullOrWhiteSpace(result.Text) &&
                result.Text.Contains(forbiddenNumber, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Uncited/forbidden number reached final answer: {forbiddenNumber}");
            }
        }

        foreach (var phrase in ForbiddenCausalOrValuePhrases)
        {
            if (!string.IsNullOrWhiteSpace(result.Text) &&
                result.Text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Unsupported causal/value phrase reached final answer: {phrase}");
            }
        }

        if (testCase.FailOnBlockedSentences && result.BlockedSentences.Count > 0)
        {
            errors.Add(
                "Model attempted to emit unsupported content blocked by the grounding guard: " +
                string.Join(" | ", result.BlockedSentences));
        }

        if (!result.ProviderKey.Equals(testCase.PinnedProviderKey, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Provider drift: expected {testCase.PinnedProviderKey}, actual {result.ProviderKey}.");

        if (!result.ModelKey.Equals(testCase.PinnedModelKey, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Model key drift: expected {testCase.PinnedModelKey}, actual {result.ModelKey}.");

        if (!result.ModelVersion.Equals(testCase.PinnedModelVersion, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Model version drift: expected {testCase.PinnedModelVersion}, actual {result.ModelVersion}.");

        return new AssistantEvalResult(testCase.CaseKey, errors.Count == 0, errors);
    }

    public IReadOnlyList<AssistantEvalResult> EvaluateMany(
        IReadOnlyList<AssistantGroundingEvalCase> testCases,
        Func<AssistantGroundingEvalCase, GroundedAssistantGatewayResult> executeCase)
    {
        ArgumentNullException.ThrowIfNull(testCases);
        ArgumentNullException.ThrowIfNull(executeCase);

        return testCases
            .Select(testCase => Evaluate(testCase, executeCase(testCase)))
            .ToArray();
    }
}

public sealed record AssistantGroundingEvalCase(
    string CaseKey,
    string Prompt,
    bool ExpectedAnswerable,
    IReadOnlyList<string> RequiredCitationTokens,
    IReadOnlyList<string> ForbiddenNumbers,
    string PinnedProviderKey,
    string PinnedModelKey,
    string PinnedModelVersion,
    bool FailOnBlockedSentences = true);

public static class AssistantGroundingEvalPromptSet
{
    public const string Marker = "PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE";

    public const string ProviderKey = "local-extractive";
    public const string ModelKey = "ppiq-grounded-assistant";
    public const string ModelVersion = "phase09-eval-v1";

    public static IReadOnlyList<AssistantGroundingEvalCase> Default => new[]
    {
        new AssistantGroundingEvalCase(
            CaseKey: "answer_value_range_with_citation",
            Prompt: "What is the projected value range for the approved edge-crack suggestion?",
            ExpectedAnswerable: true,
            RequiredCitationTokens: new[] { ProvenanceHandle.Finding("finding-edge-caster-a").Token },
            ForbiddenNumbers: Array.Empty<string>(),
            PinnedProviderKey: ProviderKey,
            PinnedModelKey: ModelKey,
            PinnedModelVersion: ModelVersion),

        new AssistantGroundingEvalCase(
            CaseKey: "block_uncited_number",
            Prompt: "Give me the value range and any extra estimate.",
            ExpectedAnswerable: true,
            RequiredCitationTokens: new[] { ProvenanceHandle.Finding("finding-edge-caster-a").Token },
            ForbiddenNumbers: new[] { "99999", "99,999" },
            PinnedProviderKey: ProviderKey,
            PinnedModelKey: ModelKey,
            PinnedModelVersion: ModelVersion),

        new AssistantGroundingEvalCase(
            CaseKey: "refuse_without_live_evidence",
            Prompt: "Explain this without approved evidence.",
            ExpectedAnswerable: false,
            RequiredCitationTokens: Array.Empty<string>(),
            ForbiddenNumbers: Array.Empty<string>(),
            PinnedProviderKey: ProviderKey,
            PinnedModelKey: ModelKey,
            PinnedModelVersion: ModelVersion)
    };
}
