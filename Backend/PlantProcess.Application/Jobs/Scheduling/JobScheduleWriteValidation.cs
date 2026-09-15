// T-106 B2.3b. One validation entry point for every path that writes a job schedule.
//
// Create, update and system upsert all asked the same weak question before this: is the
// string non-empty? A non-empty string that nothing can parse is not a schedule, and a
// system registration that writes one produces a job that can never become due.
using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Jobs.Scheduling;

public static class JobScheduleWriteValidation
{
    /// <summary>
    /// Returns null when the expression is a schedule this product can execute, and a
    /// validation error naming the reason when it is not. Canonical expressions and the
    /// shipped legacy shapes are both accepted; nothing else is.
    /// </summary>
    public static ApplicationError? Validate(string? scheduleExpression)
    {
        if (string.IsNullOrWhiteSpace(scheduleExpression))
        {
            return ApplicationError.Validation("Schedule expression is required.");
        }

        // The time zone belongs to the job, not to this check: validation only asks
        // whether the expression is decidable at all, so it is read in UTC.
        ScheduleValidationResult result = LegacyScheduleText.ValidateAny(
            scheduleExpression, "UTC", MisfirePolicy.SkipToNext, 0);

        return result.IsValid
            ? null
            : ApplicationError.Validation("Schedule expression is not executable: " + result.Message);
    }
}
