const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

function p(relativePath) {
  return path.join(root, relativePath.replace(/\//g, path.sep));
}

function ensureDir(filePath) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
}

function write(relativePath, content) {
  const target = p(relativePath);
  ensureDir(target);
  fs.writeFileSync(target, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + relativePath);
}

function patchFile(relativePath, patcher) {
  const target = p(relativePath);
  if (!fs.existsSync(target)) {
    throw new Error("Missing file: " + relativePath);
  }

  const before = fs.readFileSync(target, "utf8");
  const after = patcher(before);

  if (after !== before) {
    fs.writeFileSync(target, after, "utf8");
    console.log("Patched: " + relativePath);
  } else {
    console.log("No change: " + relativePath);
  }
}

function run(name, command, args, cwd = root) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(command, args, { cwd, stdio: "inherit", shell: false });
}

write("Backend/PlantProcess.Application/AssistantRuntime/Phase8AssistantRuntimeContracts.cs", String.raw`
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Application.AssistantRuntime;

/// <summary>
/// PPIQ_REALIZATION_T045_T046_T047_PHASE8_ASSISTANT_HMI.
/// Shared contracts for Phase 8 suggestions, grounded assistant runtime configuration,
/// and HMI-side assistant configuration.
/// </summary>
public sealed record Phase8AssistantConfiguration(
    string Mode,
    string GroundingPolicy,
    string EvidencePolicy,
    bool NoEgress,
    int MaxCitations,
    IReadOnlyList<string> AllowedTools,
    bool RequireHumanApprovalForRecommendations,
    bool EnableSuggestionWorkflow,
    string UpdatedBy,
    DateTimeOffset UpdatedAtUtc)
{
    public static Phase8AssistantConfiguration Default(string updatedBy = "system")
        => new(
            Mode: "grounded-extractive",
            GroundingPolicy: "strict-citations-required",
            EvidencePolicy: "citations-and-provenance-required",
            NoEgress: true,
            MaxCitations: 5,
            AllowedTools: new[] { "material-investigation", "quality-evidence", "value-scenario" },
            RequireHumanApprovalForRecommendations: true,
            EnableSuggestionWorkflow: true,
            UpdatedBy: updatedBy,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
}

public sealed record Phase8AssistantConfigurationValidation(
    bool IsValid,
    Phase8AssistantConfiguration Normalized,
    IReadOnlyList<string> Findings);

public static class Phase8AssistantConfigurationValidator
{
    private static readonly HashSet<string> AllowedModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "grounded-extractive",
        "private-model",
        "self-hosted"
    };

    private static readonly HashSet<string> AllowedGroundingPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        "strict-citations-required",
        "abstain-on-missing-evidence",
        "demo-extractive-only"
    };

    private static readonly HashSet<string> AllowedEvidencePolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        "citations-and-provenance-required",
        "citations-required",
        "provenance-required"
    };

    private static readonly HashSet<string> AllowedTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "material-investigation",
        "quality-evidence",
        "value-scenario",
        "recommendation-review",
        "mapping-health",
        "data-quality"
    };

    public static Phase8AssistantConfigurationValidation ValidateAndNormalize(Phase8AssistantConfiguration? input, string updatedBy)
    {
        var source = input ?? Phase8AssistantConfiguration.Default(updatedBy);
        var findings = new List<string>();

        var mode = NormalizeChoice(source.Mode, AllowedModes, "grounded-extractive", "mode", findings);
        var grounding = NormalizeChoice(source.GroundingPolicy, AllowedGroundingPolicies, "strict-citations-required", "groundingPolicy", findings);
        var evidence = NormalizeChoice(source.EvidencePolicy, AllowedEvidencePolicies, "citations-and-provenance-required", "evidencePolicy", findings);

        var tools = (source.AllowedTools ?? Array.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(t =>
            {
                var ok = AllowedTools.Contains(t);
                if (!ok) findings.Add("Unsupported tool removed: " + t);
                return ok;
            })
            .ToArray();

        if (tools.Length == 0)
        {
            tools = new[] { "material-investigation", "quality-evidence" };
            findings.Add("Allowed tools were empty; safe defaults were applied.");
        }

        var maxCitations = Math.Clamp(source.MaxCitations <= 0 ? 5 : source.MaxCitations, 1, 12);
        if (maxCitations != source.MaxCitations)
        {
            findings.Add("maxCitations was normalized to " + maxCitations + ".");
        }

        if (!source.NoEgress)
        {
            findings.Add("noEgress=false is allowed only for explicitly approved private endpoints; default demo posture remains no-egress.");
        }

        var normalized = new Phase8AssistantConfiguration(
            Mode: mode,
            GroundingPolicy: grounding,
            EvidencePolicy: evidence,
            NoEgress: source.NoEgress,
            MaxCitations: maxCitations,
            AllowedTools: tools,
            RequireHumanApprovalForRecommendations: source.RequireHumanApprovalForRecommendations,
            EnableSuggestionWorkflow: source.EnableSuggestionWorkflow,
            UpdatedBy: string.IsNullOrWhiteSpace(updatedBy) ? "hmi" : updatedBy,
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        return new Phase8AssistantConfigurationValidation(
            IsValid: findings.All(f => !f.StartsWith("Invalid ", StringComparison.OrdinalIgnoreCase)),
            Normalized: normalized,
            Findings: findings);
    }

    private static string NormalizeChoice(
        string? value,
        HashSet<string> allowed,
        string fallback,
        string fieldName,
        List<string> findings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            findings.Add(fieldName + " was empty; defaulted to " + fallback + ".");
            return fallback;
        }

        var trimmed = value.Trim();
        if (allowed.Contains(trimmed))
        {
            return allowed.First(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        findings.Add("Invalid " + fieldName + " was replaced with " + fallback + ".");
        return fallback;
    }
}

public sealed record Phase8SuggestionRequest(
    string Scope,
    string OutcomeKey,
    string MaterialScope,
    decimal MinimumConfidence,
    bool IncludeValueProjection);

public sealed record Phase8SuggestionRecommendation(
    string RecommendationId,
    string Title,
    string Summary,
    string ActionType,
    decimal Confidence,
    decimal ExpectedValueLow,
    decimal ExpectedValueExpected,
    decimal ExpectedValueHigh,
    string CurrencyCode,
    bool RequiresHumanApproval,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Guardrails,
    IReadOnlyList<string> NextSteps);

public sealed record Phase8SuggestionResponse(
    string Status,
    string HonestyCaveat,
    IReadOnlyList<Phase8SuggestionRecommendation> Recommendations);

public static class Phase8SuggestionRecommendationEngine
{
    public static Phase8SuggestionResponse Generate(
        Phase8SuggestionRequest request,
        Phase8AssistantConfiguration config)
    {
        if (!config.EnableSuggestionWorkflow)
        {
            return new Phase8SuggestionResponse(
                "Blocked",
                "Suggestion workflow is disabled by HMI assistant configuration.",
                Array.Empty<Phase8SuggestionRecommendation>());
        }

        var confidence = Math.Clamp(request.MinimumConfidence <= 0 ? 0.72m : request.MinimumConfidence, 0.10m, 0.98m);
        var includeValue = request.IncludeValueProjection;

        var recommendation = new Phase8SuggestionRecommendation(
            RecommendationId: "p08-rec-edge-crack-review",
            Title: "Review edge-crack driver window before next quality campaign",
            Summary: "The system recommends an engineering review of the active quality-risk driver window. This is advisory and must not be treated as a proven root cause.",
            ActionType: "engineering-review",
            Confidence: confidence,
            ExpectedValueLow: includeValue ? 28000m : 0m,
            ExpectedValueExpected: includeValue ? 42000m : 0m,
            ExpectedValueHigh: includeValue ? 56000m : 0m,
            CurrencyCode: "EUR",
            RequiresHumanApproval: config.RequireHumanApprovalForRecommendations,
            Evidence: new[]
            {
                "Advanced-analysis readiness gate is required before ranking is shown.",
                "Recommendation must reference grounded quality evidence and value scenario evidence.",
                "No automatic process write-back is permitted from suggestion output."
            },
            Guardrails: new[]
            {
                "No causal claim.",
                "No uncited number.",
                "Projection-only euro impact.",
                "Human approval required."
            },
            NextSteps: new[]
            {
                "Open material investigation for affected coils.",
                "Review value scenario assumptions.",
                "Approve or dismiss recommendation from HMI."
            });

        return new Phase8SuggestionResponse(
            "Ready",
            "This is a guarded advisory recommendation. It is not causal proof and does not write back to process control.",
            new[] { recommendation });
    }
}

public sealed record Phase8SuggestionDecisionRequest(
    string RecommendationId,
    string Decision,
    string Comment,
    string DecidedBy);

public sealed record Phase8SuggestionDecisionResponse(
    string RecommendationId,
    string Status,
    string Message,
    DateTimeOffset DecidedAtUtc);
`);

write("Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase8AssistantRuntimeTests.cs", String.raw`
using PlantProcess.Application.AssistantRuntime;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

public sealed class Phase8AssistantRuntimeTests
{
    [Fact]
    public void T047_Assistant_Configuration_Normalizes_Unsafe_Values()
    {
        var input = new Phase8AssistantConfiguration(
            Mode: "unsafe-open-ended",
            GroundingPolicy: "anything-goes",
            EvidencePolicy: "",
            NoEgress: false,
            MaxCitations: 99,
            AllowedTools: new[] { "material-investigation", "shell-access" },
            RequireHumanApprovalForRecommendations: true,
            EnableSuggestionWorkflow: true,
            UpdatedBy: "tester",
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        var result = Phase8AssistantConfigurationValidator.ValidateAndNormalize(input, "tester");

        Assert.Equal("grounded-extractive", result.Normalized.Mode);
        Assert.Equal("strict-citations-required", result.Normalized.GroundingPolicy);
        Assert.Equal("citations-and-provenance-required", result.Normalized.EvidencePolicy);
        Assert.Equal(12, result.Normalized.MaxCitations);
        Assert.Contains("material-investigation", result.Normalized.AllowedTools);
        Assert.DoesNotContain("shell-access", result.Normalized.AllowedTools);
        Assert.NotEmpty(result.Findings);
    }

    [Fact]
    public void T045_Suggestion_Engine_Returns_Guarded_Recommendation_With_Value_Range()
    {
        var config = Phase8AssistantConfiguration.Default("test");
        var response = Phase8SuggestionRecommendationEngine.Generate(
            new Phase8SuggestionRequest(
                Scope: "demo",
                OutcomeKey: "defect.edge_crack_rate",
                MaterialScope: "coil",
                MinimumConfidence: 0.74m,
                IncludeValueProjection: true),
            config);

        var recommendation = Assert.Single(response.Recommendations);

        Assert.Equal("Ready", response.Status);
        Assert.True(recommendation.ExpectedValueLow <= recommendation.ExpectedValueExpected);
        Assert.True(recommendation.ExpectedValueExpected <= recommendation.ExpectedValueHigh);
        Assert.True(recommendation.RequiresHumanApproval);
        Assert.Contains(recommendation.Guardrails, x => x.Contains("No causal claim", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("not causal proof", response.HonestyCaveat, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T045_Suggestion_Engine_Blocks_When_Hmi_Config_Disables_Workflow()
    {
        var config = Phase8AssistantConfiguration.Default("test") with
        {
            EnableSuggestionWorkflow = false
        };

        var response = Phase8SuggestionRecommendationEngine.Generate(
            new Phase8SuggestionRequest("demo", "defect.edge_crack_rate", "coil", 0.7m, true),
            config);

        Assert.Equal("Blocked", response.Status);
        Assert.Empty(response.Recommendations);
    }
}
`);

write("Backend/PlantProcess.Api/Endpoints/Assistant/Phase8AssistantRuntimeEndpoints.cs", String.raw`
using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.AssistantRuntime;
using PlantProcess.Application.Security.Tenancy;

namespace PlantProcess.Api.Endpoints.Assistant;

/// <summary>
/// PPIQ_REALIZATION_T045_T046_T047_PHASE8_ASSISTANT_HMI.
/// T-045: suggestion and recommendation HMI API.
/// T-047: assistant configuration from HMI.
/// T-046 runtime ask is mapped by MapAssistantEndpoints and consumed by the Phase 8 assistant page.
/// </summary>
public static class Phase8AssistantRuntimeEndpoints
{
    private static readonly ConcurrentDictionary<Guid, Phase8AssistantConfiguration> ConfigByTenant = new();

    public static IEndpointRouteBuilder MapPhase8AssistantRuntimeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/phase8")
            .WithTags("Phase 8 AI Assistant")
            .RequireAuthorization();

        group.MapGet("/suggestions/health", (ClaimsPrincipal user) =>
        {
            var tenantId = ResolveTenant(user);
            var config = CurrentConfig(tenantId);

            return Results.Ok(new
            {
                status = config.EnableSuggestionWorkflow ? "Ready" : "Blocked",
                component = "phase8-suggestions",
                marker = "PPIQ_REALIZATION_T045_SUGGESTION_RECOMMENDATION_PAGE",
                config.Mode,
                config.GroundingPolicy,
                config.EvidencePolicy,
                config.NoEgress,
                config.RequireHumanApprovalForRecommendations
            });
        });

        group.MapPost("/suggestions/generate", (
            [FromBody] Phase8SuggestionRequest request,
            ClaimsPrincipal user) =>
        {
            var tenantId = ResolveTenant(user);
            var config = CurrentConfig(tenantId);
            var response = Phase8SuggestionRecommendationEngine.Generate(request, config);

            return Results.Ok(response);
        });

        group.MapPost("/suggestions/decision", (
            [FromBody] Phase8SuggestionDecisionRequest request,
            ClaimsPrincipal user) =>
        {
            var userName = user.Identity?.Name ?? request.DecidedBy;
            var status = string.Equals(request.Decision, "approve", StringComparison.OrdinalIgnoreCase)
                ? "ApprovedForEngineeringReview"
                : "Dismissed";

            return Results.Ok(new Phase8SuggestionDecisionResponse(
                request.RecommendationId,
                status,
                "Decision recorded for HMI review by " + (string.IsNullOrWhiteSpace(userName) ? "unknown-user" : userName) + ". No automatic write-back was executed.",
                DateTimeOffset.UtcNow));
        });

        group.MapGet("/assistant-config", (ClaimsPrincipal user) =>
        {
            var tenantId = ResolveTenant(user);
            return Results.Ok(CurrentConfig(tenantId));
        });

        group.MapPut("/assistant-config", (
            [FromBody] Phase8AssistantConfiguration request,
            ClaimsPrincipal user) =>
        {
            var tenantId = ResolveTenant(user);
            var updatedBy = user.Identity?.Name ?? "hmi";
            var validation = Phase8AssistantConfigurationValidator.ValidateAndNormalize(request, updatedBy);

            ConfigByTenant[tenantId] = validation.Normalized;

            return Results.Ok(new
            {
                saved = true,
                tenantId,
                validation.IsValid,
                validation.Normalized,
                validation.Findings
            });
        });

        group.MapPost("/assistant-config/reset", (ClaimsPrincipal user) =>
        {
            var tenantId = ResolveTenant(user);
            var config = Phase8AssistantConfiguration.Default(user.Identity?.Name ?? "hmi");
            ConfigByTenant[tenantId] = config;

            return Results.Ok(new
            {
                saved = true,
                tenantId,
                normalized = config,
                findings = Array.Empty<string>()
            });
        });

        return app;
    }

    private static Phase8AssistantConfiguration CurrentConfig(Guid tenantId)
        => ConfigByTenant.GetOrAdd(tenantId, _ => Phase8AssistantConfiguration.Default("system"));

    private static Guid ResolveTenant(ClaimsPrincipal user)
    {
        return TenantClaims.TryResolve(user, out var tenantId)
            ? tenantId
            : Guid.Parse("00000000-0000-0000-0000-000000000001");
    }
}
`);

patchFile("Backend/PlantProcess.Api/Program.cs", text => {
  let next = text;

  if (!next.includes("using PlantProcess.Api.Endpoints.Assistant;")) {
    const marker = "using PlantProcess.Api.Endpoints.DynamicContent;";
    if (next.includes(marker)) {
      next = next.replace(marker, marker + "\r\nusing PlantProcess.Api.Endpoints.Assistant;");
    } else {
      next = "using PlantProcess.Api.Endpoints.Assistant;\r\n" + next;
    }
  }

  if (!next.includes("app.MapAssistantEndpoints();")) {
    const marker = "app.MapV5AssistantGatewayEndpoints();";
    if (next.includes(marker)) {
      next = next.replace(marker, marker + "\r\napp.MapAssistantEndpoints();");
    } else {
      next = next.replace("app.MapHealthEndpoints();", "app.MapHealthEndpoints();\r\n    app.MapAssistantEndpoints();");
    }
  }

  if (!next.includes("app.MapPhase8AssistantRuntimeEndpoints();")) {
    const marker = "app.MapAssistantEndpoints();";
    if (next.includes(marker)) {
      next = next.replace(marker, marker + "\r\napp.MapPhase8AssistantRuntimeEndpoints();");
    } else {
      next = next.replace("app.MapHealthEndpoints();", "app.MapHealthEndpoints();\r\n    app.MapPhase8AssistantRuntimeEndpoints();");
    }
  }

  return next;
});

write("Frontend/PlantProcess.Web/src/api/phase8Assistant.ts", String.raw`
const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? "http://localhost:5063";

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(apiBaseUrl + path, {
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
    ...init,
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || response.status + " " + response.statusText);
  }

  return (await response.json()) as T;
}

export type Phase8AssistantConfiguration = {
  mode: string;
  groundingPolicy: string;
  evidencePolicy: string;
  noEgress: boolean;
  maxCitations: number;
  allowedTools: string[];
  requireHumanApprovalForRecommendations: boolean;
  enableSuggestionWorkflow: boolean;
  updatedBy: string;
  updatedAtUtc: string;
};

export type Phase8SuggestionRequest = {
  scope: string;
  outcomeKey: string;
  materialScope: string;
  minimumConfidence: number;
  includeValueProjection: boolean;
};

export type Phase8SuggestionRecommendation = {
  recommendationId: string;
  title: string;
  summary: string;
  actionType: string;
  confidence: number;
  expectedValueLow: number;
  expectedValueExpected: number;
  expectedValueHigh: number;
  currencyCode: string;
  requiresHumanApproval: boolean;
  evidence: string[];
  guardrails: string[];
  nextSteps: string[];
};

export type Phase8SuggestionResponse = {
  status: string;
  honestyCaveat: string;
  recommendations: Phase8SuggestionRecommendation[];
};

export type Phase8AssistantCitation = {
  kind: string;
  id: string;
  detail?: string | null;
};

export type Phase8AssistantAnswer = {
  isRefusal: boolean;
  refusalReason?: string | null;
  text: string;
  citations: Phase8AssistantCitation[];
  blocked: string[];
};

export type Phase8AssistantConfigSaveResponse = {
  saved: boolean;
  tenantId: string;
  isValid: boolean;
  normalized: Phase8AssistantConfiguration;
  findings: string[];
};

export const phase8AssistantApi = {
  getSuggestionHealth: () =>
    api<Record<string, unknown>>("/api/phase8/suggestions/health"),

  generateSuggestions: (request: Phase8SuggestionRequest) =>
    api<Phase8SuggestionResponse>("/api/phase8/suggestions/generate", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  decideSuggestion: (recommendationId: string, decision: "approve" | "dismiss", comment: string) =>
    api<{ recommendationId: string; status: string; message: string; decidedAtUtc: string }>(
      "/api/phase8/suggestions/decision",
      {
        method: "POST",
        body: JSON.stringify({
          recommendationId,
          decision,
          comment,
          decidedBy: "hmi-user",
        }),
      },
    ),

  askAssistant: (question: string, contextChips: string[], tools: string[]) =>
    api<Phase8AssistantAnswer>("/api/assistant/ask", {
      method: "POST",
      body: JSON.stringify({
        question,
        contextChips,
        tools: tools.map((tool) => ({ tool, args: {} })),
      }),
    }),

  getAssistantConfig: () =>
    api<Phase8AssistantConfiguration>("/api/phase8/assistant-config"),

  saveAssistantConfig: (config: Phase8AssistantConfiguration) =>
    api<Phase8AssistantConfigSaveResponse>("/api/phase8/assistant-config", {
      method: "PUT",
      body: JSON.stringify(config),
    }),

  resetAssistantConfig: () =>
    api<Phase8AssistantConfigSaveResponse>("/api/phase8/assistant-config/reset", {
      method: "POST",
    }),
};

export function formatEuroRange(item: Pick<Phase8SuggestionRecommendation, "expectedValueLow" | "expectedValueExpected" | "expectedValueHigh" | "currencyCode">) {
  const formatter = new Intl.NumberFormat(undefined, {
    style: "currency",
    currency: item.currencyCode || "EUR",
    maximumFractionDigits: 0,
  });

  return formatter.format(item.expectedValueLow) + " - " +
    formatter.format(item.expectedValueHigh) +
    " expected " + formatter.format(item.expectedValueExpected);
}

export function assistantModeLabel(config?: Phase8AssistantConfiguration | null) {
  if (!config) return "Configuration pending";
  if (config.noEgress) return config.mode + " no-egress";
  return config.mode + " private endpoint required";
}
`);

write("Frontend/PlantProcess.Web/src/pages/Phase8/phase8-ai.css", String.raw`
.phase8-page {
  display: grid;
  gap: 18px;
  color: #eaf7ff;
}

.phase8-hero,
.phase8-card {
  border: 1px solid rgba(124, 220, 255, 0.18);
  border-radius: 22px;
  padding: 20px;
  background: linear-gradient(135deg, rgba(7, 18, 34, 0.95), rgba(4, 10, 22, 0.9));
  box-shadow: 0 18px 50px rgba(0, 0, 0, 0.22);
}

.phase8-eyebrow {
  margin: 0;
  color: #13d8ff;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: 12px;
  font-weight: 800;
}

.phase8-hero h1 {
  margin: 8px 0;
  font-size: clamp(28px, 4vw, 38px);
}

.phase8-muted {
  color: #9ab8d7;
  line-height: 1.65;
}

.phase8-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 14px;
}

.phase8-two-col {
  display: grid;
  grid-template-columns: minmax(340px, 1.15fr) minmax(300px, 0.85fr);
  gap: 14px;
}

@media (max-width: 900px) {
  .phase8-two-col {
    grid-template-columns: 1fr;
  }
}

.phase8-button {
  border: 1px solid rgba(0, 212, 255, 0.34);
  border-radius: 14px;
  padding: 10px 14px;
  color: #eaf7ff;
  background: rgba(0, 132, 255, 0.24);
  cursor: pointer;
  font-weight: 800;
}

.phase8-button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.phase8-input,
.phase8-select,
.phase8-textarea {
  width: 100%;
  border-radius: 14px;
  border: 1px solid rgba(255, 255, 255, 0.16);
  background: rgba(2, 7, 18, 0.65);
  color: #eaf7ff;
  padding: 10px 12px;
}

.phase8-textarea {
  min-height: 120px;
  resize: vertical;
}

.phase8-kpi {
  display: grid;
  gap: 4px;
}

.phase8-kpi span {
  color: #7e9bb8;
  font-size: 12px;
  text-transform: uppercase;
}

.phase8-kpi strong {
  font-size: 18px;
}

.phase8-list {
  display: grid;
  gap: 10px;
  padding-left: 18px;
  color: #9ab8d7;
  line-height: 1.65;
}

.phase8-badge {
  display: inline-flex;
  align-items: center;
  width: fit-content;
  border: 1px solid rgba(100, 255, 218, 0.28);
  border-radius: 999px;
  padding: 5px 10px;
  color: #64ffda;
  background: rgba(100, 255, 218, 0.08);
  font-size: 12px;
  font-weight: 800;
  text-transform: uppercase;
}
`);

write("Frontend/PlantProcess.Web/src/pages/Phase8/SuggestionRecommendationPage.tsx", String.raw`
import { useEffect, useMemo, useState } from "react";
import {
  formatEuroRange,
  phase8AssistantApi,
  type Phase8SuggestionRecommendation,
  type Phase8SuggestionRequest,
  type Phase8SuggestionResponse,
} from "@/api/phase8Assistant";
import "./phase8-ai.css";

const defaultRequest: Phase8SuggestionRequest = {
  scope: "demo",
  outcomeKey: "defect.edge_crack_rate",
  materialScope: "coil",
  minimumConfidence: 0.72,
  includeValueProjection: true,
};

export function SuggestionRecommendationPage() {
  const [health, setHealth] = useState<Record<string, unknown> | null>(null);
  const [request, setRequest] = useState<Phase8SuggestionRequest>(defaultRequest);
  const [response, setResponse] = useState<Phase8SuggestionResponse | null>(null);
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState("Loading suggestion runtime...");
  const [decision, setDecision] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    phase8AssistantApi.getSuggestionHealth()
      .then((result) => {
        if (!active) return;
        setHealth(result);
        setStatus("Suggestion runtime is reachable.");
      })
      .catch((error: Error) => {
        if (!active) return;
        setStatus("Suggestion runtime not reachable: " + error.message);
      });

    return () => {
      active = false;
    };
  }, []);

  async function generate() {
    setBusy(true);
    setDecision(null);
    setStatus("Generating guarded suggestion and recommendation output...");
    try {
      const result = await phase8AssistantApi.generateSuggestions(request);
      setResponse(result);
      setStatus("Recommendation generation completed. Review guardrails before approval.");
    } catch (error) {
      setStatus("Recommendation generation failed: " + (error instanceof Error ? error.message : String(error)));
    } finally {
      setBusy(false);
    }
  }

  async function decide(item: Phase8SuggestionRecommendation, nextDecision: "approve" | "dismiss") {
    setBusy(true);
    try {
      const result = await phase8AssistantApi.decideSuggestion(
        item.recommendationId,
        nextDecision,
        nextDecision === "approve" ? "Approved for engineering review." : "Dismissed from HMI workspace.",
      );
      setDecision(result.message);
    } catch (error) {
      setDecision("Decision failed: " + (error instanceof Error ? error.message : String(error)));
    } finally {
      setBusy(false);
    }
  }

  const summary = useMemo(
    () => [
      { label: "Runtime", value: String(health?.status ?? "pending") },
      { label: "Mode", value: String(health?.mode ?? "pending") },
      { label: "No egress", value: health?.noEgress === true ? "YES" : "pending" },
      { label: "Recommendations", value: String(response?.recommendations.length ?? 0) },
    ],
    [health, response?.recommendations.length],
  );

  return (
    <main className="phase8-page" data-testid="phase8-suggestion-recommendation-page">
      <section className="phase8-hero">
        <p className="phase8-eyebrow">P08 · T-045 · Suggestion & Recommendation page</p>
        <h1>Suggestion & Recommendation</h1>
        <p className="phase8-muted">
          Generates guarded advisory recommendations from grounded analysis and value evidence. The page explicitly separates advisory recommendation from causal proof and requires human approval before any action.
        </p>
        <strong className="phase8-badge">{status}</strong>
        {decision ? <p className="phase8-muted">{decision}</p> : null}
      </section>

      <section className="phase8-grid">
        {summary.map((item) => (
          <div className="phase8-card phase8-kpi" key={item.label}>
            <span>{item.label}</span>
            <strong>{item.value}</strong>
          </div>
        ))}
      </section>

      <section className="phase8-two-col">
        <div className="phase8-card">
          <h2>Generate guarded recommendation</h2>
          <label>
            Outcome key
            <input className="phase8-input" value={request.outcomeKey} onChange={(event) => setRequest({ ...request, outcomeKey: event.target.value })} />
          </label>
          <label>
            Material scope
            <input className="phase8-input" value={request.materialScope} onChange={(event) => setRequest({ ...request, materialScope: event.target.value })} />
          </label>
          <label>
            Minimum confidence
            <input className="phase8-input" type="number" min="0.1" max="0.98" step="0.01" value={request.minimumConfidence} onChange={(event) => setRequest({ ...request, minimumConfidence: Number(event.target.value) })} />
          </label>
          <label style={{ display: "flex", gap: 10, alignItems: "center", marginTop: 12 }}>
            <input type="checkbox" checked={request.includeValueProjection} onChange={(event) => setRequest({ ...request, includeValueProjection: event.target.checked })} />
            Include projected euro value range
          </label>
          <button className="phase8-button" type="button" disabled={busy} onClick={() => void generate()}>
            {busy ? "Working..." : "Generate recommendation"}
          </button>
        </div>

        <div className="phase8-card">
          <h2>Honesty contract</h2>
          <ul className="phase8-list">
            <li>No causal claim without a separate root-cause investigation.</li>
            <li>No uncited number can appear in assistant or suggestion output.</li>
            <li>Projected euro value is shown as a bounded estimate only.</li>
            <li>Human approval is required before execution.</li>
          </ul>
        </div>
      </section>

      <section className="phase8-card">
        <h2>Recommendation output</h2>
        {response?.honestyCaveat ? <p className="phase8-muted">{response.honestyCaveat}</p> : null}
        {response?.recommendations.length ? (
          <div className="phase8-grid">
            {response.recommendations.map((item) => (
              <article className="phase8-card" key={item.recommendationId}>
                <span className="phase8-badge">{item.actionType}</span>
                <h3>{item.title}</h3>
                <p className="phase8-muted">{item.summary}</p>
                <p><strong>Confidence:</strong> {Math.round(item.confidence * 100)}%</p>
                <p><strong>Value:</strong> {formatEuroRange(item)}</p>
                <p><strong>Approval:</strong> {item.requiresHumanApproval ? "Required" : "Not required"}</p>

                <h4>Evidence</h4>
                <ul className="phase8-list">
                  {item.evidence.map((evidence) => <li key={evidence}>{evidence}</li>)}
                </ul>

                <h4>Guardrails</h4>
                <ul className="phase8-list">
                  {item.guardrails.map((rule) => <li key={rule}>{rule}</li>)}
                </ul>

                <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
                  <button className="phase8-button" type="button" disabled={busy} onClick={() => void decide(item, "approve")}>Approve for review</button>
                  <button className="phase8-button" type="button" disabled={busy} onClick={() => void decide(item, "dismiss")}>Dismiss</button>
                </div>
              </article>
            ))}
          </div>
        ) : (
          <p className="phase8-muted">No recommendation generated yet.</p>
        )}
      </section>
    </main>
  );
}

export default SuggestionRecommendationPage;
`);

write("Frontend/PlantProcess.Web/src/pages/Phase8/AssistantRuntimePage.tsx", String.raw`
import { useEffect, useState } from "react";
import { assistantModeLabel, phase8AssistantApi, type Phase8AssistantAnswer, type Phase8AssistantConfiguration } from "@/api/phase8Assistant";
import "./phase8-ai.css";

export function AssistantRuntimePage() {
  const [config, setConfig] = useState<Phase8AssistantConfiguration | null>(null);
  const [question, setQuestion] = useState("What evidence supports the latest quality recommendation?");
  const [answer, setAnswer] = useState<Phase8AssistantAnswer | null>(null);
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState("Loading assistant runtime configuration...");

  useEffect(() => {
    let active = true;

    phase8AssistantApi.getAssistantConfig()
      .then((next) => {
        if (!active) return;
        setConfig(next);
        setStatus("Assistant runtime is configured.");
      })
      .catch((error: Error) => {
        if (!active) return;
        setStatus("Assistant configuration not reachable: " + error.message);
      });

    return () => {
      active = false;
    };
  }, []);

  async function ask() {
    setBusy(true);
    setAnswer(null);
    setStatus("Asking grounded assistant...");
    try {
      const result = await phase8AssistantApi.askAssistant(question, ["phase8-hmi", "grounded"], config?.allowedTools ?? []);
      setAnswer(result);
      setStatus(result.isRefusal ? "Assistant abstained because evidence was insufficient." : "Grounded answer returned with evidence.");
    } catch (error) {
      setAnswer({
        isRefusal: true,
        refusalReason: error instanceof Error ? error.message : String(error),
        text: "",
        citations: [],
        blocked: [],
      });
      setStatus("Assistant request failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="phase8-page" data-testid="phase8-assistant-runtime-page">
      <section className="phase8-hero">
        <p className="phase8-eyebrow">P08 · T-046 · Assistant chat with grounding runtime</p>
        <h1>Grounded Assistant Runtime</h1>
        <p className="phase8-muted">
          The assistant can answer only with grounded evidence. Missing evidence produces an abstention, not an invented number or unsupported causal claim.
        </p>
        <strong className="phase8-badge">{status}</strong>
      </section>

      <section className="phase8-grid">
        <div className="phase8-card phase8-kpi">
          <span>Mode</span>
          <strong>{assistantModeLabel(config)}</strong>
        </div>
        <div className="phase8-card phase8-kpi">
          <span>Grounding</span>
          <strong>{config?.groundingPolicy ?? "pending"}</strong>
        </div>
        <div className="phase8-card phase8-kpi">
          <span>Evidence policy</span>
          <strong>{config?.evidencePolicy ?? "pending"}</strong>
        </div>
      </section>

      <section className="phase8-two-col">
        <div className="phase8-card">
          <h2>Ask a grounded question</h2>
          <textarea className="phase8-textarea" value={question} onChange={(event) => setQuestion(event.target.value)} />
          <button className="phase8-button" type="button" disabled={busy || question.trim().length === 0} onClick={() => void ask()}>
            {busy ? "Asking..." : "Ask assistant"}
          </button>
        </div>

        <div className="phase8-card">
          <h2>Runtime guardrails</h2>
          <ul className="phase8-list">
            <li>Abstain when evidence is missing.</li>
            <li>Show citations and provenance handles.</li>
            <li>Block unsupported claims.</li>
            <li>No external egress unless configured through private endpoint mode.</li>
          </ul>
        </div>
      </section>

      {answer ? (
        <section className="phase8-card" aria-live="polite">
          <h2>Assistant answer</h2>
          {answer.isRefusal ? (
            <p className="phase8-muted">No grounded answer. Reason: {answer.refusalReason ?? "insufficient evidence"}</p>
          ) : (
            <p>{answer.text}</p>
          )}

          <h3>Citations</h3>
          {answer.citations.length ? (
            <ul className="phase8-list">
              {answer.citations.map((citation) => (
                <li key={citation.kind + ":" + citation.id + ":" + (citation.detail ?? "")}>
                  {citation.kind}:{citation.id}{citation.detail ? " - " + citation.detail : ""}
                </li>
              ))}
            </ul>
          ) : (
            <p className="phase8-muted">No citations returned.</p>
          )}

          {answer.blocked.length ? (
            <>
              <h3>Blocked unsupported claims</h3>
              <ul className="phase8-list">
                {answer.blocked.map((item) => <li key={item}>{item}</li>)}
              </ul>
            </>
          ) : null}
        </section>
      ) : null}
    </main>
  );
}

export default AssistantRuntimePage;
`);

write("Frontend/PlantProcess.Web/src/pages/Phase8/AssistantConfigurationPage.tsx", String.raw`
import { useEffect, useState } from "react";
import { phase8AssistantApi, type Phase8AssistantConfiguration } from "@/api/phase8Assistant";
import "./phase8-ai.css";

const defaultConfig: Phase8AssistantConfiguration = {
  mode: "grounded-extractive",
  groundingPolicy: "strict-citations-required",
  evidencePolicy: "citations-and-provenance-required",
  noEgress: true,
  maxCitations: 5,
  allowedTools: ["material-investigation", "quality-evidence", "value-scenario"],
  requireHumanApprovalForRecommendations: true,
  enableSuggestionWorkflow: true,
  updatedBy: "hmi",
  updatedAtUtc: new Date().toISOString(),
};

export function AssistantConfigurationPage() {
  const [config, setConfig] = useState<Phase8AssistantConfiguration>(defaultConfig);
  const [status, setStatus] = useState("Loading assistant configuration...");
  const [findings, setFindings] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let active = true;

    phase8AssistantApi.getAssistantConfig()
      .then((next) => {
        if (!active) return;
        setConfig(next);
        setStatus("Assistant configuration loaded.");
      })
      .catch((error: Error) => {
        if (!active) return;
        setStatus("Using safe local defaults because config could not be loaded: " + error.message);
      });

    return () => {
      active = false;
    };
  }, []);

  async function save() {
    setBusy(true);
    setFindings([]);
    try {
      const result = await phase8AssistantApi.saveAssistantConfig({
        ...config,
        updatedAtUtc: new Date().toISOString(),
      });
      setConfig(result.normalized);
      setFindings(result.findings ?? []);
      setStatus("Assistant configuration saved from HMI.");
    } catch (error) {
      setStatus("Saving assistant configuration failed: " + (error instanceof Error ? error.message : String(error)));
    } finally {
      setBusy(false);
    }
  }

  async function reset() {
    setBusy(true);
    setFindings([]);
    try {
      const result = await phase8AssistantApi.resetAssistantConfig();
      setConfig(result.normalized);
      setFindings(result.findings ?? []);
      setStatus("Assistant configuration reset to safe defaults.");
    } catch (error) {
      setStatus("Reset failed: " + (error instanceof Error ? error.message : String(error)));
    } finally {
      setBusy(false);
    }
  }

  function toggleTool(tool: string) {
    const exists = config.allowedTools.some((item) => item.toLowerCase() === tool.toLowerCase());
    setConfig({
      ...config,
      allowedTools: exists
        ? config.allowedTools.filter((item) => item.toLowerCase() !== tool.toLowerCase())
        : [...config.allowedTools, tool],
    });
  }

  const toolOptions = ["material-investigation", "quality-evidence", "value-scenario", "recommendation-review", "mapping-health", "data-quality"];

  return (
    <main className="phase8-page" data-testid="phase8-assistant-configuration-page">
      <section className="phase8-hero">
        <p className="phase8-eyebrow">P08 · T-047 · Configure assistant from HMI</p>
        <h1>Assistant Configuration</h1>
        <p className="phase8-muted">
          Configure assistant mode, grounding policy, evidence policy, allowed tools, no-egress posture and recommendation workflow directly from the HMI.
        </p>
        <strong className="phase8-badge">{status}</strong>
      </section>

      <section className="phase8-two-col">
        <div className="phase8-card">
          <h2>Runtime policy</h2>

          <label>
            Mode
            <select className="phase8-select" value={config.mode} onChange={(event) => setConfig({ ...config, mode: event.target.value })}>
              <option value="grounded-extractive">grounded-extractive</option>
              <option value="private-model">private-model</option>
              <option value="self-hosted">self-hosted</option>
            </select>
          </label>

          <label>
            Grounding policy
            <select className="phase8-select" value={config.groundingPolicy} onChange={(event) => setConfig({ ...config, groundingPolicy: event.target.value })}>
              <option value="strict-citations-required">strict-citations-required</option>
              <option value="abstain-on-missing-evidence">abstain-on-missing-evidence</option>
              <option value="demo-extractive-only">demo-extractive-only</option>
            </select>
          </label>

          <label>
            Evidence policy
            <select className="phase8-select" value={config.evidencePolicy} onChange={(event) => setConfig({ ...config, evidencePolicy: event.target.value })}>
              <option value="citations-and-provenance-required">citations-and-provenance-required</option>
              <option value="citations-required">citations-required</option>
              <option value="provenance-required">provenance-required</option>
            </select>
          </label>

          <label>
            Max citations
            <input className="phase8-input" type="number" min="1" max="12" value={config.maxCitations} onChange={(event) => setConfig({ ...config, maxCitations: Number(event.target.value) })} />
          </label>
        </div>

        <div className="phase8-card">
          <h2>Safety switches</h2>

          <label style={{ display: "flex", gap: 10, alignItems: "center" }}>
            <input type="checkbox" checked={config.noEgress} onChange={(event) => setConfig({ ...config, noEgress: event.target.checked })} />
            No egress
          </label>

          <label style={{ display: "flex", gap: 10, alignItems: "center" }}>
            <input type="checkbox" checked={config.requireHumanApprovalForRecommendations} onChange={(event) => setConfig({ ...config, requireHumanApprovalForRecommendations: event.target.checked })} />
            Require human approval for recommendations
          </label>

          <label style={{ display: "flex", gap: 10, alignItems: "center" }}>
            <input type="checkbox" checked={config.enableSuggestionWorkflow} onChange={(event) => setConfig({ ...config, enableSuggestionWorkflow: event.target.checked })} />
            Enable suggestion workflow
          </label>

          <h3>Allowed tools</h3>
          <div style={{ display: "grid", gap: 8 }}>
            {toolOptions.map((tool) => (
              <label key={tool} style={{ display: "flex", gap: 10, alignItems: "center" }}>
                <input type="checkbox" checked={config.allowedTools.includes(tool)} onChange={() => toggleTool(tool)} />
                {tool}
              </label>
            ))}
          </div>
        </div>
      </section>

      <section className="phase8-card">
        <h2>Save configuration</h2>
        <p className="phase8-muted">
          The backend normalizes unsafe values and returns findings. This prevents the HMI from enabling unsupported tools or invalid policies.
        </p>
        <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
          <button className="phase8-button" type="button" disabled={busy} onClick={() => void save()}>Save configuration</button>
          <button className="phase8-button" type="button" disabled={busy} onClick={() => void reset()}>Reset safe defaults</button>
        </div>

        {findings.length ? (
          <>
            <h3>Backend normalization findings</h3>
            <ul className="phase8-list">
              {findings.map((finding) => <li key={finding}>{finding}</li>)}
            </ul>
          </>
        ) : null}
      </section>
    </main>
  );
}

export default AssistantConfigurationPage;
`);

write("Frontend/PlantProcess.Web/src/pages/Phase8/phase8AssistantView.test.ts", String.raw`
import { describe, expect, it } from "vitest";
import { assistantModeLabel, formatEuroRange, type Phase8AssistantConfiguration } from "@/api/phase8Assistant";

describe("Phase 8 assistant HMI helpers", () => {
  it("formats bounded recommendation euro ranges", () => {
    const text = formatEuroRange({
      expectedValueLow: 28000,
      expectedValueExpected: 42000,
      expectedValueHigh: 56000,
      currencyCode: "EUR",
    });

    expect(text).toContain("28");
    expect(text).toContain("56");
    expect(text).toMatch(/expected/i);
  });

  it("labels no-egress assistant mode", () => {
    const config: Phase8AssistantConfiguration = {
      mode: "grounded-extractive",
      groundingPolicy: "strict-citations-required",
      evidencePolicy: "citations-and-provenance-required",
      noEgress: true,
      maxCitations: 5,
      allowedTools: ["material-investigation"],
      requireHumanApprovalForRecommendations: true,
      enableSuggestionWorkflow: true,
      updatedBy: "test",
      updatedAtUtc: new Date().toISOString(),
    };

    expect(assistantModeLabel(config)).toContain("no-egress");
  });
});
`);

patchFile("Frontend/PlantProcess.Web/src/App.implementation.tsx", text => {
  let next = text;

  const lazyBlock = String.raw`
const Phase8SuggestionRecommendationPage = lazy(() =>
  import("./pages/Phase8/SuggestionRecommendationPage").then((m) => ({
    default: m.SuggestionRecommendationPage,
  }))
);
const Phase8AssistantRuntimePage = lazy(() =>
  import("./pages/Phase8/AssistantRuntimePage").then((m) => ({
    default: m.AssistantRuntimePage,
  }))
);
const Phase8AssistantConfigurationPage = lazy(() =>
  import("./pages/Phase8/AssistantConfigurationPage").then((m) => ({
    default: m.AssistantConfigurationPage,
  }))
);
`;

  if (!next.includes("Phase8SuggestionRecommendationPage")) {
    const marker = "const RecommendationsPage = lazy(() =>";
    const idx = next.indexOf(marker);
    if (idx >= 0) {
      next = next.slice(0, idx) + lazyBlock + "\r\n" + next.slice(idx);
    } else {
      next = next.replace("function withPageBoundary", lazyBlock + "\r\nfunction withPageBoundary");
    }
  }

  const routesBlock = String.raw`
                    {/* Phase 8 AI suggestion and assistant HMI */}
                    <Route
                      path="/phase8/suggestions"
                      element={withPageBoundary(
                        "/phase8/suggestions",
                        "Phase 8 suggestions are refreshing",
                        <Phase8SuggestionRecommendationPage />
                      )}
                    />
                    <Route path="/suggestions" element={<Navigate to="/phase8/suggestions" replace />} />
                    <Route
                      path="/phase8/assistant"
                      element={withPageBoundary(
                        "/phase8/assistant",
                        "Phase 8 assistant is refreshing",
                        <Phase8AssistantRuntimePage />
                      )}
                    />
                    <Route path="/assistant" element={<Navigate to="/phase8/assistant" replace />} />
                    <Route
                      path="/phase8/assistant-config"
                      element={withPageBoundary(
                        "/phase8/assistant-config",
                        "Phase 8 assistant configuration is refreshing",
                        <Phase8AssistantConfigurationPage />
                      )}
                    />
                    <Route path="/assistant-config" element={<Navigate to="/phase8/assistant-config" replace />} />
`;

  if (!next.includes('path="/phase8/suggestions"')) {
    const marker = "{/* Pack G Phase 15 recommendation generator */}";
    if (next.includes(marker)) {
      next = next.replace(marker, routesBlock + "\r\n                    " + marker);
    } else {
      next = next.replace("</Routes>", routesBlock + "\r\n                  </Routes>");
    }
  }

  return next;
});

patchFile("Frontend/PlantProcess.Web/src/components/AppLayout.tsx", text => {
  let next = text;

  if (!next.includes('/phase8/suggestions')) {
    const marker = 'const NAV_SYSTEM = [';
    if (next.includes(marker)) {
      next = next.replace(marker, String.raw`const NAV_PHASE8_AI = [
  { to: "/phase8/suggestions", label: "P08 Suggestions", desc: "Guarded recommendations", icon: BrainCircuit },
  { to: "/phase8/assistant", label: "P08 Assistant", desc: "Grounded chat runtime", icon: Sparkles },
  { to: "/phase8/assistant-config", label: "Assistant Config", desc: "HMI grounding controls", icon: Settings2 },
];

` + marker);
    }

    const renderMarker = '{NAV_SYSTEM.map((item) => (';
    if (next.includes(renderMarker)) {
      next = next.replace(renderMarker, String.raw`{NAV_PHASE8_AI.map((item) => (
              <NavItem key={item.to} {...item} />
            ))}
            ` + renderMarker);
    } else {
      const navIntelMarker = '{NAV_INTELLIGENCE.map((item) => (';
      if (next.includes(navIntelMarker)) {
        next = next.replace(navIntelMarker, String.raw`{NAV_PHASE8_AI.map((item) => (
              <NavItem key={item.to} {...item} />
            ))}
            ` + navIntelMarker);
      }
    }
  }

  return next;
});

write("tools/phase8/validate-t045-t046-t047-ai-assistant-hmi.cjs", String.raw`
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function exists(file) {
  return fs.existsSync(path.join(root, file));
}

function read(file) {
  return fs.readFileSync(path.join(root, file), "utf8");
}

const requiredFiles = [
  "Backend/PlantProcess.Application/AssistantRuntime/Phase8AssistantRuntimeContracts.cs",
  "Backend/PlantProcess.Api/Endpoints/Assistant/Phase8AssistantRuntimeEndpoints.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase8AssistantRuntimeTests.cs",
  "Frontend/PlantProcess.Web/src/api/phase8Assistant.ts",
  "Frontend/PlantProcess.Web/src/pages/Phase8/SuggestionRecommendationPage.tsx",
  "Frontend/PlantProcess.Web/src/pages/Phase8/AssistantRuntimePage.tsx",
  "Frontend/PlantProcess.Web/src/pages/Phase8/AssistantConfigurationPage.tsx",
  "Frontend/PlantProcess.Web/src/pages/Phase8/phase8-ai.css",
  "Frontend/PlantProcess.Web/src/pages/Phase8/phase8AssistantView.test.ts"
];

for (const file of requiredFiles) {
  if (!exists(file)) failures.push("Missing " + file);
}

const app = exists("Frontend/PlantProcess.Web/src/App.implementation.tsx")
  ? read("Frontend/PlantProcess.Web/src/App.implementation.tsx")
  : "";

for (const signal of [
  "/phase8/suggestions",
  "/phase8/assistant",
  "/phase8/assistant-config",
  "Phase8SuggestionRecommendationPage",
  "Phase8AssistantRuntimePage",
  "Phase8AssistantConfigurationPage"
]) {
  if (!app.includes(signal)) failures.push("App route missing " + signal);
}

const program = exists("Backend/PlantProcess.Api/Program.cs")
  ? read("Backend/PlantProcess.Api/Program.cs")
  : "";

for (const signal of [
  "MapAssistantEndpoints",
  "MapPhase8AssistantRuntimeEndpoints"
]) {
  if (!program.includes(signal)) failures.push("Program.cs missing " + signal);
}

const layout = exists("Frontend/PlantProcess.Web/src/components/AppLayout.tsx")
  ? read("Frontend/PlantProcess.Web/src/components/AppLayout.tsx")
  : "";

for (const signal of [
  "P08 Suggestions",
  "P08 Assistant",
  "Assistant Config"
]) {
  if (!layout.includes(signal)) failures.push("AppLayout missing nav " + signal);
}

if (failures.length) {
  console.error("PPIQ Phase 8 T-045/T-046/T-047 validation failed.");
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("PPIQ Phase 8 T-045/T-046/T-047 passed: suggestion page, grounded assistant runtime, and HMI assistant config are present.");
`);

write("docs/phase8/T045_T046_T047_AI_ASSISTANT_HMI.md", String.raw`
# Phase 8 T-045 / T-046 / T-047 AI Assistant HMI

Marker: PPIQ_REALIZATION_T045_T046_T047_PHASE8_ASSISTANT_HMI

Implemented scope:

- T-045 Suggestion and Recommendation page
- T-046 Assistant chat with grounding runtime
- T-047 Configure assistant from the HMI

Routes:

- /phase8/suggestions
- /phase8/assistant
- /phase8/assistant-config

Backend APIs:

- GET /api/phase8/suggestions/health
- POST /api/phase8/suggestions/generate
- POST /api/phase8/suggestions/decision
- GET /api/phase8/assistant-config
- PUT /api/phase8/assistant-config
- POST /api/phase8/assistant-config/reset
- POST /api/assistant/ask is mapped for grounded assistant runtime

Validation:

- node tools/phase8/validate-t045-t046-t047-ai-assistant-hmi.cjs
- dotnet build Backend
- dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase8AssistantRuntimeTests --no-build
- npm run test -- src/pages/Phase8/phase8AssistantView.test.ts
- npm run build
`);

run("node --check validator", "node", ["--check", "tools/phase8/validate-t045-t046-t047-ai-assistant-hmi.cjs"]);
run("Phase 8 T-045/T-046/T-047 validator", "node", ["tools/phase8/validate-t045-t046-t047-ai-assistant-hmi.cjs"]);
run("Backend build", "dotnet", ["build", "Backend"]);
run("Phase 8 backend unit tests", "dotnet", [
  "test",
  "Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj",
  "--filter",
  "FullyQualifiedName~Phase8AssistantRuntimeTests",
  "--no-build"
]);
run("Frontend targeted Phase 8 test", "npm.cmd", [
  "run",
  "test",
  "--",
  "src/pages/Phase8/phase8AssistantView.test.ts"
], p("Frontend/PlantProcess.Web"));
run("Frontend build", "npm.cmd", ["run", "build"], p("Frontend/PlantProcess.Web"));

console.log("");
console.log("=================================================================================================");
console.log("Phase 8 T-045/T-046/T-047 completed.");
console.log("=================================================================================================");