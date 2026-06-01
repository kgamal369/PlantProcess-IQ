namespace PlantProcess.Api.Security;

public sealed class AuthOptions
{
    public string Issuer { get; set; } = "PlantProcessIQ";

    public string Audience { get; set; } = "PlantProcessIQ.Client";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 120;

    public string? BootstrapAdminUser { get; set; }

    public string? BootstrapAdminPassword { get; set; }

    public bool BootstrapAdminForcePasswordChange { get; set; } = true;

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