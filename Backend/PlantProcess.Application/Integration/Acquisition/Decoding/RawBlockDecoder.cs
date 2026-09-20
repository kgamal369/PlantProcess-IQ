// The declarative raw block decoder.
//
// It executes ONE immutable layout revision over ONE bounded payload. The layout is
// re-validated by the authority that froze it (RawLayoutKernel) before a single bit
// is read, so this file never becomes a second grammar. Arithmetic is checked, every
// limit is a refusal rather than a truncation, and nothing here is derived from a
// display label: identity and placement come from field_id and the layout alone.
//
// It runs no customer code. A member shape the grammar does not declare is refused
// by name instead of guessed.
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PlantProcess.Application.Integration.Acquisition.Decoding;

public static class RawBlockDecoder
{
    /// <summary>
    /// The layout grammar this decoder executes. A layout declaring anything outside
    /// it is refused; widening the grammar is a change to the layout authority, not
    /// something a consumer may infer.
    /// </summary>
    public const string SupportedLayoutKind = RawLayoutKernel.RawBlock;

    public static RawBlockDecodeResult Decode(
        JsonElement? layoutDocument,
        int layoutRevision,
        ReadOnlySpan<byte> payload,
        DecodeBudget? budget = null)
    {
        var limits = budget ?? DecodeBudget.Default;
        var payloadHash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var payloadBytes = payload.Length;

        // 1. The layout is validated by its own authority before execution.
        var normalized = RawLayoutKernel.Normalize(layoutDocument);
        if (!normalized.IsAccepted)
        {
            return RawBlockDecodeResult.Refuse(layoutRevision, string.Empty, payloadHash, payloadBytes,
                DecodeCodes.LayoutInvalid, normalized.Refusal!.Detail);
        }

        var layout = normalized.Value!;
        if (!string.Equals(layout.LayoutKind, SupportedLayoutKind, StringComparison.Ordinal))
        {
            return RawBlockDecodeResult.Refuse(layoutRevision, layout.SemanticHash, payloadHash, payloadBytes,
                DecodeCodes.GrammarUnsupported,
                "layout kind '" + layout.LayoutKind + "' is not executed by this decoder.");
        }

        // 2. Budgets, before any allocation.
        if (payloadBytes > limits.MaxPayloadBytes)
        {
            return RawBlockDecodeResult.Refuse(layoutRevision, layout.SemanticHash, payloadHash, payloadBytes,
                DecodeCodes.PayloadTooLarge,
                "the payload is " + payloadBytes.ToString(CultureInfo.InvariantCulture) + " bytes; the budget allows " +
                limits.MaxPayloadBytes.ToString(CultureInfo.InvariantCulture) + ".");
        }

        if (layout.Members.Count > limits.MaxMembers)
        {
            return RawBlockDecodeResult.Refuse(layoutRevision, layout.SemanticHash, payloadHash, payloadBytes,
                DecodeCodes.BudgetExhausted,
                "the layout places " + layout.Members.Count.ToString(CultureInfo.InvariantCulture) +
                " members; the budget allows " + limits.MaxMembers.ToString(CultureInfo.InvariantCulture) + ".");
        }

        if (payloadBytes < layout.RegionBytes)
        {
            return RawBlockDecodeResult.Refuse(layoutRevision, layout.SemanticHash, payloadHash, payloadBytes,
                DecodeCodes.PayloadTruncated,
                "the layout declares " + layout.RegionBytes.ToString(CultureInfo.InvariantCulture) +
                " bytes and the payload carries " + payloadBytes.ToString(CultureInfo.InvariantCulture) +
                "; a short block is never padded.");
        }

        if (payloadBytes > layout.RegionBytes && !limits.AllowTrailingBytes)
        {
            return RawBlockDecodeResult.Refuse(layoutRevision, layout.SemanticHash, payloadHash, payloadBytes,
                DecodeCodes.PayloadTrailingBytes,
                (payloadBytes - layout.RegionBytes).ToString(CultureInfo.InvariantCulture) +
                " byte(s) follow the declared region. Unexplained trailing bytes are a framing difference, not padding.");
        }

        // 3. Execute, member by member, against the region only.
        var region = payload.Slice(0, layout.RegionBytes);
        var values = new List<DecodedValue>(layout.Members.Count);
        var diagnostics = new List<DecodeDiagnostic>();
        long decodedBytes = 0;

        using var document = JsonDocument.Parse(layout.DocumentJson);
        foreach (var member in document.RootElement.GetProperty("members").EnumerateArray())
        {
            var placement = Placement.From(member);
            var decoded = DecodeMember(region, placement, limits, ref decodedBytes, out var diagnostic);
            if (diagnostic is not null)
            {
                diagnostics.Add(diagnostic);
                break;
            }

            values.Add(decoded!);
        }

        if (diagnostics.Count > 0)
        {
            // A refused block yields no values at all. Half a record is a fabrication.
            return new RawBlockDecodeResult(false, layoutRevision, layout.SemanticHash, payloadHash, payloadBytes,
                Array.Empty<DecodedValue>(), diagnostics);
        }

        return new RawBlockDecodeResult(true, layoutRevision, layout.SemanticHash, payloadHash, payloadBytes,
            values, Array.Empty<DecodeDiagnostic>());
    }

    private sealed record Placement(
        Guid FieldId, string Type, long ByteOffset, long BitOffset, string ByteOrder, string WordOrder,
        string Encoding, long Length, long ArrayCount)
    {
        public static Placement From(JsonElement member)
        {
            var fieldId = Guid.ParseExact(member.GetProperty("fieldId").GetString()!, "D");
            var type = member.GetProperty("type").GetString()!;
            var byteOffset = member.GetProperty("byteOffset").GetInt64();
            long bitOffset = member.TryGetProperty("bitOffset", out var bit) ? bit.GetInt64() : 0;
            var byteOrder = member.TryGetProperty("byteOrder", out var order) ? order.GetString()! : string.Empty;
            var wordOrder = member.TryGetProperty("wordOrder", out var word) ? word.GetString()! : string.Empty;
            var encoding = member.TryGetProperty("encoding", out var enc) ? enc.GetString()! : string.Empty;
            long length = member.TryGetProperty("length", out var len) ? len.GetInt64() : 0;
            long arrayCount = member.TryGetProperty("arrayCount", out var count) ? count.GetInt64() : 1;
            return new Placement(fieldId, type, byteOffset, bitOffset, byteOrder, wordOrder, encoding, length, arrayCount);
        }
    }

    private static readonly Dictionary<string, int> WidthBits = new(StringComparer.Ordinal)
    {
        ["boolean"] = 1,
        ["int8"] = 8, ["uint8"] = 8,
        ["int16"] = 16, ["uint16"] = 16,
        ["int32"] = 32, ["uint32"] = 32, ["float32"] = 32,
        ["int64"] = 64, ["uint64"] = 64, ["float64"] = 64,
    };

    private static DecodedValue? DecodeMember(
        ReadOnlySpan<byte> region,
        Placement placement,
        DecodeBudget limits,
        ref long decodedBytes,
        out DecodeDiagnostic? diagnostic)
    {
        diagnostic = null;
        var at = "field " + placement.FieldId.ToString("D") + " (" + placement.Type + ")";

        if (placement.ArrayCount > limits.MaxArrayElements)
        {
            diagnostic = new DecodeDiagnostic(DecodeCodes.BudgetExhausted,
                at + ": " + placement.ArrayCount.ToString(CultureInfo.InvariantCulture) + " elements exceed the array budget.");
            return null;
        }

        var isText = placement.Type == "string";
        var isBytes = placement.Type == "bytes";
        long widthBits;
        if (isText || isBytes)
        {
            if (placement.Length > limits.MaxStringBytes)
            {
                diagnostic = new DecodeDiagnostic(DecodeCodes.BudgetExhausted,
                    at + ": " + placement.Length.ToString(CultureInfo.InvariantCulture) + " bytes exceed the string budget.");
                return null;
            }

            widthBits = checked(placement.Length * 8);
        }
        else if (WidthBits.TryGetValue(placement.Type, out var fixedWidth))
        {
            widthBits = fixedWidth;
        }
        else
        {
            diagnostic = new DecodeDiagnostic(DecodeCodes.CodecUnsupported, at + ": no codec decodes this type.");
            return null;
        }

        long startBit;
        long spanBits;
        long endBit;
        try
        {
            startBit = checked(placement.ByteOffset * 8 + placement.BitOffset);
            spanBits = checked(widthBits * placement.ArrayCount);
            endBit = checked(startBit + spanBits);
        }
        catch (OverflowException)
        {
            diagnostic = new DecodeDiagnostic(DecodeCodes.ValueNotDecodable, at + ": the placement arithmetic overflows.");
            return null;
        }

        if (endBit > (long)region.Length * 8)
        {
            diagnostic = new DecodeDiagnostic(DecodeCodes.PayloadTruncated,
                at + ": the member ends at bit " + endBit.ToString(CultureInfo.InvariantCulture) +
                ", past the " + ((long)region.Length * 8).ToString(CultureInfo.InvariantCulture) + "-bit region.");
            return null;
        }

        var cost = (spanBits + 7) / 8;
        decodedBytes = checked(decodedBytes + cost);
        if (decodedBytes > limits.MaxDecodedBytes)
        {
            diagnostic = new DecodeDiagnostic(DecodeCodes.BudgetExhausted,
                at + ": decoding would allocate more than the budget allows.");
            return null;
        }

        var count = (int)placement.ArrayCount;
        var texts = new List<string>(count);
        object? single = null;

        for (var index = 0; index < count; index++)
        {
            if (placement.Type == "boolean")
            {
                var bitIndex = startBit + index;
                var b = region[(int)(bitIndex / 8)];
                // Bit 0 is the least significant bit of the addressed byte, which is the
                // convention the layout authority documents for a bit offset of 0..7.
                var bit = ((b >> (int)(bitIndex % 8)) & 1) == 1;
                if (count == 1) { single = bit; }
                texts.Add(bit ? "true" : "false");
                continue;
            }

            var byteWidth = (int)(widthBits / 8);
            var offset = (int)(placement.ByteOffset + ((long)index * byteWidth));
            var raw = region.Slice(offset, byteWidth);

            if (isText || isBytes)
            {
                var slice = raw.ToArray();
                if (isBytes)
                {
                    var hex = Convert.ToHexString(slice).ToLowerInvariant();
                    if (count == 1) { single = slice; }
                    texts.Add(hex);
                    continue;
                }

                var text = DecodeText(slice, placement.Encoding, out var textDiagnostic);
                if (textDiagnostic is not null)
                {
                    diagnostic = new DecodeDiagnostic(textDiagnostic.Code, at + ": " + textDiagnostic.Detail);
                    return null;
                }

                if (count == 1) { single = text; }
                texts.Add(text!);
                continue;
            }

            var ordered = ApplyOrder(raw, placement.ByteOrder, placement.WordOrder);
            var scalar = ReadScalar(placement.Type, ordered);
            if (count == 1) { single = scalar; }
            texts.Add(Canonical(placement.Type, scalar));
        }

        var canonicalText = count == 1 ? texts[0] : "[" + string.Join(",", texts) + "]";
        var value = count == 1 ? single : texts.ToArray();
        return new DecodedValue(placement.FieldId, placement.Type, value, canonicalText, startBit, spanBits, count);
    }

    /// <summary>
    /// Byte and word order, applied as declared. A value of 32 bits or more with a
    /// declared word order is read as 16-bit words: the word sequence is ordered
    /// first, then the bytes inside each word, which is what a declared wordOrder
    /// means on the sources that need it.
    /// </summary>
    private static byte[] ApplyOrder(ReadOnlySpan<byte> raw, string byteOrder, string wordOrder)
    {
        var bytes = raw.ToArray();
        if (bytes.Length == 1)
        {
            return bytes;
        }

        if (!string.IsNullOrEmpty(wordOrder) && bytes.Length >= 4 && bytes.Length % 2 == 0)
        {
            var words = new List<byte[]>();
            for (var i = 0; i < bytes.Length; i += 2)
            {
                var word = new[] { bytes[i], bytes[i + 1] };
                if (byteOrder == "little") { (word[0], word[1]) = (word[1], word[0]); }
                words.Add(word);
            }

            if (wordOrder == "little") { words.Reverse(); }
            return words.SelectMany(w => w).ToArray();
        }

        if (byteOrder == "little") { Array.Reverse(bytes); }
        return bytes;
    }

    /// <summary>
    /// EVERY ARM IS EXPLICITLY object. Without the cast C# finds a best common type
    /// for the arms - double, since every integral type converts to it - and then
    /// boxes a double. long.MinValue would come back as -9.223372036854776E+18: a
    /// silent precision loss on every 64-bit integer, with nothing failing to build.
    /// </summary>
    private static object ReadScalar(string type, byte[] bigEndian) => type switch
    {
        "int8" => (object)(sbyte)bigEndian[0],
        "uint8" => (object)bigEndian[0],
        "int16" => (object)BinaryPrimitives.ReadInt16BigEndian(bigEndian),
        "uint16" => (object)BinaryPrimitives.ReadUInt16BigEndian(bigEndian),
        "int32" => (object)BinaryPrimitives.ReadInt32BigEndian(bigEndian),
        "uint32" => (object)BinaryPrimitives.ReadUInt32BigEndian(bigEndian),
        "int64" => (object)BinaryPrimitives.ReadInt64BigEndian(bigEndian),
        "uint64" => (object)BinaryPrimitives.ReadUInt64BigEndian(bigEndian),
        "float32" => (object)BinaryPrimitives.ReadSingleBigEndian(bigEndian),
        "float64" => (object)BinaryPrimitives.ReadDoubleBigEndian(bigEndian),
        _ => throw new InvalidOperationException("Unreachable: the codec table admitted an unknown type.")
    };

    private static string Canonical(string type, object? value) => value switch
    {
        null => "null",
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static string? DecodeText(byte[] slice, string encoding, out DecodeDiagnostic? diagnostic)
    {
        diagnostic = null;

        // A NUL terminator ends the string; the remainder of the fixed field is padding
        // and is not part of the value.
        var end = Array.IndexOf(slice, (byte)0);
        var content = end >= 0 ? slice.AsSpan(0, end).ToArray() : slice;

        if (encoding == "ascii")
        {
            return DecodeAscii(content, out diagnostic);
        }

        try
        {
            if (encoding == "latin1")
            {
                return Encoding.Latin1.GetString(content);
            }

            if (encoding == "utf8")
            {
                return new UTF8Encoding(false, true).GetString(content);
            }
        }
        catch (DecoderFallbackException)
        {
            diagnostic = new DecodeDiagnostic(DecodeCodes.ValueNotDecodable,
                "the bytes are not valid " + encoding + "; they are never reinterpreted as another encoding.");
            return null;
        }

        diagnostic = new DecodeDiagnostic(DecodeCodes.CodecUnsupported,
            "encoding '" + encoding + "' has no decoder here.");
        return null;
    }

    private static string? DecodeAscii(byte[] content, out DecodeDiagnostic? diagnostic)
    {
        diagnostic = null;
        foreach (var b in content)
        {
            if (b > 0x7F)
            {
                diagnostic = new DecodeDiagnostic(DecodeCodes.ValueNotDecodable,
                    "byte 0x" + b.ToString("X2", CultureInfo.InvariantCulture) + " is outside ASCII.");
                return null;
            }
        }

        return Encoding.ASCII.GetString(content);
    }

}
