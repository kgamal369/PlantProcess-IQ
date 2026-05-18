namespace PlantProcess.Application.Common.Text;

public static class TextNormalizer
{
    public static string RequiredTrim(
        string? value,
        string fieldName,
        int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{fieldName} is required.", fieldName);

        var normalized = value.Trim();

        if (maxLength.HasValue && normalized.Length > maxLength.Value)
            throw new ArgumentException($"{fieldName} cannot exceed {maxLength.Value} characters.", fieldName);

        return normalized;
    }

    public static string? OptionalTrim(
        string? value,
        int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        if (maxLength.HasValue && normalized.Length > maxLength.Value)
            throw new ArgumentException($"Value cannot exceed {maxLength.Value} characters.");

        return normalized;
    }

    public static string NormalizeCode(string value, string fieldName, int? maxLength = 100)
    {
        return RequiredTrim(value, fieldName, maxLength).ToUpperInvariant();
    }
}



