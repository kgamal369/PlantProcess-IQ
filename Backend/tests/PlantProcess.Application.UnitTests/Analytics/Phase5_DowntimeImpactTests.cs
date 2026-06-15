using PlantProcess.Application.Analytics.Value;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

/// <summary>PPIQ-501: downtime two-quantity selection (production-impact vs equipment-stopped).</summary>
public sealed class Phase5_DowntimeImpactTests
{
    [Fact]
    public void PPIQ_501_Buffered_HSM_stop_uses_production_impact_minutes()
    {
        // HSM halted 120 min, but a downstream coil buffer absorbed most of it: only 15 min of
        // production output was actually lost. The engine must use the 15.
        var r = DowntimeImpactCalculator.Resolve(new DowntimeImpactInputs(
            EquipmentStoppedMinutes: 120m,
            ProductionImpactMinutes: 15m,
            Posture: DowntimeBufferPosture.BufferedDownstream));

        Assert.False(r.IsAbstained);
        Assert.Equal("production-impact", r.Basis);
        Assert.Equal(15m, r.AttributableProductionStopMinutes);
    }

    [Fact]
    public void PPIQ_501_Caster_water_pump_stop_uses_equipment_stopped_minutes()
    {
        // Caster water-pump trip: no buffer, the equipment stop propagates fully to production.
        var r = DowntimeImpactCalculator.Resolve(new DowntimeImpactInputs(
            EquipmentStoppedMinutes: 40m,
            ProductionImpactMinutes: 40m,
            Posture: DowntimeBufferPosture.UnbufferedHardStop));

        Assert.False(r.IsAbstained);
        Assert.Equal("equipment-stopped", r.Basis);
        Assert.Equal(40m, r.AttributableProductionStopMinutes);
    }

    [Fact]
    public void PPIQ_501_Unknown_posture_abstains_instead_of_guessing()
    {
        var r = DowntimeImpactCalculator.Resolve(
            new DowntimeImpactInputs(99m, 12m, DowntimeBufferPosture.Unknown));

        Assert.True(r.IsAbstained);
        Assert.Equal(0m, r.AttributableProductionStopMinutes);
        Assert.False(string.IsNullOrWhiteSpace(r.AbstainReason));
    }
}