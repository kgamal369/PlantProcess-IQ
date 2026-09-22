namespace PlantProcess.Application.Jobs.Execution.Transformations;

/// <summary>Scopes source reads to the tenant of the exact executed definition.</summary>
public interface IDefinitionScopedTransformationSourceReader
{
    Task<IAsyncDisposable> BindDefinitionAsync(Guid definitionId, int version, CancellationToken cancellationToken);
}
