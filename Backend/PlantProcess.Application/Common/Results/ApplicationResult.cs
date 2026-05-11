namespace PlantProcess.Application.Common.Results;

public sealed class ApplicationResult
{
    private ApplicationResult(bool isSuccess, ApplicationError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public ApplicationError? Error { get; }

    public static ApplicationResult Success()
    {
        return new ApplicationResult(true, null);
    }

    public static ApplicationResult Failure(ApplicationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ApplicationResult(false, error);
    }
}