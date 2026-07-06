using System;
using PlantProcess.Infrastructure.Connectors;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// M1-03: guards the connector credential contract. A credential-less profile must throw a
/// typed error and must NEVER resolve to an empty username (the 'ELKA01' OS-identity defect).
/// </summary>
public sealed class ConnectorCredentialResolverTests
{
    [Fact]
    public void No_credentials_throws_typed_error_and_never_empty_username()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ConnectorCredentialResolver.Resolve(null, null, "PostgreSQL"));
        Assert.Contains("no credentials resolved", ex.Message);
        Assert.Contains("will not inherit the host account identity", ex.Message);
    }

    [Fact]
    public void Empty_options_and_blank_secret_still_hard_fails()
    {
        Assert.Throws<InvalidOperationException>(
            () => ConnectorCredentialResolver.Resolve("{}", "   ", "Oracle"));
    }

    [Fact]
    public void Options_json_credentials_win()
    {
        var c = ConnectorCredentialResolver.Resolve(
            "{\"username\":\"ppiq_src\",\"password\":\"ppiq_src_local_only\"}", null, "PostgreSQL");
        Assert.Equal("ppiq_src", c.Username);
        Assert.Equal("ppiq_src_local_only", c.Password);
    }

    [Fact]
    public void Secret_reference_resolves_from_environment()
    {
        Environment.SetEnvironmentVariable("PPIQ_TEST_REF", "envuser");
        Environment.SetEnvironmentVariable("PPIQ_TEST_REF_PASSWORD", "envpass");
        try
        {
            var c = ConnectorCredentialResolver.Resolve(null, "PPIQ_TEST_REF", "MySQL");
            Assert.Equal("envuser", c.Username);
            Assert.Equal("envpass", c.Password);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PPIQ_TEST_REF", null);
            Environment.SetEnvironmentVariable("PPIQ_TEST_REF_PASSWORD", null);
        }
    }

    [Fact]
    public void Malformed_options_json_throws_typed()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ConnectorCredentialResolver.Resolve("{not json", null, "SqlServer"));
        Assert.Contains("not valid JSON", ex.Message);
    }
}
