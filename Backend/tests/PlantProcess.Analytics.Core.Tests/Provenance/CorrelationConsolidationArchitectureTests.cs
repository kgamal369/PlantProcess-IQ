using System;
using System.Linq;
using PlantProcess.Application.Analytics.Services;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests;

/// <summary>
/// T03/T102 consolidation guard.
/// The legacy CorrelationService must not silently present itself as the canonical inferential engine.
/// Until full T102 delegation is implemented, the legacy service must remain explicitly quarantined
/// behind an Obsolete contract that points callers to the canonical Analytics.Core engine.
/// </summary>
public class CorrelationConsolidationArchitectureTests
{
    [Fact]
    public void Legacy_correlation_service_is_explicitly_quarantined_by_canonical_engine_contract()
    {
        var attribute = typeof(CorrelationService)
            .GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false)
            .OfType<ObsoleteAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);

        var message = attribute!.Message ?? string.Empty;

        Assert.Contains("canonical Analytics.Core engine", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not for inferential claims", message, StringComparison.OrdinalIgnoreCase);
    }
}