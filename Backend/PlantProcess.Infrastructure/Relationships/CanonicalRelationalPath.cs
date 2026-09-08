using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PlantProcess.Application.Common.Canonical;
using PlantProcess.Application.Relationships;

namespace PlantProcess.Infrastructure.Relationships;

/// <summary>
/// Turns a governed relationship path into the table and column names the database
/// actually has, and nothing else.
///
/// It exists because the obvious approach did not survive contact with the provider.
/// Composing the path as LINQ meant naming members dynamically through EF.Property,
/// and a dynamic property access on an OUTER lambda parameter has no translation - the
/// provider rewrites the outer reference and the expression stops being a correlated
/// subquery it can render. The alternative it offers is client evaluation, which would
/// pull whole canonical populations into memory to compare two columns.
///
/// So the path is rendered as SQL. The identifiers come from EF's own relational
/// metadata rather than from a convention guessed here, which keeps one mapping
/// authority, and every value travels as a parameter. A member the model does not map
/// is refused by name; no substitute is ever sought, because looking for one would be
/// this code choosing a path.
/// </summary>
internal sealed class CanonicalRelationalPath
{
    private readonly DbContext _db;
    private readonly ICanonicalEntityCatalog _canonicalEntities;

    public CanonicalRelationalPath(DbContext db, ICanonicalEntityCatalog canonicalEntities)
    {
        _db = db;
        _canonicalEntities = canonicalEntities;
    }

    public static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    public Type RequireEntity(string canonicalEntityName, string? relationshipCode) =>
        _canonicalEntities.FindType(canonicalEntityName)
        ?? throw new RelationshipPlanExecutionException(relationshipCode, null,
            "The plan names '" + canonicalEntityName + "', which the canonical model does not map.");

    public IEntityType RequireMapped(Type clrType, string? relationshipCode) =>
        _db.Model.FindEntityType(clrType)
        ?? throw new RelationshipPlanExecutionException(relationshipCode, null,
            "'" + clrType.Name + "' is not a mapped canonical entity.");

    /// <summary>Schema-qualified table for a mapped entity.</summary>
    public string Table(IEntityType entity, string? relationshipCode)
    {
        var table = entity.GetTableName()
            ?? throw new RelationshipPlanExecutionException(relationshipCode, null,
                "'" + entity.ClrType.Name + "' maps to no table.");

        var schema = entity.GetSchema();
        return string.IsNullOrWhiteSpace(schema) ? Quote(table) : Quote(schema) + "." + Quote(table);
    }

    /// <summary>
    /// The column a declared member maps to, and its CLR type. A shadow member or an
    /// unmapped one is a mapping defect between the published relationship and the
    /// canonical model, reported as itself.
    /// </summary>
    public (string Column, Type ClrType) Member(IEntityType entity, string member, string? relationshipCode)
    {
        var property = entity.FindProperty(member);

        if (property is null || property.IsShadowProperty())
        {
            throw new RelationshipPlanExecutionException(relationshipCode, member,
                "Member '" + member + "' is not a mapped member of '" + entity.ClrType.Name +
                "'. No substitute is sought.");
        }

        var table = entity.GetTableName();
        var schema = entity.GetSchema();
        var column = table is null
            ? property.Name
            : property.GetColumnName(StoreObjectIdentifier.Table(table, schema)) ?? property.Name;

        return (column, property.ClrType);
    }

    /// <summary>Single-member key column of a mapped entity, for addressing a subject.</summary>
    public string KeyColumn(IEntityType entity, string? relationshipCode)
    {
        var key = entity.FindPrimaryKey();
        if (key is null || key.Properties.Count != 1)
        {
            throw new RelationshipPlanExecutionException(relationshipCode, null,
                "'" + entity.ClrType.Name + "' has no single-member key, so a path cannot address it.");
        }

        return Member(entity, key.Properties[0].Name, relationshipCode).Column;
    }

    /// <summary>
    /// Every declared member of one hop, AND-ed in declared order. A composite key with
    /// one member dropped is not a narrower join; it is a different one that still
    /// returns rows, so all of them are rendered or none is.
    /// </summary>
    public string MembersEqual(
        RelationshipJoinStepDto step,
        IEntityType fromEntity, string fromAlias,
        IEntityType toEntity, string toAlias)
    {
        var parts = new List<string>();

        foreach (var pair in step.Predicates.OrderBy(p => p.Order))
        {
            if (!string.Equals(pair.Comparison, "=", StringComparison.Ordinal))
            {
                throw new RelationshipPlanExecutionException(step.RelationshipCode, pair.FromColumn,
                    "Relationship '" + step.RelationshipCode + "' compares with '" + pair.Comparison +
                    "'. The canonical executor runs equality members only.");
            }

            var left = Member(fromEntity, pair.FromColumn, step.RelationshipCode);
            var right = Member(toEntity, pair.ToColumn, step.RelationshipCode);

            var leftUnderlying = Nullable.GetUnderlyingType(left.ClrType) ?? left.ClrType;
            var rightUnderlying = Nullable.GetUnderlyingType(right.ClrType) ?? right.ClrType;

            // Two different underlying types are not coerced. A silent conversion would
            // produce a comparison the plant never declared.
            if (leftUnderlying != rightUnderlying)
            {
                throw new RelationshipPlanExecutionException(step.RelationshipCode, pair.FromColumn,
                    "Relationship '" + step.RelationshipCode + "' compares " + leftUnderlying.Name +
                    " with " + rightUnderlying.Name + ". No conversion is invented.");
            }

            parts.Add(fromAlias + "." + Quote(left.Column) + " = " + toAlias + "." + Quote(right.Column));
        }

        if (parts.Count == 0)
        {
            throw new RelationshipPlanExecutionException(step.RelationshipCode, null,
                "Relationship '" + step.RelationshipCode + "' carries no members. A join with no members joins everything.");
        }

        return string.Join(" AND ", parts);
    }

    /// <summary>
    /// Soft-deleted rows are excluded where the entity carries that concept, so evidence
    /// and execution see the same population the rest of the product sees.
    /// </summary>
    public string LiveOnly(IEntityType entity, string alias, string? relationshipCode)
    {
        var deleted = entity.FindProperty("IsDeleted");
        if (deleted is null || deleted.IsShadowProperty()) return string.Empty;

        var column = Member(entity, "IsDeleted", relationshipCode).Column;
        return " AND " + alias + "." + Quote(column) + " = FALSE";
    }
}
