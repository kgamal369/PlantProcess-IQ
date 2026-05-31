
using Microsoft.Extensions.Configuration;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;

namespace PlantProcess.Application.Analytics.Services;

/// <summary>
/// Config switch for narrative providers.
/// PlantProcess:AI:NarrativeProvider = local | api
/// Local is the safe default. API is optional and still receives sanitized findings only.
/// </summary>
public sealed class ConfiguredNarrativeProvider : INarrativeProvider
{
    private readonly IConfiguration _configuration;
    private readonly LocalNarrativeProvider _localProvider;
    private readonly ApiNarrativeProvider _apiProvider;

    public ConfiguredNarrativeProvider(
        IConfiguration configuration,
        LocalNarrativeProvider localProvider,
        ApiNarrativeProvider apiProvider)
    {
        _configuration = configuration;
        _localProvider = localProvider;
        _apiProvider = apiProvider;
    }

    public string ProviderKey => ResolveProvider().ProviderKey;

    public Task<NarrativeResult> GenerateAsync(
        NarrativeRequest request,
        CancellationToken cancellationToken)
    {
        var selected = ResolveProvider();

        if (selected is ApiNarrativeProvider && !request.AllowExternalProvider)
        {
            return _localProvider.GenerateAsync(request, cancellationToken);
        }

        return selected.GenerateAsync(request, cancellationToken);
    }

    private INarrativeProvider ResolveProvider()
    {
        var configured = _configuration["PlantProcess:AI:NarrativeProvider"];

        return string.Equals(configured, "api", StringComparison.OrdinalIgnoreCase)
            ? _apiProvider
            : _localProvider;
    }
}
