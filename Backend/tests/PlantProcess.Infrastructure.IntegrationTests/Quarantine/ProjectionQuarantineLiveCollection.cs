namespace PlantProcess.Infrastructure.IntegrationTests.Quarantine;

/// <summary>
/// PPIQ T-099. The live quarantine classes share one PostgreSQL template source
/// and each one creates and drops its own database. Letting xUnit decide whether
/// they overlap would interleave CREATE DATABASE ... TEMPLATE, DROP ... WITH
/// (FORCE) and ClearAllPools across processes that are all holding connections
/// to the same cluster. Serialising them is the contract, not a precaution.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProjectionQuarantineLiveCollection
{
    public const string Name = "ProjectionQuarantineLive";
}
