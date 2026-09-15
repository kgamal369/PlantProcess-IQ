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
    private readonly IJobRunOrchestratorService _orchestrator;

    public GovernedScheduleDispatcher(IPlantProcessDbContext dbContext, IJobRunOrchestratorService orchestrator)
    {
        _dbContext = dbContext;
        _orchestrator = orchestrator;
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

        var notes = new List<string>();
        int dispatched = 0;
        int alreadyClaimed = 0;
        int refused = 0;

        foreach (DueOccurrence occurrence in due)
        {
            if (cancellationToken.IsCancellationRequested) { break; }

            var result = await _orchestrator.RunScheduledAsync(
                occurrence.JobDefinitionId,
                occurrence.OccurrenceKey,
                occurrence.NominalAtUtc,
                triggeredBy: "GovernedScheduleDispatcher",
                correlationId: occurrence.OccurrenceKey,
                cancellationToken);

            if (result.IsSuccess)
            {
                dispatched++;
                continue;
            }

            string message = result.Error?.Message ?? string.Empty;

            if (message.Contains("OCCURRENCE ALREADY CLAIMED", StringComparison.OrdinalIgnoreCase))
            {
                // Someone else won the race. That is the system working, not a failure.
                alreadyClaimed++;
                continue;
            }

            refused++;
            notes.Add(occurrence.JobCode + ": " + message);
        }

        return new DispatchReport(jobs.Count, due.Count, dispatched, alreadyClaimed, refused, notes);
    }
}
