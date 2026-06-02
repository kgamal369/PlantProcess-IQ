using PlantProcessIQ.Domain.Downtime;

namespace PlantProcessIQ.Application.Downtime;

public sealed class DowntimePropagationResult
{
    public required string NodeId { get; init; }
    public required double EquipmentStopMinutes { get; init; }
    public required double ProductionStopMinutes { get; init; }
    public required bool   FullyAbsorbed { get; init; }
    public required string Explanation { get; init; }
}

/// v4 5.2 - converts equipment-stoppage minutes into production-stoppage minutes,
/// the quantity the learning layer (P06) consumes. Deterministic; covered by T-024.
public sealed class DowntimePropagationService
{
    private readonly LineTopology _line;
    public DowntimePropagationService(LineTopology line) => _line = line;

    public DowntimePropagationResult Propagate(DowntimeEvent ev)
    {
        if (ev.EquipmentStopMinutes < 0) throw new ArgumentOutOfRangeException(nameof(ev), "Equipment stop minutes cannot be negative.");
        var node = _line.Node(ev.NodeId);
        double d = ev.EquipmentStopMinutes;

        // 1) Sequence-critical continuous node: a short trip breaks the casting sequence.
        if (node.IsSequenceCritical)
        {
            double prod = node.SequenceBreakPenaltyMinutes + Math.Ceiling(d * node.RampFactor);
            ev.ProductionStopMinutes = prod;
            return new()
            {
                NodeId = node.Id, EquipmentStopMinutes = d, ProductionStopMinutes = prod, FullyAbsorbed = false,
                Explanation = $"Sequence-critical {node.Name}: {d:0.#} min trip breaks the sequence " +
                              $"(+{node.SequenceBreakPenaltyMinutes:0} restart, +{d * node.RampFactor:0} ramp) => {prod:0} production-stop min."
            };
        }

        // 2) Off the critical path: never blocks production.
        if (!node.IsCriticalPath)
        {
            ev.ProductionStopMinutes = 0;
            return new()
            {
                NodeId = node.Id, EquipmentStopMinutes = d, ProductionStopMinutes = 0, FullyAbsorbed = true,
                Explanation = $"{node.Name} is off the critical path: 0 production-stop min."
            };
        }

        // 3) Critical path with a downstream buffer: absorb up to capacity, amplify the remainder.
        double absorbed   = Math.Min(d, node.DownstreamBufferMinutes);
        double unabsorbed = d - absorbed;
        double production = Math.Ceiling(unabsorbed * (1.0 + node.CascadeAmplification));
        ev.ProductionStopMinutes = production;
        bool full = unabsorbed <= 0;
        string why = full
            ? $"{node.Name}: {d:0.#} min fully absorbed by {node.DownstreamBufferMinutes:0} min buffer => 0 production-stop min."
            : $"{node.Name}: {absorbed:0.#} min absorbed, {unabsorbed:0.#} min cascades x{1 + node.CascadeAmplification:0.0} => {production:0} production-stop min.";
        return new() { NodeId = node.Id, EquipmentStopMinutes = d, ProductionStopMinutes = production, FullyAbsorbed = full, Explanation = why };
    }
}