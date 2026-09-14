using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Interfaces.Mapping;

/// <summary>
/// PPIQ T-099. THE ONE SHARED SINGLE-ROW SEAM.
///
/// Full execution, preview and bounded reprocess all project a row through this
/// method and nothing else. Reprocess in particular must never call the batch
/// executor: a queue entry names one exact row, and re-running a batch to reach
/// it would touch rows nobody asked about and would depend on pending-row
/// ordering that no caller controls.
/// </summary>
public interface IMappingRowProjector
{
    /// <summary>
    /// Definition-level admission. A malformed MappingJson or an unsupported
    /// target entity is a fault of the definition, not of any row, so it is
    /// answered here BEFORE the row loop and creates no quarantine evidence.
    /// </summary>
    ApplicationError? ValidateDefinition(MappingDefinition mapping, out IReadOnlyDictionary<string, string> fieldMap);

    Task<RowProjectionOutcome> ProcessOneRowAsync(
        MappingDefinition mapping,
        IReadOnlyDictionary<string, string> fieldMap,
        StagingRecord stagingRecord,
        MappingExecutionContext executionContext,
        bool previewOnly,
        CancellationToken cancellationToken);
}
