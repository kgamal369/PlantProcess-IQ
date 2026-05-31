
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;

namespace PlantProcess.Application.Analytics.Services;

/// <summary>
/// Optional API-backed narrative provider.
/// It sends only sanitized abstracted findings and never raw plant rows, material IDs,
/// equipment logs, screenshots, source documents, or customer secrets.
/// </summary>
public sealed class ApiNarrativeProvider : INarrativeProvider
{
    private readonly IConfiguration _configuration;
    private static readonly HttpClient Http = new();

    public ApiNarrativeProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ProviderKey => "api-narrative-v1";

    public async Task<NarrativeResult> GenerateAsync(
        NarrativeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var endpoint = _configuration["PlantProcess:AI:NarrativeEndpoint"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new NarrativeResult(
                "API narrative provider is configured but no endpoint is set. Offline fallback should be used.",
                ProviderKey,
                "external-configured-model",
                UsedExternalApi: false,
                RawPlantDataIncluded: false,
                SafetyNotes: new[]
                {
                    "api-provider-not-called",
                    "missing-endpoint",
                    "safe-fallback-required"
                });
        }

        var sanitizedPayload = new
        {
            purpose = request.Purpose,
            audience = request.Audience,
            findings = request.Findings.Select(x => new
            {
                x.FindingStatus,
                x.FeatureKey,
                x.OutcomeKey,
                x.Method,
                x.EffectSize,
                x.QValue,
                x.EffectiveN,
                x.Direction,
                x.StrengthBucket,
                x.ConfoundingNote,
                x.SafeMetadata
            }).ToArray(),
            safety = new
            {
                rawMaterialIdsIncluded = false,
                rawSourceRowsIncluded = false,
                rawEquipmentLogsIncluded = false,
                piiIncluded = false,
                customerSecretsIncluded = false
            }
        };

        using var response = await Http.PostAsJsonAsync(endpoint, sanitizedPayload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        return new NarrativeResult(
            text,
            ProviderKey,
            _configuration["PlantProcess:AI:NarrativeModel"] ?? "external-configured-model",
            UsedExternalApi: true,
            RawPlantDataIncluded: false,
            SafetyNotes: new[]
            {
                "api-provider-called",
                "sanitized-findings-only",
                "no-material-ids",
                "no-raw-source-rows",
                "no-equipment-log-payload"
            });
    }
}
