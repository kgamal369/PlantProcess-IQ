using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Common.Time;
using PlantProcess.Application.Contracts.Readiness;

namespace PlantProcess.Application.Services.Readiness;

public sealed class ApplicationReadinessService : IApplicationReadinessService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IClock _clock;

    public ApplicationReadinessService(
        IPlantProcessDbContext dbContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<ApplicationResult<ApplicationReadinessDto>> GetReadinessAsync(
        CancellationToken cancellationToken)
    {
        var evidence = await BuildEvidenceAsync(cancellationToken);
        var dimensions = BuildDimensions(evidence);
        var blockers = BuildTopBlockers(dimensions);
        var actions = BuildRecommendedActions(dimensions, evidence);
        var overallScore = Round(dimensions.Average(x => x.Score));
        var feasibility = ClassifyFeasibility(overallScore);

        var dto = new ApplicationReadinessDto(
            Service: "PlantProcess IQ Application Layer",
            Layer: "Application",
            Status: feasibility,
            Version: "Readiness-v1.0",
            CheckedAtUtc: _clock.UtcNow,
            RegisteredCapabilities:
            [
                "7-dimension commercial readiness scoring",
                "Source availability assessment",
                "Schema completeness assessment",
                "Mapping coverage assessment",
                "Genealogy linkage assessment",
                "Data quality assessment",
                "Correlation readiness assessment",
                "Prediction readiness assessment"
            ],
            OverallScore: overallScore,
            OverallFeasibility: feasibility,
            Dimensions: dimensions,
            TopBlockers: blockers,
            RecommendedActions: actions,
            PilotFeasibility: BuildPilotFeasibility(overallScore, dimensions),
            Evidence: evidence);

        return ApplicationResult<ApplicationReadinessDto>.Success(dto);
    }

    public async Task<ApplicationResult<CommercialReadinessReportDto>> BuildCommercialReadinessReportAsync(
        CommercialReadinessReportRequest request,
        CancellationToken cancellationToken)
    {
        var readinessResult = await GetReadinessAsync(cancellationToken);

        if (!readinessResult.IsSuccess || readinessResult.Value is null)
        {
            return ApplicationResult<CommercialReadinessReportDto>.Failure(readinessResult.Error!);
        }

        var readiness = readinessResult.Value;
        var customerName = Clean(request.CustomerName) ?? "Customer";
        var preparedBy = Clean(request.PreparedBy) ?? "PlantProcess IQ";

        var summary = BuildExecutiveSummary(customerName, readiness);

        var report = new CommercialReadinessReportDto(
            AssessmentId: Guid.NewGuid(),
            GeneratedAtUtc: _clock.UtcNow,
            OverallScore: readiness.OverallScore,
            OverallFeasibility: readiness.OverallFeasibility,
            ExecutiveSummary: summary,
            Dimensions: readiness.Dimensions,
            TopBlockers: readiness.TopBlockers,
            RecommendedActions: readiness.RecommendedActions,
            PilotFeasibility: readiness.PilotFeasibility,
            Evidence: readiness.Evidence,
            Disclaimer:
                "PlantProcess IQ readiness and correlation outputs indicate suspected contributors and data-preparation gaps. " +
                "Root-cause confirmation and process changes require process engineering validation. " +
                $"Prepared by {preparedBy}.");

        return ApplicationResult<CommercialReadinessReportDto>.Success(report);
    }

    public async Task<ApplicationResult<ReadinessPdfReportResult>> BuildCommercialReadinessPdfAsync(
        CommercialReadinessReportRequest request,
        CancellationToken cancellationToken)
    {
        var reportResult = await BuildCommercialReadinessReportAsync(request, cancellationToken);

        if (!reportResult.IsSuccess || reportResult.Value is null)
        {
            return ApplicationResult<ReadinessPdfReportResult>.Failure(reportResult.Error!);
        }

        var report = reportResult.Value;
        var customerName = Clean(request.CustomerName) ?? "Customer";
        var pdf = SimplePdfWriter.BuildReadinessPdf(customerName, report);

        var fileName =
            $"PlantProcessIQ-ReadinessAssessment-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf";

        return ApplicationResult<ReadinessPdfReportResult>.Success(
            new ReadinessPdfReportResult(
                Content: pdf,
                ContentType: "application/pdf",
                FileName: fileName));
    }

    private async Task<ReadinessEvidenceDto> BuildEvidenceAsync(CancellationToken cancellationToken)
    {
        var connectionProfileCount = await _dbContext.ConnectionProfiles
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var activeConnectionProfileCount = await _dbContext.ConnectionProfiles
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.IsActive, cancellationToken);

        var sourceDatasetCount = await _dbContext.SourceDatasetDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var sourceFieldCount = await _dbContext.SourceFieldDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.IsActive, cancellationToken);

        var mappingDefinitionCount = await _dbContext.MappingDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var activeMappingDefinitionCount = await _dbContext.MappingDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.IsActive, cancellationToken);

        var materialUnitCount = await _dbContext.MaterialUnits
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var genealogyEdgeCount = await _dbContext.GenealogyEdges
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var materialUnitsWithGenealogyCount = await _dbContext.GenealogyEdges
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => x.ParentMaterialUnitId)
            .Union(
                _dbContext.GenealogyEdges
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .Select(x => x.ChildMaterialUnitId))
            .Distinct()
            .CountAsync(cancellationToken);

        var parameterDefinitionCount = await _dbContext.ParameterDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var parameterObservationCount = await _dbContext.ParameterObservations
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var qualityEventCount = await _dbContext.QualityEvents
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var dataQualityIssueCount = await _dbContext.DataQualityIssues
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var criticalDataQualityIssueCount = await _dbContext.DataQualityIssues
            .AsNoTracking()
            .CountAsync(x =>
                !x.IsDeleted &&
                x.Severity != null &&
                x.Severity.ToLower() == "critical",
                cancellationToken);

        var highDataQualityIssueCount = await _dbContext.DataQualityIssues
            .AsNoTracking()
            .CountAsync(x =>
                !x.IsDeleted &&
                x.Severity != null &&
                x.Severity.ToLower() == "high",
                cancellationToken);

        var riskScoreCount = await _dbContext.RiskScores
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var modelRegistryCount = await _dbContext.ModelRegistries
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var latestImportBatchCount = await _dbContext.ImportBatches
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var lastSuccessfulImportAtUtc = await _dbContext.ImportBatches
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == "Completed")
            .OrderByDescending(x => x.CompletedAtUtc)
            .Select(x => x.CompletedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var lastRiskScoreAtUtc = await _dbContext.RiskScores
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.ScoredAtUtc)
            .Select(x => (DateTime?)x.ScoredAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var lastModelRegisteredAtUtc = await _dbContext.ModelRegistries
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => (DateTime?)x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new ReadinessEvidenceDto(
            connectionProfileCount,
            activeConnectionProfileCount,
            sourceDatasetCount,
            sourceFieldCount,
            mappingDefinitionCount,
            activeMappingDefinitionCount,
            materialUnitCount,
            materialUnitsWithGenealogyCount,
            genealogyEdgeCount,
            parameterDefinitionCount,
            parameterObservationCount,
            qualityEventCount,
            dataQualityIssueCount,
            criticalDataQualityIssueCount,
            highDataQualityIssueCount,
            riskScoreCount,
            modelRegistryCount,
            latestImportBatchCount,
            lastSuccessfulImportAtUtc,
            lastRiskScoreAtUtc,
            lastModelRegisteredAtUtc);
    }

    private static IReadOnlyList<ReadinessDimensionDto> BuildDimensions(ReadinessEvidenceDto e)
    {
        return
        [
            SourceAvailability(e),
            SchemaCompleteness(e),
            MappingCoverage(e),
            GenealogyLinkage(e),
            DataQuality(e),
            CorrelationReadiness(e),
            PredictionReadiness(e)
        ];
    }

    private static ReadinessDimensionDto SourceAvailability(ReadinessEvidenceDto e)
    {
        var score = 0m;
        var reasons = new List<string>();
        var blockers = new List<string>();
        var evidence = new List<string>();

        evidence.Add($"Connection profiles: {e.ConnectionProfileCount}");
        evidence.Add($"Active connection profiles: {e.ActiveConnectionProfileCount}");
        evidence.Add($"Import batches: {e.LatestImportBatchCount}");

        if (e.ConnectionProfileCount == 0)
        {
            blockers.Add("No source connection profile is configured.");
        }
        else
        {
            score += 35;
            reasons.Add("At least one source connection profile exists.");
        }

        if (e.ActiveConnectionProfileCount > 0)
        {
            score += 35;
            reasons.Add("At least one source connection profile is active.");
        }
        else
        {
            blockers.Add("No active source connection profile is available.");
        }

        if (e.LastSuccessfulImportAtUtc.HasValue)
        {
            score += 30;
            reasons.Add($"Last successful import completed at {e.LastSuccessfulImportAtUtc:O}.");
        }
        else
        {
            blockers.Add("No completed import batch was found.");
        }

        return Dimension("SOURCE_AVAILABILITY", "Source Availability", score, reasons, blockers, evidence);
    }

    private static ReadinessDimensionDto SchemaCompleteness(ReadinessEvidenceDto e)
    {
        var score = 0m;
        var reasons = new List<string>();
        var blockers = new List<string>();
        var evidence = new List<string>
        {
            $"Source datasets: {e.SourceDatasetCount}",
            $"Source fields: {e.SourceFieldCount}",
            $"Parameter definitions: {e.ParameterDefinitionCount}"
        };

        if (e.SourceDatasetCount > 0)
        {
            score += 35;
            reasons.Add("Source datasets are discovered.");
        }
        else
        {
            blockers.Add("No source datasets have been discovered.");
        }

        if (e.SourceFieldCount >= 10)
        {
            score += 35;
            reasons.Add("Source field metadata is available.");
        }
        else if (e.SourceFieldCount > 0)
        {
            score += 20;
            reasons.Add("Some source field metadata is available.");
            blockers.Add("Source field metadata is still limited.");
        }
        else
        {
            blockers.Add("No source fields are available.");
        }

        if (e.ParameterDefinitionCount > 0)
        {
            score += 30;
            reasons.Add("Canonical parameter definitions exist.");
        }
        else
        {
            blockers.Add("No canonical parameter definitions exist.");
        }

        return Dimension("SCHEMA_COMPLETENESS", "Schema Completeness", score, reasons, blockers, evidence);
    }

    private static ReadinessDimensionDto MappingCoverage(ReadinessEvidenceDto e)
    {
        var score = 0m;
        var reasons = new List<string>();
        var blockers = new List<string>();
        var evidence = new List<string>
        {
            $"Mapping definitions: {e.MappingDefinitionCount}",
            $"Active mapping definitions: {e.ActiveMappingDefinitionCount}"
        };

        if (e.MappingDefinitionCount == 0)
        {
            blockers.Add("No mapping definitions exist.");
        }
        else
        {
            score += 45;
            reasons.Add("Mapping definitions exist.");
        }

        if (e.ActiveMappingDefinitionCount > 0)
        {
            score += 55;
            reasons.Add("At least one mapping definition is active.");
        }
        else
        {
            blockers.Add("No active mapping definition is available.");
        }

        return Dimension("MAPPING_COVERAGE", "Mapping Coverage", score, reasons, blockers, evidence);
    }

    private static ReadinessDimensionDto GenealogyLinkage(ReadinessEvidenceDto e)
    {
        var score = e.MaterialUnitCount == 0
            ? 0
            : Math.Min(100, Round((decimal)e.MaterialUnitsWithGenealogyCount / e.MaterialUnitCount * 100));

        var reasons = new List<string>();
        var blockers = new List<string>();
        var evidence = new List<string>
        {
            $"Material units: {e.MaterialUnitCount}",
            $"Material units with genealogy: {e.MaterialUnitsWithGenealogyCount}",
            $"Genealogy edges: {e.GenealogyEdgeCount}"
        };

        if (e.MaterialUnitCount == 0)
        {
            blockers.Add("No material units exist.");
        }
        else if (score >= 70)
        {
            reasons.Add("Most materials have genealogy linkage.");
        }
        else if (score > 0)
        {
            reasons.Add("Some materials have genealogy linkage.");
            blockers.Add("Genealogy coverage is not yet strong enough for robust upstream/downstream investigation.");
        }
        else
        {
            blockers.Add("No genealogy linkage exists.");
        }

        return Dimension("GENEALOGY_LINKAGE", "Genealogy Linkage", score, reasons, blockers, evidence);
    }

    private static ReadinessDimensionDto DataQuality(ReadinessEvidenceDto e)
    {
        var score = 100m;
        var reasons = new List<string>();
        var blockers = new List<string>();
        var evidence = new List<string>
        {
            $"Data quality issues: {e.DataQualityIssueCount}",
            $"Critical issues: {e.CriticalDataQualityIssueCount}",
            $"High issues: {e.HighDataQualityIssueCount}"
        };

        score -= e.CriticalDataQualityIssueCount * 20m;
        score -= e.HighDataQualityIssueCount * 10m;
        score -= Math.Max(0, e.DataQualityIssueCount - e.CriticalDataQualityIssueCount - e.HighDataQualityIssueCount) * 2m;
        score = Clamp(score);

        if (e.CriticalDataQualityIssueCount > 0)
            blockers.Add($"{e.CriticalDataQualityIssueCount} critical data-quality issues must be fixed before pilot.");

        if (e.HighDataQualityIssueCount > 0)
            blockers.Add($"{e.HighDataQualityIssueCount} high-severity data-quality issues should be resolved.");

        if (e.DataQualityIssueCount == 0)
            reasons.Add("No data-quality issues are currently open.");
        else
            reasons.Add("Data-quality issues are detected and visible for remediation.");

        return Dimension("DATA_QUALITY", "Data Quality", score, reasons, blockers, evidence);
    }

    private static ReadinessDimensionDto CorrelationReadiness(ReadinessEvidenceDto e)
    {
        var parameterScore = Math.Min(50, e.ParameterObservationCount / 1000m * 50m);
        var qualityScore = Math.Min(50, e.QualityEventCount / 200m * 50m);
        var score = Round(parameterScore + qualityScore);

        var reasons = new List<string>();
        var blockers = new List<string>();
        var evidence = new List<string>
        {
            $"Parameter observations: {e.ParameterObservationCount}",
            $"Quality events: {e.QualityEventCount}"
        };

        if (e.ParameterObservationCount >= 1000)
            reasons.Add("Parameter observation volume is enough for first-pass analysis.");
        else
            blockers.Add("At least 1,000 parameter observations are recommended for reliable correlation.");

        if (e.QualityEventCount >= 200)
            reasons.Add("Quality-event volume is enough for first-pass correlation.");
        else
            blockers.Add("At least 200 quality events are recommended for reliable defect/outcome correlation.");

        return Dimension("CORRELATION_READINESS", "Correlation Readiness", score, reasons, blockers, evidence);
    }

    private static ReadinessDimensionDto PredictionReadiness(ReadinessEvidenceDto e)
    {
        var score = 0m;
        var reasons = new List<string>();
        var blockers = new List<string>();
        var evidence = new List<string>
        {
            $"Risk scores: {e.RiskScoreCount}",
            $"Model registry records: {e.ModelRegistryCount}",
            $"Last risk score: {e.LastRiskScoreAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "None"}",
            $"Last model registration: {e.LastModelRegisteredAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "None"}"
        };

        if (e.RiskScoreCount > 0)
        {
            score += 50;
            reasons.Add("Rule-based risk scoring output exists.");
        }
        else
        {
            blockers.Add("No risk scores exist yet.");
        }

        if (e.ModelRegistryCount > 0)
        {
            score += 50;
            reasons.Add("Model registry records exist.");
        }
        else
        {
            blockers.Add("No model registry records exist yet. Keep external claims to rule-based scoring until ML jobs are active.");
        }

        return Dimension("PREDICTION_READINESS", "Prediction Readiness", score, reasons, blockers, evidence);
    }

    private static ReadinessDimensionDto Dimension(
        string code,
        string name,
        decimal score,
        IReadOnlyList<string> reasons,
        IReadOnlyList<string> blockers,
        IReadOnlyList<string> evidence)
    {
        var normalized = Clamp(score);

        return new ReadinessDimensionDto(
            Code: code,
            Name: name,
            Score: normalized,
            Status: ClassifyDimension(normalized),
            Reasons: reasons.Count == 0 ? ["No positive evidence found yet."] : reasons,
            Blockers: blockers,
            Evidence: evidence);
    }

    private static IReadOnlyList<ReadinessBlockerDto> BuildTopBlockers(IReadOnlyList<ReadinessDimensionDto> dimensions)
    {
        return dimensions
            .SelectMany(d => d.Blockers.Select(blocker => new ReadinessBlockerDto(
                Blocker: blocker,
                Dimension: d.Name,
                EstimatedEffortHours: d.Score < 30 ? 16 : 8,
                Severity: d.Score < 30 ? "Critical" : d.Score < 70 ? "High" : "Medium")))
            .Take(10)
            .ToList();
    }

    private static IReadOnlyList<ReadinessRecommendedActionDto> BuildRecommendedActions(
        IReadOnlyList<ReadinessDimensionDto> dimensions,
        ReadinessEvidenceDto evidence)
    {
        var actions = dimensions
            .Where(d => d.Score < 85)
            .OrderBy(d => d.Score)
            .Select((d, index) => new ReadinessRecommendedActionDto(
                Rank: index + 1,
                Action: RecommendedActionFor(d.Code, evidence),
                Dimension: d.Name,
                ExpectedImpact: $"Improve {d.Name} readiness and unblock the next commercial gate.",
                EstimatedEffortHours: d.Score < 30 ? 16 : 8))
            .Take(7)
            .ToList();

        return actions.Count > 0
            ? actions
            : [new ReadinessRecommendedActionDto(1, "Maintain current data pipeline and validate on a larger customer dataset.", "Overall", "Preserve readiness while scaling data volume.", 4)];
    }

    private static string RecommendedActionFor(string code, ReadinessEvidenceDto evidence)
    {
        return code switch
        {
            "SOURCE_AVAILABILITY" => "Configure and test at least one active source connection profile, then run a successful import batch.",
            "SCHEMA_COMPLETENESS" => "Run schema discovery and confirm source datasets/fields are populated.",
            "MAPPING_COVERAGE" => "Create and activate mapping definitions for MaterialUnit, ProcessStepExecution, ParameterObservation, QualityEvent, and GenealogyEdge.",
            "GENEALOGY_LINKAGE" => "Add upstream/downstream genealogy mapping so material investigation can traverse parent-child material flow.",
            "DATA_QUALITY" => "Run the data-quality scanner and close critical/high issues before customer demo.",
            "CORRELATION_READINESS" => "Load more parameter observations and quality events from representative process periods.",
            "PREDICTION_READINESS" => "Keep risk scoring labelled as rule-based until model registry and ML learning jobs are active.",
            _ => "Review readiness blockers and resolve missing evidence."
        };
    }

    private static PilotFeasibilityDto BuildPilotFeasibility(
        decimal overallScore,
        IReadOnlyList<ReadinessDimensionDto> dimensions)
    {
        var blockers = dimensions
            .Where(x => x.Score < 50)
            .SelectMany(x => x.Blockers.Select(b => $"{x.Name}: {b}"))
            .Take(5)
            .ToList();

        return new PilotFeasibilityDto(
            Minimal: new PilotScopeFeasibilityDto(
                Scope: "Minimal",
                Feasible: overallScore >= 35,
                Recommendation: overallScore >= 35
                    ? "Feasible for one route, one source export, and one quality outcome."
                    : "Not feasible yet. Configure source, schema, and mapping first.",
                Blockers: overallScore >= 35 ? [] : blockers),
            Standard: new PilotScopeFeasibilityDto(
                Scope: "Standard",
                Feasible: overallScore >= 60,
                Recommendation: overallScore >= 60
                    ? "Feasible for one plant area and multiple defect/outcome families."
                    : "Wait until mapping, genealogy, and data-quality readiness improve.",
                Blockers: overallScore >= 60 ? [] : blockers),
            Full: new PilotScopeFeasibilityDto(
                Scope: "Full",
                Feasible: overallScore >= 80,
                Recommendation: overallScore >= 80
                    ? "Feasible for broad plant-scope pilot."
                    : "Not recommended until readiness exceeds 80 and data volume is stronger.",
                Blockers: overallScore >= 80 ? [] : blockers));
    }

    private static string BuildExecutiveSummary(string customerName, ApplicationReadinessDto readiness)
    {
        var e = readiness.Evidence;

        return
            $"Based on {e.MaterialUnitCount} material records, {e.ParameterObservationCount} parameter observations, " +
            $"{e.QualityEventCount} quality events, {e.ConnectionProfileCount} connection profiles, and " +
            $"{e.MappingDefinitionCount} mapping definitions, {customerName}'s PlantProcess IQ readiness score is " +
            $"{readiness.OverallScore:0.0}/100. The current feasibility classification is {readiness.OverallFeasibility}. " +
            (readiness.OverallScore >= 70
                ? "The data foundation is strong enough for controlled correlation analysis and a focused pilot readiness discussion."
                : readiness.OverallScore >= 40
                    ? "The system has a usable foundation, but several data preparation and workflow gaps should be closed before a paid pilot."
                    : "The current data foundation is not yet ready for a customer-grade pilot. Source, schema, mapping, and data-quality blockers should be resolved first.");
    }

    private static string ClassifyFeasibility(decimal score)
    {
        if (score >= 80) return "High";
        if (score >= 60) return "Medium";
        if (score >= 35) return "Low";
        return "Blocked";
    }

    private static string ClassifyDimension(decimal score)
    {
        if (score >= 85) return "Ready";
        if (score >= 60) return "Needs Improvement";
        if (score >= 30) return "At Risk";
        return "Blocked";
    }

    private static decimal Clamp(decimal value) => Math.Min(100, Math.Max(0, Round(value)));

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static class SimplePdfWriter
    {
        public static byte[] BuildReadinessPdf(string customerName, CommercialReadinessReportDto report)
        {
            var pages = new List<string>
            {
                BuildCoverPage(customerName, report),
                BuildSummaryPage(report),
                BuildDimensionPage(report),
                BuildActionsPage(report),
                BuildEvidencePage(report)
            };

            return BuildPdf(pages);
        }

        private static string BuildCoverPage(string customerName, CommercialReadinessReportDto report)
        {
            return string.Join("\n",
                "PlantProcess IQ",
                "Data Diagnostic Assessment",
                "",
                $"Customer: {customerName}",
                $"Assessment ID: {report.AssessmentId}",
                $"Generated UTC: {report.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss}",
                $"Overall Score: {report.OverallScore:0.0}/100",
                $"Feasibility: {report.OverallFeasibility}",
                "",
                "Prepared for manufacturing process-to-quality readiness review.");
        }

        private static string BuildSummaryPage(CommercialReadinessReportDto report)
        {
            return string.Join("\n",
                "Executive Summary",
                "",
                Wrap(report.ExecutiveSummary),
                "",
                "Disclaimer:",
                Wrap(report.Disclaimer));
        }

        private static string BuildDimensionPage(CommercialReadinessReportDto report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("7-Dimension Readiness Scorecards");
            sb.AppendLine();

            foreach (var dimension in report.Dimensions)
            {
                sb.AppendLine($"{dimension.Name}: {dimension.Score:0.0}/100 — {dimension.Status}");
                sb.AppendLine($"Evidence: {string.Join("; ", dimension.Evidence)}");
                sb.AppendLine($"Reasons: {string.Join("; ", dimension.Reasons)}");

                if (dimension.Blockers.Count > 0)
                    sb.AppendLine($"Blockers: {string.Join("; ", dimension.Blockers)}");

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string BuildActionsPage(CommercialReadinessReportDto report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Recommended Next Actions");
            sb.AppendLine();

            foreach (var action in report.RecommendedActions)
            {
                sb.AppendLine($"{action.Rank}. {action.Action}");
                sb.AppendLine($"Dimension: {action.Dimension}");
                sb.AppendLine($"Impact: {action.ExpectedImpact}");
                sb.AppendLine($"Estimated effort: {action.EstimatedEffortHours}h");
                sb.AppendLine();
            }

            sb.AppendLine("Pilot Feasibility");
            sb.AppendLine($"{report.PilotFeasibility.Minimal.Scope}: {report.PilotFeasibility.Minimal.Recommendation}");
            sb.AppendLine($"{report.PilotFeasibility.Standard.Scope}: {report.PilotFeasibility.Standard.Recommendation}");
            sb.AppendLine($"{report.PilotFeasibility.Full.Scope}: {report.PilotFeasibility.Full.Recommendation}");

            return sb.ToString();
        }

        private static string BuildEvidencePage(CommercialReadinessReportDto report)
        {
            var e = report.Evidence;

            return string.Join("\n",
                "Evidence Snapshot",
                "",
                $"Connection profiles: {e.ConnectionProfileCount}",
                $"Active connection profiles: {e.ActiveConnectionProfileCount}",
                $"Source datasets: {e.SourceDatasetCount}",
                $"Source fields: {e.SourceFieldCount}",
                $"Mappings: {e.MappingDefinitionCount}",
                $"Active mappings: {e.ActiveMappingDefinitionCount}",
                $"Material units: {e.MaterialUnitCount}",
                $"Materials with genealogy: {e.MaterialUnitsWithGenealogyCount}",
                $"Genealogy edges: {e.GenealogyEdgeCount}",
                $"Parameter definitions: {e.ParameterDefinitionCount}",
                $"Parameter observations: {e.ParameterObservationCount}",
                $"Quality events: {e.QualityEventCount}",
                $"Data-quality issues: {e.DataQualityIssueCount}",
                $"Critical DQ issues: {e.CriticalDataQualityIssueCount}",
                $"High DQ issues: {e.HighDataQualityIssueCount}",
                $"Risk scores: {e.RiskScoreCount}",
                $"Model registry records: {e.ModelRegistryCount}",
                $"Import batches: {e.LatestImportBatchCount}",
                $"Last successful import: {e.LastSuccessfulImportAtUtc?.ToString("O") ?? "None"}",
                $"Last risk score: {e.LastRiskScoreAtUtc?.ToString("O") ?? "None"}",
                $"Last model registered: {e.LastModelRegisteredAtUtc?.ToString("O") ?? "None"}");
        }

        private static string Wrap(string value)
        {
            const int width = 92;
            var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            var line = new StringBuilder();

            foreach (var word in words)
            {
                if (line.Length + word.Length + 1 > width)
                {
                    sb.AppendLine(line.ToString());
                    line.Clear();
                }

                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }

            if (line.Length > 0) sb.AppendLine(line.ToString());
            return sb.ToString();
        }

        private static byte[] BuildPdf(IReadOnlyList<string> pages)
        {
            var objects = new List<string>();
            var pageObjectNumbers = new List<int>();

            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            objects.Add("<< /Type /Pages /Kids [] /Count 0 >>");

            foreach (var pageText in pages)
            {
                var content = BuildPageContent(pageText);
                var contentObjectNumber = objects.Count + 1;

                objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");

                var pageObjectNumber = objects.Count + 1;
                pageObjectNumbers.Add(pageObjectNumber);

                objects.Add(
                    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] " +
                    "/Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> " +
                    "/F2 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >> >> >> " +
                    $"/Contents {contentObjectNumber} 0 R >>");
            }

            objects[1] = $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectNumbers.Select(x => $"{x} 0 R"))}] /Count {pages.Count} >>";

            var output = new MemoryStream();
            var writer = new StreamWriter(output, Encoding.ASCII, leaveOpen: true);

            writer.Write("%PDF-1.4\n");
            var offsets = new List<long> { 0 };

            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(output.Position);
                writer.Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
                writer.Flush();
            }

            var xrefPosition = output.Position;
            writer.Write($"xref\n0 {objects.Count + 1}\n");
            writer.Write("0000000000 65535 f \n");

            foreach (var offset in offsets.Skip(1))
                writer.Write($"{offset:0000000000} 00000 n \n");

            writer.Write("trailer\n");
            writer.Write($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
            writer.Write("startxref\n");
            writer.Write($"{xrefPosition}\n");
            writer.Write("%%EOF");
            writer.Flush();

            return output.ToArray();
        }

        private static string BuildPageContent(string text)
        {
            var sb = new StringBuilder();
            sb.AppendLine("BT");
            sb.AppendLine("/F2 18 Tf");
            sb.AppendLine("50 790 Td");

            var lines = text
                .Replace("\r", "")
                .Split('\n')
                .SelectMany(line => SplitPdfLine(line, 92))
                .Take(46)
                .ToList();

            var first = true;

            foreach (var line in lines)
            {
                if (!first)
                    sb.AppendLine("0 -16 Td");

                var safe = EscapePdf(line);
                sb.AppendLine($"({safe}) Tj");
                first = false;
            }

            sb.AppendLine("ET");
            return sb.ToString();
        }

        private static IEnumerable<string> SplitPdfLine(string line, int width)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                yield return "";
                yield break;
            }

            var current = line.Trim();

            while (current.Length > width)
            {
                yield return current[..width];
                current = current[width..].TrimStart();
            }

            yield return current;
        }

        private static string EscapePdf(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("—", "-")
                .Replace("–", "-")
                .Replace("≥", ">=")
                .Replace("≤", "<=")
                .Replace("€", "EUR");
        }
    }
}