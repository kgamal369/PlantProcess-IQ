namespace PlantProcess.Application.Analytics.Contracts;

public sealed record CorrelationComputeRequest(
    string OutcomeKey,
    string Grain,
    int WindowDays,
    IReadOnlyDictionary<string, string>? Filters = null);

public sealed record CorrelationComputeResult(
    Guid ComputeRunId,
    int ResultCount,
    string EngineKey,
    string Status,
    string Message);

public sealed record EmbeddingRequest(
    string Text,
    int Dimensions = 1536);

public sealed record EmbeddingResult(
    IReadOnlyList<double> Vector,
    string ProviderKey,
    string ModelKey);