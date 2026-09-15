// T-106 Phase A: cooperative cancellation semantics.
// Pure state machine. Terminal convergence onto a Cancelled run status and the API surface
// are later slices; this kernel states when each transition is legal and why.
using System;

namespace PlantProcess.Application.Jobs.Execution;

/// <summary>Where a run sits with respect to cancellation. Requesting is not the same as stopping.</summary>
public enum CancellationState
{
    None = 0,
    Requested = 1,
    Acknowledged = 2,
    TerminalCancelled = 3
}

public enum CancellationRefusalReason
{
    None = 0,
    RunNotRunning,
    RunAlreadyTerminal,
    AlreadyRequested,
    NoRequestToAcknowledge,
    NotAcknowledged
}

public sealed record CancellationTransition(
    bool Accepted, CancellationState State, CancellationRefusalReason Reason, string Message);

public static class JobCancellationContract
{
    /// <summary>An operator may request cancellation only for a run that is actually running.</summary>
    public static CancellationTransition Request(CancellationState current, bool runIsRunning, bool runIsTerminal)
    {
        if (runIsTerminal)
        {
            return Refuse(current, CancellationRefusalReason.RunAlreadyTerminal,
                "The run already reached a terminal state; there is nothing to cancel.");
        }

        if (!runIsRunning)
        {
            return Refuse(current, CancellationRefusalReason.RunNotRunning,
                "Only a running run can be cancelled.");
        }

        if (current == CancellationState.Requested || current == CancellationState.Acknowledged)
        {
            return Refuse(current, CancellationRefusalReason.AlreadyRequested,
                "Cancellation was already requested; the run keeps its current cancellation state.");
        }

        return new CancellationTransition(true, CancellationState.Requested, CancellationRefusalReason.None,
            "Cancellation requested. The run remains Running until the executor acknowledges it.");
    }

    /// <summary>The executor acknowledges; only then is stopping a fact rather than a wish.</summary>
    public static CancellationTransition Acknowledge(CancellationState current)
    {
        if (current == CancellationState.Acknowledged || current == CancellationState.TerminalCancelled)
        {
            return new CancellationTransition(true, current, CancellationRefusalReason.None,
                "Cancellation was already acknowledged; acknowledgement is idempotent.");
        }

        if (current != CancellationState.Requested)
        {
            return Refuse(current, CancellationRefusalReason.NoRequestToAcknowledge,
                "There is no cancellation request to acknowledge.");
        }

        return new CancellationTransition(true, CancellationState.Acknowledged, CancellationRefusalReason.None,
            "Executor acknowledged cancellation and stopped cooperatively.");
    }

    /// <summary>A run becomes terminally Cancelled only after an acknowledged request.</summary>
    public static CancellationTransition Complete(CancellationState current)
    {
        if (current == CancellationState.TerminalCancelled)
        {
            return new CancellationTransition(true, current, CancellationRefusalReason.None,
                "The run is already terminally cancelled.");
        }

        if (current != CancellationState.Acknowledged)
        {
            return Refuse(current, CancellationRefusalReason.NotAcknowledged,
                "A run cannot be reported as Cancelled before the executor acknowledges the request.");
        }

        return new CancellationTransition(true, CancellationState.TerminalCancelled, CancellationRefusalReason.None,
            "Run terminal state is Cancelled.");
    }

    private static CancellationTransition Refuse(CancellationState current, CancellationRefusalReason reason, string message)
        => new(false, current, reason, message);
}
