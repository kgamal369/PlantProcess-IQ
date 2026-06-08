
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Application.Analytics.Advanced;

/// <summary>
/// PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES.
/// Explicit user-facing gate state for advanced analysis readiness.
/// </summary>
public static class AdvancedReadinessGateStates
{
    public const string Ready = "Ready";
    public const string Partial = "Partial";
    public const string Blocked = "Blocked";
}

public sealed record AdvancedReadinessGateDto(
    string GateCode,
    string Title,
    string State,
    string Reason,
    string Evidence,
    bool IsBlocking);

public sealed record AdvancedReadinessGateSummaryDto(
    string State,
    bool CanRun,
    string OutcomeKey,
    string Grain,
    int WindowDays,
    int IndependentHeats,
    int OutcomeEvents,
    int ReadyCount,
    int PartialCount,
    int BlockedCount,
    string Message,
    IReadOnlyList<AdvancedReadinessGateDto> Gates);

public static class AdvancedReadinessGateProjector
{
    public const string Marker = "PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES";

    public static AdvancedReadinessGateSummaryDto Project(AnalysisReadinessDto readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);

        var gates = readiness.Dimensions
            .Select(d =>
            {
                var state = NormalizeState(d.State);
                return new AdvancedReadinessGateDto(
                    GateCode: ToGateCode(d.Name),
                    Title: d.Name,
                    State: state,
                    Reason: string.IsNullOrWhiteSpace(d.Reason) ? "No reason supplied by readiness evaluator." : d.Reason,
                    Evidence: $"{d.Name}={state}; outcome={readiness.OutcomeKey}; grain={readiness.Grain}; window={readiness.WindowDays}d",
                    IsBlocking: state == AdvancedReadinessGateStates.Blocked);
            })
            .ToArray();

        var readyCount = gates.Count(g => g.State == AdvancedReadinessGateStates.Ready);
        var partialCount = gates.Count(g => g.State == AdvancedReadinessGateStates.Partial);
        var blockedCount = gates.Count(g => g.State == AdvancedReadinessGateStates.Blocked);

        var state = blockedCount > 0 || !readiness.CanRun
            ? AdvancedReadinessGateStates.Blocked
            : partialCount > 0
                ? AdvancedReadinessGateStates.Partial
                : AdvancedReadinessGateStates.Ready;

        var message = state switch
        {
            AdvancedReadinessGateStates.Ready =>
                "Ready: all required analysis gates are satisfied. Advanced analysis may run.",
            AdvancedReadinessGateStates.Partial =>
                "Partial: analysis may run, but at least one quality/readiness dimension needs attention.",
            _ =>
                "Blocked: analysis must abstain until blocking readiness issues are fixed."
        };

        return new AdvancedReadinessGateSummaryDto(
            state,
            readiness.CanRun && state != AdvancedReadinessGateStates.Blocked,
            readiness.OutcomeKey,
            readiness.Grain,
            readiness.WindowDays,
            readiness.IndependentHeats,
            readiness.OutcomeEvents,
            readyCount,
            partialCount,
            blockedCount,
            message,
            gates);
    }

    public static string NormalizeState(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return AdvancedReadinessGateStates.Blocked;

        var value = raw.Trim();

        if (value.Equals(AdvancedReadinessGateStates.Ready, StringComparison.OrdinalIgnoreCase))
            return AdvancedReadinessGateStates.Ready;

        if (value.Equals(AdvancedReadinessGateStates.Partial, StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Warning", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Warn", StringComparison.OrdinalIgnoreCase))
            return AdvancedReadinessGateStates.Partial;

        if (value.Equals(AdvancedReadinessGateStates.Blocked, StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Blocker", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Failed", StringComparison.OrdinalIgnoreCase))
            return AdvancedReadinessGateStates.Blocked;

        return value.Contains("partial", StringComparison.OrdinalIgnoreCase)
            ? AdvancedReadinessGateStates.Partial
            : value.Contains("ready", StringComparison.OrdinalIgnoreCase)
                ? AdvancedReadinessGateStates.Ready
                : AdvancedReadinessGateStates.Blocked;
    }

    private static string ToGateCode(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "UNKNOWN_GATE";

        var chars = name
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_')
            .ToArray();

        var raw = new string(chars);

        while (raw.Contains("__", StringComparison.Ordinal))
            raw = raw.Replace("__", "_", StringComparison.Ordinal);

        return raw.Trim('_');
    }
}
