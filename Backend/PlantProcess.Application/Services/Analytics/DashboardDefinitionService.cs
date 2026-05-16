using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;
using PlantProcess.Domain.Entities.Analytics;

namespace PlantProcess.Application.Services.Analytics;

public sealed class DashboardDefinitionService : IDashboardDefinitionService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IDashboardWidgetValidationService _validator;

    public DashboardDefinitionService(
        IPlantProcessDbContext dbContext,
        IDashboardWidgetValidationService validator)
    {
        _dbContext = dbContext;
        _validator = validator;
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
            dimensionCode: NormalizeRequired(request.DimensionCode),
            measureCode: NormalizeRequired(request.MeasureCode),
            isSynthetic: request.IsSynthetic,
            parameterCode: NormalizeNullable(request.ParameterCode),
            filterJson: NormalizeJson(request.FilterJson),
            layoutJson: NormalizeJson(request.LayoutJson),
            displayOptionsJson: NormalizeJson(request.DisplayOptionsJson),
            sortOrder: request.SortOrder ?? 0,
            sourceSystem: NormalizeNullable(request.SourceSystem),
            sourceRecordId: NormalizeNullable(request.SourceRecordId));

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

        if (request.IsActive == true)
            widget.Activate();
        else if (request.IsActive == false)
            widget.Deactivate();

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
            TemplateWidget("DEFECT_TREND", "Defect Rate Trend", "line", "Day", "DefectRate", 0),
            TemplateWidget("DEFECT_BREAKDOWN", "Defect Breakdown", "bar", "DefectType", "DefectCount", 1),
            TemplateWidget("MATERIAL_BY_TYPE", "Material by Type", "bar", "MaterialUnitType", "MaterialCount", 2),
        ],
        cancellationToken);

    changed += await EnsureTemplateAsync(
        code: "SYSTEM_RISK_DASHBOARD",
        name: "Risk Dashboard",
        description:
            "Default risk dashboard showing risk score distribution, risk by equipment, and risk by material type.",
        widgets:
        [
            TemplateWidget("RISK_BY_CLASS", "Risk by Class", "donut", "RiskClass", "RiskScore", 0),
            TemplateWidget("RISK_BY_EQUIPMENT", "Risk by Equipment", "bar", "Equipment", "RiskScore", 1),
            TemplateWidget("RISK_BY_MATERIAL_TYPE", "Risk by Material Type", "bar", "MaterialUnitType", "RiskScore", 2),
        ],
        cancellationToken);

    changed += await EnsureTemplateAsync(
        code: "SYSTEM_DATA_QUALITY",
        name: "Data Quality",
        description:
            "Default data quality monitoring dashboard showing issue counts by source system and material type.",
        widgets:
        [
            TemplateWidget("DQ_BY_SOURCE", "Issues by Source", "bar", "SourceSystem", "DataQualityIssueCount", 0),
            TemplateWidget("DQ_BY_MATERIAL_TYPE", "Issues by Material Type", "bar", "MaterialUnitType", "DataQualityIssueCount", 1),
            TemplateWidget("DQ_BY_RISK_CLASS", "Issues by Risk Class", "bar", "RiskClass", "DataQualityIssueCount", 2),
        ],
        cancellationToken);

    changed += await EnsureTemplateAsync(
        code: "SYSTEM_CORRELATION_EXPLORER",
        name: "Correlation Explorer",
        description:
            "Default correlation exploration dashboard for suspected contributors, defect rates, and equipment-level patterns.",
        widgets:
        [
            TemplateWidget("CORR_DEFECT_RATE_BY_EQUIPMENT", "Defect Rate by Equipment", "bar", "Equipment", "DefectRate", 0),
            TemplateWidget("CORR_DEFECT_RATE_BY_TYPE", "Defect Rate by Defect Type", "bar", "DefectType", "DefectRate", 1),
            TemplateWidget("CORR_RISK_BY_DAY", "Risk Trend by Day", "line", "Day", "RiskScore", 2),
        ],
        cancellationToken);

    changed += await EnsureTemplateAsync(
        code: "SYSTEM_MATERIAL_INVESTIGATION_LAUNCHER",
        name: "Material Investigation Launcher",
        description:
            "Default material investigation launcher showing material populations by source and type.",
        widgets:
        [
            TemplateWidget("INV_MATERIAL_BY_SOURCE", "Material by Source", "bar", "SourceSystem", "MaterialCount", 0),
            TemplateWidget("INV_MATERIAL_BY_TYPE", "Material by Type", "table", "MaterialUnitType", "MaterialCount", 1),
            TemplateWidget("INV_RISK_BY_SOURCE", "Risk by Source", "bar", "SourceSystem", "RiskScore", 2),
        ],
        cancellationToken);

    if (changed > 0)
        await _dbContext.SaveChangesAsync(cancellationToken);

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

        var shouldUpdate =
            !string.Equals(existingWidget.WidgetTitle, seed.Title, StringComparison.Ordinal) ||
            !string.Equals(existingWidget.WidgetType, "chart", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(existingWidget.ChartType, seed.ChartType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(existingWidget.DimensionCode, seed.DimensionCode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(existingWidget.MeasureCode, seed.MeasureCode, StringComparison.OrdinalIgnoreCase);

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

    return changed;
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
    var lgX = (index % 2) * 6;
    var lgY = (index / 2) * 9;
    var stackedY = index * 9;

    return System.Text.Json.JsonSerializer.Serialize(new
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
            widget.SourceRecordId);

    private ApplicationError? ValidateWidgetRequest(CreateDashboardWidgetDefinitionRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        AddRequired(errors, nameof(request.WidgetCode), request.WidgetCode);
        AddRequired(errors, nameof(request.WidgetTitle), request.WidgetTitle);
        AddRequired(errors, nameof(request.WidgetType), request.WidgetType);
        AddRequired(errors, nameof(request.ChartType), request.ChartType);
        AddRequired(errors, nameof(request.DimensionCode), request.DimensionCode);
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
            return parameterCode is null
                ? null
                : new DashboardWidgetFiltersDto(null, null, null, null, null, null, null, null, parameterCode, null, null);

        try
        {
            return JsonSerializer.Deserialize<DashboardWidgetFiltersDto>(
                filterJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return parameterCode is null
                ? null
                : new DashboardWidgetFiltersDto(null, null, null, null, null, null, null, null, parameterCode, null, null);
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