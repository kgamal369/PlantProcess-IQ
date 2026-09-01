using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PlantProcess.Application.Definitions;

namespace PlantProcess.Infrastructure.Definitions;

/// <summary>
/// PPIQ T-091. Canonical serialization and semantic equality for portable artifacts.
///
/// WHY A HAND-WRITTEN CANONICAL FORM RATHER THAN JsonSerializer DEFAULTS.
/// Determinism here is a product requirement, not a formatting preference: the
/// official acceptance is export -> clean database -> import -> export, and the
/// two artifacts must compare equal. Default serialization orders properties by
/// reflection order and preserves whatever key order the source JSON happened
/// to have, so two semantically identical exports can differ byte for byte. The
/// canonical writer below fixes property order, sorts every collection by a
/// stated key, and canonicalises nested JSON content recursively.
///
/// WHAT IS DELIBERATELY EXCLUDED FROM SEMANTIC EQUALITY. Export timestamp,
/// source environment and exporting user. They are honest provenance and they
/// are not part of what a definition MEANS. Including them would make the
/// round-trip gate fail for a reason that has nothing to do with portability,
/// and the usual repair for that is to weaken the comparison - which would
/// remove the only proof the artifact is portable at all.
/// </summary>
public static class DefinitionArtifactCanonicalizer
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        SkipValidation = false,
    };

    /// <summary>
    /// The semantic section of the artifact, canonically ordered. This string
    /// is the unit of comparison for the round-trip gate and the input to the
    /// semantic hash. Metadata never reaches it.
    /// </summary>
    public static string ToCanonicalJson(DefinitionArtifact artifact)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", artifact.FormatVersion);
            writer.WriteString("rootRef", artifact.RootRef);

            writer.WritePropertyName("definitions");
            writer.WriteStartArray();
            foreach (var definition in artifact.Definitions.OrderBy(d => d.Ref, StringComparer.Ordinal))
            {
                WriteDefinition(writer, definition);
            }

            writer.WriteEndArray();

            writer.WritePropertyName("dependencies");
            writer.WriteStartArray();
            foreach (var dependency in artifact.Dependencies
                         .OrderBy(d => d.FromRef, StringComparer.Ordinal)
                         .ThenBy(d => d.ToRef, StringComparer.Ordinal)
                         .ThenBy(d => d.DependencyKind, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("fromRef", dependency.FromRef);
                writer.WriteString("toRef", dependency.ToRef);
                writer.WriteString("dependencyKind", dependency.DependencyKind);
                writer.WriteBoolean("isRequired", dependency.IsRequired);
                if (dependency.DependsOnVersion.HasValue)
                {
                    writer.WriteNumber("dependsOnVersion", dependency.DependsOnVersion.Value);
                }
                else
                {
                    writer.WriteNull("dependsOnVersion");
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>SHA-256 over the canonical semantic section. Provenance-free by construction.</summary>
    public static string SemanticHash(DefinitionArtifact artifact)
    {
        var bytes = Encoding.UTF8.GetBytes(ToCanonicalJson(artifact));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    /// <summary>
    /// Semantic equality between two artifacts, which is what the round-trip
    /// gate asserts. Physical row identity is not consulted, because the clean
    /// database will legitimately allocate different uuids.
    /// </summary>
    public static bool SemanticallyEqual(DefinitionArtifact left, DefinitionArtifact right) =>
        string.Equals(ToCanonicalJson(left), ToCanonicalJson(right), StringComparison.Ordinal);

    /// <summary>
    /// The full package for transport, provenance included. Only this form is
    /// written to a file or returned over HTTP; the canonical form above is the
    /// comparison unit, not the wire format.
    /// </summary>
    public static string ToTransportJson(DefinitionArtifact artifact)
    {
        var document = JsonNode.Parse(ToCanonicalJson(artifact))!.AsObject();

        // Derived integrity and provenance ride alongside each definition in
        // the transport form only. They are read back on import, never hashed.
        var byRef = artifact.Definitions.ToDictionary(d => d.Ref, StringComparer.Ordinal);
        foreach (var node in document["definitions"]!.AsArray())
        {
            var entry = node!.AsObject();
            var definition = byRef[entry["ref"]!.GetValue<string>()];
            entry["integrity"] = new JsonObject
            {
                ["definitionHash"] = definition.DefinitionHash,
                ["sourceDefinitionId"] = definition.SourceDefinitionId?.ToString(),
                ["sourceVersionId"] = definition.SourceVersionId?.ToString(),
            };
        }

        if (artifact.Metadata is not null)
        {
            document["metadata"] = new JsonObject
            {
                ["exportedAtUtc"] = artifact.Metadata.ExportedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                ["sourceEnvironment"] = artifact.Metadata.SourceEnvironment,
                ["exportedBy"] = artifact.Metadata.ExportedBy,
            };
        }

        document["semanticHash"] = SemanticHash(artifact);
        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Reads a transport package back. Structural problems are reported by
    /// returning null so the importer can raise a typed conflict; this method
    /// does not throw its way out of a malformed file.
    /// </summary>
    public static DefinitionArtifact? FromTransportJson(string json)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return null; }

        if (root is not JsonObject document) { return null; }
        if (document["formatVersion"] is null || document["rootRef"] is null) { return null; }

        var definitions = new List<ArtifactDefinition>();
        foreach (var node in document["definitions"]?.AsArray() ?? new JsonArray())
        {
            if (node is not JsonObject entry) { return null; }

            var detail = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (entry["detail"] is JsonObject detailObject)
            {
                foreach (var pair in detailObject)
                {
                    detail[pair.Key] = pair.Value is null ? null : pair.Value.ToJsonString().Trim('"');
                }
            }

            var integrity = entry["integrity"] as JsonObject ?? new JsonObject();

            var outcomes = new List<ArtifactOutcome>();
            foreach (var outcomeNode in entry["outcomes"]?.AsArray() ?? new JsonArray())
            {
                if (outcomeNode is not JsonObject outcome) { return null; }
                outcomes.Add(new ArtifactOutcome(
                    Text(outcome, "outcomeCode") ?? string.Empty,
                    Text(outcome, "outcomeType") ?? string.Empty,
                    Text(outcome, "classTaxonomyRef"),
                    Text(outcome, "ordinalRankMapJson"),
                    Text(outcome, "grainCode") ?? string.Empty,
                    Text(outcome, "detectionPositionCode") ?? string.Empty,
                    Text(outcome, "detectionTimestampField") ?? string.Empty,
                    Text(outcome, "direction") ?? string.Empty,
                    Text(outcome, "unitCode"),
                    Text(outcome, "censoringPolicy") ?? string.Empty));
            }

            definitions.Add(new ArtifactDefinition(
                Ref: Text(entry, "ref") ?? string.Empty,
                DefinitionCode: Text(entry, "definitionCode") ?? string.Empty,
                Kind: Text(entry, "kind") ?? string.Empty,
                Surface: Text(entry, "surface") ?? string.Empty,
                Name: Text(entry, "name") ?? string.Empty,
                VersionNumber: Number(entry, "versionNumber") ?? 0,
                Status: Text(entry, "status") ?? string.Empty,
                ContentJson: entry["contentJson"]?.ToJsonString() ?? "{}",
                DefinitionHash: Text(integrity, "definitionHash") ?? Text(entry, "definitionHash") ?? string.Empty,
                Detail: detail.Count == 0 ? null : detail,
                Outcomes: outcomes.Count == 0 ? null : outcomes,
                SourceDefinitionId: Guid.TryParse(Text(integrity, "sourceDefinitionId") ?? Text(entry, "sourceDefinitionId"), out var sourceId) ? sourceId : null,
                SourceVersionId: Guid.TryParse(Text(integrity, "sourceVersionId") ?? Text(entry, "sourceVersionId"), out var versionId) ? versionId : null));
        }

        var dependencies = new List<ArtifactDependency>();
        foreach (var node in document["dependencies"]?.AsArray() ?? new JsonArray())
        {
            if (node is not JsonObject entry) { return null; }
            dependencies.Add(new ArtifactDependency(
                Text(entry, "fromRef") ?? string.Empty,
                Text(entry, "toRef") ?? string.Empty,
                Text(entry, "dependencyKind") ?? string.Empty,
                entry["isRequired"]?.GetValue<bool>() ?? true,
                Number(entry, "dependsOnVersion")));
        }

        ArtifactMetadata? metadata = null;
        if (document["metadata"] is JsonObject metadataObject)
        {
            metadata = new ArtifactMetadata(
                DateTime.TryParse(Text(metadataObject, "exportedAtUtc"), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var stamp)
                    ? stamp
                    : default,
                Text(metadataObject, "sourceEnvironment"),
                Text(metadataObject, "exportedBy"));
        }

        return new DefinitionArtifact(
            document["formatVersion"]!.GetValue<int>(),
            Text(document, "rootRef") ?? string.Empty,
            definitions,
            dependencies,
            metadata);
    }

    /// <summary>
    /// The portable semantic form of ONE definition, without its package ref.
    /// This is what the importer compares an incoming definition against when
    /// the target already holds that code: same serializer, same fields, same
    /// answer as the round-trip gate.
    /// </summary>
    public static string ToCanonicalDefinitionJson(ArtifactDefinition definition)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            WriteDefinition(writer, definition with { Ref = string.Empty });
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Path-level differences between two artifacts' semantic sections. Two
    /// SHA values tell nobody what changed; this tells them which field of
    /// which definition, with both values. Definitions are keyed by ref,
    /// dependencies by from|to|kind, detail by field, outcomes by code.
    /// </summary>
    public static IReadOnlyList<string> SemanticDiff(DefinitionArtifact source, DefinitionArtifact target)
    {
        var lines = new List<string>();

        if (source.FormatVersion != target.FormatVersion)
        {
            lines.Add("formatVersion: source=" + source.FormatVersion + " target=" + target.FormatVersion);
        }

        if (!string.Equals(source.RootRef, target.RootRef, StringComparison.Ordinal))
        {
            lines.Add("rootRef: source=" + source.RootRef + " target=" + target.RootRef);
        }

        var left = source.Definitions.ToDictionary(d => d.Ref, StringComparer.Ordinal);
        var right = target.Definitions.ToDictionary(d => d.Ref, StringComparer.Ordinal);

        foreach (var reference in left.Keys.Union(right.Keys).OrderBy(r => r, StringComparer.Ordinal))
        {
            var prefix = "definitions[" + reference + "]";
            if (!left.TryGetValue(reference, out var a)) { lines.Add(prefix + ": source=<ABSENT> target=" + right[reference].DefinitionCode); continue; }
            if (!right.TryGetValue(reference, out var b)) { lines.Add(prefix + ": source=" + a.DefinitionCode + " target=<ABSENT>"); continue; }

            Compare(lines, prefix + ".definitionCode", a.DefinitionCode, b.DefinitionCode);
            Compare(lines, prefix + ".kind", a.Kind, b.Kind);
            Compare(lines, prefix + ".surface", a.Surface, b.Surface);
            Compare(lines, prefix + ".name", a.Name, b.Name);
            Compare(lines, prefix + ".versionNumber", a.VersionNumber.ToString(CultureInfo.InvariantCulture), b.VersionNumber.ToString(CultureInfo.InvariantCulture));
            Compare(lines, prefix + ".status", a.Status, b.Status);
            Compare(lines, prefix + ".contentJson", CanonicalContent(a.ContentJson), CanonicalContent(b.ContentJson));

            var da = a.Detail ?? new Dictionary<string, string?>(StringComparer.Ordinal);
            var db = b.Detail ?? new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var key in da.Keys.Union(db.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                var av = da.TryGetValue(key, out var x) ? (x ?? "null") : "<ABSENT>";
                var bv = db.TryGetValue(key, out var y) ? (y ?? "null") : "<ABSENT>";
                Compare(lines, prefix + ".detail." + key, av, bv);
            }

            var oa = (a.Outcomes ?? Array.Empty<ArtifactOutcome>()).ToDictionary(o => o.OutcomeCode, StringComparer.Ordinal);
            var ob = (b.Outcomes ?? Array.Empty<ArtifactOutcome>()).ToDictionary(o => o.OutcomeCode, StringComparer.Ordinal);
            foreach (var code in oa.Keys.Union(ob.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                var av = oa.TryGetValue(code, out var x) ? x.ToString() : "<ABSENT>";
                var bv = ob.TryGetValue(code, out var y) ? y.ToString() : "<ABSENT>";
                Compare(lines, prefix + ".outcomes[" + code + "]", av, bv);
            }

            // Derived integrity, reported for evidence but outside equality.
            if (!string.Equals(a.DefinitionHash, b.DefinitionHash, StringComparison.Ordinal))
            {
                lines.Add(prefix + ".integrity.definitionHash (derived, not part of equality): source=" +
                          a.DefinitionHash + " target=" + b.DefinitionHash);
            }
        }

        string EdgeKey(ArtifactDependency d) => d.FromRef + "|" + d.ToRef + "|" + d.DependencyKind;
        var ea = source.Dependencies.ToDictionary(EdgeKey, StringComparer.Ordinal);
        var eb = target.Dependencies.ToDictionary(EdgeKey, StringComparer.Ordinal);
        foreach (var key in ea.Keys.Union(eb.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            var prefix = "dependencies[" + key + "]";
            if (!ea.TryGetValue(key, out var x)) { lines.Add(prefix + ": source=<ABSENT> target=present"); continue; }
            if (!eb.TryGetValue(key, out var y)) { lines.Add(prefix + ": source=present target=<ABSENT>"); continue; }
            Compare(lines, prefix + ".isRequired", x.IsRequired.ToString(), y.IsRequired.ToString());
            Compare(lines, prefix + ".dependsOnVersion",
                x.DependsOnVersion?.ToString(CultureInfo.InvariantCulture) ?? "null",
                y.DependsOnVersion?.ToString(CultureInfo.InvariantCulture) ?? "null");
        }

        return lines;
    }

    private static void Compare(List<string> lines, string path, string? a, string? b)
    {
        if (!string.Equals(a, b, StringComparison.Ordinal))
        {
            lines.Add(path + ": source=" + (a ?? "null") + " target=" + (b ?? "null"));
        }
    }

    private static string CanonicalContent(string json)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions)) { WriteCanonicalJsonValue(writer, json); }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteDefinition(Utf8JsonWriter writer, ArtifactDefinition definition)
    {
        writer.WriteStartObject();
        writer.WriteString("ref", definition.Ref);
        writer.WriteString("definitionCode", definition.DefinitionCode);
        writer.WriteString("kind", definition.Kind);
        writer.WriteString("surface", definition.Surface);
        writer.WriteString("name", definition.Name);
        writer.WriteNumber("versionNumber", definition.VersionNumber);
        writer.WriteString("status", definition.Status);

        // definitionHash is NOT written here. It is the T-090 writer's answer
        // to "is this the same canonical declaration", computed over the
        // writer's own normalised input; the portable question is answered by
        // the fields below. It travels in the transport form as derived
        // integrity evidence. (CENTRAL ruling 7, T-091 r8)
        writer.WritePropertyName("contentJson");
        WriteCanonicalJsonValue(writer, definition.ContentJson);

        writer.WritePropertyName("detail");
        if (definition.Detail is null || definition.Detail.Count == 0)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartObject();
            foreach (var pair in definition.Detail.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (pair.Value is null) { writer.WriteNull(pair.Key); }
                else { writer.WriteString(pair.Key, pair.Value); }
            }

            writer.WriteEndObject();
        }

        writer.WritePropertyName("outcomes");
        if (definition.Outcomes is null || definition.Outcomes.Count == 0)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartArray();
            foreach (var outcome in definition.Outcomes.OrderBy(o => o.OutcomeCode, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("outcomeCode", outcome.OutcomeCode);
                writer.WriteString("outcomeType", outcome.OutcomeType);
                writer.WriteString("classTaxonomyRef", outcome.ClassTaxonomyRef);
                writer.WriteString("ordinalRankMapJson", outcome.OrdinalRankMapJson);
                writer.WriteString("grainCode", outcome.GrainCode);
                writer.WriteString("detectionPositionCode", outcome.DetectionPositionCode);
                writer.WriteString("detectionTimestampField", outcome.DetectionTimestampField);
                writer.WriteString("direction", outcome.Direction);
                writer.WriteString("unitCode", outcome.UnitCode);
                writer.WriteString("censoringPolicy", outcome.CensoringPolicy);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        // PROVENANCE ONLY, AND NOT IN THE CANONICAL SECTION. Source identities
        // are environment-local; writing them here would make an artifact
        // exported from two installations of the same definitions compare
        // unequal, which is the opposite of portability.
        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes nested definition content with object keys sorted recursively, so
    /// two semantically identical payloads that were authored with different key
    /// order canonicalise to the same bytes. Arrays keep their order: element
    /// order inside a definition payload is authored meaning, not incidental.
    /// </summary>
    private static void WriteCanonicalJsonValue(Utf8JsonWriter writer, string json)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json); }
        catch (JsonException) { writer.WriteStringValue(json); return; }

        WriteNode(writer, node);
    }

    private static void WriteNode(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                return;

            case JsonObject obj:
                writer.WriteStartObject();
                foreach (var pair in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(pair.Key);
                    WriteNode(writer, pair.Value);
                }

                writer.WriteEndObject();
                return;

            case JsonArray array:
                writer.WriteStartArray();
                foreach (var item in array) { WriteNode(writer, item); }
                writer.WriteEndArray();
                return;

            default:
                node.WriteTo(writer);
                return;
        }
    }

    private static string? Text(JsonObject entry, string name)
    {
        var value = entry[name];
        if (value is null) { return null; }
        return value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString();
    }

    private static int? Number(JsonObject entry, string name)
    {
        var value = entry[name];
        if (value is null || value.GetValueKind() != JsonValueKind.Number) { return null; }
        return value.GetValue<int>();
    }
}
