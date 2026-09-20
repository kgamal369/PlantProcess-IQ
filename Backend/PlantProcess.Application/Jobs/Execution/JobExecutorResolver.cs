using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Execution;

/// <summary>
/// Resolves over the executors the container registered. No list of families is
/// written here: a second list would drift from the registrations it claims to
/// describe, and the registrations are the fact.
///
/// TWO EXECUTORS FOR ONE FAMILY IS A CONFIGURATION DEFECT, not a precedence question.
/// Picking one silently would make which executor ran depend on registration order.
/// </summary>
public sealed class JobExecutorResolver : IJobExecutorResolver
{
    private readonly Dictionary<JobDefinitionType, IJobExecutor> _byFamily;

    public JobExecutorResolver(IEnumerable<IJobExecutor> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);

        _byFamily = new Dictionary<JobDefinitionType, IJobExecutor>();

        foreach (IJobExecutor executor in executors)
        {
            if (_byFamily.ContainsKey(executor.Executes))
            {
                throw new InvalidOperationException(
                    "Two executors are registered for job family " + executor.Executes
                    + ". Exactly one implementation may execute a family, so this is a "
                    + "composition defect rather than a choice to be resolved at runtime.");
            }

            _byFamily.Add(executor.Executes, executor);
        }
    }

    public IJobExecutor? Resolve(JobDefinitionType jobType)
    {
        IJobExecutor? found;
        return _byFamily.TryGetValue(jobType, out found) ? found : null;
    }
}