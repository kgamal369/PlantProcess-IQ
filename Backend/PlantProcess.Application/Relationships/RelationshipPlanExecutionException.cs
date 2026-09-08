using System;

namespace PlantProcess.Application.Relationships;

/// <summary>
/// A governed plan that cannot be executed against the mapped canonical model.
///
/// This is not a relationship refusal and not a dimension refusal, and it is
/// deliberately neither RL nor DB. The relationship authority chose a lawful path;
/// the declaration is sound; what failed is the executor's ability to run that
/// path against the entities and members the model actually maps. Naming the
/// relationship and member that failed is the whole content of the error. The
/// one thing an executor may never do on this condition is look for another way
/// through - that would be a second path authority, restored quietly.
/// </summary>
public sealed class RelationshipPlanExecutionException : Exception
{
    public const string Reason = "relationship_plan_unexecutable";

    public RelationshipPlanExecutionException(string? relationshipCode, string? member, string message)
        : base(message)
    {
        RelationshipCode = relationshipCode;
        Member = member;
    }

    public string? RelationshipCode { get; }

    public string? Member { get; }
}
