using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Common.Validation;

public sealed class ValidationResult
{
    private readonly List<ValidationFailure> _failures = new();

    public IReadOnlyList<ValidationFailure> Failures => _failures;

    public bool IsValid => _failures.Count == 0;

    public void Add(string field, string message, string? code = null)
    {
        _failures.Add(new ValidationFailure(field, message, code));
    }

    public ApplicationError ToApplicationError(string message = "Validation failed.")
    {
        var details = _failures
            .GroupBy(x => x.Field)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.Message).ToArray());

        return ApplicationError.Validation(message, details);
    }
}