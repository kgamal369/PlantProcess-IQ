namespace PlantProcess.Application.Common.Results;

public sealed class ApplicationResult<T>
{
    private ApplicationResult(bool isSuccess, T? value, ApplicationError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public ApplicationError? Error { get; }

    public static ApplicationResult<T> Success(T value)
    {
        return new ApplicationResult<T>(true, value, null);
    }

    public static ApplicationResult<T> Failure(ApplicationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ApplicationResult<T>(false, default, error);
    }
}


