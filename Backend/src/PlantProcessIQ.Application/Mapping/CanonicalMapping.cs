using System.Globalization;

namespace PlantProcessIQ.Application.Mapping;

// ---------- type & unit model ----------
public enum TypeCategory { String, Number, Integer, Boolean, DateTime, Unknown }
public enum UnitDimension { None, Length, Temperature, Mass, Pressure, Speed }

/// Dimension-aware unit conversion. Base units: Length=m, Temperature=K, Mass=kg, Pressure=Pa, Speed=m/s.
/// Temperature uses an affine transform (factor + offset to base K).
public static class UnitConverter
{
    private static readonly Dictionary<string, (UnitDimension dim, double factor, double offset)> U =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["m"]=(UnitDimension.Length,1,0), ["cm"]=(UnitDimension.Length,0.01,0),
            ["mm"]=(UnitDimension.Length,0.001,0), ["um"]=(UnitDimension.Length,1e-6,0),
            ["k"]=(UnitDimension.Temperature,1,0), ["c"]=(UnitDimension.Temperature,1,273.15),
            ["degc"]=(UnitDimension.Temperature,1,273.15), ["f"]=(UnitDimension.Temperature,5.0/9.0,255.3722222222222),
            ["kg"]=(UnitDimension.Mass,1,0), ["g"]=(UnitDimension.Mass,0.001,0), ["t"]=(UnitDimension.Mass,1000,0),
            ["pa"]=(UnitDimension.Pressure,1,0), ["kpa"]=(UnitDimension.Pressure,1000,0), ["bar"]=(UnitDimension.Pressure,100000,0),
            ["m/s"]=(UnitDimension.Speed,1,0), ["m/min"]=(UnitDimension.Speed,1.0/60.0,0),
        };

    public static bool IsKnown(string unit) => U.ContainsKey(Norm(unit));
    public static UnitDimension DimensionOf(string unit) => U.TryGetValue(Norm(unit), out var u) ? u.dim : UnitDimension.None;
    public static bool CanConvert(string from, string to) => IsKnown(from) && IsKnown(to) && DimensionOf(from) == DimensionOf(to);

    public static double Convert(double value, string from, string to)
    {
        var f = U[Norm(from)]; var t = U[Norm(to)];
        if (f.dim != t.dim) throw new InvalidOperationException($"Cannot convert {from} ({f.dim}) to {to} ({t.dim}).");
        double baseVal = value * f.factor + f.offset;   // to base unit
        return (baseVal - t.offset) / t.factor;         // from base unit
    }
    private static string Norm(string u) => u.Trim().Replace("\u00B0", "").Replace("\u00BA", "");
}

// ---------- canonical contract ----------
public sealed record CanonicalField(string Name, TypeCategory Type, bool Required,
                                    UnitDimension Unit = UnitDimension.None, string? CanonicalUnit = null);

public sealed class CanonicalEntity
{
    public required string Name { get; init; }
    public required IReadOnlyList<CanonicalField> Fields { get; init; }
    public IEnumerable<CanonicalField> Required => Fields.Where(f => f.Required);
    public CanonicalField? Field(string n) => Fields.FirstOrDefault(f => string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase));
}

/// The heat/slab/coil/quality_event canonical shapes downstream layers consume.
/// INTEGRATE: align field names/units with your canonical schema if they differ.
public static class CanonicalContract
{
    public static readonly CanonicalEntity Heat = new() { Name = "heat", Fields = new CanonicalField[]
    {
        new("heat_id",   TypeCategory.String,   true),
        new("grade",     TypeCategory.String,   true),
        new("ladle_id",  TypeCategory.String,   false),
        new("tundish_id",TypeCategory.String,   false),
        new("mould_id",  TypeCategory.String,   false),
        new("tapped_at", TypeCategory.DateTime, true),
    }};
    public static readonly CanonicalEntity Slab = new() { Name = "slab", Fields = new CanonicalField[]
    {
        new("slab_id",      TypeCategory.String,   true),
        new("heat_id",      TypeCategory.String,   true),
        new("width_mm",     TypeCategory.Number,   true,  UnitDimension.Length, "mm"),
        new("thickness_mm", TypeCategory.Number,   false, UnitDimension.Length, "mm"),
        new("cast_at",      TypeCategory.DateTime, true),
    }};
    public static readonly CanonicalEntity Coil = new() { Name = "coil", Fields = new CanonicalField[]
    {
        new("coil_id",      TypeCategory.String,   true),
        new("heat_id",      TypeCategory.String,   true),
        new("slab_id",      TypeCategory.String,   false),
        new("grade",        TypeCategory.String,   true),
        new("width_mm",     TypeCategory.Number,   true,  UnitDimension.Length,      "mm"),
        new("entry_temp_c", TypeCategory.Number,   false, UnitDimension.Temperature, "c"),
        new("produced_at",  TypeCategory.DateTime, true),
    }};
    public static readonly CanonicalEntity QualityEvent = new() { Name = "quality_event", Fields = new CanonicalField[]
    {
        new("event_id",    TypeCategory.String,   true),
        new("coil_id",     TypeCategory.String,   true),
        new("defect_code", TypeCategory.String,   true),
        new("severity",    TypeCategory.Number,   false),
        new("detected_at", TypeCategory.DateTime, true),
    }};

    public static readonly IReadOnlyDictionary<string, CanonicalEntity> All =
        new Dictionary<string, CanonicalEntity>(StringComparer.OrdinalIgnoreCase)
        { ["heat"]=Heat, ["slab"]=Slab, ["coil"]=Coil, ["quality_event"]=QualityEvent };
}

// ---------- business keys ----------
public sealed class BusinessKeyConflictException : Exception
{
    public BusinessKeyConflictException(string key, string a, string b)
        : base($"CoilId '{key}' resolves two distinct physical coils: '{a}' and '{b}'.") { }
}

public static class BusinessKeys
{
    /// CoilId normalization: trim, upper-case, strip a leading 'C-'/'C' prefix, drop leading zeros.
    public static string NormalizeCoilId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw new ArgumentException("CoilId is empty.", nameof(raw));
        var s = raw.Trim().ToUpperInvariant();
        if (s.StartsWith("C-")) s = s[2..];
        else if (s.StartsWith("C") && s.Length > 1 && char.IsDigit(s[1])) s = s[1..];
        s = s.TrimStart('0');
        return s.Length == 0 ? "0" : s;
    }
}

/// Maps raw source ids to a normalized business key, detecting genuine conflicts.
public sealed class CoilIdResolver
{
    private readonly Dictionary<string, string> _seen = new(StringComparer.Ordinal);
    public string Resolve(string rawId, string physicalRef)
    {
        var key = BusinessKeys.NormalizeCoilId(rawId);
        if (_seen.TryGetValue(key, out var existing) && existing != physicalRef)
            throw new BusinessKeyConflictException(key, existing, physicalRef);
        _seen[key] = physicalRef;
        return key;
    }
}

// ---------- mapping spec, staged sample, ports ----------
public sealed record FieldMapping(string CanonicalField, string SourceColumn, string? SourceUnit = null);

public sealed class MappingSpec
{
    public required string CanonicalEntityName { get; init; }
    public required string SourceTable { get; init; }
    public required IReadOnlyList<FieldMapping> Fields { get; init; }
}

public sealed record StagedColumn(string Name, TypeCategory InferredType);

public sealed class StagedSample
{
    public required IReadOnlyList<StagedColumn> Columns { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows { get; init; }
    public StagedColumn? Column(string n) => Columns.FirstOrDefault(c => string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase));
}

public interface IStagingSampleReader
{
    Task<StagedSample> SampleAsync(string sourceTable, int sampleSize, CancellationToken ct = default);
}

public interface ICanonicalViewStore
{
    Task ApplyViewAsync(string viewName, string selectSql, CancellationToken ct = default);
    Task<bool> ViewExistsAsync(string viewName, CancellationToken ct = default);
}

// ---------- validation result ----------
public enum MappingErrorCode
{
    RequiredFieldUnmapped, UnknownCanonicalEntity, UnknownCanonicalField,
    SourceColumnMissing, IncompatibleType, UnitConversionUnsupported, UnitConversionFailed
}

public sealed record MappingError(MappingErrorCode Code, string Field, string Message);

public sealed class MappingValidationResult
{
    public List<MappingError> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
    public void Add(MappingErrorCode c, string field, string msg) => Errors.Add(new(c, field, msg));
}

// ---------- the validator: required coverage + type compat + unit conversion on real sample rows ----------
public sealed class CanonicalMappingValidator
{
    private readonly IStagingSampleReader _staging;
    private readonly int _sampleSize;
    public CanonicalMappingValidator(IStagingSampleReader staging, int sampleSize = 200)
    { _staging = staging; _sampleSize = sampleSize; }

    public async Task<MappingValidationResult> ValidateAsync(MappingSpec spec, CancellationToken ct = default)
    {
        var res = new MappingValidationResult();

        if (!CanonicalContract.All.TryGetValue(spec.CanonicalEntityName, out var entity))
        {
            res.Add(MappingErrorCode.UnknownCanonicalEntity, spec.CanonicalEntityName,
                $"Unknown canonical entity '{spec.CanonicalEntityName}'. Expected one of: {string.Join(", ", CanonicalContract.All.Keys)}.");
            return res;
        }

        var mapped = new HashSet<string>(spec.Fields.Select(f => f.CanonicalField), StringComparer.OrdinalIgnoreCase);

        // Gate 1 - required-field coverage
        foreach (var rf in entity.Required)
            if (!mapped.Contains(rf.Name))
                res.Add(MappingErrorCode.RequiredFieldUnmapped, rf.Name,
                    $"Required field '{entity.Name}.{rf.Name}' is not mapped.");

        var sample = await _staging.SampleAsync(spec.SourceTable, _sampleSize, ct);

        foreach (var fm in spec.Fields)
        {
            var cf = entity.Field(fm.CanonicalField);
            if (cf is null)
            {
                res.Add(MappingErrorCode.UnknownCanonicalField, fm.CanonicalField,
                    $"'{fm.CanonicalField}' is not a field of canonical entity '{entity.Name}'.");
                continue;
            }

            var sc = sample.Column(fm.SourceColumn);
            if (sc is null)
            {
                res.Add(MappingErrorCode.SourceColumnMissing, cf.Name,
                    $"Source column '{spec.SourceTable}.{fm.SourceColumn}' mapped to '{entity.Name}.{cf.Name}' does not exist.");
                continue;
            }

            // Gate 2 - type compatibility (verified against the sample values, not just declared types)
            if (!IsTypeCompatible(cf.Type, sample, fm.SourceColumn, out var offending))
                res.Add(MappingErrorCode.IncompatibleType, cf.Name,
                    $"Column '{fm.SourceColumn}' (inferred {sc.InferredType}) is not compatible with '{entity.Name}.{cf.Name}' ({cf.Type}). " +
                    (offending is null ? "" : $"Example offending value: '{offending}'."));

            // Gate 3 - unit conversion (only when the canonical field carries a dimension and a source unit is declared)
            if (cf.Unit != UnitDimension.None && fm.SourceUnit is not null && cf.CanonicalUnit is not null)
            {
                if (!UnitConverter.CanConvert(fm.SourceUnit, cf.CanonicalUnit))
                    res.Add(MappingErrorCode.UnitConversionUnsupported, cf.Name,
                        $"No conversion from '{fm.SourceUnit}' to canonical unit '{cf.CanonicalUnit}' for '{entity.Name}.{cf.Name}'.");
                else if (!VerifyConversionOnSample(sample, fm.SourceColumn, fm.SourceUnit, cf.CanonicalUnit, out var why))
                    res.Add(MappingErrorCode.UnitConversionFailed, cf.Name,
                        $"Unit conversion '{fm.SourceUnit}'->'{cf.CanonicalUnit}' failed on sample for '{cf.Name}': {why}.");
            }
        }
        return res;
    }

    private static bool IsTypeCompatible(TypeCategory target, StagedSample s, string col, out string? offending)
    {
        offending = null;
        if (target == TypeCategory.String) return true;  // anything renders to text
        foreach (var row in s.Rows)
        {
            row.TryGetValue(col, out var raw);
            if (string.IsNullOrWhiteSpace(raw)) continue;
            bool ok = target switch
            {
                TypeCategory.Number   => double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out _),
                TypeCategory.Integer  => long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
                TypeCategory.Boolean  => bool.TryParse(raw, out _) || raw is "0" or "1",
                TypeCategory.DateTime => DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _),
                _ => true
            };
            if (!ok) { offending = raw; return false; }
        }
        return true;
    }

    private static bool VerifyConversionOnSample(StagedSample s, string col, string from, string to, out string why)
    {
        why = "";
        foreach (var row in s.Rows)
        {
            row.TryGetValue(col, out var raw);
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            { why = $"value '{raw}' is not numeric"; return false; }
            var converted = UnitConverter.Convert(v, from, to);
            if (double.IsNaN(converted) || double.IsInfinity(converted))
            { why = $"value '{raw}' converted to a non-finite number"; return false; }
        }
        return true;
    }
}

// ---------- on success: emit the canonical read view downstream consumes ----------
public sealed class CanonicalViewEmitter
{
    private readonly ICanonicalViewStore _store;
    public CanonicalViewEmitter(ICanonicalViewStore store) => _store = store;

    public string BuildSelect(MappingSpec spec, CanonicalEntity entity)
    {
        var cols = new List<string>();
        foreach (var fm in spec.Fields)
        {
            var cf = entity.Field(fm.CanonicalField)!;
            string expr = QuoteIdent(fm.SourceColumn);
            if (cf.Unit != UnitDimension.None && fm.SourceUnit is not null && cf.CanonicalUnit is not null
                && !string.Equals(fm.SourceUnit, cf.CanonicalUnit, StringComparison.OrdinalIgnoreCase))
                expr = UnitSqlExpression(fm.SourceColumn, fm.SourceUnit, cf.CanonicalUnit);
            cols.Add($"{expr} AS {QuoteIdent(cf.Name)}");
        }
        return $"SELECT {string.Join(", ", cols)} FROM {QualifiedSource(spec.SourceTable)}";
    }

    public async Task EmitAsync(MappingSpec spec, CancellationToken ct = default)
    {
        var entity = CanonicalContract.All[spec.CanonicalEntityName];
        await _store.ApplyViewAsync($"canon.{entity.Name}_v", BuildSelect(spec, entity), ct);
    }

    // affine unit conversion expressed inline as SQL arithmetic, mirroring UnitConverter exactly
    private static string UnitSqlExpression(string col, string from, string to)
    {
        var (ff, fo) = AffineToBase(from);
        var (tf, to2) = AffineToBase(to);
        string c = QuoteIdent(col);
        return $"((({c})::numeric * {Lit(ff)} + {Lit(fo)}) - {Lit(to2)}) / {Lit(tf)}";
    }
    private static (double factor, double offset) AffineToBase(string unit) => unit.Trim().ToLowerInvariant() switch
    {
        "m"=>(1,0), "cm"=>(0.01,0), "mm"=>(0.001,0), "um"=>(1e-6,0),
        "k"=>(1,0), "c" or "degc"=>(1,273.15), "f"=>(5.0/9.0,255.3722222222222),
        "kg"=>(1,0), "g"=>(0.001,0), "t"=>(1000,0),
        "pa"=>(1,0), "kpa"=>(1000,0), "bar"=>(100000,0),
        "m/s"=>(1,0), "m/min"=>(1.0/60.0,0),
        _ => throw new InvalidOperationException($"No SQL unit affine for '{unit}'.")
    };
    private static string Lit(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    private static string QuoteIdent(string id) => "\"" + id.Replace("\"", "\"\"") + "\"";
    private static string QualifiedSource(string t) => t.Contains('.') ? t : "staging." + t;
}