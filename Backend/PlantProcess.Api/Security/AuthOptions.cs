namespace PlantProcess.Api.Security;

public sealed class AuthOptions
{
    public string Issuer { get; set; } = "PlantProcessIQ";
    public string Audience { get; set; } = "PlantProcessIQ.Client";
    public string SigningKey { get; set; } = "CHANGE_THIS_DEVELOPMENT_KEY_MINIMUM_32_CHARACTERS";
    public int AccessTokenMinutes { get; set; } = 120;

    public string BootstrapAdminUser { get; set; } = "admin";
    public string BootstrapAdminPassword { get; set; } = "ChangeMe123!";
}