using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Readiness;

namespace PlantProcess.Application.Services.Readiness;

public interface IApplicationReadinessService
{
    Task<ApplicationResult<ApplicationReadinessDto>> GetReadinessAsync(
        CancellationToken cancellationToken);

    Task<ApplicationResult<CommercialReadinessReportDto>> BuildCommercialReadinessReportAsync(
        CommercialReadinessReportRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ReadinessPdfReportResult>> BuildCommercialReadinessPdfAsync(
        CommercialReadinessReportRequest request,
        CancellationToken cancellationToken);
}

public sealed record ReadinessPdfReportResult(
    byte[] Content,
    string ContentType,
    string FileName);