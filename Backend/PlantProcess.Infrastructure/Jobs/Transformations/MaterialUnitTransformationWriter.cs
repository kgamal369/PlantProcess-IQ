using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Application.Jobs.Execution.Transformations;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Jobs.Transformations;

/// <summary>
/// THE ONE COMMISSIONED CANONICAL TARGET FOR GOVERNED TRANSFORMATION EXECUTION.
///
/// MaterialUnit is commissioned because it already holds an enforceable source identity:
/// a filtered unique key over its provenance pair. Every other projection target stays
/// refused until its own identity and idempotency contract is converged by its owner.
///
/// Rows are built through the entity's own constructor and production-window method, so
/// the domain's invariants apply exactly as they do on every other write path. This is not
/// a metadata-driven writer: only the fields listed below have a sanctioned path.
///
/// Semantics, all decided before anything is written:
///   new source identity                    inserted, provenance preserved
///   same identity, same canonical effect   accepted unchanged, same canonical Id
///   same identity, different effect        typed conflict, nothing written
///   business key held by another identity  typed conflict, nothing written
/// Reprojection and supersession are not decided here.
/// </summary>
public sealed class MaterialUnitTransformationWriter : ITransformationCanonicalWriter
{
    private static readonly string TargetName = nameof(MaterialUnit);

    private static readonly HashSet<string> Writable = new(StringComparer.Ordinal)
    {
        nameof(MaterialUnit.MaterialCode),
        nameof(MaterialUnit.MaterialUnitType),
        nameof(MaterialUnit.SiteId),
        nameof(MaterialUnit.ProductFamily),
        nameof(MaterialUnit.GradeOrRecipe),
        nameof(MaterialUnit.ProductionStartUtc),
        nameof(MaterialUnit.ProductionEndUtc),
        nameof(MaterialUnit.PlantTimeZoneId),
        nameof(MaterialUnit.PlantUtcOffsetMinutes),
    };

    private readonly PlantProcessDbContext _db;

    public MaterialUnitTransformationWriter(PlantProcessDbContext db)
    {
        _db = db;
    }

    public bool IsCommissioned(string targetEntity) =>
        string.Equals(targetEntity, TargetName, StringComparison.Ordinal);

    public string? FirstUnwritableField(string targetEntity, IReadOnlyCollection<string> boundFields)
    {
        if (!IsCommissioned(targetEntity))
        {
            return boundFields.OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault() ?? targetEntity;
        }

        foreach (string field in boundFields.OrderBy(f => f, StringComparer.Ordinal))
        {
            if (!Writable.Contains(field)) { return field; }
        }

        return null;
    }

    public async Task<ApplicationResult<CanonicalWriteResult>> WriteAsync(
        CanonicalWriteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsCommissioned(request.TargetEntity))
        {
            return Fail(JobExecutionDiagnosticCodes.CanonicalTargetNotCommissioned,
                request.TargetEntity + " has no commissioned write path.");
        }

        if (request.Rows.Count == 0)
        {
            return ApplicationResult<CanonicalWriteResult>.Success(new CanonicalWriteResult(0, 0));
        }

        var candidates = new List<(CanonicalWriteRow Row, MaterialUnit Unit)>(request.Rows.Count);
        foreach (CanonicalWriteRow row in request.Rows)
        {
            try
            {
                candidates.Add((row, Build(row)));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                          or FormatException or InvalidCastException or OverflowException)
            {
                return Fail(JobExecutionDiagnosticCodes.CanonicalWriteFailed,
                    "Source identity " + row.SourceSystem + " / " + row.SourceRecordId
                    + " cannot become a canonical row: " + ex.Message + " Nothing was written.");
            }
        }

        var systems = candidates.Select(c => c.Row.SourceSystem).Distinct(StringComparer.Ordinal).ToList();
        var records = candidates.Select(c => c.Row.SourceRecordId).Distinct(StringComparer.Ordinal).ToList();

        List<MaterialUnit> byIdentity = await _db.MaterialUnits
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.SourceSystem != null && x.SourceRecordId != null
                        && systems.Contains(x.SourceSystem) && records.Contains(x.SourceRecordId))
            .ToListAsync(cancellationToken);

        var sites = candidates.Select(c => c.Unit.SiteId).Distinct().ToList();
        var codes = candidates.Select(c => c.Unit.MaterialCode).Distinct(StringComparer.Ordinal).ToList();

        List<MaterialUnit> byBusinessKey = await _db.MaterialUnits
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => sites.Contains(x.SiteId) && codes.Contains(x.MaterialCode))
            .ToListAsync(cancellationToken);

        var inserts = new List<MaterialUnit>();
        var claimedKeys = new HashSet<string>(StringComparer.Ordinal);
        int unchanged = 0;

        foreach (var (row, unit) in candidates)
        {
            MaterialUnit? existing = byIdentity.FirstOrDefault(x =>
                string.Equals(x.SourceSystem, row.SourceSystem, StringComparison.Ordinal)
                && string.Equals(x.SourceRecordId, row.SourceRecordId, StringComparison.Ordinal));

            if (existing is not null)
            {
                if (!existing.IsDeleted && SameEffect(existing, unit))
                {
                    unchanged++;
                    continue;
                }

                return Fail(JobExecutionDiagnosticCodes.CanonicalIdentityConflict,
                    "Source identity " + row.SourceSystem + " / " + row.SourceRecordId
                    + " already holds canonical row " + existing.Id + " with a different effect. "
                    + "Reprojection is not decided by this runtime, so nothing was written.");
            }

            string businessKey = unit.SiteId.ToString("N") + "\u001f" + unit.MaterialCode;
            bool heldElsewhere = byBusinessKey.Any(x => x.SiteId == unit.SiteId
                && string.Equals(x.MaterialCode, unit.MaterialCode, StringComparison.Ordinal));

            if (heldElsewhere || !claimedKeys.Add(businessKey))
            {
                return Fail(JobExecutionDiagnosticCodes.CanonicalIdentityConflict,
                    "The canonical business key of source identity " + row.SourceSystem + " / "
                    + row.SourceRecordId + " is already held by another identity. Nothing was written.");
            }

            inserts.Add(unit);
        }

        if (inserts.Count > 0)
        {
            _db.MaterialUnits.AddRange(inserts);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                foreach (MaterialUnit unit in inserts)
                {
                    _db.Entry(unit).State = EntityState.Detached;
                }

                if (ex is OperationCanceledException) { throw; }

                return Fail(JobExecutionDiagnosticCodes.CanonicalWriteFailed,
                    "The canonical write was refused by the database: " + ex.GetBaseException().Message);
            }
        }

        return ApplicationResult<CanonicalWriteResult>.Success(new CanonicalWriteResult(inserts.Count, unchanged));
    }

    private static MaterialUnit Build(CanonicalWriteRow row)
    {
        IReadOnlyDictionary<string, object?> f = row.Fields;

        string code = TextOf(f, nameof(MaterialUnit.MaterialCode)) ?? string.Empty;
        string unitType = TextOf(f, nameof(MaterialUnit.MaterialUnitType)) ?? string.Empty;
        Guid site = GuidOf(f, nameof(MaterialUnit.SiteId)) ?? Guid.Empty;
        string? family = TextOf(f, nameof(MaterialUnit.ProductFamily));
        string? recipe = TextOf(f, nameof(MaterialUnit.GradeOrRecipe));
        DateTime? start = DateOf(f, nameof(MaterialUnit.ProductionStartUtc));
        DateTime? end = DateOf(f, nameof(MaterialUnit.ProductionEndUtc));
        string? zone = TextOf(f, nameof(MaterialUnit.PlantTimeZoneId));
        int? offset = IntOf(f, nameof(MaterialUnit.PlantUtcOffsetMinutes));

        if (!start.HasValue && (end.HasValue || zone is not null || offset.HasValue))
        {
            throw new InvalidOperationException(
                "a production end, zone or offset was bound without a production start, and the domain "
                + "applies those values only with a production window.");
        }

        var unit = new MaterialUnit(
            code,
            unitType,
            site,
            family,
            recipe,
            isSynthetic: false,
            sourceSystem: row.SourceSystem,
            sourceRecordId: row.SourceRecordId);

        if (start.HasValue)
        {
            unit.SetProductionWindow(
                start.Value,
                end,
                offset.HasValue ? TimeSpan.FromMinutes(offset.Value) : null,
                zone ?? string.Empty);
        }

        return unit;
    }

    private static bool SameEffect(MaterialUnit a, MaterialUnit b) =>
        string.Equals(a.MaterialCode, b.MaterialCode, StringComparison.Ordinal)
        && string.Equals(a.MaterialUnitType, b.MaterialUnitType, StringComparison.Ordinal)
        && a.SiteId == b.SiteId
        && string.Equals(a.ProductFamily, b.ProductFamily, StringComparison.Ordinal)
        && string.Equals(a.GradeOrRecipe, b.GradeOrRecipe, StringComparison.Ordinal)
        && a.ProductionStartUtc == b.ProductionStartUtc
        && a.ProductionEndUtc == b.ProductionEndUtc
        && string.Equals(a.PlantTimeZoneId, b.PlantTimeZoneId, StringComparison.Ordinal)
        && a.PlantUtcOffsetMinutes == b.PlantUtcOffsetMinutes;

    private static object? ValueOf(IReadOnlyDictionary<string, object?> fields, string name)
    {
        object? value;
        if (!fields.TryGetValue(name, out value)) { return null; }
        return value is DBNull ? null : value;
    }

    private static string? TextOf(IReadOnlyDictionary<string, object?> fields, string name)
    {
        object? value = ValueOf(fields, name);
        if (value is null) { return null; }
        if (value is string s) { return s; }
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static Guid? GuidOf(IReadOnlyDictionary<string, object?> fields, string name)
    {
        object? value = ValueOf(fields, name);
        if (value is null) { return null; }
        if (value is Guid g) { return g; }
        if (value is string s) { return Guid.Parse(s.Trim()); }
        throw new InvalidCastException("a value bound to an identifier field is not an identifier.");
    }

    private static DateTime? DateOf(IReadOnlyDictionary<string, object?> fields, string name)
    {
        object? value = ValueOf(fields, name);
        if (value is null) { return null; }
        if (value is DateTime d) { return d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime(); }
        if (value is DateTimeOffset o) { return o.UtcDateTime; }
        if (value is string s)
        {
            return DateTime.Parse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        throw new InvalidCastException("a value bound to an instant field is not an instant.");
    }

    private static int? IntOf(IReadOnlyDictionary<string, object?> fields, string name)
    {
        object? value = ValueOf(fields, name);
        if (value is null) { return null; }
        if (value is int i) { return i; }
        if (value is string s) { return int.Parse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture); }
        if (value is IConvertible) { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
        throw new InvalidCastException("a value bound to a whole-number field is not a whole number.");
    }

    private static ApplicationResult<CanonicalWriteResult> Fail(string code, string message) =>
        ApplicationResult<CanonicalWriteResult>.Failure(
            new ApplicationError(code, message, ApplicationErrorType.BusinessRule));
}
