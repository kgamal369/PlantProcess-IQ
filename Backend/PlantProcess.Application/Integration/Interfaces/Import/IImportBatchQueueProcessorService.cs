using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Dtos;

namespace PlantProcess.Application.Integration.Interfaces.Import;

public interface IImportBatchQueueProcessorService
{
    Task<ApplicationResult<ImportQueueProcessingSummary>> ProcessPendingBatchesAsync(
        int maxBatches,
        int rowsPerBatch,
        bool stopOnFirstError,
        bool runDataQualityScan,
        CancellationToken cancellationToken);
}




