using PlantProcess.Application.Dashboarding.Interfaces;

namespace PlantProcess.Api.Hosting;

/// <summary>
/// Invokes the system-template authority once per start.
///
/// The product has one operational authority for system dashboard templates,
/// IDashboardDefinitionService.EnsureSystemTemplatesAsync. Until this service
/// existed nothing called it on the startup path: it was reachable only through
/// the ensure endpoint, and a retired SQL seed happened to create the same
/// family on every replay, which hid the gap. With that seed retired, a clean
/// installation produced no templates at all.
///
/// The authority is idempotent and repairs drift, so calling it every start is
/// the whole mechanism. A second start changes nothing.
///
/// Failure never stops the API, matching FirstRunProvisioningHostedService: a
/// template that could not be reconciled is a degraded surface, not a reason to
/// refuse to serve. The failure is logged and the ensure endpoint remains
/// available to retry it.
/// </summary>
public sealed class SystemTemplateProvisioningHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SystemTemplateProvisioningHostedService> _logger;

    public SystemTemplateProvisioningHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SystemTemplateProvisioningHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var authority = scope.ServiceProvider.GetRequiredService<IDashboardDefinitionService>();

            var result = await authority.EnsureSystemTemplatesAsync(cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "System template provisioning did not succeed: {Error}. The API will continue to start and the " +
                    "ensure endpoint can retry it.",
                    result.Error);

                return;
            }

            if (result.Value == 0)
            {
                _logger.LogInformation("System templates already present; nothing created.");
            }
            else
            {
                _logger.LogInformation("System templates provisioned: {Created} created.", result.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "System template provisioning failed; the API will continue to start. Provisioning can be retried " +
                "through the ensure endpoint.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}