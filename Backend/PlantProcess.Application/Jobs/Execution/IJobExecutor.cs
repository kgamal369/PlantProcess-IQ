using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Execution;

/// <summary>One family, one executor, one governed execution.</summary>
public interface IJobExecutor
{
    /// <summary>The family this executor executes. Dispatch keys on this and nothing else.</summary>
    JobDefinitionType Executes { get; }

    /// <summary>
    /// PRE-ADMISSION. Everything the executor can prove about the exact resolved version
    /// is proven here, before a run record exists: that the version can be read, that it
    /// declares what it writes, that it compiles, that its source identity is available
    /// and unambiguous, and that its canonical target is commissioned. A version that
    /// fails any of these is refused with a typed code and never becomes a misleading run.
    ///
    /// The returned plan is the frozen result of that proof. The run executes the plan;
    /// it never resolves the target again.
    /// </summary>
    Task<ApplicationResult<JobExecutionPlan>> AdmitAsync(
        ResolvedJobTarget target,
        CancellationToken cancellationToken);

    Task<ApplicationResult<JobExecutionOutcome>> ExecuteAsync(
        JobExecutionContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// The admitted plan. The base carries only the exact resolution taken before the run
/// existed; each executor derives the facts it proved at admission.
/// </summary>
public abstract record JobExecutionPlan(ResolvedJobTarget Target);

/// <summary>
/// What an execution actually did, measured rather than asserted.
///
/// BlockResults is the child grain: one entry per authored block of the admitted plan,
/// carrying its stable identity, its ordinal and its own outcome. Nothing here is
/// manufactured for a block that never executed: a block that was never reached is
/// reported as Cancelled or left out, never as Succeeded.
///
/// Cancelled is not a flavour of failure and not a flavour of success. It means the
/// executor observed an operator request at a governed boundary and stopped there.
/// </summary>
public sealed record JobExecutionOutcome(
    bool Succeeded,
    bool Cancelled,
    string Message,
    int CanonicalRowsWritten,
    IReadOnlyList<JobExecutionBlockResult> BlockResults,
    string? DiagnosticCode,
    string? DiagnosticDetail);

public sealed record JobExecutionBlockResult(
    string BlockId,
    int ExecutionOrdinal,
    JobRunBlockStatus Status,
    int? InputRows,
    int? OutputRows,
    string? DiagnosticCode,
    string? DiagnosticDetail);
