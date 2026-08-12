using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Analytics.Advanced;

namespace PlantProcess.Infrastructure.IntegrationTests.Analytics;

/// <summary>
/// T-045. Construction doubles for the Class-2 dependencies.
///
/// These suites test the CLASS-1 aggregate path. They need the two readiness
/// dependencies only because DashboardWidgetQueryService takes them, and a
/// Class-1 query never reaches either one.
///
/// BOTH THROW. A stub that returned a plausible empty answer would let a test
/// silently drift into the Class-2 path and still pass, which is the failure
/// mode this whole task exists to remove. If one of these is ever called from a
/// Class-1 test, the test fails and names why.
/// </summary>
internal static class ClassOneOnly
{
    public static IAnalysisReadinessService Readiness { get; } = new ThrowingReadinessService();

    public static IAnalysisOutcomeTargetResolver TargetResolver { get; } = new ThrowingTargetResolver();

    private sealed class ThrowingReadinessService : IAnalysisReadinessService
    {
        public Task<AnalysisReadinessDto> EvaluateAsync(AdvancedAnalysisRequest request, CancellationToken ct)
        {
            throw new InvalidOperationException(
                "A Class-1 aggregate test reached the readiness service. The query under test routed " +
                "into the Class-2 seam, which it must never do.");
        }
    }

    private sealed class ThrowingTargetResolver : IAnalysisOutcomeTargetResolver
    {
        public Task<AnalysisOutcomeTarget?> ResolveAsync(string outcomeKey, CancellationToken ct)
        {
            throw new InvalidOperationException(
                "A Class-1 aggregate test reached the analysis target resolver. The query under test " +
                "routed into the Class-2 seam, which it must never do.");
        }
    }
}