using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Dashboarding;

public class DashboardWidgetDefinition : BaseEntity
{
    public Guid DashboardDefinitionId { get; private set; }
    public string WidgetCode { get; private set; } = null!;
    public string WidgetTitle { get; private set; } = null!;
    public string WidgetType { get; private set; } = null!;
    public string ChartType { get; private set; } = null!;
    public string DimensionCode { get; private set; } = null!;
    public string MeasureCode { get; private set; } = null!;
    public string? ParameterCode { get; private set; }
    public string FilterJson { get; private set; } = "{}";
    public string LayoutJson { get; private set; } = "{}";
    public string DisplayOptionsJson { get; private set; } = "{}";
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public string? QueryExpression { get; private set; }
    public string AdvancedExpressionJson { get; private set; } = "{}";
    public short ExpressionVersion { get; private set; } = 1;
    public bool ExpressionEnabled { get; private set; }
    public DateTime? ExpressionLastValidatedAtUtc { get; private set; }
    public WidgetExpressionStatus ExpressionLastValidationStatus { get; private set; } = WidgetExpressionStatus.Pending;
    public string? ExpressionLastValidationMessage { get; private set; }

    public DashboardDefinition? DashboardDefinition { get; private set; }

    private DashboardWidgetDefinition()
    {
    }

    public DashboardWidgetDefinition(
        Guid dashboardDefinitionId,
        string widgetCode,
        string widgetTitle,
        string widgetType,
        string chartType,
        string dimensionCode,
        string measureCode,
        bool isSynthetic,
        string? parameterCode = null,
        string? filterJson = null,
        string? layoutJson = null,
        string? displayOptionsJson = null,
        int sortOrder = 0,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (dashboardDefinitionId == Guid.Empty)
            throw new ArgumentException("Dashboard definition ID is required.", nameof(dashboardDefinitionId));
        if (string.IsNullOrWhiteSpace(widgetCode))
            throw new ArgumentException("Widget code is required.", nameof(widgetCode));
        if (string.IsNullOrWhiteSpace(widgetTitle))
            throw new ArgumentException("Widget title is required.", nameof(widgetTitle));
        if (string.IsNullOrWhiteSpace(widgetType))
            throw new ArgumentException("Widget type is required.", nameof(widgetType));
        if (string.IsNullOrWhiteSpace(chartType))
            throw new ArgumentException("Chart type is required.", nameof(chartType));
        if (string.IsNullOrWhiteSpace(measureCode))
            throw new ArgumentException("Measure code is required.", nameof(measureCode));

        DashboardDefinitionId = dashboardDefinitionId;
        WidgetCode = widgetCode.Trim();
        WidgetTitle = widgetTitle.Trim();
        WidgetType = widgetType.Trim();
        ChartType = chartType.Trim();
        DimensionCode = NormalizeBinding(dimensionCode);
        MeasureCode = measureCode.Trim();
        ParameterCode = NormalizeNullable(parameterCode);
        FilterJson = NormalizeJson(filterJson);
        LayoutJson = NormalizeJson(layoutJson);
        DisplayOptionsJson = NormalizeJson(displayOptionsJson);
        SortOrder = sortOrder;
        IsActive = true;

        IsSynthetic = isSynthetic;
        SourceSystem = NormalizeNullable(sourceSystem);
        SourceRecordId = NormalizeNullable(sourceRecordId);

        ExpressionEnabled = false;
        ExpressionVersion = 1;
        ExpressionLastValidationStatus = WidgetExpressionStatus.Pending;
        AdvancedExpressionJson = "{}";
    }

    public void UpdateDefinition(
        string widgetTitle,
        string widgetType,
        string chartType,
        string dimensionCode,
        string measureCode,
        string? parameterCode,
        string? filterJson,
        string? displayOptionsJson)
    {
        if (string.IsNullOrWhiteSpace(widgetTitle))
            throw new ArgumentException("Widget title is required.", nameof(widgetTitle));
        if (string.IsNullOrWhiteSpace(widgetType))
            throw new ArgumentException("Widget type is required.", nameof(widgetType));
        if (string.IsNullOrWhiteSpace(chartType))
            throw new ArgumentException("Chart type is required.", nameof(chartType));
        if (string.IsNullOrWhiteSpace(measureCode))
            throw new ArgumentException("Measure code is required.", nameof(measureCode));

        WidgetTitle = widgetTitle.Trim();
        WidgetType = widgetType.Trim();
        ChartType = chartType.Trim();
        DimensionCode = NormalizeBinding(dimensionCode);
        MeasureCode = measureCode.Trim();
        ParameterCode = NormalizeNullable(parameterCode);
        FilterJson = NormalizeJson(filterJson);
        DisplayOptionsJson = NormalizeJson(displayOptionsJson);

        MarkAsUpdated();
    }

    public void UpdateLayout(string? layoutJson, int? sortOrder = null)
    {
        LayoutJson = NormalizeJson(layoutJson);

        if (sortOrder.HasValue)
            SortOrder = sortOrder.Value;

        MarkAsUpdated();
    }

    public void ConfigureExpression(
        string? queryExpression,
        string? advancedExpressionJson,
        short expressionVersion,
        bool expressionEnabled,
        WidgetExpressionStatus validationStatus,
        string? validationMessage,
        DateTime? validatedAtUtc = null)
    {
        if (expressionVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(expressionVersion), "Expression version must be greater than zero.");

        if (expressionEnabled && validationStatus != WidgetExpressionStatus.Valid)
        {
            throw new InvalidOperationException(
                "Cannot enable widget expression unless expression_last_validation_status is Valid.");
        }

        QueryExpression = NormalizeNullable(queryExpression);
        AdvancedExpressionJson = NormalizeJson(advancedExpressionJson);
        ExpressionVersion = expressionVersion;
        ExpressionEnabled = expressionEnabled;
        ExpressionLastValidationStatus = validationStatus;
        ExpressionLastValidationMessage = NormalizeNullable(validationMessage);
        ExpressionLastValidatedAtUtc = validatedAtUtc ?? DateTime.UtcNow;

        MarkAsUpdated();
    }

    public void MarkExpressionValidation(
        WidgetExpressionStatus validationStatus,
        string? validationMessage,
        DateTime? validatedAtUtc = null)
    {
        if (ExpressionEnabled && validationStatus != WidgetExpressionStatus.Valid)
        {
            throw new InvalidOperationException(
                "Cannot keep widget expression enabled after a non-valid validation result.");
        }

        ExpressionLastValidationStatus = validationStatus;
        ExpressionLastValidationMessage = NormalizeNullable(validationMessage);
        ExpressionLastValidatedAtUtc = validatedAtUtc ?? DateTime.UtcNow;

        MarkAsUpdated();
    }

    public void EnableExpression()
    {
        if (ExpressionLastValidationStatus != WidgetExpressionStatus.Valid)
            throw new InvalidOperationException("Cannot enable widget expression before successful validation.");

        ExpressionEnabled = true;
        MarkAsUpdated();
    }

    public void DisableExpression(string? reason = null)
    {
        ExpressionEnabled = false;
        ExpressionLastValidationMessage = NormalizeNullable(reason) ?? ExpressionLastValidationMessage;
        MarkAsUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    private static string NormalizeJson(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "{}" : value.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// T-046 Pack 4B1. A binding the widget does not use.
    ///
    /// The entity required a dimension on every widget. That predates the chart
    /// grammar and contradicts it: a KPI declares SupportsDimension = false, and
    /// the five dimensionless widgets already live in the presentation database
    /// could not be recreated through the API meant to author them.
    ///
    /// It returns the EMPTY STRING, not null, because that is how every existing
    /// row already stores a blank binding. Changing that would need a migration,
    /// and this pack changes one invariant and nothing else.
    ///
    /// WHETHER A CHART NEEDS A DIMENSION IS NOT ASKED HERE. The entity accepts a
    /// shape; DashboardWidgetValidationService judges its meaning. A chart-type
    /// test in this file would be a second grammar, and an architecture guard
    /// fails the build if one appears.
    /// </summary>
    private static string NormalizeBinding(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
