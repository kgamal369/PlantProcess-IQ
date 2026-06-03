// P4-04 Advanced Analysis page (doctrine §7.4) — Standard kit + brand tokens.
import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { StandardCard, DataFetchBoundary, StandardButton, ppiqTokens } from "../../components/standard";
import { getAdvancedResults, getAnalysisReadiness, type AdvancedFindingDto, type AnalysisReadinessDto } from "../../api/advancedAnalysis";

const c = ppiqTokens.color;
const stateColor = (s: string) => (s === "Ready" ? c.success : s === "Partial" ? c.warning : c.danger);
const pct = (x?: number | null) => (x == null ? "\u2014" : `${Math.round(x * 100)}%`);
const num = (x?: number | null, d = 3) => (x == null ? "\u2014" : x.toFixed(d));

export function AdvancedAnalysisPage() {
  const [params] = useSearchParams();
  const outcomeKey = params.get("outcomeKey") ?? "defect.edge_crack_rate";
  const grain = params.get("grain") ?? "coil";
  const windowDays = Number(params.get("windowDays") ?? "30");
  const runId = params.get("runId") ?? undefined;

  const [readiness, setReadiness] = useState<AnalysisReadinessDto | null>(null);
  const [findings, setFindings] = useState<AdvancedFindingDto[]>([]);
  const [caveat, setCaveat] = useState("This is a diagnostic association, not a guaranteed root cause.");
  const [isLoading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let live = true; setLoading(true); setError(null);
    Promise.all([getAnalysisReadiness(outcomeKey, grain, windowDays), getAdvancedResults(outcomeKey, runId)])
      .then(([rd, res]) => { if (!live) return; setReadiness(rd); setFindings(res); const cav = res.find((f) => f.honestyCaveat)?.honestyCaveat; if (cav) setCaveat(cav); })
      .catch((e) => { if (live) setError(e); })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [outcomeKey, grain, windowDays, runId, reloadKey]);

  const contributors = useMemo(() => findings.filter((f) => f.isRenderable), [findings]);
  const excluded = useMemo(() => findings.filter((f) => !f.isRenderable), [findings]);
  const blocked = readiness != null && !readiness.canRun;

  const badge = readiness ? (
    <span style={{ background: stateColor(readiness.overall), color: c.navy900, fontWeight: 700, padding: "4px 12px", borderRadius: 999, fontSize: 12 }}>
      {readiness.overall.toUpperCase()}
    </span>
  ) : null;

  return (
    <div style={{ display: "grid", gap: ppiqTokens.spacing.lg, padding: ppiqTokens.spacing.lg }}>
      <StandardCard
        eyebrow="Advanced analysis \u00b7 doctrine \u00a77.4"
        title={`Suspected contributors \u2014 ${outcomeKey}`}
        subtitle={`grain=${grain} \u00b7 window=${windowDays}d \u00b7 n=${readiness?.outcomeEvents ?? "\u2014"} \u00b7 independent heats=${readiness?.independentHeats ?? "\u2014"}`}
        actions={badge}
        elevation="raised"
      >
        <DataFetchBoundary
          title="Advanced analysis" isLoading={isLoading} error={error}
          isEmpty={!blocked && contributors.length === 0} onRetry={() => setReloadKey((k) => k + 1)}
          emptyTitle="No supported contributors"
          emptyMessage="No statistically supported contributors were found for this target and window."
        >
          {blocked ? (
            <div style={{ borderLeft: `3px solid ${c.danger}`, padding: ppiqTokens.spacing.md, background: c.surface2, borderRadius: ppiqTokens.radius.md }}>
              <strong style={{ color: c.danger }}>Analysis blocked by the data-readiness gate.</strong>
              <ul style={{ margin: "8px 0 0", color: c.textMuted, fontSize: 13 }}>
                {readiness!.dimensions.map((d) => <li key={d.name}>{d.name}: {d.reason}</li>)}
              </ul>
            </div>
          ) : (
            <div style={{ display: "grid", gap: ppiqTokens.spacing.md }}>
              {contributors.map((f) => {
                const e = Math.min(1, Math.abs(f.effectSize ?? 0));
                return (
                  <div key={f.findingId} style={{ border: `1px solid ${c.borderSubtle}`, borderRadius: ppiqTokens.radius.md, padding: ppiqTokens.spacing.md, background: c.surface1 }}>
                    <div style={{ display: "flex", justifyContent: "space-between" }}>
                      <strong style={{ color: c.text }}>{f.findingId}</strong>
                      <span style={{ color: c.brandCyan, fontFamily: "monospace", fontSize: 12 }}>{f.method}</span>
                    </div>
                    <div style={{ height: 8, background: c.navy700, borderRadius: 4, marginTop: 8 }}>
                      <div style={{ width: `${e * 100}%`, height: 8, background: c.brandCyan, borderRadius: 4 }} />
                    </div>
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 16, marginTop: 8, fontSize: 12, color: c.textMuted, fontFamily: "monospace" }}>
                      <span>effect {num(f.effectSize)}</span>
                      <span>q {num(f.qValue)}{f.significant ? " \u2713sig" : ""}</span>
                      <span>stability {pct(f.stabilityConsistency)} [{num(f.stabilityLower)}, {num(f.stabilityUpper)}]</span>
                      <span>n {f.sampleSize}</span>
                      <span style={{ color: f.survivesStratification ? c.success : c.warning }}>{f.survivesStratification ? "survives stratification" : "fails stratification"}</span>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </DataFetchBoundary>
      </StandardCard>

      {excluded.length > 0 && (
        <StandardCard eyebrow="Screening" title="Excluded features" elevation="flat">
          <ul style={{ margin: 0, color: c.textMuted, fontSize: 13 }}>
            {excluded.map((f) => {
              let reason = ""; try { reason = (JSON.parse(f.evidence ?? "{}") as { reason?: string }).reason ?? ""; } catch { /* ignore */ }
              return <li key={f.findingId}>{f.findingId}{reason ? ` \u2014 ${reason}` : ""}</li>;
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
