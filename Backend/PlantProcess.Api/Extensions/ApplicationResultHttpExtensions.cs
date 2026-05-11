using PlantProcess.Application.Common.Results;

namespace PlantProcess.Api.Extensions;

public static class ApplicationResultHttpExtensions
{
    public static IResult ToHttpResult<T>(
        this ApplicationResult<T> result,
        Func<T, IResult> onSuccess)
    {
        if (result.IsSuccess && result.Value is not null)
            return onSuccess(result.Value);

        return ToProblem(result.Error);
    }

    public static IResult ToHttpResult(
        this ApplicationResult result,
        Func<IResult> onSuccess)
    {
        if (result.IsSuccess)
            return onSuccess();

        return ToProblem(result.Error);
    }

    private static IResult ToProblem(ApplicationError? error)
    {
        if (error is null)
        {
            return Results.Problem(
                title: "unexpected.failure",
                detail: "An unexpected application error occurred.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var statusCode = error.Type switch
        {
            ApplicationErrorType.Validation => StatusCodes.Status400BadRequest,
            ApplicationErrorType.NotFound => StatusCodes.Status404NotFound,
            ApplicationErrorType.Conflict => StatusCodes.Status409Conflict,
            ApplicationErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            ApplicationErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ApplicationErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ApplicationErrorType.NotImplemented => StatusCodes.Status501NotImplemented,
            ApplicationErrorType.Infrastructure => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: statusCode,
            extensions: error.Details is null
                ? null
                : new Dictionary<string, object?>
                {
                    ["errors"] = error.Details
                });
    }
}