using PlantProcess.Application.Analytics.Contracts;

namespace PlantProcess.Application.Analytics.Interfaces;

public interface IQualityLabelBuilderService
{
    Task<QualityTrainingLabelPreviewDto> BuildPreviewAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountLabeledMaterialsAsync(
        CancellationToken cancellationToken = default);
}