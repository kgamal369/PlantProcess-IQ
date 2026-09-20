using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Domain.Tests.Integration;

public sealed class JobRunBlockEvidenceTests
{
    private static JobRunBlockEvidence New(string blockId = "node-a", int ordinal = 0)
    {
        return new JobRunBlockEvidence(Guid.NewGuid(), blockId, ordinal);
    }

    [Fact]
    public void Evidence_without_a_real_run_is_refused()
    {
        Assert.Throws<ArgumentException>(
            () => new JobRunBlockEvidence(Guid.Empty, "node-a", 0));
    }

    [Fact]
    public void Evidence_without_a_stable_block_id_is_refused()
    {
        Assert.Throws<ArgumentException>(
            () => new JobRunBlockEvidence(Guid.NewGuid(), "   ", 0));
    }

    [Fact]
    public void A_new_block_starts_pending_and_measures_nothing()
    {
        var evidence = New();

        Assert.Equal(JobRunBlockStatus.Pending, evidence.Status);
        Assert.Null(evidence.InputRows);
        Assert.Null(evidence.OutputRows);
        Assert.Null(evidence.StartedAtUtc);
    }

    [Fact]
    public void A_failed_block_must_carry_a_typed_diagnostic()
    {
        var evidence = New();
        evidence.MarkRunning();

        Assert.Throws<ArgumentException>(() => evidence.MarkFailed("  ", "detail"));
    }

    [Fact]
    public void A_cancelled_block_can_never_later_report_success()
    {
        var evidence = New();
        evidence.MarkRunning();
        evidence.MarkCancelled();

        Assert.Equal(JobRunBlockStatus.Cancelled, evidence.Status);
        Assert.Throws<InvalidOperationException>(() => evidence.MarkSucceeded(10, 10));
    }

    [Fact]
    public void A_terminal_block_is_never_rewritten()
    {
        var evidence = New();
        evidence.MarkRunning();
        evidence.MarkSucceeded(5, 5);

        Assert.Throws<InvalidOperationException>(
            () => evidence.MarkFailed("JOB_EXEC_BLOCK_FAILED", "late"));
    }
}