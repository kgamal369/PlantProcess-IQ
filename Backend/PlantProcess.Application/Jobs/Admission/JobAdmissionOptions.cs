namespace PlantProcess.Application.Jobs.Admission;

/// <summary>
/// One lane as the runtime configuration declares it. Values shipped without a site
/// benchmark are UnqualifiedDefault and are never presented as certified capacity.
/// </summary>
public sealed class JobLaneOptions
{
    public string LaneCode { get; set; } = string.Empty;

    public int MaxConcurrency { get; set; } = 1;

    public double ResourceCapacity { get; set; } = 1;

    public int QueueCapacity { get; set; } = 8;

    public bool HardReserved { get; set; }

    /// <summary>
    /// Lane contract metadata only. It never claims that an executor can checkpoint, yield
    /// or resume a run; that contract belongs to the commissioned runtime for that family.
    /// </summary>
    public bool Preemptible { get; set; }

    /// <summary>
    /// The heaviest compute weight this deployment expects to submit to the lane. When it is
    /// declared and exceeds the lane capacity, the configuration is rejected at startup
    /// rather than leaving a workload that can never be admitted.
    /// </summary>
    public double? MaxDeclaredComputeWeight { get; set; }

    public JobCapacityProvenance Provenance { get; set; } = JobCapacityProvenance.UnqualifiedDefault;
}

/// <summary>Runtime admission configuration. One immutable snapshot is taken at startup.</summary>
public sealed class JobAdmissionOptions
{
    public const string SectionName = "Jobs:Admission";

    /// <summary>
    /// Identifies this admission instance. Capacity here governs one runtime instance, not a
    /// site and not a cluster. Replicas each carry their own declared share.
    /// </summary>
    public string AdmissionScope { get; set; } = "default";

    public int MaxOutstandingDispatches { get; set; } = 8;
    public int AdmissionWaitSeconds { get; set; } = 30;
    public List<JobLaneOptions> Lanes { get; set; } = new();
}

/// <summary>
/// The production configuration provider. It reads the runtime options once, validates them
/// and hands admission an immutable snapshot. It owns no table, no hot rebalancing and no
/// capacity claim beyond the provenance each lane declares.
/// </summary>
public sealed class JobAdmissionOptionsConfigurationProvider : IJobAdmissionConfigurationProvider
{
    private readonly IReadOnlyList<JobLaneDefinition> _lanes;

    public JobAdmissionOptionsConfigurationProvider(JobAdmissionOptions options)
        : this(options, JobLaneCatalogue.Shared)
    {
    }

    public JobAdmissionOptionsConfigurationProvider(JobAdmissionOptions options, JobLaneCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalogue);

        AdmissionScope = string.IsNullOrWhiteSpace(options.AdmissionScope) ? "default" : options.AdmissionScope;

        List<JobLaneOptions> declared = options.Lanes is { Count: > 0 }
            ? options.Lanes
            : ShippedDefaults();

        var errors = new List<string>();
        if (options.MaxOutstandingDispatches < 1 || options.MaxOutstandingDispatches > 4096)
            errors.Add("MaxOutstandingDispatches must be between 1 and 4096.");
        if (options.AdmissionWaitSeconds < 1 || options.AdmissionWaitSeconds > 3600)
            errors.Add("AdmissionWaitSeconds must be between 1 and 3600.");
        MaxOutstandingDispatches = options.MaxOutstandingDispatches;
        AdmissionWait = TimeSpan.FromSeconds(options.AdmissionWaitSeconds);
        var lanes = new List<JobLaneDefinition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (JobLaneOptions lane in declared)
        {
            var definition = new JobLaneDefinition(
                lane.LaneCode,
                lane.MaxConcurrency,
                lane.ResourceCapacity,
                lane.QueueCapacity,
                lane.Provenance,
                lane.HardReserved,
                lane.Preemptible);

            errors.AddRange(definition.Validate(catalogue));

            if (!seen.Add(lane.LaneCode))
            {
                errors.Add("Lane '" + lane.LaneCode + "' is declared more than once.");
            }

            if (lane.MaxDeclaredComputeWeight is { } heaviest && (!double.IsFinite(heaviest) || heaviest <= 0 || heaviest > lane.ResourceCapacity))
            {
                errors.Add(
                    "Lane '" + lane.LaneCode + "' declares a heaviest workload of " + heaviest +
                    " which can never fit its resource capacity of " + lane.ResourceCapacity + ".");
            }

            lanes.Add(definition);
        }

        // Every lane the catalogue currently requires must carry exactly one configuration.
        // This is deliberately not "exactly these seven": a lane a later task registers
        // through the same authority is validated here without reopening this mechanism.
        foreach (JobLaneRegistration required in catalogue.RequiredLanes())
        {
            if (!seen.Contains(required.LaneCode))
            {
                errors.Add(
                    "Registered lane '" + required.LaneCode + "' (owner " + required.Owner +
                    ") has no configuration.");
            }
        }

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "Job admission configuration is not usable: " + string.Join(" ", errors),
                nameof(options));
        }

        _lanes = lanes;
    }

    public string AdmissionScope { get; }
    public int MaxOutstandingDispatches { get; }
    public TimeSpan AdmissionWait { get; }

    public IReadOnlyList<JobLaneDefinition> GetLanes() => _lanes;

    /// <summary>
    /// Values shipped with the product. They are unqualified defaults: they keep the runtime
    /// bounded, and they are not a benchmark, a recommendation or a site limit. Site sizing
    /// belongs to the calibration tasks and to their measured evidence.
    /// </summary>
    public static List<JobLaneOptions> ShippedDefaults()
        => new()
        {
            Lane(JobLaneCodes.Import, 2, 4, 16),
            Lane(JobLaneCodes.Projection, 2, 4, 16),
            Lane(JobLaneCodes.Analysis, 2, 4, 16),
            Lane(JobLaneCodes.MlTraining, 1, 8, 8, preemptible: true),
            Lane(JobLaneCodes.MlBatchScoring, 1, 4, 8),
            Lane(JobLaneCodes.MlOnlineScoring, 2, 4, 8, hardReserved: true),
            Lane(JobLaneCodes.Report, 2, 4, 16)
        };

    private static JobLaneOptions Lane(
        string laneCode,
        int maxConcurrency,
        double resourceCapacity,
        int queueCapacity,
        bool hardReserved = false,
        bool preemptible = false)
        => new()
        {
            LaneCode = laneCode,
            MaxConcurrency = maxConcurrency,
            ResourceCapacity = resourceCapacity,
            QueueCapacity = queueCapacity,
            HardReserved = hardReserved,
            Preemptible = preemptible,
            Provenance = JobCapacityProvenance.UnqualifiedDefault
        };
}
