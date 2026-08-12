using System.Threading;
using System.Threading.Tasks;

namespace PlantProcess.Application.Analytics.Advanced;

/// <summary>
/// An analysis target resolved from the governed outcome definitions: the
/// outcome key as it is actually registered, plus the grain that outcome is
/// measured at.
/// </summary>
public sealed record AnalysisOutcomeTarget(
    string OutcomeKey,
    string Grain,
    string DisplayName,
    string OutcomeType);

/// <summary>
/// T-045 Pack B. Resolves an analysis target by outcome key.
///
/// WHY THIS EXISTS. AdvancedAnalysisRequest requires a Grain, and grain is NOT
/// NULL on ml_outcome_definitions - it is a property of the outcome, not of the
/// widget that displays its readiness. A widget definition therefore cannot
/// carry it, and hardcoding one would put plant vocabulary into engine code.
///
/// It is a separate interface rather than a DbContext query because the ML
/// feature store is not exposed through IPlantProcessDbContext and that
/// interface publishes no raw-SQL surface. Same pattern, same lifetime and same
/// registration point as IFeatureVectorLoader.
///
/// An unknown or deleted key resolves to null. It is never defaulted: guessing
/// a grain would produce a readiness verdict about a population nobody asked
/// about, which reads exactly like a measurement.
/// </summary>
public interface IAnalysisOutcomeTargetResolver
{
    Task<AnalysisOutcomeTarget?> ResolveAsync(string outcomeKey, CancellationToken ct);
}