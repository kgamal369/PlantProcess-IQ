// PPIQ-GENERATED (T005)
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlantProcess.Application.Integration.Security;

/// <summary>
/// PPIQ-T005. Supplies, per tenant, the set of tables a read-only analytic query may reference:
/// the canonical product tables UNION the tenant's registered source tables. The SQL allowlist is
/// therefore derived from real configuration instead of being hardcoded in the validator.
/// </summary>
public interface ITableAllowlistProvider
{
    Task<IReadOnlySet<string>> GetAllowedTablesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}