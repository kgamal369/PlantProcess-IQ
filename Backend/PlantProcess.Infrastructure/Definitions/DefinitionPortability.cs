using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;

namespace PlantProcess.Infrastructure.Definitions;

/// <summary>
/// PPIQ T-091. The portability facade the application layer depends on.
///
/// Export and import are separate classes because they have nothing in common
/// beyond the artifact type: one reads the canonical store and canonicalises,
/// the other validates and orchestrates writes. This facade is the single
/// registered contract so callers depend on one interface rather than two
/// concrete Infrastructure types.
/// </summary>
public sealed class DefinitionPortability : IDefinitionPortability
{
    private readonly DefinitionExporter _exporter;
    private readonly DefinitionImporter _importer;

    public DefinitionPortability(DefinitionExporter exporter, DefinitionImporter importer)
    {
        _exporter = exporter;
        _importer = importer;
    }

    public Task<ApplicationResult<DefinitionArtifact>> ExportAsync(
        Guid tenantId,
        Guid definitionId,
        int? versionNumber,
        CancellationToken cancellationToken) =>
        _exporter.ExportAsync(tenantId, definitionId, versionNumber, cancellationToken);

    public Task<ApplicationResult<DefinitionImportResult>> ImportAsync(
        Guid tenantId,
        Guid ownerId,
        DefinitionArtifact artifact,
        CancellationToken cancellationToken) =>
        _importer.ImportAsync(tenantId, ownerId, artifact, cancellationToken);
}
