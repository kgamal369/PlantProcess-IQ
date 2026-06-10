
import { useMemo, useState } from "react";
import {
  buildMonthlyValueReportHtml, computePayback, computeWorkedCasePreview, formatMoney, runEngineAbstainProof, runEngineWorkedCase, type P3T14ImpactResult, } from "../../api/p3T14ValueExecutive";
import "./value-executive.css";

import { StandardP2Button, StandardP2Input, StandardP2Table } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
export const P3_T14_VALUE_ROI_EXECUTIVE_SURFACE = "P3_T14_VALUE_ROI_EXECUTIVE_SURFACE";

function openReport(html: string) {
  const win = window.open("", "_blank", "noopener,noreferrer,width=1100,height=800");
  if (!win) {
    throw new Error("Popup blocked. Allow popups to open the monthly value report.");
  }

  win.document.open();
  win.document.write(html);
  win.document.close();
  win.focus();
  setTimeout(() => win.print(), 250);
}

export function ValueExecutiveDashboardPage() {
  const [impact, setImpact] = useState<P3T14ImpactResult | null>(null);
  const [abstain, setAbstain] = useState<P3T14ImpactResult | null>(null);
  const [monthlyLicenseCost, setMonthlyLicenseCost] = useState(12000);
  const [status, setStatus] = useState("Ready to call the deterministic value engine.");
  const [error, setError] = useState<string | null>(null);
  const [isRunning, setIsRunning] = useState(false);

  const preview = useMemo(() => computeWorkedCasePreview(), []);
  const payback = useMemo(() => computePayback(impact, monthlyLicenseCost), [impact, monthlyLicenseCost]);

  async function runDashboard() {
    setIsRunning(true);
    setError(null);
    setAbstain(null);
    setStatus("Calling value engine with approved finding and versioned cost assumptions...");

    try {
      const result = await runEngineWorkedCase();
      setImpact(result);
      setStatus("Engine result loaded. Low/Mid/High values below are rendered from /api/value/impact.");
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
      setStatus("Could not compute value impact.");
    } finally {
      setIsRunning(false);
    }
  }

  async function runAbstain() {
    setIsRunning(true);
    setError(null);
    setStatus("Calling value engine with missing assumptions to prove ABSTAIN behavior...");

    try {
      const result = await runEngineAbstainProof();
      setAbstain(result);
      setStatus("ABSTAIN proof complete. No fabricated money value should appear for the missing-assumption case.");
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
      setStatus("Could not run abstain proof.");
    } finally {
      setIsRunning(false);
    }
  }

  function printReport() {
    const html = buildMonthlyValueReportHtml(impact, monthlyLicenseCost);
    openReport(html);
  }

  const currency = impact?.currency ?? "EUR";

  return (
    <main className="value-exec-page" data-p3-task="P3-T14" data-testid="p3-t14-value-executive-dashboard">
      <section className="value-exec-hero">
        <div className="value-exec-kicker">P3-T14 · Value/ROI executive surface</div>
        <h1>Executive Value Dashboard</h1>
        <p className="value-exec-muted">
          Calls the deterministic value engine, renders bounded Low/Mid/High impact, exposes every input and
          provenance handle, and refuses to show money when assumptions are missing.
        </p>

        <div className="value-exec-actions">
          <StandardButton className="value-exec-button" type="button" onClick={runDashboard} isDisabled={isRunning}>
            {isRunning ? "Running..." : "Run approved finding through value engine"}
          </StandardButton>
          <StandardButton className="value-exec-button warning" type="button" onClick={runAbstain} isDisabled={isRunning}>
            Prove ABSTAIN on missing assumptions
          </StandardButton>
          <StandardButton className="value-exec-button secondary" type="button" onClick={printReport} isDisabled={!impact || impact.isAbstained}>
            Open monthly value report PDF
          </StandardButton>
        </div>

        <p className="value-exec-muted" role="status">{status}</p>
        {error ? <p className="value-exec-abstain" role="alert">Controlled error: {error}</p> : null}
      </section>

      <section className="value-exec-panel">
        <h2>Doctrine worked-case preflight</h2>
        <p className="value-exec-muted">
          Local arithmetic preview only: {preview.formula}. The executive cards are not accepted until the API engine returns the same bounded range.
        </p>
        <div className="value-exec-grid">
          <div className="value-exec-card"><span>Preview Low</span><strong>{formatMoney(preview.low, preview.currency)}</strong></div>
          <div className="value-exec-card"><span>Preview Mid</span><strong>{formatMoney(preview.mid, preview.currency)}</strong></div>
          <div className="value-exec-card"><span>Preview High</span><strong>{formatMoney(preview.high, preview.currency)}</strong></div>
        </div>
      </section>

      <section className="value-exec-panel">
        <h2>Engine output</h2>

        {impact && !impact.isAbstained ? (
          <>
            <div className="value-exec-grid">
              <div className="value-exec-card" data-testid="p3-t14-low"><span>Low</span><strong>{formatMoney(impact.low, currency)}</strong></div>
              <div className="value-exec-card" data-testid="p3-t14-mid"><span>Mid</span><strong>{formatMoney(impact.mid, currency)}</strong></div>
              <div className="value-exec-card" data-testid="p3-t14-high"><span>High</span><strong>{formatMoney(impact.high, currency)}</strong></div>
            </div>

            <p className="value-exec-muted">
              Assumption version {impact.assumptionVersion}. Support status: {impact.supportStatus}. {impact.honestyCaveat}
            </p>
          </>
        ) : (
          <p className="value-exec-muted">No engine result yet. Run the approved finding to render the bounded range.</p>
        )}

        {impact?.isAbstained ? (
          <div className="value-exec-abstain" data-testid="p3-t14-abstain">
            ABSTAIN — {impact.abstainReason ?? "insufficient basis"}. No fabricated money number is displayed.
          </div>
        ) : null}
      </section>

      <section className="value-exec-panel">
        <h2>Payback view vs license cost</h2>
        <div className="value-exec-license">
          <label htmlFor="p3t14-license-cost">Monthly license cost</label>
          <StandardP2Input
            id="p3t14-license-cost"
            type="number"
            min="1"
            value={monthlyLicenseCost}
            onChange={(event) => setMonthlyLicenseCost(Number(event.target.value))}
          />
          <strong>{formatMoney(monthlyLicenseCost, currency)}</strong>
        </div>

        <div className="value-exec-grid">
          <div className="value-exec-card"><span>Low multiple</span><strong>{payback.lowMultiple.toFixed(2)}×</strong></div>
          <div className="value-exec-card"><span>Mid multiple</span><strong>{payback.midMultiple.toFixed(2)}×</strong></div>
          <div className="value-exec-card"><span>High multiple</span><strong>{payback.highMultiple.toFixed(2)}×</strong></div>
        </div>
      </section>

      <section className="value-exec-panel">
        <h2>Input drill-through and provenance</h2>
        <p className="value-exec-muted">
          Every value term below comes from the engine response and carries its own provenance handle.
        </p>

        <StandardP2Table className="value-exec-table">
          <thead>
            <tr>
              <th>Term</th>
              <th>Low</th>
              <th>Mid</th>
              <th>High</th>
              <th>Input drill-through</th>
              <th>Provenance handle</th>
            </tr>
          </thead>
          <tbody>
            {impact && !impact.isAbstained && impact.terms.length > 0 ? (
              impact.terms.map((term) => (
                <tr key={term.name + term.handle}>
                  <td>{term.name}</td>
                  <td>{formatMoney(term.low, currency)}</td>
                  <td>{formatMoney(term.mid, currency)}</td>
                  <td>{formatMoney(term.high, currency)}</td>
                  <td>
                    <details>
                      <summary>Show inputs</summary>
                      <code className="value-exec-code">{term.inputsJson}</code>
                    </details>
                  </td>
                  <td><code className="value-exec-code">{term.handle}</code></td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={6}>No value terms rendered yet, or the engine abstained.</td>
              </tr>
            )}
          </tbody>
        </StandardP2Table>
      </section>

      {abstain ? (
        <section className="value-exec-panel">
          <h2>Missing-assumption proof</h2>
          <div className="value-exec-abstain" data-testid="p3-t14-abstain-proof">
            {abstain.isAbstained
              ? "ABSTAIN — " + (abstain.abstainReason ?? "insufficient basis") + ". No fabricated money number is displayed."
              : "Unexpected: engine returned a value even though assumptions were deliberately removed."}
          </div>
        </section>
      ) : null}
    </main>
  );
}

export default ValueExecutiveDashboardPage;
