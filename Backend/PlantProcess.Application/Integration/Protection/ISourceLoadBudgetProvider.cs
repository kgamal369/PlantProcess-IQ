using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Protection;

/// <summary>
/// Resolves the load budget that applies to a given source read. Allows future
/// per-connection or per-dataset overrides without touching the enforcement seam.
/// </summary>
public interface ISourceLoadBudgetProvider
{
    SourceLoadBudget GetBudget(ConnectionProfile? connectionProfile, SourceDatasetDefinition? datasetDefinition);
}