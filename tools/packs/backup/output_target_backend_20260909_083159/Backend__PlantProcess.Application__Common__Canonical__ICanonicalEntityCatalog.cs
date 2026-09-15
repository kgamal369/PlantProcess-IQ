using System;

namespace PlantProcess.Application.Common.Canonical;

/// <summary>
/// The one place a canonical entity's NAME and its mapped CLR type are the same fact.
///
/// Three parts of the product had grown their own way of saying which entity they mean.
/// A declared dimension carries a CLR type resolved from its published catalogue. A
/// query population carries a CLR type. A published relationship carries left_entity and
/// right_entity as canonical entity names. Those are three vocabularies for one idea,
/// and while they stayed separate no consumer could ask the relationship model a
/// question about the entities it actually had in its hands.
///
/// This is the structural adapter between them, and structural is the whole point. It
/// holds no list of entities, no industry noun and no per-tenant vocabulary. The mapped
/// model is the source: a type that the model maps has a canonical name, and a type it
/// does not map has none. A name this catalogue cannot produce is refused, never
/// invented - inventing one would put a plant's vocabulary inside the product, which is
/// the thing the whole design exists to prevent.
/// </summary>
public interface ICanonicalEntityCatalog
{
    /// <summary>
    /// The canonical entity name for a mapped CLR entity type, or null when the type is
    /// not a mapped canonical entity. Null is an answer: the caller refuses on it.
    /// </summary>
    string? NameOf(Type mappedEntityType);

    /// <summary>
    /// The mapped CLR entity type for a canonical entity name, or null when no mapped
    /// entity carries that name. Used to walk a governed path whose steps name entities
    /// rather than types.
    /// </summary>
    Type? FindType(string canonicalEntityName);

    /// <summary>
    /// The name of the single primary-key member of a mapped entity, or null when the
    /// entity has no single-member key. A composite key is not an error here; it is a
    /// shape this catalogue cannot summarise, and the caller decides what that means.
    /// </summary>
    string? PrimaryKeyMemberOf(Type mappedEntityType);
}
