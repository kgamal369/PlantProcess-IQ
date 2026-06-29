namespace PlantProcess.Api.Security;

public sealed class FirstRunProvisioningState
{
    private readonly object _gate = new();

    public string? TokenHash { get; private set; }
    public DateTime? GeneratedAtUtc { get; private set; }

    public void SetToken(string rawToken)
    {
        lock (_gate)
        {
            TokenHash = PasswordHasher.Sha256(rawToken);
            GeneratedAtUtc = DateTime.UtcNow;
        }
    }

    public bool Validate(string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || string.IsNullOrWhiteSpace(TokenHash))
            return false;

        return string.Equals(PasswordHasher.Sha256(rawToken), TokenHash, StringComparison.Ordinal);
    }

    public void Clear()
    {
        lock (_gate)
        {
            TokenHash = null;
            GeneratedAtUtc = null;
        }
    }
}

public sealed class FirstRunProvisioningHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FirstRunProvisioningState _state;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FirstRunProvisioningHostedService> _logger;

    public FirstRunProvisioningHostedService(
        IServiceScopeFactory scopeFactory,
        FirstRunProvisioningState state,
        IWebHostEnvironment environment,
        ILogger<FirstRunProvisioningHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ProvisionInternalAsync(cancellationToken);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(
                ex,
                "PPIQ first-run provisioning failed for environment {Environment}; the API will continue to start. " +
                "Provisioning can be retried via the provisioning endpoint.",
                _environment.EnvironmentName);
        }
    }

    private async Task ProvisionInternalAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<AuthStore>();

        var hasUser = await store.HasAnyUserAsync(cancellationToken);
        if (hasUser)
        {
            _state.Clear();
            return;
        }

        // Auto-provision the PERMANENT sysadmin (system / support account) from configuration.
        // This is the ONLY account created at install time. Customer / tenant admins are added
        // later, manually, during commissioning - never here. The account is created with
        // is_owner=true and there is no delete-user path, so it is effectively undeletable.
        var auth = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthOptions>>().Value;
        var sysAdmin = System.Linq.Enumerable.FirstOrDefault(
            auth.Users,
            u => !string.IsNullOrWhiteSpace(u.UserName) && !string.IsNullOrWhiteSpace(u.Password));

        if (sysAdmin is not null && sysAdmin.Password.Length >= 12)
        {
            var created = await store.CreateOwnerAsync(
                sysAdmin.UserName,
                sysAdmin.Password,
                string.IsNullOrWhiteSpace(sysAdmin.DisplayName)
                    ? "PPIQ System Administrator"
                    : sysAdmin.DisplayName!,
                cancellationToken);
            _state.Clear();
            _logger.LogWarning(
                "PPIQ permanent sysadmin provisioned at first run for environment {Environment}. " +
                "UserName={UserName} (system/support account; customer admins are added later during commissioning).",
                _environment.EnvironmentName,
                created.UserName);
            return;
        }

        // Fallback: no usable configured sysadmin -> emit a one-time manual-claim token.
        var token = PasswordHasher.CreateSecureToken();
        _state.SetToken(token);

        _logger.LogWarning(
            "PPIQ FIRST-RUN PROVISIONING TOKEN generated for environment {Environment}. " +
            "Use it once at POST /auth/provisioning/claim, then store a real owner password. Token={ProvisioningToken}",
            _environment.EnvironmentName,
            token);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
