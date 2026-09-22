using System.Text.Json;

namespace PlantProcess.Application.Jobs.Execution.Transformations;

/// <summary>
/// THE CANONICAL REFRESH PARAMETER VOCABULARY.
///
/// T-064 froze the transport and left each job class to own its keys. This class owns
/// exactly one: projection. Absent parameters, an empty object and
/// {"projection":"ordinary"} are an ordinary run; {"projection":"reproject"} is the
/// explicit authority to replace an existing canonical effect. Anything else is refused
/// before a run exists, because an unrecognised key that silently meant nothing is
/// exactly how an operator believes a reprojection was authorised when it was not.
/// </summary>
public static class CanonicalProjectionParameters
{
    public const string ProjectionKey = "projection";
    public const string OrdinaryValue = "ordinary";
    public const string ReprojectValue = "reproject";

    /// <summary>Returns null when the parameters are lawful, or a sentence naming the defect.</summary>
    public static string? TryRead(string? parametersJson, out CanonicalProjectionMode mode)
    {
        mode = CanonicalProjectionMode.Ordinary;

        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(parametersJson);
        }
        catch (JsonException)
        {
            return "The job's target parameters are not valid JSON.";
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return "The job's target parameters must be a JSON object.";
            }

            bool seen = false;
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (seen)
                {
                    mode = CanonicalProjectionMode.Ordinary;
                    return "Target parameter '" + property.Name + "' is declared more than once.";
                }

                if (!string.Equals(property.Name, ProjectionKey, StringComparison.Ordinal))
                {
                    return "Target parameter '" + property.Name + "' is not part of the canonical refresh vocabulary; "
                        + "only '" + ProjectionKey + "' is accepted.";
                }

                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return "Target parameter '" + ProjectionKey + "' must be the text '" + OrdinaryValue
                        + "' or '" + ReprojectValue + "'.";
                }

                seen = true;
                string? value = property.Value.GetString();
                if (string.Equals(value, OrdinaryValue, StringComparison.Ordinal))
                {
                    mode = CanonicalProjectionMode.Ordinary;
                }
                else if (string.Equals(value, ReprojectValue, StringComparison.Ordinal))
                {
                    mode = CanonicalProjectionMode.Reproject;
                }
                else
                {
                    mode = CanonicalProjectionMode.Ordinary;
                    return "Target parameter '" + ProjectionKey + "' must be the text '" + OrdinaryValue
                        + "' or '" + ReprojectValue + "'.";
                }
            }
        }

        return null;
    }
}
