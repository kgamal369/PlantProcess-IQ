namespace PlantProcess.Application.Analytics.Value;

/// <summary>Versioned, tenant-scoped cost-assumption store. Every edit creates a new version and an audit row.</summary>
public interface ICostAssumptionStore
{
    Task<CostAssumptionSet?> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Creates a new version for the tenant and returns its version number.</summary>
    Task<int> CreateVersionAsync(Guid tenantId, CostAssumptionSet set, string actor, CancellationToken cancellationToken);
}