const fs = require("node:fs");
const path = require("node:path");

const file = "Backend/tests/PlantProcess.Api.IntegrationTests/Infrastructure/AuthenticatedApiTestBase.cs";
let text = fs.readFileSync(file, "utf8");

const oldBlock = /private static string ResolveIntegrationTestConnectionString\(\)\s*\{[\s\S]*?\n\s*\}\s*\n\s*private static string\? ReadConnectionStringFromAppSettings\(\)/m;

const newBlock = `private static string ResolveIntegrationTestConnectionString()
    {
        var candidates = new[]
        {
            NormalizeLocalConnectionString(Environment.GetEnvironmentVariable("PPIQ_TEST_CONNECTION_STRING")),
            NormalizeLocalConnectionString(Environment.GetEnvironmentVariable("PLANTPROCESS_TEST_CONNECTION_STRING")),
            NormalizeLocalConnectionString(Environment.GetEnvironmentVariable("ConnectionStrings__PlantProcessDb")),
            NormalizeLocalConnectionString(ReadConnectionStringFromAppSettings()),
        };

        foreach (var candidate in candidates)
        {
            if (IsUsableConnectionString(candidate))
            {
                return candidate!;
            }
        }

        throw new InvalidOperationException(
            "API integration tests need a valid LOCAL PostgreSQL connection string. " +
            "Do not use Infrastructure\\\\deploy\\\\.env here unless Docker/Postgres is really running with that same password. " +
            "Set PPIQ_TEST_CONNECTION_STRING in the same PowerShell session, for example: " +
            "$env:PPIQ_TEST_CONNECTION_STRING='Host=localhost;Port=5432;Database=plantprocessiq;Username=postgres;Password=<your-real-local-password>'");
    }

    private static string? NormalizeLocalConnectionString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().Trim('"');

        normalized = normalized
            .Replace("Host=postgres", "Host=localhost", StringComparison.OrdinalIgnoreCase)
            .Replace("Server=postgres", "Server=localhost", StringComparison.OrdinalIgnoreCase)
            .Replace("Data Source=postgres", "Data Source=localhost", StringComparison.OrdinalIgnoreCase);

        return normalized;
    }

    private static string? ReadConnectionStringFromAppSettings()`;

if (!oldBlock.test(text)) {
  throw new Error("Could not find ResolveIntegrationTestConnectionString block.");
}

text = text.replace(oldBlock, newBlock);

fs.writeFileSync(file, text, "utf8");
console.log("Patched AuthenticatedApiTestBase.cs to stop using deploy .env fallback for Windows dotnet test.");
