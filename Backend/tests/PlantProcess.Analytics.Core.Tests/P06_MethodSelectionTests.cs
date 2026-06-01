using PlantProcess.Analytics.Core.Methods;
using Xunit;
namespace PlantProcess.Analytics.Core.Tests;
// T-030: each variable-pair shape -> expected method + recorded rationale; unsupported -> NotApplicable.
public sealed class P06_MethodSelectionTests
{
[Fact] public void Numeric_numeric_monotonic_uses_spearman()
{
var c = MethodSelector.Select(VariableType.Numeric, VariableType.Numeric, numericRelationshipNonlinear: false);
Assert.Equal(AnalysisMethod.Spearman, c.Method);
Assert.True(c.IsApplicable);
Assert.False(string.IsNullOrWhiteSpace(c.Rationale));
}
[Fact] public void Numeric_numeric_nonlinear_uses_mutual_information()
    => Assert.Equal(AnalysisMethod.MutualInformation, MethodSelector.Select(VariableType.Numeric, VariableType.Numeric, numericRelationshipNonlinear: true).Method);

[Fact] public void Categorical_categorical_uses_cramers_v()
    => Assert.Equal(AnalysisMethod.CramersV, MethodSelector.Select(VariableType.Categorical, VariableType.Categorical).Method);

[Fact] public void Binary_binary_uses_cramers_v()
    => Assert.Equal(AnalysisMethod.CramersV, MethodSelector.Select(VariableType.Binary, VariableType.Binary).Method);

[Fact] public void Binary_numeric_uses_point_biserial_either_order()
{
    Assert.Equal(AnalysisMethod.PointBiserial, MethodSelector.Select(VariableType.Binary, VariableType.Numeric).Method);
    Assert.Equal(AnalysisMethod.PointBiserial, MethodSelector.Select(VariableType.Numeric, VariableType.Binary).Method);
}

[Fact] public void Many_collinear_predictors_use_lasso_vif()
    => Assert.Equal(AnalysisMethod.LassoVif, MethodSelector.Select(VariableType.Numeric, VariableType.Numeric, manyCollinearPredictors: true).Method);

[Fact] public void Unsupported_shape_returns_not_applicable()
{
    var c = MethodSelector.Select(VariableType.Numeric, VariableType.Categorical);
    Assert.Equal(AnalysisMethod.NotApplicable, c.Method);
    Assert.False(c.IsApplicable);
}

[Fact] public void Selector_is_deterministic()
{
    var a = MethodSelector.Select(VariableType.Numeric, VariableType.Numeric);
    var b = MethodSelector.Select(VariableType.Numeric, VariableType.Numeric);
    Assert.Equal(a.Method, b.Method);
    Assert.Equal(a.Rationale, b.Rationale);
}
}