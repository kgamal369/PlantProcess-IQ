namespace PlantProcess.Application.Common.Validation;

public static class Guard
{
    public static void AgainstEmpty(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException($"{fieldName} cannot be empty.", fieldName);
    }

    public static void AgainstNullOrWhiteSpace(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{fieldName} is required.", fieldName);
    }

    public static void AgainstNegative(int value, string fieldName)
    {
        if (value < 0)
            throw new ArgumentException($"{fieldName} cannot be negative.", fieldName);
    }

    public static void AgainstInvalidUtc(DateTime value, string fieldName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException($"{fieldName} must be UTC.", fieldName);
    }

    public static void AgainstInvalidUtc(DateTime? value, string fieldName)
    {
        if (value.HasValue && value.Value.Kind != DateTimeKind.Utc)
            throw new ArgumentException($"{fieldName} must be UTC.", fieldName);
    }
}



