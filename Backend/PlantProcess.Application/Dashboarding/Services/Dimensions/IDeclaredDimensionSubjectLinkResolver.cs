using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlantProcess.Application.Dashboarding.Services.Dimensions;

/// <summary>
/// Resolves a declared dimension that is published against a RELATED canonical entity
/// into the subject keys its value selects.
///
/// The Application layer holds no model metadata: IPlantProcessDbContext publishes
/// named sets, a database facade and SaveChanges, and nothing that could answer "which
/// reference links these two entities". That question is persistence knowledge and its
/// answer lives behind this contract, exactly as the declared-dimension catalogue holds
/// the answer to "which entity and member does this declaration bind to".
///
/// The implementation decides nothing on its own: it reads the model, hands the
/// candidates to DeclaredDimensionProjection.SelectSubjectLinkField, and executes the
/// projection that class builds. A refusal is a typed refusal, never an empty set.
/// </summary>
public interface IDeclaredDimensionSubjectLinkResolver
{
    Task<IReadOnlyList<Guid>> SubjectKeysWhereDeclaredEqualsAsync(
        DeclaredDimension declared,
        Type subjectEntityType,
        string value,
        CancellationToken cancellationToken);
}