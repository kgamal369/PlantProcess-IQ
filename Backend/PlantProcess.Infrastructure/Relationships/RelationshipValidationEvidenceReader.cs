using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Canonical;
using PlantProcess.Application.Relationships;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Relationships;

/// <summary>
/// Runs one declared relationship against the real canonical rows and reports what it
/// found. Nothing here interprets the result; the service decides what the counts mean.
///
/// The members executed are exactly the members declared, AND-ed in declared order on
/// the entities declared, rendered as SQL from EF's relational metadata. If a member
/// does not map, the read fails by name and the caller must not persist a state from
/// it: a relationship that could not be evaluated is unevaluated, not failed.
/// </summary>
public sealed class RelationshipValidationEvidenceReader : IRelationshipValidationEvidenceReader
{
    private readonly PlantProcessDbContext _db;
    private readonly ICanonicalEntityCatalog _canonicalEntities;

    public RelationshipValidationEvidenceReader(PlantProcessDbContext db, ICanonicalEntityCatalog canonicalEntities)
    {
        _db = db;
        _canonicalEntities = canonicalEntities;
    }

    public async Task<RelationshipValidationEvidenceDto> ReadAsync(
        RelationshipDto relationship, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        var names = new CanonicalRelationalPath(_db, _canonicalEntities);
        var code = relationship.RelationshipCode;

        var left = names.RequireMapped(names.RequireEntity(relationship.LeftEntity, code), code);
        var right = names.RequireMapped(names.RequireEntity(relationship.RightEntity, code), code);

        // The declared members as one executable hop, left to right, in declared order.
        var step = new RelationshipJoinStepDto(
            relationship.Id, code, relationship.LeftEntity, relationship.RightEntity,
            relationship.JoinType, relationship.Cardinality,
            relationship.IsGrainConverting, relationship.AttributionRule,
            relationship.Members
                .OrderBy(m => m.MemberOrder)
                .Select(m => new RelationshipJoinPredicateDto(m.LeftColumn, m.RightColumn, m.Comparison, m.MemberOrder))
                .ToList());

        var leftTable = names.Table(left, code);
        var rightTable = names.Table(right, code);
        var join = names.MembersEqual(step, left, "l", right, "r");
        var leftLive = names.LiveOnly(left, "l", code);
        var rightLive = names.LiveOnly(right, "r", code);

        var leftPopulation = await CountAsync(
            "SELECT count(*) AS \"Value\" FROM " + leftTable + " l WHERE TRUE" + leftLive, cancellationToken);

        var rightPopulation = await CountAsync(
            "SELECT count(*) AS \"Value\" FROM " + rightTable + " r WHERE TRUE" + rightLive, cancellationToken);

        var leftMatched = await CountAsync(
            "SELECT count(*) AS \"Value\" FROM " + leftTable + " l WHERE TRUE" + leftLive +
            " AND EXISTS (SELECT 1 FROM " + rightTable + " r WHERE " + join + rightLive + ")", cancellationToken);

        var rightMatched = await CountAsync(
            "SELECT count(*) AS \"Value\" FROM " + rightTable + " r WHERE TRUE" + rightLive +
            " AND EXISTS (SELECT 1 FROM " + leftTable + " l WHERE " + join + leftLive + ")", cancellationToken);

        // Fan-out is recorded as a fact rather than a maximum: whether ONE row on a side
        // reaches more than one row on the other. That is the only thing a declared
        // cardinality can be contradicted by.
        var rightFansOut = leftPopulation != 0 && await ExistsAsync(
            "SELECT EXISTS (SELECT 1 FROM " + leftTable + " l WHERE TRUE" + leftLive +
            " AND (SELECT count(*) FROM " + rightTable + " r WHERE " + join + rightLive + ") > 1) AS \"Value\"",
            cancellationToken);

        var leftFansOut = rightPopulation != 0 && await ExistsAsync(
            "SELECT EXISTS (SELECT 1 FROM " + rightTable + " r WHERE TRUE" + rightLive +
            " AND (SELECT count(*) FROM " + leftTable + " l WHERE " + join + leftLive + ") > 1) AS \"Value\"",
            cancellationToken);

        return new RelationshipValidationEvidenceDto(
            leftPopulation, rightPopulation,
            leftMatched, rightMatched,
            leftPopulation - leftMatched, rightPopulation - rightMatched,
            rightFansOut, leftFansOut,
            Cardinality(leftFansOut, rightFansOut),
            relationship.Cardinality,
            Contradicts(relationship.Cardinality, leftFansOut, rightFansOut),
            DateTime.UtcNow);
    }

    private async Task<long> CountAsync(string sql, CancellationToken cancellationToken) =>
        await _db.Database.SqlQueryRaw<long>(sql).SingleAsync(cancellationToken);

    private async Task<bool> ExistsAsync(string sql, CancellationToken cancellationToken) =>
        await _db.Database.SqlQueryRaw<bool>(sql).SingleAsync(cancellationToken);

    private static string Cardinality(bool leftFansOut, bool rightFansOut)
    {
        var leftSide = leftFansOut ? "n" : "1";
        var rightSide = rightFansOut ? (leftFansOut ? "m" : "n") : "1";
        return leftSide + "-" + rightSide;
    }

    /// <summary>
    /// A declaration is contradicted only by an observed fan-out it forbids. Declaring
    /// 'n-m' forbids nothing; declaring '1-1' forbids fan-out on either side.
    /// </summary>
    private static bool Contradicts(string declared, bool leftFansOut, bool rightFansOut) =>
        declared switch
        {
            RelationshipCardinalities.OneToOne => leftFansOut || rightFansOut,
            RelationshipCardinalities.OneToMany => leftFansOut,
            RelationshipCardinalities.ManyToOne => rightFansOut,
            _ => false
        };
}
