// Source Time Authority persistence contract (backlog reference: T-233).
//
// The durable shape of the frozen analytics-kernel contract, and nothing more. A
// declaration is validated by the kernel registry itself before it is written, so
// the product has exactly one idea of what a lawful time declaration is. This file
// adds only what persistence needs: an effective window and provenance.
//
// Enumerations travel as their exact member names. A numeric string is refused,
// because "1" is not a declaration anybody made on purpose.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Analytics.Core.Kernel;

namespace PlantProcess.Application.Temporal;

/// <summary>What a caller submits. The contract fields, an effective window and provenance.</summary>
public sealed record SourceTimeAuthorityDeclarationRequest(
    string? SourceKey,
    string? SignalKey,
    string? Role,
    string? OffsetOrigin,
    TimeSpan FixedOffset,
    string? ZoneKey,
    TimeSpan Resolution,
    TimeSpan MaxClockSkew,
    string? UncertaintyConvention,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    Guid? CreatedBy,
    string? SourceSystem,
    string? SourceRecordId);

/// <summary>What the product answers. The contract fields, the derived uncertainty, the window.</summary>
public sealed record SourceTimeAuthorityResponse(
    Guid Id,
    string SourceKey,
    string SignalKey,
    string Role,
    string OffsetOrigin,
    TimeSpan FixedOffset,
    string ZoneKey,
    TimeSpan Resolution,
    TimeSpan MaxClockSkew,
    string UncertaintyConvention,
    TimeSpan Uncertainty,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc);

/// <summary>A persisted declaration, already proven lawful by the kernel registry.</summary>
public sealed record PersistedTimeSignalDeclaration(
    Guid Id,
    TimeSignalDeclaration Declaration,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc)
{
    public SourceTimeAuthorityResponse ToResponse() => new(
        Id,
        Declaration.SourceKey,
        Declaration.SignalKey,
        Declaration.Role.ToString(),
        Declaration.OffsetOrigin.ToString(),
        Declaration.FixedOffset,
        Declaration.ZoneKey,
        Declaration.Resolution,
        Declaration.MaxClockSkew,
        Declaration.UncertaintyConvention.ToString(),
        Declaration.Uncertainty,
        EffectiveFromUtc,
        EffectiveToUtc);
}

/// <summary>The outcome of a declaration attempt: an identity, or a stable refusal code.</summary>
public sealed record SourceTimeAuthorityWriteResult(bool IsAccepted, Guid? Id, string Code, string Detail)
{
    public static SourceTimeAuthorityWriteResult Accepted(Guid id) => new(true, id, string.Empty, string.Empty);

    public static SourceTimeAuthorityWriteResult Refused(string code, string detail) => new(false, null, code, detail);
}

public static class SourceTimeAuthorityPersistenceCodes
{
    /// <summary>A persistence-only refusal: the effective window itself is unusable.</summary>
    public const string EffectiveWindowInvalid = "STP01 effective_window_invalid";
}

public static class SourceTimeAuthorityDeclarations
{
    /// <summary>
    /// Turn a request into the exact contract value the kernel would hold, or refuse
    /// with the kernel's own code. The kernel registry performs the validation.
    /// </summary>
    public static bool TryNormalise(
        SourceTimeAuthorityDeclarationRequest request,
        out TimeSignalDeclaration? declaration,
        out string code)
    {
        ArgumentNullException.ThrowIfNull(request);
        declaration = null;

        if (!TryParseMember<TimeRole>(request.Role, out var role))
        {
            code = SourceTimeCodes.RoleNotAuthorised;
            return false;
        }

        if (!TryParseMember<TimeOffsetOrigin>(request.OffsetOrigin, out var origin))
        {
            code = SourceTimeCodes.OffsetNotDeclared;
            return false;
        }

        if (!TryParseMember<TimeUncertaintyConvention>(request.UncertaintyConvention, out var convention))
        {
            code = SourceTimeCodes.TimeQualityNotDeclared;
            return false;
        }

        if (request.EffectiveToUtc.HasValue && request.EffectiveToUtc.Value <= request.EffectiveFromUtc)
        {
            code = SourceTimeAuthorityPersistenceCodes.EffectiveWindowInvalid;
            return false;
        }

        var candidate = new TimeSignalDeclaration(
            request.SourceKey ?? string.Empty,
            request.SignalKey ?? string.Empty,
            role,
            origin,
            request.FixedOffset,
            request.ZoneKey ?? string.Empty,
            request.Resolution,
            request.MaxClockSkew,
            convention);

        var registry = new SourceTimeAuthorityRegistry();
        if (!registry.TryDeclareSignal(candidate, out code))
        {
            return false;
        }

        if (!registry.TryGetSignal(candidate.SourceKey, candidate.SignalKey, out var normalised) || normalised is null)
        {
            code = SourceTimeCodes.SignalNotDeclared;
            return false;
        }

        declaration = normalised;
        code = string.Empty;
        return true;
    }

    /// <summary>
    /// Build the registry in force from persisted declarations. A stored row the
    /// kernel would refuse is not quietly skipped: the load fails closed.
    /// </summary>
    public static SourceTimeAuthorityRegistry ToRegistry(IEnumerable<PersistedTimeSignalDeclaration> persisted)
    {
        ArgumentNullException.ThrowIfNull(persisted);

        var registry = new SourceTimeAuthorityRegistry();
        foreach (var row in persisted.OrderBy(r => r.EffectiveFromUtc).ThenBy(r => r.Id))
        {
            if (!registry.TryDeclareSignal(row.Declaration, out var code))
            {
                throw new InvalidOperationException(
                    "Persisted source time declaration " + row.Id + " is refused by the contract: " + code);
            }
        }

        return registry;
    }

    /// <summary>Exact member name only. Numeric text and Undeclared-by-default are refused.</summary>
    public static bool TryParseMember<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(name, trimmed, StringComparison.Ordinal))
            {
                value = Enum.Parse<TEnum>(name);
                return true;
            }
        }

        return false;
    }
}

/// <summary>Loads the exact tenant-scoped declarations in force for projection.</summary>
public interface ISourceTimeAuthorityRegistryProvider
{
    Task<SourceTimeAuthorityRegistry> LoadRegistryAsync(Guid tenantId, DateTime asOfUtc, CancellationToken cancellationToken);
}

/// <summary>One shared persisted-row decoder for every source-time reader.</summary>
public static class SourceTimeAuthorityPersistenceRow
{
    public static PersistedTimeSignalDeclaration Read(DbDataReader reader)
    {
        var id = reader.GetGuid(0);
        if (!SourceTimeAuthorityDeclarations.TryParseMember<TimeRole>(reader.GetString(3), out var role) ||
            !SourceTimeAuthorityDeclarations.TryParseMember<TimeOffsetOrigin>(reader.GetString(4), out var origin) ||
            !SourceTimeAuthorityDeclarations.TryParseMember<TimeUncertaintyConvention>(reader.GetString(9), out var convention))
            throw new InvalidOperationException("Stored source time declaration " + id + " carries an unknown contract member.");

        var declaration = new TimeSignalDeclaration(
            reader.GetString(1), reader.GetString(2), role, origin,
            TimeSpan.FromTicks(reader.GetInt64(5)), reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            TimeSpan.FromTicks(reader.GetInt64(7)), TimeSpan.FromTicks(reader.GetInt64(8)), convention);
        var from = DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc);
        DateTime? to = reader.IsDBNull(11) ? null : DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc);
        return new PersistedTimeSignalDeclaration(id, declaration, from, to);
    }
}

public sealed record SourceTimeConsumerResolution(
    bool IsResolved, DateTime? UtcValue, string Code, string Detail, bool IsSyntaxFailure);

/// <summary>
/// Converts the raw source representation to RawSourceTime without borrowing host locale/timezone,
/// then delegates every authority decision to SourceTimeAuthorityKernel.
/// </summary>
public static class SourceTimeRuntimeResolver
{
    private static readonly string[] LocalFormats =
    {
        "yyyy-MM-ddTHH:mm:ss.fffffff", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd", "dd/MM/yyyy HH:mm:ss.fff", "dd/MM/yyyy HH:mm:ss",
        "dd/MM/yyyy", "d/M/yyyy HH:mm:ss", "d/M/yyyy", "MM/dd/yyyy HH:mm:ss.fff",
        "MM/dd/yyyy HH:mm:ss", "MM/dd/yyyy", "M/d/yyyy HH:mm:ss", "M/d/yyyy",
        "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy"
    };

    public static SourceTimeConsumerResolution Resolve(
        SourceTimeAuthorityRegistry registry,
        string? sourceKey,
        string? signalKey,
        string? rawText,
        TimeRole? requiredRole = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (!registry.TryGetSignal(sourceKey, signalKey, out var declaration) || declaration is null)
            return Refused(SourceTimeCodes.SignalNotDeclared, "No source-time declaration is in force for this source and signal.");
        if (string.IsNullOrWhiteSpace(rawText))
            return Syntax("The governed timestamp value is empty.");

        if (!TryRaw(declaration, rawText.Trim(), out var raw, out var parseDetail, out var authorityCode))
        {
            if (!string.IsNullOrEmpty(authorityCode)) return Refused(authorityCode, parseDetail);
            return Syntax(parseDetail);
        }

        var resolved = requiredRole.HasValue
            ? SourceTimeAuthorityKernel.ResolveAs(registry, sourceKey, signalKey, requiredRole.Value, raw!)
            : SourceTimeAuthorityKernel.Resolve(registry, sourceKey, signalKey, raw!);
        return resolved.IsResolved
            ? new SourceTimeConsumerResolution(true, resolved.Instant!.Instant.UtcDateTime, resolved.Code, string.Empty, false)
            : Refused(resolved.Code, "The Source Time Authority refused the timestamp: " + resolved.Code);
    }

    private static bool TryRaw(
        TimeSignalDeclaration declaration, string text, out RawSourceTime? raw, out string detail, out string authorityCode)
    {
        raw = null; detail = string.Empty; authorityCode = string.Empty;

        if (HasEmbeddedOffset(text))
        {
            if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dto))
            { detail = "The timestamp carries an offset marker but is not a valid offset-bearing timestamp."; return false; }
            raw = RawSourceTime.WithEmbeddedOffset(dto);
            return true;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole))
        {
            if (whole > 10_000_000_000)
            { raw = RawSourceTime.WithEmbeddedOffset(DateTimeOffset.FromUnixTimeMilliseconds(whole)); return true; }
            if (whole > 1_000_000_000)
            { raw = RawSourceTime.WithEmbeddedOffset(DateTimeOffset.FromUnixTimeSeconds(whole)); return true; }
            if (whole > 20_000 && whole < 80_000)
            { return FromLocal(declaration, DateTime.SpecifyKind(DateTime.FromOADate(whole), DateTimeKind.Unspecified), out raw, out detail, out authorityCode); }
        }
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var oa) && oa > 20_000 && oa < 80_000)
            return FromLocal(declaration, DateTime.SpecifyKind(DateTime.FromOADate(oa), DateTimeKind.Unspecified), out raw, out detail, out authorityCode);

        if (!DateTime.TryParseExact(text, LocalFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) &&
            !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out local))
        { detail = "The timestamp cannot be parsed without inventing a locale or offset."; return false; }

        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return FromLocal(declaration, local, out raw, out detail, out authorityCode);
    }

    private static bool FromLocal(TimeSignalDeclaration declaration, DateTime local, out RawSourceTime? raw, out string detail, out string authorityCode)
    {
        raw = null; detail = string.Empty; authorityCode = string.Empty;
        if (declaration.OffsetOrigin != TimeOffsetOrigin.DeclaredZoneRule)
        { raw = RawSourceTime.WithoutOffset(local); return true; }

        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(declaration.ZoneKey); }
        catch (TimeZoneNotFoundException)
        { authorityCode = SourceTimeCodes.ZoneRuleOffsetNotSupplied; detail = "Declared zone '" + declaration.ZoneKey + "' is not available to the runtime."; return false; }
        catch (InvalidTimeZoneException)
        { authorityCode = SourceTimeCodes.ZoneRuleOffsetNotSupplied; detail = "Declared zone '" + declaration.ZoneKey + "' is invalid on this runtime."; return false; }

        if (zone.IsAmbiguousTime(local)) { raw = RawSourceTime.AmbiguousUnderZoneRules(local); return true; }
        if (zone.IsInvalidTime(local))
        { authorityCode = SourceTimeCodes.ZoneRuleOffsetNotSupplied; detail = "The local timestamp does not exist under the declared zone rules."; return false; }
        raw = RawSourceTime.WithRuntimeResolvedOffset(local, zone.GetUtcOffset(local), false);
        return true;
    }

    private static bool HasEmbeddedOffset(string value) =>
        value.EndsWith("Z", StringComparison.OrdinalIgnoreCase) ||
        Regex.IsMatch(value, @"[+-]\d{2}:?\d{2}$", RegexOptions.CultureInvariant);

    private static SourceTimeConsumerResolution Refused(string code, string detail) => new(false, null, code, detail, false);
    private static SourceTimeConsumerResolution Syntax(string detail) => new(false, null, string.Empty, detail, true);
}