// Industrial acquisition configuration and stable field authority: shared contract.
//
// Every refusal in this subsystem carries a stable code and a sentence naming the
// field, group or member that caused it. The database functions raise the same
// codes, so a refusal reads the same whether the application or the store found it.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PlantProcess.Application.Integration.Acquisition;

/// <summary>A typed refusal: a stable code plus a sentence a person can act on.</summary>
public sealed record AcquisitionRefusal(string Code, string Detail);

/// <summary>An accepted value or a typed refusal. There is no third state.</summary>
public sealed class AcquisitionOutcome<T>
{
    private AcquisitionOutcome(bool accepted, T? value, AcquisitionRefusal? refusal)
    {
        IsAccepted = accepted;
        Value = value;
        Refusal = refusal;
    }

    public bool IsAccepted { get; }

    public T? Value { get; }

    public AcquisitionRefusal? Refusal { get; }

    public static AcquisitionOutcome<T> Accept(T value) => new(true, value, null);

    public static AcquisitionOutcome<T> Refuse(string code, string detail) =>
        new(false, default, new AcquisitionRefusal(code, detail));

    public static AcquisitionOutcome<T> Refuse(AcquisitionRefusal refusal) =>
        new(false, default, refusal ?? throw new ArgumentNullException(nameof(refusal)));
}

/// <summary>The stable refusal vocabulary. The prefix before the space is the code.</summary>
public static class AcquisitionCodes
{
    public const string DatasetNotFound = "IAG01 dataset_not_found";
    public const string DatasetGovernedByAnotherTenant = "IAG02 dataset_governed_by_another_tenant";
    public const string DatasetNotGoverned = "IAG03 dataset_not_governed";
    public const string ConnectionIncoherent = "IAG04 connection_incoherent";

    public const string FieldDeclarationInvalid = "IAF01 field_declaration_invalid";
    public const string LocatorBoundToOtherField = "IAF02 locator_bound_to_other_field";
    public const string LocatorChangeRequiresReconciliation = "IAF03 locator_change_requires_reconciliation";
    public const string TypeChangeRequiresReconciliation = "IAF04 type_change_requires_reconciliation";
    public const string FieldKeyNotUnique = "IAF05 field_key_not_unique";
    public const string FieldNotFound = "IAF06 field_not_found";

    public const string LayoutInvalid = "IAL01 layout_invalid";
    public const string LayoutRevisionUnknown = "IAL02 layout_revision_unknown";

    public const string ConfigurationInvalid = "IAC01 configuration_invalid";
    public const string FieldReferenceUnknown = "IAC02 field_reference_unknown";
    public const string FieldRevisionStale = "IAC03 field_revision_stale_revalidation_required";
    public const string LayoutReferenceInvalid = "IAC04 layout_reference_invalid";
    public const string OperationNotExecutable = "IAC05 source_operation_not_executable";
    public const string VersionNotFound = "IAC06 configuration_version_not_found";
    public const string VersionNotPublished = "IAC07 configuration_version_not_published";
    public const string RuntimeNotCommissioned = "IAC08 acquisition_runtime_not_commissioned";
    public const string SecretMaterialRefused = "IAC09 secret_material_refused";

    /// <summary>The code part of a raised message: everything before the first colon.</summary>
    public static string CodeOf(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var colon = message.IndexOf(':');
        return colon > 0 ? message.Substring(0, colon).Trim() : message.Trim();
    }
}

/// <summary>
/// Deterministic JSON for hashing and persistence: object members ordered by
/// ordinal name, arrays in their authored order, numbers in invariant form. Two
/// declarations that differ only in whitespace or member order hash identically.
/// </summary>
public static class AcquisitionCanonicalJson
{
    private static readonly string[] SecretNames =
    {
        "password", "passwd", "pwd", "secret", "token", "apikey", "privatekey",
        "credential", "credentials", "connectionstring", "clientsecret"
    };

    public static string Canonicalize(JsonElement element)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            Write(element, writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static string Canonicalize(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Canonicalize(document.RootElement);
    }

    public static string Sha256Hex(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>
    /// True when any property name anywhere in the document names secret material.
    /// Authored acquisition content never carries credentials: those stay behind the
    /// approved collector-side secret reference.
    /// </summary>
    public static bool TryFindSecretMaterial(JsonElement element, out string path)
    {
        return Find(element, "$", out path);
    }

    private static bool Find(JsonElement element, string at, out string path)
    {
        path = string.Empty;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var folded = property.Name.Replace("_", string.Empty).Replace("-", string.Empty)
                    .ToLowerInvariant();
                foreach (var name in SecretNames)
                {
                    if (string.Equals(folded, name, StringComparison.Ordinal))
                    {
                        path = at + "." + property.Name;
                        return true;
                    }
                }

                if (Find(property.Value, at + "." + property.Name, out path))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                if (Find(item, at + "[" + index.ToString(CultureInfo.InvariantCulture) + "]", out path))
                {
                    return true;
                }

                index++;
            }
        }

        return false;
    }

    private static void Write(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    Write(item, writer);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                if (element.TryGetDecimal(out var number))
                {
                    writer.WriteRawValue(number.ToString("0.################", CultureInfo.InvariantCulture));
                }
                else
                {
                    element.WriteTo(writer);
                }

                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}

/// <summary>Small typed readers shared by the kernels. Every miss is a refusal, never a default.</summary>
internal static class AcquisitionJson
{
    public static bool TryString(JsonElement owner, string name, out string value)
    {
        value = string.Empty;
        if (owner.ValueKind != JsonValueKind.Object ||
            !owner.TryGetProperty(name, out var element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return true;
    }

    public static bool Has(JsonElement owner, string name) =>
        owner.ValueKind == JsonValueKind.Object &&
        owner.TryGetProperty(name, out var element) &&
        element.ValueKind != JsonValueKind.Null;

    public static bool TryLong(JsonElement owner, string name, out long value)
    {
        value = 0;
        return owner.ValueKind == JsonValueKind.Object &&
               owner.TryGetProperty(name, out var element) &&
               element.ValueKind == JsonValueKind.Number &&
               element.TryGetInt64(out value);
    }

    public static bool TryDecimal(JsonElement owner, string name, out decimal value)
    {
        value = 0m;
        return owner.ValueKind == JsonValueKind.Object &&
               owner.TryGetProperty(name, out var element) &&
               element.ValueKind == JsonValueKind.Number &&
               element.TryGetDecimal(out value);
    }

    public static bool TryBool(JsonElement owner, string name, out bool value)
    {
        value = false;
        if (owner.ValueKind != JsonValueKind.Object || !owner.TryGetProperty(name, out var element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.True) { value = true; return true; }
        if (element.ValueKind == JsonValueKind.False) { value = false; return true; }
        return false;
    }

    public static bool TryGuid(JsonElement owner, string name, out Guid value)
    {
        value = Guid.Empty;
        return TryString(owner, name, out var text) &&
               Guid.TryParseExact(text, "D", out value) &&
               value != Guid.Empty;
    }

    public static IReadOnlyList<string> UnknownMembers(JsonElement owner, IReadOnlyCollection<string> allowed)
    {
        var unknown = new List<string>();
        if (owner.ValueKind != JsonValueKind.Object)
        {
            return unknown;
        }

        foreach (var property in owner.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                unknown.Add(property.Name);
            }
        }

        unknown.Sort(StringComparer.Ordinal);
        return unknown;
    }

    public static bool IsCleanText(string value, int maxLength) =>
        value.Length > 0 &&
        value.Length <= maxLength &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        !value.Any(char.IsControl);
}
