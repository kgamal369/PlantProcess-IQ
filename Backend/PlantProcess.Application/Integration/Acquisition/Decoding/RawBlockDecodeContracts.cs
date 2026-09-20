// Declarative raw block decoding: the contract.
//
// Three things stay apart here, deliberately. TRANSPORT DELIVERY is how bytes
// arrived and how many times; DECODING turns an exact immutable layout version
// plus a bounded payload into typed values; SOURCE-EVENT IDENTITY is what the
// source says happened. A retry changes the delivery attempt and nothing else:
// the same bytes under the same layout version decode to the same values and the
// same event identity, on any machine, in any order.
//
// Nothing in this namespace schedules, stores, or reaches a database.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PlantProcess.Application.Integration.Acquisition.Decoding;

/// <summary>Stable refusal codes. The prefix before the space is the code.</summary>
public static class DecodeCodes
{
    public const string LayoutInvalid = "IAD01 layout_invalid";
    public const string PayloadTooLarge = "IAD02 payload_too_large";
    public const string PayloadTruncated = "IAD03 payload_truncated";
    public const string PayloadTrailingBytes = "IAD04 payload_trailing_bytes";
    public const string BudgetExhausted = "IAD05 decode_budget_exhausted";
    public const string CodecUnsupported = "IAD06 codec_unsupported";
    public const string ValueNotDecodable = "IAD07 value_not_decodable";
    public const string GrammarUnsupported = "IAD08 layout_grammar_unsupported";
    public const string EventIdentityMissing = "IAD09 source_event_identity_missing";
    public const string EventIdentityInvalid = "IAD10 source_event_identity_invalid";
    public const string TypedValueNotRawDecoded = "IAD11 typed_value_is_never_raw_decoded";
    public const string LayoutRevisionMismatch = "IAD12 layout_revision_mismatch";

    public static string CodeOf(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) { return string.Empty; }
        var space = message!.IndexOf(' ');
        return space > 0 ? message.Substring(0, space) : message;
    }
}

/// <summary>
/// Explicit ceilings. Every one of them is a refusal rather than a truncation, so a
/// payload that would cost more than the contract allows is never half-decoded.
/// </summary>
public sealed record DecodeBudget(
    int MaxPayloadBytes = 65536,
    int MaxMembers = 4096,
    int MaxDecodedBytes = 1048576,
    int MaxStringBytes = 65535,
    int MaxArrayElements = 65535,
    bool AllowTrailingBytes = false)
{
    public static DecodeBudget Default { get; } = new();
}

/// <summary>
/// How one delivery arrived. It is evidence, never identity: attempt number and
/// arrival time are excluded from every hash this file computes.
/// </summary>
public sealed record DeliveryAttempt(string TransportKey, int AttemptNumber, DateTime ReceivedAtUtc);

/// <summary>One decoded member. CanonicalText is the invariant form used for hashing.</summary>
public sealed record DecodedValue(
    Guid FieldId,
    string DeclaredType,
    object? Value,
    string CanonicalText,
    long StartBit,
    long BitWidth,
    int ArrayCount);

public sealed record DecodeDiagnostic(string Code, string Detail);

/// <summary>
/// The decoded event. Accepted is false whenever any diagnostic was raised: there
/// is no partially decoded record, because a half-decoded block is a fabrication.
/// </summary>
public sealed record RawBlockDecodeResult(
    bool Accepted,
    int LayoutRevision,
    string LayoutSemanticHash,
    string PayloadHash,
    int PayloadBytes,
    IReadOnlyList<DecodedValue> Values,
    IReadOnlyList<DecodeDiagnostic> Diagnostics)
{
    /// <summary>
    /// A digest of every decoded member, identity members included. This is CONTENT
    /// integrity, deliberately separate from the event identity: two deliveries that
    /// claim the same occurrence but carry different content have the same identity
    /// and different digests, which is exactly what lets the acceptance authority
    /// report a conflicting delivery instead of quietly minting a second occurrence.
    /// </summary>
    public string ContentDigest
    {
        get
        {
            if (!Accepted) { return string.Empty; }
            var builder = new StringBuilder("[");
            var first = true;
            foreach (var value in Values.OrderBy(v => v.FieldId))
            {
                if (!first) { builder.Append(','); }
                builder.Append("{\"fieldId\":\"").Append(value.FieldId.ToString("D"))
                       .Append("\",\"type\":\"").Append(value.DeclaredType)
                       .Append("\",\"value\":\"").Append(value.CanonicalText.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal))
                       .Append("\"}");
                first = false;
            }

            builder.Append(']');
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
        }
    }

    public static RawBlockDecodeResult Refuse(int layoutRevision, string layoutHash, string payloadHash, int payloadBytes, string code, string detail) =>
        new(false, layoutRevision, layoutHash, payloadHash, payloadBytes,
            Array.Empty<DecodedValue>(), new[] { new DecodeDiagnostic(code, detail) });
}

/// <summary>
/// The identity of the occurrence the source reported, derived only from what the
/// source said: the governed dataset, the exact layout revision, and the declared
/// identity members' decoded values. A replay of the same bytes yields the same
/// identity; a retry never mints a new one from the clock.
/// </summary>
public sealed record SourceEventIdentity(string Value, string CanonicalDocument)
{
    public static AcquisitionOutcome<SourceEventIdentity> Derive(
        Guid datasetGovernanceId,
        int layoutRevision,
        RawBlockDecodeResult decoded,
        IReadOnlyList<Guid> identityFieldIds)
    {
        ArgumentNullException.ThrowIfNull(decoded);
        ArgumentNullException.ThrowIfNull(identityFieldIds);

        if (!decoded.Accepted)
        {
            return AcquisitionOutcome<SourceEventIdentity>.Refuse(DecodeCodes.EventIdentityInvalid,
                "a refused decode has no source event identity.");
        }

        if (datasetGovernanceId == Guid.Empty || layoutRevision < 1)
        {
            return AcquisitionOutcome<SourceEventIdentity>.Refuse(DecodeCodes.EventIdentityInvalid,
                "an event identity names its governed dataset and the exact layout revision it was decoded under.");
        }

        if (identityFieldIds.Count == 0)
        {
            return AcquisitionOutcome<SourceEventIdentity>.Refuse(DecodeCodes.EventIdentityMissing,
                "the configuration declares no source-event identity member, so occurrences cannot be told apart.");
        }

        var builder = new StringBuilder();
        builder.Append("{\"datasetGovernanceId\":\"").Append(datasetGovernanceId.ToString("D"))
               .Append("\",\"layoutRevision\":").Append(layoutRevision.ToString(CultureInfo.InvariantCulture))
               .Append(",\"members\":[");

        var first = true;
        foreach (var fieldId in identityFieldIds.OrderBy(id => id))
        {
            var value = decoded.Values.FirstOrDefault(v => v.FieldId == fieldId);
            if (value is null)
            {
                return AcquisitionOutcome<SourceEventIdentity>.Refuse(DecodeCodes.EventIdentityMissing,
                    "identity member " + fieldId.ToString("D") + " is not placed by layout revision " +
                    layoutRevision.ToString(CultureInfo.InvariantCulture) + ".");
            }

            if (!first) { builder.Append(','); }
            builder.Append("{\"fieldId\":\"").Append(fieldId.ToString("D"))
                   .Append("\",\"type\":\"").Append(value.DeclaredType)
                   .Append("\",\"value\":").Append(JsonScalar(value.CanonicalText))
                   .Append('}');
            first = false;
        }

        builder.Append("]}");
        var document = builder.ToString();
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document))).ToLowerInvariant();
        return AcquisitionOutcome<SourceEventIdentity>.Accept(new SourceEventIdentity(identity, document));
    }

    private static string JsonScalar(string canonicalText)
    {
        var escaped = canonicalText
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return "\"" + escaped + "\"";
    }
}

/// <summary>
/// A typed source item is read as a value by its own provider path and is never
/// decoded again from raw bytes. This guard is pure so the rule can be proven
/// behaviourally, not only by reading the source.
/// </summary>
public static class TypedValueGuard
{
    public static DecodeDiagnostic? Check(
        IReadOnlyList<LayoutMember> members,
        IReadOnlyDictionary<Guid, AcquisitionFieldFact> fields)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(fields);

        foreach (var member in members)
        {
            if (!fields.TryGetValue(member.FieldId, out var fact))
            {
                return new DecodeDiagnostic(DecodeCodes.LayoutInvalid,
                    "layout member " + member.FieldId.ToString("D") + " names a field this dataset does not govern.");
            }

            if (fact.LocatorKind != SourceLocatorGrammar.RawMember)
            {
                return new DecodeDiagnostic(DecodeCodes.TypedValueNotRawDecoded,
                    "field " + member.FieldId.ToString("D") + " reads a typed " + fact.LocatorKind +
                    " value from its provider; decoding it from raw bytes would be a second, disagreeing reading.");
            }
        }

        return null;
    }
}
