using FluentAssertions;
using PlantProcess.Application.Integration.Connectors;
using Xunit;

namespace PlantProcess.Application.UnitTests.Integration;

public sealed class ConnectorCertificationGateTests
{
    [Fact]
    public void IsCertified_False_WhenEnvUnset()
    {
        const string key = "PPIQ_CONNECTOR_CERTIFIED_POSTGRESQL";
        var prior = System.Environment.GetEnvironmentVariable(key);
        try
        {
            System.Environment.SetEnvironmentVariable(key, null);
            ConnectorCertification.IsCertified("PostgreSql").Should().BeFalse();
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(key, prior);
        }
    }

    [Theory]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("TRUE")]
    public void IsCertified_True_WhenEnvAffirmative(string value)
    {
        const string key = "PPIQ_CONNECTOR_CERTIFIED_MYSQL";
        var prior = System.Environment.GetEnvironmentVariable(key);
        try
        {
            System.Environment.SetEnvironmentVariable(key, value);
            ConnectorCertification.IsCertified("MySql").Should().BeTrue();
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(key, prior);
        }
    }

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("")]
    public void IsCertified_False_WhenEnvNegative(string value)
    {
        const string key = "PPIQ_CONNECTOR_CERTIFIED_ORACLE";
        var prior = System.Environment.GetEnvironmentVariable(key);
        try
        {
            System.Environment.SetEnvironmentVariable(key, value);
            ConnectorCertification.IsCertified("Oracle").Should().BeFalse();
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(key, prior);
        }
    }
}