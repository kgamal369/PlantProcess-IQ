import { SearchX } from "lucide-react";

export function EmptyInsightState({
  title = "No data for this selection",
  message = "Try clearing one or more filters, changing the time range, or selecting another source system.",
}: {
  title?: string;
  message?: string;
}) {
  return (
    <div className="empty-insight">
      <SearchX size={26} />
      <strong>{title}</strong>
      <p>{message}</p>
    </div>
  );
}