
import { useEffect, useState } from "react";
import { assistantApi, type AssistantConfiguration } from "@/api/assistantApi";
import "./phase8-ai.css";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Input, StandardP2Select } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
const defaultConfig: AssistantConfiguration = {
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
  const [config, setConfig] = useState<AssistantConfiguration>(defaultConfig);
  const [status, setStatus] = useState("Loading assistant configuration...");
  const [findings, setFindings] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let active = true;

    assistantApi.getAssistantConfig()
      .then((next) => {
        if (!active) return;
        setConfig(next);
        setStatus("Assistant configuration loaded.");
      })
      .catch((error: Error) => {
        if (!active) return;
        setStatus("Using safe local defaults; configuration unavailable: " + error.message);
      });

    return () => {
      active = false;
    };
  }, []);

  async function save() {
    setBusy(true);
    setFindings([]);
    try {
      const result = await assistantApi.saveAssistantConfig({
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
      const result = await assistantApi.resetAssistantConfig();
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
        <p className="phase8-eyebrow">Grounding, evidence and egress controls for the assistant.</p>
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
            <StandardP2Select className="phase8-select" value={config.mode} onChange={(event) => setConfig({ ...config, mode: event.target.value })}>
              <option value="grounded-extractive">grounded-extractive</option>
              <option value="private-model">private-model</option>
              <option value="self-hosted">self-hosted</option>
            </StandardP2Select>
          </label>

          <label>
            Grounding policy
            <StandardP2Select className="phase8-select" value={config.groundingPolicy} onChange={(event) => setConfig({ ...config, groundingPolicy: event.target.value })}>
              <option value="strict-citations-required">strict-citations-required</option>
              <option value="abstain-on-missing-evidence">abstain-on-missing-evidence</option>
              <option value="demo-extractive-only">extractive-only</option>
            </StandardP2Select>
          </label>

          <label>
            Evidence policy
            <StandardP2Select className="phase8-select" value={config.evidencePolicy} onChange={(event) => setConfig({ ...config, evidencePolicy: event.target.value })}>
              <option value="citations-and-provenance-required">citations-and-provenance-required</option>
              <option value="citations-required">citations-required</option>
              <option value="provenance-required">provenance-required</option>
            </StandardP2Select>
          </label>

          <label>
            Max citations
            <StandardP2Input className="phase8-input" type="number" min="1" max="12" value={config.maxCitations} onChange={(event) => setConfig({ ...config, maxCitations: Number(event.target.value) })} />
          </label>
        </div>

        <div className="phase8-card">
          <h2>Safety switches</h2>

          <label>
            <StandardP2Input type="checkbox" checked={config.noEgress} onChange={(event) => setConfig({ ...config, noEgress: event.target.checked })} />
            No egress
          </label>

          <label>
            <StandardP2Input type="checkbox" checked={config.requireHumanApprovalForRecommendations} onChange={(event) => setConfig({ ...config, requireHumanApprovalForRecommendations: event.target.checked })} />
            Require human approval for recommendations
          </label>

          <label>
            <StandardP2Input type="checkbox" checked={config.enableSuggestionWorkflow} onChange={(event) => setConfig({ ...config, enableSuggestionWorkflow: event.target.checked })} />
            Enable suggestion workflow
          </label>

          <h3>Allowed tools</h3>
          <div>
            {toolOptions.map((tool) => (
              <label key={tool}>
                <StandardP2Input type="checkbox" checked={config.allowedTools.includes(tool)} onChange={() => toggleTool(tool)} />
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
        <div>
          <StandardButton className="phase8-button" type="button" isDisabled={busy} onClick={() => void save()}>Save configuration</StandardButton>
          <StandardButton className="phase8-button" type="button" isDisabled={busy} onClick={() => void reset()}>Reset safe defaults</StandardButton>
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
