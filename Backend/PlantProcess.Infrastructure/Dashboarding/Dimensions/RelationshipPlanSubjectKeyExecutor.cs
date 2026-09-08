using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PlantProcess.Application.Common.Canonical;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using PlantProcess.Application.Relationships;
using PlantProcess.Infrastructure.Persistence;
using PlantProcess.Infrastructure.Relationships;

namespace PlantProcess.Infrastructure.Dashboarding.Dimensions;

/// <summary>
/// Executes a governed relationship plan and returns the subject identities it reaches.
///
/// The rendered shape is nested existence, composed from the population inwards:
///
///     SELECT DISTINCT s."id" FROM subject s
///     WHERE EXISTS (SELECT 1 FROM hop h
///                   WHERE h."m0" = s."n0" AND h."m1" = s."n1"
///                     AND EXISTS (SELECT 1 FROM source x
///                                 WHERE x."k0" = h."j0" AND x."declared" = @value))
///
/// Every member of a hop is AND-ed in declared order, because real plants key on two or
/// three columns and a composite key with one member dropped is a different join that
/// still returns rows. The subject's identity and the members that reach it are separate
/// facts: a subject may be identified by one key while the relationship finding it
/// compares three business columns.
///
/// Nothing here selects a path. Identifiers come from EF's relational metadata, the
/// declared value travels as a parameter, and a member the model does not map is refused
/// by name with no substitute sought - looking for one would be this class choosing a
/// path, which is the defect this task removed.
/// </summary>
public sealed class RelationshipPlanSubjectKeyExecutor : IRelationshipPlanSubjectKeyExecutor
{
    private readonly PlantProcessDbContext _db;
    private readonly ICanonicalEntityCatalog _canonicalEntities;

    public RelationshipPlanSubjectKeyExecutor(PlantProcessDbContext db, ICanonicalEntityCatalog canonicalEntities)
    {
        _db = db;
        _canonicalEntities = canonicalEntities;
    }

    public async Task<IReadOnlyList<Guid>> SubjectKeysAsync(
        RelationshipJoinPlanDto plan,
        DeclaredDimension declared,
        Type subjectEntityType,
        string value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(subjectEntityType);

        if (!plan.Planned || plan.Steps.Count == 0)
        {
            throw new RelationshipPlanExecutionException(null, null,
                "A refused plan carries no steps and must never reach execution.");
        }

        var names = new CanonicalRelationalPath(_db, _canonicalEntities);

        var sourceType = declared.SourceEntityType!;
        var sourceName = _canonicalEntities.NameOf(sourceType);

        if (!string.Equals(plan.FromEntity, sourceName, StringComparison.Ordinal))
        {
            throw new RelationshipPlanExecutionException(plan.Steps[0].RelationshipCode, null,
                "The plan starts at '" + plan.FromEntity + "' but declaration '" + declared.Code +
                "' lives on '" + (sourceName ?? sourceType.Name) + "'.");
        }

        // entities[0] is where the declaration lives; entities[n] must be the population.
        var entities = new IEntityType[plan.Steps.Count + 1];
        entities[0] = names.RequireMapped(sourceType, plan.Steps[0].RelationshipCode);
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            entities[i + 1] = names.RequireMapped(
                names.RequireEntity(step.ToEntity, step.RelationshipCode), step.RelationshipCode);
        }

        var subject = entities[entities.Length - 1];
        if (subject.ClrType != subjectEntityType)
        {
            var last = plan.Steps[plan.Steps.Count - 1];
            throw new RelationshipPlanExecutionException(last.RelationshipCode, null,
                "The plan ends at '" + last.ToEntity + "' but the population is '" +
                (_canonicalEntities.NameOf(subjectEntityType) ?? subjectEntityType.Name) + "'.");
        }

        var declaredMember = names.Member(entities[0], declared.SourceField, null);
        if ((Nullable.GetUnderlyingType(declaredMember.ClrType) ?? declaredMember.ClrType) != typeof(string))
        {
            throw new RelationshipPlanExecutionException(null, declared.SourceField,
                "Declared dimension '" + declared.Code + "' binds to a member that is not a text scalar.");
        }

        // Innermost condition: the declaration's own row carries the value.
        var current = "e0." + CanonicalRelationalPath.Quote(declaredMember.Column) + " = {0}";

        // One EXISTS per hop. Step i selects FROM entities[i] and correlates to the
        // alias of entities[i+1], so each level closes over the one outside it and the
        // outermost correlates to the population itself.
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            var fromAlias = "e" + i;
            var toAlias = i == plan.Steps.Count - 1 ? "s" : "e" + (i + 1);

            current = new StringBuilder()
                .Append("EXISTS (SELECT 1 FROM ")
                .Append(names.Table(entities[i], step.RelationshipCode)).Append(' ').Append(fromAlias)
                .Append(" WHERE ")
                .Append(names.MembersEqual(step, entities[i], fromAlias, entities[i + 1], toAlias))
                .Append(names.LiveOnly(entities[i], fromAlias, step.RelationshipCode))
                .Append(" AND ").Append(current).Append(')')
                .ToString();
        }

        var sql = "SELECT DISTINCT s." + CanonicalRelationalPath.Quote(names.KeyColumn(subject, null)) +
                  " AS \"Value\" FROM " + names.Table(subject, null) + " s WHERE " + current +
                  names.LiveOnly(subject, "s", null);

        return await _db.Database
            .SqlQueryRaw<Guid>(sql, value ?? string.Empty)
            .ToListAsync(cancellationToken);
    }
}
