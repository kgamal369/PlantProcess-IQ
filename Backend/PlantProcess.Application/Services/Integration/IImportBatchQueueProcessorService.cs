using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;

namespace PlantProcess.Application.Services.Integration;

public interface IImportBatchQueueProcessorService
{
    Task<ApplicationResult<ImportQueueProcessingSummary>> ProcessPendingBatchesAsync(
        int maxBatches,
        int rowsPerBatch,
        bool stopOnFirstError,
        bool runDataQualityScan,
        CancellationToken cancellationToken);
}
