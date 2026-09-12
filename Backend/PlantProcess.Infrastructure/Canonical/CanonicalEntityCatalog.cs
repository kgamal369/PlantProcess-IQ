using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Canonical;
using PlantProcess.Domain.Common;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Canonical;

/// <summary>
/// Canonical entity identity read from the mapped model.
///
/// There is deliberately no dictionary here. A dictionary would have to be written by
/// someone, would name a plant's entities in product source, and would drift from the
/// model the moment either changed. Instead the model answers: the entities it maps are
/// the canonical entities, and the name of one is the name of its CLR type.
///
/// That convention is not invented here either. The declared-dimension catalogue already
/// accepts a published sourceCatalog only when it matches a mapped entity's type name,
/// so the convention was already load-bearing across the product - it simply had no
/// single owner. This is that owner.
///
/// Names are matched ordinally and case-sensitively. A published relationship endpoint
/// that differs from the mapped name by case is a different name, and quietly accepting
/// it would mean two spellings could resolve to one entity while the plant believes it
/// declared one.
/// </summary>
public sealed class CanonicalEntityCatalog : ICanonicalEntityCatalog
{
    private readonly Dictionary<Type, string> _nameByType;
    private readonly Dictionary<string, Type> _typeByName;
    private readonly Dictionary<Type, string?> _keyByType;

    /// <summary>
    /// T-262. Fields per projection target, built once with the rest of the model read.
    /// Only projection targets get an entry: asking for the fields of something that is
    /// not a legal target should answer nothing rather than answer usefully.
    /// </summary>
    private readonly Dictionary<string, IReadOnlyList<CanonicalProjectionField>> _fieldsByTarget;

    /// <summary>
    /// T-253. The subset that declares itself an authoring output target. Built here
    /// from the same enumeration, so it can never name an entity the model does not map
    /// and can never omit one that the model maps and the Domain marks.
    /// </summary>
    private readonly SortedSet<string> _projectionTargets;

    public CanonicalEntityCatalog(PlantProcessDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        _nameByType = new Dictionary<Type, string>();
        _typeByName = new Dictionary<string, Type>(StringComparer.Ordinal);
        _keyByType = new Dictionary<Type, string?>();
        _projectionTargets = new SortedSet<string>(StringComparer.Ordinal);
        _fieldsByTarget = new Dictionary<string, IReadOnlyList<CanonicalProjectionField>>(StringComparer.Ordinal);

        foreach (var entity in db.Model.GetEntityTypes())
        {
            // Owned and shared-type entities are not addressable subjects of a
            // relationship: they have no independent identity to join to. Skipping them
            // keeps the namespace exactly the set of things a path may end at.
            if (entity.IsOwned()) continue;
            if (entity.HasSharedClrType) continue;

            var clrType = entity.ClrType;
            if (clrType is null) continue;

            var name = clrType.Name;

            // A name claimed by two mapped types is a name that cannot identify either.
            // Both are dropped rather than one silently winning by enumeration order.
            if (_typeByName.TryGetValue(name, out var existing))
            {
                if (existing != clrType)
                {
                    _typeByName.Remove(name);
                    _nameByType.Remove(existing);
                    _nameByType.Remove(clrType);
                }
                continue;
            }

            _nameByType[clrType] = name;
            _typeByName[name] = clrType;

            // T-253. Declared by the Domain, read here. A mapped entity is NOT a
            // projection target unless it says so, so adding an entity to the model
            // does not silently widen what an author may write into.
            if (typeof(ICanonicalProjectionTarget).IsAssignableFrom(clrType))
            {
                _projectionTargets.Add(name);

                // T-262. Read here, from the same entity the name came from, so the
                // fields offered to an author and the fields a server validates
                // against are one answer rather than two that agree today.
                var fields = new List<CanonicalProjectionField>();
                foreach (var property in entity.GetProperties())
                {
                    // Shadow properties have no CLR member and therefore no declaring
                    // type to judge; they are infrastructure by definition.
                    var member = property.PropertyInfo;
                    bool systemOwned = member is null
                        || member.DeclaringType == typeof(BaseEntity);

                    fields.Add(new CanonicalProjectionField(
                        property.Name,
                        property.ClrType.Name,
                        !property.IsNullable,
                        systemOwned));
                }

                _fieldsByTarget[name] = fields
                    .OrderBy(f => f.Name, StringComparer.Ordinal)
                    .ToArray();
            }

            var key = entity.FindPrimaryKey();
            _keyByType[clrType] = key is not null && key.Properties.Count == 1
                ? key.Properties[0].Name
                : null;
        }
    }

    /// <summary>
    /// T-253. Ordinal ordering, because names are matched ordinally everywhere else in
    /// this class and a picker whose order changed with the host culture would be a
    /// different list on a different machine.
    /// </summary>
    public IReadOnlyList<string> ProjectionTargetNames() => _projectionTargets.ToArray();

    public bool IsProjectionTarget(string canonicalEntityName) =>
        !string.IsNullOrWhiteSpace(canonicalEntityName)
        && _projectionTargets.Contains(canonicalEntityName.Trim());

    public string? NameOf(Type mappedEntityType)
    {
        if (mappedEntityType is null) return null;
        return _nameByType.TryGetValue(mappedEntityType, out var name) ? name : null;
    }

    public Type? FindType(string canonicalEntityName)
    {
        if (string.IsNullOrWhiteSpace(canonicalEntityName)) return null;
        return _typeByName.TryGetValue(canonicalEntityName, out var type) ? type : null;
    }

    public IReadOnlyList<CanonicalProjectionField> ProjectionFieldsOf(string canonicalEntityName)
    {
        if (string.IsNullOrWhiteSpace(canonicalEntityName))
        {
            return Array.Empty<CanonicalProjectionField>();
        }

        return _fieldsByTarget.TryGetValue(canonicalEntityName.Trim(), out var fields)
            ? fields
            : Array.Empty<CanonicalProjectionField>();
    }

    public string? PrimaryKeyMemberOf(Type mappedEntityType)
    {
        if (mappedEntityType is null) return null;
        return _keyByType.TryGetValue(mappedEntityType, out var key) ? key : null;
    }
}
