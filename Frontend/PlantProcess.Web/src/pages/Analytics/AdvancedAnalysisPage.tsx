
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

import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
import { AnalysisHonestyBar } from "../../components/standard/AnalysisHonestyBar";
const c = ppiqTokens.color;

const stateColor = (s: string) => {
  const view = readinessGateView(s);
  return view.tone === "success" ? c.success : view.tone === "warning" ? c.warning : c.danger;
};

const pct = (x?: number | null) => (x == null ? "—" : `${Math.round(x * 100)}%`);
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
  const [caveat, setCaveat] = useState("This is a diagnostic association, not a confirmed root cause.");
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
    >
      {view.label}
    </span>
  );

  return (
    <div>
      <AnalysisHonestyBar population={contributors.length + excluded.length} excluded={excluded.length} blocked={blocked} />
      <StandardCard
        eyebrow="Advanced analysis · doctrine §7.4"
        title={`Suspected contributors — ${outcomeKey}`}
        subtitle={`grain=${grain} · window=${windowDays}d · n=${readiness?.outcomeEvents ?? "—"} · independent heats=${readiness?.independentHeats ?? "—"}`}
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
            <div>
              <strong>Analysis blocked by the data-readiness gate.</strong>
              <ul>
                {(gateSummary?.gates ?? []).map((gate) => (
                  <li key={gate.gateCode}>{gate.title}: {gate.reason}</li>
                ))}
                {!gateSummary && readiness?.dimensions.map((d) => <li key={d.name}>{d.name}: {d.reason}</li>)}
              </ul>
            </div>
          ) : (
            <div>
              {contributors.map((f) => {
                const e = Math.min(1, Math.abs(f.effectSize ?? 0));
                return (
                  <div key={f.findingId}>
                    <div>
                      <strong>{f.findingId}</strong>
                      <span>{f.method}</span>
                    </div>
                    <div>
                      <div />
                    </div>
                    <div>
                      <span>effect {num(f.effectSize)}</span>
                      <span>q {num(f.qValue)}{f.significant ? " ✓sig" : ""}</span>
                      <span>stability {pct(f.stabilityConsistency)} [{num(f.stabilityLower)}, {num(f.stabilityUpper)}]</span>
                      <span>n {f.sampleSize}</span>
                      <span>
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
        <div data-testid="phase8-readiness-gates">
          <div>
            <span>State: <strong>{state}</strong></span>
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
              >
                <div>
                  <strong>{gate.title}</strong>
                  <span>{gateView.label}</span>
                </div>
                <p>{gate.reason}</p>
                <p>{gate.evidence}</p>
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
          <ul>
            {excluded.map((f) => {
              let reason = "";
              try {
                reason = (JSON.parse(f.evidence ?? "{}") as { reason?: string }).reason ?? "";
              } catch {
                // ignore malformed evidence JSON
              }
              return <li key={f.findingId}>{f.findingId}{reason ? ` — ${reason}` : ""}</li>;
            })}
          </ul>
        </StandardCard>
      )}

      <StandardCard elevation="flat">
        <div>
          <span>Honesty</span>
          <span>{caveat}</span>
        </div>
      </StandardCard>
    </div>
  );
}
