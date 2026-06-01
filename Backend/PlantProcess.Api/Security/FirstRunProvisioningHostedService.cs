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
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<AuthStore>();

        var hasUser = await store.HasAnyUserAsync(cancellationToken);
        if (hasUser)
        {
            _state.Clear();
            return;
        }

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
