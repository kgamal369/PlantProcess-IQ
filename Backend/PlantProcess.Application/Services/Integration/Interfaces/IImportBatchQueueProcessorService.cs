using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration.Dtos;

namespace PlantProcess.Application.Services.Integration.Interfaces;

public interface IImportBatchQueueProcessorService
{
    Task<ApplicationResult<ImportQueueProcessingSummary>> ProcessPendingBatchesAsync(
        int maxBatches,
        int rowsPerBatch,
        bool stopOnFirstError,
        bool runDataQualityScan,
        CancellationToken cancellationToken);
}
