using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Dashboarding.Interfaces;
using PlantProcess.Domain.Entities.Dashboarding;
using System.Text.Json;
using PlantProcess.Application.Definitions;

namespace PlantProcess.Application.Dashboarding.Services.Dashboards;

public sealed class DashboardDefinitionService : IDashboardDefinitionService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IDashboardWidgetValidationService _validator;
    private readonly IWidgetQueryExpressionService _expressions;

    /// PPIQ T-090. Widget definitions are semantic definitions, so every writer
    /// in this service ends at the canonical store. Before T-090 the ordinary
    /// authoring path and the system-template path both wrote the operational
    /// row directly and nothing recorded a version, which made this class a
    /// second definition authority.
    private readonly ICanonicalDefinitionWriter _canonical;

    /// Identity is resolved through Infrastructure: the Application
    /// project cannot open a database connection and should not.
    private readonly ICanonicalIdentityResolver _identity;

    public DashboardDefinitionService(
        IPlantProcessDbContext dbContext,
        IDashboardWidgetValidationService validator,
        IWidgetQueryExpressionService expressions,
        ICanonicalDefinitionWriter canonical,
        ICanonicalIdentityResolver identity)
    {
        _dbContext = dbContext;
        _validator = validator;
        _expressions = expressions;
        _canonical = canonical;
        _identity = identity;
    }

    public async Task<ApplicationResult<IReadOnlyList<DashboardDefinitionDto>>> GetDashboardsAsync(
        bool includeInactive,
        bool includeSystemTemplates,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.DashboardDefinitions
            .AsNoTracking()
            .Include(x => x.Widgets)
            .Where(x => !x.IsDeleted);

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        if (!includeSystemTemplates)
            query = query.Where(x => !x.IsSystemTemplate);

        var dashboards = await query
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.IsSystemTemplate)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return ApplicationResult<IReadOnlyList<DashboardDefinitionDto>>.Success(
            dashboards.Select(ToDto).ToList());
    }

    public async Task<ApplicationResult<DashboardDefinitionDto>> GetDashboardByIdAsync(
        Guid dashboardDefinitionId,
        CancellationToken cancellationToken)
    {
        var dashboard = await _dbContext.DashboardDefinitions
            .AsNoTracking()
            .Include(x => x.Widgets)
            .Where(x => x.Id == dashboardDefinitionId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (dashboard is null)
            return ApplicationResult<DashboardDefinitionDto>.Failure(
                ApplicationError.NotFound("Dashboard definition was not found."));

        return ApplicationResult<DashboardDefinitionDto>.Success(ToDto(dashboard));
    }

    public async Task<ApplicationResult<Guid>> CreateDashboardAsync(
        CreateDashboardDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateDashboardRequest(request.DashboardCode, request.Name, request.LayoutJson);
        if (errors.Count > 0)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Dashboard definition is invalid.", errors));

        var code = NormalizeRequired(request.DashboardCode);

        var exists = await _dbContext.DashboardDefinitions
            .AnyAsync(x => x.DashboardCode == code && !x.IsDeleted, cancellationToken);

        if (exists)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Conflict($"Dashboard code '{code}' already exists."));

        if (request.IsDefault)
            await ClearDefaultDashboardsAsync(cancellationToken);

        var dashboard = new DashboardDefinition(
            dashboardCode: code,
            name: NormalizeRequired(request.Name),
            isSynthetic: request.IsSynthetic,
            description: NormalizeNullable(request.Description),
            layoutJson: NormalizeJson(request.LayoutJson),
            isDefault: request.IsDefault,
            isSystemTemplate: request.IsSystemTemplate,
            sourceSystem: NormalizeNullable(request.SourceSystem),
            sourceRecordId: NormalizeNullable(request.SourceRecordId));

        _dbContext.DashboardDefinitions.Add(dashboard);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<Guid>.Success(dashboard.Id);
    }

    public async Task<ApplicationResult> UpdateDashboardAsync(
        Guid dashboardDefinitionId,
        UpdateDashboardDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var dashboard = await _dbContext.DashboardDefinitions
            .FirstOrDefaultAsync(x => x.Id == dashboardDefinitionId && !x.IsDeleted, cancellationToken);

        if (dashboard is null)
            return ApplicationResult.Failure(ApplicationError.NotFound("Dashboard definition was not found."));

        if (string.IsNullOrWhiteSpace(request.Name))
            return ApplicationResult.Failure(
                ApplicationError.Validation("Dashboard name is required."));

        dashboard.Rename(request.Name, request.Description);

        if (request.IsDefault == true)
        {
            await ClearDefaultDashboardsAsync(cancellationToken, dashboard.Id);
            dashboard.SetAsDefault();
        }
        else if (request.IsDefault == false)
        {
            dashboard.RemoveDefaultFlag();
        }

        if (request.IsActive == true)
            dashboard.Activate();
        else if (request.IsActive == false)
            dashboard.Deactivate();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> UpdateDashboardLayoutAsync(
        Guid dashboardDefinitionId,
        UpdateDashboardLayoutRequest request,
        CancellationToken cancellationToken)
    {
        var layoutError = ValidateJsonObject(request.LayoutJson, nameof(request.LayoutJson));
        if (layoutError is not null)
            return ApplicationResult.Failure(layoutError);

        var dashboard = await _dbContext.DashboardDefinitions
            .FirstOrDefaultAsync(x => x.Id == dashboardDefinitionId && !x.IsDeleted, cancellationToken);

        if (dashboard is null)
            return ApplicationResult.Failure(ApplicationError.NotFound("Dashboard definition was not found."));

        dashboard.UpdateLayout(request.LayoutJson);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> DeactivateDashboardAsync(
        Guid dashboardDefinitionId,
        CancellationToken cancellationToken)
    {
        var dashboard = await _dbContext.DashboardDefinitions
            .FirstOrDefaultAsync(x => x.Id == dashboardDefinitionId && !x.IsDeleted, cancellationToken);

        if (dashboard is null)
            return ApplicationResult.Failure(ApplicationError.NotFound("Dashboard definition was not found."));

        dashboard.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<Guid>> CreateWidgetAsync(
        Guid dashboardDefinitionId,
        CreateDashboardWidgetDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var dashboardExists = await _dbContext.DashboardDefinitions
            .AnyAsync(x => x.Id == dashboardDefinitionId && !x.IsDeleted && x.IsActive, cancellationToken);

        if (!dashboardExists)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.NotFound("Active dashboard definition was not found."));

        var validation = ValidateWidgetRequest(request);
        if (validation is not null)
            return ApplicationResult<Guid>.Failure(validation);

        var code = NormalizeRequired(request.WidgetCode);

        var exists = await _dbContext.DashboardWidgetDefinitions.AnyAsync(
            x => x.DashboardDefinitionId == dashboardDefinitionId &&
                 x.WidgetCode == code &&
                 !x.IsDeleted,
            cancellationToken);

        if (exists)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Conflict($"Widget code '{code}' already exists in this dashboard."));

        var widget = new DashboardWidgetDefinition(
            dashboardDefinitionId: dashboardDefinitionId,
            widgetCode: code,
            widgetTitle: NormalizeRequired(request.WidgetTitle),
            widgetType: NormalizeRequired(request.WidgetType),
            chartType: NormalizeRequired(request.ChartType),
            dimensionCode: request.DimensionCode?.Trim() ?? string.Empty,
            measureCode: NormalizeRequired(request.MeasureCode),
            isSynthetic: request.IsSynthetic,
            parameterCode: NormalizeNullable(request.ParameterCode),
            filterJson: NormalizeJson(request.FilterJson),
            layoutJson: NormalizeJson(request.LayoutJson),
            displayOptionsJson: NormalizeJson(request.DisplayOptionsJson),
            sortOrder: request.SortOrder ?? 0,
            sourceSystem: NormalizeNullable(request.SourceSystem),
            sourceRecordId: NormalizeNullable(request.SourceRecordId));

        ApplyExpression(widget, request.QueryExpression);

        // T-090. Canonical authority first, operational row second, one unit of
        // work. A widget that exists operationally without a canonical version
        // would be exactly the dual authority this task removes.
        var refusal = await WidgetCanonicalConvergence.WriteForRequestAsync(
            _canonical, _identity, _dbContext, dashboardDefinitionId, widget, cancellationToken);

        if (refusal is not null)
        {
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation(refusal));
        }

        _dbContext.DashboardWidgetDefinitions.Add(widget);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<Guid>.Success(widget.Id);
    }

    public async Task<ApplicationResult> UpdateWidgetAsync(
        Guid dashboardDefinitionId,
        Guid widgetDefinitionId,
        UpdateDashboardWidgetDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var widget = await _dbContext.DashboardWidgetDefinitions
            .FirstOrDefaultAsync(
                x => x.Id == widgetDefinitionId &&
                     x.DashboardDefinitionId == dashboardDefinitionId &&
                     !x.IsDeleted,
                cancellationToken);

        if (widget is null)
            return ApplicationResult.Failure(ApplicationError.NotFound("Dashboard widget definition was not found."));

        var validation = ValidateWidgetRequest(new CreateDashboardWidgetDefinitionRequest(
            WidgetCode: widget.WidgetCode,
            WidgetTitle: request.WidgetTitle,
            WidgetType: request.WidgetType,
            ChartType: request.ChartType,
            DimensionCode: request.DimensionCode,
            MeasureCode: request.MeasureCode,
            ParameterCode: request.ParameterCode,
            FilterJson: request.FilterJson,
            LayoutJson: widget.LayoutJson,
            DisplayOptionsJson: request.DisplayOptionsJson,
            SortOrder: widget.SortOrder,
            IsSynthetic: widget.IsSynthetic,
            SourceSystem: widget.SourceSystem,
            SourceRecordId: widget.SourceRecordId));

        if (validation is not null)
            return ApplicationResult.Failure(validation);

        widget.UpdateDefinition(
            widgetTitle: request.WidgetTitle,
            widgetType: request.WidgetType,
            chartType: request.ChartType,
            dimensionCode: request.DimensionCode,
            measureCode: request.MeasureCode,
            parameterCode: request.ParameterCode,
            filterJson: request.FilterJson,
            displayOptionsJson: request.DisplayOptionsJson);

        ApplyExpression(widget, request.QueryExpression);

        if (request.IsActive == true)
            widget.Activate();
        else if (request.IsActive == false)
            widget.Deactivate();

        // Semantic change forks a canonical version. Identical redeclaration
        // does not, because the writer decides by semantic hash.
        var refusal = await WidgetCanonicalConvergence.WriteForRequestAsync(
            _canonical, _identity, _dbContext, dashboardDefinitionId, widget, cancellationToken);

        if (refusal is not null)
        {
            return ApplicationResult.Failure(ApplicationError.Validation(refusal));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> UpdateWidgetLayoutAsync(
        Guid dashboardDefinitionId,
        Guid widgetDefinitionId,
        UpdateDashboardWidgetLayoutRequest request,
        CancellationToken cancellationToken)
    {
        var layoutError = ValidateJsonObject(request.LayoutJson, nameof(request.LayoutJson));
        if (layoutError is not null)
            return ApplicationResult.Failure(layoutError);

        var widget = await _dbContext.DashboardWidgetDefinitions
            .FirstOrDefaultAsync(
                x => x.Id == widgetDefinitionId &&
                     x.DashboardDefinitionId == dashboardDefinitionId &&
                     !x.IsDeleted,
                cancellationToken);

        if (widget is null)
            return ApplicationResult.Failure(ApplicationError.NotFound("Dashboard widget definition was not found."));

        widget.UpdateLayout(request.LayoutJson, request.SortOrder);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<Guid>> CloneWidgetAsync(
        Guid dashboardDefinitionId,
        Guid widgetDefinitionId,
        CloneDashboardWidgetRequest request,
        CancellationToken cancellationToken)
    {
        var source = await _dbContext.DashboardWidgetDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == widgetDefinitionId &&
                     x.DashboardDefinitionId == dashboardDefinitionId &&
                     !x.IsDeleted,
                cancellationToken);

        if (source is null)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.NotFound("Dashboard widget definition was not found."));

        var cloneCode = string.IsNullOrWhiteSpace(request.WidgetCode)
            ? $"{source.WidgetCode}_COPY_{DateTime.UtcNow:yyyyMMddHHmmss}"
            : request.WidgetCode.Trim();

        var cloneTitle = string.IsNullOrWhiteSpace(request.WidgetTitle)
            ? $"{source.WidgetTitle} Copy"
            : request.WidgetTitle.Trim();

        var exists = await _dbContext.DashboardWidgetDefinitions.AnyAsync(
            x => x.DashboardDefinitionId == dashboardDefinitionId &&
                 x.WidgetCode == cloneCode &&
                 !x.IsDeleted,
            cancellationToken);

        if (exists)
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Conflict($"Widget code '{cloneCode}' already exists in this dashboard."));

        var clone = new DashboardWidgetDefinition(
            dashboardDefinitionId: dashboardDefinitionId,
            widgetCode: cloneCode,
            widgetTitle: cloneTitle,
            widgetType: source.WidgetType,
            chartType: source.ChartType,
            dimensionCode: source.DimensionCode,
            measureCode: source.MeasureCode,
            isSynthetic: source.IsSynthetic,
            parameterCode: source.ParameterCode,
            filterJson: source.FilterJson,
            layoutJson: source.LayoutJson,
            displayOptionsJson: source.DisplayOptionsJson,
            sortOrder: request.SortOrder ?? source.SortOrder + 1,
            sourceSystem: source.SourceSystem,
            sourceRecordId: $"cloned-from:{source.Id}");

        // A clone is a NEW definition with its own canonical V1, not a copy of
        // another definition's history.
        var refusal = await WidgetCanonicalConvergence.WriteForRequestAsync(
            _canonical, _identity, _dbContext, dashboardDefinitionId, clone, cancellationToken);

        if (refusal is not null)
        {
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation(refusal));
        }

        _dbContext.DashboardWidgetDefinitions.Add(clone);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<Guid>.Success(clone.Id);
    }

    public async Task<ApplicationResult> DeactivateWidgetAsync(
        Guid dashboardDefinitionId,
        Guid widgetDefinitionId,
        CancellationToken cancellationToken)
    {
        var widget = await _dbContext.DashboardWidgetDefinitions
            .FirstOrDefaultAsync(
                x => x.Id == widgetDefinitionId &&
                     x.DashboardDefinitionId == dashboardDefinitionId &&
                     !x.IsDeleted,
                cancellationToken);

        if (widget is null)
            return ApplicationResult.Failure(ApplicationError.NotFound("Dashboard widget definition was not found."));

        widget.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult.Success();
    }

public async Task<ApplicationResult<int>> EnsureSystemTemplatesAsync(
    CancellationToken cancellationToken)
{
    var changed = 0;

    changed += await EnsureTemplateAsync(
        code: "SYSTEM_QUALITY_OVERVIEW",
        name: "Quality Overview",
        description:
            "Default quality intelligence dashboard showing defect trend, defect breakdown, and material population.",
        widgets:
        [
            TemplateWidget("DEFECT_TREND", "Defect Rate Trend", "line", DashboardMetadataCodes.Dimensions.Day, DashboardMetadataCodes.Measures.DefectRate, 0),
            TemplateWidget("DEFECT_BREAKDOWN", "Defect Breakdown", "bar", DashboardMetadataCodes.Dimensions.DefectType, DashboardMetadataCodes.Measures.DefectCount, 1),
            TemplateWidget("MATERIAL_BY_TYPE", "Material by Type", "bar", DashboardMetadataCodes.Dimensions.MaterialUnitType, DashboardMetadataCodes.Measures.MaterialCount, 2),
        ],
        cancellationToken);

    changed += await EnsureTemplateAsync(
        code: "SYSTEM_RISK_DASHBOARD",
        name: "Risk Dashboard",
        description:
            "Default risk dashboard showing risk score distribution, risk by equipment, and risk by material type.",
        widgets:
        [
            TemplateWidget("RISK_BY_CLASS", "Risk by Class", "bar", DashboardMetadataCodes.Dimensions.RiskClass, DashboardMetadataCodes.Measures.RiskScore, 0),
            TemplateWidget("RISK_BY_EQUIPMENT", "Risk by Equipment", "bar", DashboardMetadataCodes.Dimensions.Equipment, DashboardMetadataCodes.Measures.RiskScore, 1),
            TemplateWidget("RISK_BY_MATERIAL_TYPE", "Risk by Material Type", "bar", DashboardMetadataCodes.Dimensions.MaterialUnitType, DashboardMetadataCodes.Measures.RiskScore, 2),
        ],
        cancellationToken);

    changed += await EnsureTemplateAsync(
        code: "SYSTEM_DATA_QUALITY",
        name: "Data Quality",
        description:
            "Default data quality monitoring dashboard showing issue counts by source system and material type.",
        widgets:
        [
            TemplateWidget("DQ_BY_SOURCE", "Issues by Source", "bar", DashboardMetadataCodes.Dimensions.SourceSystem, DashboardMetadataCodes.Measures.DataQualityIssueCount, 0),
            TemplateWidget("DQ_BY_MATERIAL_TYPE", "Issues by Material Type", "bar", DashboardMetadataCodes.Dimensions.MaterialUnitType, DashboardMetadataCodes.Measures.DataQualityIssueCount, 1),
            TemplateWidget("DQ_BY_RISK_CLASS", "Issues by Risk Class", "bar", DashboardMetadataCodes.Dimensions.RiskClass, DashboardMetadataCodes.Measures.DataQualityIssueCount, 2),
        ],
        cancellationToken);

    changed += await EnsureTemplateAsync(
        code: "SYSTEM_CORRELATION_EXPLORER",
        name: "Correlation Explorer",
        description:
            "Default correlation exploration dashboard for suspected contributors, defect rates, and equipment-level patterns.",
        widgets:
        [
            TemplateWidget("CORR_RISK_BY_DAY", "Risk Trend by Day", "line", DashboardMetadataCodes.Dimensions.Day, DashboardMetadataCodes.Measures.RiskScore, 0),
        ],
        cancellationToken);

    changed += await EnsureTemplateAsync(
        code: "SYSTEM_MATERIAL_INVESTIGATION_LAUNCHER",
        name: "Material Investigation Launcher",
        description:
            "Default material investigation launcher showing material populations by source and type.",
        widgets:
        [
            TemplateWidget("INV_MATERIAL_BY_SOURCE", "Material by Source", "bar", DashboardMetadataCodes.Dimensions.SourceSystem, DashboardMetadataCodes.Measures.MaterialCount, 0),
            TemplateWidget("INV_MATERIAL_BY_TYPE", "Material by Type", "table", DashboardMetadataCodes.Dimensions.MaterialUnitType, DashboardMetadataCodes.Measures.MaterialCount, 1),
            TemplateWidget("INV_RISK_BY_SOURCE", "Risk by Source", "bar", DashboardMetadataCodes.Dimensions.SourceSystem, DashboardMetadataCodes.Measures.RiskScore, 2),
        ],
        cancellationToken);

    if (changed > 0)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException?.Message.Contains("23505") == true ||
                ex.InnerException?.Message.Contains("duplicate key value violates unique constraint") == true)
        {
            // A concurrent request already inserted the same widget_code.
            // The templates exist — this call's goal is achieved. Safe to ignore.
        }
    }

    return ApplicationResult<int>.Success(changed);
}

private async Task<int> EnsureTemplateAsync(
    string code,
    string name,
    string description,
    IEnumerable<TemplateWidgetSeed> widgets,
    CancellationToken cancellationToken)
{
    var changed = 0;

    var dashboard = await _dbContext.DashboardDefinitions
        .Include(x => x.Widgets)
        .FirstOrDefaultAsync(
            x => x.DashboardCode == code && !x.IsDeleted,
            cancellationToken);

    if (dashboard is null)
    {
        dashboard = new DashboardDefinition(
            dashboardCode: code,
            name: name,
            isSynthetic: true,
            description: description,
            layoutJson: "{}",
            isDefault: code == "SYSTEM_QUALITY_OVERVIEW",
            isSystemTemplate: true,
            sourceSystem: "PlantProcessIQ.SystemTemplates",
            sourceRecordId: code);

        _dbContext.DashboardDefinitions.Add(dashboard);
        changed++;
    }
    else
    {
        dashboard.Rename(name, description);

        if (!dashboard.IsActive)
            dashboard.Activate();

        changed++;
    }

    foreach (var seed in widgets)
    {
        var existingWidget = dashboard.Widgets
            .FirstOrDefault(x =>
                !x.IsDeleted &&
                string.Equals(x.WidgetCode, seed.Code, StringComparison.OrdinalIgnoreCase));

        if (existingWidget is null)
        {
            _dbContext.DashboardWidgetDefinitions.Add(new DashboardWidgetDefinition(
                dashboardDefinitionId: dashboard.Id,
                widgetCode: seed.Code,
                widgetTitle: seed.Title,
                widgetType: "chart",
                chartType: seed.ChartType,
                dimensionCode: seed.DimensionCode,
                measureCode: seed.MeasureCode,
                isSynthetic: true,
                parameterCode: null,
                filterJson: "{}",
                layoutJson: BuildWidgetLayout(seed.SortOrder),
                displayOptionsJson: "{}",
                sortOrder: seed.SortOrder,
                sourceSystem: "PlantProcessIQ.SystemTemplates",
                sourceRecordId: seed.Code));

            changed++;
            continue;
        }

        // Ordinal, not OrdinalIgnoreCase. Under the insensitive comparison a row
        // carrying MaterialCount was judged equal to materialCount, so the repair
        // path looked at a widget the engine cannot execute and moved on. The
        // codes are case-sensitive to the query engine, so they are compared that
        // way here.
        var shouldUpdate =
            !string.Equals(existingWidget.WidgetTitle, seed.Title, StringComparison.Ordinal) ||
            !string.Equals(existingWidget.WidgetType, "chart", StringComparison.Ordinal) ||
            !string.Equals(existingWidget.ChartType, seed.ChartType, StringComparison.Ordinal) ||
            !string.Equals(existingWidget.DimensionCode, seed.DimensionCode, StringComparison.Ordinal) ||
            !string.Equals(existingWidget.MeasureCode, seed.MeasureCode, StringComparison.Ordinal);

        if (shouldUpdate)
        {
            existingWidget.UpdateDefinition(
                widgetTitle: seed.Title,
                widgetType: "chart",
                chartType: seed.ChartType,
                dimensionCode: seed.DimensionCode,
                measureCode: seed.MeasureCode,
                parameterCode: existingWidget.ParameterCode,
                filterJson: existingWidget.FilterJson,
                displayOptionsJson: existingWidget.DisplayOptionsJson);

            existingWidget.UpdateLayout(
                layoutJson: string.IsNullOrWhiteSpace(existingWidget.LayoutJson) || existingWidget.LayoutJson == "{}"
                    ? BuildWidgetLayout(seed.SortOrder)
                    : existingWidget.LayoutJson,
                sortOrder: seed.SortOrder);

            changed++;
        }
    }

    RetireUndeclaredProductWidgets(dashboard, widgets, ref changed);

    // T-090. Product-owned widgets are semantic definitions too. This runs on
    // every application start, so it MUST be idempotent: the canonical writer
    // decides by semantic hash and an unchanged declaration returns the existing
    // version. Without that, thirteen product widgets would fork thirteen
    // versions per boot and the immutable history would record restarts.
    var templateRefusal = await WidgetCanonicalConvergence.ConvergeTemplateAsync(
        _canonical, _identity, _dbContext, dashboard,
        widgets.Select(w => (w.Code, w.Title, w.ChartType, w.DimensionCode, w.MeasureCode)),
        cancellationToken);

    // A declined canonical write fails the ensure operation, loudly. Silence
    // here produced a 200 with zero canonical rows. (W1-T090-TEMPLATE-01)
    if (templateRefusal is not null)
    {
        throw new InvalidOperationException(
            "System template canonical convergence was refused: " + templateRefusal);
    }

    return changed;
}

/// <summary>
/// Convergence, not accumulation.
///
/// This authority created and repaired widgets but never retired one. Deleting a
/// widget from the declarations therefore left the persisted row active and
/// executing: two widgets removed at source kept failing the release replay
/// afterwards, because nothing had told the database they were gone.
///
/// The persisted product-owned set now converges to the declared set. A widget
/// the declarations no longer name is retired through the repository's existing
/// convention - deactivated, then soft-deleted with a reason - so the history
/// stays readable and the widget-code uniqueness index releases the code.
///
/// Bounded by provenance, not by code. Only rows this authority wrote are
/// considered, compared ordinally against the product template marker. A
/// customer-authored widget survives even when its code, dimension or measure
/// matches a product one, and even when it sits on a canonical system dashboard.
/// Product authority reconciles its own rows and nothing else.
/// </summary>
private static void RetireUndeclaredProductWidgets(
    DashboardDefinition dashboard,
    IEnumerable<TemplateWidgetSeed> widgets,
    ref int changed)
{
    var declared = new HashSet<string>(
        widgets.Select(seed => seed.Code),
        StringComparer.OrdinalIgnoreCase);

    var undeclared = dashboard.Widgets
        .Where(widget =>
            !widget.IsDeleted &&
            string.Equals(widget.SourceSystem, "PlantProcessIQ.SystemTemplates", StringComparison.Ordinal) &&
            !declared.Contains(widget.WidgetCode))
        .ToList();

    foreach (var widget in undeclared)
    {
        widget.Deactivate();
        widget.SoftDelete("no longer declared by the canonical system-template authority");
        changed++;
    }
}

public async Task<ApplicationResult<int>> RepairSystemTemplatesAsync(
    CancellationToken cancellationToken)
{
    // EnsureSystemTemplatesAsync is intentionally idempotent:
    // - creates missing system dashboards,
    // - creates missing widgets,
    // - repairs invalid widget dimension/measure codes,
    // - normalizes system template widget definitions.
    return await EnsureSystemTemplatesAsync(cancellationToken);
}

private static TemplateWidgetSeed TemplateWidget(
    string code,
    string title,
    string chartType,
    string dimensionCode,
    string measureCode,
    int sortOrder)
{
    return new TemplateWidgetSeed(
        Code: code,
        Title: title,
        ChartType: chartType,
        DimensionCode: dimensionCode,
        MeasureCode: measureCode,
        SortOrder: sortOrder);
}

private static string BuildWidgetLayout(int index)
{
    var lgX = index % 2 * 6;
    var lgY = index / 2 * 9;
    var stackedY = index * 9;

    return JsonSerializer.Serialize(new
    {
        lg = new
        {
            x = lgX,
            y = lgY,
            w = 6,
            h = 9,
            minW = 4,
            minH = 6
        },
        md = new
        {
            x = 0,
            y = stackedY,
            w = 10,
            h = 8,
            minW = 4,
            minH = 6
        },
        sm = new
        {
            x = 0,
            y = stackedY,
            w = 6,
            h = 8,
            minW = 3,
            minH = 5
        },
        xs = new
        {
            x = 0,
            y = stackedY,
            w = 4,
            h = 8,
            minW = 3,
            minH = 5
        },
        xxs = new
        {
            x = 0,
            y = stackedY,
            w = 2,
            h = 8,
            minW = 2,
            minH = 5
        }
    });
}

    private sealed record TemplateWidgetSeed(
        string Code,
        string Title,
        string ChartType,
        string DimensionCode,
        string MeasureCode,
        int SortOrder);

    private static DashboardDefinitionDto ToDto(DashboardDefinition dashboard) =>
        new(
            dashboard.Id,
            dashboard.UserId,
            dashboard.DashboardCode,
            dashboard.Name,
            dashboard.Description,
            dashboard.LayoutJson,
            dashboard.IsDefault,
            dashboard.IsSystemTemplate,
            dashboard.IsActive,
            dashboard.IsSynthetic,
            dashboard.SourceSystem,
            dashboard.SourceRecordId,
            dashboard.Widgets
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.WidgetTitle)
                .Select(ToWidgetDto)
                .ToList());

    private static DashboardWidgetDefinitionDto ToWidgetDto(DashboardWidgetDefinition widget) =>
        new(
            widget.Id,
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
            widget.IsActive,
            widget.IsSynthetic,
            widget.SourceSystem,
            widget.SourceRecordId,
            widget.QueryExpression,
            widget.ExpressionEnabled);

    /// <summary>
    /// Persists an authored query expression against a widget.
    ///
    /// The parse IS the validation event. The domain refuses to enable an
    /// expression whose status is not Valid, so a failed parse is stored
    /// disabled, carrying the parser's own message. The widget keeps its
    /// catalogue binding in that case and still draws, which is why a bad
    /// expression can never blank a page.
    ///
    /// A null or blank expression clears any previous one.
    /// </summary>
    private void ApplyExpression(DashboardWidgetDefinition widget, string? queryExpression)
    {
        if (string.IsNullOrWhiteSpace(queryExpression))
        {
            widget.ConfigureExpression(
                queryExpression: null,
                advancedExpressionJson: "{}",
                expressionVersion: 1,
                expressionEnabled: false,
                validationStatus: WidgetExpressionStatus.Pending,
                validationMessage: null);

            return;
        }

        var parsed = _expressions.Parse(new WidgetQueryExpressionRequest(
            Expression: queryExpression,
            Filters: null,
            Options: null));

        if (parsed.IsSuccess)
        {
            widget.ConfigureExpression(
                queryExpression: queryExpression,
                advancedExpressionJson: "{}",
                expressionVersion: 1,
                expressionEnabled: true,
                validationStatus: WidgetExpressionStatus.Valid,
                validationMessage: "Validated on save.");

            return;
        }

        widget.ConfigureExpression(
            queryExpression: queryExpression,
            advancedExpressionJson: "{}",
            expressionVersion: 1,
            expressionEnabled: false,
            validationStatus: WidgetExpressionStatus.Invalid,
            validationMessage: parsed.Error?.Message);
    }
    private ApplicationError? ValidateWidgetRequest(CreateDashboardWidgetDefinitionRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        AddRequired(errors, nameof(request.WidgetCode), request.WidgetCode);
        AddRequired(errors, nameof(request.WidgetTitle), request.WidgetTitle);
        AddRequired(errors, nameof(request.WidgetType), request.WidgetType);
        AddRequired(errors, nameof(request.ChartType), request.ChartType);
        AddRequired(errors, nameof(request.MeasureCode), request.MeasureCode);

        AddJsonError(errors, nameof(request.FilterJson), request.FilterJson);
        AddJsonError(errors, nameof(request.LayoutJson), request.LayoutJson);
        AddJsonError(errors, nameof(request.DisplayOptionsJson), request.DisplayOptionsJson);

        if (errors.Count > 0)
            return ApplicationError.Validation("Dashboard widget definition is invalid.", errors);

        var validation = _validator.Validate(new DashboardWidgetQueryDto(
            WidgetType: request.WidgetType,
            ChartType: request.ChartType,
            DimensionCode: request.DimensionCode,
            MeasureCode: request.MeasureCode,
            ParameterCode: request.ParameterCode,
            Filters: DeserializeFilters(request.FilterJson, request.ParameterCode),
            Options: new DashboardWidgetQueryOptionsDto(
                MaxRows: 100,
                RawRowLimit: 500,
                SortDirection: "desc",
                IncludeWarnings: true)));

        if (validation.IsFailure)
            return validation.Error;

        return null;
    }

    private static DashboardWidgetFiltersDto? DeserializeFilters(string? filterJson, string? parameterCode)
    {
        if (string.IsNullOrWhiteSpace(filterJson))
        {
            return parameterCode is null
                ? null
                : new DashboardWidgetFiltersDto(
                    SiteId: null,
                    AreaId: null,
                    EquipmentId: null,
                    MaterialCode: null,
                    MaterialUnitType: null,
                    SourceSystem: null,
                    DefectType: null,
                    RiskClass: null,
                    ShiftCode: null,
                    ParameterCode: parameterCode,
                    FromUtc: null,
                    ToUtc: null);
        }

        try
        {
            var filters = JsonSerializer.Deserialize<DashboardWidgetFiltersDto>(
                filterJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (filters is null)
            {
                return parameterCode is null
                    ? null
                    : new DashboardWidgetFiltersDto(
                        SiteId: null,
                        AreaId: null,
                        EquipmentId: null,
                        MaterialCode: null,
                        MaterialUnitType: null,
                        SourceSystem: null,
                        DefectType: null,
                        RiskClass: null,
                        ShiftCode: null,
                        ParameterCode: parameterCode,
                        FromUtc: null,
                        ToUtc: null);
            }

            if (!string.IsNullOrWhiteSpace(parameterCode) &&
                string.IsNullOrWhiteSpace(filters.ParameterCode))
            {
                return filters with
                {
                    ParameterCode = parameterCode
                };
            }

            return filters;
        }
        catch
        {
            return parameterCode is null
                ? null
                : new DashboardWidgetFiltersDto(
                    SiteId: null,
                    AreaId: null,
                    EquipmentId: null,
                    MaterialCode: null,
                    MaterialUnitType: null,
                    SourceSystem: null,
                    DefectType: null,
                    RiskClass: null,
                    ShiftCode: null,
                    ParameterCode: parameterCode,
                    FromUtc: null,
                    ToUtc: null);
        }
    }

    private static IReadOnlyDictionary<string, string[]> ValidateDashboardRequest(
        string dashboardCode,
        string name,
        string? layoutJson)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        AddRequired(errors, nameof(dashboardCode), dashboardCode);
        AddRequired(errors, nameof(name), name);
        AddJsonError(errors, nameof(layoutJson), layoutJson);

        return errors;
    }

    private static ApplicationError? ValidateJsonObject(string? json, string fieldName)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        AddJsonError(errors, fieldName, json);
        return errors.Count == 0
            ? null
            : ApplicationError.Validation("JSON payload is invalid.", errors);
    }

    private static void AddRequired(Dictionary<string, string[]> errors, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors[key] = new[] { $"{key} is required." };
    }

    private static void AddJsonError(Dictionary<string, string[]> errors, string key, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
                errors[key] = new[] { $"{key} must be a JSON object." };
        }
        catch (JsonException ex)
        {
            errors[key] = new[] { $"{key} is invalid JSON: {ex.Message}" };
        }
    }

    private static string NormalizeRequired(string value) => value.Trim();

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeJson(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "{}" : value.Trim();

    private async Task ClearDefaultDashboardsAsync(
        CancellationToken cancellationToken,
        Guid? exceptDashboardId = null)
    {
        var currentDefaults = await _dbContext.DashboardDefinitions
            .Where(x => x.IsDefault && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var dashboard in currentDefaults)
        {
            if (exceptDashboardId.HasValue && dashboard.Id == exceptDashboardId.Value)
                continue;

            dashboard.RemoveDefaultFlag();
        }
    }
}

/// <summary>
/// PPIQ T-090. Canonical convergence for every widget writer in this service.
///
/// FOUR WRITERS, ONE AUTHORITY. CreateWidgetAsync, UpdateWidgetAsync,
/// CloneWidgetAsync and the system-template path all create or change semantic
/// widget definitions. Converging only the first three would leave startup
/// templates writing operational rows with no canonical version, which is the
/// dual authority this task removes. Clone is included deliberately: a clone is
/// a NEW definition, not a copy of an existing one's history.
///
/// RESTART IDEMPOTENCE. EnsureSystemTemplatesAsync runs on every application
/// start. The canonical writer decides by semantic hash, so an unchanged
/// declaration returns the existing version and thirteen product widgets do not
/// fork thirteen versions per boot. Convergence, not accumulation - the same
/// principle RetireUndeclaredProductWidgets applies to the operational row.
/// </summary>
internal static class WidgetCanonicalConvergence
{
    /// <summary>
    /// The canonical definition code for a widget. Dashboard code and widget
    /// code together, because a widget code is only unique within its dashboard
    /// and the canonical store is unique per tenant.
    /// </summary>
    internal static string DefinitionCodeFor(string dashboardCode, string widgetCode) =>
        dashboardCode.Trim() + ":" + widgetCode.Trim();

    /// <summary>
    /// The declared semantic payload of a widget definition. Layout and sort
    /// order are deliberately excluded: they are serving presentation state,
    /// and including them would fork a version every time someone dragged a
    /// widget. Measure, dimension, parameter, chart type, filters and display
    /// options are authored semantics and are included.
    /// </summary>
    internal static string PayloadFor(
        string widgetCode,
        string widgetTitle,
        string widgetType,
        string chartType,
        string dimensionCode,
        string measureCode,
        string? parameterCode,
        string? filterJson,
        string? displayOptionsJson,
        string? queryExpression) =>
        JsonSerializer.Serialize(new
        {
            widgetCode,
            widgetTitle,
            widgetType,
            chartType,
            dimensionCode,
            measureCode,
            parameterCode,
            filterJson,
            displayOptionsJson,
            queryExpression,
        });

    internal static Dictionary<string, object?> DetailFor(
        string widgetType,
        string chartType,
        string dimensionCode,
        string measureCode,
        string? filterJson) =>
        new(StringComparer.Ordinal)
        {
            ["widget_kind"] = widgetType,
            ["chart_type"] = chartType,
            ["dimension_code"] = dimensionCode,
            ["measure_code"] = measureCode,
            ["saved_filter_json"] = string.IsNullOrWhiteSpace(filterJson) ? "{}" : filterJson,
        };

    /// <summary>
    /// Writes the canonical widget definition inside the caller's transaction.
    /// Returns the refusal message when the canonical authority declines, so the
    /// caller can fail the whole operation rather than proceed with an
    /// operational row that has no canonical version behind it.
    /// </summary>
    internal static async Task<string?> WriteAsync(
        ICanonicalDefinitionWriter canonical,
        Guid tenantId,
        Guid ownerId,
        string definitionCode,
        string widgetTitle,
        string payloadJson,
        Dictionary<string, object?> detail,
        CancellationToken cancellationToken)
    {
        var written = await canonical.WriteVersionAsync(
            new CanonicalDefinitionWrite(
                DefinitionKind.Widget,
                tenantId,
                ownerId,
                definitionCode,
                widgetTitle,
                payloadJson,
                CanonicalVersionStatus.Published,
                detail),
            cancellationToken);

        return written.IsSuccess ? null : written.Error?.Message ?? "canonical widget write refused";
    }

    /// <summary>
    /// Writes the canonical definition for one widget inside a transaction the
    /// caller may or may not already own. Returns null on success, a message on
    /// refusal, so the caller fails the whole operation rather than leaving an
    /// operational row with no canonical version behind it.
    /// </summary>
    internal static async Task<string?> WriteForRequestAsync(
        ICanonicalDefinitionWriter canonical,
        ICanonicalIdentityResolver identity,
        IPlantProcessDbContext db,
        Guid dashboardDefinitionId,
        DashboardWidgetDefinition widget,
        CancellationToken cancellationToken)
    {
        var dashboard = await db.DashboardDefinitions
            .Where(d => d.Id == dashboardDefinitionId)
            .Select(d => new { d.DashboardCode })
            .FirstOrDefaultAsync(cancellationToken);

        if (dashboard is null)
        {
            return "the dashboard this widget belongs to no longer exists";
        }

        var tenantId = await identity.ResolveTenantAsync(null, cancellationToken);
        var ownerId = await identity.ResolveOwnerAsync(null, cancellationToken);

        if (tenantId is null || ownerId is null)
        {
            return "no tenant or owner identity could be resolved for a canonical widget definition";
        }

        return await InUnitOfWorkAsync(db, cancellationToken, async () =>
            await WriteAsync(
                canonical,
                tenantId.Value,
                ownerId.Value,
                DefinitionCodeFor(dashboard.DashboardCode, widget.WidgetCode),
                widget.WidgetTitle,
                PayloadFor(widget.WidgetCode, widget.WidgetTitle, widget.WidgetType, widget.ChartType,
                           widget.DimensionCode, widget.MeasureCode, widget.ParameterCode,
                           widget.FilterJson, widget.DisplayOptionsJson, widget.QueryExpression),
                DetailFor(widget.WidgetType, widget.ChartType, widget.DimensionCode,
                          widget.MeasureCode, widget.FilterJson),
                cancellationToken));
    }

    /// <summary>
    /// Converges every declared product widget on a system dashboard, and
    /// retires the canonical publication of any this authority previously wrote
    /// but no longer declares.
    ///
    /// Runs on every application start. Idempotence comes from the semantic hash
    /// inside the writer, not from a guard here.
    /// </summary>
    /// <param name="declared">
    /// Code, title, chart type, dimension and measure for each declared widget.
    /// Deliberately not the caller's seed record: that type is private to
    /// DashboardDefinitionService, and widening it so a helper could name it
    /// would export an internal shape for the helper's convenience.
    /// </param>
    internal static async Task<string?> ConvergeTemplateAsync(
        ICanonicalDefinitionWriter canonical,
        ICanonicalIdentityResolver identity,
        IPlantProcessDbContext db,
        DashboardDefinition dashboard,
        IEnumerable<(string Code, string Title, string ChartType, string DimensionCode, string MeasureCode)> declared,
        CancellationToken cancellationToken)
    {
        var tenantId = await identity.ResolveTenantAsync(null, cancellationToken);
        var ownerId = await identity.ResolveOwnerAsync(null, cancellationToken);

        if (tenantId is null || ownerId is null)
        {
            // Startup before provisioning has completed. Convergence runs again
            // on the next boot rather than writing under an invented identity.
            return null;
        }

        return await InUnitOfWorkAsync(db, cancellationToken, async () =>
        {
            foreach (var seed in declared)
            {
                var refusal = await WriteAsync(
                    canonical, tenantId.Value, ownerId.Value,
                    DefinitionCodeFor(dashboard.DashboardCode, seed.Code),
                    seed.Title,
                    PayloadFor(seed.Code, seed.Title, "chart", seed.ChartType,
                               seed.DimensionCode, seed.MeasureCode, null, "{}", "{}", null),
                    DetailFor("chart", seed.ChartType, seed.DimensionCode, seed.MeasureCode, "{}"),
                    cancellationToken);

                if (refusal is not null) { return refusal; }
            }

            var declaredCodes = new HashSet<string>(
                declared.Select(s => s.Code), StringComparer.Ordinal);

            foreach (var widget in dashboard.Widgets.Where(w =>
                         w.IsDeleted &&
                         string.Equals(w.SourceSystem, "PlantProcessIQ.SystemTemplates", StringComparison.Ordinal) &&
                         !declaredCodes.Contains(w.WidgetCode)))
            {
                var refusal = await RetireAsync(
                    canonical, tenantId.Value,
                    DefinitionCodeFor(dashboard.DashboardCode, widget.WidgetCode),
                    cancellationToken);

                if (refusal is not null) { return refusal; }
            }

            return null;
        });
    }

    /// <summary>
    /// Opens a transaction only when the caller has none, so a write already
    /// inside a unit of work stays in it.
    /// </summary>
    private static async Task<string?> InUnitOfWorkAsync(
        IPlantProcessDbContext db,
        CancellationToken cancellationToken,
        Func<Task<string?>> work)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            return await work();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var refusal = await work();

        if (refusal is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return refusal;
        }

        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    /// <summary>
    /// A retired product widget must stop being advertised as current published
    /// truth. The canonical version history stays - it is evidence of what ran -
    /// but the published version is superseded so no runtime resolution returns
    /// a widget the declarations no longer name.
    /// </summary>
    internal static async Task<string?> RetireAsync(
        ICanonicalDefinitionWriter canonical,
        Guid tenantId,
        string definitionCode,
        CancellationToken cancellationToken)
    {
        var found = await canonical.FindByCodeAsync(tenantId, definitionCode, cancellationToken);
        if (found.IsFailure)
        {
            return found.Error?.Message ?? "canonical lookup refused";
        }

        // A widget that never reached the canonical store has nothing to retire.
        // That is a success, not a silent skip: the desired end state holds.
        if (found.Value is null)
        {
            return null;
        }

        var retired = await canonical.RetireAsync(found.Value.Value, cancellationToken);
        return retired.IsSuccess ? null : retired.Error?.Message ?? "canonical retirement refused";
    }
}
