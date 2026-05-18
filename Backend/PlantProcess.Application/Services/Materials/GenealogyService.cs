using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Materials;
using PlantProcess.Domain.Entities.Materials;

namespace PlantProcess.Application.Services.Materials;

public sealed class GenealogyService : IGenealogyService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly ILogger<GenealogyService> _logger;

    public GenealogyService(
        IPlantProcessDbContext dbContext,
        ILogger<GenealogyService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApplicationResult<Guid>> CreateEdgeAsync(
        CreateGenealogyEdgeCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ParentMaterialUnitId == Guid.Empty)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Parent material unit ID is required."));

        if (command.ChildMaterialUnitId == Guid.Empty)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Child material unit ID is required."));

        if (command.ParentMaterialUnitId == command.ChildMaterialUnitId)
            return ApplicationResult<Guid>.Failure(ApplicationError.BusinessRule("A material cannot be its own parent."));

        if (string.IsNullOrWhiteSpace(command.RelationshipType))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Relationship type is required."));

        var parentExists = await _dbContext.MaterialUnits
            .AnyAsync(x => x.Id == command.ParentMaterialUnitId, cancellationToken);

        if (!parentExists)
            return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Parent material unit does not exist."));

        var childExists = await _dbContext.MaterialUnits
            .AnyAsync(x => x.Id == command.ChildMaterialUnitId, cancellationToken);

        if (!childExists)
            return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Child material unit does not exist."));

        var duplicate = await _dbContext.GenealogyEdges.AnyAsync(x =>
                x.ParentMaterialUnitId == command.ParentMaterialUnitId &&
                x.ChildMaterialUnitId == command.ChildMaterialUnitId &&
                x.RelationshipType == command.RelationshipType.Trim(),
            cancellationToken);

        if (duplicate)
            return ApplicationResult<Guid>.Failure(ApplicationError.Conflict("Genealogy edge already exists."));

        var createsCycle = await WouldCreateCycleAsync(
            command.ParentMaterialUnitId,
            command.ChildMaterialUnitId,
            cancellationToken);

        if (createsCycle)
        {
            return ApplicationResult<Guid>.Failure(
                ApplicationError.BusinessRule("Genealogy edge would create a material genealogy cycle."));
        }

        var edge = new GenealogyEdge(
            parentMaterialUnitId: command.ParentMaterialUnitId,
            childMaterialUnitId: command.ChildMaterialUnitId,
            relationshipType: command.RelationshipType,
            isSynthetic: command.Metadata.IsSynthetic,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId);

        edge.SetEffectiveWindow(command.EffectiveFromUtc, command.EffectiveToUtc);

        _dbContext.GenealogyEdges.Add(edge);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created genealogy edge. GenealogyEdgeId={GenealogyEdgeId}, ParentMaterialUnitId={ParentMaterialUnitId}, ChildMaterialUnitId={ChildMaterialUnitId}, RelationshipType={RelationshipType}, CorrelationId={CorrelationId}",
            edge.Id,
            edge.ParentMaterialUnitId,
            edge.ChildMaterialUnitId,
            edge.RelationshipType,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(edge.Id);
    }

    private async Task<bool> WouldCreateCycleAsync(
        Guid parentMaterialUnitId,
        Guid childMaterialUnitId,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(parentMaterialUnitId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!visited.Add(current))
                continue;

            if (current == childMaterialUnitId)
                return true;

            var parents = await _dbContext.GenealogyEdges
                .AsNoTracking()
                .Where(x => x.ChildMaterialUnitId == current)
                .Select(x => x.ParentMaterialUnitId)
                .ToListAsync(cancellationToken);

            foreach (var parent in parents)
                queue.Enqueue(parent);
        }

        return false;
    }
}



