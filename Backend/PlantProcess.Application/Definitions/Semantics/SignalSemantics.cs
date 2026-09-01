using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Definitions.Semantics;

/// <summary>
/// PPIQ T-210. THE SIGNAL AND AGGREGATION SEMANTICS CONTRACT.
///
/// This is the public surface T-211 (execution), T-212 (Layer-A integration),
/// T-242 (Canvas aggregate blocks) and T-092 (registry projection) consume.
/// It is deliberately small: the grammar, the resolution result and one
/// resolve call. The authority behind it is ppiq_meta.parameter_definitions
/// and ppiq_meta.kpi_parameter_bindings, resolved by
/// ppiq_meta.resolve_aggregation_semantics in exactly one order:
///
///     published KPI binding override
///     -> published parameter aggregation_kind
///     -> AG01
///
/// then validated against the signal kind -> AG02.
///
/// THE NO-DEFAULT RULE. There is no product default of Average, or of anything
/// else, for a numeric column. A consumer that receives AG01 must refuse the
/// operation and say why; it must not pick a method the customer never
/// declared and present the result as a fact.
/// </summary>
public interface ISignalSemanticsResolver
{
    /// <summary>
    /// Resolves the aggregation to use for a parameter in a tenant, optionally
    /// under a KPI binding and optionally for an explicitly requested method.
    /// Never throws for a semantic refusal: AG01/AG02 are returned as a
    /// failure whose error message begins with the refusal code.
    /// </summary>
    Task<ApplicationResult<ResolvedAggregation>> ResolveAsync(
        Guid tenantId,
        Guid parameterId,
        Guid? kpiBindingId,
        AggregationKind? requested,
        CancellationToken cancellationToken);

    /// <summary>
    /// Declares (or redeclares) a parameter's signal semantics. Identical
    /// redeclaration is idempotent: the semantics version does not advance.
    /// A declared default aggregation that is not defensible for the signal
    /// kind is refused with AG02 before anything is written.
    /// </summary>
    Task<ApplicationResult<SignalSemantics>> DeclareAsync(
        Guid tenantId,
        Guid parameterId,
        SignalSemanticsDeclaration declaration,
        CancellationToken cancellationToken);

    /// <summary>The current declaration for a parameter, or a failure if it has none.</summary>
    Task<ApplicationResult<SignalSemantics>> GetAsync(
        Guid tenantId,
        Guid parameterId,
        CancellationToken cancellationToken);
}

/// <summary>Product grammar. Customer definitions select from it; nothing here is an industry noun.</summary>
public enum SignalKind
{
    Unknown = 0,
    Analog = 1,
    State = 2,
    Counter = 3,
    Event = 4,
    LabSample = 5,
    Composition = 6,
    Level = 7,
    Rate = 8,
    Derived = 9,
}

/// <summary>
/// Product grammar. Percentile and WeightedMean are declared in M2 but not
/// executed; an executor that meets one must refuse honestly, not approximate.
/// </summary>
public enum AggregationKind
{
    SampleMean = 1,
    TimeWeightedMean = 2,
    Integral = 3,
    Delta = 4,
    StateDuration = 5,
    Count = 6,
    Min = 7,
    Max = 8,
    Last = 9,
    Percentile = 10,
    WeightedMean = 11,
}

/// <summary>
/// How observations arrive. Owned here because an executor cannot decide
/// whether SampleMean is lawful without it: a plain mean of fixed-cadence
/// samples is a time mean; a plain mean of irregular or on-change samples is
/// not, and the defensible method is time-weighted.
/// </summary>
public enum SamplingBasis
{
    FixedCadence = 1,
    Irregular = 2,
    OnChange = 3,
    Batch = 4,
}

public enum InterpolationKind { None = 0, HoldLast = 1, Linear = 2, Step = 3 }

public enum WeightBasis { Time = 1, Sample = 2, Quantity = 3, Declared = 4 }

public enum CounterResetPolicy { None = 0, ResetToZero = 1, Rollover = 2, RefuseOnReset = 3 }

public enum QualityPolicy { GoodOnly = 1, GoodAndUncertain = 2, All = 3, RefuseOnBad = 4 }

public enum TimeBasis { ObservationTime = 1, ArrivalTime = 2, ProcessTime = 3 }

/// <summary>Refusal codes. The message of a failed ApplicationResult begins with one of these.</summary>
public static class AggregationRefusal
{
    public const string SemanticsUndeclared = "AG01";
    public const string InvalidForSignal = "AG02";
}

public sealed record SignalSemanticsDeclaration(
    SignalKind SignalKind,
    SamplingBasis? SamplingBasis,
    AggregationKind? DefaultAggregation,
    InterpolationKind? Interpolation,
    WeightBasis? WeightBasis,
    int? MaximumGapSeconds,
    CounterResetPolicy? CounterResetPolicy,
    QualityPolicy? QualityPolicy,
    TimeBasis? TimeBasis);

public sealed record SignalSemantics(
    Guid ParameterId,
    Guid TenantId,
    SignalKind SignalKind,
    SamplingBasis? SamplingBasis,
    AggregationKind? DefaultAggregation,
    InterpolationKind? Interpolation,
    WeightBasis? WeightBasis,
    int? MaximumGapSeconds,
    CounterResetPolicy? CounterResetPolicy,
    QualityPolicy? QualityPolicy,
    TimeBasis? TimeBasis,
    int SemanticsVersion,
    DateTime? DeclaredAtUtc);

/// <summary>
/// The answer a consumer executes with. Source names where the method came
/// from so a widget can show "KPI override" versus "parameter default".
/// </summary>
public sealed record ResolvedAggregation(
    AggregationKind Kind,
    AggregationResolutionSource Source,
    SignalKind SignalKind,
    SamplingBasis? SamplingBasis,
    WeightBasis? WeightBasis,
    int SemanticsVersion);

public enum AggregationResolutionSource
{
    Parameter = 1,
    KpiBinding = 2,
    Requested = 3,
}
