namespace PlantProcess.Application.Assistant;

/// <summary>
/// PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE.
/// PPIQ-T027/T048: deterministic assistant eval harness.
/// CI should fail when answers contain uncited numbers, missing citations, provider drift, or wrong refusal behavior.
/// </summary>
public sealed class AssistantEvalHarness
{
    public AssistantEvalResult Evaluate(AssistantEvalCase testCase, GroundedAssistantGatewayResult result)
    {
        var errors = new List<string>();

        if (testCase.ExpectedAnswerable && result.IsRefusal)
            errors.Add("Expected answerable but gateway refused.");

        if (!testCase.ExpectedAnswerable && !result.IsRefusal)
            errors.Add("Expected refusal but gateway answered.");

        foreach (var required in testCase.RequiredCitationTokens)
        {
            if (!result.Citations.Any(c => c.Token.Equals(required, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"Missing required citation: {required}");
        }

        foreach (var number in testCase.ForbiddenNumbers)
        {
            if (!string.IsNullOrWhiteSpace(result.Text) &&
                result.Text.Contains(number, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Uncited/forbidden number reached answer: {number}");
            }
        }

        if (result.ModelVersion != testCase.PinnedModelVersion)
            errors.Add($"Model version drift: expected {testCase.PinnedModelVersion}, actual {result.ModelVersion}.");

        return new AssistantEvalResult(testCase.CaseKey, errors.Count == 0, errors);
    }
}

public sealed record AssistantEvalCase(
    string CaseKey,
    bool ExpectedAnswerable,
    IReadOnlyList<string> RequiredCitationTokens,
    IReadOnlyList<string> ForbiddenNumbers,
    string PinnedModelVersion);

public sealed record AssistantEvalResult(
    string CaseKey,
    bool Passed,
    IReadOnlyList<string> Errors);