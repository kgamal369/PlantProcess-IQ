
namespace PlantProcess.Application.Analytics.Value;

public interface IValueRealizationService
{
    ValueRealizationResult Calculate(ValueRealizationRequest request);
}
