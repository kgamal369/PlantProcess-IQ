using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Jobs.Admission;

/// <summary>
/// Admission refusals expressed in the job error family the runtime already uses, so an
/// operator reads one vocabulary. Temporary saturation is not here, because queuing is not
/// an error: a candidate that waits inside its lane bound and is then admitted produced no
/// failure at all.
/// </summary>
public static class JobAdmissionErrors
{
    public static ApplicationError Refused(JobAdmissionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new ApplicationError(
            decision.DiagnosticCode ?? PlantProcess.Application.Jobs.Execution.JobExecutionDiagnosticCodes.AdmissionInvalidRequest,
            decision.Detail,
            ApplicationErrorType.BusinessRule);
    }
}
