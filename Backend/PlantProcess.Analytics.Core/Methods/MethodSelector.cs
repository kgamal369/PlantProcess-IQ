namespace PlantProcess.Analytics.Core.Methods;

public enum VariableType { Numeric, Categorical, Binary }
public enum AnalysisMethod { Spearman, MutualInformation, CramersV, PointBiserial, LassoVif, NotApplicable }

public sealed record MethodChoice(AnalysisMethod Method, string Rationale, bool IsApplicable);

/// <summary>Deterministic method auto-selection by variable-pair shape (v4 7.3). Records WHY.</summary>
public static class MethodSelector
{
    public static MethodChoice Select(
        VariableType a,
        VariableType b,
        bool numericRelationshipNonlinear = false,
        bool manyCollinearPredictors = false)
    {
        if (manyCollinearPredictors)
            return new(AnalysisMethod.LassoVif, "Many or collinear predictors: Lasso screen with VIF collinearity check.", true);

        if (a == VariableType.Numeric && b == VariableType.Numeric)
            return numericRelationshipNonlinear
                ? new(AnalysisMethod.MutualInformation, "Numeric/numeric suspected nonlinear: mutual information captures non-monotonic dependence.", true)
                : new(AnalysisMethod.Spearman, "Numeric/numeric monotonic: Spearman rank correlation is robust to non-normality and monotone transforms.", true);

        bool aCat = a == VariableType.Categorical || a == VariableType.Binary;
        bool bCat = b == VariableType.Categorical || b == VariableType.Binary;

        bool binaryNumeric = (a == VariableType.Binary && b == VariableType.Numeric) || (a == VariableType.Numeric && b == VariableType.Binary);
        if (binaryNumeric)
            return new(AnalysisMethod.PointBiserial, "Binary/numeric: point-biserial correlation.", true);

        if (aCat && bCat)
            return new(AnalysisMethod.CramersV, "Categorical/categorical (incl. binary): Cramer's V from chi-square contingency.", true);

        return new(AnalysisMethod.NotApplicable, $"Unsupported variable-pair shape ({a},{b}); no valid method selected.", false);
    }
}