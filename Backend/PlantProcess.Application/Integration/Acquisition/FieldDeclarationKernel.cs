// Stable field declarations.
//
// field_id is the PPIQ identity. The technical key, the display name, the declared
// type and the source locator are governed metadata carried by immutable revisions
// of that identity. Nothing here derives identity or decoding from a display name.
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlantProcess.Application.Integration.Acquisition;

/// <summary>What a caller submits for one field.</summary>
public sealed record FieldDeclarationRequest(
    Guid? FieldId,
    string? FieldKey,
    string? DisplayName,
    string? DeclaredType,
    JsonElement? TypeShape,
    string? SourceUnit,
    IReadOnlyList<string>? Roles,
    JsonElement? SourceLocator,
    int? LayoutRevision,
    string? Reconcile);

/// <summary>A validated field declaration, ready for the governed revision function.</summary>
public sealed record NormalizedFieldDeclaration(
    Guid? FieldId,
    string FieldKey,
    string DisplayName,
    string DeclaredType,
    string TypeShapeJson,
    string? SourceUnit,
    IReadOnlyList<string> Roles,
    CanonicalSourceLocator Locator,
    int? LayoutRevision,
    string? Reconcile,
    string SemanticHash);

public static class FieldDeclarationKernel
{
    /// <summary>The one explicit reconciliation a caller may assert.</summary>
    public const string ReconcileSameField = "same_field";

    public const int MaxFieldsPerRequest = 2000;

    public static readonly IReadOnlyList<string> DeclaredTypes = new[]
    {
        "boolean", "int8", "uint8", "int16", "uint16", "int32", "uint32", "int64", "uint64",
        "float32", "float64", "decimal", "string", "bytes", "datetime", "date", "time",
        "duration", "guid"
    };

    public static readonly IReadOnlyList<string> RoleVocabulary = new[]
    {
        "payload", "trigger", "event_identity", "quality", "time", "key"
    };

    private static readonly string[] IntegerTypes =
        { "int8", "uint8", "int16", "uint16", "int32", "uint32", "int64", "uint64" };

    private static readonly Regex FieldKeyPattern =
        new(@"^[A-Za-z][A-Za-z0-9_.\-]{0,199}$", RegexOptions.CultureInvariant);

    public static bool IsInteger(string declaredType) => IntegerTypes.Contains(declaredType, StringComparer.Ordinal);

    public static bool IsNumeric(string declaredType) =>
        IsInteger(declaredType) ||
        declaredType is "float32" or "float64" or "decimal";

    public static AcquisitionOutcome<NormalizedFieldDeclaration> Normalize(
        string? providerType,
        FieldDeclarationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = request.FieldKey ?? string.Empty;
        if (!FieldKeyPattern.IsMatch(key))
        {
            return Refuse("field key '" + key + "' must start with a letter and use letters, digits, '_', '.' or '-' (at most 200).");
        }

        var name = request.DisplayName ?? string.Empty;
        if (!AcquisitionJson.IsCleanText(name, 200))
        {
            return Refuse(key + ": display name is required, trimmed, at most 200 characters.");
        }

        var type = request.DeclaredType ?? string.Empty;
        if (!DeclaredTypes.Contains(type, StringComparer.Ordinal))
        {
            return Refuse(key + ": declared type '" + type + "' is not one of " + string.Join(", ", DeclaredTypes) + ".");
        }

        var shape = NormalizeShape(key, type, request.TypeShape, out var shapeRefusal);
        if (shape is null)
        {
            return AcquisitionOutcome<NormalizedFieldDeclaration>.Refuse(shapeRefusal!);
        }

        string? unit = null;
        if (request.SourceUnit is not null)
        {
            if (!AcquisitionJson.IsCleanText(request.SourceUnit, 64))
            {
                return Refuse(key + ": source unit must be trimmed text of at most 64 characters.");
            }

            unit = request.SourceUnit;
        }

        if (request.Roles is null || request.Roles.Count == 0)
        {
            return Refuse(key + ": at least one role must be declared explicitly.");
        }

        var roles = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var role in request.Roles)
        {
            if (!RoleVocabulary.Contains(role ?? string.Empty, StringComparer.Ordinal))
            {
                return Refuse(key + ": role '" + role + "' is not one of " + string.Join(", ", RoleVocabulary) + ".");
            }

            if (!roles.Add(role!))
            {
                return Refuse(key + ": role '" + role + "' is declared twice.");
            }
        }

        var locator = SourceLocatorGrammar.Normalize(providerType, request.SourceLocator);
        if (!locator.IsAccepted)
        {
            return AcquisitionOutcome<NormalizedFieldDeclaration>.Refuse(
                locator.Refusal!.Code, key + ": " + locator.Refusal.Detail);
        }

        if (request.LayoutRevision.HasValue)
        {
            if (request.LayoutRevision.Value < 1)
            {
                return Refuse(key + ": a layout revision must be greater than zero.");
            }

            if (locator.Value!.Kind != SourceLocatorGrammar.RawMember)
            {
                return Refuse(key + ": only a raw member is decoded through a layout; a typed source item is never decoded twice.");
            }
        }

        if (request.Reconcile is not null)
        {
            if (!string.Equals(request.Reconcile, ReconcileSameField, StringComparison.Ordinal))
            {
                return Refuse(key + ": reconcile must be '" + ReconcileSameField + "' or absent.");
            }

            if (request.FieldId is null)
            {
                return Refuse(key + ": a reconciliation must name the field_id it continues.");
            }
        }

        if (request.FieldId.HasValue && request.FieldId.Value == Guid.Empty)
        {
            return Refuse(key + ": field_id must not be the empty identity.");
        }

        var hash = AcquisitionCanonicalJson.Sha256Hex(SemanticDocument(
            locator.Value!, key, name, type, shape, unit, roles.ToList(), request.LayoutRevision));

        return AcquisitionOutcome<NormalizedFieldDeclaration>.Accept(new NormalizedFieldDeclaration(
            request.FieldId, key, name, type, shape, unit, roles.ToList(), locator.Value!,
            request.LayoutRevision, request.Reconcile, hash));
    }

    /// <summary>A whole PUT: every field valid, keys and identities unique inside the request.</summary>
    public static AcquisitionOutcome<IReadOnlyList<NormalizedFieldDeclaration>> NormalizeAll(
        string? providerType,
        IReadOnlyList<FieldDeclarationRequest>? requests)
    {
        if (requests is null || requests.Count == 0)
        {
            return AcquisitionOutcome<IReadOnlyList<NormalizedFieldDeclaration>>.Refuse(
                AcquisitionCodes.FieldDeclarationInvalid, "at least one field must be declared.");
        }

        if (requests.Count > MaxFieldsPerRequest)
        {
            return AcquisitionOutcome<IReadOnlyList<NormalizedFieldDeclaration>>.Refuse(
                AcquisitionCodes.FieldDeclarationInvalid,
                "at most " + MaxFieldsPerRequest.ToString(CultureInfo.InvariantCulture) + " fields may be declared in one request.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<Guid>();
        var locators = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<NormalizedFieldDeclaration>();
        foreach (var request in requests)
        {
            var one = Normalize(providerType, request);
            if (!one.IsAccepted)
            {
                return AcquisitionOutcome<IReadOnlyList<NormalizedFieldDeclaration>>.Refuse(one.Refusal!);
            }

            var field = one.Value!;
            if (!keys.Add(field.FieldKey))
            {
                return AcquisitionOutcome<IReadOnlyList<NormalizedFieldDeclaration>>.Refuse(
                    AcquisitionCodes.FieldKeyNotUnique, field.FieldKey + ": the key appears twice in this request.");
            }

            if (field.FieldId.HasValue && !ids.Add(field.FieldId.Value))
            {
                return AcquisitionOutcome<IReadOnlyList<NormalizedFieldDeclaration>>.Refuse(
                    AcquisitionCodes.FieldDeclarationInvalid, field.FieldKey + ": the field_id appears twice in this request.");
            }

            if (!locators.Add(field.Locator.Identity))
            {
                return AcquisitionOutcome<IReadOnlyList<NormalizedFieldDeclaration>>.Refuse(
                    AcquisitionCodes.LocatorBoundToOtherField, field.FieldKey + ": the same source locator appears twice in this request.");
            }

            result.Add(field);
        }

        return AcquisitionOutcome<IReadOnlyList<NormalizedFieldDeclaration>>.Accept(result);
    }

    private static string? NormalizeShape(string key, string type, JsonElement? shape, out AcquisitionRefusal? refusal)
    {
        refusal = null;
        var values = new SortedDictionary<string, long>(StringComparer.Ordinal);
        if (shape is not null && shape.Value.ValueKind != JsonValueKind.Null)
        {
            var element = shape.Value;
            if (element.ValueKind != JsonValueKind.Object)
            {
                refusal = new AcquisitionRefusal(AcquisitionCodes.FieldDeclarationInvalid, key + ": type shape must be an object.");
                return null;
            }

            var allowed = new List<string> { "arrayLength" };
            if (type is "string" or "bytes") allowed.Add("maxLength");
            if (type == "decimal") { allowed.Add("precision"); allowed.Add("scale"); }

            var unknown = AcquisitionJson.UnknownMembers(element, allowed);
            if (unknown.Count > 0)
            {
                refusal = new AcquisitionRefusal(AcquisitionCodes.FieldDeclarationInvalid,
                    key + ": type '" + type + "' does not accept shape member(s) " + string.Join(", ", unknown) + ".");
                return null;
            }

            foreach (var name in allowed)
            {
                if (!element.TryGetProperty(name, out var raw) || raw.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                if (!AcquisitionJson.TryLong(element, name, out var number))
                {
                    refusal = new AcquisitionRefusal(AcquisitionCodes.FieldDeclarationInvalid, key + ": shape member " + name + " must be an integer.");
                    return null;
                }

                values[name] = number;
            }
        }

        if (values.TryGetValue("arrayLength", out var arrayLength) && (arrayLength < 1 || arrayLength > 65535))
        {
            refusal = new AcquisitionRefusal(AcquisitionCodes.FieldDeclarationInvalid, key + ": arrayLength must be 1 to 65535.");
            return null;
        }

        if (values.TryGetValue("maxLength", out var maxLength) && (maxLength < 1 || maxLength > 1048576))
        {
            refusal = new AcquisitionRefusal(AcquisitionCodes.FieldDeclarationInvalid, key + ": maxLength must be 1 to 1048576.");
            return null;
        }

        if (values.ContainsKey("precision") != values.ContainsKey("scale"))
        {
            refusal = new AcquisitionRefusal(AcquisitionCodes.FieldDeclarationInvalid, key + ": precision and scale are declared together.");
            return null;
        }

        if (values.TryGetValue("precision", out var precision))
        {
            var scale = values["scale"];
            if (precision < 1 || precision > 38 || scale < 0 || scale > precision)
            {
                refusal = new AcquisitionRefusal(AcquisitionCodes.FieldDeclarationInvalid,
                    key + ": precision must be 1 to 38 and scale 0 to precision.");
                return null;
            }
        }

        var builder = new StringBuilder("{");
        var first = true;
        foreach (var pair in values)
        {
            if (!first) builder.Append(',');
            builder.Append('"').Append(pair.Key).Append("\":").Append(pair.Value.ToString(CultureInfo.InvariantCulture));
            first = false;
        }

        return builder.Append('}').ToString();
    }

    private static string SemanticDocument(
        CanonicalSourceLocator locator,
        string key,
        string name,
        string type,
        string shapeJson,
        string? unit,
        IReadOnlyList<string> roles,
        int? layoutRevision)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("declaredType", type);
            writer.WriteString("displayName", name);
            writer.WriteString("fieldKey", key);
            if (layoutRevision.HasValue) writer.WriteNumber("layoutRevision", layoutRevision.Value);
            else writer.WriteNull("layoutRevision");
            writer.WritePropertyName("locator");
            writer.WriteRawValue(locator.CanonicalJson);
            writer.WriteStartArray("roles");
            foreach (var role in roles) writer.WriteStringValue(role);
            writer.WriteEndArray();
            writer.WritePropertyName("typeShape");
            writer.WriteRawValue(shapeJson);
            if (unit is null) writer.WriteNull("unit");
            else writer.WriteString("unit", unit);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static AcquisitionOutcome<NormalizedFieldDeclaration> Refuse(string detail) =>
        AcquisitionOutcome<NormalizedFieldDeclaration>.Refuse(AcquisitionCodes.FieldDeclarationInvalid, detail);
}
