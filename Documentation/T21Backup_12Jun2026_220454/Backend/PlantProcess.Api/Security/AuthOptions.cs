using Microsoft.AspNetCore.Http;

namespace PlantProcess.Api.Security;

public sealed class AuthOptions
{
    public string Issuer { get; set; } = "PlantProcessIQ";
    public string Audience { get; set; } = "PlantProcessIQ.Client";

    /// <summary>
    /// Primary JWT signing key. Must be injected from environment / secret store.
    /// Never commit production keys to appsettings*.json.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Previous validation keys during rotation. These keys validate old tokens
    /// during a controlled overlap window but are never used to mint new tokens.
    /// </summary>
    public List<string> SecondarySigningKeys { get; set; } = new();

    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 7;

    public string RefreshCookieName { get; set; } = "__Host-ppiq-refresh";
    public bool RefreshCookieSecure { get; set; } = true;
    public SameSiteMode RefreshCookieSameSite { get; set; } = SameSiteMode.Strict;

    public string? BootstrapAdminUser { get; set; }
    public string? BootstrapAdminPassword { get; set; }
    public bool BootstrapAdminForcePasswordChange { get; set; } = true;

    /// <summary>Argon2id memory cost in KiB. Doctrine v5 target default: about 64 MB.</summary>
    public int Argon2MemoryKb { get; set; } = 65536;

    /// <summary>Argon2id iterations / time cost.</summary>
    public int Argon2Iterations { get; set; } = 3;

    /// <summary>Argon2id degree of parallelism.</summary>
    public int Argon2Parallelism { get; set; } = 1;

    /// <summary>Hash size in bytes.</summary>
    public int Argon2HashBytes { get; set; } = 32;

    public List<BootstrapUserOptions> Users { get; set; } = new();
}

public sealed class BootstrapUserOptions
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";
    public string? DisplayName { get; set; }
    public bool IsBootstrapAdmin { get; set; }
    public bool ForcePasswordChangeOnFirstLogin { get; set; }
}
