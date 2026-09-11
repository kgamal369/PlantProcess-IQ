// PPIQ T-252. THE ONE AUTHORITY FOR A MUTABLE AUTOMATED TEST DATABASE.
//
// Before this existed, seven independent fallback chains each ended in a
// hardcoded literal - six at ppiq_app and one at ppiq_presentation. Setting an
// environment variable did not remove them; any invocation that forgot the
// variable silently acquired permission to mutate a long-lived database. The
// literal was the defect, not the absence of a variable.
//
// So there is no fallback here at all. A mutating automated test either receives
// an explicit connection from the runner that owns the database's lifecycle, or
// it does not run. On a developer laptop that is a precise skip; under the
// release runner the connection is always supplied, so nothing skips.
//
// Master Design 4.5.2b assigns each database a role that the others cannot
// borrow. These five can never be the target of automated mutation.
namespace PlantProcess.TestSupport;

public static class TestDatabaseTarget
{
    public const string IntegrationVariable = "PPIQ_T252_INTEGRATION_CONNECTION";
    public const string RegressionVariable = "PPIQ_T252_REGRESSION_CONNECTION";

    // A database the test process itself connects to cannot be a TEMPLATE source:
    // PostgreSQL refuses the copy while any session is attached, and freeing it by
    // force is precisely the shared-state mutation T-252 exists to remove. So the
    // runner provisions a separate copy that nothing else ever opens.
    public const string TemplateSourceVariable = "PPIQ_T252_TEMPLATE_CONNECTION";

    private static readonly string[] ProtectedNames =
    {
        "postgres",
        "ppiq_app",
        "ppiq_presentation",
        "ppiq_acceptance_empty",
        "plantprocessiq"
    };

    public static bool IsProtected(string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return false;
        }

        foreach (var name in ProtectedNames)
        {
            if (string.Equals(name, databaseName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // Deliberately not Npgsql's builder: this type is linked into two test
    // projects and must not drag a provider dependency into either of them.
    public static string? DatabaseNameOf(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = part[..separator].Trim();
            if (string.Equals(key, "Database", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Initial Catalog", StringComparison.OrdinalIgnoreCase))
            {
                return part[(separator + 1)..].Trim();
            }
        }

        return null;
    }

    public static bool TryResolve(string variable, out string connectionString, out string reason)
    {
        connectionString = string.Empty;
        var supplied = Environment.GetEnvironmentVariable(variable);

        if (string.IsNullOrWhiteSpace(supplied))
        {
            reason = variable + " is not set. A mutating integration test needs a disposable database supplied by the runner that owns its lifecycle; it never falls back to a shared database.";
            return false;
        }

        var candidate = supplied.Trim().Trim('"');
        var database = DatabaseNameOf(candidate);

        if (string.IsNullOrWhiteSpace(database))
        {
            reason = variable + " names no database. Expected a connection string containing Database=<name>.";
            return false;
        }

        if (IsProtected(database))
        {
            reason = variable + " resolves to the protected database '" + database + "'. Automated mutation of a long-lived database is refused.";
            return false;
        }

        connectionString = candidate;
        reason = string.Empty;
        return true;
    }

    public static string Require(string variable)
    {
        if (TryResolve(variable, out var connectionString, out var reason))
        {
            return connectionString;
        }

        throw new InvalidOperationException(reason);
    }

    /// <summary>The runner-owned generic disposable database for mutating integration tests.</summary>
    public static string RequireIntegration() => Require(IntegrationVariable);

    /// <summary>
    /// The runner-owned disposable clone of ppiq_presentation. Evidence produced
    /// against it is historical populated regression evidence and never closes
    /// generic acceptance, because its provenance is the presentation oracle.
    /// </summary>
    public static string RequireRegression() => Require(RegressionVariable);

    /// <summary>A runner-owned database reserved as a TEMPLATE source; no test opens it.</summary>
    public static string RequireTemplateSource() => Require(TemplateSourceVariable);

    public static bool TryIntegration(out string connectionString, out string reason) =>
        TryResolve(IntegrationVariable, out connectionString, out reason);

    public static bool TryRegression(out string connectionString, out string reason) =>
        TryResolve(RegressionVariable, out connectionString, out reason);
}
