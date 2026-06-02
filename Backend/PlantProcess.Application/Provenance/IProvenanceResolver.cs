namespace PlantProcess.Application.Provenance;

/// <summary>Resolves a <see cref="ProvenanceHandle"/> to the live artifact it points at.</summary>
public interface IProvenanceResolver
{
    Task<ProvenanceResolution> ResolveAsync(ProvenanceHandle handle, CancellationToken cancellationToken);
}