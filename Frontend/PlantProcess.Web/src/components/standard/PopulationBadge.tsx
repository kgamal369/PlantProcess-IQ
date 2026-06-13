// P5-T04: population N is ALWAYS stated on analytics surfaces (A3). Renders "N = 1,234"; when the
// engine reports no/zero population it renders an explicit "N = 0" rather than a blank, so a reader
// can never mistake "nothing rendered" for "no relationship found".
import type { ReactElement } from "react";

export function PopulationBadge({
  n,
  label = "N",
}: {
  n: number | null | undefined;
  label?: string;
}): ReactElement {
  const value = typeof n === "number" && Number.isFinite(n) ? n : 0;
  return (
    <span
      className="ppiq-population-badge"
      data-testid="population-badge"
      title={`Population: ${value.toLocaleString()} observations`}
    >
      {label} = {value.toLocaleString()}
    </span>
  );
}
