using FluentAssertions;
using PlantProcess.Domain.Entities.Dashboarding;

namespace PlantProcess.Domain.Tests.Dashboarding;

public sealed class DashboardWidgetDefinitionExpressionTests
{
    [Fact]
    public void Expression_enabled_true_requires_valid_status()
    {
        var widget = CreateWidget();

        var action = () => widget.ConfigureExpression(
            "source: vw_quality_events; measure: count(*)",
            "{}",
            1,
            expressionEnabled: true,
            WidgetExpressionStatus.Pending,
            "Not validated");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Valid*");
    }

    [Fact]
    public void Valid_expression_can_be_enabled()
    {
        var widget = CreateWidget();

        widget.ConfigureExpression(
            "source: vw_quality_events; dimension: material_code; measure: count(*)",
            "{}",
            1,
            expressionEnabled: true,
            WidgetExpressionStatus.Valid,
            "Valid");

        widget.ExpressionEnabled.Should().BeTrue();
        widget.ExpressionLastValidationStatus.Should().Be(WidgetExpressionStatus.Valid);
    }

    private static DashboardWidgetDefinition CreateWidget()
    {
        return new DashboardWidgetDefinition(
            Guid.NewGuid(),
            "TEST_WIDGET",
            "Test widget",
            "table",
            "table",
            "Material",
            "Count",
            isSynthetic: true);
    }
}
