using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Definitions.Contracts;
using PlantProcess.Application.Definitions.Interfaces;
using PlantProcess.Domain.Entities.Dashboarding;
using PlantProcess.Domain.Entities.Definitions;
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
/// PPIQ T-039. THE M1 COMPATIBILITY ADAPTER BEHIND IDefinitionService.
///
/// Widget is the only kind M1 versions, and every other kind is REFUSED rather
/// than answered with a synthesised version one. A fabricated history is worse
/// than an absent one: it reads as a fact and is not.
///
/// The atomicity rule is not a comment here, it is the control flow. Every
/// write path opens one transaction, locks the operational row, allocates the
/// number under that lock, writes both the current definition and the snapshot,
/// and only then commits. A successful update without its version row cannot
/// exist, because there is no path that commits one without the other.
/// </summary>
public sealed class DefinitionService : IDefinitionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PlantProcessDbContext _db;

    public DefinitionService(PlantProcessDbContext db)
    {
        _db = db;
    }

    private static ApplicationResult<T> OnlyWidget<T>(DefinitionKind kind)
    {
        return ApplicationResult<T>.Failure(ApplicationError.Validation(
            "This build stores versions for the widget kind only. The " + kind
            + " kind has no version adapter yet, and answering with a version that was never"
            + " written would be a fabricated history."));
    }

    private static ApplicationResult<T> BadPayload<T>()
    {
        return ApplicationResult<T>.Failure(ApplicationError.Validation(
            "The definition payload could not be read as a widget definition."
            + " It must carry the dashboard, the widget code and title, the chart type,"
            + " and the dimension and measure the widget displays."));
    }

    private static WidgetDefinitionPayload? Parse(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) { return null; }
        try
        {
            var parsed = JsonSerializer.Deserialize<WidgetDefinitionPayload>(payloadJson, JsonOptions);
            if (parsed is null) { return null; }
            if (parsed.DashboardDefinitionId == Guid.Empty) { return null; }
            if (string.IsNullOrWhiteSpace(parsed.WidgetCode)) { return null; }
            if (string.IsNullOrWhiteSpace(parsed.WidgetTitle)) { return null; }
            return parsed;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string PayloadOf(DashboardWidgetDefinition widget)
    {
        var payload = new WidgetDefinitionPayload(
            widget.DashboardDefinitionId,
            widget.WidgetCode,
            widget.WidgetTitle,
            widget.WidgetType,
            widget.ChartType,
            widget.DimensionCode,
            widget.MeasureCode,
            widget.ParameterCode,
            widget.FilterJson,
            widget.LayoutJson,
            widget.DisplayOptionsJson,
            widget.SortOrder,
            widget.QueryExpression);

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static DefinitionSnapshot ToSnapshot(DefinitionVersion row)
    {
        return new DefinitionSnapshot(
            DefinitionKind.Widget,
            row.DefinitionId,
            row.VersionNumber,
            row.PayloadJson,
            row.CreatedAtUtc,
            row.CreatedBy);
    }

    private const string WidgetKind = nameof(DefinitionKind.Widget);

    /// <summary>
    /// Allocates the next version number and writes the snapshot. MUST be
    /// called inside a transaction that already holds the row lock taken by
    /// <see cref="LockWidgetAsync"/>; the unique index is the backstop if a
    /// caller ever forgets.
    /// </summary>
    private async Task<DefinitionVersion> AppendSnapshotAsync(
        DashboardWidgetDefinition widget, CancellationToken cancellationToken)
    {
        var highest = await _db.DefinitionVersions
            .Where(v => v.DefinitionKind == WidgetKind && v.DefinitionId == widget.Id)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var row = new DefinitionVersion(WidgetKind, widget.Id, highest + 1, PayloadOf(widget), null);
        _db.DefinitionVersions.Add(row);
        return row;
    }

    /// <summary>
    /// The smallest locking mechanism the current persistence already offers: a
    /// row lock on the operational definition, taken inside the write
    /// transaction. It serialises concurrent writers of THIS widget and nothing
    /// else, which is exactly the scope the version sequence needs.
    /// </summary>
    private async Task LockWidgetAsync(Guid widgetId, CancellationToken cancellationToken)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM public.dashboard_widget_definitions WHERE id = {widgetId} FOR UPDATE",
            cancellationToken);
    }

    public async Task<ApplicationResult<DefinitionSnapshot>> CreateAsync(
        DefinitionKind kind, string payloadJson, CancellationToken cancellationToken)
    {
        if (kind != DefinitionKind.Widget) { return OnlyWidget<DefinitionSnapshot>(kind); }

        var parsed = Parse(payloadJson);
        if (parsed is null) { return BadPayload<DefinitionSnapshot>(); }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var widget = new DashboardWidgetDefinition(
            parsed.DashboardDefinitionId,
            parsed.WidgetCode,
            parsed.WidgetTitle,
            parsed.WidgetType,
            parsed.ChartType,
            parsed.DimensionCode,
            parsed.MeasureCode,
            false,
            parsed.ParameterCode,
            parsed.FilterJson,
            parsed.LayoutJson,
            parsed.DisplayOptionsJson,
            parsed.SortOrder);

        if (!string.IsNullOrWhiteSpace(parsed.QueryExpression))
        {
            // The expression is definition-bearing, so it is written. Its
            // validation state is not, so it is left where a validation run
            // will set it.
            widget.ConfigureExpression(
                parsed.QueryExpression, null, 1, false, WidgetExpressionStatus.Pending, null);
        }

        _db.DashboardWidgetDefinitions.Add(widget);
        await _db.SaveChangesAsync(cancellationToken);

        var snapshot = await AppendSnapshotAsync(widget, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ApplicationResult<DefinitionSnapshot>.Success(ToSnapshot(snapshot));
    }

    public async Task<ApplicationResult<DefinitionSnapshot>> UpdateAsync(
        DefinitionKind kind, Guid definitionId, string payloadJson, CancellationToken cancellationToken)
    {
        if (kind != DefinitionKind.Widget) { return OnlyWidget<DefinitionSnapshot>(kind); }

        var parsed = Parse(payloadJson);
        if (parsed is null) { return BadPayload<DefinitionSnapshot>(); }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockWidgetAsync(definitionId, cancellationToken);

        var widget = await _db.DashboardWidgetDefinitions
            .FirstOrDefaultAsync(w => w.Id == definitionId, cancellationToken);
        if (widget is null)
        {
            return ApplicationResult<DefinitionSnapshot>.Failure(
                ApplicationError.NotFound("That widget definition does not exist."));
        }

        widget.UpdateDefinition(
            parsed.WidgetTitle,
            parsed.WidgetType,
            parsed.ChartType,
            parsed.DimensionCode,
            parsed.MeasureCode,
            parsed.ParameterCode,
            parsed.FilterJson,
            parsed.DisplayOptionsJson);
        widget.UpdateLayout(parsed.LayoutJson, parsed.SortOrder);
        widget.ConfigureExpression(
            parsed.QueryExpression, null, 1, false, WidgetExpressionStatus.Pending, null);

        var snapshot = await AppendSnapshotAsync(widget, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ApplicationResult<DefinitionSnapshot>.Success(ToSnapshot(snapshot));
    }

    public async Task<ApplicationResult<DefinitionSnapshot>> GetCurrentAsync(
        DefinitionKind kind, Guid definitionId, CancellationToken cancellationToken)
    {
        if (kind != DefinitionKind.Widget) { return OnlyWidget<DefinitionSnapshot>(kind); }

        var widget = await _db.DashboardWidgetDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == definitionId, cancellationToken);
        if (widget is null)
        {
            return ApplicationResult<DefinitionSnapshot>.Failure(
                ApplicationError.NotFound("That widget definition does not exist."));
        }

        var newest = await _db.DefinitionVersions
            .AsNoTracking()
            .Where(v => v.DefinitionKind == WidgetKind && v.DefinitionId == definitionId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        // The CURRENT definition is read from the OPERATIONAL row, not from the
        // newest snapshot. They agree, and reading the operational row is what
        // keeps that true rather than assumed. The version number and timestamp
        // come from the newest snapshot because that is where they are recorded.
        return ApplicationResult<DefinitionSnapshot>.Success(new DefinitionSnapshot(
            DefinitionKind.Widget,
            widget.Id,
            newest?.VersionNumber ?? 0,
            PayloadOf(widget),
            newest?.CreatedAtUtc ?? DateTime.UtcNow,
            newest?.CreatedBy));
    }

    public async Task<ApplicationResult<DefinitionSnapshot>> GetVersionAsync(
        DefinitionKind kind, Guid definitionId, int versionNumber, CancellationToken cancellationToken)
    {
        if (kind != DefinitionKind.Widget) { return OnlyWidget<DefinitionSnapshot>(kind); }

        var row = await _db.DefinitionVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                v => v.DefinitionKind == WidgetKind && v.DefinitionId == definitionId && v.VersionNumber == versionNumber,
                cancellationToken);

        if (row is null)
        {
            return ApplicationResult<DefinitionSnapshot>.Failure(
                ApplicationError.NotFound("Version " + versionNumber + " of that definition was never written."));
        }

        return ApplicationResult<DefinitionSnapshot>.Success(ToSnapshot(row));
    }

    public async Task<ApplicationResult<IReadOnlyList<DefinitionVersionSummary>>> ListVersionsAsync(
        DefinitionKind kind, Guid definitionId, CancellationToken cancellationToken)
    {
        if (kind != DefinitionKind.Widget) { return OnlyWidget<IReadOnlyList<DefinitionVersionSummary>>(kind); }

        var rows = await _db.DefinitionVersions
            .AsNoTracking()
            .Where(v => v.DefinitionKind == WidgetKind && v.DefinitionId == definitionId)
            .OrderBy(v => v.VersionNumber)
            .Select(v => new DefinitionVersionSummary(v.VersionNumber, v.CreatedAtUtc, v.CreatedBy, v.IsPublished))
            .ToListAsync(cancellationToken);

        // No rows is an honest answer. It is not an invitation to invent one.
        return ApplicationResult<IReadOnlyList<DefinitionVersionSummary>>.Success(rows);
    }

    public async Task<ApplicationResult<DefinitionSnapshot>> PublishAsync(
        DefinitionKind kind, Guid definitionId, int versionNumber, CancellationToken cancellationToken)
    {
        if (kind != DefinitionKind.Widget) { return OnlyWidget<DefinitionSnapshot>(kind); }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockWidgetAsync(definitionId, cancellationToken);

        var rows = await _db.DefinitionVersions
            .Where(v => v.DefinitionKind == WidgetKind && v.DefinitionId == definitionId)
            .ToListAsync(cancellationToken);

        var target = rows.FirstOrDefault(v => v.VersionNumber == versionNumber);
        if (target is null)
        {
            return ApplicationResult<DefinitionSnapshot>.Failure(
                ApplicationError.NotFound("Version " + versionNumber + " of that definition was never written."));
        }

        foreach (var row in rows)
        {
            row.MarkPublished(row.VersionNumber == versionNumber);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ApplicationResult<DefinitionSnapshot>.Success(ToSnapshot(target));
    }
}