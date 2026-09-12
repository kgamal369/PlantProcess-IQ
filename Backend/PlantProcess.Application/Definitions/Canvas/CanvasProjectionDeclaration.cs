using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlantProcess.Application.Definitions.Canvas;

/// <summary>
/// PPIQ T-262. WHAT A TRANSFORMATION WRITES, AND WHERE EACH VALUE COMES FROM.
///
/// outputTarget said WHERE a definition writes. It never said WHAT, and between the two
/// sat an assumption nobody had written down. An executor that filled it in would have
/// been choosing the product's projection semantics inside a job.
///
/// The declaration names one target entity and, for each canonical business field it
/// writes, exactly which authored output supplies it. Nothing is matched by position,
/// by type or by resembling a name. A surface may SUGGEST an identical name; only an
/// accepted binding is persisted, because a suggestion nobody looked at is not a
/// decision anybody made.
///
/// IDENTITY, PROVENANCE AND REPROCESSING ARE ABSENT ON PURPOSE. They are PPIQ
/// invariants owned by the runtime. A declaration able to set them would let one
/// definition write rows another definition could never reconcile with.
///
/// BINDINGS ARE SORTED BEFORE SERIALISATION. The content is hashed, so an author who
/// bound three fields in a different order would otherwise create a new immutable
/// version that means exactly what the old one meant. Order carries no meaning here -
/// unlike a graph's joins, where it does - so it is normalised away.
/// </summary>
public sealed record CanvasProjectionDeclaration(
    string TargetEntity,
    IReadOnlyList<ProjectionFieldBinding> FieldBindings)
{
    public const string KindColumn = "column";
    public const string KindDerived = "derived";
    public const string KindSql = "sql";

    /// <summary>
    /// Deterministic JSON. Keys are written in a fixed order and bindings are sorted by
    /// target field, so identical semantics serialise byte-identically and a reorder is
    /// not a new version.
    /// </summary>
    public string ToJson()
    {
        var bindings = new JsonArray();
        foreach (var b in FieldBindings.OrderBy(x => x.TargetField, StringComparer.Ordinal))
        {
            var node = new JsonObject
            {
                ["targetField"] = b.TargetField,
                ["sourceKind"] = b.SourceKind,
                ["sourceField"] = b.SourceField,
            };
            if (!string.IsNullOrWhiteSpace(b.SourceTable)) { node["sourceTable"] = b.SourceTable; }
            bindings.Add(node);
        }

        var root = new JsonObject
        {
            ["targetEntity"] = TargetEntity,
            ["fieldBindings"] = bindings,
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Parses a declaration. Returns null on success and leaves the value in
    /// <paramref name="declaration"/>; returns a sentence when the payload cannot be a
    /// declaration at all. An absent payload is not an error here - the caller decides
    /// whether absence is permitted, because absence means something different on a
    /// legacy read than it does on a save.
    /// </summary>
    public static string? TryParse(string? json, out CanvasProjectionDeclaration? declaration)
    {
        declaration = null;
        if (string.IsNullOrWhiteSpace(json)) { return null; }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            return "The projection declaration is not valid JSON: " + ex.Message;
        }

        if (node is not JsonObject root)
        {
            return "The projection declaration must be a JSON object.";
        }

        var target = root["targetEntity"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(target))
        {
            return "The projection declaration names no target entity.";
        }

        var bindings = new List<ProjectionFieldBinding>();
        if (root["fieldBindings"] is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not JsonObject b) { return "Every field binding must be a JSON object."; }

                var targetField = b["targetField"]?.GetValue<string>();
                var sourceKind = b["sourceKind"]?.GetValue<string>();
                var sourceField = b["sourceField"]?.GetValue<string>();
                var sourceTable = b["sourceTable"]?.GetValue<string>();

                if (string.IsNullOrWhiteSpace(targetField))
                {
                    return "A field binding names no target field.";
                }
                if (sourceKind is not (KindColumn or KindDerived or KindSql))
                {
                    return "Binding for '" + targetField + "' declares source kind '"
                        + (sourceKind ?? "none") + "'. It must be column, derived or sql.";
                }
                if (string.IsNullOrWhiteSpace(sourceField))
                {
                    return "Binding for '" + targetField + "' names no source output.";
                }
                if (sourceKind == KindColumn && string.IsNullOrWhiteSpace(sourceTable))
                {
                    return "Binding for '" + targetField + "' selects a column and names no table. "
                        + "A column is identified by its table and its name, never by name alone.";
                }

                bindings.Add(new ProjectionFieldBinding(
                    targetField!.Trim(), sourceKind!, sourceTable?.Trim(), sourceField!.Trim()));
            }
        }

        if (bindings.Count == 0)
        {
            return "The projection declaration binds no fields. A definition that writes nothing "
                + "is not a projection.";
        }

        declaration = new CanvasProjectionDeclaration(target!.Trim(), bindings);
        return null;
    }

    /// <summary>
    /// The queryable projection written into transformation_details.target_entities.
    ///
    /// It is DERIVED from the validated declaration in the same write, so the detail row
    /// and the hashed content cannot be authored independently and cannot disagree. For
    /// Release 1 there is exactly one target, which is why this is a one-element array
    /// rather than a shape that invites a second.
    /// </summary>
    public static string ToDetailProjection(string? declarationJson, string outputTarget)
    {
        var error = TryParse(declarationJson, out var declaration);
        if (error is not null || declaration is null)
        {
            return "[]";
        }

        var fields = new JsonArray();
        foreach (var b in declaration.FieldBindings.OrderBy(x => x.TargetField, StringComparer.Ordinal))
        {
            fields.Add(new JsonObject
            {
                ["targetField"] = b.TargetField,
                ["sourceKind"] = b.SourceKind,
                ["sourceTable"] = b.SourceTable,
                ["sourceField"] = b.SourceField,
            });
        }

        var entry = new JsonObject
        {
            ["entity"] = outputTarget.Trim(),
            ["fieldBindings"] = fields,
        };

        return new JsonArray(entry).ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// The first SQL output name bound more than once, or null. A duplicate output name
    /// does not identify one value, so a binding to it is not a binding.
    /// </summary>
    public static string? FirstAmbiguousSource(CanvasProjectionDeclaration declaration)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in declaration.FieldBindings)
        {
            if (b.SourceKind != KindSql) { continue; }
            if (!seen.Add(b.SourceField)) { return b.SourceField; }
        }

        return null;
    }
}

/// <summary>
/// T-262. One authored binding.
///
/// SourceKind is the vocabulary of authored outputs, not of storage. A column is a
/// selected source field identified by table and column - T-033 rules that a Select
/// projects and does not rename, so the pair is the identity and no alias is added.
/// A derived output is identified by the alias its author gave it. A sql output is
/// identified by the name the validated statement returns.
/// </summary>
public sealed record ProjectionFieldBinding(
    string TargetField,
    string SourceKind,
    string? SourceTable,
    string SourceField);