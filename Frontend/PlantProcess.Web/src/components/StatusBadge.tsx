type StatusBadgeProps = {
  status?: string;
};

export function StatusBadge({ status }: StatusBadgeProps) {
  const normalized = (status || "Unknown").toLowerCase();

  const className =
    normalized.includes("healthy") ||
    normalized.includes("pass") ||
    normalized.includes("completed")
      ? "status-badge success"
      : normalized.includes("warning")
      ? "status-badge warning"
      : normalized.includes("fail") || normalized.includes("error")
      ? "status-badge danger"
      : "status-badge neutral";

  return <span className={className}>{status || "Unknown"}</span>;
}