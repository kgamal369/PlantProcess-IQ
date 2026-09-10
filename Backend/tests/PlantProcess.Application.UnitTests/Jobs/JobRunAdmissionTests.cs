using System;
using System.Linq;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs;

/// <summary>
/// T-106. Executor capability truth, and the admission that consumes it before
/// a run record is ever opened.
/// </summary>
public sealed class JobRunAdmissionTests
{
    private static readonly JobExecutionCapabilityAuthority Authority = new();

    private static JobDefinition Job(JobDefinitionType jobType, bool isEnabled = true)
    {
        return new JobDefinition(
            jobCode: "PROBE_" + jobType,
            jobName: "Probe " + jobType,
            jobType: jobType,
            scheduleExpression: "Manual",
            isSynthetic: false,
            isEnabled: isEnabled);
    }

    [Fact]
    public void Every_job_family_is_classified_so_a_new_one_cannot_arrive_as_accidentally_runnable()
    {
        foreach (JobDefinitionType jobType in Enum.GetValues<JobDefinitionType>())
        {
            JobExecutionCapability answer = Authority.Describe(jobType);

            Assert.Equal(jobType, answer.JobType);
            Assert.False(string.IsNullOrWhiteSpace(answer.Statement));
        }
    }

    [Fact]
    public void The_executable_set_is_exactly_what_the_runtime_commissions()
    {
        Assert.Equal(
            new[]
            {
                JobDefinitionType.DbLinkImport,
                JobDefinitionType.DataQualityScan,
                JobDefinitionType.RiskScoring
            },
            Authority.ExecutableFamilies.ToArray());

        foreach (JobDefinitionType jobType in Enum.GetValues<JobDefinitionType>())
        {
            bool expected = Authority.ExecutableFamilies.Contains(jobType);
            Assert.Equal(expected, Authority.Describe(jobType).IsExecutableByRuntime);
        }
    }

    [Theory]
    [InlineData(JobDefinitionType.DbLinkImport)]
    [InlineData(JobDefinitionType.DataQualityScan)]
    [InlineData(JobDefinitionType.RiskScoring)]
    public void A_supported_family_is_admitted(JobDefinitionType jobType)
    {
        var result = JobRunAdmission.Admit(Job(jobType), Authority);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(JobDefinitionType.CanonicalRefresh)]
    [InlineData(JobDefinitionType.MlParamsVsDefects)]
    [InlineData(JobDefinitionType.MlParamsVsDowntime)]
    [InlineData(JobDefinitionType.MlParamsVsKpis)]
    [InlineData(JobDefinitionType.MlWeeklyFull)]
    [InlineData(JobDefinitionType.Custom)]
    public void An_unsupported_family_is_refused_with_JX01_before_anything_runs(JobDefinitionType jobType)
    {
        var result = JobRunAdmission.Admit(Job(jobType), Authority);

        Assert.True(result.IsFailure);
        Assert.Equal(JobExecutionErrorCodes.NoExecutorForJobFamily, result.Error!.Code);
        Assert.Contains(jobType.ToString(), result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_paused_job_is_refused_even_when_its_family_is_supported()
    {
        var result = JobRunAdmission.Admit(Job(JobDefinitionType.DataQualityScan, isEnabled: false), Authority);

        Assert.True(result.IsFailure);
    }
}