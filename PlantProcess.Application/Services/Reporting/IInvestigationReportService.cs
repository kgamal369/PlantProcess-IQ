using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Reporting;

namespace PlantProcess.Application.Services.Reporting;

public interface IInvestigationReportService
{
    Task<ApplicationResult<InvestigationReportDto>> BuildMaterialInvestigationReportAsync(
        Guid materialUnitId,
        string? requestedBy,
        CancellationToken cancellationToken);

    Task<ApplicationResult<InvestigationPdfReportResult>> BuildMaterialInvestigationPdfAsync(
        Guid materialUnitId,
        string? requestedBy,
        CancellationToken cancellationToken);
}
