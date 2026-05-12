import { X } from "lucide-react";
import { useDashboardSelections } from "../../state/DashboardSelectionContext";

export function DrilldownDrawer() {
  const { drilldown, closeDrilldown } = useDashboardSelections();

  if (!drilldown.isOpen) {
    return null;
  }

  return (
    <aside className="drilldown-drawer">
      <div className="drilldown-drawer__header">
        <div>
          <span>{drilldown.type}</span>
          <h3>{drilldown.title}</h3>
          {drilldown.subtitle ? <p>{drilldown.subtitle}</p> : null}
        </div>

        <button className="icon-button" onClick={closeDrilldown} type="button">
          <X size={18} />
        </button>
      </div>

      <div className="drilldown-drawer__body">
        <PrettyObject value={drilldown.payload} />
      </div>
    </aside>
  );
}

function PrettyObject({ value }: { value: unknown }) {
  if (value === null || value === undefined) {
    return <p>No drilldown data available.</p>;
  }

  if (typeof value !== "object") {
    return <p>{String(value)}</p>;
  }

  return (
    <div className="detail-list">
      {Object.entries(value as Record<string, unknown>).map(([key, item]) => (
        <div key={key} className="detail-row">
          <span>{formatKey(key)}</span>
          <strong>{formatValue(item)}</strong>
        </div>
      ))}
    </div>
  );
}

function formatKey(key: string) {
  return key
    .replace(/([A-Z])/g, " $1")
    .replace(/^./, (value) => value.toUpperCase());
}

function formatValue(value: unknown) {
  if (value === null || value === undefined) return "-";
  if (typeof value === "object") return JSON.stringify(value);
  return String(value);
}