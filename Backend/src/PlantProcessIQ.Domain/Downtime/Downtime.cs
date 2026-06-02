namespace PlantProcessIQ.Domain.Downtime;

public enum StoppageClass { Electrical, Mechanical, Operation, Production }

public sealed record StoppageReason(string Code, string Description);

/// A raw stoppage on a line node. EquipmentStopMinutes is what the sensor/log reports;
/// ProductionStopMinutes is DERIVED by the propagation service (never stored raw - see v4 5.2).
public sealed class DowntimeEvent
{
    public required string NodeId { get; init; }
    public required StoppageClass Class { get; init; }
    public required StoppageReason Reason { get; init; }
    public required double EquipmentStopMinutes { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public double ProductionStopMinutes { get; set; }   // filled by DowntimePropagationService
}

/// One station on the production line and its buffering characteristics.
public sealed class LineNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required double ThroughputPerMinute { get; init; }     // tons or pieces / min
    public double DownstreamBufferMinutes { get; init; }          // minutes of supply held downstream of this node
    public bool   IsSequenceCritical { get; init; }               // continuous node (e.g. caster): a short stop breaks the sequence
    public double SequenceBreakPenaltyMinutes { get; init; }      // fixed restart penalty once the sequence breaks
    public double RampFactor { get; init; }                       // ramp minutes per stop-minute on a sequence-critical node
    public double CascadeAmplification { get; init; }             // amplification for an unbuffered critical-path (non-sequence) node
    public bool   IsCriticalPath { get; init; } = true;
}

public sealed class LineTopology
{
    private readonly Dictionary<string, LineNode> _nodes;
    public LineTopology(IEnumerable<LineNode> nodes) =>
        _nodes = nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

    public LineNode Node(string id) =>
        _nodes.TryGetValue(id, out var n) ? n : throw new KeyNotFoundException($"Unknown line node '{id}'.");
    public bool Has(string id) => _nodes.ContainsKey(id);

    /// The demo flat-steel line used by the worked cases (HSM roll change absorbed; caster trip cascades).
    /// INTEGRATE: replace with the topology loaded from your line-configuration store.
    public static LineTopology Demo() => new(new[]
    {
        new LineNode { Id = "CASTER", Name = "Continuous caster", ThroughputPerMinute = 4.0,
                       DownstreamBufferMinutes = 0, IsSequenceCritical = true,
                       SequenceBreakPenaltyMinutes = 300, RampFactor = 4.0, IsCriticalPath = true },
        new LineNode { Id = "REHEAT", Name = "Reheat furnace", ThroughputPerMinute = 4.0,
                       DownstreamBufferMinutes = 35, CascadeAmplification = 0.6 },
        new LineNode { Id = "HSM", Name = "Hot strip mill", ThroughputPerMinute = 4.0,
                       DownstreamBufferMinutes = 30, CascadeAmplification = 0.8 },
        new LineNode { Id = "PICKLE", Name = "Pickling line", ThroughputPerMinute = 4.0,
                       DownstreamBufferMinutes = 60, CascadeAmplification = 0.3 },
    });
}