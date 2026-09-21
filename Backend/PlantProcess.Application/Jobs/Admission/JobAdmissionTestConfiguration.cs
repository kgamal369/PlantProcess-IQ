namespace PlantProcess.Application.Jobs.Admission;

/// <summary>
/// Lane capacities for acceptance runs. Every value here is TEST_CONFIGURATION: it certifies
/// admission behaviour and says nothing about how a site should be sized. Site capacity
/// belongs to the calibration tasks and to their benchmark evidence.
/// </summary>
public sealed class JobAdmissionTestConfigurationProvider : IJobAdmissionConfigurationProvider
{
    public const string Provenance = "TEST_CONFIGURATION";

    private readonly IReadOnlyList<JobLaneDefinition> _lanes;

    public JobAdmissionTestConfigurationProvider(params JobLaneDefinition[] lanes)
    {
        _lanes = lanes is { Length: > 0 } ? lanes : Default();
    }

    public IReadOnlyList<JobLaneDefinition> GetLanes() => _lanes;

    public static IReadOnlyList<JobLaneDefinition> Default()
        => new[]
        {
            new JobLaneDefinition(JobLaneCodes.Import, 2, 4, 8, JobCapacityProvenance.TestConfiguration),
            new JobLaneDefinition(JobLaneCodes.Projection, 2, 4, 8, JobCapacityProvenance.TestConfiguration),
            new JobLaneDefinition(JobLaneCodes.Analysis, 2, 4, 8, JobCapacityProvenance.TestConfiguration),
            new JobLaneDefinition(
                JobLaneCodes.MlTraining,
                1,
                8,
                4,
                JobCapacityProvenance.TestConfiguration,
                IsPreemptible: true),
            new JobLaneDefinition(JobLaneCodes.MlBatchScoring, 2, 6, 4, JobCapacityProvenance.TestConfiguration),
            new JobLaneDefinition(
                JobLaneCodes.MlOnlineScoring,
                2,
                4,
                4,
                JobCapacityProvenance.TestConfiguration,
                IsHardReserved: true),
            new JobLaneDefinition(JobLaneCodes.Report, 2, 4, 8, JobCapacityProvenance.TestConfiguration)
        };
}
