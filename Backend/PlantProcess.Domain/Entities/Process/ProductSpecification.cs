using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Process;

/// <summary>
/// T-044-R1. A LIMIT THAT DEPENDS ON WHAT IS BEING MADE.
///
/// ParameterDefinition carries expected bounds for a parameter in general.
/// Those cannot express a bound that differs by grade or recipe, which is what
/// a specification is: the same parameter is acceptable in one product and out
/// of specification in another.
///
/// Nothing here is chemistry. The source that first needed it happens to name
/// its parameters after elements, but the contract is a min, a target and a max
/// for one parameter under one product scope - equally a pH band, a moisture
/// limit or a curing temperature.
/// </summary>
public class ProductSpecification : BaseEntity
{
    public string SpecificationCode { get; private set; } = null!;

    public string? ProductFamily { get; private set; }

    public string GradeOrRecipe { get; private set; } = null!;

    public Guid ParameterDefinitionId { get; private set; }

    /// <summary>Nullable because a specification may be one-sided: a maximum
    /// with no floor is a complete and common requirement.</summary>
    public decimal? MinValue { get; private set; }

    public decimal? TargetValue { get; private set; }

    public decimal? MaxValue { get; private set; }

    public string? UnitOfMeasure { get; private set; }

    public DateTime EffectiveFromUtc { get; private set; }

    /// <summary>Null means still in force. An open interval is a fact, not a
    /// missing value.</summary>
    public DateTime? EffectiveToUtc { get; private set; }

    public string? Provenance { get; private set; }

    private ProductSpecification()
    {
    }

    public ProductSpecification(
        string specificationCode,
        string gradeOrRecipe,
        Guid parameterDefinitionId,
        DateTime effectiveFromUtc,
        bool isSynthetic,
        string? productFamily = null,
        decimal? minValue = null,
        decimal? targetValue = null,
        decimal? maxValue = null,
        string? unitOfMeasure = null,
        DateTime? effectiveToUtc = null,
        string? provenance = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(specificationCode))
            throw new ArgumentException("Specification code is required.", nameof(specificationCode));

        if (string.IsNullOrWhiteSpace(gradeOrRecipe))
            throw new ArgumentException("Grade or recipe is required.", nameof(gradeOrRecipe));

        if (parameterDefinitionId == Guid.Empty)
            throw new ArgumentException("Parameter definition is required.", nameof(parameterDefinitionId));

        // A specification with no bound at all states nothing. One bound is
        // enough; none is not a specification.
        if (minValue is null && targetValue is null && maxValue is null)
            throw new ArgumentException("A specification needs at least one of min, target or max.", nameof(targetValue));

        if (minValue.HasValue && maxValue.HasValue && minValue > maxValue)
            throw new ArgumentException("The minimum cannot exceed the maximum.", nameof(minValue));

        SpecificationCode = specificationCode.Trim();
        GradeOrRecipe = gradeOrRecipe.Trim();
        ParameterDefinitionId = parameterDefinitionId;
        ProductFamily = productFamily?.Trim();
        MinValue = minValue;
        TargetValue = targetValue;
        MaxValue = maxValue;
        UnitOfMeasure = unitOfMeasure?.Trim();
        EffectiveFromUtc = DateTime.SpecifyKind(effectiveFromUtc, DateTimeKind.Utc);
        EffectiveToUtc = effectiveToUtc.HasValue
            ? DateTime.SpecifyKind(effectiveToUtc.Value, DateTimeKind.Utc)
            : null;
        Provenance = provenance?.Trim();
        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }
}