using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Jobs.Dependencies;

/// <summary>
/// T-106. THE DEPENDENCY ALGEBRA, WITH NO PERSISTENCE IN IT.
///
/// Kept pure on purpose. A cycle refusal that can only be exercised against a
/// database is a refusal nobody falsifies, and the negative controls this task
/// owes - self edge, duplicate edge, two-job cycle, multi-job cycle - are all
/// statements about a set of edges, not about storage.
///
/// The order is DETERMINISTIC, not merely valid. Kahn's algorithm admits many
/// correct orders; a job chain that reorders itself between runs cannot be
/// used as evidence of anything, so the ready set is drained in a fixed
/// comparison order.
/// </summary>
public static class JobDependencyGraph
{
    /// <summary>
    /// Answers whether one more edge may be written, given the edges already
    /// stored. Every refusal carries a stable JD code.
    /// </summary>
    public static ApplicationResult ValidateNewEdge(
        JobDependencyEdge candidate,
        IReadOnlyCollection<JobDependencyEdge> existing)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(existing);

        if (candidate.JobDefinitionId == candidate.DependsOnJobDefinitionId)
        {
            return ApplicationResult.Failure(
                JobDependencyErrors.SelfDependency(candidate.JobDefinitionId));
        }

        foreach (JobDependencyEdge edge in existing)
        {
            if (edge.JobDefinitionId == candidate.JobDefinitionId
                && edge.DependsOnJobDefinitionId == candidate.DependsOnJobDefinitionId)
            {
                return ApplicationResult.Failure(
                    JobDependencyErrors.DuplicateDependencyEdge(
                        candidate.JobDefinitionId, candidate.DependsOnJobDefinitionId));
            }
        }

        // Walk forward from the proposed predecessor. Reaching the proposed
        // dependent means the new edge would close the loop.
        if (Reaches(candidate.DependsOnJobDefinitionId, candidate.JobDefinitionId, existing))
        {
            return ApplicationResult.Failure(
                JobDependencyErrors.DependencyCycle(
                    candidate.JobDefinitionId, candidate.DependsOnJobDefinitionId));
        }

        return ApplicationResult.Success();
    }

    /// <summary>
    /// Every job the given job transitively depends on, plus the job itself.
    /// </summary>
    public static IReadOnlyList<Guid> DependencyClosure(
        Guid root,
        IReadOnlyCollection<JobDependencyEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);

        var seen = new HashSet<Guid> { root };
        var pending = new Queue<Guid>();
        pending.Enqueue(root);

        while (pending.Count > 0)
        {
            Guid current = pending.Dequeue();

            foreach (JobDependencyEdge edge in edges)
            {
                if (edge.JobDefinitionId != current)
                {
                    continue;
                }

                if (seen.Add(edge.DependsOnJobDefinitionId))
                {
                    pending.Enqueue(edge.DependsOnJobDefinitionId);
                }
            }
        }

        var ordered = new List<Guid>(seen);
        ordered.Sort();
        return ordered;
    }

    /// <summary>
    /// Predecessors first, deterministically. A cycle anywhere in the given
    /// set is refused with JD02 rather than answered with a partial order.
    /// </summary>
    public static ApplicationResult<IReadOnlyList<Guid>> TopologicalOrder(
        IReadOnlyCollection<Guid> nodes,
        IReadOnlyCollection<JobDependencyEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var all = new SortedSet<Guid>(nodes);

        var relevant = new List<JobDependencyEdge>();
        foreach (JobDependencyEdge edge in edges)
        {
            if (all.Contains(edge.JobDefinitionId) && all.Contains(edge.DependsOnJobDefinitionId))
            {
                relevant.Add(edge);
            }
        }

        var remainingPredecessors = new Dictionary<Guid, int>();
        foreach (Guid node in all)
        {
            remainingPredecessors[node] = 0;
        }

        foreach (JobDependencyEdge edge in relevant)
        {
            remainingPredecessors[edge.JobDefinitionId] = remainingPredecessors[edge.JobDefinitionId] + 1;
        }

        var ready = new SortedSet<Guid>();
        foreach (KeyValuePair<Guid, int> pair in remainingPredecessors)
        {
            if (pair.Value == 0)
            {
                ready.Add(pair.Key);
            }
        }

        var order = new List<Guid>();

        while (ready.Count > 0)
        {
            Guid next = ready.Min;
            ready.Remove(next);
            order.Add(next);

            foreach (JobDependencyEdge edge in relevant)
            {
                if (edge.DependsOnJobDefinitionId != next)
                {
                    continue;
                }

                remainingPredecessors[edge.JobDefinitionId] = remainingPredecessors[edge.JobDefinitionId] - 1;

                if (remainingPredecessors[edge.JobDefinitionId] == 0)
                {
                    ready.Add(edge.JobDefinitionId);
                }
            }
        }

        if (order.Count != all.Count)
        {
            Guid stuck = Guid.Empty;
            Guid blocker = Guid.Empty;

            foreach (KeyValuePair<Guid, int> pair in remainingPredecessors)
            {
                if (pair.Value > 0)
                {
                    stuck = pair.Key;
                    break;
                }
            }

            foreach (JobDependencyEdge edge in relevant)
            {
                if (edge.JobDefinitionId == stuck)
                {
                    blocker = edge.DependsOnJobDefinitionId;
                    break;
                }
            }

            return ApplicationResult<IReadOnlyList<Guid>>.Failure(
                JobDependencyErrors.DependencyCycle(stuck, blocker));
        }

        return ApplicationResult<IReadOnlyList<Guid>>.Success(order);
    }

    private static bool Reaches(Guid from, Guid target, IReadOnlyCollection<JobDependencyEdge> edges)
    {
        if (from == target)
        {
            return true;
        }

        var seen = new HashSet<Guid> { from };
        var pending = new Queue<Guid>();
        pending.Enqueue(from);

        while (pending.Count > 0)
        {
            Guid current = pending.Dequeue();

            foreach (JobDependencyEdge edge in edges)
            {
                if (edge.JobDefinitionId != current)
                {
                    continue;
                }

                if (edge.DependsOnJobDefinitionId == target)
                {
                    return true;
                }

                if (seen.Add(edge.DependsOnJobDefinitionId))
                {
                    pending.Enqueue(edge.DependsOnJobDefinitionId);
                }
            }
        }

        return false;
    }
}