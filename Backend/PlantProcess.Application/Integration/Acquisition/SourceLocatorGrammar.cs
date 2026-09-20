// Provider-specific source locators behind one provider-neutral field identity.
//
// A locator says WHERE a field is read from. It is versioned metadata of a field,
// never the field's identity: the stable identity is field_id. The locator
// identity computed here is a lookup and conflict key inside one governed dataset,
// so a refresh that presents an unchanged locator reaches the field already bound
// to it, while a changed locator never silently inherits an existing field.
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlantProcess.Application.Integration.Acquisition;

/// <summary>A validated locator in canonical form, with its dataset-local lookup key.</summary>
public sealed record CanonicalSourceLocator(string Kind, string CanonicalJson, string Identity);

public static class SourceLocatorGrammar
{
    public const string RelationalColumn = "relational_column";
    public const string FileColumn = "file_column";
    public const string OpcNode = "opc_node";
    public const string RawMember = "raw_member";

    private const int MaxText = 512;

    private static readonly Regex IndexRangePattern =
        new(@"^\d+(:\d+)?(,\d+(:\d+)?)*$", RegexOptions.CultureInvariant);

    private static readonly Regex AttributePattern =
        new(@"^[A-Za-z][A-Za-z0-9]{0,63}$", RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, string[]> Required = new(StringComparer.Ordinal)
    {
        [RelationalColumn] = new[] { "object", "column" },
        [FileColumn] = new[] { "column" },
        [OpcNode] = new[] { "namespaceUri", "identifierType", "identifier" },
        [RawMember] = new[] { "region", "path" },
    };

    private static readonly Dictionary<string, string[]> Optional = new(StringComparer.Ordinal)
    {
        [RelationalColumn] = new[] { "schema" },
        [FileColumn] = new[] { "sheet", "table", "range" },
        [OpcNode] = new[] { "attribute", "indexRange" },
        [RawMember] = Array.Empty<string>(),
    };

    /// <summary>
    /// Which locator families a provider can address. This reads the provider type the
    /// connector truth catalogue already declares; it does not declare providers of its
    /// own, and an unknown provider has no locator family at all.
    /// </summary>
    public static IReadOnlyList<string> FamiliesOf(string? providerType)
    {
        switch ((providerType ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "csv":
            case "excel":
                return new[] { FileColumn };
            case "postgresql":
            case "sqlserver":
            case "mysql":
            case "oracle":
                return new[] { RelationalColumn };
            case "opcuahistorian":
                return new[] { OpcNode, RawMember };
            default:
                return Array.Empty<string>();
        }
    }

    public static AcquisitionOutcome<CanonicalSourceLocator> Normalize(string? providerType, JsonElement? locator)
    {
        if (locator is null || locator.Value.ValueKind != JsonValueKind.Object)
        {
            return Refuse("the source locator must be a JSON object with a kind.");
        }

        var element = locator.Value;
        if (!AcquisitionJson.TryString(element, "kind", out var kind) || !Required.ContainsKey(kind))
        {
            return Refuse("the source locator kind must be one of " +
                          string.Join(", ", Required.Keys.OrderBy(k => k, StringComparer.Ordinal)) + ".");
        }

        var families = FamiliesOf(providerType);
        if (!families.Contains(kind, StringComparer.Ordinal))
        {
            return Refuse("provider '" + (providerType ?? string.Empty) + "' cannot address a '" + kind +
                          "' locator.");
        }

        if (element.TryGetProperty("namespaceIndex", out _))
        {
            return Refuse("an OPC namespace index is transient and is never a locator; declare namespaceUri.");
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal) { "kind" };
        foreach (var name in Required[kind]) allowed.Add(name);
        foreach (var name in Optional[kind]) allowed.Add(name);

        var unknown = AcquisitionJson.UnknownMembers(element, allowed);
        if (unknown.Count > 0)
        {
            return Refuse("locator kind '" + kind + "' does not declare member(s): " + string.Join(", ", unknown) + ".");
        }

        var values = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["kind"] = kind };
        foreach (var name in Required[kind])
        {
            if (!AcquisitionJson.TryString(element, name, out var text) || !AcquisitionJson.IsCleanText(text, MaxText))
            {
                return Refuse("locator member '" + name + "' is required and must be trimmed text of at most " +
                              MaxText.ToString(CultureInfo.InvariantCulture) + " characters.");
            }

            values[name] = text;
        }

        foreach (var name in Optional[kind])
        {
            if (!element.TryGetProperty(name, out var raw) || raw.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (raw.ValueKind != JsonValueKind.String ||
                !AcquisitionJson.IsCleanText(raw.GetString() ?? string.Empty, MaxText))
            {
                return Refuse("locator member '" + name + "' must be trimmed text of at most " +
                              MaxText.ToString(CultureInfo.InvariantCulture) + " characters.");
            }

            values[name] = raw.GetString()!;
        }

        if (kind == OpcNode)
        {
            var refusal = ValidateOpc(values);
            if (refusal is not null)
            {
                return AcquisitionOutcome<CanonicalSourceLocator>.Refuse(refusal);
            }
        }

        var canonical = Serialize(values);
        var identity = AcquisitionCanonicalJson.Sha256Hex(canonical);
        return AcquisitionOutcome<CanonicalSourceLocator>.Accept(new CanonicalSourceLocator(kind, canonical, identity));
    }

    private static AcquisitionRefusal? ValidateOpc(SortedDictionary<string, string> values)
    {
        var uri = values["namespaceUri"];
        if (uri.All(char.IsDigit) || uri.StartsWith("ns=", StringComparison.OrdinalIgnoreCase))
        {
            return new AcquisitionRefusal(AcquisitionCodes.FieldDeclarationInvalid,
                "namespaceUri must be the namespace URI, not a namespace index.");
        }

        var type = values["identifierType"];
        var identifier = values["identifier"];
        switch (type)
        {
            case "i":
                if (!uint.TryParse(identifier, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    return new AcquisitionRefusal(AcquisitionCodes.FieldDeclarationInvalid,
                        "a numeric OPC identifier must be an unsigned 32-bit integer.");
                }

                break;
            case "g":
                if (!Guid.TryParseExact(identifier, "D", out var guid))
                {
                    return new AcquisitionRefusal(AcquisitionCodes.FieldDeclarationInvalid,
                        "a GUID OPC identifier must be a canonical GUID.");
                }

                values["identifier"] = guid.ToString("D");
                break;
            case "s":
            case "b":
                break;
            default:
                return new AcquisitionRefusal(AcquisitionCodes.FieldDeclarationInvalid,
                    "identifierType must be one of i, s, g, b.");
        }

        if (!values.ContainsKey("attribute"))
        {
            // OPC UA's own default attribute for a variable read. Stated explicitly so
            // two locators that mean the same node attribute hash identically.
            values["attribute"] = "Value";
        }
        else if (!AttributePattern.IsMatch(values["attribute"]))
        {
            return new AcquisitionRefusal(AcquisitionCodes.FieldDeclarationInvalid,
                "attribute must be an OPC attribute name.");
        }

        if (values.TryGetValue("indexRange", out var range) && !IndexRangePattern.IsMatch(range))
        {
            return new AcquisitionRefusal(AcquisitionCodes.FieldDeclarationInvalid,
                "indexRange must follow the OPC numeric range grammar.");
        }

        return null;
    }

    private static string Serialize(SortedDictionary<string, string> values)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var pair in values)
            {
                writer.WriteString(pair.Key, pair.Value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static AcquisitionOutcome<CanonicalSourceLocator> Refuse(string detail) =>
        AcquisitionOutcome<CanonicalSourceLocator>.Refuse(AcquisitionCodes.FieldDeclarationInvalid, detail);
}
