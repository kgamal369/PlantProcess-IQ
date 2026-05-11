namespace PlantProcess.Application.Common.Validation;

public sealed record ValidationFailure(
    string Field,
    string Message,
    string? Code = null);