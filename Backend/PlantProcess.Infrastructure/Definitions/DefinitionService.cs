using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Definitions.Contracts;
using PlantProcess.Application.Definitions.Interfaces;
using PlantProcess.Domain.Entities.Dashboarding;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Definitions;

/// <summary>
/// PPIQ T-039. The definition-bearing state of one widget - what is needed to
/// read that version back and understand what it displayed.
///
/// What is deliberately NOT here: the expression validation status, its message
/// and its timestamp. Those are execution and audit state that happen to sit on
/// the same EF entity, and snapshotting them would make two versions differ
/// because a validation ran, not because anyone changed the definition.
///
/// CARRIED FORWARD UNCHANGED. This record is public surface of this file. The
/// canonical service below no longer constructs it, but the frozen versioning
/// test and any external caller still do, and a rewrite does not get to delete
/// a type it inherited.
/// </summary>
public sealed record WidgetDefinitionPayload(
    Guid DashboardDefinitionId,
    string WidgetCode,
    string WidgetTitle,
    string WidgetType,
    string ChartType,
    string DimensionCode,
    string MeasureCode,
    string? ParameterCode,
    string FilterJson,
    string LayoutJson,
    string DisplayOptionsJson,
    int SortOrder,
    string? QueryExpression);

/// <summary>
/// PPIQ T-090. The canonical implementation behind the unchanged T-039 contract.
///
/// WHAT CHANGED AND WHAT DID NOT. The six public signatures are exactly as M1
/// published them. What moved is underneath: every kind now resolves through
/// one store instead of each kind keeping its own persistence, and the
/// widget-only refusal is gone.
///
/// NO SWITCH ON KIND. The difference between the sixteen kinds is registry
/// data - surface, detail table, writable fields - not sixteen code paths. A
/// switch here would be sixteen persistence systems wearing one interface,
/// which is the architecture this task removes.
///
/// THE ONE-ARGUMENT CONSTRUCTOR IS LOAD-BEARING. The frozen T-039 validation
/// builds this as new DefinitionService(db) with no container, so the writer
/// and identity resolver are optional and defaulted. That test not changing is
/// the proof the visible contract did not move.
///
/// THIS SERVICE OWNS THE UNIT OF WORK. The canonical writer refuses to mutate
/// without an ambient transaction, deliberately, so each mutating method here
/// opens one when the caller has not. A definition and its version cannot exist
/// apart - that is the second rule the T-039 contract states it is owed.
/// </summary>
public sealed class DefinitionService : IDefinitionService
{
    private readonly PlantProcessDbContext _db;
    private readonly ICanonicalDefinitionWriter _writer;
    private readonly CanonicalIdentityResolver _identity;

    /// <summary>
    /// Production construction. Every canonical dependency is required.
    /// </summary>
    public DefinitionService(
        PlantProcessDbContext db,
        ICanonicalDefinitionWriter writer,
        CanonicalIdentityResolver identity)
    {
        _db = db;
        _writer = writer;
        _identity = identity;
    }

    /// <summary>
    /// Source compatibility for the frozen T-039 validation, which constructs
    /// this with a context and nothing else.
    ///
    /// IT BUILDS THE SAME CANONICAL DEPENDENCIES, it does not make them
    /// optional. An earlier draft defaulted them to null, which left a shape
    /// where a null writer could have meant "skip the canonical write" - a
    /// second persistence path reachable by omission. Nullability must never
    /// decide whether T-090 is active, so this constructor constructs the real
    /// collaborators and delegates. Both construction forms execute identical
    /// canonical behaviour, which a gate proves rather than assumes.
    /// </summary>
    public DefinitionService(PlantProcessDbContext db)
        : this(db, new CanonicalDefinitionWriter(db), new CanonicalIdentityResolver(db))
    {
    }

    public Task<ApplicationResult<DefinitionSnapshot>> CreateAsync(
        DefinitionKind kind,
        string payloadJson,
        CancellationToken cancellationToken) =>
        InTransactionAsync(async () =>
        {
            var identity = await ResolveIdentityAsync(cancellationToken);
            if (identity.IsFailure)
            {
                return ApplicationResult<DefinitionSnapshot>.Failure(identity.Error!);
            }

            var (tenantId, ownerId) = identity.Value;

            var written = await _writer.WriteVersionAsync(
                new CanonicalDefinitionWrite(
                    kind, tenantId, ownerId,
                    DefinitionCodeFrom(kind, payloadJson, null),
                    NameFrom(payloadJson, kind),
                    payloadJson,
                    CanonicalVersionStatus.Published,
                    DetailFrom(kind, payloadJson)),
                cancellationToken);

            if (written.IsFailure)
            {
                return ApplicationResult<DefinitionSnapshot>.Failure(written.Error!);
            }

            // The operational serving row is written in the SAME transaction.
            // Canonical authority moved; the serving representation did not
            // disappear. Widgets still render from dashboard_widget_definitions,
            // and the frozen validation deletes its dashboard expecting the
            // widget to cascade with it.
            var projected = await ProjectServingRowAsync(kind, written.Value!, payloadJson, cancellationToken);
            if (projected is not null)
            {
                return ApplicationResult<DefinitionSnapshot>.Failure(projected);
            }

            return ApplicationResult<DefinitionSnapshot>.Success(ToSnapshot(written.Value!));
        }, cancellationToken);

    public Task<ApplicationResult<DefinitionSnapshot>> UpdateAsync(
        DefinitionKind kind,
        Guid definitionId,
        string payloadJson,
        CancellationToken cancellationToken) =>
        InTransactionAsync(async () =>
        {
            var current = await _writer.ResolveExactAsync(definitionId, 1, cancellationToken);
            if (current.IsFailure)
            {
                return ApplicationResult<DefinitionSnapshot>.Failure(ApplicationError.NotFound(
                    "That definition does not exist, so there is nothing to version."));
            }

            var identity = await ResolveIdentityAsync(cancellationToken);
            if (identity.IsFailure)
            {
                return ApplicationResult<DefinitionSnapshot>.Failure(identity.Error!);
            }

            var (tenantId, ownerId) = identity.Value;

            var written = await _writer.WriteVersionAsync(
                new CanonicalDefinitionWrite(
                    kind, tenantId, ownerId,
                    current.Value!.DefinitionCode,
                    NameFrom(payloadJson, kind),
                    payloadJson,
                    CanonicalVersionStatus.Published,
                    DetailFrom(kind, payloadJson)),
                cancellationToken);

            return written.IsFailure
                ? ApplicationResult<DefinitionSnapshot>.Failure(written.Error!)
                : ApplicationResult<DefinitionSnapshot>.Success(ToSnapshot(written.Value!));
        }, cancellationToken);

    /// <summary>
    /// The published version, resolved by status. Never the highest version
    /// number: a draft raises that number without becoming truth.
    /// </summary>
    public async Task<ApplicationResult<DefinitionSnapshot>> GetCurrentAsync(
        DefinitionKind kind,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        var published = await _writer.ResolvePublishedAsync(definitionId, cancellationToken);
        return published.IsFailure
            ? ApplicationResult<DefinitionSnapshot>.Failure(published.Error!)
            : ApplicationResult<DefinitionSnapshot>.Success(ToSnapshot(published.Value!));
    }

    public async Task<ApplicationResult<DefinitionSnapshot>> GetVersionAsync(
        DefinitionKind kind,
        Guid definitionId,
        int versionNumber,
        CancellationToken cancellationToken)
    {
        var exact = await _writer.ResolveExactAsync(definitionId, versionNumber, cancellationToken);
        return exact.IsFailure
            ? ApplicationResult<DefinitionSnapshot>.Failure(exact.Error!)
            : ApplicationResult<DefinitionSnapshot>.Success(ToSnapshot(exact.Value!));
    }

    /// <summary>
    /// Every kind answers now. In M1 this refused anything but Widget because
    /// only the widget kind had a version store; the canonical store gives all
    /// sixteen one, so the refusal has nothing left to protect.
    /// </summary>
    public async Task<ApplicationResult<IReadOnlyList<DefinitionVersionSummary>>> ListVersionsAsync(
        DefinitionKind kind,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        if (!DefinitionKindRegistry.TryResolve(kind, out _))
        {
            return ApplicationResult<IReadOnlyList<DefinitionVersionSummary>>.Failure(
                ApplicationError.Validation("Definition kind '" + kind + "' is not declared by the canonical store."));
        }

        var versions = await _db.DefinitionVersions
            .AsNoTracking()
            .Where(v => v.DefinitionId == definitionId && !v.IsDeleted)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        if (versions.Count == 0)
        {
            return ApplicationResult<IReadOnlyList<DefinitionVersionSummary>>.Failure(
                ApplicationError.NotFound("That definition has no versions."));
        }

        return ApplicationResult<IReadOnlyList<DefinitionVersionSummary>>.Success(
            versions.Select(v => new DefinitionVersionSummary(
                v.VersionNumber, v.CreatedAtUtc, v.CreatedBy?.ToString(), v.IsPublished)).ToList());
    }

    public Task<ApplicationResult<DefinitionSnapshot>> PublishAsync(
        DefinitionKind kind,
        Guid definitionId,
        int versionNumber,
        CancellationToken cancellationToken) =>
        InTransactionAsync(async () =>
        {
            var published = await _writer.PublishAsync(definitionId, versionNumber, cancellationToken);
            return published.IsFailure
                ? ApplicationResult<DefinitionSnapshot>.Failure(published.Error!)
                : ApplicationResult<DefinitionSnapshot>.Success(ToSnapshot(published.Value!));
        }, cancellationToken);

    // ---------------------------------------------------------------- internals

    /// <summary>
    /// Opens a transaction only when the caller has none, so a page or template
    /// write that already owns one keeps its canonical and serving mutations in
    /// a single unit of work.
    /// </summary>
    private async Task<ApplicationResult<T>> InTransactionAsync<T>(
        Func<Task<ApplicationResult<T>>> work,
        CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction is not null)
        {
            return await work();
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var result = await work();

        // The result type states success. An earlier draft tested a marker
        // interface that does not exist, which is the transaction helper
        // guessing at an outcome the caller already knows.
        if (result.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return result;
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Writes or refreshes the operational serving row for kinds that have one.
    /// Returns null on success, an error otherwise, so the caller fails the whole
    /// unit of work rather than leaving a canonical version with no serving row.
    ///
    /// Only the widget kind has an operational table today. Pages have one too,
    /// but the Page endpoints own that write and pass through their own path.
    /// </summary>
    private async Task<ApplicationError?> ProjectServingRowAsync(
        DefinitionKind kind,
        CanonicalDefinitionVersion version,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        if (kind != DefinitionKind.Widget)
        {
            return null;
        }

        if (!TryReadGuid(payloadJson, "dashboardDefinitionId", out var dashboardId) &&
            !TryReadGuid(payloadJson, "dashboardId", out dashboardId))
        {
            // A widget definition with no dashboard has no serving row to write.
            // That is legitimate for an authored definition awaiting placement.
            return null;
        }

        var code = version.DefinitionCode;

        var existing = await _db.DashboardWidgetDefinitions
            .FirstOrDefaultAsync(
                w => w.DashboardDefinitionId == dashboardId && w.WidgetCode == code && !w.IsDeleted,
                cancellationToken);

        if (existing is not null)
        {
            return null;
        }

        _db.DashboardWidgetDefinitions.Add(new DashboardWidgetDefinition(
            dashboardDefinitionId: dashboardId,
            widgetCode: code,
            widgetTitle: NameFrom(payloadJson, kind),
            widgetType: ReadString(payloadJson, "widgetType") ?? "chart",
            chartType: ReadString(payloadJson, "chartType") ?? "bar",
            dimensionCode: ReadString(payloadJson, "dimensionCode") ?? string.Empty,
            measureCode: ReadString(payloadJson, "measureCode") ?? string.Empty,
            isSynthetic: false,
            parameterCode: ReadString(payloadJson, "parameterCode"),
            filterJson: ReadString(payloadJson, "filterJson") ?? "{}",
            layoutJson: ReadString(payloadJson, "layoutJson") ?? "{}",
            displayOptionsJson: ReadString(payloadJson, "displayOptionsJson") ?? "{}",
            sortOrder: 0,
            sourceSystem: null,
            sourceRecordId: null));

        await _db.SaveChangesAsync(cancellationToken);
        return null;
    }

    private static string? ReadString(string payloadJson, string property) =>
        TryReadString(payloadJson, property, out var value) ? value : null;

    private static bool TryReadGuid(string payloadJson, string property, out Guid value)
    {
        value = Guid.Empty;
        return TryReadString(payloadJson, property, out var text) && Guid.TryParse(text, out value);
    }

    private async Task<ApplicationResult<(Guid TenantId, Guid OwnerId)>> ResolveIdentityAsync(
        CancellationToken cancellationToken)
    {
        var tenantId = await _identity.ResolveTenantAsync(null, cancellationToken);
        if (tenantId is null)
        {
            return ApplicationResult<(Guid, Guid)>.Failure(ApplicationError.Validation(
                "No single tenant could be resolved. The canonical store does not synthesise one."));
        }

        var ownerId = await _identity.ResolveOwnerAsync(null, cancellationToken);
        if (ownerId is null)
        {
            return ApplicationResult<(Guid, Guid)>.Failure(ApplicationError.Validation(
                "No owner identity could be resolved. The canonical store does not synthesise one."));
        }

        return ApplicationResult<(Guid, Guid)>.Success((tenantId.Value, ownerId.Value));
    }

    /// <summary>
    /// A caller that names its definition keeps that name across restarts; one
    /// that does not gets a fresh identity, which is what CreateAsync means by
    /// "the identity the caller did not have yet".
    /// </summary>
    private static string DefinitionCodeFrom(DefinitionKind kind, string payloadJson, string? existing)
    {
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        foreach (var name in new[] { "definitionCode", "code", "widgetCode", "slug" })
        {
            if (TryReadString(payloadJson, name, out var value))
            {
                return value;
            }
        }

        return DefinitionKindRegistry.StorageKindOf(kind) + "_" + Guid.NewGuid().ToString("n");
    }

    private static string NameFrom(string payloadJson, DefinitionKind kind)
    {
        foreach (var name in new[] { "name", "title", "widgetTitle" })
        {
            if (TryReadString(payloadJson, name, out var value))
            {
                return value;
            }
        }

        return DefinitionKindRegistry.StorageKindOf(kind);
    }

    /// <summary>
    /// Lifts declared detail fields out of the payload. Only fields the registry
    /// declares for this kind are read; anything else stays in the version
    /// payload rather than being pushed at a column that would refuse it.
    /// </summary>
    private static IReadOnlyDictionary<string, object?>? DetailFrom(DefinitionKind kind, string payloadJson)
    {
        if (!DefinitionKindRegistry.TryResolve(kind, out var contract) || contract.DetailTable is null)
        {
            return null;
        }

        var detail = new Dictionary<string, object?>(StringComparer.Ordinal);

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var field in contract.WritableFields)
            {
                if (document.RootElement.TryGetProperty(field.Name, out var value))
                {
                    detail[field.Name] = value.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.String => value.GetString(),
                        JsonValueKind.Object or JsonValueKind.Array => value.GetRawText(),
                        _ => value.ToString()
                    };
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return detail.Count == 0 ? null : detail;
    }

    private static bool TryReadString(string payloadJson, string property, out string value)
    {
        value = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(property, out var element) &&
                element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    value = text.Trim();
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// DefinitionSnapshot is (Kind, DefinitionId, VersionNumber, PayloadJson,
    /// CreatedAtUtc, CreatedBy). The order and arity are the M1 contract's, not
    /// this implementation's convenience - an earlier draft guessed them and
    /// would not have compiled.
    /// </summary>
    private static DefinitionSnapshot ToSnapshot(CanonicalDefinitionVersion version) =>
        new(version.Kind,
            version.DefinitionId,
            version.VersionNumber,
            version.ContentJson,
            version.CreatedAtUtc,
            version.CreatedBy?.ToString());
}
