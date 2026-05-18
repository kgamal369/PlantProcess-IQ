using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Common.Time;
using PlantProcess.Application.Contracts.Readiness;
using PlantProcess.Application.Services.Readiness;

namespace PlantProcess.Application.Services.Readiness;

public sealed class ApplicationReadinessService : IApplicationReadinessService
{
    private readonly IClock _clock;

    public ApplicationReadinessService(IClock clock)
    {
        _clock = clock;
    }

    public Task<ApplicationResult<ApplicationReadinessDto>> GetReadinessAsync(
        CancellationToken cancellationToken)
    {
        var dto = new ApplicationReadinessDto(
            Service: "PlantProcess IQ Application Layer",
            Layer: "Application",
            Status: "Ready",
            Version: "Phase 1",
            CheckedAtUtc: _clock.UtcNow,
            RegisteredCapabilities:
            [
                "Common application result model",
                "Common validation model",
                "Command/query contract foundation",
                "Service contract foundation",
                "Application DI registration",
                "Readiness service"
            ]);

        return Task.FromResult(ApplicationResult<ApplicationReadinessDto>.Success(dto));
    }
}


