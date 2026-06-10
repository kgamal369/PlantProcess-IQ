import { DataFetchBoundary } from "@/components/standard/DataFetchBoundary";
import { useApiResource } from "@/hooks/useApiResource";
import { AdvancedResultPanel, type AdvancedResult } from "./AdvancedResultPanel";

import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
type ResultsResponse = { runId: string | null; honestyCaveat: string; results: AdvancedResult[] };

export function AdvancedResultsList(props: { outcomeKey?: string }) {
  const qs = props.outcomeKey ? `?outcomeKey=${encodeURIComponent(props.outcomeKey)}` : "";
  const r = useApiResource<ResultsResponse>(`/api/analytics/advanced/results${qs}`);
  const results = r.data?.results ?? [];
  return (
    <DataFetchBoundary
      title="Advanced analysis results"
      isLoading={r.isLoading}
      error={r.error}
      isEmpty={results.length === 0}
      emptyMessage="No managed correlation run yet. Run a correlation with the managed engine to populate findings."
      onRetry={r.reload}
    >
      <div>
        {results.map((res, i) => <AdvancedResultPanel key={res.findingId ?? i} result={res} />)}
      </div>
    </DataFetchBoundary>
  );
}