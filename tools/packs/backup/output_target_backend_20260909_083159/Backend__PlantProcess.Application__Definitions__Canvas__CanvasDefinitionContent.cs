using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlantProcess.Application.Definitions.Canvas;

/// <summary>
/// PPIQ T-244. THE CANONICAL CONTENT OF A CANVAS-AUTHORED TRANSFORMATION.
///
/// A Canvas definition is one DefinitionKind.Transformation whose ContentJson
/// carries its authoring representation explicitly. Graph and authored SQL are
/// two representations of the same governed definition, never two kinds and
/// never two stores.
///
/// DETERMINISM IS THE CONTRACT. The canonical writer hashes ContentJson to
/// decide whether a save is a redeclaration or a new immutable version. This
/// type therefore serialises with sorted object keys, no whitespace and no
/// volatile field: no timestamps, no client ids, no viewport, no UI state.
/// Two saves of the same semantics produce byte-identical ContentJson.
///
/// SQL IS NOT NORMALISED HERE. The server safe-SQL authority already returns
/// normalized_sql from its own resolver; the lifecycle service stores that,
/// so the canonical hash follows the product's existing SQL authority rather
/// than a second notion of equivalence invented in this file.
/// </summary>
public static class CanvasDefinitionContent
{
    public const string RepresentationGraph = "graph";
    public const string RepresentationSql = "sql";

    private static readonly JsonSerializerOptions Compact = new()
    {
        WriteIndented = false,
    };

    /// <summary>Content for a graph-authored definition.</summary>
    public static string ForGraph(string graphJson)
    {
        var graph = ParseObject(graphJson, "graph");

        var root = new JsonObject
        {
            ["graph"] = Canonicalise(graph),
            ["representation"] = RepresentationGraph,
        };

        return Serialise(root);
    }

    /// <summary>
    /// Content for a SQL-authored definition. The forked graph, when present,
    /// travels inside the content: the acceptance line that the graph is still
    /// retrievable afterwards is satisfied by the artifact, not by memory.
    /// </summary>
    public static string ForSql(string normalisedSql, string? forkedFromGraphJson)
    {
        if (string.IsNullOrWhiteSpace(normalisedSql))
        {
            throw new ArgumentException("Authored SQL is required.", nameof(normalisedSql));
        }

        var root = new JsonObject
        {
            ["representation"] = RepresentationSql,
            ["sql"] = normalisedSql,
        };

        if (!string.IsNullOrWhiteSpace(forkedFromGraphJson))
        {
            root["forkedFromGraph"] = Canonicalise(ParseObject(forkedFromGraphJson, "forkedFromGraph"));
        }

        return Serialise(root);
    }

    /// <summary>Reads the representation discriminator back out of stored content.</summary>
    public static CanvasDefinitionRepresentation Read(string contentJson)
    {
        var root = ParseObject(contentJson, "content");
        var representation = root["representation"]?.GetValue<string>();

        return representation switch
        {
            RepresentationGraph => new CanvasDefinitionRepresentation(
                RepresentationGraph,
                GraphJson: root["graph"]?.ToJsonString(Compact),
                Sql: null,
                ForkedFromGraphJson: null),
            RepresentationSql => new CanvasDefinitionRepresentation(
                RepresentationSql,
                GraphJson: null,
                Sql: root["sql"]?.GetValue<string>(),
                ForkedFromGraphJson: root["forkedFromGraph"]?.ToJsonString(Compact)),
            _ => throw new InvalidOperationException(
                "Canonical content carries no recognised representation. It cannot be reopened as a Canvas definition."),
        };
    }

    private static JsonObject ParseObject(string json, string what)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The " + what + " payload is not valid JSON: " + ex.Message, nameof(json));
        }

        return node as JsonObject
            ?? throw new ArgumentException("The " + what + " payload must be a JSON object.", nameof(json));
    }

    /// <summary>
    /// Sorted keys, recursively. Arrays keep their order because order is
    /// semantic for a graph (join order, filter order, projection order).
    /// </summary>
    private static JsonNode? Canonicalise(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var sorted = new JsonObject();
                foreach (var key in obj.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal))
                {
                    sorted[key] = Canonicalise(obj[key]?.DeepClone());
                }

                return sorted;
            }

            case JsonArray array:
            {
                var copy = new JsonArray();
                foreach (var item in array)
                {
                    copy.Add(Canonicalise(item?.DeepClone()));
                }

                return copy;
            }

            default:
                return node?.DeepClone();
        }
    }

    private static string Serialise(JsonObject root) => Canonicalise(root)!.ToJsonString(Compact);
}

/// <summary>What a stored Canvas definition says it is.</summary>
public sealed record CanvasDefinitionRepresentation(
    string Representation,
    string? GraphJson,
    string? Sql,
    string? ForkedFromGraphJson);
