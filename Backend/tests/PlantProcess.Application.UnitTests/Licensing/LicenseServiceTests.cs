using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Options;
using PlantProcess.Application.Licensing.Services;

namespace PlantProcess.Application.UnitTests.Licensing;

public sealed class LicenseServiceTests
{
    [Fact]
    public void Light_should_block_postgresql_connector()
    {
        var service = CreateService("Light");

        var result = service.EnsureConnectorAllowed("PostgreSql");

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ApplicationErrorType.Forbidden);
    }

    [Fact]
    public void Pro_should_allow_postgresql_connector()
    {
        var service = CreateService("Pro");

        var result = service.EnsureConnectorAllowed("PostgreSql");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Light_should_block_15_minute_refresh()
    {
        var service = CreateService("Light");

        var result = service.EnsureRefreshIntervalAllowed(15);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Pro_should_allow_15_minute_refresh()
    {
        var service = CreateService("Pro");

        var result = service.EnsureRefreshIntervalAllowed(15);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Pro_should_block_2_minute_refresh()
    {
        var service = CreateService("Pro");

        var result = service.EnsureRefreshIntervalAllowed(2);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ProPlus_should_allow_2_minute_refresh()
    {
        var service = CreateService("ProPlus");

        var result = service.EnsureRefreshIntervalAllowed(2);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Light_should_block_schema_sql_builder()
    {
        var service = CreateService("Light");

        var result = service.EnsureFeatureEnabled(LicenseFeature.SchemaSqlViewBuilder);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Pro_should_allow_schema_sql_builder()
    {
        var service = CreateService("Pro");

        var result = service.EnsureFeatureEnabled(LicenseFeature.SchemaSqlViewBuilder);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Pro_should_block_ml_learning_jobs()
    {
        var service = CreateService("Pro");

        var result = service.EnsureFeatureEnabled(LicenseFeature.MlLearningJobs);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ProPlus_should_allow_ml_learning_jobs()
    {
        var service = CreateService("ProPlus");

        var result = service.EnsureFeatureEnabled(LicenseFeature.MlLearningJobs);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Light_should_block_second_source_when_limit_is_reached()
    {
        var service = CreateService("Light");

        var result = service.EnsureSourceCountAllowed(currentActiveSourceCount: 1);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Pro_should_block_fourth_source_when_limit_is_reached()
    {
        var service = CreateService("Pro");

        var result = service.EnsureSourceCountAllowed(currentActiveSourceCount: 3);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ProPlus_should_allow_until_eight_sources()
    {
        var service = CreateService("ProPlus");

        var result = service.EnsureSourceCountAllowed(currentActiveSourceCount: 7);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Light_should_block_fourth_dashboard_when_limit_is_reached()
    {
        var service = CreateService("Light");

        var result = service.EnsureDashboardCountAllowed(currentActiveDashboardCount: 3);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Enterprise_should_allow_branded_reports()
    {
        var service = CreateService("Enterprise");

        var result = service.EnsureFeatureEnabled(LicenseFeature.BrandedReportPdf);

        result.IsSuccess.Should().BeTrue();
    }

    private static LicenseService CreateService(string tier)
    {
        var options = Options.Create(new LicenseOptions
        {
            Tier = tier,
            IsTrial = false,
            DisplayName = $"{tier} Test License"
        });

        return new LicenseService(
            options,
            NullLogger<LicenseService>.Instance);
    }
}