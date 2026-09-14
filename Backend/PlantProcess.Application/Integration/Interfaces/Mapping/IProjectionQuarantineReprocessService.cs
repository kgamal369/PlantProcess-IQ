using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Mapping;

namespace PlantProcess.Application.Integration.Interfaces.Mapping;

/// <summary>
/// PPIQ T-099. Bounded reprocessing of exactly one quarantined row.
///
/// T-099 owns this and POST /api/quarantine/reprocess only. List, detail,
/// dismiss and Mapping Health belong to T-101.
/// </summary>
public interface IProjectionQuarantineReprocessService
{
    Task<ApplicationResult<QuarantineReprocessResult>> ReprocessAsync(
        Guid quarantineId,
        string? tenantCode,
        CancellationToken cancellationToken);
}
