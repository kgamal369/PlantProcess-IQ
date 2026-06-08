const fs = require("fs");
const path = require("path");

const root = process.cwd();

function p(relativePath) {
  return path.join(root, relativePath.replace(/\//g, path.sep));
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(relativePath) {
  return fs.existsSync(p(relativePath));
}

function read(relativePath) {
  return fs.readFileSync(p(relativePath), "utf8");
}

function write(relativePath, content) {
  const target = p(relativePath);
  ensureDir(path.dirname(target));
  fs.writeFileSync(target, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + relativePath);
}

function backup(relativePath) {
  const source = p(relativePath);
  if (!fs.existsSync(source)) return;

  const stamp = new Date().toISOString().replace(/[-:]/g, "").replace(/\..+/, "").replace("T", "_");
  const target = p(".phase8_backup/t045_readiness_gates_" + stamp + "/" + relativePath);
  ensureDir(path.dirname(target));
  fs.copyFileSync(source, target);
}

write("Backend/PlantProcess.Application/Analytics/Advanced/AdvancedReadinessGateSurface.cs", `
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Application.Analytics.Advanced;

/// <summary>
/// PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES.
/// Explicit user-facing gate state for advanced analysis readiness.
/// </summary>
public static class AdvancedReadinessGateStates
{
    public const string Ready = "Ready";
    public const string Partial = "Partial";
    public const string Blocked = "Blocked";
}

public sealed record AdvancedReadinessGateDto(
    string GateCode,
    string Title,
    string State,
    string Reason,
    string Evidence,
    bool IsBlocking);

public sealed record AdvancedReadinessGateSummaryDto(
    string State,
    bool CanRun,
    string OutcomeKey,
    string Grain,
    int WindowDays,
    int IndependentHeats,
    int OutcomeEvents,
    int ReadyCount,
    int PartialCount,
    int BlockedCount,
    string Message,
    IReadOnlyList<AdvancedReadinessGateDto> Gates);

public static class AdvancedReadinessGateProjector
{
    public const string Marker = "PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES";

    public static AdvancedReadinessGateSummaryDto Project(AnalysisReadinessDto readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);

        var gates = readiness.Dimensions
            .Select(d =>
            {
                var state = NormalizeState(d.State);
                return new AdvancedReadinessGateDto(
                    GateCode: ToGateCode(d.Name),
                    Title: d.Name,
                    State: state,
                    Reason: string.IsNullOrWhiteSpace(d.Reason) ? "No reason supplied by readiness evaluator." : d.Reason,
                    Evidence: $"{d.Name}={state}; outcome={readiness.OutcomeKey}; grain={readiness.Grain}; window={readiness.WindowDays}d",
                    IsBlocking: state == AdvancedReadinessGateStates.Blocked);
            })
            .ToArray();

        var readyCount = gates.Count(g => g.State == AdvancedReadinessGateStates.Ready);
        var partialCount = gates.Count(g => g.State == AdvancedReadinessGateStates.Partial);
        var blockedCount = gates.Count(g => g.State == AdvancedReadinessGateStates.Blocked);

        var state = blockedCount > 0 || !readiness.CanRun
            ? AdvancedReadinessGateStates.Blocked
            : partialCount > 0
                ? AdvancedReadinessGateStates.Partial
                : AdvancedReadinessGateStates.Ready;

        var message = state switch
        {
            AdvancedReadinessGateStates.Ready =>
                "Ready: all required analysis gates are satisfied. Advanced analysis may run.",
            AdvancedReadinessGateStates.Partial =>
                "Partial: analysis may run, but at least one quality/readiness dimension needs attention.",
            _ =>
                "Blocked: analysis must abstain until blocking readiness issues are fixed."
        };

        return new AdvancedReadinessGateSummaryDto(
            state,
            readiness.CanRun && state != AdvancedReadinessGateStates.Blocked,
            readiness.OutcomeKey,
            readiness.Grain,
            readiness.WindowDays,
            readiness.IndependentHeats,
            readiness.OutcomeEvents,
            readyCount,
            partialCount,
            blockedCount,
            message,
            gates);
    }

    public static string NormalizeState(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return AdvancedReadinessGateStates.Blocked;

        var value = raw.Trim();

        if (value.Equals(AdvancedReadinessGateStates.Ready, StringComparison.OrdinalIgnoreCase))
            return AdvancedReadinessGateStates.Ready;

        if (value.Equals(AdvancedReadinessGateStates.Partial, StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Warning", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Warn", StringComparison.OrdinalIgnoreCase))
            return AdvancedReadinessGateStates.Partial;

        if (value.Equals(AdvancedReadinessGateStates.Blocked, StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Blocker", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Failed", StringComparison.OrdinalIgnoreCase))
            return AdvancedReadinessGateStates.Blocked;

        return value.Contains("partial", StringComparison.OrdinalIgnoreCase)
            ? AdvancedReadinessGateStates.Partial
            : value.Contains("ready", StringComparison.OrdinalIgnoreCase)
                ? AdvancedReadinessGateStates.Ready
                : AdvancedReadinessGateStates.Blocked;
    }

    private static string ToGateCode(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "UNKNOWN_GATE";

        var chars = name
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_')
            .ToArray();

        var raw = new string(chars);

        while (raw.Contains("__", StringComparison.Ordinal))
            raw = raw.Replace("__", "_", StringComparison.Ordinal);

        return raw.Trim('_');
    }
}
`);

write("Backend/tests/PlantProcess.Application.UnitTests/Analytics/Advanced/Phase8_T045ReadinessGateSurfaceTests.cs", `
using PlantProcess.Application.Analytics.Advanced;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics.Advanced;

/// <summary>
/// PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES.
/// Certifies Ready / Partial / Blocked readiness gate projection for API and HMI.
/// </summary>
public sealed class Phase8_T045ReadinessGateSurfaceTests
{
    [Fact]
    public void T045_Projects_AllReady_Dimensions_To_Ready_State()
    {
        var readiness = new AnalysisReadinessDto(
            Overall: "Ready",
            CanRun: true,
            Dimensions: new[]
            {
                new ReadinessDimensionDto("Population", "Ready", "Enough independent heats."),
                new ReadinessDimensionDto("Freshness", "Ready", "Data is fresh."),
                new ReadinessDimensionDto("Completeness", "Ready", "Required fields are complete.")
            },
            OutcomeKey: "defect.edge_crack_rate",
            Grain: "coil",
            WindowDays: 3650,
            IndependentHeats: 200,
            OutcomeEvents: 900);

        var surface = AdvancedReadinessGateProjector.Project(readiness);

        Assert.Equal(AdvancedReadinessGateStates.Ready, surface.State);
        Assert.True(surface.CanRun);
        Assert.Equal(3, surface.ReadyCount);
        Assert.Equal(0, surface.PartialCount);
        Assert.Equal(0, surface.BlockedCount);
        Assert.All(surface.Gates, gate => Assert.Equal(AdvancedReadinessGateStates.Ready, gate.State));
        Assert.Contains("Ready", surface.Message);
    }

    [Fact]
    public void T045_Projects_Warning_Dimension_To_Partial_State()
    {
        var readiness = new AnalysisReadinessDto(
            Overall: "Partial",
            CanRun: true,
            Dimensions: new[]
            {
                new ReadinessDimensionDto("Population", "Ready", "Enough independent heats."),
                new ReadinessDimensionDto("Freshness", "Warning", "Data is usable but slightly stale.")
            },
            OutcomeKey: "defect.edge_crack_rate",
            Grain: "coil",
            WindowDays: 180,
            IndependentHeats: 75,
            OutcomeEvents: 300);

        var surface = AdvancedReadinessGateProjector.Project(readiness);

        Assert.Equal(AdvancedReadinessGateStates.Partial, surface.State);
        Assert.True(surface.CanRun);
        Assert.Equal(1, surface.ReadyCount);
        Assert.Equal(1, surface.PartialCount);
        Assert.Equal(0, surface.BlockedCount);
        Assert.Contains(surface.Gates, gate => gate.State == AdvancedReadinessGateStates.Partial);
        Assert.Contains("attention", surface.Message);
    }

    [Fact]
    public void T045_Projects_Blocking_Dimension_To_Blocked_State()
    {
        var readiness = new AnalysisReadinessDto(
            Overall: "Blocked",
            CanRun: false,
            Dimensions: new[]
            {
                new ReadinessDimensionDto("Population", "Blocked", "Not enough independent heats."),
                new ReadinessDimensionDto("Completeness", "Ready", "Fields are complete.")
            },
            OutcomeKey: "defect.edge_crack_rate",
            Grain: "coil",
            WindowDays: 30,
            IndependentHeats: 4,
            OutcomeEvents: 12);

        var surface = AdvancedReadinessGateProjector.Project(readiness);

        Assert.Equal(AdvancedReadinessGateStates.Blocked, surface.State);
        Assert.False(surface.CanRun);
        Assert.Equal(1, surface.BlockedCount);

        var blocker = Assert.Single(surface.Gates, gate => gate.IsBlocking);
        Assert.Equal("POPULATION", blocker.GateCode);
        Assert.Contains("Not enough", blocker.Reason);
        Assert.Contains("analysis must abstain", surface.Message);
    }

    [Theory]
    [InlineData("Ready", "Ready")]
    [InlineData("Warning", "Partial")]
    [InlineData("Warn", "Partial")]
    [InlineData("Partial", "Partial")]
    [InlineData("Failed", "Blocked")]
    [InlineData("Blocker", "Blocked")]
    [InlineData("Blocked", "Blocked")]
    public void T045_Normalizes_Legacy_And_Canonical_Readiness_States(string input, string expected)
    {
        Assert.Equal(expected, AdvancedReadinessGateProjector.NormalizeState(input));
    }
}
`);

const endpointPath = "Backend/PlantProcess.Api/Endpoints/Analytics/AdvancedResultsEndpoints.cs";
backup(endpointPath);

let endpoint = read(endpointPath);

if (!endpoint.includes("using PlantProcess.Application.Analytics.Advanced;")) {
  endpoint = endpoint.replace(
    "using Npgsql;",
    "using Npgsql;\nusing PlantProcess.Application.Analytics.Advanced;"
  );
}

if (!endpoint.includes("/readiness/gates")) {
  const routePattern = /group\.MapGet\("\/readiness",[\s\S]*?\)\);/;

  const match = endpoint.match(routePattern);
  if (!match) {
    throw new Error("Could not find /readiness endpoint anchor in AdvancedResultsEndpoints.cs");
  }

  const gatesRoute = `
        // PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES: canonical API surface for HMI readiness badges.
        group.MapGet("/readiness/gates", async (
            string outcomeKey,
            string? grain,
            int? windowDays,
            System.Guid? tenantId,
            IAnalysisReadinessService readiness,
            System.Threading.CancellationToken ct) =>
        {
            var request = new AdvancedAnalysisRequest(
                outcomeKey,
                string.IsNullOrWhiteSpace(grain) ? "coil" : grain!,
                windowDays ?? 3650,
                tenantId ?? AdvancedDefaults.DemoTenant);

            var dto = await readiness.EvaluateAsync(request, ct);
            return Results.Ok(AdvancedReadinessGateProjector.Project(dto));
        });`;

  endpoint = endpoint.replace(routePattern, match[0] + "\n" + gatesRoute);
  write(endpointPath, endpoint);
}

const apiPath = "Frontend/PlantProcess.Web/src/api/advancedAnalysis.ts";
backup(apiPath);

let api = read(apiPath);

if (!api.includes("AdvancedReadinessGateSummaryDto")) {
  api = api.replace(
`export interface AnalysisReadinessDto {
  overall: string; canRun: boolean; dimensions: ReadinessDimensionDto[];
  outcomeKey: string; grain: string; windowDays: number; independentHeats: number; outcomeEvents: number;
}`,
`export interface AnalysisReadinessDto {
  overall: string; canRun: boolean; dimensions: ReadinessDimensionDto[];
  outcomeKey: string; grain: string; windowDays: number; independentHeats: number; outcomeEvents: number;
}

export type AdvancedReadinessGateState = "Ready" | "Partial" | "Blocked";

export interface AdvancedReadinessGateDto {
  gateCode: string;
  title: string;
  state: AdvancedReadinessGateState;
  reason: string;
  evidence: string;
  isBlocking: boolean;
}

export interface AdvancedReadinessGateSummaryDto {
  state: AdvancedReadinessGateState;
  canRun: boolean;
  outcomeKey: string;
  grain: string;
  windowDays: number;
  independentHeats: number;
  outcomeEvents: number;
  readyCount: number;
  partialCount: number;
  blockedCount: number;
  message: string;
  gates: AdvancedReadinessGateDto[];
}`
  );

  api = api.replace(
`export const getAnalysisReadiness = (outcomeKey: string, grain = "coil", windowDays = 30) =>
  apiClient.get<AnalysisReadinessDto>(\`${'${BASE}'}/readiness?${'${qs({ outcomeKey, grain, windowDays })}'}\`);`,
`export const getAnalysisReadiness = (outcomeKey: string, grain = "coil", windowDays = 30) =>
  apiClient.get<AnalysisReadinessDto>(\`${'${BASE}'}/readiness?${'${qs({ outcomeKey, grain, windowDays })}'}\`);

export const getAnalysisReadinessGates = (outcomeKey: string, grain = "coil", windowDays = 30) =>
  apiClient.get<AdvancedReadinessGateSummaryDto>(\`${'${BASE}'}/readiness/gates?${'${qs({ outcomeKey, grain, windowDays })}'}\`);`
  );

  write(apiPath, api);
}

write("Frontend/PlantProcess.Web/src/pages/Analytics/advancedReadinessGateView.ts", `
export type ReadinessGateState = "Ready" | "Partial" | "Blocked";

export type ReadinessGateView = {
  state: ReadinessGateState;
  label: string;
  tone: "success" | "warning" | "danger";
};

export function normalizeReadinessGateState(raw: string | null | undefined): ReadinessGateState {
  if (!raw) return "Blocked";

  const value = raw.trim().toLowerCase();

  if (value === "ready") return "Ready";
  if (value === "partial" || value === "warning" || value === "warn") return "Partial";
  if (value === "blocked" || value === "failed" || value === "blocker") return "Blocked";

  if (value.includes("partial")) return "Partial";
  if (value.includes("ready")) return "Ready";

  return "Blocked";
}

export function readinessGateView(raw: string | null | undefined): ReadinessGateView {
  const state = normalizeReadinessGateState(raw);

  if (state === "Ready") {
    return { state, label: "READY", tone: "success" };
  }

  if (state === "Partial") {
    return { state, label: "PARTIAL", tone: "warning" };
  }

  return { state, label: "BLOCKED", tone: "danger" };
}

export function readinessGateSummaryText(state: string, ready: number, partial: number, blocked: number): string {
  const normalized = normalizeReadinessGateState(state);

  if (normalized === "Ready") {
    return \`Ready: \${ready} gate(s) ready; advanced analysis may run.\`;
  }

  if (normalized === "Partial") {
    return \`Partial: \${ready} ready, \${partial} partial, \${blocked} blocked.\`;
  }

  return \`Blocked: \${blocked} blocking gate(s); advanced analysis must abstain.\`;
}
`);

write("Frontend/PlantProcess.Web/src/pages/Analytics/advancedReadinessGateView.test.ts", `
import { describe, expect, it } from "vitest";
import {
  normalizeReadinessGateState,
  readinessGateSummaryText,
  readinessGateView,
} from "./advancedReadinessGateView";

describe("T-045 advanced readiness gate view", () => {
  it("normalizes Ready / Partial / Blocked states", () => {
    expect(normalizeReadinessGateState("Ready")).toBe("Ready");
    expect(normalizeReadinessGateState("Warning")).toBe("Partial");
    expect(normalizeReadinessGateState("Partial")).toBe("Partial");
    expect(normalizeReadinessGateState("Failed")).toBe("Blocked");
    expect(normalizeReadinessGateState("Blocked")).toBe("Blocked");
  });

  it("returns UI tone for HMI badges", () => {
    expect(readinessGateView("Ready")).toEqual({ state: "Ready", label: "READY", tone: "success" });
    expect(readinessGateView("Partial").tone).toBe("warning");
    expect(readinessGateView("Blocked").tone).toBe("danger");
  });

  it("builds explicit readiness summary text", () => {
    expect(readinessGateSummaryText("Ready", 4, 0, 0)).toContain("may run");
    expect(readinessGateSummaryText("Partial", 3, 1, 0)).toContain("partial");
    expect(readinessGateSummaryText("Blocked", 2, 0, 1)).toContain("must abstain");
  });
});
`);

const pagePath = "Frontend/PlantProcess.Web/src/pages/Analytics/AdvancedAnalysisPage.tsx";
backup(pagePath);

write(pagePath, `
// P4-04 Advanced Analysis page (doctrine §7.4) — Standard kit + brand tokens.
// PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES: HMI exposes Ready / Partial / Blocked gates.
import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { StandardCard, DataFetchBoundary, ppiqTokens } from "../../components/standard";
import {
  getAdvancedResults,
  getAnalysisReadiness,
  getAnalysisReadinessGates,
  type AdvancedFindingDto,
  type AnalysisReadinessDto,
  type AdvancedReadinessGateSummaryDto,
} from "../../api/advancedAnalysis";
import { readinessGateSummaryText, readinessGateView } from "./advancedReadinessGateView";

const c = ppiqTokens.color;

const stateColor = (s: string) => {
  const view = readinessGateView(s);
  return view.tone === "success" ? c.success : view.tone === "warning" ? c.warning : c.danger;
};

const pct = (x?: number | null) => (x == null ? "—" : \`\${Math.round(x * 100)}%\`);
const num = (x?: number | null, d = 3) => (x == null ? "—" : x.toFixed(d));

export function AdvancedAnalysisPage() {
  const [params] = useSearchParams();
  const outcomeKey = params.get("outcomeKey") ?? "defect.edge_crack_rate";
  const grain = params.get("grain") ?? "coil";
  const windowDays = Number(params.get("windowDays") ?? "30");
  const runId = params.get("runId") ?? undefined;

  const [readiness, setReadiness] = useState<AnalysisReadinessDto | null>(null);
  const [gateSummary, setGateSummary] = useState<AdvancedReadinessGateSummaryDto | null>(null);
  const [findings, setFindings] = useState<AdvancedFindingDto[]>([]);
  const [caveat, setCaveat] = useState("This is a diagnostic association, not a guaranteed root cause.");
  const [isLoading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let live = true;

    setLoading(true);
    setError(null);

    Promise.all([
      getAnalysisReadiness(outcomeKey, grain, windowDays),
      getAnalysisReadinessGates(outcomeKey, grain, windowDays),
      getAdvancedResults(outcomeKey, runId),
    ])
      .then(([rd, gates, res]) => {
        if (!live) return;

        setReadiness(rd);
        setGateSummary(gates);
        setFindings(res);

        const cav = res.find((f) => f.honestyCaveat)?.honestyCaveat;
        if (cav) setCaveat(cav);
      })
      .catch((e) => {
        if (live) setError(e);
      })
      .finally(() => {
        if (live) setLoading(false);
      });

    return () => {
      live = false;
    };
  }, [outcomeKey, grain, windowDays, runId, reloadKey]);

  const contributors = useMemo(() => findings.filter((f) => f.isRenderable), [findings]);
  const excluded = useMemo(() => findings.filter((f) => !f.isRenderable), [findings]);
  const blocked = (gateSummary?.state ?? readiness?.overall) === "Blocked" || (readiness != null && !readiness.canRun);

  const state = gateSummary?.state ?? readiness?.overall ?? "Blocked";
  const view = readinessGateView(state);

  const badge = (
    <span
      data-testid="phase8-readiness-state-badge"
      style={{
        background: stateColor(state),
        color: c.navy900,
        fontWeight: 700,
        padding: "4px 12px",
        borderRadius: 999,
        fontSize: 12,
      }}
    >
      {view.label}
    </span>
  );

  return (
    <div style={{ display: "grid", gap: ppiqTokens.spacing.lg, padding: ppiqTokens.spacing.lg }}>
      <StandardCard
        eyebrow="Advanced analysis · doctrine §7.4"
        title={\`Suspected contributors — \${outcomeKey}\`}
        subtitle={\`grain=\${grain} · window=\${windowDays}d · n=\${readiness?.outcomeEvents ?? "—"} · independent heats=\${readiness?.independentHeats ?? "—"}\`}
        actions={badge}
        elevation="raised"
      >
        <DataFetchBoundary
          title="Advanced analysis"
          isLoading={isLoading}
          error={error}
          isEmpty={!blocked && contributors.length === 0}
          onRetry={() => setReloadKey((k) => k + 1)}
          emptyTitle="No supported contributors"
          emptyMessage="No statistically supported contributors were found for this target and window."
        >
          {blocked ? (
            <div style={{ borderLeft: \`3px solid \${c.danger}\`, padding: ppiqTokens.spacing.md, background: c.surface2, borderRadius: ppiqTokens.radius.md }}>
              <strong style={{ color: c.danger }}>Analysis blocked by the data-readiness gate.</strong>
              <ul style={{ margin: "8px 0 0", color: c.textMuted, fontSize: 13 }}>
                {(gateSummary?.gates ?? []).map((gate) => (
                  <li key={gate.gateCode}>{gate.title}: {gate.reason}</li>
                ))}
                {!gateSummary && readiness?.dimensions.map((d) => <li key={d.name}>{d.name}: {d.reason}</li>)}
              </ul>
            </div>
          ) : (
            <div style={{ display: "grid", gap: ppiqTokens.spacing.md }}>
              {contributors.map((f) => {
                const e = Math.min(1, Math.abs(f.effectSize ?? 0));
                return (
                  <div key={f.findingId} style={{ border: \`1px solid \${c.borderSubtle}\`, borderRadius: ppiqTokens.radius.md, padding: ppiqTokens.spacing.md, background: c.surface1 }}>
                    <div style={{ display: "flex", justifyContent: "space-between" }}>
                      <strong style={{ color: c.text }}>{f.findingId}</strong>
                      <span style={{ color: c.brandCyan, fontFamily: "monospace", fontSize: 12 }}>{f.method}</span>
                    </div>
                    <div style={{ height: 8, background: c.navy700, borderRadius: 4, marginTop: 8 }}>
                      <div style={{ width: \`\${e * 100}%\`, height: 8, background: c.brandCyan, borderRadius: 4 }} />
                    </div>
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 16, marginTop: 8, fontSize: 12, color: c.textMuted, fontFamily: "monospace" }}>
                      <span>effect {num(f.effectSize)}</span>
                      <span>q {num(f.qValue)}{f.significant ? " ✓sig" : ""}</span>
                      <span>stability {pct(f.stabilityConsistency)} [{num(f.stabilityLower)}, {num(f.stabilityUpper)}]</span>
                      <span>n {f.sampleSize}</span>
                      <span style={{ color: f.survivesStratification ? c.success : c.warning }}>
                        {f.survivesStratification ? "survives stratification" : "fails stratification"}
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </DataFetchBoundary>
      </StandardCard>

      <StandardCard
        eyebrow="PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES"
        title="Readiness gates"
        subtitle={gateSummary?.message ?? readinessGateSummaryText(state, 0, 0, blocked ? 1 : 0)}
        elevation="flat"
      >
        <div data-testid="phase8-readiness-gates" style={{ display: "grid", gap: 10 }}>
          <div style={{ display: "flex", gap: 12, flexWrap: "wrap", fontSize: 13, color: c.textMuted }}>
            <span>State: <strong style={{ color: stateColor(state) }}>{state}</strong></span>
            <span>Ready: {gateSummary?.readyCount ?? "—"}</span>
            <span>Partial: {gateSummary?.partialCount ?? "—"}</span>
            <span>Blocked: {gateSummary?.blockedCount ?? "—"}</span>
          </div>

          {(gateSummary?.gates ?? []).map((gate) => {
            const gateView = readinessGateView(gate.state);
            return (
              <div
                key={gate.gateCode}
                data-testid="phase8-readiness-gate-row"
                style={{
                  border: \`1px solid \${c.borderSubtle}\`,
                  borderLeft: \`4px solid \${stateColor(gate.state)}\`,
                  borderRadius: ppiqTokens.radius.md,
                  padding: ppiqTokens.spacing.md,
                  background: c.surface1,
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", gap: 12 }}>
                  <strong style={{ color: c.text }}>{gate.title}</strong>
                  <span style={{ color: stateColor(gate.state), fontWeight: 800 }}>{gateView.label}</span>
                </div>
                <p style={{ margin: "6px 0 0", color: c.textMuted, fontSize: 13 }}>{gate.reason}</p>
                <p style={{ margin: "4px 0 0", color: c.textMuted, fontSize: 12, fontFamily: "monospace" }}>{gate.evidence}</p>
              </div>
            );
          })}

          {!gateSummary && readiness?.dimensions.map((dimension) => (
            <div key={dimension.name} data-testid="phase8-readiness-gate-row">
              <strong>{dimension.name}</strong>: {dimension.state} — {dimension.reason}
            </div>
          ))}
        </div>
      </StandardCard>

      {excluded.length > 0 && (
        <StandardCard eyebrow="Screening" title="Excluded features" elevation="flat">
          <ul style={{ margin: 0, color: c.textMuted, fontSize: 13 }}>
            {excluded.map((f) => {
              let reason = "";
              try {
                reason = (JSON.parse(f.evidence ?? "{}") as { reason?: string }).reason ?? "";
              } catch {
                // ignore malformed evidence JSON
              }
              return <li key={f.findingId}>{f.findingId}{reason ? \` — \${reason}\` : ""}</li>;
            })}
          </ul>
        </StandardCard>
      )}

      <StandardCard elevation="flat" style={{ borderColor: c.borderStrong }}>
        <div style={{ display: "flex", gap: 10, alignItems: "flex-start" }}>
          <span style={{ color: c.brandCyan, fontWeight: 700 }}>Honesty</span>
          <span style={{ color: c.text, fontSize: 13 }}>{caveat}</span>
        </div>
      </StandardCard>
    </div>
  );
}
`);

write("tools/phase8/validate-t045-readiness-gates.cjs", `
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function file(relativePath) {
  return path.join(root, relativePath);
}

function exists(relativePath) {
  return fs.existsSync(file(relativePath));
}

function read(relativePath) {
  return fs.readFileSync(file(relativePath), "utf8");
}

const checks = [
  {
    file: "Backend/PlantProcess.Application/Analytics/Advanced/AdvancedReadinessGateSurface.cs",
    signals: [
      "PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES",
      "AdvancedReadinessGateStates",
      "Ready",
      "Partial",
      "Blocked",
      "AdvancedReadinessGateDto",
      "AdvancedReadinessGateSummaryDto",
      "AdvancedReadinessGateProjector",
      "NormalizeState"
    ]
  },
  {
    file: "Backend/PlantProcess.Api/Endpoints/Analytics/AdvancedResultsEndpoints.cs",
    signals: [
      "/readiness/gates",
      "PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES",
      "AdvancedReadinessGateProjector.Project"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Analytics/Advanced/Phase8_T045ReadinessGateSurfaceTests.cs",
    signals: [
      "T045_Projects_AllReady_Dimensions_To_Ready_State",
      "T045_Projects_Warning_Dimension_To_Partial_State",
      "T045_Projects_Blocking_Dimension_To_Blocked_State",
      "T045_Normalizes_Legacy_And_Canonical_Readiness_States"
    ]
  },
  {
    file: "Frontend/PlantProcess.Web/src/api/advancedAnalysis.ts",
    signals: [
      "AdvancedReadinessGateSummaryDto",
      "AdvancedReadinessGateDto",
      "getAnalysisReadinessGates",
      "/readiness/gates"
    ]
  },
  {
    file: "Frontend/PlantProcess.Web/src/pages/Analytics/AdvancedAnalysisPage.tsx",
    signals: [
      "PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES",
      "getAnalysisReadinessGates",
      "phase8-readiness-state-badge",
      "phase8-readiness-gates",
      "phase8-readiness-gate-row",
      "Readiness gates"
    ]
  },
  {
    file: "Frontend/PlantProcess.Web/src/pages/Analytics/advancedReadinessGateView.test.ts",
    signals: [
      "normalizes Ready / Partial / Blocked states",
      "returns UI tone for HMI badges",
      "builds explicit readiness summary text"
    ]
  }
];

for (const check of checks) {
  if (!exists(check.file)) {
    failures.push({ file: check.file, reason: "missing file" });
    continue;
  }

  const text = read(check.file);

  for (const signal of check.signals) {
    if (!text.includes(signal)) {
      failures.push({ file: check.file, reason: "missing signal: " + signal });
    }
  }
}

if (failures.length) {
  console.error("PPIQ-T045 failed: Ready/Partial/Blocked readiness gates are incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T045 passed: Ready/Partial/Blocked readiness gates are exposed in API and HMI.");
`);

write("docs/phase8/T045_READY_PARTIAL_BLOCKED_READINESS_GATES.md", `
# T-045 Ready / Partial / Blocked Readiness Gates

Marker: PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES

## Purpose

Expose advanced-analysis readiness as an explicit API and HMI contract:

- Ready
- Partial
- Blocked

## Backend

Adds:

- AdvancedReadinessGateStates
- AdvancedReadinessGateDto
- AdvancedReadinessGateSummaryDto
- AdvancedReadinessGateProjector
- GET /api/analytics/advanced/readiness/gates

## Frontend / HMI

Adds visible readiness gate cards to the Advanced Analysis page.

Each gate displays:

- state
- reason
- evidence string
- blocking status

## Guardrail

Blocked analysis must abstain. Partial analysis may run but must show that at least one dimension needs attention.

## Validation

Run:

    node tools/phase8/validate-t045-readiness-gates.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase8_T045ReadinessGateSurfaceTests --no-build
    cd Frontend/PlantProcess.Web
    npm run build
    npx vitest run src/pages/Analytics/advancedReadinessGateView.test.ts --config vitest.config.ts
`);