namespace PlantProcess.Application.Jobs.Execution.Transformations;

/// <summary>
/// THE DETERMINISTIC ORDER, COMPUTED ONCE FROM THE GOVERNED BOARD.
///
/// Two executions of the same immutable version must execute the same blocks in the
/// same order. Dictionary enumeration does not promise that, so the order is a
/// topological sort with one stable tie-break: when several blocks are simultaneously
/// ready, the smallest block id goes first. The tie-break is ordinal string comparison
/// on the authored id, which is stable across machines, cultures and runs.
/// </summary>
public static class TransformationExecutionPlan
{
    /// <summary>
    /// Orders blocks by dependency, refusing a cycle rather than breaking one. A cycle
    /// is an authoring defect the executor cannot repair, and an order invented from a
    /// cycle would execute a block before the block it consumes.
    /// </summary>
    public static IReadOnlyList<string> Order(
        IReadOnlyList<string> blockIds,
        IReadOnlyDictionary<string, IReadOnlyList<string>> dependsOn,
        out string? refusal)
    {
        refusal = null;

        var remaining = new List<string>(blockIds);
        remaining.Sort(StringComparer.Ordinal);

        var done = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();

        while (remaining.Count > 0)
        {
            string? ready = null;

            foreach (string candidate in remaining)
            {
                bool satisfied = true;

                IReadOnlyList<string>? needs;
                if (dependsOn.TryGetValue(candidate, out needs) && needs is not null)
                {
                    foreach (string need in needs)
                    {
                        if (!done.Contains(need)) { satisfied = false; break; }
                    }
                }

                if (satisfied) { ready = candidate; break; }
            }

            if (ready is null)
            {
                refusal =
                    "The authored blocks form a dependency cycle, so no execution order exists. "
                    + "The definition is refused rather than executed in an invented order.";
                return Array.Empty<string>();
            }

            ordered.Add(ready);
            done.Add(ready);
            remaining.Remove(ready);
        }

        return ordered;
    }
}