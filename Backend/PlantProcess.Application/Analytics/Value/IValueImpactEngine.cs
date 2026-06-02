namespace PlantProcess.Application.Analytics.Value;

public interface IValueImpactEngine
{
    ValueImpactResult Compute(ValueImpactInputs inputs, CostAssumptionSet assumptions);
}