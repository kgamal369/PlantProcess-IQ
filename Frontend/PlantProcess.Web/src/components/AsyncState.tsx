import { AlertTriangle, Loader2 } from "lucide-react";

export function LoadingPanel({ text = "Loading dashboard..." }: { text?: string }) {
  return (
    <div className="state-panel">
      <Loader2 className="spin" size={24} />
      <strong>{text}</strong>
      <span>Aggregating dashboard-ready data from the backend.</span>
    </div>
  );
}

export function ErrorPanel({
  title = "Could not load data",
  error,
}: {
  title?: string;
  error: unknown;
}) {
  return (
    <div className="state-panel state-panel--error">
      <AlertTriangle size={24} />
      <strong>{title}</strong>
      <span>{error instanceof Error ? error.message : String(error)}</span>
    </div>
  );
}