using Microsoft.Extensions.Options;
using PlantProcess.Application.Integration.Protection;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.Integration.Protection;

/// <summary>
/// Configuration-backed source-load budget provider. Reads the current
/// SourceLoadBudgetOptions (section "PlantProcess:SourceLoad") on each call so the
/// budget can be tuned without redeploying. Per-source overrides can be added here.
/// </summary>
public sealed class OptionsSourceLoadBudgetProvider : ISourceLoadBudgetProvider
{
    private readonly IOptionsMonitor<SourceLoadBudgetOptions> _options;

    public OptionsSourceLoadBudgetProvider(IOptionsMonitor<SourceLoadBudgetOptions> options)
    {
        _options = options;
    }

    public SourceLoadBudget GetBudget(ConnectionProfile? connectionProfile, SourceDatasetDefinition? datasetDefinition)
    {
        return _options.CurrentValue.ToBudget();
    }
}