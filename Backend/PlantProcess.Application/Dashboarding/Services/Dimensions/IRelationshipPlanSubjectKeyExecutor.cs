using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Relationships;

namespace PlantProcess.Application.Dashboarding.Services.Dimensions;

/// <summary>
/// Executes a relationship path that has ALREADY been chosen, and chooses nothing.
///
/// This replaces the interim contract T-094 had to introduce before a path authority
/// existed. That one was called a resolver and behaved like one: it read the mapped
/// model, counted the foreign keys between two entities, and refused when there were
/// none or more than one. Counting references is choosing a path, and a second thing
/// choosing paths is a second answer to the same question - which is exactly the defect
/// the canonical relationship model exists to remove.
///
/// The division of labour is now fixed and this contract sits at the end of it:
///
///     IRelationshipResolver     decides WHICH governed path
///     IRelationshipJoinPlanner  turns that path into ordered, oriented predicates
///     this                      executes the plan it is handed
///
/// An implementation may read the mapped model only to execute the members the plan
/// names. It may not look for an alternative when a member will not map, may not prefer
/// one path over another, and may not fall back to reference discovery. A plan it cannot
/// run is refused by name, because a fallback would quietly restore the second authority.
/// </summary>
public interface IRelationshipPlanSubjectKeyExecutor
{
    /// <summary>
    /// The subject keys selected by a declared dimension's value, reached along a
    /// governed plan whose first entity is the declaration's entity and whose last is
    /// the subject of the population.
    /// </summary>
    Task<IReadOnlyList<Guid>> SubjectKeysAsync(
        RelationshipJoinPlanDto plan,
        DeclaredDimension declared,
        Type subjectEntityType,
        string value,
        CancellationToken cancellationToken);
}
