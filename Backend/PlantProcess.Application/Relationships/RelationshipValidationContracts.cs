namespace PlantProcess.Application.Relationships;

/// <summary>
/// What was actually observed when a declared relationship was run against real
/// canonical rows. Every number is a count the database returned; none is derived
/// from the declaration itself, because the point of validation is to compare the
/// two.
/// </summary>
public sealed record RelationshipValidationEvidenceDto(
    long LeftPopulation,
    long RightPopulation,
    long LeftMatched,
    long RightMatched,
    long LeftOrphans,
    long RightOrphans,
    bool RightFansOut,
    bool LeftFansOut,
    string ObservedCardinality,
    string DeclaredCardinality,
    bool CardinalityContradicted,
    DateTime ValidatedAtUtc);

/// <summary>The outcome persisted on the relationship after validation.</summary>
public sealed record RelationshipValidationResultDto(
    Guid RelationshipId,
    string RelationshipCode,
    string ValidationState,
    string Reason,
    RelationshipValidationEvidenceDto Evidence);

/// <summary>
/// Reads validation evidence for one published relationship from the canonical
/// model. The reader executes the DECLARED members on the DECLARED entities and
/// counts. It chooses nothing, prefers nothing, and if a declared member does not
/// map it reports that rather than substituting one that does.
/// </summary>
public interface IRelationshipValidationEvidenceReader
{
    Task<RelationshipValidationEvidenceDto> ReadAsync(RelationshipDto relationship, CancellationToken cancellationToken);
}
