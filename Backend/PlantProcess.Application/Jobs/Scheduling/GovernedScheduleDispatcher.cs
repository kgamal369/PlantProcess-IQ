using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Application.Jobs.Admission;
// T-106 B2.3c. The governed schedule dispatcher.
//
// It owns one question: given this poll instant, which enabled jobs have a nominal
// occurrence that is now due, and what is that occurrence's identity? It owns no schedule
// semantics of its own - the grammar, the translator and the occurrence calculator are the
// accepted authorities - and it creates no run row: JobRuntimeService remains the single
// run-creation authority, reached through the accepted orchestration and admission path.
//
// The poll instant never becomes the occurrence identity. A job polled at 10:04:57 whose
// nominal occurrence is 10:00:00 claims 10:00:00.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Jobs.Scheduling;

public sealed record DueOccurrence(
    Guid JobDefinitionId,
    string JobCode,
    DateTime NominalAtUtc,
    string OccurrenceKey);

public sealed record DispatchReport(
    int JobsConsidered,
    int OccurrencesDue,
    int Dispatched,
    int AlreadyClaimed,
    int Refused,
    IReadOnlyList<string> Notes);
public sealed class GovernedScheduleDispatcher
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IServiceScopeFactory _scopes;
    private readonly BoundedJobDispatcher _dispatch;

    public GovernedScheduleDispatcher(IPlantProcessDbContext dbContext,
        IServiceScopeFactory scopes, BoundedJobDispatcher dispatch)
    {
        _dbContext = dbContext;
        _scopes = scopes;
        _dispatch = dispatch;
    }

    /// <summary>
    /// Pure: what is due at this instant, given these jobs. Separated from dispatch so the
    /// whole due/not-due decision can be falsified without a database or a runtime.
    /// </summary>
    public static IReadOnlyList<DueOccurrence> DueAt(
        IEnumerable<JobDefinition> jobs,
        DateTime pollAtUtc,
        Func<JobDefinition, DateTime?> lastNominalOrStart)
    {
        var due = new List<DueOccurrence>();

        foreach (JobDefinition job in jobs)
        {
            if (!job.IsEnabled || job.IsDeleted) { continue; }

            ScheduleValidationResult schedule = LegacyScheduleText.ValidateAny(
                job.ScheduleExpression, "UTC", MisfirePolicy.SkipToNext, 0);

            if (!schedule.IsValid) { continue; }
            if (schedule.Kind == ScheduleKind.Manual || schedule.Kind == ScheduleKind.Event) { continue; }

            DateTime after = lastNominalOrStart(job) ?? pollAtUtc.AddMinutes(-1);
            DateTime? nominal = ScheduleOccurrenceCalculator.NextOccurrenceUtc(schedule, after, after);

            if (nominal is null || nominal.Value > pollAtUtc) { continue; }

            // Catch up to the newest occurrence that is already due rather than replaying a
            // backlog: SkipToNext is the default misfire policy of a poll loop.
            DateTime claimed = nominal.Value;
            while (true)
            {
                DateTime? following = ScheduleOccurrenceCalculator.NextOccurrenceUtc(schedule, claimed, after);
                if (following is null || following.Value > pollAtUtc) { break; }
                claimed = following.Value;
            }

            due.Add(new DueOccurrence(
                job.Id,
                job.JobCode,
                claimed,
                ScheduleOccurrenceCalculator.BuildOccurrenceKey(job.Id, claimed)));
        }

        return due;
    }

    public async Task<DispatchReport> DispatchDueAsync(DateTime pollAtUtc, CancellationToken cancellationToken)
    {
        List<JobDefinition> jobs = await _dbContext.JobDefinitions
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .ToListAsync(cancellationToken);

        IReadOnlyList<DueOccurrence> due = DueAt(jobs, pollAtUtc, job => job.LastRunStartedAtUtc);

        var notes = new System.Collections.Concurrent.ConcurrentQueue<string>();
        int dispatched = 0, claimed = 0, refused = 0;
        // At most the shared bound of incomplete tasks is retained. A rejected occurrence
        // is unstarted, not queued for an invented retry; DueAt retains SkipToNext authority.
        var pending = new List<Task>(_dispatch.MaxOutstanding);
        foreach (DueOccurrence occurrence in due)
        {
            pending.RemoveAll(task => task.IsCompleted);
            if (!_dispatch.TryDispatch(occurrence.OccurrenceKey, async token =>
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    var orchestrator = scope.ServiceProvider.GetRequiredService<IJobRunOrchestratorService>();
                    var result = await orchestrator.RunScheduledAsync(
                        occurrence.JobDefinitionId, occurrence.OccurrenceKey, occurrence.NominalAtUtc,
                        "GovernedScheduleDispatcher", occurrence.OccurrenceKey, token);
                    if (result.IsSuccess) Interlocked.Increment(ref dispatched);
                    else if ((result.Error?.Message ?? "").Contains("OCCURRENCE ALREADY CLAIMED", StringComparison.OrdinalIgnoreCase))
                        Interlocked.Increment(ref claimed);
                    else
                    {
                        Interlocked.Increment(ref refused);
                        notes.Enqueue(occurrence.JobCode + ": " + result.Error?.Message);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    Interlocked.Increment(ref refused);
                    notes.Enqueue(occurrence.JobCode + ": dispatch cancelled; no success claimed.");
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref refused);
                    notes.Enqueue(occurrence.JobCode + ": " + ex.Message);
                    throw;
                }
            }, cancellationToken, out Task completion))
            {
                Interlocked.Increment(ref refused);
                notes.Enqueue(occurrence.JobCode + ": unstarted (cancelled, already in flight, or shared dispatch bound reached). No retry identity reserved.");
            }
            else pending.Add(completion);
        }
        // Bounded set only. Cooperative cancellation does not abandon a task or its scope.
        foreach (Task completion in pending) await completion.ConfigureAwait(false);
        return new DispatchReport(jobs.Count, due.Count, dispatched, claimed, refused, notes.ToArray());
    }
}
