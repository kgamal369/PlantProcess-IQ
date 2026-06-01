using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Analytics.Core.Common;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Analytics.Core.Readiness;
using PlantProcess.Analytics.Engine;
using PlantProcess.Application.Analytics.Contracts;

namespace PlantProcess.Analytics.Engine.Tests;

internal sealed class FakeFeatureSource : ICanonicalFeatureSource
{
    private readonly FeatureMatrix _matrix;
    public FakeFeatureSource(FeatureMatrix matrix) => _matrix = matrix;
    public Task<FeatureMatrix> LoadAsync(CorrelationComputeRequest request, CancellationToken cancellationToken) => Task.FromResult(_matrix);

    /// <summary>One strong numeric driver, one strong binary driver, several pure-noise numerics, one categorical (skipped).</summary>
    public static FeatureMatrix PlantedSignalsAndDecoys(ReadinessInput readiness, int n = 250, int noiseCols = 4, ulong seed = 42UL)
    {
        var rng = new DeterministicRandom(seed);
        var speed = new double?[n];
        var binary = new double?[n];
        var outcome = new double[n];
        var noises = new double?[noiseCols][];
        for (int j = 0; j < noiseCols; j++) noises[j] = new double?[n];
        var route = new double?[n];

        for (int i = 0; i < n; i++)
        {
            double s = rng.NextUniform(0, 20);
            double b = rng.NextDouble() < 0.5 ? 1.0 : 0.0;
            speed[i] = s;
            binary[i] = b;
            outcome[i] = 50.0 + 0.5 * s + 4.0 * b + rng.NextGaussian(0, 0.3);
            for (int j = 0; j < noiseCols; j++) noises[j][i] = rng.NextGaussian();
            route[i] = rng.NextInt(3); // categorical code; engine skips numeric-vs-categorical
        }

        var cols = new List<FeatureColumn>
        {
            new("casting_speed", VariableType.Numeric, speed),
            new("is_grade_dx51d", VariableType.Binary, binary),
            new("route_code", VariableType.Categorical, route)
        };
        for (int j = 0; j < noiseCols; j++) cols.Add(new($"noise_{j}", VariableType.Numeric, noises[j]));

        return new FeatureMatrix("defect_rate", "coil", outcome, cols, readiness, ExcludedRecords: 5);
    }
}

internal sealed class InMemoryFindingSink : IAnalysisFindingSink
{
    public Guid LastRunId { get; private set; }
    public List<AnalysisFinding> Findings { get; } = new();
    public Task WriteAsync(Guid computeRunId, CorrelationComputeRequest request, IReadOnlyList<AnalysisFinding> findings, CancellationToken cancellationToken)
    {
        LastRunId = computeRunId;
        Findings.Clear();
        Findings.AddRange(findings);
        return Task.CompletedTask;
    }
}