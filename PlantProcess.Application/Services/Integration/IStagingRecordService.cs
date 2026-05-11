using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;

namespace PlantProcess.Application.Services.Integration;

public interface IStagingRecordService
{
    Task<ApplicationResult<StagingRecordBulkCreateResult>> CreateBulkAsync(
        BulkCreateStagingRecordsCommand command,
        CancellationToken cancellationToken);
}
