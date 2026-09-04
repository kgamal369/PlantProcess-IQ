using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlantProcess.Application.Dashboarding.Services.Dimensions;

/// <summary>
/// Read-only access to the customer's PUBLISHED declared dimensions for one tenant.
/// The definition store stays the lifecycle authority; this catalogue only projects it
/// into something the execution engine can bind.
/// </summary>
public interface IDeclaredDimensionCatalog
{
    Task<IReadOnlyList<DeclaredDimension>> GetPublishedAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<DeclaredDimension?> FindAsync(Guid tenantId, string code, CancellationToken cancellationToken);
}