namespace PlantProcess.Application.Analytics.Value;

/// <summary>
/// PPIQ-501: downtime modelled as TWO distinct quantities. Equipment-stopped minutes are the raw
/// time the equipment was halted; production-impact minutes are the time production output was
/// actually lost. A buffered downstream stop (e.g. an HSM stop absorbed by a coil buffer) costs
/// only the production-impact minutes; an unbuffered hard stop (e.g. a caster water-pump trip)
/// propagates the full equipment stop to production. The value engine must consume the
/// attributable production-stop minutes, never the raw equipment-stop time, and must abstain
/// rather than guess when the buffer posture is unknown.
/// </summary>
public enum DowntimeBufferPosture
{
    Unknown = 0,
    BufferedDownstream = 1,
    UnbufferedHardStop = 2
}

public sealed record DowntimeImpactInputs(
    decimal EquipmentStoppedMinutes,
    decimal ProductionImpactMinutes,
    DowntimeBufferPosture Posture);

public sealed record DowntimeImpactResult(
    decimal AttributableProductionStopMinutes,
    string Basis,
    bool IsAbstained,
    string? AbstainReason);

public static class DowntimeImpactCalculator
{
    public static DowntimeImpactResult Resolve(DowntimeImpactInputs inputs)
    {
        if (inputs.EquipmentStoppedMinutes < 0m || inputs.ProductionImpactMinutes < 0m)
        {
            return new DowntimeImpactResult(0m, "none", true,
                "Negative downtime minutes: refusing to attribute a production stop.");
        }

        return inputs.Posture switch
        {
            DowntimeBufferPosture.BufferedDownstream =>
                new DowntimeImpactResult(inputs.ProductionImpactMinutes, "production-impact", false, null),

            DowntimeBufferPosture.UnbufferedHardStop =>
                new DowntimeImpactResult(inputs.EquipmentStoppedMinutes, "equipment-stopped", false, null),

            _ =>
                new DowntimeImpactResult(0m, "none", true,
                    "Unknown buffer posture: cannot attribute production-stop minutes without it.")
        };
    }
}