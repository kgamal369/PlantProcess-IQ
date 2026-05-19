namespace PlantProcess.Api.Security;

public sealed class AuthOptions
{
    public string Issuer { get; set; } = "PlantProcessIQ";

    public string Audience { get; set; } = "PlantProcessIQ.Client";

    /// <summary>
    /// Must be provided by appsettings.Development.json for local dev,
    /// and by environment/user-secrets/vault outside Development.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 120;

    /// <summary>
    /// Backward-compatible bootstrap admin. Kept for local/dev bootstrap only.
    /// In production, StartupConfigurationValidator rejects default values.
    /// </summary>
    public string? BootstrapAdminUser { get; set; }

    public string? BootstrapAdminPassword { get; set; }

    /// <summary>
    /// MVP user source. This is not the final user store.
    /// It gives you real role tokens now: Admin / DataManager / Engineer / Viewer.
    /// Phase 12 should replace this with a DB-backed user/role table.
    /// </summary>
    public List<BootstrapUserOptions> Users { get; set; } = new();
}

public sealed class BootstrapUserOptions
{
    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = "Viewer";

    public string? DisplayName { get; set; }
}