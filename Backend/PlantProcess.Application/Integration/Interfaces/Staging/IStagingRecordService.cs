using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Commands;
using PlantProcess.Application.Integration.Contracts.Staging;

namespace PlantProcess.Application.Integration.Interfaces.Staging;

public interface IStagingRecordService
{
    Task<ApplicationResult<StagingRecordBulkCreateResult>> CreateBulkAsync(
        BulkCreateStagingRecordsCommand command,
        CancellationToken cancellationToken);
}



