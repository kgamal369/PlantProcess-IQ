namespace PlantProcess.Application.Contracts.Readiness;

public sealed record ApplicationReadinessDto(
    string Service,
    string Layer,
    string Status,
    string Version,
    DateTime CheckedAtUtc,
    IReadOnlyList<string> RegisteredCapabilities);


