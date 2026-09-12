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

    /// <summary>
    /// T-253. THE GOVERNED OUTPUT TARGET, AT THE ROOT OF THE CONTENT.
    ///
    /// It sits beside the representation rather than inside it, so one reader answers
    /// "what does this definition write to" without first asking whether it is a graph
    /// or a statement. It is hashed with everything else: a definition that changes its
    /// output identity IS a different definition and the writer has to see that.
    ///
    /// The value is a canonical entity name that declares itself a projection target.
    /// Never a physical relation, never a customer table, never page context.
    /// </summary>
    public const string RootOutputTarget = "outputTarget";

    /// <summary>
    /// The pre-T-253 graph-local field. Retained because T-242 history is full of it and
    /// those versions must stay reopenable. It is no longer the authority.
    /// </summary>
    public const string GraphTargetEntity = "targetEntity";

    /// <summary>
    /// T-243. THE AUTHORED BOARD, BESIDE THE COMPILED GRAPH.
    ///
    /// Until now a saved definition kept only what it COMPILED TO: tables, joins,
    /// filters, derived columns, a projection. That is enough to run and nowhere near
    /// enough to reopen. The blocks a person dragged out, where they put them, how they
    /// wired them and which purpose they were authoring under all existed in the browser
    /// and nowhere else, so closing the tab destroyed the document and left the query.
    ///
    /// The board therefore travels at the ROOT of the content, as a sibling of graph
    /// rather than a field inside it. The graph stays exactly what the execution path
    /// consumes; the board is what a person edits. One definition, two faces, one store.
    ///
    /// IT IS HASHED, like everything else here, and that has a consequence worth stating
    /// rather than discovering: moving a block and saving produces a NEW IMMUTABLE
    /// VERSION. Position is part of what was authored, the writer hashes the whole
    /// content, and there is no unhashed sibling to hide layout in. Saving an untouched
    /// board still reuses its version, because the bytes are identical.
    /// </summary>
    public const string RootBoard = "board";

    /// <summary>
    /// T-262. THE GOVERNED PROJECTION DECLARATION.
    ///
    /// outputTarget said WHERE a definition writes. It never said WHAT. Between the two
    /// sat an assumption nobody had written down, and an executor that filled it in
    /// would have been choosing the product's projection semantics inside a job.
    ///
    /// The declaration names the target entity and, for each canonical business field
    /// it writes, exactly where the value comes from. Nothing is matched by position,
    /// by type or by resembling a name. A surface may SUGGEST an identical name; only
    /// an accepted binding is persisted, because a suggestion the author never looked
    /// at is not a decision they made.
    ///
    /// It is representation-independent and it is HASHED. Changing a binding changes
    /// what the definition means, so it is a new immutable version - the same law that
    /// already governs outputTarget and board.
    ///
    /// Identity, provenance and reprocessing are absent on purpose. They are PPIQ
    /// invariants, not author choices, and a declaration that could set them would let
    /// one definition write rows another definition could never reconcile with.
    /// </summary>
    public const string RootProjection = "projection";

    private static readonly JsonSerializerOptions Compact = new()
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Content for a graph-authored definition.
    ///
    /// T-253. The root output target is REQUIRED, and where the graph still carries its
    /// own targetEntity the two must agree EXACTLY. Disagreement is refused rather than
    /// resolved by precedence: a rule that silently preferred one of two contradicting
    /// identities would make the losing one a lie the store then keeps forever.
    /// </summary>
    public static string ForGraph(string graphJson, string outputTarget)
    {
        if (string.IsNullOrWhiteSpace(outputTarget))
        {
            throw new ArgumentException(
                "A governed output target is required. It is never defaulted.", nameof(outputTarget));
        }

        var graph = ParseObject(graphJson, "graph");
        var declared = TrimmedOrNull(graph[GraphTargetEntity]);
        var target = outputTarget.Trim();

        // T-243. The browser sends the board INSIDE the graph payload, because that is
        // the one blob the existing session draft carries and this task introduces no
        // second transport. It is lifted out here so the stored graph stays the clean
        // execution shape the SQL generator reads, and the board stands on its own.
        JsonNode? board = null;
        if (graph[RootBoard] is JsonNode authored)
        {
            board = authored.DeepClone();
            graph.Remove(RootBoard);
        }

        if (declared is not null && !string.Equals(declared, target, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The graph names '" + declared + "' as its target entity while the governed output target is '"
                + target + "'. One definition cannot carry two output identities.",
                nameof(outputTarget));
        }

        // T-262. The declaration rides inside the graph payload for the same reason the
        // board does: the session draft is one blob and this task introduces no second
        // transport. It is lifted to the root so the stored graph stays the clean
        // execution shape, and the declaration stands beside it as its own fact.
        JsonNode? projection = null;
        if (graph[RootProjection] is JsonNode declared2)
        {
            projection = declared2.DeepClone();
            graph.Remove(RootProjection);
        }

        var root = new JsonObject
        {
            ["graph"] = Canonicalise(graph),
            [RootOutputTarget] = target,
            ["representation"] = RepresentationGraph,
        };

        if (board is not null)
        {
            root[RootBoard] = Canonicalise(board);
        }

        if (projection is not null)
        {
            root[RootProjection] = Canonicalise(projection);
        }

        return Serialise(root);
    }

    /// <summary>
    /// Content for a SQL-authored definition. The forked graph, when present,
    /// travels inside the content: the acceptance line that the graph is still
    /// retrievable afterwards is satisfied by the artifact, not by memory.
    /// </summary>
    public static string ForSql(
        string normalisedSql,
        string? forkedFromGraphJson,
        string outputTarget,
        string? projectionDeclarationJson = null)
    {
        if (string.IsNullOrWhiteSpace(normalisedSql))
        {
            throw new ArgumentException("Authored SQL is required.", nameof(normalisedSql));
        }

        // T-253. Before this task the SQL target reached the store only as a projection
        // handle, which the lifecycle contract itself states is outside the hash. It
        // could not survive a version and reopen could not return it. It is canonical
        // content now.
        if (string.IsNullOrWhiteSpace(outputTarget))
        {
            throw new ArgumentException(
                "A governed output target is required. It is never defaulted.", nameof(outputTarget));
        }

        var root = new JsonObject
        {
            [RootOutputTarget] = outputTarget.Trim(),
            ["representation"] = RepresentationSql,
            ["sql"] = normalisedSql,
        };

        if (!string.IsNullOrWhiteSpace(forkedFromGraphJson))
        {
            root["forkedFromGraph"] = Canonicalise(ParseObject(forkedFromGraphJson, "forkedFromGraph"));
        }

        // T-262. Both representations carry the SAME declaration semantics. A definition
        // forked from blocks to SQL keeps what it writes and where each value comes
        // from; only the way the value is produced changed.
        if (!string.IsNullOrWhiteSpace(projectionDeclarationJson))
        {
            root[RootProjection] = Canonicalise(
                ParseObject(projectionDeclarationJson!, "projection"));
        }

        return Serialise(root);
    }

    /// <summary>Reads the representation discriminator back out of stored content.</summary>
    public static CanvasDefinitionRepresentation Read(string contentJson)
    {
        var root = ParseObject(contentJson, "content");
        var representation = root["representation"]?.GetValue<string>();

        var storedTarget = TrimmedOrNull(root[RootOutputTarget]);

        // T-262. Null for every version written before this task. That is not a defect
        // and no migration invents one: such a version reopens exactly as it was saved
        // and is refused governed execution until an author saves a new version that
        // declares what it writes.
        var storedProjection = root[RootProjection]?.ToJsonString(Compact);

        return representation switch
        {
            // T-253 LEGACY GRAPH. Content written before this task has no root target but
            // does carry graph.targetEntity, which the author chose. Reading it back is
            // recovery of a stated fact, not fabrication, and the old version is never
            // rewritten - re-saving produces a new immutable version, which is correct.
            // T-243. BoardJson is null for every version saved before this task. That is
            // not a defect and it is not repaired by inventing a layout: such a version
            // reopens as the query it always was, and the surface says so.
            RepresentationGraph => new CanvasDefinitionRepresentation(
                RepresentationGraph,
                GraphJson: root["graph"]?.ToJsonString(Compact),
                Sql: null,
                ForkedFromGraphJson: null,
                OutputTarget: storedTarget ?? TrimmedOrNull(root["graph"]?[GraphTargetEntity]),
                BoardJson: root[RootBoard]?.ToJsonString(Compact),
                ProjectionJson: storedProjection),

            // T-253 LEGACY SQL. There is nothing authored to recover: the old target was
            // a projection handle, and a handle is not identity. It reopens as null and
            // the author states a target before the next save. Nothing is taken from the
            // projection, the page, a material default or a physical relation.
            RepresentationSql => new CanvasDefinitionRepresentation(
                RepresentationSql,
                GraphJson: null,
                Sql: root["sql"]?.GetValue<string>(),
                ForkedFromGraphJson: root["forkedFromGraph"]?.ToJsonString(Compact),
                OutputTarget: storedTarget,
                BoardJson: null,
                ProjectionJson: storedProjection),

            _ => throw new InvalidOperationException(
                "Canonical content carries no recognised representation. It cannot be reopened as a Canvas definition."),
        };
    }

    /// <summary>
    /// T-253. The one place a stored target string becomes a value or a null. An empty
    /// or whitespace string is NOT a target named "", it is the absence of one, and the
    /// pre-T-253 graph fixtures in this repository contain exactly that.
    /// </summary>
    public static string? TrimmedOrNull(JsonNode? node)
    {
        if (node is null) { return null; }

        string? raw;
        try
        {
            raw = node.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            // A non-string in a name position is not a name. The caller refuses on null
            // rather than stringifying whatever happened to be there.
            return null;
        }
        catch (FormatException)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    /// <summary>
    /// T-253. The target entity a graph payload declares, or null. A caller holding a
    /// graph but not yet its content uses this to carry the AUTHORED value forward
    /// explicitly, instead of letting a default appear somewhere downstream.
    /// </summary>
    public static string? TryReadGraphTargetEntity(string graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson)) { return null; }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(graphJson);
        }
        catch (JsonException)
        {
            return null;
        }

        return node is JsonObject obj ? TrimmedOrNull(obj[GraphTargetEntity]) : null;
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

/// <summary>
/// What a stored Canvas definition says it is.
///
/// T-253. OutputTarget is representation-independent on purpose: a consumer asking what
/// this definition writes to gets one answer whether it was authored as blocks or as a
/// statement. Null means the stored content declares none, which is a legacy SQL
/// definition and not a licence to guess.
/// </summary>
public sealed record CanvasDefinitionRepresentation(
    string Representation,
    string? GraphJson,
    string? Sql,
    string? ForkedFromGraphJson,
    string? OutputTarget,
    string? BoardJson = null,
    // T-262. Null means the version declares no projection. It is readable, reopenable
    // and never rewritten; it is simply not executable as a governed projection.
    string? ProjectionJson = null);
