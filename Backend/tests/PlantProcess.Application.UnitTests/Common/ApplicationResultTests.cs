using FluentAssertions;
using PlantProcess.Application.Common.Constants;
using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.UnitTests.Common;

public sealed class ApplicationResultTests
{
    [Fact]
    public void Success_result_should_be_successful()
    {
        var result = ApplicationResult.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_result_should_contain_validation_error()
    {
        var error = ApplicationError.Validation("Test validation failure");

        var result = ApplicationResult.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.ValidationFailed);
        result.Error.Message.Should().Be("Test validation failure");
        result.Error.Type.Should().Be(ApplicationErrorType.Validation);
    }

    [Fact]
    public void Failure_result_should_throw_when_error_is_null()
    {
        Action act = () => ApplicationResult.Failure(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validation_error_should_preserve_details_when_supplied()
    {
        var details = new Dictionary<string, string[]>
        {
            ["Sql"] = new[] { "Only SELECT statements are allowed." }
        };

        var error = ApplicationError.Validation(
            "Invalid schema-view SQL.",
            details);

        error.Code.Should().Be(ApplicationErrorCodes.ValidationFailed);
        error.Message.Should().Be("Invalid schema-view SQL.");
        error.Type.Should().Be(ApplicationErrorType.Validation);
        error.Details.Should().NotBeNull();
        error.Details!.Should().ContainKey("Sql");
        error.Details["Sql"].Should().Contain("Only SELECT statements are allowed.");
    }

    [Fact]
    public void Not_found_error_should_have_not_found_type()
    {
        var error = ApplicationError.NotFound("Material was not found.");

        error.Code.Should().Be(ApplicationErrorCodes.NotFound);
        error.Message.Should().Be("Material was not found.");
        error.Type.Should().Be(ApplicationErrorType.NotFound);
    }

    [Fact]
    public void Conflict_error_should_have_conflict_type()
    {
        var error = ApplicationError.Conflict("Concurrent update detected.");

        error.Code.Should().Be(ApplicationErrorCodes.Conflict);
        error.Message.Should().Be("Concurrent update detected.");
        error.Type.Should().Be(ApplicationErrorType.Conflict);
    }

    [Fact]
    public void Infrastructure_error_should_have_infrastructure_type()
    {
        var error = ApplicationError.Infrastructure("Database unavailable.");

        error.Code.Should().Be(ApplicationErrorCodes.InfrastructureFailure);
        error.Message.Should().Be("Database unavailable.");
        error.Type.Should().Be(ApplicationErrorType.Infrastructure);
    }
}