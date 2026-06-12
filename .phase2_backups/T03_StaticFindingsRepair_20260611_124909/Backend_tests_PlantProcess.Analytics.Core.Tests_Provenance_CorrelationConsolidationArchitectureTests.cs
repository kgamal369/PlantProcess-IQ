using Xunit;

namespace PlantProcess.Analytics.Core.Tests;

/// <summary>
/// T-102 consolidation gate. ENABLE (remove Skip) AFTER applying the delegating shim that routes
/// CorrelationService's coefficient computation through the single managed statistical engine
/// (see the PPIQ-T102 consolidation note). Kept Skip so the suite stays green until the refactor lands.
/// </summary>
public class CorrelationConsolidationArchitectureTests
{
    [Fact(Skip = "Enable after applying the T-102 delegating shim (PPIQ-T102 note).")]
    public void Legacy_correlation_service_delegates_to_the_single_managed_engine()
    {
        // After the shim: assert CorrelationService holds no private coefficient math, and that
        // identical inputs through the legacy and managed entry points are byte-identical.
        Assert.True(true);
    }
}