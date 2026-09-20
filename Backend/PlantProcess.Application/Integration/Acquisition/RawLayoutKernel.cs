// Immutable raw block/message layout declarations.
//
// This kernel refuses a layout that could not be decoded honestly: a member
// outside the declared region, an impossible bit coordinate, a width the type
// does not have, a byte order on something that has no byte order, or two
// members claiming the same bits. It declares; it does not decode. The decoder
// consumes an accepted, immutable revision of this document.
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PlantProcess.Application.Integration.Acquisition;

/// <summary>A validated layout in canonical form.</summary>
public sealed record NormalizedLayout(
    string LayoutKind,
    int RegionBytes,
    string DocumentJson,
    string SemanticHash,
    IReadOnlyList<LayoutMember> Members);

/// <summary>One declared member, with its occupied bit span inside the region.</summary>
public sealed record LayoutMember(
    Guid FieldId,
    string MemberType,
    long StartBit,
    long EndBitExclusive);

public static class RawLayoutKernel
{
    public const string RawBlock = "raw_block";
    public const int MaxRegionBytes = 65536;
    public const int MaxMembers = 4096;

    private static readonly Dictionary<string, int> FixedWidthBits = new(StringComparer.Ordinal)
    {
        ["boolean"] = 1,
        ["int8"] = 8, ["uint8"] = 8,
        ["int16"] = 16, ["uint16"] = 16,
        ["int32"] = 32, ["uint32"] = 32, ["float32"] = 32,
        ["int64"] = 64, ["uint64"] = 64, ["float64"] = 64,
    };

    private static readonly string[] Orders = { "big", "little" };
    private static readonly string[] Encodings = { "ascii", "utf8", "latin1" };

    private static readonly string[] DocumentMembers = { "layoutKind", "regionBytes", "members", "evidence" };

    private static readonly string[] MemberMembers =
    {
        "fieldId", "type", "byteOffset", "bitOffset", "byteOrder", "wordOrder", "encoding", "length", "arrayCount"
    };

    public static AcquisitionOutcome<NormalizedLayout> Normalize(JsonElement? document)
    {
        if (document is null || document.Value.ValueKind != JsonValueKind.Object)
        {
            return Refuse("the layout must be a JSON object.");
        }

        var root = document.Value;
        var unknown = AcquisitionJson.UnknownMembers(root, DocumentMembers);
        if (unknown.Count > 0)
        {
            return Refuse("the layout does not declare member(s): " + string.Join(", ", unknown) + ".");
        }

        if (!AcquisitionJson.TryString(root, "layoutKind", out var layoutKind) || layoutKind != RawBlock)
        {
            return Refuse("layoutKind must be '" + RawBlock + "'.");
        }

        if (!AcquisitionJson.TryLong(root, "regionBytes", out var regionBytes) || regionBytes < 1 || regionBytes > MaxRegionBytes)
        {
            return Refuse("regionBytes must be an integer from 1 to " + MaxRegionBytes.ToString(CultureInfo.InvariantCulture) + ".");
        }

        string? evidence = null;
        if (AcquisitionJson.Has(root, "evidence"))
        {
            if (!AcquisitionJson.TryString(root, "evidence", out var text) || !AcquisitionJson.IsCleanText(text, 2000))
            {
                return Refuse("evidence must be trimmed text of at most 2000 characters.");
            }

            evidence = text;
        }

        if (!root.TryGetProperty("members", out var membersElement) || membersElement.ValueKind != JsonValueKind.Array)
        {
            return Refuse("members must be an array.");
        }

        var count = membersElement.GetArrayLength();
        if (count < 1 || count > MaxMembers)
        {
            return Refuse("a layout declares 1 to " + MaxMembers.ToString(CultureInfo.InvariantCulture) + " members.");
        }

        var regionBits = regionBytes * 8L;
        var seen = new HashSet<Guid>();
        var members = new List<(LayoutMember Member, SortedDictionary<string, object> Canonical)>();
        var index = 0;
        foreach (var member in membersElement.EnumerateArray())
        {
            var at = "member " + index.ToString(CultureInfo.InvariantCulture);
            index++;

            if (member.ValueKind != JsonValueKind.Object)
            {
                return Refuse(at + " must be an object.");
            }

            var memberUnknown = AcquisitionJson.UnknownMembers(member, MemberMembers);
            if (memberUnknown.Count > 0)
            {
                return Refuse(at + " does not declare member(s): " + string.Join(", ", memberUnknown) + ".");
            }

            if (!AcquisitionJson.TryGuid(member, "fieldId", out var fieldId))
            {
                return Refuse(at + ": fieldId must be a canonical field identity.");
            }

            at = at + " (" + fieldId.ToString("D") + ")";
            if (!seen.Add(fieldId))
            {
                return Refuse(at + ": a field is placed once per layout.");
            }

            if (!AcquisitionJson.TryString(member, "type", out var type))
            {
                return Refuse(at + ": type is required.");
            }

            var isText = type == "string";
            var isBytes = type == "bytes";
            if (!FixedWidthBits.ContainsKey(type) && !isText && !isBytes)
            {
                return Refuse(at + ": type '" + type + "' has no raw decoding in this contract.");
            }

            if (!AcquisitionJson.TryLong(member, "byteOffset", out var byteOffset) || byteOffset < 0 || byteOffset >= regionBytes)
            {
                return Refuse(at + ": byteOffset must lie inside the declared region.");
            }

            var canonical = new SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["fieldId"] = fieldId.ToString("D"),
                ["type"] = type,
                ["byteOffset"] = byteOffset,
            };

            long bitOffset = 0;
            if (type == "boolean")
            {
                if (!AcquisitionJson.TryLong(member, "bitOffset", out bitOffset) || bitOffset < 0 || bitOffset > 7)
                {
                    return Refuse(at + ": a boolean declares bitOffset 0 to 7.");
                }

                canonical["bitOffset"] = bitOffset;
            }
            else if (AcquisitionJson.Has(member, "bitOffset"))
            {
                return Refuse(at + ": only a boolean member declares a bit offset.");
            }

            long widthBits;
            if (isText || isBytes)
            {
                if (!AcquisitionJson.TryLong(member, "length", out var length) || length < 1 || length > 65535)
                {
                    return Refuse(at + ": a " + type + " member declares length 1 to 65535 bytes.");
                }

                widthBits = length * 8;
                canonical["length"] = length;
            }
            else
            {
                if (AcquisitionJson.Has(member, "length"))
                {
                    return Refuse(at + ": a fixed-width " + type + " does not declare a length.");
                }

                widthBits = FixedWidthBits[type];
            }

            if (isText)
            {
                if (!AcquisitionJson.TryString(member, "encoding", out var encoding) || !Encodings.Contains(encoding, StringComparer.Ordinal))
                {
                    return Refuse(at + ": a string declares encoding " + string.Join(", ", Encodings) + ".");
                }

                canonical["encoding"] = encoding;
            }
            else if (AcquisitionJson.Has(member, "encoding"))
            {
                return Refuse(at + ": only a string member declares an encoding.");
            }

            var multiByteNumber = FixedWidthBits.TryGetValue(type, out var fixedBits) && fixedBits >= 16;
            if (multiByteNumber)
            {
                if (!AcquisitionJson.TryString(member, "byteOrder", out var byteOrder) || !Orders.Contains(byteOrder, StringComparer.Ordinal))
                {
                    return Refuse(at + ": a multi-byte " + type + " declares byteOrder big or little.");
                }

                canonical["byteOrder"] = byteOrder;
                if (AcquisitionJson.Has(member, "wordOrder"))
                {
                    if (fixedBits < 32)
                    {
                        return Refuse(at + ": word order applies only to values of 32 bits or more.");
                    }

                    if (!AcquisitionJson.TryString(member, "wordOrder", out var wordOrder) || !Orders.Contains(wordOrder, StringComparer.Ordinal))
                    {
                        return Refuse(at + ": wordOrder must be big or little.");
                    }

                    canonical["wordOrder"] = wordOrder;
                }
            }
            else if (AcquisitionJson.Has(member, "byteOrder") || AcquisitionJson.Has(member, "wordOrder"))
            {
                return Refuse(at + ": a " + type + " member has no byte or word order.");
            }

            long arrayCount = 1;
            if (AcquisitionJson.Has(member, "arrayCount") &&
                (!AcquisitionJson.TryLong(member, "arrayCount", out arrayCount) || arrayCount < 1 || arrayCount > 65535))
            {
                return Refuse(at + ": arrayCount must be 1 to 65535.");
            }

            canonical["arrayCount"] = arrayCount;

            var start = checked(byteOffset * 8 + bitOffset);
            var span = checked(widthBits * arrayCount);
            var end = checked(start + span);
            if (end > regionBits)
            {
                return Refuse(at + ": the member ends at bit " + end.ToString(CultureInfo.InvariantCulture) +
                              ", outside the " + regionBits.ToString(CultureInfo.InvariantCulture) + "-bit region.");
            }

            members.Add((new LayoutMember(fieldId, type, start, end), canonical));
        }

        var ordered = members
            .OrderBy(m => m.Member.StartBit)
            .ThenBy(m => m.Member.FieldId)
            .ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            var previous = ordered[i - 1].Member;
            var current = ordered[i].Member;
            if (current.StartBit < previous.EndBitExclusive)
            {
                return Refuse("members " + previous.FieldId.ToString("D") + " and " + current.FieldId.ToString("D") +
                              " overlap at bit " + current.StartBit.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        var json = Serialize(layoutKind, regionBytes, evidence, ordered.Select(o => o.Canonical));
        return AcquisitionOutcome<NormalizedLayout>.Accept(new NormalizedLayout(
            layoutKind,
            (int)regionBytes,
            json,
            AcquisitionCanonicalJson.Sha256Hex(json),
            ordered.Select(o => o.Member).ToList()));
    }

    private static string Serialize(
        string layoutKind,
        long regionBytes,
        string? evidence,
        IEnumerable<SortedDictionary<string, object>> members)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            if (evidence is null) writer.WriteNull("evidence");
            else writer.WriteString("evidence", evidence);
            writer.WriteString("layoutKind", layoutKind);
            writer.WriteStartArray("members");
            foreach (var member in members)
            {
                writer.WriteStartObject();
                foreach (var pair in member)
                {
                    if (pair.Value is long number) writer.WriteNumber(pair.Key, number);
                    else writer.WriteString(pair.Key, Convert.ToString(pair.Value, CultureInfo.InvariantCulture));
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteNumber("regionBytes", regionBytes);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static AcquisitionOutcome<NormalizedLayout> Refuse(string detail) =>
        AcquisitionOutcome<NormalizedLayout>.Refuse(AcquisitionCodes.LayoutInvalid, detail);
}
