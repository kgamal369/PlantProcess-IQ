namespace PlantProcess.Application.Analytics.Suggestions;

public interface ISuggestionEngine
{
    /// <summary>Deterministically generates suggestion cards from approved findings (stable ids and ordering).</summary>
    IReadOnlyList<SuggestionCard> Generate(IReadOnlyList<ApprovedFinding> approvedFindings);
}