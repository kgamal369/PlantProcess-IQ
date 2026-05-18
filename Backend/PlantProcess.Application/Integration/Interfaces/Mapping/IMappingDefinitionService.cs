using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Mapping;

namespace PlantProcess.Application.Integration.Interfaces.Mapping;

/// <summary>
/// Service contract for MappingDefinition management.
/// Implemented by MappingDefinitionService.
/// A MappingDefinition describes how to map columns from a source object
/// to canonical target entity fields using a JSON field-map specification.
/// </summary>
public interface IMappingDefinitionService
{
    /// <summary>
    /// Creates a new mapping definition.
    /// Validates: source system existence, mapping code uniqueness,
    /// target entity is in the allowed canonical list, and MappingJson is valid JSON.
    /// </summary>
    Task<ApplicationResult<Guid>> CreateAsync(
        CreateMappingDefinitionCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the field-map JSON and version of an existing mapping definition.
    /// Used when the source system schema changes and the field mapping must be revised.
    /// Returns ApplicationResult (not void) so callers can handle NotFound / Validation failures.
    /// </summary>
    Task<ApplicationResult> UpdateMappingJsonAsync(
        Guid mappingDefinitionId,
        string mappingJson,
        string mappingVersion,
        CancellationToken cancellationToken);
}


