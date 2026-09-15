using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Canonical;
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

    public CanonicalEntityCatalog(PlantProcessDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        _nameByType = new Dictionary<Type, string>();
        _typeByName = new Dictionary<string, Type>(StringComparer.Ordinal);
        _keyByType = new Dictionary<Type, string?>();

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

            var key = entity.FindPrimaryKey();
            _keyByType[clrType] = key is not null && key.Properties.Count == 1
                ? key.Properties[0].Name
                : null;
        }
    }

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

    public string? PrimaryKeyMemberOf(Type mappedEntityType)
    {
        if (mappedEntityType is null) return null;
        return _keyByType.TryGetValue(mappedEntityType, out var key) ? key : null;
    }
}
