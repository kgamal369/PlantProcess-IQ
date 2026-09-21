namespace PlantProcess.Application.Jobs.Admission;

/// <summary>
/// The single catalogue of admission lanes.
///
/// The baseline lanes are the ones the current design declares. The catalogue is additive:
/// a later owning task may register one more governed lane through this same authority,
/// which is how industrial acquisition obtains a reserved admission class without a private
/// pool, a private governor or a second scheduler. A registration is lawful only when it
/// comes with an owner; nothing registers a lane implicitly, and no lane is ever removed.
/// </summary>
public sealed class JobLaneCatalogue
{
    private readonly Dictionary<string, JobLaneRegistration> _lanes = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public JobLaneCatalogue()
    {
        foreach (string laneCode in JobLaneCodes.Baseline)
        {
            _lanes[laneCode] = new JobLaneRegistration(laneCode, "baseline", Required: true);
        }
    }

    /// <summary>The process-wide catalogue. Baseline lanes are present from construction.</summary>
    public static JobLaneCatalogue Shared { get; } = new();

    /// <summary>Every lane that must carry exactly one configuration.</summary>
    public IReadOnlyList<JobLaneRegistration> RequiredLanes()
    {
        lock (_gate)
        {
            return _lanes.Values
                .Where(lane => lane.Required)
                .OrderBy(lane => lane.LaneCode, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public bool IsRegistered(string laneCode)
    {
        lock (_gate)
        {
            return _lanes.ContainsKey(laneCode);
        }
    }

    /// <summary>
    /// Registers one additional governed lane. The owner is the task that owns the lane's
    /// semantics. Re-registering the same lane with the same owner is idempotent; changing
    /// an existing lane's owner is refused, so a lane can never be quietly taken over.
    /// </summary>
    public JobLaneRegistration Register(string laneCode, string owner, bool required = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(laneCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        lock (_gate)
        {
            if (_lanes.TryGetValue(laneCode, out JobLaneRegistration? existing))
            {
                if (!string.Equals(existing.Owner, owner, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Lane '" + laneCode + "' is already registered by '" + existing.Owner + "'.");
                }

                return existing;
            }

            var registration = new JobLaneRegistration(laneCode, owner, required);
            _lanes[laneCode] = registration;
            return registration;
        }
    }
}

/// <summary>One registered lane and the task that owns its semantics.</summary>
public sealed record JobLaneRegistration(string LaneCode, string Owner, bool Required);
