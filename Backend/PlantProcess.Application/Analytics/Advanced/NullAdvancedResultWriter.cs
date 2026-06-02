using System;
using System.Threading;
using System.Threading.Tasks;

namespace PlantProcess.Application.Analytics.Advanced;

/// <summary>No-op writer for unit tests and for runs where persistence is disabled.</summary>
public sealed class NullAdvancedResultWriter : IAdvancedResultWriter
{
    public Task<Guid> WriteAsync(AdvancedAnalysisRequest request, AdvancedAnalysisRunResult result, CancellationToken ct)
        => Task.FromResult(result.RunId);
}
