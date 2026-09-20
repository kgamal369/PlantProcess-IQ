using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs;

public sealed class JobExecutorResolverTests
{
    private sealed class StubExecutor : IJobExecutor
    {
        public StubExecutor(JobDefinitionType executes) { Executes = executes; }

        public JobDefinitionType Executes { get; }

        public Task<ApplicationResult<JobExecutionPlan>> AdmitAsync(
            ResolvedJobTarget target, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Dispatch test only.");
        }

        public Task<ApplicationResult<JobExecutionOutcome>> ExecuteAsync(
            JobExecutionContext context, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Dispatch test only.");
        }
    }

    [Fact]
    public void An_unregistered_family_resolves_to_nothing_rather_than_a_fallback()
    {
        var resolver = new JobExecutorResolver(new IJobExecutor[]
        {
            new StubExecutor(JobDefinitionType.CanonicalRefresh)
        });

        Assert.Null(resolver.Resolve(JobDefinitionType.DbLinkImport));
    }

    [Fact]
    public void A_registered_family_resolves_to_its_executor()
    {
        var expected = new StubExecutor(JobDefinitionType.CanonicalRefresh);
        var resolver = new JobExecutorResolver(new IJobExecutor[] { expected });

        Assert.Same(expected, resolver.Resolve(JobDefinitionType.CanonicalRefresh));
    }

    [Fact]
    public void Two_executors_for_one_family_is_a_composition_defect()
    {
        Assert.Throws<InvalidOperationException>(() => new JobExecutorResolver(new IJobExecutor[]
        {
            new StubExecutor(JobDefinitionType.CanonicalRefresh),
            new StubExecutor(JobDefinitionType.CanonicalRefresh)
        }));
    }

    [Fact]
    public void The_resolver_cannot_answer_whether_a_family_is_supported()
    {
        // The capability authority is the only place that answers support. If the
        // resolver ever grew that method this test would stop compiling, which is the
        // point of asserting it structurally rather than in a comment.
        var members = typeof(IJobExecutorResolver).GetMembers();

        foreach (var member in members)
        {
            Assert.DoesNotContain("Support", member.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Executable", member.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Every_runtime_diagnostic_is_a_distinct_job_execution_code()
    {
        var codes = typeof(JobExecutionDiagnosticCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        Assert.True(codes.Length >= 13);
        Assert.All(codes, c => Assert.StartsWith("JOB_EXEC_", c, StringComparison.Ordinal));
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(codes, c => c.StartsWith("PROJECTION_", StringComparison.Ordinal));
    }
}
