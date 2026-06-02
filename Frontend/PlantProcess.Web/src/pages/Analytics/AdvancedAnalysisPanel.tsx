// P4-04 Advanced Analysis panel (doctrine 7.4). SELF-CONTAINED on purpose so it compiles
// without the Standard component library; swap the primitives below for StandardSurface/
// StandardTable/StandardBadge and wire into your router when integrating.
import { useEffect, useState } from "react";
import {
  getAdvancedResults, getAnalysisReadiness,
  type AdvancedFindingDto, type AnalysisReadinessDto,
} from "../../api/advancedAnalysis";

const C = { navy: "#050B18", panel: "#0B1626", cyan: "#00D4FF", text: "#E6EDF7", sub: "#8FA3BF", warn: "#F5A623", bad: "#E5484D", good: "#3FB950", line: "#1C2A40" };
const mono = "'JetBrains Mono', ui-monospace, monospace";

function readinessColor(s: string) { return s === "Ready" ? C.good : s === "Partial" ? C.warn : C.bad; }
function pct(x: number | null | undefined) { return x == null ? "—" : `${Math.round(x * 100)}%`; }
function num(x: number | null | undefined, d = 3) { return x == null ? "—" : x.toFixed(d); }

export interface AdvancedAnalysisPanelProps { outcomeKey: string; grain?: string; windowDays?: number; targetLabel?: string; }

export default function AdvancedAnalysisPanel({ outcomeKey, grain = "coil", windowDays = 30, targetLabel }: AdvancedAnalysisPanelProps) {
  const [readiness, setReadiness] = useState<AnalysisReadinessDto | null>(null);
  const [findings, setFindings] = useState<AdvancedFindingDto[]>([]);
  const [caveat, setCaveat] = useState<string>("This is a diagnostic association, not a guaranteed root cause.");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let live = true;
    (async () => {
      setLoading(true); setError(null);
      try {
        const [rd, res] = await Promise.all([
          getAnalysisReadiness(outcomeKey, grain, windowDays),
          getAdvancedResults(outcomeKey),
        ]);
        if (!live) return;
        setReadiness(rd);
        setFindings(res.results ?? []);
        if (res.honestyCaveat) setCaveat(res.honestyCaveat);
      } catch (e) { if (live) setError((e as Error).message); }
      finally { if (live) setLoading(false); }
    })();
    return () => { live = false; };
  }, [outcomeKey, grain, windowDays]);

  const contributors = findings.filter(f => f.isRenderable);
  const excluded = findings.filter(f => !f.isRenderable);

  const wrap: React.CSSProperties = { background: C.navy, color: C.text, padding: 20, borderRadius: 10, fontFamily: "Inter, system-ui, sans-serif" };
  const card: React.CSSProperties = { background: C.panel, border: `1px solid ${C.line}`, borderRadius: 8, padding: 16, marginTop: 12 };

  if (loading) return <div style={wrap}>Loading advanced analysis…</div>;
  if (error) return <div style={{ ...wrap, color: C.warn }}>Could not load analysis: {error}</div>;

  return (
    <div style={wrap}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline" }}>
        <div>
          <div style={{ fontSize: 18, fontWeight: 700 }}>Suspected contributors — {targetLabel ?? outcomeKey}</div>
          <div style={{ color: C.sub, fontFamily: mono, fontSize: 12 }}>
            grain={grain} · window={windowDays}d · n={readiness?.outcomeEvents ?? "—"} · heats={readiness?.independentHeats ?? "—"}
          </div>
        </div>
        {readiness && (
          <span style={{ background: readinessColor(readiness.overall), color: C.navy, fontWeight: 700, padding: "4px 12px", borderRadius: 999, fontSize: 12 }}>
            {readiness.overall.toUpperCase()}
          </span>
        )}
      </div>

      {readiness && !readiness.canRun && (
        <div style={{ ...card, borderColor: C.bad }}>
          <b style={{ color: C.bad }}>Analysis blocked by the data-readiness gate.</b>
          <ul style={{ margin: "8px 0 0", color: C.sub, fontSize: 13 }}>
            {readiness.dimensions.map(d => <li key={d.name}>{d.name}: {d.reason}</li>)}
          </ul>
        </div>
      )}

      {readiness?.canRun && contributors.length === 0 && (
        <div style={card}>No statistically supported contributors were found for this target and window.</div>
      )}

      {contributors.map((f, i) => {
        const e = Math.min(1, Math.abs(f.effectSize ?? 0));
        return (
          <div key={f.findingId + i} style={card}>
            <div style={{ display: "flex", justifyContent: "space-between" }}>
              <b>{f.findingId}</b>
              <span style={{ fontFamily: mono, fontSize: 12, color: C.cyan }}>{f.method}</span>
            </div>
            <div style={{ height: 8, background: C.line, borderRadius: 4, marginTop: 8 }}>
              <div style={{ width: `${e * 100}%`, height: 8, background: C.cyan, borderRadius: 4 }} />
            </div>
            <div style={{ display: "flex", gap: 18, marginTop: 8, fontFamily: mono, fontSize: 12, color: C.sub, flexWrap: "wrap" }}>
              <span>effect {num(f.effectSize)}</span>
              <span>q {num(f.qValue)}{f.significant ? " ✓sig" : ""}</span>
              <span>stability {pct(f.stabilityConsistency)} [{num(f.stabilityLower)}, {num(f.stabilityUpper)}]</span>
              <span>n {f.sampleSize}</span>
              <span style={{ color: f.survivesStratification ? C.good : C.warn }}>
                {f.survivesStratification ? "survives stratification" : "fails stratification"}
              </span>
            </div>
          </div>
        );
      })}

      {excluded.length > 0 && (
        <div style={{ ...card, borderColor: C.warn }}>
          <b style={{ color: C.warn }}>Excluded features</b>
          <ul style={{ margin: "8px 0 0", color: C.sub, fontSize: 13 }}>
            {excluded.map((f, i) => {
              let reason = ""; try { reason = JSON.parse(f.evidence ?? "{}").reason ?? ""; } catch { /* ignore */ }
              return <li key={f.findingId + i}>{f.findingId}{reason ? ` — ${reason}` : ""}</li>;
            })}
          </ul>
        </div>
      )}

      <div style={{ ...card, borderColor: C.cyan, background: "#06121F" }}>
        <b style={{ color: C.cyan }}>Honesty</b>
        <div style={{ color: C.text, marginTop: 6, fontSize: 13 }}>{caveat}</div>
      </div>
    </div>
  );
}
