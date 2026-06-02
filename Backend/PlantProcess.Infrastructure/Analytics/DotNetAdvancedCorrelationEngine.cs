using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Analytics.Advanced;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;

namespace PlantProcess.Infrastructure.Analytics;

/// <summary>
/// P4-01. ICorrelationComputeEngine adapter that routes the live correlation path to the
/// Phase-3 .NET Analytics.Core engine (method-aware findings, FDR, stability, stratification),
/// replacing the Pearson facade. Tenant resolves from Filters["tenantId"] until Phase 2's
/// ITenantAccessor lands; otherwise the demo tenant.
/// </summary>
public sealed class DotNetAdvancedCorrelationEngine : ICorrelationComputeEngine
{
    private readonly IAdvancedCorrelationService _service;
    public DotNetAdvancedCorrelationEngine(IAdvancedCorrelationService service) => _service = service;

    public string EngineKey => "dotnet-analytics-core-v1";

    public async Task<CorrelationComputeResult> ComputeAsync(CorrelationComputeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OutcomeKey))
            throw new ArgumentException("Outcome key is required.", nameof(request));

        var tenant = AdvancedDefaults.DemoTenant;
        if (request.Filters is not null && request.Filters.TryGetValue("tenantId", out var t) && Guid.TryParse(t, out var g))
            tenant = g;

        var advReq = new AdvancedAnalysisRequest(
            request.OutcomeKey.Trim(),
            string.IsNullOrWhiteSpace(request.Grain) ? "coil" : request.Grain.Trim(),
            request.WindowDays <= 0 ? 3650 : request.WindowDays,
            tenant,
            request.Filters?.Select(kv => $"{kv.Key}={kv.Value}").ToList());

        var r = await _service.ComputeAsync(advReq, cancellationToken);
        var status = !r.CanRun ? "Blocked" : (r.Findings.Count > 0 ? "Ok" : "NoData");
        return new CorrelationComputeResult(r.RunId, r.Findings.Count, EngineKey, status, r.Message);
    }
}
