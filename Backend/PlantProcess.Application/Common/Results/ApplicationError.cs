using PlantProcess.Application.Common.Constants;

namespace PlantProcess.Application.Common.Results;

public sealed record ApplicationError(
    string Code,
    string Message,
    ApplicationErrorType Type,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static ApplicationError Validation(
        string message,
        IReadOnlyDictionary<string, string[]>? details = null)
    {
        return new ApplicationError(
            ApplicationErrorCodes.ValidationFailed,
            message,
            ApplicationErrorType.Validation,
            details);
    }

    public static ApplicationError NotFound(string message)
    {
        return new ApplicationError(
            ApplicationErrorCodes.NotFound,
            message,
            ApplicationErrorType.NotFound);
    }

    public static ApplicationError Conflict(string message)
    {
        return new ApplicationError(
            ApplicationErrorCodes.Conflict,
            message,
            ApplicationErrorType.Conflict);
    }

    public static ApplicationError BusinessRule(string message)
    {
        return new ApplicationError(
            ApplicationErrorCodes.BusinessRuleViolation,
            message,
            ApplicationErrorType.BusinessRule);
    }

    public static ApplicationError Forbidden(string message)
    {
        return new ApplicationError(
            ApplicationErrorCodes.Forbidden,
            message,
            ApplicationErrorType.Forbidden);
    }
    
    public static ApplicationError Infrastructure(string message)
    {
        return new ApplicationError(
            ApplicationErrorCodes.InfrastructureFailure,
            message,
            ApplicationErrorType.Infrastructure);
    }

    public static ApplicationError Unexpected(string message)
    {
        return new ApplicationError(
            ApplicationErrorCodes.UnexpectedFailure,
            message,
            ApplicationErrorType.Unexpected);
    }

    public static ApplicationError NotImplemented(string serviceName)
    {
        return new ApplicationError(
            ApplicationErrorCodes.NotImplemented,
            $"{serviceName} is registered, but its implementation will be added in the next phase.",
            ApplicationErrorType.NotImplemented);
    }
}



