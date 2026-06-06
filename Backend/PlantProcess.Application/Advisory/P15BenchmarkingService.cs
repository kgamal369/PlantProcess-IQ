namespace PlantProcess.Application.Advisory;

/// <summary>
/// PPIQ_PACK_G7_T100_CROSS_PLANT_INDUSTRY_BENCHMARKING
/// Privacy-preserving cross-plant and industry benchmarking service.
///
/// Guardrails:
/// - no identifiable cross-tenant rows
/// - only anonymized aggregate benchmark bands
/// - minimum cohort size is enforced
/// - suppressed benchmark below cohort threshold
/// - industry reference bands are configurable/demo-driven
/// - generic manufacturing model, not steel-only
/// </summary>
public sealed class P15BenchmarkingService
{
    public P15BenchmarkDashboardResponse BuildDemoDashboard()
    {
        var request = BuildDemoRequest();
        var benchmark = Benchmark(request, cohortSize: 12);
        var suppressed = Benchmark(BuildDemoRequest(minimumCohortSize: 8), cohortSize: 3);

        return new P15BenchmarkDashboardResponse
        {
            Status = "Ready",
            Message = "Cross-plant and industry benchmark dashboard generated from anonymized aggregate demo bands.",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            TenantId = request.TenantId,
            PlantId = request.PlantId,
            IndustryCode = request.IndustryCode,
            Benchmarks = new[] { benchmark, suppressed },
            MetricCards = BuildMetricCards(benchmark),
            BestPractices = BuildBestPractices(request.IndustryCode),
            PrivacyGuards = DefaultPrivacyGuards()
        };
    }

    public P15BenchmarkRequest BuildDemoRequest(int minimumCohortSize = P15AdvisoryValueContract.DefaultMinimumBenchmarkCohortSize)
    {
        return new P15BenchmarkRequest
        {
            TenantId = "demo-tenant",
            PlantId = "demo-plant-01",
            MetricCode = "energy_intensity_index",
            IndustryCode = "generic-discrete-manufacturing",
            MinimumCohortSize = minimumCohortSize
        };
    }

    public P15BenchmarkResponse Benchmark(P15BenchmarkRequest request, int cohortSize)
    {
        var privacy = P15AdvisoryHonestyPolicy.ValidateBenchmarkVisibility(request, cohortSize);
        if (!privacy.IsAllowed)
        {
            return new P15BenchmarkResponse
            {
                MetricCode = request.MetricCode,
                IndustryCode = request.IndustryCode,
                Visibility = P15BenchmarkVisibility.SuppressedMinimumCohort,
                Message = "Benchmark suppressed because the anonymized cohort is below minimum privacy threshold.",
                Band = null,
                PrivacyGuards = DefaultPrivacyGuards().Append("Suppressed below minimum cohort size.").ToArray()
            };
        }

        var baseValue = BuildStableBaseValue(request);
        var band = new P15BenchmarkBand
        {
            BandCode = $"band-{Sanitize(request.IndustryCode)}-{Sanitize(request.MetricCode)}",
            P10 = Round(baseValue * 0.82m),
            P25 = Round(baseValue * 0.91m),
            P50 = Round(baseValue),
            P75 = Round(baseValue * 1.10m),
            P90 = Round(baseValue * 1.22m),
            CohortSize = cohortSize,
            Visibility = P15BenchmarkVisibility.Visible
        };

        return new P15BenchmarkResponse
        {
            MetricCode = request.MetricCode,
            IndustryCode = request.IndustryCode,
            Visibility = P15BenchmarkVisibility.Visible,
            Message = "Benchmark visible as anonymized aggregate band only. No identifiable cross-tenant rows are exposed.",
            Band = band,
            PrivacyGuards = DefaultPrivacyGuards()
        };
    }

    private static P15BenchmarkMetricCard[] BuildMetricCards(P15BenchmarkResponse benchmark)
    {
        if (benchmark.Band is null)
        {
            return Array.Empty<P15BenchmarkMetricCard>();
        }

        var plantValue = Round(benchmark.Band.P50 * 1.08m);
        var percentile = plantValue <= benchmark.Band.P25 ? 25m : plantValue <= benchmark.Band.P50 ? 50m : plantValue <= benchmark.Band.P75 ? 75m : 90m;

        return new[]
        {
            new P15BenchmarkMetricCard
            {
                MetricCode = benchmark.MetricCode,
                Label = "Plant vs anonymized industry benchmark",
                PlantValue = plantValue,
                IndustryMedian = benchmark.Band.P50,
                PercentileEstimate = percentile,
                BenchmarkVisibility = benchmark.Visibility,
                Interpretation = "Plant value is compared only against aggregate industry band. No source plant row is exposed."
            }
        };
    }

    private static P15BestPracticeReference[] BuildBestPractices(string industryCode)
    {
        return new[]
        {
            new P15BestPracticeReference
            {
                PracticeId = "bp-generic-energy-window-control",
                IndustryCode = industryCode,
                Title = "Stabilize high-impact process parameter windows",
                Description = "Use approved advisory windows and monitor post-change KPI drift before scaling to more areas.",
                EvidenceLevel = "AggregateBenchmarkReference",
                SafetyCaveat = "Template guidance only. Local process engineering approval is required."
            },
            new P15BestPracticeReference
            {
                PracticeId = "bp-generic-quality-energy-review",
                IndustryCode = industryCode,
                Title = "Review quality and energy trade-off together",
                Description = "Compare quality-risk and energy-intensity metrics together before accepting a recommendation.",
                EvidenceLevel = "AggregateBenchmarkReference",
                SafetyCaveat = "Correlation is not causation. Use as decision support, not automatic control."
            }
        };
    }

    private static string[] DefaultPrivacyGuards() =>
        new[]
        {
            "No identifiable cross-tenant row exposure.",
            "Only anonymized aggregate bands are returned.",
            "Minimum cohort size is enforced.",
            "Below-minimum cohort benchmark is suppressed.",
            "Reference bands are configuration/template driven."
        };

    private static decimal BuildStableBaseValue(P15BenchmarkRequest request)
    {
        var hash = StableHash($"{request.IndustryCode}|{request.MetricCode}");
        return 80m + (hash % 35);
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var ch in value.ToLowerInvariant())
            {
                hash = (hash * 31) + ch;
            }

            return Math.Abs(hash);
        }
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Sanitize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : new string(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
}

public sealed record P15BenchmarkDashboardResponse
{
    public required string Status { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public required string TenantId { get; init; }
    public required string PlantId { get; init; }
    public required string IndustryCode { get; init; }
    public P15BenchmarkResponse[] Benchmarks { get; init; } = Array.Empty<P15BenchmarkResponse>();
    public P15BenchmarkMetricCard[] MetricCards { get; init; } = Array.Empty<P15BenchmarkMetricCard>();
    public P15BestPracticeReference[] BestPractices { get; init; } = Array.Empty<P15BestPracticeReference>();
    public string[] PrivacyGuards { get; init; } = Array.Empty<string>();
}

public sealed record P15BenchmarkMetricCard
{
    public required string MetricCode { get; init; }
    public required string Label { get; init; }
    public decimal PlantValue { get; init; }
    public decimal IndustryMedian { get; init; }
    public decimal PercentileEstimate { get; init; }
    public P15BenchmarkVisibility BenchmarkVisibility { get; init; }
    public required string Interpretation { get; init; }
}

public sealed record P15BestPracticeReference
{
    public required string PracticeId { get; init; }
    public required string IndustryCode { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string EvidenceLevel { get; init; }
    public required string SafetyCaveat { get; init; }
}
