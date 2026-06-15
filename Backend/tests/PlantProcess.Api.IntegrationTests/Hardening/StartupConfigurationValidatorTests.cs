using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using PlantProcess.Api.Configuration;
using PlantProcess.Api.Options;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Hardening;

// P2-T03 (64-char signing-key floor) and P2-T02 (trust-anchor: dev/template keys
// rejected outside Development). Calls the validator directly and asserts on the
// aggregated InvalidOperationException message, so unrelated config gaps do not
// affect the signing-key assertions.
public sealed class StartupConfigurationValidatorTests
{
    private static InvalidOperationException? Run(string signingKey, string environmentName)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlantProcess:Auth:SigningKey"] = signingKey,
            })
            .Build();

        var env = new TestWebHostEnvironment { EnvironmentName = environmentName };
        var options = new PlantProcessOptions
        {
            RequireConfiguredCors = false,
            RequireDatabaseConnectionString = false,
        };

        try
        {
            StartupConfigurationValidator.Validate(config, env, options, Array.Empty<string>());
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    [SkippableFact]
    public void Rejects_signing_key_shorter_than_64_in_production()
    {
        var ex = Run(new string('A', 63), "Production");
        Assert.NotNull(ex);
        Assert.Contains("at least 64 characters", ex!.Message);
    }

    [SkippableFact]
    public void Rejects_development_marker_signing_key_outside_development()
    {
        // length is fine (>= 64) but the key carries a dev marker -> must be rejected
        var ex = Run("DEV_ONLY_" + new string('A', 60), "Production");
        Assert.NotNull(ex);
        Assert.Contains("Remove DEV_ONLY/CHANGE_THIS", ex!.Message);
    }

    [SkippableFact]
    public void Accepts_strong_64_char_signing_key_in_production_no_length_error()
    {
        var ex = Run(new string('A', 64), "Production");
        Assert.True(ex == null || !ex.Message.Contains("at least 64 characters"),
            "a strong 64-char key must not trip the signing-key length floor");
    }
}