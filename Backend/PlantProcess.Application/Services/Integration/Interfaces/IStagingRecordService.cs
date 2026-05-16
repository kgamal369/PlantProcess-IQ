using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Application.Contracts.Integration.Commands;
using PlantProcess.Application.Contracts.Integration.Dtos;

namespace PlantProcess.Application.Services.Integration.Interfaces;

public interface IStagingRecordService
{
    Task<ApplicationResult<StagingRecordBulkCreateResult>> CreateBulkAsync(
        BulkCreateStagingRecordsCommand command,
        CancellationToken cancellationToken);
}
