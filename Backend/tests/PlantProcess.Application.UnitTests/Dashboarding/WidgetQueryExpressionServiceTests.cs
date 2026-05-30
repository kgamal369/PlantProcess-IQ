using FluentAssertions;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Widgets;

namespace PlantProcess.Application.UnitTests.Dashboarding;

public sealed class WidgetQueryExpressionServiceTests
{
    [Fact]
    public void Compile_should_parse_structured_widget_expression()
    {
        var service = new WidgetQueryExpressionService();

        var result = service.Compile(new WidgetQueryExpressionRequest(
            "source: vw_quality_events; dimension: material_code; measure: count(*); filter: risk_level = 'High'; sort: material_code desc; limit: 25; timeWindow: event_at_utc last-30-days",
            null,
            null));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Source.Should().Be("vw_quality_events");
        result.Value.Dimensions.Should().ContainSingle(x => x.Column == "material_code");
        result.Value.Measures.Should().ContainSingle(x => x.Aggregate == "count" && x.Column == "*");
        result.Value.Filters.Should().ContainSingle(x => x.Column == "risk_level" && x.Operator == "=" && x.Value == "High");
        result.Value.Sort.Should().ContainSingle(x => x.Column == "material_code" && x.Direction == "DESC");
        result.Value.Limit.Should().Be(25);
        result.Value.TimeWindow!.Column.Should().Be("event_at_utc");
    }

    [Fact]
    public void Compile_should_return_unknown_keyword_failure()
    {
        var service = new WidgetQueryExpressionService();

        var result = service.Compile(new WidgetQueryExpressionRequest(
            "source: vw_quality_events; unknownKey: value; measure: count(*)",
            null,
            null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("UnknownKeyword");
    }

    [Fact]
    public void Compile_should_require_source()
    {
        var service = new WidgetQueryExpressionService();

        var result = service.Compile(new WidgetQueryExpressionRequest(
            "dimension: material_code; measure: count(*)",
            null,
            null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("MissingValue");
    }
}
